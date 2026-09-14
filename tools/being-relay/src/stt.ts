import OpenAI, { toFile } from "openai";

const rate = 16000;

function wav(pcm: Buffer): Buffer {
  const header = Buffer.alloc(44);
  header.write("RIFF", 0);
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVEfmt ", 8);
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(rate, 24);
  header.writeUInt32LE(rate * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36);
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
}

export async function transcribe(client: OpenAI, pcm: Buffer, signal: AbortSignal): Promise<string> {
  const result = await client.audio.transcriptions.create(
    { file: await toFile(wav(pcm), "utterance.wav"), model: process.env.OPENAI_STT_MODEL ?? "whisper-1" },
    { signal },
  );
  return result.text.trim();
}
