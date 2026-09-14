import Anthropic from "@anthropic-ai/sdk";
import OpenAI from "openai";
import type { WebSocket } from "ws";
import { answer } from "./claude.js";
import { speak } from "./tts.js";
import { transcribe } from "./stt.js";
import type { BeingContext, ServerMessage } from "./protocol.js";

export class Session {
  private readonly history: Anthropic.MessageParam[] = [];
  private context: BeingContext = { place: "", placeName: "", layout: "", pulled: [], platform: "desktop" };
  private turn = new AbortController();
  private turns = 0;

  constructor(
    private readonly socket: WebSocket,
    private readonly claude: Anthropic,
    private readonly openai: OpenAI,
    private readonly system: string,
    private readonly maxTurns: number,
  ) {}

  hello(context: BeingContext): Promise<void> {
    this.context = context;
    return this.ask("[The player has just summoned you. Greet them in two short sentences and invite a question.]");
  }

  async voice(pcm: Buffer, context: BeingContext): Promise<void> {
    this.context = context;
    const signal = this.restart();
    try {
      const text = await transcribe(this.openai, pcm, signal);
      if (!text) return this.send({ type: "done" });
      this.send({ type: "transcript", text });
      await this.run(text, signal);
    } catch (e) {
      this.fail(e, signal);
    }
  }

  async ask(text: string, context?: BeingContext): Promise<void> {
    if (context) this.context = context;
    const signal = this.restart();
    try {
      await this.run(text, signal);
    } catch (e) {
      this.fail(e, signal);
    }
  }

  interrupt(): void {
    this.turn.abort();
  }

  private async run(text: string, signal: AbortSignal): Promise<void> {
    if (++this.turns > this.maxTurns) return this.send({ type: "error", text: "The Cosmic Being has said enough for one visit." });
    this.history.push({ role: "user", content: text });
    let queue = Promise.resolve();
    await answer(this.claude, this.system, this.history, this.context, signal, {
      onSentence: (sentence) => {
        this.send({ type: "text", text: sentence });
        queue = queue.then(() => speak(this.openai, sentence, signal, (pcm) => this.socket.send(pcm)));
      },
      onAction: (name, id) => this.send({ type: "action", name, args: id }),
    });
    await queue;
    if (!signal.aborted) this.send({ type: "done" });
  }

  private restart(): AbortSignal {
    this.turn.abort();
    this.turn = new AbortController();
    return this.turn.signal;
  }

  private fail(e: unknown, signal: AbortSignal): void {
    if (signal.aborted) return;
    const text = e instanceof Error ? e.message : String(e);
    console.error("session:", text);
    this.send({ type: "error", text: "The Cosmic Being lost its train of thought. Try again." });
  }

  private send(message: ServerMessage): void {
    if (this.socket.readyState === this.socket.OPEN) this.socket.send(JSON.stringify(message));
  }
}
