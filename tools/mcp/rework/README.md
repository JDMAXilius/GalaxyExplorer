# `tools/mcp/rework/` — verifying the rework's Phases 0–3 from a terminal

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
| `p3_build_rig.cs` | `Cosmic/Verify/P3 Build Rig` | CS-139 |
| `p3_setup.cs` | `Cosmic/Verify/P3 Setup` | CS-140 |
| `p3_enter_play.cs` | `Cosmic/Verify/P3 Enter Play` | CS-140 |
| `p3_run.cs` | `Cosmic/Verify/P3 Run` | CS-140 |
| `p3_leave_play.cs` | `Cosmic/Verify/P3 Leave Play` | CS-140 |
| `p3_teardown.cs` | `Cosmic/Verify/P3 Teardown` | CS-140 |

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

---

## Phase 3 — the rig and the feel test (CS-139, CS-140)

Phase 3 is `Interaction/Grabbable.cs`, `Interaction/Pull.cs`, `Interaction/Mouse.cs`, `Interaction/Hotkeys.cs`
and the rig builder in `Editor/Scene.cs`. None of it has run either. CS-139 is the compile-and-build gate;
CS-140 is one planet-sized body pulled, grabbed, scaled, spun, restored and left to stray, on the desktop
pointer alone. The Quest half of CS-140 — near pinch, far pinch, two-handed scale — is not scriptable from
here and stays with the owner.

### Extra preconditions, on top of the three above

4. **Open a saved scene with no camera tagged `MainCamera`.** The rig prefab brings its own eye. With a
   second one loaded, `Camera.main` is whichever Unity hands back first, and `Mouse`, `Pull` and `Room` all
   silently aim through the wrong one. `Cosmic/Verify/P3 Setup` reports that as a `[P3] FAIL` naming the
   stray camera; deactivate it, or open an empty saved scene, and run setup again. The Phase 2 host scene
   (`solar_system_prefab_scene`) **does** have a camera, so it is the wrong host for Phase 3.
5. **`Assets/Cosmic/Prefabs/room_dim.mat` and `Assets/Cosmic/Data/Generated/audio_library.asset` have to
   exist before `p3_build_rig.cs`.** The rig builder wires both into the rig root; with `Room.dimMaterial`
   null, `Room` logs its own error the instant play starts, and with `Audio.library` null every grab and
   pull is silent. The `[P3] rig inputs:` line says which of the two came through, and neither is fatal to
   the assertions. `Cosmic/Verify/P2 Setup` is what makes them, and `P2 Teardown` keeps them on purpose, so
   a finished Phase 2 run leaves both in place. If you have to run `P2 Setup` now, run `P2 Teardown`
   straight after it and before `p3_setup.cs`: setup also drops a `cosmic_verify` host carrying its own
   `Room` and `Audio`, and a second `Room` next to the rig's is a coin toss over which one is the singleton.
6. **A Game view has to be open and visible, and the real mouse has to stay off it.** The run drives
   `Mouse.current` and `Keyboard.current` with synthetic state events; a physical mouse move queues events
   into the same device and fights them. If the whole run fails at the first hover, that is the usual cause
   — the other is that the input system is not processing events while the Game view is unfocused, which
   *Window → Analysis → Input Debugger → Options → Lock Input to Game View* fixes.

### The sequence

```bash
# CS-139 — compile, then build the rig and check it, twice
pwsh tools/mcp/compile.ps1
node tools/mcp/umcp.js run tools/mcp/rework/p3_build_rig.cs
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-140 — put the rig and one test body in the open scene and save
node tools/mcp/umcp.js run tools/mcp/rework/p3_setup.cs
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-140 — play mode
node tools/mcp/umcp.js run tools/mcp/rework/p3_enter_play.cs
#   wait ~5 s for the domain reload; "Unity not detected" here is the reload, not a fault
node tools/mcp/umcp.js run tools/mcp/rework/p3_run.cs
#   wait ~20 s
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

# CS-140 — stop and clean up
node tools/mcp/umcp.js run tools/mcp/rework/p3_leave_play.cs
node tools/mcp/umcp.js run tools/mcp/rework/p3_teardown.cs
```

### What PASS looks like

`p3_build_rig.cs` — twenty-two assertions and:

```
Cosmic rig created -> Assets/Cosmic/Prefabs/rig.prefab; nothing missing
[P3] PASS the rig root carries XROrigin
...
[P3] PASS a second Cosmic/Build/Rig left the prefab GUID unchanged (8f1c...)
[P3] DONE 22/22
```

`p3_setup.cs`:

```
[P3] setup: rig instantiated from Assets/Cosmic/Prefabs/rig.prefab, cosmic_verify_body created at
     (0.0, 1.2, 2.0) - 0.15 m across, 2 m in front of Main Camera, saved into Assets/.../<scene>.unity.
```

`p3_run.cs`, about twenty seconds later — thirty-one `[P3] PASS` lines, one `[P3] SKIP`, and:

```
[P3] DONE 31/31
```

The skip is the orbit: `Mouse.pivot` is unassigned in the rig as built, so an empty-space drag has nothing
to turn. Wire a pivot and the same run reads `32/32`. A skip is neither a pass nor a failure and is left
out of the count on purpose.

### The body sits two metres out, not one

`Mouse` parks its attach transform 1 m down the ray, and `Pull.IsFar` calls any interactor whose attach is
within 12 cm of the body a *near* grab and refuses to start a dwell. A body at exactly 1 m would therefore
never dwell-pull, and the beam assertions would fail for a reason that has nothing to do with the beam.
At 2 m the hover is far, and the pull then parks the body at the attach *plus its own half-width standoff*
— about 1.12 m from the eye, not 1.00 m — which is why that assertion is written against
`Mouse.attachTransform` with the standoff added, rather than against a flat 1 m.

### What the run asserts, and how tight

| Check | Tolerance |
|---|---|
| hover registers, `Pull` still `Parked`, beam rising | 0.35 s after the cursor lands; beam strictly between 0 and 1 |
| dwell starts a pull | held 2.65 s against a 2 s dwell |
| pull ends `Loose`, 0.25 m across, at the attach, `Placed` still false | ±0.02 m on width; 0.1 m plus the half-width standoff on position |
| LMB selects | within 2 player frames of the queued press |
| 200 px drag moves the held body | more than 0.05 m |
| release deselects and marks `Placed` | exact |
| three wheel notches scale by 1.1³ | ±5 % |
| 60 notches clamp at `MaxMetres` (3 m) | ±0.02 m |
| −100 notches clamp at `MinMetres` (0.05 m) | ±0.005 m |
| right-drag spins the body | more than 1° |
| stray 6 m away for 5 s comes home, clears `Placed`, parks `Pull` | 0.01 m, after a 6.5 s wait (5 s grace plus the 0.8 s tween) |
| `R` raises `Hotkeys.Restore`, tweens home, parks `Pull`, kills the live dwell | 0.01 m, within 1.3 s of the key |
| LMB on empty space selects nothing | exact |
| `Room.Changed` and `Prefs.Changed` never fire | exact |

### NOT-OK, and reading past it

Every wrapper here will usually come back **NOT-OK**, and it means nothing on its own. The relay reports
NOT-OK whenever *anything at all* was logged as a warning during the command, and this project has two
warnings that fire on nearly every command (the TouchScript `WindowsTouch.dll.meta` `PluginImporter`
version, and TMP's `CanvasRenderer` chatter). Phase 3 adds one more, once per play session:

> Hand Tracking Subsystem not found or not running, can't subscribe to hand tracking status. …

That is `XRInputModalityManager` on a desktop editor with no headset. It is expected, it is logged once,
and the run is unaffected: with no hand subsystem the manager leaves both hand GameObjects inactive, which
is exactly what the desktop test wants — only the `Mouse` interactor is live. A host scene that still has
its own `AudioListener` adds a second once-per-session warning next to the rig's; that one means the scene
is the wrong host (see precondition 4) even though it changes no assertion. Neither is **attributable** to
any of these commands: `p3_enter_play.cs` returns before play mode actually starts, and `p3_run.cs`
returns before its first step runs, so both land in the console rather than in a command's result. **Read the `[P3]` lines, never the OK/NOT-OK status.** The only status that means
anything is the wrapper's own text when a menu item is missing.

### On FAIL

Grep the console for `[P3] FAIL`. The lines are ordered, so the last PASS before the first FAIL says where
the run got to. These are the ones whose failure usually means something other than what they say:

- **Everything from the first hover onward fails** — no synthetic input is reaching the game. Check that a
  Game view is open, that the physical mouse is not sitting on it, and turn on *Lock Input to Game View* in
  the Input Debugger. `[P3] PASS the cursor on the body makes the Grabbable hovered` is the canary: if that
  one passes, input works and any later failure is real.
- **`the select lands within 2 frames of the press` fails with a 3** — an editor tick landed either side of
  a player frame boundary. Re-run before believing it; a real regression fails every time, and the other
  select assertions fail with it.
- **`the body is hovered again once it is back to arm's length size` fails** — the wheel clamp left the body
  3 m across at about 1.1 m from the eye, which puts the camera *inside* the sphere, where a raycast has no
  front face to hit. The run shrinks it back in code for exactly that reason; if this still fails, the
  shrink did not take, and the `-100 notches` line below it will fail too for the same reason.
- **`a placed body left 6 m out of reach comes home by itself` fails** — `Grabbable.autoReturn` is off. It
  is off by default per RULES.md and `Cosmic/Verify/P3 Setup` turns it on through `SerializedObject`; if the
  field was renamed, setup logs `[P3] FAIL Grabbable has no serialized field 'autoReturn'` much earlier.
- **`the R key raises Hotkeys.Restore` fails** — the Desktop action map is not enabled, or `Hotkeys.actions`
  came through unwired. `Hotkeys` logs its own error in that case at play start.
- **`Room.Changed never fired` fails** — something outside Phase 3 is in the scene driving the room, most
  likely a leftover `cosmic_verify` host from the Phase 2 run. `Cosmic/Verify/P2 Teardown` removes it.
- **The sequence stops partway with no DONE line** — Error Pause is on, or a step threw. A thrown step logs
  `[P3] FAIL step N threw:` and the sequence carries on, so a silent stop is the pause.

### What Phase 3 cannot be asserted on

Judged, not asserted, and the owner has to do them on the Quest before CS-140 closes: near pinch and far
pinch with hands, two-handed scale and rotate inside the limits, whether the 2 s dwell *feels* like a
tractor beam rather than a delay, and whether a released body stays put where the hand left it. The desktop
run says the state machine is correct; it says nothing about how any of it feels.

`Cosmic/Verify/P3 Teardown` removes `cosmic_verify_body` and the rig instance from the scene and saves. It
**keeps** `Assets/Cosmic/Prefabs/rig.prefab`: that is not a fixture, it is the rig Phase 6 builds the one
shipping scene around. Commit it.
