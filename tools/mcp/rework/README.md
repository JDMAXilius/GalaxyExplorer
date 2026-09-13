# `tools/mcp/rework/` — verifying the rework's Phases 0–2 from a terminal

Phases 0–2 of the Cosmic rework are committed but have **never been through a compiler and never run**.
They also have no scene presence: no `AudioLibrary` asset, no dim material, no host GameObject. So there
is nothing to look at and nothing to press. This folder is the harness that makes those things, drives
them in play mode, and prints a verdict the terminal can read.

Everything here is a thin `IRunCommand` wrapper. The work lives in menu items in
`Assets/Cosmic/Editor/Verify.cs` (`Cosmic.Editor` assembly), for two reasons: the relay runner compiles a
throwaway assembly that does not reference `Cosmic.Runtime`, so it cannot name `Room`, `Audio` or
`Prefs` at all — and a missing menu item is itself the compile verdict for CS-126.

**One client at a time.** The relay is a single shared connection and two clients deadlock. Same reason
only one `unity-editor` agent may run at once. Read `tools/mcp/README.md` first; every runner limit there
still applies here.

| Wrapper | Menu item it runs | Ticket |
|---|---|---|
| `p0_compile.cs` | `Cosmic/Verify/P0 Compile Check` | CS-126 |
| `p1_data.cs` | `Cosmic/Verify/P1 Import And Check` | CS-127 |
| `p2_setup.cs` | `Cosmic/Verify/P2 Setup` | CS-128 |
| `p2_enter_play.cs` | `Cosmic/Verify/P2 Enter Play` | CS-129 |
| `p2_run.cs` | `Cosmic/Verify/P2 Run` | CS-129 |
| `p2_leave_play.cs` | `Cosmic/Verify/P2 Leave Play` | CS-130 |
| `p2_teardown.cs` | `Cosmic/Verify/P2 Teardown` | CS-130 |

---

## Before you start

1. Editor open on this project, relay running, **not** in play mode.
2. **Open a saved scene with a `MainCamera` in it, by hand.** The harness never calls `OpenScene`: in
   Single mode it raises a modal save prompt that blocks the relay until someone clicks it in the editor.
   `Assets/scenes/development_scenes/solar_system_prefab_scene.unity` is the recommended host — small,
   and it already carries a camera tagged `MainCamera`, which the dim quad and the Black-clear checks
   need. `main_scene` works too but brings the whole legacy boot flow along with it.
3. **Switch Error Pause off in the console.** A `FAIL` is a `Debug.LogError`, and with Error Pause on the
   first one freezes play mode halfway through the sequence.

---

## The sequence, top to bottom

```bash
# CS-126 — compile, then confirm what loaded
pwsh tools/mcp/compile.ps1
node tools/mcp/umcp.js run tools/mcp/rework/p0_compile.cs
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-127 — import the copy twice, then the parity gate
node tools/mcp/umcp.js run tools/mcp/rework/p1_data.cs
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'
python3 tools/parity/check_data.py            # from the repo root, outside the editor

# CS-128 — make the two real assets and the host object, save the scene
node tools/mcp/umcp.js run tools/mcp/rework/p2_setup.cs
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-129 — play mode
node tools/mcp/umcp.js run tools/mcp/rework/p2_enter_play.cs
#   wait ~5 s for the domain reload; "Unity not detected" here is the reload, not a fault
node tools/mcp/umcp.js run tools/mcp/rework/p2_run.cs
#   wait ~15 s
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-130 — stop and clean up
node tools/mcp/umcp.js run tools/mcp/rework/p2_leave_play.cs
node tools/mcp/umcp.js run tools/mcp/rework/p2_teardown.cs
```

`compile.ps1` first, always. Nothing below it means anything against an assembly that did not build, and
`ExecuteMenuItem` on a menu item that does not exist returns `false` rather than throwing — which is why
every wrapper reports that case in words instead of looking like a silent pass.

---

## What PASS looks like

`p0_compile.cs`:

```
[P0] assemblies loaded: Cosmic.Runtime=True Cosmic.Editor=True (of NNN total)
[P0] PASS both Cosmic assemblies are present
[P0] Assets/Cosmic/Input/actions.inputactions imports as InputActionAsset; maps: XR=10 actions, Desktop=29 actions
[P0] PASS the actions asset imports as an InputActionAsset with both the XR and Desktop maps
```

`p1_data.cs`:

```
Cosmic copy import: N bodies, N moons, N places, N destinations, N hints, N references wired -> Assets/Cosmic/Data/Generated
[P1] generated: places=N bodies=N layouts=0
[P1] PASS a second import left all N generated GUIDs unchanged
[P1] now run, from the repo root: python3 tools/parity/check_data.py
[P1] expected residual loss, and nothing else: ...
```

`p2_setup.cs`:

```
[P2] setup: Assets/Cosmic/Data/Generated/audio_library.asset created, Assets/Cosmic/Prefabs/room_dim.mat created,
     cosmic_verify created and saved into Assets/scenes/.../<scene>.unity. Camera.main is present.
```

`p2_run.cs`, about fifteen seconds later — thirty `[P2] PASS` lines and:

```
[P2] DONE 30/30
```

Anything other than `30/30`, or any `[P2] FAIL` line at all, is a failure of that check specifically. The
lines are ordered, so the last PASS before the first FAIL says where the sequence got to.

---

## On FAIL

Grep the console for `[P2] FAIL`. The wording of each line names the assertion, and these are the ones
whose failure usually means something other than what it says:

- **`room_dim_quad is an active MeshRenderer` and `Camera.main clears to opaque black` both fail** —
  there is no `Camera.main` in the scene. `Room.View()` returns null and nothing is ever built. Open a
  scene that has a camera tagged `MainCamera` and start again at `p2_setup.cs`.
- **`the dim reaches alpha 0.5` fails but the quad exists** — `room_dim.mat` is on the wrong shader, or
  `Room.dimMaterial` came through unassigned and `Room.Build` logged its own error early in the run.
- **Every music check fails** — `audio_library.asset` has no `musicByRoom` entries, or `Audio.library`
  is unassigned. `Audio` fails silently by design when the library is null, so look for the setup line,
  not for an exception.
- **`two unplaced Select calls in one frame debounce to one voice` fails with a delta of 2** —
  `EditorApplication.update` ticked twice inside one player frame and `Time.unscaledTime` advanced
  between the two calls. Re-run before believing it; a real debounce regression fails every time.
- **`[P2] FAIL not in play mode`** — `p2_enter_play.cs` reported success and play mode never started.
  That is CS-119's shape. Check `isPlaying` directly before re-running.
- **The sequence stops partway with no DONE line** — Error Pause is on, or a step threw. A thrown step
  logs `[P2] FAIL step N threw:` with the stack and the sequence carries on, so a silent stop is the
  pause.

Re-running `p2_run.cs` while a sequence is live reports the step it has reached instead of starting a
second one. To start over, leave play mode and enter it again — the domain reload clears the statics.

---

## Looking and listening

Three of the Phase 2 behaviours cannot be asserted, only judged, and the owner has to do them:

- The Passthrough→Dimmed→Black crossfade is a **2 s** fade and must be click-free, with no restart when
  the same room state is set twice.
- The duck under narration must be audible but must not swamp the voice.
- The dim must read as a soft grey veil, not a hard-edged plate in front of the face.

For the last one, frame the **scene view** at the player's eye (`SceneView.pivot` / `rotation` from
`Camera.main`) and capture it with `Unity_Camera_Capture '{}'`. Do not reach for `ScreenCapture` — the
relay refuses it outright with *"User interactions are not supported for MCP tool calls"* — and do not
pass `cameraInstanceID`; only the scene view captures reliably, and the scene view does not use the game
camera's clear colour, so a black room reads as the editor's light background there.

---

## What is kept and what is thrown away

`Cosmic/Verify/P2 Teardown` removes `cosmic_verify` from the scene and saves. It **keeps**
`Assets/Cosmic/Data/Generated/audio_library.asset` and `Assets/Cosmic/Prefabs/room_dim.mat` on purpose:
those are the real inputs Phase 6 wires into the one scene, not fixtures. Commit both.

`Verify.cs` is test tooling and says so in its own one comment line — it does not count against RULES.md's
six-editor-script budget for the rework.
