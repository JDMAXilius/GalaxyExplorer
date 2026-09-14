import Anthropic from "@anthropic-ai/sdk";
import type { BeingContext } from "./protocol.js";

export const tools: Anthropic.Tool[] = [
  {
    name: "open_place",
    description: "Travel to a place in the simulation. Use the id from the knowledge list.",
    input_schema: { type: "object", properties: { id: { type: "string" } }, required: ["id"], additionalProperties: false },
    strict: true,
  },
  {
    name: "pull_body",
    description: "Pull a planet, the Sun or a moon out of the row in front of the player. Only in the Planets place.",
    input_schema: { type: "object", properties: { id: { type: "string" } }, required: ["id"], additionalProperties: false },
    strict: true,
  },
  {
    name: "restore",
    description: "Put every body back in its arrangement.",
    input_schema: { type: "object", properties: {}, additionalProperties: false },
    strict: true,
  },
];

export interface Reply {
  onSentence: (text: string) => void;
  onAction: (name: string, id: string) => void;
}

export async function answer(
  client: Anthropic,
  system: string,
  history: Anthropic.MessageParam[],
  context: BeingContext,
  signal: AbortSignal,
  reply: Reply,
): Promise<void> {
  const situated = [...history];
  const last = situated[situated.length - 1];
  situated[situated.length - 1] = { role: "user", content: `${describe(context)}\n\n${last.content as string}` };

  const stream = client.messages.stream(
    {
      model: process.env.CLAUDE_MODEL ?? "claude-opus-5",
      max_tokens: 1024,
      output_config: { effort: (process.env.CLAUDE_EFFORT ?? "low") as "low" },
      system: [{ type: "text", text: system, cache_control: { type: "ephemeral" } }],
      tools,
      messages: situated,
    },
    { signal },
  );

  let pending = "";
  stream.on("text", (delta) => {
    pending += delta;
    let cut: number;
    while ((cut = pending.search(/[.!?]\s/)) >= 0) {
      reply.onSentence(pending.slice(0, cut + 1).trim());
      pending = pending.slice(cut + 2);
    }
  });

  const message = await stream.finalMessage();
  if (pending.trim()) reply.onSentence(pending.trim());

  history.push({ role: "assistant", content: message.content });
  const uses = message.content.filter((b): b is Anthropic.ToolUseBlock => b.type === "tool_use");
  if (uses.length === 0) return;

  for (const use of uses) reply.onAction(use.name, String((use.input as { id?: string }).id ?? ""));
  history.push({
    role: "user",
    content: uses.map((use) => ({ type: "tool_result" as const, tool_use_id: use.id, content: "done" })),
  });
  await answer(client, system, history, context, signal, reply);
}

const describe = (c: BeingContext) =>
  `[Situation: the player is in "${c.placeName || c.place || "the intro"}"` +
  `${c.layout ? `, layout ${c.layout}` : ""}` +
  `${c.pulled?.length ? `, holding ${c.pulled.join(", ")}` : ""}` +
  `, on ${c.platform}.]`;
