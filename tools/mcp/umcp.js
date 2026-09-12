#!/usr/bin/env node
// umcp.js - a dependency-free MCP stdio client for the Unity relay.
//
//   node umcp.js list
//   node umcp.js call <tool> '<json>'
//   node umcp.js run  <file.cs>
//
// It spawns `relay_win.exe --mcp --project-path <repo>`, speaks JSON-RPC 2.0 over the child's
// stdio, and prints the text content of the reply. Node built-ins only, so there is nothing to
// install and nothing to keep up to date.
//
// Two things here are deliberately defensive rather than merely cautious, because the relay's
// exact dialect is not documented anywhere we control:
//
//   * Framing. The MCP stdio transport in the wild is newline-delimited JSON, but this relay was
//     written against Content-Length headers (LSP style). Sending the wrong one does not produce
//     an error - it produces a hang, because each side sits waiting for a frame the other will
//     never finish. So the reader accepts both unconditionally, and the writer negotiates: it
//     tries Content-Length, and if the handshake goes unanswered it respawns the relay and tries
//     newline framing. `--framing=lsp|ndjson` skips the negotiation once you know.
//
//   * The argument name for Unity_RunCommand. Rather than guessing `code` and failing with a
//     schema error, `run` reads the tool's own inputSchema from tools/list and picks the string
//     property that carries the source.

'use strict';

const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

// ---------------------------------------------------------------- configuration

const REPO_ROOT = path.resolve(__dirname, '..', '..');

const DEFAULTS = {
  relay: process.env.UNITY_RELAY ||
    path.join(os.homedir(), '.unity', 'relay', 'relay_win.exe'),
  project: process.env.UNITY_PROJECT || REPO_ROOT,
  framing: process.env.UMCP_FRAMING || 'auto',   // auto | lsp | ndjson
  timeout: Number(process.env.UMCP_TIMEOUT || 300),  // seconds, per tool call
  handshake: 8,          // seconds to wait for an initialize reply before trying the other framing
  retries: 6,            // "Unity not detected" retries - that message means a domain reload
  retryDelay: 5,         // seconds between those retries
  verbose: false,
  raw: false,
};

// The protocol version we ask for. A server that speaks another one answers with its own in the
// initialize result; we do not enforce a match, because refusing to talk over a version skew would
// be a worse failure than talking.
const PROTOCOL_VERSION = '2025-06-18';
const FALLBACK_PROTOCOL_VERSION = '2024-11-05';

const RUN_TOOL = 'Unity_RunCommand';

// Candidate names for the "here is the C# source" argument, best guess first.
const SOURCE_ARG_CANDIDATES = [
  'code', 'script', 'source', 'csharp', 'cs', 'command', 'commandText', 'text', 'content', 'body',
];

// ---------------------------------------------------------------- tiny helpers

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function die(message, code = 2) {
  // writeSync, not process.stderr.write: stderr to a pipe is asynchronous, and process.exit does
  // not wait for it, so the message that explains the failure is exactly the one that gets lost.
  try {
    fs.writeSync(2, `umcp: ${message}\n`);
  } catch (e) {
    process.stderr.write(`umcp: ${message}\n`);
  }
  process.exit(code);
}

function note(opts, message) {
  if (opts.verbose) process.stderr.write(`umcp: ${message}\n`);
}

function usage() {
  return [
    'usage:',
    '  node umcp.js list',
    '  node umcp.js call <tool> \'<json arguments>\'',
    '  node umcp.js run  <file.cs>            (defaults to the Unity_RunCommand tool)',
    '',
    'options:',
    '  --relay=<path>       relay executable            (env UNITY_RELAY)',
    '  --project=<path>     --project-path handed to it (env UNITY_PROJECT, default: this repo)',
    '  --framing=auto|lsp|ndjson                        (env UMCP_FRAMING, default auto)',
    '  --timeout=<seconds>  per call, default 300       (env UMCP_TIMEOUT)',
    '  --retries=<n>        retries on "Unity not detected", default 6',
    '  --retry-delay=<sec>  between those retries, default 5',
    '  --tool=<name>        override the tool `run` uses',
    '  --arg k=v            extra argument for `run` (repeatable)',
    '  --raw                print the whole JSON-RPC result, not just its text content',
    '  --verbose            protocol chatter and relay stderr on stderr',
    '',
    'exit codes: 0 ok, 1 the tool reported an error, 2 usage or transport failure.',
  ].join('\n');
}

// ---------------------------------------------------------------- argument parsing

function parseArgs(argv) {
  const opts = Object.assign({}, DEFAULTS, { extraArgs: {}, tool: null });
  const rest = [];

  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--arg') {
      const kv = argv[++i] || '';
      const eq = kv.indexOf('=');
      if (eq < 0) die(`--arg wants key=value, got "${kv}"`);
      opts.extraArgs[kv.slice(0, eq)] = kv.slice(eq + 1);
    } else if (a === '--raw') {
      opts.raw = true;
    } else if (a === '--verbose' || a === '-v') {
      opts.verbose = true;
    } else if (a === '--help' || a === '-h') {
      process.stdout.write(usage() + '\n');
      process.exit(0);
    } else if (a.startsWith('--')) {
      const eq = a.indexOf('=');
      const key = (eq < 0 ? a.slice(2) : a.slice(2, eq));
      const value = eq < 0 ? argv[++i] : a.slice(eq + 1);
      switch (key) {
        case 'relay': opts.relay = value; break;
        case 'project': opts.project = value; break;
        case 'framing': opts.framing = value; break;
        case 'timeout': opts.timeout = Number(value); break;
        case 'handshake': opts.handshake = Number(value); break;
        case 'retries': opts.retries = Number(value); break;
        case 'retry-delay': opts.retryDelay = Number(value); break;
        case 'tool': opts.tool = value; break;
        default: die(`unknown option --${key}`);
      }
    } else {
      rest.push(a);
    }
  }

  if (!['auto', 'lsp', 'ndjson'].includes(opts.framing)) {
    die(`--framing must be auto, lsp or ndjson (got "${opts.framing}")`);
  }
  return { opts, rest };
}

// ---------------------------------------------------------------- the connection

class Connection {
  constructor(opts, framing) {
    this.opts = opts;
    this.framing = framing;          // what we *send*; the reader always accepts both
    this.buf = Buffer.alloc(0);
    this.pending = new Map();        // id -> {resolve, reject, timer}
    this.nextId = 1;
    this.closed = false;
    this.exitInfo = null;
    this.stderr = '';
  }

  spawn() {
    if (!fs.existsSync(this.opts.relay)) {
      die(`relay not found at ${this.opts.relay}\n` +
        '      Set --relay=<path> or the UNITY_RELAY environment variable.\n' +
        '      On Windows it is usually %USERPROFILE%\\.unity\\relay\\relay_win.exe');
    }

    note(this.opts, `spawning ${this.opts.relay} --mcp --project-path ${this.opts.project}`);
    this.child = spawn(this.opts.relay, ['--mcp', '--project-path', this.opts.project], {
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true,
    });

    this.child.on('error', (e) => die(`could not start the relay: ${e.message}`));
    this.child.stdout.on('data', (c) => this.feed(c));
    this.child.stderr.on('data', (c) => {
      const s = c.toString('utf8');
      this.stderr += s;
      note(this.opts, `relay stderr: ${s.trimEnd()}`);
    });
    this.child.on('exit', (code, signal) => {
      this.closed = true;
      this.exitInfo = { code, signal };
      for (const [, p] of this.pending) {
        clearTimeout(p.timer);
        p.reject(new Error(`the relay exited (code ${code}, signal ${signal}) before replying`));
      }
      this.pending.clear();
    });
  }

  // ---- reading: accepts Content-Length frames and newline-delimited JSON, interleaved.

  feed(chunk) {
    this.buf = Buffer.concat([this.buf, chunk]);

    for (;;) {
      // Leading blank lines belong to neither framing; drop them so one stray \r\n cannot
      // make every following frame look malformed.
      let s = 0;
      while (s < this.buf.length && (this.buf[s] === 0x0a || this.buf[s] === 0x0d)) s++;
      if (s > 0) this.buf = this.buf.slice(s);
      if (this.buf.length === 0) return;

      const peek = this.buf.slice(0, Math.min(96, this.buf.length)).toString('latin1');
      if (/^content-length\s*:/i.test(peek)) {
        const sep = findHeaderEnd(this.buf);
        if (!sep) return;                                  // header incomplete, wait for more
        const header = this.buf.slice(0, sep.index).toString('latin1');
        const m = /content-length\s*:\s*(\d+)/i.exec(header);
        if (!m) { this.buf = this.buf.slice(sep.index + sep.length); continue; }
        const len = parseInt(m[1], 10);
        const start = sep.index + sep.length;
        if (this.buf.length < start + len) return;         // body incomplete, wait for more
        const body = this.buf.slice(start, start + len).toString('utf8');
        this.buf = this.buf.slice(start + len);
        this.observed = 'lsp';
        this.dispatch(body);
        continue;
      }

      const nl = this.buf.indexOf(0x0a);
      if (nl < 0) return;
      const line = this.buf.slice(0, nl).toString('utf8').trim();
      this.buf = this.buf.slice(nl + 1);
      if (line.length === 0) continue;
      this.observed = 'ndjson';
      this.dispatch(line);
    }
  }

  dispatch(text) {
    let msg;
    try {
      msg = JSON.parse(text);
    } catch (e) {
      // Relays print banners and log lines on stdout too. Not fatal: it just is not a message.
      note(this.opts, `non-JSON on stdout: ${text.slice(0, 200)}`);
      return;
    }
    if (msg.id === undefined || msg.id === null) {
      note(this.opts, `notification: ${msg.method || '(none)'}`);
      return;
    }
    const waiter = this.pending.get(msg.id);
    if (!waiter) {
      note(this.opts, `reply for unknown id ${msg.id}`);
      return;
    }
    this.pending.delete(msg.id);
    clearTimeout(waiter.timer);
    if (msg.error) {
      waiter.reject(new Error(
        `${msg.error.message || 'error'} (code ${msg.error.code})` +
        (msg.error.data ? ` ${JSON.stringify(msg.error.data)}` : '')));
    } else {
      waiter.resolve(msg.result);
    }
  }

  // ---- writing

  write(obj) {
    if (this.closed) throw new Error('the relay connection is closed');
    const json = JSON.stringify(obj);
    if (this.framing === 'lsp') {
      const body = Buffer.from(json, 'utf8');
      this.child.stdin.write(`Content-Length: ${body.length}\r\n\r\n`);
      this.child.stdin.write(body);
    } else {
      this.child.stdin.write(json + '\n');
    }
    note(this.opts, `-> ${json.slice(0, 300)}`);
  }

  notify(method, params) {
    this.write({ jsonrpc: '2.0', method, params: params || {} });
  }

  request(method, params, timeoutSeconds) {
    const id = this.nextId++;
    const seconds = timeoutSeconds === undefined ? this.opts.timeout : timeoutSeconds;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new TimeoutError(`no reply to ${method} within ${seconds} s`));
      }, Math.max(1, seconds) * 1000);
      this.pending.set(id, { resolve, reject, timer });
      try {
        this.write({ jsonrpc: '2.0', id, method, params: params || {} });
      } catch (e) {
        clearTimeout(timer);
        this.pending.delete(id);
        reject(e);
      }
    });
  }

  async initialize() {
    let result;
    try {
      result = await this.request('initialize', {
        protocolVersion: PROTOCOL_VERSION,
        capabilities: {},
        clientInfo: { name: 'umcp', version: '1.0.0' },
      }, this.opts.handshake);
    } catch (e) {
      if (e instanceof TimeoutError) throw e;
      // A server that rejects the version outright gets one more chance on the older one.
      note(this.opts, `initialize refused (${e.message}); retrying on ${FALLBACK_PROTOCOL_VERSION}`);
      result = await this.request('initialize', {
        protocolVersion: FALLBACK_PROTOCOL_VERSION,
        capabilities: {},
        clientInfo: { name: 'umcp', version: '1.0.0' },
      }, this.opts.handshake);
    }

    // Required by the spec and load-bearing in practice: several servers queue every tools/* call
    // until they have seen it.
    this.notify('notifications/initialized', {});
    this.serverInfo = result && result.serverInfo;
    return result;
  }

  async listTools() {
    const tools = [];
    let cursor;
    do {
      const page = await this.request('tools/list', cursor ? { cursor } : {});
      for (const t of (page && page.tools) || []) tools.push(t);
      cursor = page && page.nextCursor;
    } while (cursor);
    return tools;
  }

  callTool(name, args) {
    return this.request('tools/call', { name, arguments: args || {} });
  }

  close() {
    if (!this.child) return;
    const child = this.child;
    try { child.stdin.end(); } catch (e) { /* already gone */ }
    // Closing stdin should be enough, but an unref'd timer makes sure we never leave a relay
    // running against the project - a second one would deadlock the next call.
    setTimeout(() => { try { child.kill(); } catch (e) { /* already gone */ } }, 1200).unref();
  }
}

class TimeoutError extends Error {}

function findHeaderEnd(buf) {
  const crlf = buf.indexOf('\r\n\r\n', 0, 'latin1');
  const lf = buf.indexOf('\n\n', 0, 'latin1');
  if (crlf >= 0 && (lf < 0 || crlf <= lf)) return { index: crlf, length: 4 };
  if (lf >= 0) return { index: lf, length: 2 };
  return null;
}

// Opens a connection, negotiating the framing if it was left on auto. On a handshake timeout the
// relay is respawned rather than reused: a server that was mid-frame when we gave up would treat
// our next bytes as the tail of the frame it is still waiting for.
async function connect(opts) {
  const order = opts.framing === 'auto' ? ['lsp', 'ndjson'] : [opts.framing];
  let lastError;

  for (const framing of order) {
    const conn = new Connection(opts, framing);
    conn.spawn();
    try {
      const info = await conn.initialize();
      note(opts, `handshake ok with ${framing} framing` +
        (info && info.serverInfo ? ` (${info.serverInfo.name} ${info.serverInfo.version})` : ''));
      if (conn.observed && conn.observed !== framing) {
        // It answered in the other framing. Follow it: the reply is the only direct evidence.
        note(opts, `server replied in ${conn.observed}; switching our writes to match`);
        conn.framing = conn.observed;
      }
      return conn;
    } catch (e) {
      lastError = e;
      note(opts, `handshake failed with ${framing} framing: ${e.message}`);
      conn.close();
      await sleep(250);
    }
  }

  die('the relay never answered initialize.\n' +
    `      last error: ${lastError ? lastError.message : 'unknown'}\n` +
    '      Is the relay already running against this project? Only one client at a time.\n' +
    '      Try --framing=ndjson, or --verbose to see what it is sending.');
}

// ---------------------------------------------------------------- result rendering

function textOf(result) {
  if (!result) return '';
  const parts = [];
  for (const item of result.content || []) {
    if (item && typeof item.text === 'string') parts.push(item.text);
    else if (item) parts.push(JSON.stringify(item));
  }
  if (parts.length === 0 && result.structuredContent) {
    parts.push(JSON.stringify(result.structuredContent, null, 2));
  }
  return parts.join('\n');
}

function looksLikeDomainReload(text) {
  // The relay says "Unity not detected" while the editor is reloading its domain. It is not a
  // failure of the command, it is a failure to have anywhere to run it - the answer is to wait.
  return /unity\s+(is\s+)?not\s+(detected|connected|running|available)/i.test(text) ||
    /no\s+unity\s+(editor\s+)?(instance|connection)/i.test(text) ||
    /domain\s+reload/i.test(text);
}

// ---------------------------------------------------------------- commands

async function cmdList(conn, opts) {
  const tools = await conn.listTools();
  if (opts.raw) {
    process.stdout.write(JSON.stringify(tools, null, 2) + '\n');
    return 0;
  }
  if (tools.length === 0) {
    process.stdout.write('(the relay reported no tools)\n');
    return 0;
  }
  for (const t of tools) {
    const props = (t.inputSchema && t.inputSchema.properties) || {};
    const keys = Object.keys(props);
    process.stdout.write(`${t.name}\n`);
    if (t.description) process.stdout.write(`    ${t.description.split('\n')[0]}\n`);
    if (keys.length) process.stdout.write(`    args: ${keys.join(', ')}\n`);
  }
  return 0;
}

async function cmdCall(conn, opts, toolName, jsonText) {
  let args = {};
  if (jsonText !== undefined && jsonText !== '') {
    try {
      args = JSON.parse(jsonText);
    } catch (e) {
      die(`the arguments are not valid JSON: ${e.message}\n      got: ${jsonText}`);
    }
  }
  const result = await conn.callTool(toolName, args);
  return report(result, opts);
}

async function cmdRun(conn, opts, file) {
  if (!fs.existsSync(file)) die(`no such file: ${file}`);
  const source = fs.readFileSync(file, 'utf8');

  const toolName = opts.tool || RUN_TOOL;
  const tools = await conn.listTools();
  let tool = tools.find((t) => t.name === toolName);
  if (!tool) {
    tool = tools.find((t) => /run.?command/i.test(t.name));
    if (!tool) {
      die(`the relay has no ${toolName}. Tools it does have: ` +
        tools.map((t) => t.name).join(', '));
    }
    process.stderr.write(`umcp: ${toolName} not found, using ${tool.name}\n`);
  }

  const key = sourceArgumentName(tool, opts);
  const args = Object.assign({}, opts.extraArgs);
  args[key] = source;

  for (let attempt = 0; ; attempt++) {
    const result = await conn.callTool(tool.name, args);
    const text = textOf(result);
    if (looksLikeDomainReload(text) && attempt < opts.retries) {
      process.stderr.write(
        `umcp: the editor is reloading its domain; retrying in ${opts.retryDelay} s ` +
        `(${attempt + 1}/${opts.retries})\n`);
      await sleep(opts.retryDelay * 1000);
      continue;
    }
    return report(result, opts, true);
  }
}

// Which argument carries the C# source. Read from the tool's own schema rather than assumed,
// because the alternative is a schema error that reads like a broken script.
function sourceArgumentName(tool, opts) {
  const schema = tool.inputSchema || {};
  const props = schema.properties || {};
  const names = Object.keys(props);

  for (const candidate of SOURCE_ARG_CANDIDATES) {
    const hit = names.find((n) => n.toLowerCase() === candidate.toLowerCase());
    if (hit && isStringProp(props[hit])) return hit;
  }

  const required = (schema.required || []).filter((n) => isStringProp(props[n]));
  if (required.length === 1) return required[0];

  const strings = names.filter((n) => isStringProp(props[n]));
  if (strings.length === 1) return strings[0];

  process.stderr.write(
    `umcp: could not tell which argument of ${tool.name} takes the source ` +
    `(properties: ${names.join(', ') || 'none declared'}); using "code". ` +
    'Override with --tool and --arg if this is wrong.\n');
  note(opts, JSON.stringify(schema));
  return 'code';
}

function isStringProp(p) {
  if (!p) return false;
  if (p.type === 'string') return true;
  return Array.isArray(p.type) && p.type.includes('string');
}

function report(result, opts, isRun) {
  if (opts.raw) {
    process.stdout.write(JSON.stringify(result, null, 2) + '\n');
  } else {
    const text = textOf(result);
    process.stdout.write((text || '(no text content)') + '\n');
  }
  if (isRun) {
    process.stderr.write(
      'umcp: note - the relay reports NOT-OK whenever anything was logged as a warning, even for ' +
      'a command that did exactly what it was asked. Read the text above for the real verdict.\n');
  }
  return result && result.isError ? 1 : 0;
}

// ---------------------------------------------------------------- entry point

async function main() {
  const { opts, rest } = parseArgs(process.argv.slice(2));
  const command = rest[0];
  if (!command) { process.stdout.write(usage() + '\n'); process.exit(2); }

  const conn = await connect(opts);
  let code = 0;
  try {
    switch (command) {
      case 'list':
        code = await cmdList(conn, opts);
        break;
      case 'call':
        if (!rest[1]) die('call needs a tool name. `node umcp.js list` shows them.');
        code = await cmdCall(conn, opts, rest[1], rest[2]);
        break;
      case 'run':
        if (!rest[1]) die('run needs a .cs file.');
        code = await cmdRun(conn, opts, path.resolve(rest[1]));
        break;
      default:
        die(`unknown command "${command}"\n\n${usage()}`);
    }
  } catch (e) {
    conn.close();
    die(e instanceof TimeoutError
      ? `${e.message}\n      A long RunCommand may simply need --timeout=<seconds>.`
      : e.message);
  }
  conn.close();

  // exitCode rather than process.exit: stdout to a pipe is asynchronous, and exiting here would
  // truncate the very report the caller is reading. Node leaves once the write has drained and the
  // relay has gone.
  process.exitCode = code;
}

main();
