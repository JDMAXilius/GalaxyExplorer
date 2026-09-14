# The Cosmic Being

A small blue sphere of light, the same point-cloud hologram the intro's Earth wears, that keeps station beside the
player and answers spoken questions about the universe. Claude is its mind; OpenAI is its ears and voice; a small
relay holds both keys so the app never does.

It is the intro object, not something like it: `being_hologram.mat` carries the intro material's own `_Size` of 0.07,
so the point cloud reads at 14 cm exactly as the player already met it at floor-placement size. `_Size` is a point
sprite's size in object space, so the ratio of point to sphere is the same whatever the sphere is scaled to.

## Flow

1. The dock's being button (Quest: the small button under the dock; desktop: the last button on the HUD dock, or `C`)
   summons the sphere. It fades in beside the player, connects to the relay, and the relay speaks a greeting.
2. Tap the sphere (pinch, poke or click). It pops once to say it felt that, brightens and listens. Speak. When you
   stop, it goes quiet on its own after 1.2 s of silence and thinks.
3. The recording goes to the relay in one piece. Whisper transcribes it, Claude answers, each finished sentence is
   turned into speech and streamed back, and the sphere swells with the voice - the talking pose.
4. Tap while it is speaking to interrupt. Press the dock button again to dismiss it.

The Claude API is text only: no speech in or out, and streaming is server-sent events over HTTP. That is why the
voice comes from OpenAI and why there is a relay. The relay is also the only place an API key lives; a key inside an
APK or a desktop build can be read out.

## It behaves like a body

The being takes the same inputs a planet does, through the same components, so a hand, a hand ray and the mouse all
reach it by one path:

- **Carry it.** A `ManipulationHandler` on the root, one- or two-handed, far interaction on. Grab sounds are on here
  and off on the bodies, because a body's `ForceSolver` already plays them and the being has no solver.
- **Brush it.** A `TouchNudge`, the same component `ForceSolver` adds to every planet and moon (CS-173): stroke it
  without grabbing and the sphere turns a little with the stroke and springs back. Its target is the `hologram` child,
  so the nudge composes with the point cloud's own spin and leaves the root to the anchor.
- **Tap it.** A press and release inside 0.4 s that moved less than 30 mm. The same quick-and-still test the bodies
  use for "put it back" (CS-174), for the same reason: a click event cannot tell a tap from a carry, and carrying the
  being across the room must not start a conversation.
- **It faces you.** `BeingAnchor` turns the being's front toward the head at 540 deg/s, about the world's up only -
  a being that pitched to follow a player looking at their feet would read as falling over.
- **It yields, then keeps station from where you left it.** While a hand has it the anchor writes no position at all.
  On release it re-reads its own offset from wherever it was put down and follows from there, so moving it is a
  decision made once rather than a tug of war.

## Pieces

| Where | What |
|---|---|
| `Assets/scripts/being/CosmicBeing.cs` | Phases Idle, Listening, Thinking, Speaking; the tap; summon and dismiss |
| `BeingAnchor.cs` | Keeps station beside the head and turns to face it; yields to a hand and re-reads its offset on release |
| `BeingVisual.cs` | Phase and loudness into the hologram's `_Active`, `_Blend`, rotation speed and the rim's `_Multiplier`; the press pop and the talking swell, both as size |
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
- The talking pose is size, not a mouth. There is no face to animate, so the voice drives the sphere's scale through
  a follow of 18/s: a loud syllable is a bigger sphere and the gaps between words let it settle. Rim brightness
  tracks the same level, so it reads in peripheral vision as well as head-on.
- The builders refuse to run in play mode. `Build UI Prefabs` in play mode threw inside TMP's outline setter - it
  reaches through a `CanvasRenderer` that `Awake` has not wired on a freshly created object - which aborted the run
  partway and left every prefab it had not reached at its old contents, with one exception line to show for it.
- Claude Opus 5 at low effort with a cached system prompt, and the latency instruction in the prompt, because this is
  a voice: the first sentence has to arrive fast. Raise `CLAUDE_EFFORT` in `.env` if answers feel thin.
- Knowledge is parsed from the module and body assets at relay start, never copied, so it cannot drift from the app.
- A session is capped at 40 turns (`MAX_TURNS`); the being says so and stops.
