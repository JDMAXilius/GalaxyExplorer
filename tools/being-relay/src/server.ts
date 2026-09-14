import Anthropic from "@anthropic-ai/sdk";
import OpenAI from "openai";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { WebSocketServer, type RawData } from "ws";
import { knowledge } from "./knowledge.js";
import { Session } from "./session.js";
import type { ClientMessage } from "./protocol.js";

const here = dirname(fileURLToPath(import.meta.url));
const dataDir = resolve(here, "..", process.env.DATA_DIR ?? "../../Assets/data");
const system = `${readFileSync(join(here, "..", "prompt", "system.md"), "utf8")}\n\n${knowledge(dataDir)}`;
const claude = new Anthropic();
const openai = new OpenAI();
const port = Number(process.env.PORT ?? 8787);

new WebSocketServer({ port, path: "/being" }).on("connection", (socket) => {
  const session = new Session(socket, claude, openai, system, Number(process.env.MAX_TURNS ?? 40));
  let expecting: { bytes: number; context: ClientMessage["context"] } | null = null;

  socket.on("message", (data: RawData, isBinary: boolean) => {
    const buffer = Buffer.isBuffer(data) ? data : Buffer.concat(Array.isArray(data) ? data : [Buffer.from(data)]);
    if (isBinary) {
      if (expecting) void session.voice(buffer, expecting.context!);
      expecting = null;
      return;
    }

    const message = JSON.parse(buffer.toString("utf8")) as ClientMessage;
    switch (message.type) {
      case "hello":
        void session.hello(message.context!);
        break;
      case "turn":
        if (message.audioBytes) expecting = { bytes: message.audioBytes, context: message.context };
        else void session.ask(message.text ?? "", message.context);
        break;
      case "interrupt":
        session.interrupt();
        break;
    }
  });

  socket.on("close", () => session.interrupt());
});

console.log(`being-relay listening on ws://0.0.0.0:${port}/being`);
