# The Cosmic Being

A small blue sphere of light, made of the intro ball's own point cloud, that keeps station beside the player and
answers spoken questions about the universe. One module, `Assets/Being/`, in its own assembly `Cosmic.Companion`,
talks to OpenAI's Realtime API over a single WebSocket: the microphone goes up, the transcript, the answer, the
voice and the tool calls come back down. There is no relay, no second service and no other model.

## Flow

1. The dock's being button (Quest: the small button under the dock; desktop: the last button on the HUD dock, or
   `C`) summons the sphere. It fades in beside the player, opens the socket, and greets on its own - spoken by the
   model, or from the stored clip when there is no key or no network.
2. It then listens for `listenWindowSeconds` (6 s). A chime marks the window opening. While it listens the sphere
   breathes, and your own voice pulls its points inward, so "it hears me" is something you can see.
3. Speech ends by the server's own voice detection; the sphere spins fast while it thinks and fills in as it talks.
   After an answer it listens again. With nothing said the window closes with a soft tick and it goes idle.
4. Tap the sphere (pinch, poke or click) while idle: it listens, silently. Tap while it is talking: it stops and
   listens. Press the dock button again to dismiss it.

`alwaysListening` on the settings asset keeps the microphone open between answers instead of closing the window;
it is still closed while the being speaks, so it never hears itself.

## Sound

The being is the one voice in the room while it is awake: music and ambience drop to 55 % and the narrator stops
(`IHost.Duck`). Its own voice plays through a 3D source that is at full volume within 1.5 m (`fullVolumeMetres`),
60 % spatialised, at `voiceGain` 1.4. The music beds sit at 70 % and narration at 80 % of their old levels.

## Pieces

| File | What |
|---|---|
| `Being.cs` | Phases Idle, Listening, Thinking, Speaking; the tap; the listen window; summon and dismiss |
| `Session.cs` | The Realtime protocol: `session.update` (instructions, voice, PCM formats, server VAD, tools), audio append and clear, `response.create` and `response.cancel`, events out as C# events |
| `Realtime.cs` | One `ClientWebSocket`: connect with the bearer key, serialised sends, a receive loop, main-thread inbox |
| `Mic.cs` | Armed while the being is out so listening starts on the next frame; 24 kHz PCM16 in 50 ms frames; level; the Android permission |
| `Voice.cs` | 24 kHz PCM16 into a streamed clip through a ring buffer; a quarter second of start buffer; the odd byte of a split sample carried, never dropped; loudness and four bands for the look |
| `Look.cs` | Phase, loudness, bands and the player's own mic level into the points' `_Density`, `_Inward`, `_Bands`, `_Active`, spin, and the rim's glow; the press pop; the reveal |
| `Follow.cs` | Keeps station in the head's yaw-and-pitch frame, faces the head, yields to a hand and re-reads its offset on release |
| `IHost.cs` | What the app provides: the situation (place, layout, what is held), the three actions, ducking, the tap click, whether a hand holds it |
| `BeingSettings.cs` | Every number, with its unit in its name |
| `BeingKeys.cs` | The OpenAI key; blank reads `OPENAI_API_KEY`. The asset is gitignored |
| `Editor/BeingPrefab.cs` | **Cosmic Simulation → Build Being**: knowledge, mesh, materials, cues, settings, prefab, wired into the old dock prefab |
| `Editor/Knowledge.cs` | **Cosmic Simulation → Build Being Knowledge**: every place and body asset into `Data/being_knowledge.txt` |
| `Data/being_prompt.txt` | The system instructions; the knowledge is appended at connect |
| `Shaders/Points.shader`, `Rim.shader` | The point cloud and the rim, stereo macros on, target 3.5 |
| `Assets/scripts/being/Host.cs`, `Assets/Cosmic/Being/Host.cs` | The two trees' halves: context, actions, ducking, grab. Each tree adds its own at summon; the being never references a tree |

## Protocol

Up: `session.update` once; `input_audio_buffer.append` (base64 PCM16 24 kHz) while listening; `input_audio_buffer.clear`
when a window closes unused; a `conversation.item.create` user message carrying the situation line before each turn;
`response.create` for typed questions and the greeting; `response.cancel` on interrupt; `function_call_output` after
a tool.

Down: `session.updated`; `input_audio_buffer.speech_started` / `speech_stopped`; `conversation.item.input_audio_transcription.completed`;
`response.output_audio.delta`; `response.output_audio_transcript.done`; `response.function_call_arguments.done`;
`response.done`; `error`.

## Running it

Paste the key into `Assets/Being/Data/being_keys.asset` (Inspector, *Open Ai Key*), or export `OPENAI_API_KEY`
on a dev machine. Build the being once from the menu; the dock prefab is wired by that build, the Cosmic scene by
**Cosmic → Build → Main Scene**. The APK needs the key in the asset; anyone with the APK can read it out, so a
shipping build wants a short-lived client secret from a small endpoint instead - not done.

## Decisions

- One socket for everything. Whisper, a chat model and a TTS voice in series made the tap-to-first-word wait the
  sum of three services; the Realtime API overlaps them and speaks in its own voice.
- The server decides when you stopped talking; the app decides only when the microphone is open.
- The microphone stays armed while the being is out and closes with it, so opening a window costs nothing, and the
  settings window's level meter yields to it.
- The microphone opens `echoTailSeconds` (0.6 s) after the being stops talking, and after the chime. Opened any
  sooner, speakers let it hear the end of its own sentence and answer itself - seen on the first online run.
- A cancelled answer is dropped by its response id: audio already in flight when you tap keeps arriving, and
  without that it resumed the interrupted answer.
- English only (`language` on the transcription, and in the prompt): a room with other voices otherwise gets
  answered in whatever language it hears.
- Half-duplex: the microphone is never open while the being speaks, so there is no echo to cancel.
- Tap is silent. The greeting happens once, on arrival.
- The knowledge is generated from the assets, not copied, so it cannot drift from the app.
