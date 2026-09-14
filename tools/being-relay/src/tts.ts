import OpenAI from "openai";

export async function speak(client: OpenAI, text: string, signal: AbortSignal, onChunk: (pcm: Buffer) => void): Promise<void> {
  const response = await client.audio.speech.create(
    {
      model: process.env.OPENAI_TTS_MODEL ?? "gpt-4o-mini-tts",
      voice: (process.env.OPENAI_VOICE ?? "coral") as "coral",
      input: text,
      response_format: "pcm",
      instructions: "A calm, warm, wondrous guide to the universe. Unhurried, clear, never theatrical.",
    },
    { signal },
  );
  for await (const chunk of response.body as AsyncIterable<Uint8Array>) {
    if (signal.aborted) return;
    onChunk(Buffer.from(chunk));
  }
}
