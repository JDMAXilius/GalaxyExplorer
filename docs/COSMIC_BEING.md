# The Cosmic Being

A small blue sphere of light, the same point-cloud hologram the intro's Earth wears, that floats at the left of the
player's view and answers spoken questions about the universe. Claude is its mind; OpenAI is its ears and voice;
a small relay holds both keys so the app never does.

## Flow

1. The dock's being button (Quest: the small button under the dock; desktop: the last button on the HUD dock, or `C`)
   summons the sphere. It fades in beside the player, connects to the relay, and the relay speaks a greeting.
2. Tap the sphere (pinch, poke or click). It brightens and listens. Speak. When you stop, it goes quiet on its own
   after 1.2 s of silence and thinks.
3. The recording goes to the relay in one piece. Whisper transcribes it, Claude answers, each finished sentence is
   turned into speech and streamed back, and the sphere pulses to the voice.
4. Tap while it is speaking to interrupt. Press the dock button again to dismiss it.

The Claude API is text only: no speech in or out, and streaming is server-sent events over HTTP. That is why the
voice comes from OpenAI and why there is a relay. The relay is also the only place an API key lives; a key inside an
APK or a desktop build can be read out.

## Pieces

| Where | What |
|---|---|
| `Assets/scripts/being/CosmicBeing.cs` | Phases Idle, Listening, Thinking, Speaking; the tap; summon and dismiss |
| `BeingAnchor.cs` | Head-follow at the left, same flatten-and-drop maths as the hint cards |
| `BeingVisual.cs` | Phase and loudness into the hologram's `_Active`, `_Blend`, rotation speed and the rim's `_Multiplier` |
| `BeingMic.cs` | Record at 16 kHz until silence, PCM16 out; asks for the microphone on Android |
| `BeingSpeaker.cs` | 24 kHz PCM16 chunks into a streaming clip on the voice mixer; loudness out; honours narration mute |
| `BeingLink.cs` | One `ClientWebSocket` to the relay; text frames are JSON, binary frames are audio |
| `BeingContext.cs` | Snapshot sent with each turn: place, layout, pulled bodies, platform |
| `BeingActions.cs` | The relay's tool calls: open a place, pull a body, restore |
| `BeingSettings.cs` | Relay address, silence tuning, placement |
| `Editor/BeingBuilder.cs` | **Cosmic Simulation → Build Cosmic Being**: materials, settings, prefab, wired into both docks |
| `tools/being-relay/` | Node + TypeScript: `server.ts` (WebSocket), `session.ts` (history, interrupt, cost cap), `claude.ts` (streaming, cached system prompt, tools, sentence split), `stt.ts`, `tts.ts`, `knowledge.ts` (reads `Assets/data` at start), `prompt/system.md` |

## Protocol

Unity → relay: `{"type":"hello","context":…}`; `{"type":"turn","text":"…","context":…}`; `{"type":"turn","audioBytes":N,"context":…}` followed by one binary frame of 16 kHz mono PCM16; `{"type":"interrupt"}`.

Relay → Unity: `{"type":"transcript","text"}`; `{"type":"text","text"}` per sentence; `{"type":"action","name","args"}`; binary frames of 24 kHz mono PCM16; `{"type":"done"}`; `{"type":"error","text"}`.

## Running it

    cd tools/being-relay && cp .env.example .env   # add the two keys
    npm install && npm run dev

Set `RelayUrl` on `Assets/data/being/cosmic_being_settings.asset` to the machine's address (the Quest needs the LAN
address, not localhost), build the being once from the menu, rebuild the UI prefabs and the desktop dock, and press
the button.

## Decisions

- Tap-to-talk, not always listening: no echo of its own voice, no voice-activity service, no open microphone.
- Claude Opus 5 at low effort with a cached system prompt, and the latency instruction in the prompt, because this is
  a voice: the first sentence has to arrive fast. Raise `CLAUDE_EFFORT` in `.env` if answers feel thin.
- Knowledge is parsed from the module and body assets at relay start, never copied, so it cannot drift from the app.
- A session is capped at 40 turns (`MAX_TURNS`); the being says so and stops.
