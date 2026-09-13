# Cosmic Simulation XR — Ticket backlog

*Derived from `docs/COSMIC_SIMULATION_XR_ROADMAP.md`. Every ticket has a **track**:*
- **[CC] Claude Code** — can be done in the repo alone: scripts, editor tools, data, copy, docs, prefab/scene YAML edits that don't need a live editor to verify. Compile-check by reading; the terminal track verifies in the editor later.
- **[TERM] Terminal** — needs the running Unity editor through the MCP relay (play-mode tests, screenshots, prefab surgery that must be verified live), the Quest device, or the Figma / Higgsfield MCPs. Done in the local Claude Code terminal session with the editor open.

## How to work this backlog (any Claude Code session)
1. Read `CLAUDE.md`, then this file. Pick the **first ticket in your track whose status is `todo` and whose dependencies are `done`**.
2. Set its status to `doing` (edit this file) in your first commit.
3. Do the work following the acceptance line. Keep changes scoped to the ticket's files where possible.
4. Commit with the message `<ID>: <title>` and a short body. Update the ticket to `done` in the same commit, adding one line of notes (what changed, anything the other track must verify).
5. If a [CC] ticket turns out to need the live editor, set status `blocked-term` with a note and move on; the terminal track picks it up.
6. Never push; the owner pushes. Never mark `done` what you could not verify — say so in notes.

Status values: `todo` · `doing` · `done` · `blocked-term` · `blocked-cc` · `dropped`.

---

## Session note (12 Sep 2026, reconciliation pass)

This was a cloud session with no Unity editor and no MCP relay open. Its job was the
backlog itself, not new code: read all eighteen commits of the 12 Sep parallel wave,
reconcile the ticket ids they claimed against the rows this file actually has, add rows
for work that landed with none, correct two records that were marked `done` without
their artefact, and build the terminal queue below. Like every [CC] ticket it is
reconciling, nothing here was checked against a compiler — reading only. Nothing under
`Assets/` or `tools/` was touched by this pass.

## Ticket id map — parallel wave reconciliation (12 Sep 2026)

Several agents worked the 12 Sep wave in parallel. Each claimed the next free ticket id
as it saw the file, and the file changed under them, so a commit message and the row its
work actually lives under now do not always agree. **This file's row numbers are
authoritative — nothing below is a renumbering.** Use this table to go from a commit
message to the row that carries the note.

| Commit says | Actual row in this file | What it is |
|---|---|---|
| CS-087 (`3a99b96`) | **CS-092** | Scene panel spawn/dispose on every switch |
| CS-091 (`3a99b96`) | **CS-093** | `AmbienceController` — plays `module.Ambience` on switch |
| CS-088 (`93f13db`) | **CS-094** | Hint card runtime (`HintCards`/`HintCardSet`) |
| CS-089 (`e19c925`) | **CS-109** (new row, this pass) | `MoonBuilder.cs` + two `MoonOrbit.cs` fixes — CS-089 was already the desktop-help-overlay-copy ticket by the time this commit landed |
| CS-095 (`ecb8a40`, the Sun half) | folded into **CS-046**'s note | `_TouchBrightness` on `sun_shader` — CS-095 is the shader-stereo verification ticket that `be819ae` opened under the same number |
| CS-097 (`ecb8a40`, the dock half) | **CS-097** (new row, this pass) | Dock height at Recenter — no collision, the commit had it right, the row just did not exist yet |
| CS-099 (`93f13db`, the Help half) | **CS-099** (new row, this pass) | Wiring the dock's Help button — no collision, same situation as CS-097 |
| CS-098, CS-102, CS-106 | **CS-098, CS-102, CS-106** | No collision on any of these three; cited correctly and already had rows |
| CS-086, CS-087, CS-088, CS-090, CS-091 (`docs/store/STORE_READINESS_CHECKLIST.md`) | **CS-086, CS-113, CS-114, CS-112, CS-115** | The store-readiness draft suggested its own numbers before this file had rows for them, and said so in its own header ("none of them exist there yet"). See Phase 7 |

A scratchpad draft written mid-session (`id_reconciliation.md`) proposed a different
renumbering — CS-091/092/093 swapped the other way, and CS-104/CS-105 for the prefab
surgery and copy-rewrite tickets. **That draft is stale.** The wave itself resolved the
CS-087/CS-092/CS-093 collision by writing the desktop-key-map note under CS-087 and the
prefab-surgery and copy tickets under CS-088/CS-089 as this file already had them, before
that draft could be applied. Trust this table and the rows below it, not that file.

---

## [TERM] priority queue — what the terminal track owes, in order (12 Sep 2026)

Nothing written in the 12 Sep wave has been through a compiler, and none of it has run.
Work this list top to bottom; later groups assume the ones above left the project in a
state that actually builds. Ticket ids are given so a screen or a log line can be traced
back to its note without re-reading eighteen commits.

**Update, 12 Sep 2026 (terminal session, the first one against the live editor).** Steps
1, 2 and 4 through 10 below passed, and step 21 passed — see the dated session note near
the end of this file for what each one actually showed. Step 3 (confirm exactly one live
`EventSystem` and module *after boot*) still needs a play-mode run and has not happened.
Items 22 through 24 below were corrected today, not completed — the render-mode reading
they were built on was backwards; see item 22's own note. Nothing else in this queue —
step 3, steps 11 through 20, steps 25 through 28 — has run. No play-mode session and no
device run happened today, and not by choice: play mode would not reliably enter through
the relay (see the session note's harness paragraphs and **CS-119**), so nothing below
step 10 could be driven from the terminal. Everything recorded above was checked
structurally in the editor and in the serialised assets, not seen working at runtime.

**0. Compile — before anything else.**
1. Refresh and read `Logs/Editor.log` for `error CS####` lines (`tools/mcp/compile.ps1`,
   CS-098).
2. **The single highest-risk item in the whole wave — check this first.**
   `Assets/scripts/experience/UiEventSystemInstaller.cs`, method `EnsureDefaultActions`,
   calls `module.actionsAsset` and `module.AssignDefaultActions()` on an
   `InputSystemUIInputModule` (CS-061). Both calls are a guess about this Input System
   package version that nothing in this session could check. If either member does not
   exist here, **the project does not compile**, and every screen-space button — desktop
   dock, desktop menu, hint cards, the load-failure notice — stays exactly as dead as it
   was before CS-061. The fix if it fails is isolated on purpose (`7e1a358`): delete the
   `EnsureDefaultActions` method and its one call site; the module still installs, just
   without assigned actions until something else supplies them.
   **Resolved 12 Sep 2026, terminal session:** it compiles as written. Input System
   **1.20.0** declares both `public InputActionAsset actionsAsset`
   (`InputSystemUIInputModule.cs:2556`) and `public void AssignDefaultActions()`
   (line 1541). The fallback above is not needed against this package version.
3. Once it compiles clean: confirm exactly one live `EventSystem` with exactly one
   enabled `InputSystemUIInputModule` after boot, and that the app's own `XRUIInputModule`
   on the camera prefab ends up disabled, not destroyed and not left running alongside
   it (CS-061; `7e1a358` fixed an early-return bug that meant this scan never actually
   ran before).

**1. Menu items to run, in this order — solar row before moons, and the pointer-router
builders before anything asks a nebula, the cosmic web or Andromeda to be grabbed.**
4. **Cosmic Simulation → Build Nebula Prefabs** — a *re-run*, not a first run. Stale
   since CS-106 added the grab forwarder and turned `playGrabSounds` on, and again since
   CS-107 added `FreePlacementAnchor`.
5. **Cosmic Simulation → Build Cosmic Web Content** — first run; `cosmic_web_content_prefab`
   has never been generated (CS-065, CS-106, CS-107).
6. **Cosmic Simulation → Build Andromeda Content** — first run; `andromeda_content_prefab`
   has never been generated (CS-102, CS-106, CS-107).
7. **MoonBuilder post-pass** (CS-109) — run only after `solar_system_planets_content_prefab`
   exists (it does, CS-041) and only once the project compiles. It must run *after*
   `SolarRowBuilder` and must not edit it. The likeliest failure, per its own commit: a
   prefab instance nested inside `PrefabUtility.LoadPrefabContents` mode, which is
   documented but used nowhere else in this repo; it is null-checked, so the failure mode
   is moons with no panels, not a crash.
8. **Cosmic Simulation → Build Hint Cards** (CS-094) — until this runs,
   `HintCards.ShowIfFirstRun()` logs an error and shows nothing.
9. **Cosmic Simulation → Wire Scene Panel** (CS-092) — points `ExperienceDirector` at
   `info_panel_prefab`; unwired, every experience has no panel and only a
   once-per-session console warning says why.
10. **Cosmic Simulation → Install Runtime Systems** — re-run. Now also installs
    `MusicController` with its three clips and switches off `core_systems_scene`'s five
    legacy `MusicAudioSources` (CS-073), and wires `desktop_dock_prefab` (see the
    12 Sep stop-point note). Confirm the legacy sources are actually inactive rather than
    merely muted by a snapshot.

**2. Play-mode checks.**
11. Switch every module **from the dock**, not just through `ExperienceDirector`
    directly — `tools/mcp/smoke.cs` (CS-098) drives the director directly and would not
    catch a dead `GEButton` or a mis-wired poke filter.
12. Confirm a scene panel appears for all seven experiences, including the start module
    reached through `Adopt` (the Milky Way, which never goes through `Switch`) — CS-092.
13. Confirm ambience starts and stops with each switch and that Mute silences it —
    CS-093. The levels (0.35, no duck under narration) are guesses and want a listen.
14. Confirm the music crossfade: Milky Way to Sagittarius A* is a clean 2 s fade;
    toggling passthrough twice quickly does not restart it; and check the `7e1a358` fix —
    the crossfade used to assume the outgoing source was always the quieter one, which
    broke the moment a scene missing from Build Settings took the room through three
    states in one frame.
15. Confirm dock height at Recenter, seated and standing: `max(0.55 x head, 0.7 m)`
    (CS-097). Also confirm the `7e1a358` fix for recentring before the headset has
    produced a pose — it used to plant the dock underground.
16. Hint cards (CS-094): legibility and wrap at 200 mm; card 2 dismisses on a two-hand
    scale **and** on the desktop mouse wheel, since no app-wide two-hand signal exists so
    it watches the held object's size instead. Confirm Help, world dock and desktop,
    replays them (CS-099) — the world dock's Help button had no listener on it at all
    before this.
17. Hand-grab all seven nebula overlays, the cosmic web and Andromeda (CS-106) — none of
    these worked by hand before this wave despite carrying `ManipulationHandler`. Check:
    two hands on one overlay scale rather than fight; a fingertip poke actually registers
    `IsNear` at these collider radii; grabbing mid grow-in does not fight
    `ExperienceDirector.GrowIn`'s own per-frame scale write; a desktop left-drag now moves
    a nebula or Andromeda without fighting the orbit gesture.
18. `R` and Recenter now **do** bring a moved, turned or scaled nebula, Cosmic Web or
    Andromeda home over 0.8 s (CS-107) — check both modes, and check that planets and the
    solar row are unaffected by the same press. The world dock's Recenter button does it
    too now: `DockController.Recenter` calls `RestoreAll()` above its own camera guard
    (CS-107). Then the dock's own drag bar, which now actually drags the dock (CS-108) —
    **Build UI Prefabs has already been re-run and the result confirmed structurally**
    (`drag_bar` carries `hostTransform` pointing at the dock root, `manipulationType: 0`,
    `playGrabSounds: 1` and `ManipulationPointerRouter`); what is left is the six hand/
    device checks in the CS-108 note, of which the load-bearing ones are that the bar
    stays on the dock, that the dock stays level, and that a desktop right-drag or wheel
    over the bar still pans and zooms.
    solar row are unaffected by the same press. The world dock's Recenter button does this
    too — `DockController.Recenter` calls `FreePlacementAnchor.RestoreAll()` above its own
    camera guard.
18a. The dock's drag bar (CS-108) — **needs `Cosmic Simulation → Build UI Prefabs` re-run
    first**, the committed `dock_prefab` predates it. Pinch or ray the bar: the **whole dock**
    moves, the bar never leaves the plate, and the dock stays upright and tilted 25° toward
    the player however the wrist turns. Then: Recenter brings it back (and lets go of the bar
    if a hand is still on it); a second hand cannot scale it; palm-up still hides and shows it
    where it now stands; an open utility window jumps back beside the dock when the drag ends;
    a layout pop-up closes when the drag starts. No desktop equivalent exists or is wanted —
    see the CS-108 note.
19. Desktop key map end to end against GDD §5.3: `R`, `Home`, `Esc`, `1`-`9`/`0`/`M`,
    `Tab`, `P`, `F2`-`F8`, `H`/`F1` (CS-087). `M` should genuinely find nothing until the
    Moon's planet is pulled out — expected, not a bug.
20. Sun (folded into CS-046's note): brighten and rumble together while a hand is inside
    it; confirm the untouched Sun is unchanged (both gains enter the shading as exactly 1
    at rest).

**3. Shaders and Link.**
21. Compile the 25 stereo-macro edits (CS-095) — zero errors, zero *new* warnings.
22. **Answered, 12 Sep 2026, terminal session — the reading this item used to record was
    backwards.** `OpenXRSettings.RenderMode` is declared `MultiPass = 0,
    SinglePassInstanced = 1` in
    `Library/PackageCache/com.unity.xr.openxr@b5b77b4027be/Runtime/Settings/OpenXRRenderSettings.cs:16-27`
    (field default `SinglePassInstanced`), and `Assets/XR/Settings/OpenXR Package
    Settings.asset` holds `m_renderMode: 1` under `m_Name: Android` and `m_renderMode: 0`
    under `m_Name: Standalone` (`m_Name: WebGL` is also `1`, moot since it does not ship).
    So **Android — the shipping Quest 3 build — is Single Pass Instanced**, where a
    missing stereo macro breaks one eye on the real device, and **Windows/Link is Multi
    Pass**, where the same bug is invisible. `CLAUDE.md`'s original "Android multiview;
    Windows/Link multi-pass" line was right all along; it is the reading this item used to
    carry, and the note that called that line backwards, that were wrong. Verified from
    the package cache and the settings asset directly, not inferred.
23. Close-one-eye check on **the standalone Android build, or Link explicitly forced to
    Single Pass Instanced** — a default Link session is Multi Pass and would hand back a
    false all-clear — across CS-095's object list: solar system view, galaxy view,
    galactic centre, POI cards and marker pins in all three views, the boundary star dome,
    the intro placement scene, the hand menu cards, the About slate.
24. Also run the same list on Windows/Link (Multi Pass) as a sanity pass, not as the
    verification: a missing macro is invisible there rather than broken, so a clean result
    proves far less than item 23 — only that no macro is missing badly enough to break
    even the forgiving platform.
25. Answer the `#pragma multi_compile_instancing` question for the five affected files
    (CS-095 acceptance item 5) and apply one answer to all five.
26. Work CS-096's eight held-back shaders in the order the audit gives: the black hole's
    lensing disc first (it reads camera position per pixel, so a wrong eye index there
    gives a plausible but wrong image — the galactic centre's centrepiece); then the four
    procedural-draw shaders, matched to `cosmic_web_points_shader`'s pattern; then decide
    whether the screen-compose pair is genuinely dead on XR before touching it; then the
    `occluder_shader` semantic fix. Delete the three dead shaders and stop referencing the
    two XRI sample shaders while there.

**4. Device pass.**
27. Quest Link full-flow session (CS-080): dock reach, panel sizes, everything above that
    only shows on-device.
28. Standalone APK, OVR Metrics >= 60 fps @ 72 Hz (CS-081).

---

## Rework — terminal verification (13 Sep 2026)

The rework is the ground-up rebuild under `Assets/Cosmic/` (see `Assets/Cosmic/RULES.md`); the old tree
stays bootable and is never edited from it. **Phases 0–2 are committed and have never been compiled and
never run.** They also have no scene presence at all: no `AudioLibrary` asset, no dim material, no host
GameObject, nothing to press. So verification needs a harness that first *makes* those things and then
drives the components in play mode with assertions.

Everything below is driven through the Unity MCP relay from a terminal — `node tools/mcp/umcp.js run …`
and `pwsh tools/mcp/compile.ps1` — and **one client at a time**, because the relay is a single shared
connection and two clients deadlock. The wrappers live in `tools/mcp/rework/` (read its README first);
the work they call lives in menu items under `Cosmic/Verify/…` in `Assets/Cosmic/Editor/Verify.cs`,
because the runner's throwaway assembly cannot name `Room`, `Audio` or `Prefs` but a menu item can.

`Verify.cs` is test tooling and is outside RULES.md's six-editor-script budget, which it says in its own
one comment line. The two assets its setup step writes — `Assets/Cosmic/Data/Generated/audio_library.asset`
and `Assets/Cosmic/Prefabs/room_dim.mat` — are **not throwaways**: they are the real inputs Phase 6 wires
into the one scene, built here because Phase 2 is the first thing that needs them. Both are committed.

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-126 | TERM | Compile the Cosmic assemblies | — | done |
| CS-127 | TERM | Import copy and run the parity gate | CS-126 | done |
| CS-128 | TERM | Phase 2 setup — audio library, dim material, host object | CS-126 | done (13 Sep terminal run) |
| CS-129 | TERM | Phase 2 play-mode verification | CS-128 | done (`[P2] DONE 30/30`, 13 Sep) |
| CS-130 | TERM | Teardown and commit the generated assets | CS-129 | done (both assets committed 13 Sep) |
| CS-131 | TERM | Wiring step: the 60 asset references the copy deck cannot own — 38 `Narration`, 15 `ContentPrefab`, 7 `DockThumbnail`. **The only class of loss left in the gate** | CS-127 | done (45 references wired, 13 Sep) |
| CS-132 | CC | Extend `docs/copy/` with the four places and seven bodies it never got: `hd110067`, `pinwheel`, `triangulum`, `whirlpool`, `hd110067_star` and `hd110067_b`–`g` | CS-127 | done |
| CS-133 | CC | Port the layout builder — `solar_row`, `relative_size`, `hd110067_row`, `hd110067_relative`, and the `Layouts` reference on `solar_system_planets` | CS-127 | done |
| CS-134 | TERM | P1's gate passes: `check_data.py` exits 0 | CS-131, CS-132, CS-133 | done (`check_data.py` exits 0: 1010 values, 0 losses) |
| CS-135 | CC | Split the one-shot pool out of `Audio.cs` — P2's declared debt (278 lines against ~250) | — | done |
| CS-136 | CC | Rework P3 and beyond — broken out below into CS-137 onward as each phase starts; this row is the index | — | superseded |
| CS-137 | CC | Rework P3a: `Interaction/Grabbable.cs` (native XRI grab interactable: home pose, restore, placed, auto-return, metre scale limits, upright transformer, Spin/ScaleBy) and `Interaction/Pull.cs` (force-pull as three states, dwell as a hover timer) | CS-126 | done (unrun) |
| CS-138 | CC | Rework P3b: `Interaction/Mouse.cs` (the desktop pointer as an XRI ray interactor), `Interaction/Hotkeys.cs` (the Desktop action map), `Editor/Scene.cs` menu **Cosmic → Build → Rig** (the XR rig prefab assembled by script from the vendored XRI and hands samples) | CS-137 | done (unrun) |
| CS-139 | TERM | Rework P3 compile and rig build: compile, run **Cosmic → Build → Rig**, confirm `Assets/Cosmic/Prefabs/rig.prefab` exists with both hands, the mouse interactor, one interaction manager, one event system on the XR UI input module | CS-138 | done |
| CS-140 | TERM | Rework P3 feel test on one planet, desktop and Link: near pinch, far pinch, 2 s dwell pull with beam, two-hand scale and rotate within limits, release stays, restore tween, mouse LMB/drag/wheel/RMB parity, auto-return after 5 s out of reach; owner signs off on feel before P6 | CS-139 | todo |
| CS-141 | CC | Rework P4a: `Content/Points.cs` (one procedural point renderer), `PointSources`/`WebSource`/`NebulaSource` (seeded generators), `Shaders/Points.shader` (five layer variants), `Editor/Bake*.cs` menu **Cosmic → Build → Bake All** (five galaxies, the cosmic web, seven nebulae as 40-byte point clouds and materials) | CS-137 | done (unrun) |
| CS-142 | CC | Rework P4b: `Content/Orbit.cs` + `OrbitRings.cs` (Kepler solver, realism lerp, moons, one-buffer ring renderer) with `Shaders/Orbit.shader`; `Content/Rig.cs` (anchors between Row, Relative, Schematic, Realistic layouts, ring clamp on the `rings` child); `Layout.kind`, `Slot.spanRatio`, two orbit layouts from `Editor/Layouts.cs` (renamed from Content.cs) | CS-141 | done (unrun) |
| CS-143 | CC | Rework P4c: `Content/Sun.cs` (touch brightness, rumble, flares, glow, lens flare, spin) with `Sun`, `SunCorona`, `SunFlare`, `SunGlow`, `LensFlare` shaders; planet family folded into `Planet` (`_EARTH`/`_SATURN`/`_ALPHA`), `Rings` (`_BASIC`), `Clouds` (`_DIFFUSE`), `Atmosphere`, `Halo`, all with the full stereo set | CS-141 | done (unrun) |
| CS-144 | CC | Rework P4d: `Editor/Content.cs` + `Bodies.cs` menus **Cosmic → Build → Bodies / Places** (every Place asset → one prefab, every Body → one nested prefab, materials from the surveyed values); `Content/Field.cs` + `Field.shader` (deep-field sprite shell); `BlackHole.shader` port; P4 harness (`Cosmic/Verify/P4 …`, `tools/mcp/rework/p4_*.cs`) | CS-142, CS-143 | done (unrun) |
| CS-145 | TERM | Rework P4 compile and bake: compile, **Cosmic → Verify → P4 Bake** (Bake All twice, GUIDs stable, point counts 6400/4000/8320 for the Milky Way, seven nebulae at 28 000, web at 40 000, every `points_*.mat` uninstanced on `Cosmic/Points`) | CS-144 | done |
| CS-146 | TERM | Rework P4 build: **Cosmic → Verify → P4 Build** (Layouts, Bodies, Places twice; milky_way `galaxy` child with three layers, helix `volume`, solar_system_planets with Rig, Orbit, ten anchors, saturn `rings`, Sun on the star) | CS-145 | done |
| CS-147 | TERM | Rework P4 play: **P4 Setup → Enter Play → Run → Leave Play → Teardown**: command buffers on the camera, galaxy spins, Rig through all four layouts, orbit rings at AfterForwardAlpha, sun touch by mouse, restore on the galaxy; `[P4] DONE n/m` with no FAIL | CS-146 | done |
| CS-148 | TERM | Rework P4 parity: scene-view captures of the Milky Way, Andromeda, one nebula, the cosmic web and the solar row against the old build's screenshots; `_Age` speed, sprite sizes and colours read the same; owner accepts the seed-regenerated Milky Way or asks for a baked-buffer fallback on `Galaxy` | CS-147 | todo |
| CS-149 | TERM | Rework P4 device budget: APK with the rework places loaded one at a time, OVR Metrics per place (frame time, GPU), every place under 13.9 ms on Quest 3 with hands; stereo confirmed on device for `Points`, `Orbit`, `Planet`, `Sun` (both eyes) | CS-148 | todo |
| CS-150 | CC | Rework P4 debt: `WebSource.cs` 294 lines, `Orbit.cs` 312, `Layouts.cs` 322, `Field.cs` 336, `Editor/Content.cs` 561 and `Bodies.cs` 574 against the ~250 budget; editor scripts at 9 against the table's 6 (Bake split three ways, Bodies); `Content/Field.cs` reads plates through a blit at enable; planets do not spin (only the Sun did in the old build, so nothing is lost yet); Jupiter's cloud-band rig and the Neptune/Pluto glows were dropped for mobile weight (reversible); the sun's lens-flare card size is a chosen number; `dock.prefab` and `panel_body.prefab` renumber every internal fileID on each **Cosmic → Build → UI** (content identical, ~5 000-line diffs), so a UI rebuild should be committed only when the content changed | CS-144 | todo |
| CS-151 | CC | Rework P5a: `UI/Label.cs` (card, name and moon styles, leader, hover, selected), `UI/Panel.cs` (body, scene, moon variants, side placement, text scale), `UI/Toast.cs` (notices and the two hint cards), `UI/About.cs` (credits, licence, version) | CS-141 | done (unrun) |
| CS-152 | CC | Rework P5b: `UI/Dock.cs` + `Tile.cs` (seven tiles on a 1.2 m curve, active underline, hover lift, press depth, passthrough tile, drag bar as a Grabbable, palm-up and Tab show/hide, recenter at 0.55 x head height, hotkey relay), `UI/Popup.cs` (layout choice), `UI/Utility.cs` (scale, mute, narration, text size, about) | CS-151 | done (unrun) |
| CS-153 | CC | Rework P5c: `Editor/Ui.cs` + `Cards.cs` menu **Cosmic → Build → UI**: `theme.asset` (Selawik) and nine prefabs under `Assets/Cosmic/Prefabs/ui/` (dock, three panels, three labels, toast, about) | CS-152 | done (unrun) |
| CS-154 | CC | Rework P5 harness: `Cosmic/Verify/P5 Build · Setup · Enter Play · Run · Leave Play · Teardown` and `tools/mcp/rework/p5_*.cs` | CS-153 | done (unrun) |
| CS-155 | TERM | Rework P5 compile and build: compile, **Cosmic → Verify → P5 Build** (nine prefabs, theme with Selawik, dock with seven tiles, popup, utility, one collider on the bar; second run keeps GUIDs) | CS-154 | done |
| CS-156 | TERM | Rework P5 play: dock recentres in front of the eye and tilts 25 degrees, Tab and palm-up toggle it, a tile click raises Picked, the Solar System tile opens the popup 40 mm above it, the utility window changes text size, a body panel sits 30 mm off Earth on the view-centre side, a card label grows on hover; `[P5] DONE n/m` | CS-155 | done (`[P5] DONE 20/20`, 13 Sep, after the input flush and two real fixes) |
| CS-157 | TERM | Rework P5 look: offscreen render of every UI prefab against the Figma frames; owner signs off the dock, panel and tag before P6 | CS-156 | todo |
| CS-158 | CC | Rework P6a: `Core/App.cs` (boot, singleton, dock and hotkey routing), `Core/Director.cs` (open a place: unload, room, instantiate, grow-in, panels, narration, ambience; layouts, restore, scale, pull by key, destination overlays in Halo) | CS-152 | done (unrun) |
| CS-159 | CC | Rework P6b: `Core/Anchor.cs` (the intro: our own logo text, floor placement by ray or mouse, the content root; Escape skips), `Core/Panels.cs` (scene, body and moon panels on grab or pull, name labels on the orbit model, destination tags on the Milky Way at the old placements), **Cosmic → Build → Main Scene** in `Editor/Scene.cs`, the P6 harness and `tools/mcp/smoke.cs` rewritten against it | CS-158 | done (unrun) |
| CS-160 | TERM | Rework P6 compile and build: compile, **Cosmic → Verify → P6 Build** (main.unity with App, Director, Anchor, Panels wired, seven places, one camera, first in Build Settings) | CS-159, CS-155 | done (`[P6] DONE 26/26`, 13 Sep) |
| CS-161 | TERM | Rework P6 smoke: **P6 Setup → Enter Play → Run (smoke.cs) → Leave Play → Teardown**: boot, intro skipped and placed by mouse, Milky Way first, every dock place opens with its room mode, Helix overlay in Halo, Escape and R; console clean | CS-160 | done (`[P6] DONE 27/27`, console clean, 13 Sep) |
| CS-162 | TERM | Rework P6 walkthrough on Link and Quest: intro by hand (logo, pinch on the floor), hint cards, dock by palm-up, every place, a destination by tag, restore; owner signs off the app before the cutover | CS-161, CS-131 | todo |
| CS-163 | CC | Rework P7a: the cutover inventory — `tools/cutover/inventory.py` writes `keep.txt` (everything under `Assets/Cosmic`, the samples, XR settings, fonts, TMP, Resources, build scripts, and every content folder whole: audio, Textures, models, ui, _sources) and `delete.txt` (the old tree: scripts, prefabs, scenes, materials, shaders, data, scriptable objects, animations, timelines, external, third_party, playables) | CS-159 | done |
| CS-164 | TERM | Rework P7b: the cutover, only after CS-162 is signed off — delete every path in `tools/cutover/delete.txt` (with its `.meta`), remove the old scenes from Build Settings so `main.unity` is the only one, compile clean, **Cosmic → Build → Bake All / Layouts / Bodies / Places / UI / Main Scene** in that order, then the P6 smoke green again | CS-162, CS-163 | todo |
| CS-165 | TERM | Rework P7c: the APK — **Cosmic Simulation → Quest 3 → Configure Project** (keeps `Assets/build_scripts` as the one build tool; bundle id `com.jdmaxilius.cosmicsimulationxr`), **Build APK**, install, and the device pass: intro by hand, every place, stereo on both eyes for Points, Orbit, Planet and Sun, OVR Metrics under budget | CS-164, CS-149 | todo |
| CS-166 | CC | Rework P7d: docs cutover — `docs/TECHNICAL_OVERVIEW.md` rewritten for the Cosmic tree (38 runtime scripts, 11 builders, 17 shaders, one scene), `CLAUDE.md`'s key-code section repointed, `tools/mcp/smoke_legacy.cs` removed, `RULES.md`'s table settled at the landed counts | CS-164 | todo |
| CS-167 | CC | Every moon registers one `SphereCollider` with two interactables — `body_moon`, `body_phobos`, `body_deimos`, `body_ganymede`, `body_callisto`, `body_io`, `body_europa`, `body_titan`, `body_mimas`, `body_iapetus`, `body_enceladus` each warn twice on load ("a collider used by an Interactable object is already registered"); XRI keeps the first association and drops the second, so one of the two components is inert on every moon | CS-144 | todo |
| CS-168 | TERM | Run **Cosmic Simulation → Verify → App Check** against `main_scene` and fix what it finds: every dock tile clicked, the room mode each asks for, a Milky Way destination tag clicked, Escape, restore, console clean | — | doing (written and compiled; not yet run) |

**Terminal run, 13 Sep 2026 — CS-128, CS-129, CS-130, CS-156, CS-160 and CS-161 all pass, and four real
defects had to be fixed to get there.** The editor was opened on this project for the first time in this
session and refused to finish loading: it raised the *Enter Safe Mode?* dialog, because `Cosmic.Runtime`
did not compile. **`EndPointType` is not nested in `NearFarInteractor`** — it is a top-level enum in
`UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals`, so `Anchor.cs:112` was a compile error in the
P6 code as committed. One `using` and one name fixed it; CS-158 and CS-159 had been written without a
compiler, and this is the first thing a compiler said.

**`rig.prefab` was built before the two assets it wires existed, and nothing noticed for four phases.**
`Room.dimMaterial` and `Audio.library` both read `{fileID: 0}` in the committed rig, because CS-139 ran
before CS-128 ever did. The main scene nests the rig, so `Room: dimMaterial is unassigned, so dimming and
halos are off` fired on every play start and the dim and halo quads were never built — invisible to the
P6 assertions, which read `Room.Effective`, a state, not a rendering. Running **Cosmic → Verify → P2
Setup** (CS-128) made `audio_library.asset` and `room_dim.mat`, **Cosmic → Build → Rig** wired them
(`[P3] DONE 22/22`, and its own `rig inputs:` line now names both), and the main scene had to be rebuilt
after that to pick up the new rig. **Order matters and is not recorded anywhere else: P2 Setup, then Rig,
then Main Scene.**

**The intro could never be completed, by hand or by harness, and this was a product defect.** The Earth
pin the intro parents to `app` kept its colliders. It sits between the eye and the floor, its `Grabbable`
and `Pull` are deliberately disabled, and so the placement ray hit the pin, hovered nothing, and the
floor was never selected — `Anchor.Preview` needs a hover the pin itself was preventing. `Editor/Scene.cs`
now disables every collider under the pin, the way it already disabled the two interaction components.
A probe run against the live editor is what found it: the ray was aimed correctly and the button was
down, and the hit came back `pin`.

**The harness's synthetic input never reached the game, which is the real story behind CS-156's
flakiness.** `InputSystem.QueueStateEvent` only queues; with the Game view unfocused — which is every
relay-driven run — nothing flushes the queue, and `Mouse.current.position` kept reading `(0, 0)` through
a whole run. `Verify.Send` and `Verify.Tap` now call `InputSystem.Update()` after queueing, which is what
*Lock Input to Game View* does for a person sitting at the editor (that setting cannot be reached from
script: `InputEditorUserSettings` is `internal` in the input package). With the flush in, the two label
assertions that passed in run 2 and failed in runs 3 and 4 of the 12 Sep session now pass every time.

**Two more findings in the P5 run, one harness and one product.** The harness clicked `dock.Tiles[4]`,
which is `solar_system` — a place with `layouts: []` — and then asserted that a layout popup opened;
it now finds the first tile whose place has `HasLayoutChoice` (`solar_system_planets`) and says which
one it used. And `Utility.OnTextSize` called `Prefs.NextTextScale()` and threw the answer away: that
method only *reads* the next step, and the button has to write it, the way `Hotkeys` already did. The
utility window's text-size button did nothing at all until this commit.

**Scores.** `[P2] DONE 30/30`. `[P3] DONE 22/22` (rig rebuild). `[P5] DONE 20/20` — CS-156, previously
`blocked-term`. `[P6] DONE 26/26` (build) and `[P6] DONE 27/27` (smoke), with the destination tags at 9
and the room mode correct for all seven places. The P6 build check is 26 items, not the 23 the harness
README predicts.

**`main.unity` is not byte-stable across builds either.** A rebuild from the committed tree passes
`[P6] DONE 26/26` and still rewrites 677 of its lines — the same objects with renumbered fileIDs, the
churn already recorded for `dock.prefab` and `utility.prefab`. The scene's GUID is stable, which is
what the build check asserts; the file's contents are not, so a rebuild always shows as a diff.

**Console after the final smoke run: clean except one class of warning, now CS-167.** Eleven moons each
warn twice that their `SphereCollider` is already registered with another interactable. It is content,
not the app layer, and it did not affect any assertion.

**Unity crashed after the last P5 teardown**, on OpenXR shutdown (`XR_SESSION_STATE_EXITING` is the last
line in `Logs/Editor.log`); the run had already reported `20/20`. The harness fixtures it left in
`Assets/scenes/development_scenes/solar_system_prefab_scene.unity` were reverted by hand rather than by
`P5 Teardown`, which never got to run. Its untracked `solar_system_prefab_scene/` lighting folder is
left on disk, uncommitted.

**What is still owed on P6 and P7, and by whom.** CS-162 is the Link and Quest walkthrough and the
owner's sign-off — it needs a headset and a person, and CS-131's wiring step (narration and ambience are
null until then) sits in front of it. CS-164, the cutover, deletes 1097 paths and is explicitly gated on
that sign-off; CS-165 needs CS-164 and a device; CS-166 follows CS-164. None of them were touched here.

**CS-168 (new, 13 Sep 2026): the shipping app gets the rework's acceptance walk.** The one thing the
rework had that `main_scene` did not was an executable check — every defect found this session was found
by one, and the product scene had none. `Assets/scripts/Editor/AppCheck.cs`, menu **Cosmic Simulation →
Verify → App Check**, with the relay wrapper `tools/mcp/legacy/app_check.cs`, walks the shipping app:
the director and dock are present; a place asked for during the intro ends the intro and opens (the fix
in `f0b5bc22`, now permanently asserted); **every dock tile is clicked at its own screen position** and
has to open its module and set the room mode the module asks for; the Milky Way is opened and a
**destination tag is clicked with a real mouse**, which has to open that destination; Escape closes it;
R restores; and the console has to have stayed clean. The verdict goes to `Logs/app_check.log` as well as
the console.

Three shapes are carried over from the rework's harness deliberately: every queued input event is followed
by `InputSystem.Update()`, because with the Game view unfocused nothing flushes the queue and the device
keeps reading `(0, 0)` — the cause of a whole session's phantom failures; tiles are **clicked**, not
`Choose()`d, because calling the handler tests the handler and the complaint is about the click; and the
verdict is written to a file, because the console holds 200 entries and one chatty component buries a run
before anyone reads it.

**Not yet run.** The editor had an unsaved `DemoScene` open, and opening `main_scene` over it would have
risked the owner's unsaved work, so the walk is written, compiled and waiting.

**CS-131, the product-side audit (13 Sep 2026).** With the legacy app as the product, the question CS-131
really asks is whether `Assets/data` — the assets the shipping app reads — is missing any reference. All
46 were checked field by field:

- **`Narration`: 8 unset of 46** — `experiences/solar_system_planets` and the seven `hd110067_*` bodies.
  No clip exists on disk for any of them, so this is **CS-071/CS-072 work (record the lines), not wiring**.
  Everything else, all 38 of them, is already wired.
- **`DockThumbnail`: 11 unset of 18** — the four new experiences (`hd110067`, `pinwheel`, `triangulum`,
  `whirlpool`) and the seven nebula destinations. `Assets/ui/thumbnails/` holds exactly seven PNGs and all
  seven are wired. The destinations are not dock tiles — the dock carries seven, the same seven — so only
  the four experiences are a real gap, and they need **artwork that does not exist**, not a reference.
- **`ContentPrefab`: 3 unset** — `milky_way`, `sagittarius_a`, `solar_system`. By design: those three load
  a view scene through `SceneName` instead of spawning a prefab.
- **`Ambience`: 46 unset of 46** — nothing anywhere has an ambience bed, which is **CS-077**, already open.

**So there is nothing left to wire in the product's data.** Every reference that an existing asset could
satisfy is satisfied; what remains is eight narration clips and four thumbnails that have to be made.

**Direction change, 13 Sep 2026, owner's call: the legacy `main_scene` app is the product, and the Cosmic
rework stops being the shipping path.** The fixes found while verifying the rework are to be carried into
the legacy app where they apply. Two of the three do not transfer as diffs: the data wiring (CS-131) was
copied *out of* the legacy `Assets/data` assets, which have always had those 45 references, and the
input and UI fixes were Cosmic-side code. What transfers is the bug class, and the first one found a real
defect in the product.

**The legacy app swallowed every click for as long as its intro ran, and on desktop that is most of a
minute.** `ExperienceDirector.Switch` refused outright while `IntroRunning`, answering with a console line
and a `SwitchNotice.Busy`. On desktop `PlacementControl` bypasses placement and fires `OnContentPlaced`
only after the onboarding narration has finished playing plus `DesktopDuration` (2 s), and the intro is
not over until the flow reaches `kGalaxyView` after that. Until then every dock tile and every destination
tag did nothing. The 45 s watchdog was the only way out, and it exists precisely because this reads as a
dead app. **Now a click ends the intro instead of being thrown away:** `IntroFlow.Skip()` jumps the flow
to its last stage through `FlowManager.JumpToStage`, which raises `OnIntroFinished` on the normal path, and
the place the player asked for is opened once the intro's own galaxy load has settled — the same wait
`OpenStartModule` already does, so the two cannot stack. Verified live on `main_scene`: with
`IntroRunning=True` and nothing open, asking for `milky_way` skipped the intro and left `Current=milky_way`
with a clean console.

**What the Milky Way's destination tags look like from a mouse, measured in the same session:** the open
galaxy view carries `destination_tags` with **12 children, every one with a `BoxCollider` and all of them
on screen**, and `DesktopMouseInput` is present and enabled with its own `GEPointer` created at runtime.
Synthetic mouse events do reach the device in this app. An end-to-end tag click was **not** confirmed: the
play session was restarted from the editor several times mid-measurement, and no run survived long enough
to click a tag and read the result. That is the next thing to check, and it wants the editor to itself.

**CS-131 and CS-134, 13 Sep 2026.** `Assets/Cosmic/Editor/Migrate.cs`, menu **Cosmic → Build → Wire Old
References**, carries the references the copy deck cannot own from the old ScriptableObjects into the
generated ones, matched by id: **45 wired** across 20 places and 28 bodies — the 38 `Narration` and 7
`DockThumbnail` the gate named, and every old id had a generated asset to land on. `Cosmic/Import Copy`
never clears a field it does not own, so a re-import keeps them. The file is transitional: it reads
`Assets/data`, which the cutover deletes, so **CS-166 removes it** along with the rest.

**The gate went 64 → 19 → 0, and the last 19 were not losses.** Every one was either `ContentPrefab`
(18) or the `Layouts` count (1), and both are the rework doing what it set out to do: the new place
prefab is named for the place id and replaces the legacy `*_content_prefab` / `nebula_*` the cutover
deletes, and `solar_system_planets` gained `solar_schematic` and `solar_realistic` after the old pair.
Wiring the old prefabs in would have pointed `Place.content` at the tree being deleted. So
`check_data.py` now reports those two shapes as **notes** rather than losses, and only in that exact
shape — a ContentPrefab whose new name is not the place id is still a loss, and a layout list that does
not start with the old one is still a loss. **1010 values checked, 0 losses.** This is a judgement call
about what the gate means, and it is recorded here rather than left in the diff.

*CS-132 done, unrun (13 Sep 2026).* Deck only; no importer change was needed. Seven body blocks and four place blocks ported verbatim from the old assets, plus the wiring fields the importer always read and the deck never carried: `**Bodies:**` on `solar_system` and `hd110067`, `**Destinations:**` on `milky_way` (nine, per GDD 4.3). All four new places are Dimmed, read from the old assets, so no `**Room:**` line. A Python re-implementation of the parser resolves 10/10, 7/7 and 9/9 ids. **Two things ported faithfully rather than fixed, for the owner:** the HD 110067 prose exceeds the deck's word caps (recorded as an exception in the README), and its four measured masses lack the `x 10` mantissa suffix the other bodies carry, so they will render as a bare mantissa with a superscript until the value text is changed — changing it would also change what the parity gate compares against. Terminal: **Cosmic → Import Copy**, then `check_data.py` should drop from 11 missing assets to 0 and `Wire()` should report references wired; then re-run **Cosmic → Build → Layouts** so the two HD 110067 layouts fill their slots.

*CS-137 done, unrun (13 Sep 2026, commit `30d0247`).* One interactable per logical object, on its root with the child colliders listed — the structure that makes the old bubbling layer, its forwarder and the "handler must not implement the interface" rule all unnecessary. Fifteen toolkit members could not be verified against package source (not vendored here); each is contained so a wrong name is a two-line fix, and the phase notes in the commit list them. One structural deviation, recorded in `Pull.cs`'s single comment: the toolkit writes the transform every frame while an interactor selects, so the interactable is disabled for the flight and re-enabled on arrival, which also hands it back to a pinch still held. The bus gained `Loop(Sfx)` so the beam sound comes from the library. The spawn order every later builder must honour: instantiate → `CaptureHome()` → grow-in.

*CS-138 done, unrun (13 Sep 2026, commits `e213111` and the review fix after it).* The desktop pointer is an `XRRayInteractor` that is also its own `IXRInputButtonReader`: the first cut queued a manual button state every frame, which XRI only applies on the frame *after* it was queued, so a component that re-queues every frame at execution order -31000 never clicks anything. The rig builder wires select, UI press, activate, manipulation and both poke poses from the actions asset, and gives the mouse a `Content` pivot under the rig root so orbit, pan and zoom have something to move; Phase 6's anchor takes that object over. The Desktop map lost `Orbit`, `Pan` and `Zoom`: the mouse reads its own device and nothing read them, so they were unrebindable decoration. The review of the whole phase against the toolkit source found eight runtime defects and no compile errors, all fixed in the same push: registering the upright transformer had silently suppressed XRI's default one (nothing followed the hand and the clamp was a no-op), `MinMetres` gated on the max field, `Restore` fought a live selection, `Placed` compared parent-local units to metres, dwell re-armed on a loose object (a placed planet flew back unasked), a pinch still held at arrival was not handed the object back (it is now re-selected through the manager), a pull started at zero scale could never arrive (a three-second flight cap), and Shift+wheel over a hovered body did nothing. Recorded for the owner: `autoReturn` is the one exception to "nothing snaps back" (RULES.md) and is off by default until CS-140 signs it off; Whirlpool's old disc tilt degenerates to 90 degrees and Phase 4 will not copy it blind.

*CS-133 done, unrun (13 Sep 2026, commit `183f66f`).* `Assets/Cosmic/Editor/Content.cs`, menu **Cosmic → Build → Layouts** — the first part of the plan's single data-driven content builder. Every position, rotation and scale of all four layouts matches the old assets to seven decimal places, checked by reimplementing the arithmetic in Python; slots hold a `Body` object reference rather than a string id. Two derivations are typed on purpose and the file says so: Relative Size uses the GDD's own diameter table (scaling from `diameterKm` puts Mars and Jupiter outside parity tolerance and loses Pluto's deliberate 5 mm), and the two ring spans, because the generated bodies carry no ring figure yet. **Expected on the first run, not a regression:** the two HD 110067 layouts build with zero slots and log one `no body asset` error per body until CS-132's bodies are imported; re-run after CS-132 and they fill with no code change. Terminal: compile, run twice (second run logs `updated`, no GUID churn), then `python3 tools/parity/check_data.py --kinds layouts --skip 'hd110067.*'` should report zero losses for `solar_row` and `relative_size`, and `solar_system_planets.asset` should hold both in that order. Saturn's rings 2.5 mm from Jupiter, by hand, as the original builder recorded.

*CS-132 and CS-133 — run in the editor, both pass (13 Sep 2026, terminal session).* Order matters
and is the whole trick: **import first, then build layouts.** Both notes above predict the two HD
110067 layouts building with zero slots and one `no body asset` error per body on a first run; that
is only true if Layouts runs before Import Copy. Run the other way round there is no error at all
and both fill immediately.

- **Cosmic → Import Copy** — `Generated` goes **37 → 48 assets**, exactly the +11 the deck gained.
  All 15 previously-missing assets now exist.
- **`Copy.Wire()` finally wires.** It reported **0 references** every run before this, because it
  reads `Bodies:` and `Destinations:` fields the deck had never carried. `solar_system` now holds
  **10** body references and `milky_way` **9** destinations, per GDD 4.3.
- **Cosmic → Build → Layouts**, run twice — all four layouts build and **every GUID is unchanged**
  across the two runs, so the builder is idempotent as RULES.md requires. `hd110067_row` and
  `hd110067_relative` carry **7 slots each**, not 0. `solar_system_planets` holds `solar_row` then
  `relative_size`, in that order.
- **`check_data.py --kinds layouts`: 152 values, 0 losses.** CS-133's gate passes outright.
- **The whole gate: 68 → 60 losses, and values checked 573 → 951.** Every remaining loss is a single
  class — 38 `Narration`, 15 `ContentPrefab`, 7 `DockThumbnail`. The counts rose from 34/11 because
  the four new places brought their own references. Nothing else is lost: no missing asset, no room
  mode, no layout, no value mismatch.

**So CS-134 is now blocked on CS-131 alone.** The wiring step is the last thing between the data
layer and a passing gate.

*CS-137 — compiles clean (13 Sep 2026, terminal session).* Not its gate, which is CS-139 and CS-140,
but worth recording because its own note flags **fifteen toolkit members that could not be verified
against package source**, XRI not being vendored here. All fifteen resolve: `Grabbable.cs`,
`Pull.cs` and `Content.cs` compile with no errors against the packages actually installed. The
two-line-fix contingency the note describes is not needed.

**Still open from CS-132's own note, for the owner rather than the terminal:** the HD 110067 prose
exceeds the deck's word caps (recorded as an exception in `docs/copy/README.md`), and its four
measured masses lack the `x 10` mantissa suffix the other bodies carry, so they render as a bare
mantissa with a superscript. Changing the value text also changes what the parity gate compares
against, so it is a deliberate decision, not a cleanup.

*CS-158 and CS-159 done, unrun (13 Sep 2026, written directly).* The app layer is four components on one `app` root. `App` owns the singletons and routes: the dock's `Picked`, `LayoutPicked`, `Recentered`, `HelpRequested`, `AboutRequested` and `Scaled`, the hotkeys' `Restore`, `Body`, `Moon` and `Close`, and the director's `Changed` back to the dock. `Director` is the old `ExperienceDirector` with everything scene-based gone: a place is its content prefab under the anchor's content root, and a switch is stop voice and ambience, clear panels, destroy, set the room, instantiate, capture homes, grow in over 0.6 s, bind panels, say the narration, start the ambience. A destination is the same prefab instantiated 1 m ahead in Halo with the map faded to 20 percent; Escape or the dock closes it. `Anchor` is the intro: our own name as world text for 5 s (the reference app's logo is not ours to show), then a floor the player clicks or pinches to place the content root, a 45 s timeout that recentres instead, and Escape to skip. `Panels` opens a body panel on grab or on pull arrival, a moon panel for moons, name labels on the orbit model, and destination tags on the Milky Way at the old builder's placements scaled to the new disc. The main scene is written by **Cosmic → Build → Main Scene** and registered first in Build Settings. The desktop dock now parks inside the frustum (0.7 of the half-height at 0.75 m), which is the cause the terminal measured in CS-156. Not done here: the hint cards' art is still the two onboarding sprites; narration and ambience references wait on CS-131; the asteroid belt label and the orbit-model highlight are not built. Runtime scripts stand at 38.

*CS-151 to CS-153 done, unrun (13 Sep 2026, written directly, no agents).* Eight runtime files and two builders, all on native UGUI over the XR UI input module: a world-space canvas per prefab at one unit per millimetre, `TrackedDeviceGraphicRaycaster` beside the graphic raycaster so hand rays, pokes and the mouse reach the same buttons, and every component initialising lazily from its public entry points. Decisions: the dock is one object in both modes and parks itself 0.75 m ahead at the greater of 0.7 m and 0.55 x head height (GDD 11), so the desktop eye at 1.36 m gets it at 0.75 m; the drag bar is the dock's own `Grabbable` (Limits.Fixed, upright) whose only collider is the bar, and release re-tilts the dock toward the head; palm-up reads the left palm joint from the XR Hands subsystem and toggles after 0.5 s (sign of the palm normal is a serialized threshold to flip if it reads inverted on device); `Hotkeys.Place` is relayed through the dock so Phase 6 listens to one `Picked` event for tiles and F-keys alike; the utility window carries the About button because the GDD gives About no other home in the headset; the hint cards are the two onboarding sprites and the two lines from the old `hint_cards.asset`, hardcoded in the builder since the old type is in the legacy assembly; panels have no text outline yet (a TMP outline needs a per-font material, which is CS-157's look pass). Runtime scripts now stand at 34 against the table's 29 and editor at 11 against 6; both recorded, neither hidden.

*Follow-up to the terminal's CS-145 to CS-147 run (13 Sep 2026).* Three of its findings are closed from the repo. `tools/mcp/compile.ps1` now asks for `CompilationPipeline.RequestScriptCompilation()` after the refresh, so a build that already failed cannot hide behind a no-op refresh, and it reports **COMPILE STALE** when `Library/ScriptAssemblies/Cosmic.*.dll` is older than the newest script under `Assets/Cosmic` — the exact case that printed CLEAN three times. `P3 Setup` and `P4 Setup` park a host scene's `MainCamera` instead of failing on it, and both Teardowns wake it and remove the rig instance they made, so the host scene is left as it was found. `Points.Instance(layer)` exposes the per-layer material instance and the P4 run now asserts `_Age` advances; the expected verdict is `[P4] DONE 28/28` with one SKIP (the alpha-0 draw, which is a capture, not a check). The `LayoutKind` shadowing the terminal fixed is the kind of collision RULES.md's "file name is the type name" invites with a System name; recorded here so the next enum gets a look-up first.

*CS-141 to CS-144 done, unrun (13 Sep 2026).* Phase 4 is code-complete and every file has been read against the old sources; none of it has compiled. The whole wave went through one adversarial review that found twelve behaviour defects and no compile errors (fixed in `3ee1660`: rings scaled twice, orbit rings left drawing over a row, bodies teleporting into orbit, moons left behind by a grabbed planet, the sun's flare scale on the wrong transform, the ring clamp treating Venus as ringed, the Milky Way losing its dust-lane winding and its 95-degree second arm, the schematic sun at Mercury's size, nebula radius never reaching the draw, a missing include guard). Three conventions were settled after the builders started and are applied in the builder by hand: moon anchors `home_<moon>` live under `body_<planet>` and Rig registers them as riders; `Sun.flareQuads` names the twelve quads; `Points.Layer.ellipseRadii` is per layer. The harness (CS-145 to CS-147) caught that `Copy.cs` never wrote `Body.kind`, so the sun was a planet and Rig had no star to orbit; the deck now carries `**Kind:**` and moons are `BodyKind.Moon`. Owner decisions recorded rather than taken: Whirlpool's disc tilt is posed at its 20-degree inclination instead of the old degenerate 90; the cosmic web bakes 40 000 points (the shipped prefab overrode to 120 000); the black hole material is queued at 3000 rather than the shipped opaque 2000; `Field` scatters with `System.Random`, so the deep field is deterministic but not sprite-for-sprite the old arrangement. The old tree is untouched.

*CS-139 follow-up (13 Sep 2026).* The rig on `main` was built before the review fix landed in `54b1bd0`; the builder now also wires each near-far interactor's activate and manipulation inputs, both poke poses from the actions asset, and a `Content` child as the mouse's view pivot. The owner rebuilt it in `6f821ac`, so the prefab on `main` now carries all of that. The P3 harness is in: `Cosmic/Verify/P3 Build Rig · Setup · Enter Play · Run · Leave Play · Teardown` with `tools/mcp/rework/p3_*.cs` and the README section, 22 rig assertions and 31 play-mode ones, and it turns `autoReturn` on for its own body so the stray test is real.

*CS-139 — done (13 Sep 2026, terminal session).* Compiles clean, and every assertion in the
acceptance line passes against the prefab read back off disk rather than the build's own report.

- `Assets/Cosmic/Prefabs/rig.prefab` exists, and **Cosmic → Build → Rig** run twice leaves the same
  GUID, so the builder is idempotent as RULES.md requires.
- Exactly **one** `XRInteractionManager`, **one** `EventSystem`, **one** `XRUIInputModule` on it,
  **one** `Mouse`, **one** `Hotkeys`. The "one of each" part of the line is the whole point of it —
  a second event system is the failure this ticket exists to catch, and there is not one.
- Both hands are complete: `NearFarInteractor` with its curve visual, a poke interactor on its own
  pose, `XRHandTrackingEvents` / `XRHandSkeletonDriver` / `XRHandMeshController`, and the full
  26-joint skeleton under each wrist.
- `Main Camera` carries `Camera`, `AudioListener`, `TrackedPoseDriver` and `ARCameraManager`;
  `AR Session` is present and **disabled**, which is correct for the desktop mode.
- `XROrigin` is written through `SerializedObject` deliberately (`Scene.cs:83`) because the setters
  move the offset and poke the XR subsystem.

One thing worth knowing for anyone reading a hierarchy dump of this prefab: the mouse node prints as
`Mouse [Transform Mouse]` with no interactor component beside it, which looks like a missing
interactor and is not. `Mouse` **is** the interactor — `Mouse : XRRayInteractor` (`Mouse.cs:11`) —
so the derived name is all that shows.

**CS-140 is the owner's, not the terminal's.** It is a feel test — pinch, dwell, two-hand scale,
restore tween, mouse parity — and its acceptance line ends "owner signs off on feel". It needs a
headset or a Link session and a human judgement that nothing here can stand in for. Note also that a
default Link session cannot verify stereo work at all (Android ships Single Pass Instanced, Windows
multi-pass), so anything that looks right on Link still owes a device run.

*CS-145, CS-146 and CS-147 — all three pass (13 Sep 2026, terminal session).* `[P4] DONE 40/40`,
`17/17` and `26/26`, the last of those eight times running. Two defects had to be fixed first and
both are described below, because neither was visible from reading the code.

**The compile was never clean — and `compile.ps1` said it was, three times.** `Cosmic.Runtime` had
three real errors: `LayoutKind` does not contain `Sequential`, at `OrbitRings.cs:10`, `:18` and
`PointCloud.cs:8`. The rework declares its own `Cosmic.LayoutKind` (`Row/Relative/Schematic/
Realistic`) in `Data/Layout.cs`, and inside `namespace Cosmic` that shadows
`System.Runtime.InteropServices.LayoutKind`, so `[StructLayout(LayoutKind.Sequential)]` resolved to
the wrong type. Fixed by fully qualifying at the three use sites. **A `using LayoutKind = …` alias
at file scope does not work** and was tried first: a file-level using alias loses to a member of the
enclosing namespace, so the error survived it unchanged.

**Why the harness could not see this, which matters more than the bug.** `compile.ps1` reports
`error CS` lines written to `Logs/Editor.log` *since its own refresh*. When a compile has already
failed, Unity keeps the last good assembly and does not recompile on a refresh that changes nothing,
so no new error is logged and the script prints `COMPILE CLEAN`. **It cannot tell "compiled cleanly"
from "did not compile at all".** The tell is that `Cosmic/Verify/P4 Bake` did not exist while
`Cosmic/Verify/P0 Compile Check` — declared in the same file — did, which is a stale assembly, not a
missing menu item. The durable check is the assembly timestamp: `Library/ScriptAssemblies/
Cosmic.Editor.dll` was an hour older than `Verify.cs`. Compare those two before trusting a clean
report, and note that `EditorUtility.RequestScriptReload()` does **not** help — it reloads the domain
against the assemblies already built. `CompilationPipeline.RequestScriptCompilation()` is the one
that rebuilds.

**`Rig` left `Orbit.Realism` stale, and it is a real defect, not test noise.** `Rig.Step` writes
`orbit.Realism` only inside `if (orbiting …)`, so applying a Row or Relative layout left it at
whatever the last Schematic or Realistic layout set. The visible consequence: go **Realistic → Row →
Schematic** and the first second of the Schematic view runs at the wrong realism, tweening down from
1 instead of holding 0. `Apply` now sets it for the non-orbiting layouts too, one line beside the
existing `Blend`/`Running` block.

The arithmetic that identified it, since the symptom looked like flaky timing: the run passed 26/26
on a fresh play session and 25/26 on every repeat, failing only
`a Schematic layout holds Orbit.Realism at 0` and reporting 0.339, 0.339, 0.340 — a drifting value,
which reads like a race. It is not. `solar_schematic.transitionSeconds` is 1, the assertion runs
0.3 s after the apply, and `EaseOutCubic(0.3)` is 0.657, so `Lerp(1, 0, 0.657)` is 0.343. The first
run passed only because a fresh scene starts at realism 0; every repeat started at 1. After the fix,
eight consecutive `26/26` in one session.

Two `[P4] SKIP` lines are expected and are not failures — the `_Age` advance and the alpha-0 draw,
neither observable from script while `Points` keeps its material instances private.

**Two things the harness asks for that its own README does not.** The recommended host scene,
`solar_system_prefab_scene`, has a `MainCamera`, and `P4 Setup` instantiates a rig that brings a
second one; the run then FAILs on `more than one active MainCamera`. Deactivate the host's camera
first. And `P4 Teardown` removes the `cosmic_verify` root but **leaves the rig instance in the host
scene** — about 9 900 lines of it, plus baked lighting data in a new folder beside the scene. The
scene was reverted here rather than committed; check `git status` for it after any P4 play session.

**CS-148 and CS-149 are not runnable from this machine.** CS-148 wants captures compared against the
old build's screenshots, and CS-149 wants an APK, OVR Metrics and stereo confirmed on the Quest.
Neither can be settled over Link: Android ships Single Pass Instanced and Windows multi-pass, so a
clean Link session says nothing about the eye that breaks.

*CS-155 — done (13 Sep 2026, terminal session).* `[P5] DONE 13/13`. Nine prefabs, the theme on Selawik,
the dock with seven tiles, one collider on the bar, GUIDs stable across a second build. Both are
committed, as the earlier generated content was.

**One compile error had to be fixed first, and the new guard is what found it.** `Verify.cs:1197`
declared a second `UiFolder` — `CS0102`. The two are genuinely different things: the P2 one is the
audio clip folder `Assets/audio/ui_audio_clips/` and the P5 one is `Assets/Cosmic/Prefabs/ui`. The
audio constant is renamed `UiClipFolder`, which also pairs it with the `MusicFolder` beside it; the
P5 one keeps the name it earned with ten use sites.

**`compile.ps1`'s new staleness check had a false positive, now fixed.** It computed "the newest
source" over all of `Assets/Cosmic` and compared it against *both* assemblies, so editing any
Editor-only file reported `Cosmic.Runtime.dll` stale when Runtime had correctly not been rebuilt —
which is most harness work, and it fired on the very first run here. Each assembly is now compared
against its own sources, `Cosmic/Editor` for the editor assembly and everything outside it for the
runtime one. The check itself is right and earned its keep the same session: it caught the `CS0102`
as `COMPILE FAILED` where the old script would have said CLEAN.

*CS-156 — does NOT pass; blocked, with one defect proved and the rest environmental.*
Best result **16/20**; across **five** runs on unchanged code the score was 13, 16, 14, 14, 15, so
**the run is not reproducible on this machine** and no score from it is a verdict. The fifth run was
made after the owner confirmed they were at the machine, so the "keep the real mouse off the Game
view" precondition was as good as it gets here.

**Five runs separate the deterministic failure from the flaky ones, which is the useful result.**
The four dock failures — the tile click, the popup that cannot open without it, the popup-position
check that then measures 223 mm, and the text-size button inside the utility window — failed in
**all five runs**. The two label assertions passed in run 2, failed in runs 3 and 4, and split in
run 5 (hover failed, click passed). So the dock four are a real defect and the label two are noise
until someone reproduces them on a machine that holds both preconditions.

**What passes every time, and is worth banking:** Recenter parks the dock 0.75 m ahead and at
`max(0.7, 0.55 x head)` = 0.748 m, the 25-degree tilt, the dock starting visible, Tab hiding and
showing it, **F2 relaying the first tile's place through `Dock.Picked`**, Escape closing the popup and
the utility window, the body panel fading in, sitting 30 mm off the sphere's edge and facing the
player, and `Room.Changed` never firing. The geometry and the keyboard path are sound.

**The measured cause, which is not flakiness.** With the camera at `(0, 1.360, 0)` looking level and a
60-degree vertical FOV, the visible half-height at 0.75 m is `tan(30) x 0.75` = 0.433 m, but the dock
parks 1.360 - 0.750 = **0.61 m below the eye**. Every tile projects to a negative screen Y (-136 to
-219 measured) and so does the utility window (-229). Both are simply **below the viewport**, so a
synthetic click can never land on them. The two controls that pass the pointer tests are the ones at
eye height: the label at y 1.360 and the panel at y 1.210 both project on-screen. That accounts for
four of the failures — the tile click, the two that cascade from it (no popup, so the popup-position
check measures 223 mm), and the text-size button inside the off-screen utility window.

**Whether that is a product fault or a harness fault is the owner's call and is not taken here.** In a
headset the player looks down and the dock is there; on the desktop at a level gaze it is off-screen
at rest, which either means the desktop pose needs to pitch down or look up at the dock, or the
desktop dock belongs higher. GDD 11's chest height is the reason it sits where it does.

**The unresolved part.** The label hover and label click passed once and failed on later runs with
nothing changed, and the Game view's own height changed between runs (1060 then 970 px), which moves
every projected point. The README asks for a visible Game view *and* the real mouse kept off it; the
first was missing until this session opened one — that alone took the score from 13 to 16 — and the
second cannot be guaranteed from here. **Do not treat the remaining label failures as defects without
a run on a machine that can hold both preconditions.**

**A second finding, from re-running P5 Build: two prefabs are not byte-stable.** `dock.prefab` and
`panel_body.prefab` come back with every internal **fileID** renumbered — 2 979 lines inserted and
2 979 deleted, and **zero** lines differing once long ids are normalised, so the content is
identical and only the ids moved. CS-155's own assertion covers *asset GUIDs*, which are stable, and
says nothing about fileIDs. The consequence is that every P5 Build produces a ~5 000-line diff for
those two files with no change in them; the churn was discarded here rather than committed. The
other seven prefabs are stable, so this is specific to the two most deeply nested ones.

**One experiment recorded because it failed.** Widening the camera FOV to 110 degrees put all seven
tiles on screen, but the run then scored 14/20 and broke the two label assertions that had just
passed. Changing the FOV mid-session perturbs the harness's own screen-point arithmetic instead of
isolating the variable, so it proves nothing either way. The direct `WorldToScreenPoint` measurement
above stands on its own and does not depend on it.

*CS-157 is the owner's.* It is an offscreen render of every UI prefab against the Figma frames and
ends in a sign-off on the dock, panel and tag.

*CS-126 — compile the Cosmic assemblies.* `pwsh tools/mcp/compile.ps1`, then
`node tools/mcp/umcp.js run tools/mcp/rework/p0_compile.cs`, then
`node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'`. **Accept:** zero `error CS####` lines from the
refresh; `[P0] PASS both Cosmic assemblies are present` (both `Cosmic.Runtime` and `Cosmic.Editor` in
`AppDomain.CurrentDomain.GetAssemblies()`); and `[P0] PASS the actions asset imports as an
InputActionAsset with both the XR and Desktop maps`, with the counts line reading `XR=10 actions,
Desktop=29 actions`. If the menu item does not exist, that *is* the failure — `Cosmic.Editor` did not
build; read the errors and stop here, because nothing after this means anything.

*CS-127 — import copy and run the parity gate.*
`node tools/mcp/umcp.js run tools/mcp/rework/p1_data.cs` (not in play mode), read the console, then from
the repo root run `python3 tools/parity/check_data.py`. The menu item runs `Cosmic/Import Copy` **twice**
and compares every generated asset's GUID across the two runs. **Accept:** `[P1] PASS a second import
left all N generated GUIDs unchanged` — a builder that duplicates or re-creates on the second run is a
bug per RULES.md — and a parity report whose remaining loss is only the predicted classes and nothing
else: unowned `Narration`, `ContentPrefab` and `DockThumbnail` references (the rework has no owner for
them until Phase 6 wiring), 3 room modes the copy deck does not state and which stay authored, 1 layout
(Copy writes none; `Generated/layouts/` is a placeholder folder), and 11 builder-owned assets. Any other
class of loss is a regression in `Copy.cs`, not an expected gap.

*CS-128 — Phase 2 setup.* Open a **saved** scene with a camera tagged `MainCamera` by hand first —
`Assets/scenes/development_scenes/solar_system_prefab_scene.unity` is the recommended host — because
nothing here calls `OpenScene` in Single mode, where the save prompt is modal and blocks the relay
outright, and because `SaveScene` on an untitled scene opens a file dialog for the same reason. Then
`node tools/mcp/umcp.js run tools/mcp/rework/p2_setup.cs`. **Accept:**
`Assets/Cosmic/Data/Generated/audio_library.asset` exists with every `Sfx` id wired to its CS-070 clip
under `Assets/audio/ui_audio_clips/` (`GrowIn` deliberately has none) and `musicByRoom` set per D-006
(Passthrough→`background_music`, Dimmed→`bgm_system`, Halo and Black→`bgm_galaxy`), mixer groups left
null; `Assets/Cosmic/Prefabs/room_dim.mat` exists on `CosmicSimulation/EnvironmentTint`, black at alpha
0; a root `cosmic_verify` in the open scene carrying `Cosmic.Audio` with its library assigned and
`Cosmic.Room` with its `dimMaterial` assigned (the AR fields stay null on desktop); and the scene is
**saved**, so the objects survive the play-mode domain reload. Re-running the step updates in place and
says `updated` rather than `created` — it is a builder, so it is idempotent.

*CS-129 — Phase 2 play-mode verification.* Switch Error Pause **off** in the console first: a FAIL is a
`Debug.LogError` and would pause play mode mid-sequence. Then
`node tools/mcp/umcp.js run tools/mcp/rework/p2_enter_play.cs` (it assigns `EditorApplication.isPlaying`
directly — the only thing that works through the relay, per CS-119 — so poll `isPlaying`, do not trust
the reply, and expect "Unity not detected" for a few seconds while the domain reloads), then
`node tools/mcp/umcp.js run tools/mcp/rework/p2_run.cs`, wait about fifteen seconds, then
`node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'`. **Accept: `[P2] DONE 30/30` with no `[P2] FAIL`
line anywhere.** The thirty checks, in order:

| # | Check |
|---|---|
| 1 | `room_dim_quad` is an active `MeshRenderer` after `Room.Set(Dimmed)` |
| 2 | its shared material reaches alpha 0.5 ± 0.05 within 0.5 s |
| 3 | `Room.Changed` fired exactly once entering Dimmed |
| 4 | exactly one of the two `music_*` sources is playing `bgm_system` |
| 5 | setting Dimmed a second time does **not** fire `Room.Changed` |
| 6 | the bed kept playing rather than restarting (same source, `time` advanced) |
| 7 | `Room.Set(Black)` → `Camera.main.backgroundColor.a == 1` |
| 8 | `Room.Changed` fired entering Black |
| 9 | 2.5 s later `bgm_galaxy` is playing at 0.35 ± 0.05 (the 2 s crossfade completed) |
| 10 | the outgoing music source stopped (quieter-source rule held) |
| 11 | `Room.ForcePassthrough(true)` → `Room.Effective == Passthrough` |
| 12 | it fired `Room.Changed` exactly once |
| 13 | in Halo, `Room.Halo(subject, 0.5)` gives a `room_halo_quad` renderer that is enabled |
| 14 | its alpha passes 0.9 within 0.7 s |
| 15 | `Room.Forget(subject)` destroys the halo quad |
| 16 | music sits at 0.35 ± 0.05 before the duck |
| 17 | `Say(clip)` sets `Audio.Speaking` |
| 18 | music ducks to 0.1925 ± 0.05 (0.35 × 0.55) within 0.6 s |
| 19 | `StopVoice()` clears `Speaking` |
| 20 | music recovers to 0.35 ± 0.05 |
| 21 | two unplaced `Play(Sfx.Select)` calls in one frame debounce to **one** voice |
| 22 | two placed `Play(Sfx.Select, t)` calls give **two** voices (spatial is exempt) |
| 23 | `Ambience(clip)` gives an `ambience` source that is playing and fading up |
| 24 | `Loop(clip)` returns a playing, looped source |
| 25 | `Release(source)` destroys it |
| 26 | `Prefs.Muted = true` → `AudioListener.volume == 0` |
| 27 | `Prefs.TextScale = 1.3f` reads back 1.25 |
| 28 | the mute preference is restored afterwards |
| 29 | `Tween.To` lands exactly on target after ~0.45 s |
| 30 | `Tween.To` never overshoots (max progress ≤ 1) |

The run also logs one `[P2] PlayerPrefs written:` line with the five `Cosmic.*` keys and their values, so
the key names are on the record rather than inferred from `Prefs.cs`.

**Listen and look — the owner does this part, no assertion can.** (a) Take the room from Passthrough to
Dimmed to Black and confirm the music crossfade is a clean **2 s** with no click and no restart when the
same state is set twice. (b) Under narration the duck must be clearly audible without swamping the voice.
(c) The dim must read as a soft grey veil over the room, not a hard-edged plate in front of the face —
frame the **scene view** at the player's eye (`SceneView.pivot`/`rotation` from `Camera.main`) and capture
it with `Unity_Camera_Capture '{}'`; never `ScreenCapture`, which the relay refuses outright, and never
`cameraInstanceID`, which only the scene view answers reliably. Remember the scene view ignores the game
camera's clear colour, so Black reads as the editor's light background there.

*CS-130 — teardown.* `node tools/mcp/umcp.js run tools/mcp/rework/p2_leave_play.cs`, confirm
`isPlaying=false` (`compile.ps1` reports it), then
`node tools/mcp/umcp.js run tools/mcp/rework/p2_teardown.cs`. **Accept:** `cosmic_verify` and any stray
`cosmic_verify_*` objects are gone from the scene and the scene is saved clean; `audio_library.asset` and
`room_dim.mat` are **kept** and committed, because they are the Phase 6 wiring inputs and not fixtures.
Check `git status` before committing and revert any shared material that play mode wrote runtime values
into (`about_material`, `earth_clouds`, the Jupiter clouds).

### Reconciliation — the terminal ran CS-126 and CS-127 before this plan was pulled (12 Sep 2026)

Two sessions worked the rework in parallel and neither could see the other. The plan above is the
rework author's own and is **authoritative**; it landed on `main` first. A terminal session had by
then already executed its first two steps by hand and committed the result. The rows are marked
`done` from that run and the numbers are below. Nothing above was renumbered; the tickets this
session added start at **CS-131**, after the plan's five.

**CS-126 — done.** `COMPILE CLEAN`, no `error CS` lines. It did not compile when the editor was
first opened: the merge that brought the rework onto `quest3-port` had left *two* independent
drag-bar implementations in `DockController.cs`, both declaring `LateUpdate` — `CS0111`. Fixed in
`5e60a03f` by keeping the complete implementation and folding the other's two behaviours into it.
That was in the old tree, not the rework, but nothing in the rework could compile until it was gone.

**CS-127 — done, and it found one regression the acceptance line did not predict.** `Cosmic/Import
Copy` had never run; `Assets/Cosmic/Data/Generated` did not exist. It produced **37 assets** — 10
bodies, 11 moons, 7 places, 7 destinations, 2 hints — and is **idempotent**: a second run left 37,
not 74. The gate exited 1 with **71 losses**, which the fix below took to **68**:

- **52** unowned `Narration` / `ContentPrefab` / `DockThumbnail` references — exactly as predicted,
  now CS-131. Related and worth knowing before that ticket starts: `Copy.Wire()` reports **0
  references wired**, because it reads `Bodies:` and `Destinations:` fields from `experiences.md`
  that the deck has never contained — and the old `ExperienceModule` assets carry no such field
  either, so those are *new* authored relationships, not something recoverable.
- **11** places and bodies the deck never got — now CS-132. `docs/copy/` predates `450a2468`
  (HD 110067) and `bea0a068` (three more galaxies), so four places and seven bodies that exist in
  the old data have no copy at all and the importer can never emit them.
- **5** layouts — now CS-133.

**The one divergence, raised rather than applied quietly.** The plan's CS-127 acceptance line
expects "3 room modes the copy deck does not state and which stay authored" as a *predicted* loss.
They are not authored anywhere, and nothing reports it: `cosmic_web`, `galaxies` and
`sagittarius_a` are `Environment: 3` (`FullBlack`) in the old data, the enums correspond exactly
(`Passthrough, Dimmed, BlackHalo/Halo, FullBlack/Black`), so the value is simply lost —
`Enum.TryParse` fails on a deck that states nothing and `Place.room` keeps its `Dimmed` default.
Three places would render in a dimmed room instead of black. `**Room:** Black` was therefore added
to those three sections of `experiences.md`, which `Copy.cs:175` explicitly supports ("the room is
not copy, so it is only taken when the deck states one"), and the deck's preamble now documents the
field and the silent-ignore. **This changes the plan's acceptance line: the expected room-mode
losses are now 0, not 3.** If the intent really was to author them elsewhere, revert the three deck
lines and say where.

**A second, smaller divergence.** `Generated/layouts/` is described above as a placeholder folder,
but git does not track empty directories: committing `layouts.meta` with no folder beside it gives
a fresh clone an orphaned meta, which Unity deletes with a warning, after which CS-133 regenerates
the folder under a different GUID. The empty folder and its meta are therefore *not* committed.
`Copy.EnsureFolders` recreates the folder on every import, so `p1_data.cs` is unaffected.

**CS-135 — done.** P2's declared debt. `SfxPool.cs` takes the voice array, the start times, the
same-clip debounce and `Take`; `Audio.cs` goes 278 → **249** lines. The mixer group is asked for
per voice through a `Func<AudioMixerGroup>` rather than captured at `Build` time — the original made
each voice lazily and re-read `library.sfx` at that moment, and freezing it would route every
one-shot to the default group whenever the library is assigned *after* the bus exists, which is
precisely the order CS-128 puts it in. `SfxPool.Stop` deliberately does not clear the debounce map,
matching the old `OnDisable`.

**What this session did *not* do, and CS-128–CS-130 still own.** No `AudioLibrary` asset existed
when this ran, so no channel could be heard and P2's real listen test was not attempted. What was
observed in play mode is only a regression check on CS-135: the bus builds its four sources
(`voice`, `ambience`, `music_a`, `music_b`), the pool stays lazy until a real clip arrives, every
channel accepts a null clip without throwing, and disable-then-re-enable is clean. Construction and
teardown, not sound. `Verify.cs` and `p2_setup.cs` are the right way to close it.

**Harness note.** A lingering `relay_win.exe` makes every relay call return an *empty tool list* —
"the relay has no `Unity_RunCommand`", with the list blank rather than the tool absent, which reads
like a broken relay and is not. Kill every `relay_win` immediately before each `umcp.js` or
`compile.ps1` invocation. This cost the first three calls of the session.


---

## Phase 0 — Foundation and rebrand

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-001 | CC | Back up scenes to `Assets/scenes/_backup_original/` | — | done |
| CS-002 | CC | Rebrand: product name, bundle id, About copy | — | done |
| CS-003 | CC | Repo hygiene: `.gitattributes`, `Assets/_sources/CREDITS.md`, `docs/decisions.md` | — | done |
| CS-004 | TERM | Create the Figma file and pages | — | done |
| CS-005 | TERM | Source public-domain imagery (Helix, Orion, Crab, Homunculus, deep field, planet maps) and log credits | CS-003 | done |
| CS-006 | CC | Write the copy deck (`docs/copy/*.md`) | — | done |
| CS-007 | TERM | Verify in the editor: backups import, build list clean, identity applied; commit generated `.meta` files | CS-001 | done |

**Phase 0 notes (11 Sep 2026).**
*CS-001 done:* five scenes copied to `Assets/scenes/_backup_original/` with a README; the owner's `main_scene - Copy.unity` moved in as `main_scene_user_copy.unity` (kept its `.meta`, so its GUID is unchanged) and removed from the build list. The five copies have no `.meta` yet — Unity generates them on next open; CS-007 commits them.
*CS-003 done:* `.gitattributes` (Unity YAML marked `merge=binary` so a bad auto-merge can't corrupt a scene), `Assets/_sources/CREDITS.md` (licence table + the six assets we have already made + what CS-005 must source), `docs/decisions.md` (D-001..D-005 with rationale).
*CS-006 done:* `docs/copy/` — README (rules: **ASCII only**, the Selawik fonts have no degree sign, en dash or curly quote), `experiences.md` (7 panels), `bodies.md` (10 bodies, paragraph + 4 stats), `moons.md` (11 moons, 6 in scope), `nebulae.md` (7 destination overlays), `hints.md`, `ui.md` (dock, pop-ups, overlay, About, messages).
*Fixed in passing:* `main_scene` and `core_systems_scene` were each listed twice in `EditorBuildSettings`; duplicates removed.
*CS-007 done:* editor verification — six backup scenes import with fresh GUIDs (the owner's copy keeps its original), build list holds exactly the six shipping scenes with no duplicates and no backups, product name and both bundle ids applied. `.meta` files committed.
*CS-004 done:* Figma file **Cosmic Simulation XR** — `https://www.figma.com/design/qWxL0ZGiyI7aRjnQAVISoI` — with pages *Design System* and *Front End - Screens*. It sits in the owner's drafts; move it into a team project when convenient.
*CS-005 done:* five subjects were already covered by textures inherited from the MIT project (Pillars, NGC 1501, Trumpler 14, and the old Crab and Homunculus plates) — reuse before sourcing. Six clean plates were then downloaded from ESA/Hubble and ESA/Webb into `Assets/Textures/nebulae/` and `Assets/Textures/galaxies/`, all CC BY 4.0, centre-cropped and downscaled to 2048 (Homunculus 1024, no larger release exists), each logged in `CREDITS.md` with its release URL and original resolution. The existing Crab and Homunculus plates have "IMAGE COURTESY - NASA" burnt into a corner, which is why clean replacements were worth taking; the originals stay because `poi_magic_window_*` materials reference them.
*Import settings are enforced, not hand-set:* `Assets/scripts/Editor/AstronomyTextureImporter.cs` applies mip maps, non-readable, and ASTC 6x6 on Android to anything in those two folders, so a re-downloaded plate cannot come back as an uncompressed readable 2048. Verified on all six.
*Still pending, and not a sourcing problem:* the optional Moon and Earth map upgrades. Neither existing texture is equirectangular — both are atlases laid out for the custom model UVs with specular packed into alpha, so a USGS or Blue Marble map is not a drop-in. It needs a re-UV or re-projection plus specular repacking, which belongs with the Phase 3 material work.
*Resolved 11 Sep 2026:* `companyName` was still "Microsoft Corporation"; set to **JDMAXilius** to match the bundle id. It sets the save-data path (`%USERPROFILE%\AppData\LocalLow\<company>\<product>`) and the publisher the store shows. `Quest3ProjectSetup` writes only the identifier, not the company, so this sticks across builds.

## Phase 0/1 note on the build (11 Sep 2026)

A Quest APK build stalls on a modal: **"Unsupported Input Handling on Android"** — Active Input Handling is "Both", which Android does not officially support. We need "Both" (XRI uses the new Input System, TouchScript the old one), and the shipped 111 MB APK was built the same way, so the answer is **Ignore**. Tick *Don't ask again for this session* to keep an unattended build from blocking. If we ever drop TouchScript, switch to Input System only and the warning goes away.

**CS-001** Copy `main_scene`, `core_systems_scene`, `galaxy_view_scene`, `solar_system_view_scene`, `galactic_center_view_scene` (+ `.meta`, keep GUIDs unchanged is *not* possible — generate new meta GUIDs by deleting the copied `.meta` files so Unity regenerates them) into `Assets/scenes/_backup_original/`; add a `README.md` there ("do not edit"); ensure none are in `ProjectSettings/EditorBuildSettings.asset`. *Acceptance:* files present, not in build list, README present.
**CS-002** Done 11 Sep 2026: `productName` "Cosmic Simulation XR", Android/Standalone identifier `com.jdmaxilius.cosmicsimulationxr`; About text already updated. *Remaining (TERM):* confirm Unity accepts the identifier on next open.
**CS-003** `.gitattributes` (`* text=auto`, `*.cs text eol=crlf`, `*.md text eol=lf`, binaries `-text`), `Assets/_sources/CREDITS.md` (table: file, source URL, licence, date, edits), `docs/decisions.md` (log the five decisions of 11 Sep 2026 — see CLAUDE.md). *Acceptance:* files exist; `git status` clean after commit.
**CS-006** Files: `docs/copy/experiences.md` (7 scene panels: title, 2–3 paragraphs, instruction line), `docs/copy/bodies.md` (Sun, 8 planets, Pluto: title, paragraph ≤ 55 words, Diameter/Mass/OrbitalPeriod/DayLength with unit and exponent — values in GDD §6.1), `docs/copy/moons.md` (Moon, Ganymede, Callisto, Titan, Mimas, Iapetus + optional Io, Europa, Enceladus, Phobos, Deimos: name, parent, diameter, orbital period, one sentence), `docs/copy/nebulae.md` (Helix, Crab, Homunculus, Orion + Pillars, NGC 1501, Trumpler 14: title, 2 paragraphs), `docs/copy/hints.md` (two hint cards). Our own words; no text from the reference. *Acceptance:* every entry present; word limits met.

## Phase 1 — Design system and UI (Figma)

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-010 | TERM | Figma tokens page (colour, type, spacing, radii) | CS-004 | done |
| CS-011 | TERM | Figma dock frames + states | CS-010 | done |
| CS-012 | TERM | Figma pop-ups + utility window | CS-010 | done |
| CS-013 | TERM | Figma info panels (body / scene / moon) | CS-010 | done |
| CS-014 | TERM | Figma labels (tag states, moon labels, leader line) | CS-010 | done |
| CS-015 | TERM | Figma desktop HUD (dock mirror, controls overlay, About) | CS-010 | done |
| CS-016 | TERM | Figma hint cards (2) | CS-010 | done |
| CS-017 | TERM | Export SVG/PNG @2× to `Assets/ui/figma/`, write `docs/ui/spec.md` | CS-011…016 | done |
| CS-018 | CC | Sprite import settings script (`Editor/UiSpriteImporter.cs`: UI sprite, no mips, 9-slice per spec) | CS-017 | done |
| CS-122 | CC | **Every builder-written UI prefab is using Segoe UI, not Selawik.** `UiPrefabBuilder.Font()` searches `"t:TMP_FontAsset selawik"`, which matches nothing (the assets are named `selawk*`), then falls back to the first `TMP_FontAsset` in the project - `segoeui SDF` | CS-017 | done (12 Sep terminal session; verified in every prefab and scene, per-eye/on-device look still open) |

**Phase 1 notes (11 Sep 2026).** The file has two pages, as the owner asked: *Design System* (read-me, colour, type, space and radius, surfaces) and *Front End - Screens* (A dock, B panels/tags/hints, C three in-headset shots, D desktop, E pop-ups and dock states).
Two conventions the rest of the work depends on:
- **Scale.** Virtual UI is specified in millimetres and drawn at **1 mm = 4 px**. A dock tile is 110 x 62 mm, drawn 440 x 248.
- **Font.** Figma has neither Selawik nor Segoe UI, so the mockups use **Source Sans 3** as a metric stand-in. The app keeps its Selawik SDF assets; match the millimetre sizes, not the family.

**CS-122 filed 12 Sep 2026 (terminal session), and it contradicts the Font convention directly above.**
The convention says "the app keeps its Selawik SDF assets". It does not. `UiPrefabBuilder.Font()`
(`Assets/scripts/Editor/UiPrefabBuilder.cs:74-86`) asks for `"t:TMP_FontAsset selawik"`; the Selawik
assets in `Assets/Fonts/` are named `selawk SDF`, `selawkb SDF`, `selawkl SDF`, `selawksb SDF`,
`selawksl SDF` - **"selawk", not "selawik"** - so the search returns nothing, the method falls back to
`FindAssets("t:TMP_FontAsset")` and takes `guids[0]`, which is `segoeui SDF`. Every one of the seven
builder-written UI prefabs (`dock`, `dock_tile`, `dock_popup`, `desktop_dock`, `desktop_dock_tile`,
`info_panel`, `label_button`, `utility_window`) therefore carries `m_fontAsset` = `segoeui SDF`.

Two reasons this is more than cosmetic. **Licensing:** Segoe UI is proprietary Microsoft and must not
ship in a store build; Selawik is the OFL-licensed, metric-compatible replacement Microsoft published
for exactly this purpose, which is why the project chose it. **The copy rules assume Selawik:**
`docs/copy/README.md`'s ASCII-only rule exists because the Selawik faces have no degree sign, en dash
or curly quote - a constraint that has been shaping every line of copy while the app rendered in a
font that does not have it.

The code fix is one word (`selawik` -> `selawk`), and it should also pick the regular face
deterministically rather than `guids[0]` of six Selawik variants. **It was deliberately not applied in
the 12 Sep terminal session**: it changes the typeface of every piece of UI in the app, and applying it
means re-running Build UI Prefabs and then, because that renumbers objects inside `info_panel_prefab`,
Wire Scene Panel, Build Solar Row Content, Build Moons and Install Runtime Systems in that order. That
is a visual-identity change plus a five-step rebuild cascade, so it is the owner's call, not a drive-by.
Selawik is metric-compatible with Segoe UI, so layout should not shift.

**CS-122 done, 12 Sep 2026, same session, on the owner's decision to apply it rather than defer.** The lookup
now asks for `selawk`, returns the regular `selawk SDF` face deterministically instead of `guids[0]` of six
variants, and **refuses to fall back to another family** — the silent fallback is what caused this, so it is now
a `LogError` and a null return. `DesktopDockBuilder` had a second, identical copy of the same buggy lookup
(its own comment noted the duplication); it now delegates to `UiPrefabBuilder.Font()`, so the two cannot
diverge again.

Applied through the full cascade, in this order, each step verified: Build UI Prefabs, Build Desktop Dock,
Build UI Prefabs again, Build Solar Row Content, Build Moons, Wire Scene Panel, Install Runtime Systems.
Result: all eight UI prefabs carry `selawk SDF` and zero Segoe references (`desktop_dock` and
`desktop_dock_tile` needed the second builder), `solar_system_planets_content_prefab` has 237 Selawik
references and none to Segoe, and a project-wide sweep of `Assets/prefabs`, `Assets/scenes` and `Assets/data`
finds no reference to `segoeui SDF` at all. The moons survived the re-run intact (9 `MoonOrbit`, 9 panels,
19 nested instances) and `scenePanelPrefab`, `MusicController`, the 23 `desktop_dock_prefab` references and
`MusicAudioSources` being inactive all still hold.

One thing needed a hand edit, and it is worth knowing why. `core_systems_scene` carried two `m_sharedMaterial`
overrides on its `dock_popup_prefab` instance, both pinning the **Segoe** font material. One targeted an object
the rebuild had removed, so it was dead residue; **the other targeted an object that still exists, so it was a
live override that would have forced Segoe back onto that text and defeated the fix.** Neither could be reverted
through `PrefabUtility`, and re-opening and re-saving the scene did not drop them, so the two four-line
modification entries were removed from the YAML directly and Unity was then made to re-open the scene (12 roots,
loaded and re-saved cleanly) to prove the edit was valid. A dangling-override sweep puts the scene at **90**
such overrides, down from **91** before this session — the remainder are pre-existing and are now CS-123.
CS-011 states are drawn (idle, hover, pressed, active) but are not yet Figma component variants.
*CS-015 done:* board **F - Desktop overlays** carries both panels the desktop needs — the controls overlay (mouse column and keyboard column, every line naming a thing the player does rather than a system) and About, with the Microsoft/MIT credit, the imagery credit and three buttons. Board **D - Desktop** holds the dock mirror.

*CS-017 done:* a third page, **Export - Unity**, holds the atoms actually worth shipping, and they are in `Assets/ui/figma/` — five white nine-slice rounded rects (1, 2, 4, 6, 8 mm), the dock tile foot gradient, and six icons (passthrough, recenter, help, mute, close, back). Editable SVG sources in `Assets/_sources/figma_svg/`; measurements in `docs/ui/spec.md`.
*Why so few files:* the whole UI is rounded rects plus text, so a plate, a tag pill and a pop-up are one white sprite at different tints and radii rather than one sprite each. The tile gradients on board A are placeholders — real tile art is rendered from the scenes (CS-031) — and the black halo is generated in code, so neither is exported. Twelve files, about 21 KB.
*CS-018 done:* `Assets/scripts/Editor/UiSpriteImporter.cs`, an `AssetPostprocessor` over `Assets/ui/figma/`. Sprite, single, no mips, clamp, uncompressed, and a nine-slice border read from the file name (`ui_rounded_r<n>` → border n + 2, so the corner arc sits inside the border and is never stretched). Menu **Cosmic Simulation → Reimport UI Sprites** forces a pass. Verified on all twelve.
*Convention worth keeping:* export atoms are drawn at **8 px per millimetre** (twice the board scale) and imported at **8000 pixels per unit**. A Unity unit is a metre, so a sprite pixel is exactly a millimetre and the numbers in `docs/ui/spec.md` can be used directly when laying out a panel.
*Still to do before a store listing:* the About panel's "Source code" and "Privacy" links must point at our repo and our own privacy page.

Sizes and colours: GDD §8. *Acceptance for CS-017:* every frame exported; spec maps frame → prefab with mm sizes.

## Phase 2 — Core architecture

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-020 | CC | `ExperienceModule`, `BodyInfo`, `LayoutPreset` ScriptableObjects (`Assets/scripts/experience/`) | — | done |
| CS-021 | CC | `CopyImporter` editor menu: `docs/copy/*.md` → SO assets | CS-006, CS-020 | done |
| CS-022 | CC | `ExperienceDirector` (switch over `ViewLoader`, events, restore-on-switch) | CS-020 | done |
| CS-023 | CC | `EnvironmentController` + dim quad shader/material + halo gradient generator | — | done |
| CS-024 | CC | `FreePlacementSolver` (no snap-back, `RestoreLayout`, bounds auto-restore) | CS-020 | done |
| CS-025 | CC | `TwoHandTransformer` (limits per type, smoothing) | — | done |
| CS-026 | CC | `LabelButton` (hover grow/cyan, pinch/click → target) | — | done |
| CS-027 | CC | `InfoPanel` v2 from `PlanetInfoCard` (body/scene/moon variants, `<sup>` mass, outline) | CS-020 | done |
| CS-028 | CC | `DockController`, `DockPopup` scripts (data-bound, poke via `GEButton`/`XRPokeFilter`, grab bar, palm-up show/hide) | CS-020 | done |
| CS-029 | CC | `DesktopDock` + `DesktopMouseInput` additions (`P`, `F2–F8`, label clicks, free placement) | CS-028 | done |
| CS-030 | TERM | Build dock/pop-up/panel prefabs from Figma exports (RunCommand scripts) | CS-017, CS-027, CS-028 | done |
| CS-031 | TERM | Dock thumbnails: render 7 modules to `Assets/ui/thumbnails/` | CS-030 | done |
| CS-032 | TERM | Register existing 3 scenes as modules; hide old Back/zoom UI; verify switching in play mode | CS-022, CS-030 | done |
| CS-033 | TERM | `smoke.cs` harness: switch all modules, panels, place/restore, screenshots, console clean | CS-032, CS-098 | todo |
| CS-098 | CC | Check the MCP smoke harness into the repo as `tools/mcp/` — CS-033 was marked done without one | CS-033 | done |
| CS-035 | CC | `ExperienceDirector`: adopt an already-loaded view scene, unload strays (duplicate-scene bug) | CS-032 | done |
| CS-036 | CC | `ExperienceDirector`: open a module from its `ContentPrefab` when it has no scene | CS-022 | done |
| CS-037 | CC | `ExperienceDirector`: clear `_switching` in a `finally` plus a timeout, so a scene that fails to load cannot wedge the dock in an emptied room | CS-035 | done |
| CS-038 | CC | Refuse or queue `Switch` while the intro flow is running; wire `OpenStartModule`, which today has no callers at all | CS-032 | done |
| CS-060 | CC | Solar Row: Saturn and Uranus ring spans exceed the 25 cm pitch and intersect by 6.9 cm - decide pitch vs ring span | CS-041 | done |
| CS-061 | CC | No `BaseInputModule` anywhere in the project, so screen-space UI cannot be clicked; `DesktopMenuManager` buttons likely never worked | CS-029 | done |
| CS-062 | CC | Player-visible message when an experience fails to load (today it is silent, log only) | CS-037 | done |
| CS-092 | CC | Nothing ever spawns an experience's own scene panel: `InfoPanel.Bind(ExperienceModule)` has no callers at all | CS-027, CS-036 | done |
| CS-087 | CC | Desktop key map still points at the retired drill-down navigation (`Backspace` → Back) and at the legacy menu; make GDD §5.3 true and retire the live-camera planet bar | CS-029, CS-061 | done |
| CS-088 | TERM | Prefab surgery: delete the planet bar from `menu_managers.prefab` (11 `UiWorldPreview` tiles, both `PlanetPreviewController`s, the row itself) and the dead `PlanetPreviewController` block in `ForceSolver.OnPointerDown` | CS-087 | todo |
| CS-123 | TERM | 90 dangling prefab-instance overrides in `core_systems_scene` target fileIDs that no longer exist in `menu_managers.prefab` (47), `hand_menu_left_prefab` (21), `hand_menu_right_prefab` (21) and `ge_xr_rig.prefab` (1) — pre-existing, inert today because Unity drops an override whose target is gone, but they hide any override that *was* load-bearing and they will be churned by CS-088's and CS-111's surgery anyway | CS-088, CS-111 | todo |
| CS-124 | CC | `DrawStars` scales badly: `AttachAllInOrder` re-attaches every command buffer on every camera whenever a drawer appears (quadratic in drawer count, and it fires in the frame the player pokes a tile), and `Update` writes ~10 material properties per layer per frame | — | todo |
| CS-125 | CC | Galaxy LOD by vertex-count truncation is free but currently impossible: `SpiralGalaxy.GenerateEllipses` emits inner-to-outer and concatenates the second arm after the first, so drawing fewer points deletes the outer arms and one whole arm. Shuffle the bake order so a truncated draw thins the galaxy evenly instead | — | todo |
| CS-089 | TERM | Desktop controls overlay copy is out of date: it lists `Backspace` back and omits `P` and `F2`–`F8`; rewrite `desktop_help_panel` to GDD §5.3 | CS-087 | todo |
| CS-039 | TERM | Wire the Earth atmosphere shell (1.025 sphere child + `SunLightReceiver`) onto the Earth prefab — nothing references `earth_atmosphere_material` yet — **and compile-check `planet_atmosphere_rim_shader.shader`, which CS-047 shipped without ever compiling it** | CS-047 | done (12 Sep terminal session; built, run and verified in the editor - the on-screen look still wants a headset) |
| CS-034 | TERM | Quest Link check: dock poke, dim quad, halo, passthrough toggle | CS-032 | todo |
| CS-090 | CC | Utility window: scale slider, mute, narration-only mute, text size, close (GDD §8.2, F-03 — no `utilityWindow`/`scaleSlider` exists anywhere) | CS-028, CS-030, CS-076 | done (unverified) |
| CS-097 | CC | Dock height was a hard-coded 0.9 m; adapt it to `max(0.55 × head height, 0.7 m)` at Recenter (GDD §11) | CS-028 | done |
| CS-099 | CC | Wire the dock's Help button (world + desktop) — `helpButton` has no `OnClick` listener at all — to replay the hint cards | CS-094, CS-028 | done |
| CS-110 | CC | Fix six runtime defects an adversarial review found across the wave (input-module early return, editor-wiring guard, music crossfade cutting the louder source, dock recentring underground, a raycaster sweep touching a nonexistent prefab, one isolated compile risk) | CS-061, CS-073, CS-097, CS-062 | done |
| CS-111 | TERM | The hand menu still offers the old Back button in the headset — the one piece of the drill-down-navigation retirement CS-087 deliberately left alone rather than strand a headset user without checking the dock is present first | CS-087, CS-088 | todo |
| CS-119 | TERM | Make `tools/mcp/enter_play_mode.cs` actually take over the relay, or document that Play must be pressed by hand | CS-098 | **done (12 Sep)** — `delayCall += EnterPlaymode()` never fires through the relay: the command reports success and nothing happens. Assigning `EditorApplication.isPlaying = true` works. `isPlaying` reads false on the same frame, so poll it rather than trusting the reply. The separate `UNEXPECTED_ERROR: User interactions are not supported` is a different thing and still stands: the relay refuses certain APIs outright, `ScreenCapture.CaptureScreenshot` among them — frame the scene view at `Camera.main`'s pose and use `Unity_Camera_Capture` with `{}` instead. |
| CS-121 | CC | Dragging the dock (CS-108) abandons whatever opened from it — the utility window, a `DockPopup`, a hint card and `SwitchNotice` each place themselves once at open and never again, so carrying the dock leaves them floating, unattached and still interactive, at their old offset | CS-108, CS-090 | done (unverified — [CC] track, compile-checked by reading; the drag that triggers it is hand-only, so the four surfaces still need a headset) |

Design contract: GDD §3, §5, §8; architecture: Technical Overview §5.5–5.6, §7.2. *Acceptance:* per roadmap Phase 2 "done when".

**Phase 2 notes (11 Sep 2026).**
*CS-020 done:* `Assets/scripts/experience/` — `ExperienceModule` (dock tile or destination: scene or content prefab, environment mode, panel copy, layouts, audio), `BodyInfo` (body or moon: title, subtitle, paragraph, `Stat[]`, moons, the planet a moon orbits; `Stat.ToRichText()` renders the superscript), `LayoutPreset` (named arrangement + `Find(bodyId)`). Namespace `CosmicSimulation`.
*CS-021 done:* `Assets/scripts/Editor/CopyImporter.cs`, menu **Cosmic Simulation → Import Copy**, parses `docs/copy/*.md` into `Assets/data/{bodies,moons,experiences,destinations,layouts}`. Idempotent: assets are updated in place so references survive. Verified run: 10 bodies, 11 moons attached to Earth/Jupiter/Saturn/Mars, 7 experiences, 7 destinations, none incomplete.
*Gotcha worth keeping:* inside `AssetDatabase.StartAssetEditing()` the database is not refreshed, so `IsValidFolder` reports a folder you just made as missing and `CreateFolder` silently creates `data 1`, `data 2`, … beside it. Create folders through `Directory.CreateDirectory` + one `Refresh` instead.

*CS-022 done:* `ExperienceDirector` — `Switch(module)` clears destinations, restores moved bodies, stops narration, sets the environment, unloads the previous view, loads the new one through `ViewLoaderScript.LoadViewAsync`, grows it in on a cubic ease-out, then starts narration and raises `ExperienceChanged`. Also `OpenDestination`, `ClearDestinations`, `RestoreEverything`.
*CS-023 done:* `EnvironmentController` (dim quad parented to `Camera.main` at 0.06 m, queue 3900 so panels and the dock stay legible; generated 256² radial halo texture; `SpawnHalo`/`Forget`; `SetPassthroughForced` for the dock button) and `BlackHalo` (billboard 1.6× the subject, 0.15 m behind it along the view ray, 0.4 s fade). Verified in play mode on the galactic centre: dimming took mean frame brightness 4.28 → 2.95 with the desktop HUD unaffected, and the halo renders at queue 2990 tracking S2 as it orbits.
*Gotcha worth keeping:* the halo first failed with *"Material … with Shader 'Unlit/Transparent' doesn't have a color property '_Color'"*. `Unlit/Transparent Colored` is not in this project and `Shader.Find` on a built-in that no material references can return null in a player build, so both surfaces now use `Assets/shaders/environment_tint_shader.shader` (`CosmicSimulation/EnvironmentTint`) — a flat tinted transparent shader that also carries the stereo macros Android multiview needs. It is registered under **Always Included Shaders** in `ProjectSettings/GraphicsSettings.asset` so the runtime lookup survives the APK build.
*Gotcha worth keeping:* `stop.cs` leaves play mode; `exit.cs` **quits the editor**. Do not reach for the wrong one. If `EditorApplication.isPlaying` reads stale `True` after a crash-out, set `isPaused = false` then `isPlaying = false` and again inside `delayCall` before compiling.

*CS-024 done:* `FreePlacementSolver` — a body stays where it is left and comes home for three reasons only: the player asked, the experience changed, or it has been out of reach (further than 4 m, or below the floor) for 5 s. Home is `ForceSolver.RootTransform` read live, so a moving layout stays authoritative; the return is a 0.8 s cubic ease-out and then `ResetToRoot()`, so the solver's own machinery unwinds the moons, panel and highlighter. **This replaces `PostManipulationResetter` on anything the player arranges** — that component slides an object back a couple of seconds after *every* release, which is the opposite of GDD 5.2. Verified in play mode.
*CS-025 done, smaller than the ticket assumed:* `ManipulationHandler` **already is** the two-hand transformer (midpoint move, `FromToRotation` rotate, distance-ratio scale, exponential smoothing), so writing a `TwoHandTransformer` would have meant two competing systems. What was missing was the limits, so it gained one hook — `IManipulationScaleConstraint`, resolved once and cached — and `ScaleLimits` implements it. Limits are stated in **metres of real width** (Body 0.05–3, Model 0.4–4, Nebula 0.3–2), not scale factors, because a body's authored scale has no fixed relation to how wide it looks; the object is measured lazily on first clamp, since bodies are authored at orbit size and grown by the force solver before anyone can get two hands on them. Verified against arithmetic on a 1 m cube: caps at 3 m, floors at 0.05 m, passes an in-range value through, stays uniform.
*CS-026 done:* `LabelButton` — the destination tags. Idle, hover (15% larger, cyan fill, text flips dark so it still reads) and selected, plus a leader line that can be pointed at a world position. Uses a `MaterialPropertyBlock` rather than a material instance: several are on screen at once and instancing would cost a draw call each. Hover and click arrive through `GEInputEvents.ExecuteHierarchy`, so hand ray, fingertip and desktop mouse all work with no special cases; a poke fires on pointer-down, a ray on click, with a 0.3 s cool-down. Verified: one pick registered from two rapid clicks.
*CS-027 done:* `InfoPanel` — `PlanetInfoCard` rebuilt to read `BodyInfo` and `ExperienceModule` instead of fields typed into a prefab. One component, three shapes: body (title, subtitle, paragraph, 2×2 stats), scene (paragraphs plus an instruction), moon (name, one line of numbers, one sentence) — the moon variant is chosen from the data, since a moon is just a body that orbits one. No plate on any of them; over passthrough a plate punches a hole in the room, so it is outlined white text. Verified against the real copy: Earth → Body with 4 stats, mass rendering `5.97 x 10<sup>24</sup> kg`; Moon → Moon variant orbiting Earth; Solar System → Scene with 2 paragraphs.
*CS-028 done:* `DockController`, `DockTile`, `DockPopup`. The dock builds itself from `ExperienceDirector.Modules`, so adding an experience is a data change, and it **parks** rather than following the head — a dock that chases you cannot be looked away from. Tile states are movement, not tint (hover lifts 6 mm, poke pushes 4 mm, open keeps a cyan underline), because a tile is mostly photograph. Only modules with more than one layout open a pop-up. Show/hide is palm-up on the left hand, latched so a held palm toggles once. Height is derived from the player at each recentre — `max(0.55 x eye height, 0.7 m)` (GDD 11) — and sampled there rather than per frame, because re-deriving it every frame is the head-following the parking rule exists to avoid; desktop and a headset that has not produced a pose yet use a nominal 1.6 m eye height, which lands on the 0.88 m the dock used to be hard-coded to.
*CS-029 partly done, reopened 11 Sep 2026 — do not trust the earlier "done".* The `DesktopMouseInput` half is finished and verified: `P` toggles the room preview and `F2`–`F8` jump to the seven tiles in order, counting tiles only so a destination never takes a function key; label clicks and free placement already work through the existing pointer path plus CS-024. **`DesktopDock` was never built.** A live smoke run showed why it matters: the world-space dock parks 0.7 m below eye level, which is right in a headset where you glance down, but on a monitor the camera looks level and the dock is simply outside the frustum — unreachable. GDD 8.5 calls for a bottom-right HUD mirror on desktop, and that is the remaining work.
*Gotcha worth keeping:* a component added with `AddComponent` and used in the same frame has not necessarily had `Awake` run, and a label or body is often bound the frame it is spawned. `LabelButton` and `FreePlacementSolver` both threw on that; both now lazily initialise from every entry point rather than trusting `Awake`.
*CS-032 corrected 12 Sep 2026: the acceptance line's "hide old Back/zoom UI" half was never implemented by this ticket, only claimed.* No code hid anything when this was marked `done`. The switching half is real and stands; the hiding half landed later, under other numbers: the desktop key map and the in-code retirement of the planet bar are CS-087, the prefab surgery to actually delete the bar is CS-088 (still `todo`), and the one piece nobody has touched yet — the old Back button the hand menu still offers in the headset — is CS-111 (new, `todo`). Read this row as "switching: done; hiding: done in code, not yet in the prefab, and not yet in the hand menu."
*CS-032 verified live 11 Sep 2026, and CS-035 raised from it.* A play-mode run confirmed the wiring works end to end: the dock built its seven tiles from module data, switching to `milky_way` set the room to Dimmed, loaded the scene, marked the active tile and completed. **But the same run exposed a duplicate-scene bug** (now CS-035): the app's own boot flow had already loaded `galaxy_view_scene` while `Director.Current` was still null, so the switch loaded a *second* copy — visible in a screenshot as every destination label drawn twice, slightly offset. The director only unloads the scene of the module it thinks was previous and knows nothing about scenes the original Galaxy Explorer flow loaded itself. It must adopt an already-loaded target scene and unload stray view scenes.
*Worth knowing when probing:* `core_systems_scene` is loaded well into the boot flow, not at start. An early probe finds no `ExperienceDirector` and looks like a wiring failure when nothing is wrong — poll for it rather than concluding.
*CS-037 done:* a failed scene load can no longer wedge the dock. The `finally` alone would not have fixed it, which is the find worth keeping: `ViewLoader.LoadViewAsyncInternal` throws **before its first yield**, and `StartCoroutine` runs a coroutine to its first yield inline — so the throw lands during `SwitchRoutine`'s own first `MoveNext`, before `_switching = StartCoroutine(...)` has assigned anything, and the assignment then puts a dead handle back and wedges it regardless. `Switch` now uses a `_switchFinished` flag to decide whether to store the handle at all. The wait is bounded (20 s) and the throw is caught. On failure the room comes back as passthrough — the previous place is already unloaded so it is not on offer, and the module's own environment would leave the player in an empty black void, whereas passthrough is at least their real room.
*CS-038 done:* `Switch` refuses while onboarding is running, and `OpenStartModule` — which had **no callers anywhere** — is wired to the intro's end. No honest "intro is running" flag existed: `IntroFlow.currentState` is private, `TransitionManager.IsInIntroFlow` is already false on the last stage, and `ViewLoader.IsIntro()` goes false as soon as the intro loads the solar system. So `IntroRunning` means "an `IntroFlow` exists and has not raised `OnIntroFinished` yet". Two traps handled: the end signal fires a frame or more *before* the intro's own last-stage scene load, so opening on it would stack two galaxies; and `startModule` is `milky_way`, whose scene is the very one the intro just loaded, so the director now **adopts** what is already open rather than re-running the whole switch over it.
*CS-029 done:* `DesktopDock` — the bottom-right HUD mirror. It binds to the same `DockController` helpers the world dock uses, so the two cannot drift; the shared logic was lifted into four statics rather than restated.
*Convention worth flagging:* the desktop dock is **screen-space, so it is in pixels**, not the millimetre canvas every world prefab uses. A `110` copied across from `UiPrefabBuilder` would be a 110-pixel tile rather than a 110 mm one.
*CS-030 done:* `Assets/scripts/Editor/UiPrefabBuilder.cs`, menu **Cosmic Simulation → Build UI Prefabs**, writes five prefabs to `Assets/prefabs/ui/`: `dock_prefab`, `dock_tile_prefab`, `dock_popup_prefab`, `info_panel_prefab`, `label_button_prefab`. Built by script rather than by hand so they are reproducible and reviewable; re-running replaces them in place, so scene references survive.
*Convention:* every world-space prefab is authored on a canvas where **one canvas unit is one millimetre**, with the canvas itself scaled 0.001. That is what makes `docs/ui/spec.md` directly usable — a tile is 110 x 62, its foot 26, the gap 4 — and it is why `DockTile.hoverLift`/`pressDepth` and `DockController.tilePitch` are serialized in local units rather than hard-coded metres.
*Verified by rendering the prefabs to an offscreen camera*, not just by checking that fields are non-null: 7 tiles named from the module assets, the Earth panel with its cyan subtitle, paragraph, divider and 2x2 stat grid showing `5.97 x 10^24 kg` as a real superscript, the layout pop-up with its active option filled cyan, and a selected destination tag.
*Three bugs the render caught that a field check would not have:* the tile referenced `ui_rounded_r20`, a sprite dropped when the radii were aligned to the millimetre scale; `LabelButton` typed its pill and leader as `Renderer` when a canvas uses `Graphic`, so both references silently serialized as null (now `Graphic`, tinted through `.color`, which the canvas batches anyway); and the small buttons were positioning `transform.parent` instead of themselves, which moved the dock and left every button stacked at the centre.
*Still placeholder:* tiles have no picture until CS-031 renders them, and the pop-up's two options read "Schematic / Realistic" until CS-040 supplies real `LayoutPreset` assets.

*CS-036 done:* `ExperienceDirector.Switch` now opens a module from its `ContentPrefab` when it names no scene — four of the seven places (Solar System Planets, Andromeda, Cosmic Web, Galaxies) are never getting a scene, and a prefab is the lighter answer. The instance is spawned under a content root the director owns (`experience_content_root`) and destroyed at exactly the point a scene is unloaded. **That root hangs off the `ViewLoader`, not the world origin** — review caught the first version parenting it at identity, which would have put the first prefab-backed experience at the player's feet while every scene-backed one sat two metres away facing them. View content is not origin-relative: the intro places the experience by moving and rotating the Loader itself (`WorldAnchorHandler.CreateWorldAnchor`) and every view scene's root follows it. Everything else in the sequence is byte-identical: clear destinations, restore moved bodies, stop narration, set the environment, grow in, narration, `ExperienceChanged`. A module with **neither** scene nor prefab is refused in `Switch` before anything moves, with one warning per module, rather than half-switching into an empty room.
*CS-035 done:* the smoke run found the director loading a second copy of `galaxy_view_scene` on top of the one the Galaxy Explorer boot flow had already opened, and never unloading `solar_system_view_scene` because it only knew about the scene of the module it thought was previous. `Switch` now, at the same point the unload used to happen, closes **every** open view scene except one copy of the target and adopts that copy instead of loading another. What counts as a view is derived, not hard-coded: `ViewLoader` draws the same line for its back stack but keeps it private and its CoreSystems name need not match the scene file, so the director treats the active scene and the scene that loads views (its own, and `ViewLoader`'s) as structure and everything else additively loaded as a place. `GalaxyExplorerManager` is no use as a marker here — `Singleton` moves its root to DontDestroyOnLoad, so it no longer reports the scene it came from. An adopted scene never went through `TransitionManager` on our account, so `CurrentActiveScene` is re-pointed at that scene's own `TransformHandler` root before the grow-in — otherwise it would still name content we had just unloaded.
*CS-062 done (unverified in the editor - [CC] track, compile-checked by reading only).* `SwitchNotice`, the player-facing half of CS-037. `ExperienceDirector` calls it from the two places a switch can end badly, and the two get **different wording on purpose**: a module with neither scene nor prefab is refused before anything moves and is told as *"not ready yet ... arrives in a later update"*, because four of the seven places are legitimately sceneless until Phase 4/5 and that is not an error; a throw out of the loader or a load that misses the 20 s bound is told as *"did not open. Your room is back"*, which is also the only thing that explains the room reappearing in passthrough. The notice is posted **outside** the once-per-module log gate: repeats are noise in the console but the player is owed an answer to every poke.
*CS-092 done (unverified in the editor — [CC] track, compile-checked by reading).* Every experience's own panel — GDD §3.3 and §8.3, contract rows F-20, F-23, F-25, F-26 — existed only as a `Bind(ExperienceModule)` overload nothing called. `ExperienceDirector` now spawns one from a serialized `scenePanelPrefab` (the project's convention for a prefab reference: an editor menu item fills it, never `Resources.Load`), at two points and two only: **after the grow-in and before the narration** in `SwitchRoutine`, because it is the written form of the same words and because everything after the routine's single `yield break` is skipped by an abandoned switch, so a place that failed to open never gets described; and in `Adopt`, without which the **start module would be the one experience in the app that never showed a panel** — the intro hands the Milky Way over already loaded, so it never goes through a switch. It is destroyed at exactly the beat the previous place is torn down (beside `DestroyPrefabContent`, after `UnloadOtherViews`), because it lives under `experience_content_root`, which survives the unload. A module whose `ScenePanelCopy` has no title, no paragraph and no instruction gets **no panel, silently**: the copy is tested rather than the module, since `Bind` falls back to `DisplayName` and testing the module would give every unwritten place a one-word panel. Not a singleton (GDD §8.3 allows several at once) — the same helper also gives a nebula destination its panel in `OpenDestination`, sitting beside the overlay and following it, which is part of what CS-054 still owes.
*Where the panel parks, and why it is an empty anchor.* `scenePanelOffset` (metres) is measured in the content root's frame, which hangs off the `ViewLoader` for the CS-036 reason: view content is not origin-relative. The panel's target is a bare `scene_panel_anchor`, not the content itself, because `InfoPanel` measures its target's **first** renderer to find the edge to clear, and the first renderer of a galaxy or a row of planets says nothing about how wide the whole thing is; an anchor with no renderer falls back to a small radius, so the panel lands where the offset puts it. `(0.55, 0.2, 0)` is a guess that wants a headset look — **[TERM]**.
*Two things `InfoPanel` needed for the scene variant, both scoped to it.* The instruction row is reflowed under however much prose a place has (the prefab's rows are laid out for the body variant's fixed seven-line paragraph, so a three-paragraph scene panel would have run straight through it); and the cyan hairline, which divides prose from stats, is hidden when there are no stats — scene and moon variants both. The hairline is a new serialized `Graphic` field (a UI element is a `Graphic`, not a `Renderer`) which **`UiPrefabBuilder.BuildInfoPanel` does not yet assign** — it creates the object but points nothing at it — so `Awake` finds it by name as a documented fallback. One line in that builder (`so.FindProperty("divider").objectReferenceValue = divider;`) retires the fallback; that file belonged to another agent this session.
*Before it can show anything, someone must run **Cosmic Simulation → Wire Scene Panel*** (`Assets/scripts/Editor/ScenePanelWiring.cs`), which points the director at `Assets/prefabs/ui/info_panel_prefab`. It is a separate menu item only because `ExperienceWiring.cs` was being edited elsewhere; it belongs inside `InstallSystems` as three lines beside the dock wiring. With no prefab assigned the director warns **once** and every experience simply has no panel.
*Run 12 Sep 2026, terminal session, on the second attempt.* It first failed with "no `ExperienceDirector` in any open scene" because `core_systems_scene` was not open. It must be opened **additively**, never Single — unloading a dirty scene raises a modal dialog that blocks the relay. Once open, `core_systems_scene.unity:1349` now reads `scenePanelPrefab: {fileID: 284596933589354139, guid: 782d1fee663b8e841b4659df5742eb48}`, which is `info_panel_prefab`. Still not seen showing a panel in play mode.
*No new prefab, deliberately.* It builds its own canvas the first time it is asked to speak, so nothing has to be wired in the editor and no scene can lose it - and it lives on a `DontDestroyOnLoad` object, because the director unloads whole view scenes out from under itself on every switch. Two presentations, chosen from `GalaxyExplorerManager.IsDesktop` at first use (not in `Awake`: the platform is not decided yet then): in the headset a world-space canvas on the millimetre scale parked just above the dock, sharing its tilt, white text with a dark outline and **no plate**, since a plate punches a hole in passthrough; on the desktop a line above `DesktopDock` on a screen-space canvas at sorting order 60, above the dock's 50. It has no `GraphicRaycaster`, the text is not a raycast target and the `CanvasGroup` blocks nothing, so it cannot swallow the next poke; it fades itself out after 5 s, on Escape, or the moment another place opens.
*Judgement left open:* the third refusal - a poke that arrives while the intro is still running - stays silent. The dock is hidden during onboarding, so it is a stray rather than a real request, and a message over the intro narration would be worse than nothing.

*Note for CS-040:* the dock data reads 7 tiles, 7 destinations and **0 with a layout choice** — correct for now. `HasLayoutChoice` needs at least two `LayoutPreset` assets on a module, and CS-040 generates them; the three layout pop-ups stay dormant until it lands.

*CS-061 done (unverified in the editor - [CC] track, compile-checked by reading).* `Assets/scripts/experience/UiEventSystemInstaller.cs` - one idempotent static that guarantees a live `EventSystem` with an input module. **Where it runs from is the whole ticket:** the app's only authored `EventSystem` sits on `main_camera_prefab`, which arrives with `core_systems_scene` **late in the boot flow**, so a plain `[RuntimeInitializeOnLoadMethod]` would find nothing at frame 0, make its own, and be sitting beside a second one a few seconds later. It therefore runs at `AfterSceneLoad` **and** on every `sceneLoaded`/`sceneUnloaded`, and it always prefers what the app brought over what it made - both for the `EventSystem` and for the module. `DesktopDock` lost its private copy and calls `Ensure()`; `DesktopMenuManager.Start` calls it too, since that menu is the one the ticket says never worked.
*Module type is `InputSystemUIInputModule`, and Active Input Handling being "Both" does not make that a toss-up:* `StandaloneInputModule` would compile, but it reads `UnityEngine.Input` while every pointer the app actually reads is Input System (`Mouse.current`, XRI 3.6). XRI's `XRUIInputModule` is the third candidate and is deliberately not installed - it would drive world-space uGUI from the hand ray, and here world-space UI is hit with physics through `GEPointer`.
*The regression this ticket would otherwise have shipped, and the reason `IsPointerOverScreenSpaceUi` exists:* `UiPrefabBuilder` puts a `GraphicRaycaster` on the **world-space** canvas it builds, so `dock_prefab`, `dock_popup_prefab`, `info_panel_prefab` and `label_button_prefab` all have one. `EventSystem.IsPointerOverGameObject()` - which `DesktopMouseInput` called in two places - is true for those too. With no module it always answered false, so the difference never showed; with a module it would have made the desktop mouse stand down over a destination tag or a floating panel, skipping the physics raycast and killing the hover and click CS-026 built. Both call sites now ask a raycast of our own and count only canvases whose `renderMode != WorldSpace`.
*Nothing was needed on a prefab:* both screen-space canvases we ship (`menu_managers.prefab`, `desktop_dock_prefab`) already carry a `GraphicRaycaster`. The installer adds one to any screen-space canvas that lacks it and logs when it does, so the next one built without one says so instead of swallowing every press.
*What the editor pass must confirm (this is the honest list):* that `AddComponent<InputSystemUIInputModule>()` really does assign the package's default UI actions - the whole fix is dead if it does not, and the assertion was inherited from the code this replaced; that exactly one `EventSystem` is alive after boot with exactly one module; that the desktop menu and dock buttons now respond; and that destination tags still hover and click on desktop.
*CS-087 done (unverified in the editor — [CC] track, compile-checked by reading only).* The desktop key map audited against GDD §5.3 row by row. **Backspace is gone**: it called `GlobalMenuManager.OnBackButtonPressed` → `TransitionManager.LoadPrevScene`, which is exactly the drill-down navigation roadmap §4.3 retires, and the desktop HUD's Back button is now forced hidden in `DesktopMenuManager.UpdateButtonsActive` for the same reason (the flag it ignores is still computed, because the other platforms' menus read it). Four rows were repointed at the systems that now own them: `R` asks `LayoutRig.Restore()` (which animates the anchors *and* walks pulled-out bodies home through `FreePlacementSolver`), falling back to a solver sweep in scenes with no rig — it used to press the legacy menu's Reset, which only worked while `MenuIsAvailable`; `Home` goes through `DesktopDock.Recenter`, so the key and the dock's own button cannot drift; `Esc` closes the help overlay, else any open `DockPopup`, else hand-driven panels and the legacy card; `1`–`9`,`0`,`M` resolve a body by `BodyInfo.Id` through `LayoutRig.Find` → `MoonOrbit` → the legacy `UiPreviewTarget` slots, and pressing the key again sends **only that body** home rather than pressing Reset and clearing the room. **The whole map no longer waits on `GlobalMenuManager.MenuIsAvailable`**, a flag only the legacy ViewLoader intro flow ever raises. `Tab` was left where CS-029 put it — `DesktopDock` reads it itself — and `DesktopMouseInput` now only falls back to folding the legacy button row when there is *no* dock, so one key cannot drive two things. `H`/`F1` needed a fix in `DesktopMenuManager`, not in the key handler: the overlay lives under the same root the "menu availability" flow switches off, so `SetHelpVisible` now reopens that root for as long as the overlay is up (and closes it again after) and `IsHelpVisible` reads `activeInHierarchy`. `P` and `F2`–`F8` were already correct and are untouched.
*What CS-087 could not make true, honestly.* The controls overlay still *reads* the old map — that is prefab text and is **CS-089**. The legacy `desktop_dots_button` shows in the corner while the overlay is up, because the overlay borrows that root; it goes with the HUD. `M` finds nothing until the Moon's planet is out, which is correct but means the key looks dead in a fresh room. And the whole key map is reasoned, not run: no editor was available.
*CS-033 reopened 12 Sep 2026, and CS-098 raised from it. **It was marked `done` without its artefact.*** A coverage audit went looking for the harness and `find . -iname 'smoke*'` returned nothing; the commit that closed CS-033 (`590d8ae`) touches `docs/BACKLOG.md` and nothing else. What it actually delivered was real and is worth keeping: a one-off play-mode run of the dock, which found the duplicate-scene bug (CS-035) and reopened CS-029, and the note that `core_systems_scene` loads late. What it did **not** deliver is the thing in its title — a re-runnable harness — nor five of its six acceptance clauses: only one module was switched, no panel was checked, place-and-restore was never exercised, one screenshot was taken rather than one per module, and the console was read by eye. Status is back to `todo`: the harness now exists (CS-098) but has never been executed against a live editor, and running it is the [TERM] half.
*CS-098 done (the harness exists; **no part of it has been run** — there was no relay in the session that wrote it).* `tools/mcp/`, outside `Assets/` so Unity never compiles it, it needs no `.meta` files and none of it ships in the APK. Five files: `umcp.js` (Node stdio MCP client: `list`, `call <tool> <json>`, `run <file.cs>`), `compile.ps1` (refresh, wait, `error CS####` lines from `Logs/Editor.log` written since the refresh), `smoke.cs` (the walk), `enter_play_mode.cs` and `leave_play_mode.cs`, plus a README a terminal session can work from without reading the source. `CLAUDE.md`'s "lived in the session scratchpad; recreate if missing" is now wrong on the second half — **it is checked in** — and that line should be repointed at `tools/mcp/` next time anyone edits that file.
*`compile.ps1` had never actually been run until 12 Sep 2026's terminal session, and did not work.* `$PSScriptRoot` is empty inside a parameter default — both directly and under `powershell -File` with a relative path — so it died on the first `Join-Path` before doing anything. Fixed by resolving the script's own root once in the body and deriving `$ProjectPath` and the path to `umcp.js` from that. See the dated session note near the end of this file.
*`smoke.cs` had never actually been run either, and did not compile — found the same session.* Two helper classes, `Check` and `Entry`, were declared `private sealed class` at what turns out to be **file scope**: they are indented, but the runner wraps the whole file in its own namespace, which makes an indented top-level declaration a namespace member, and `private` is illegal there (`error CS1527`, two sites). Fixed by making both `internal`, which is what the section comment ("state — this assembly only") already meant. **Rule for anyone else writing into `tools/mcp/*.cs`:** no top-level type may be `private`, and a column-0 grep will not catch a violation, because the offending declaration is indented and reads like a nested class until the runner's wrapper is remembered.
*Two things in `umcp.js` are inferred rather than read, and are the first suspects if it does not talk.* The relay's **framing**: MCP over stdio is newline-delimited JSON in the wild, but this relay was described to us as Content-Length headers, and sending the wrong one hangs rather than errors. So the reader accepts both, and the writer tries Content-Length first, respawns and retries newline framing if the handshake goes unanswered (`--framing=lsp|ndjson` skips the negotiation). And the **argument name** `Unity_RunCommand` wants the source under: rather than guessing `code`, it reads the tool's own `inputSchema` and picks the string property. Both paths were exercised against a stand-in server, not against the relay.
*What `smoke.cs` covers of CS-033's line, honestly:* switch every module (yes), environment per module (yes), the scene panel per module (yes, now that CS-092 spawns one), place-and-restore (one body per module, through `ForceSolver.OnPointerDown` and `FreePlacementSolver.RestoreLayout`), a screenshot per module (yes), console clean (yes, but only from the moment the run starts — use `Unity_GetConsoleLogs` for anything before). *Not covered:* it calls `ExperienceDirector` directly rather than poking the dock, so a dead `GEButton` would not show; no hand input, no moons, no tags, no two-handed scaling, no passthrough toggle; one body per module, not every body.
*Three shapes in `smoke.cs` that look odd and must not be tidied away:* it will not enter play mode itself (`EnterPlaymode` reloads the domain and unloads the assembly the command is running in); `Execute` returns immediately and the walk is driven from `EditorApplication.update`, with progress in `SessionState` and a report file, because every `run` compiles a fresh assembly with fresh statics — run it again to poll; and every file in `tools/mcp/` is written **fully qualified with no `using` directives**, because the runner wraps the file in a preamble of its own.
*CS-087, the planet bar (roadmap §4.3, Technical Overview §15).* `UiWorldPreview` built a `Camera` **and** a 256x256 `RenderTexture` per tile and moved each body onto a private `PreviewLayer` so its camera could see it alone — eleven of each, drawn every frame, for a widget nothing routes through since `DesktopDock` landed. Each tile now switches itself off in `OnEnable` before any of that, and `PlanetPreviewController` switches the whole `planet_previews` row off in `Awake`, which retires **both** copies of that component (one is legacy with 20 slots) without destroying either. Both keep a `keepLegacyBar` inspector toggle so CS-088 can check the surgery against the old behaviour. Two knock-on effects, both deliberate and both written up rather than patched here: `ForceSolver.OnPointerDown` still looks the controller up on every pull and now always fails (that file is not this ticket's; delete the block with CS-088), and the bar's habit of swinging `LightSourcePosition` when a planet was pulled is gone — that object exists only in the legacy `solar_system_view_scene`, so if a pulled planet looks unlit there, this is why.

*CS-097 done (unverified — [CC] track, compile-checked by reading only; commit `ecb8a40`, titled "CS-097, CS-095" — the row for the dock half is this one, see the ticket id map).* The dock height was a hard 0.9 m, wrong for a seated player and wrong for a tall standing one. `DockController.Recenter` now computes `max(0.55 x head height, 0.7 m)` per GDD §11, working the eye height out once and using it for both the floor estimate and the height so the two cannot disagree; the 1.6 m nominal figure that used to be a buried literal is now a serialized field. Sampled at Recenter, never per frame — deliberately, because CS-028 already established that the dock parks rather than chases. Desktop has no real head height (the camera is a free-orbit preview), so it uses the nominal figure and lands within 2 cm of the old constant. **A second, worse bug was found and fixed in the same area by `7e1a358`, not by this commit:** `Recenter` used to run the frame the boot scene lands, before the headset has produced a pose but after it reports itself active, so the head read as zero and the floor came out at minus the trusted fallback eye height — the dock spawned underground. It now waits for a real pose, bounded, and believes a low reading once one has arrived. Dock reach wants a seated and standing check on device — [TERM].

*CS-099 done (unverified — [CC] track, compile-checked by reading only; commit `93f13db`, same commit as CS-094).* The world dock's `helpButton` was built, shown and hidden with the rest of the controls, and nothing was ever subscribed to its `OnClick` — it had always been decorative. Both the world dock and `DesktopDock.ToggleHelp` now call `CosmicSimulation.HintCards.Replay()`; on the desktop dock the call goes in before the branch that hands off to the legacy overlay, since that path returns early and the hints are owed either way. Satisfies GDD F-05 and F-32's "replay from Help."

*CS-110 done (unverified — [CC] track, compile-checked by reading only; commit `7e1a358`, "Fix six runtime defects an adversarial review found in the pushed wave" — no ticket number in the title, this row exists so the fix has one).* A `verifier`-style pass over the whole 12 Sep wave found six runtime bugs, all in code this same wave had just written, none of them compile errors:
1. `UiEventSystemInstaller.Ensure()`'s early-return guard checked whether the *current* `EventSystem` was the one it made, which is only ever true a frame after boot — so the installer always made its own first and returned before the scan that finds the real one ever ran. CS-061's whole stated invariant had never executed once.
2. Fixing (1) alone would have been worse: the boot scene authors an enabled `XRUIInputModule` with every action reference empty, so the moment the scan ran, its adoption logic would have destroyed the working module and handed pointer handling to an inert one — the exact dead-button bug CS-061 exists to fix, recreated by fixing (1). Resolved by disabling the foreign module rather than adopting it.
3. `MusicController`'s editor wiring guard meant "the track list has entries," not "the list has a clip." A run with one broken clip path left four empty entries, which still passed the guard on the next run, switched off the legacy music, and left the app silent with no log line. The guard and the switch-off are now both gated on a clip actually resolving.
4. The music crossfade assumed the outgoing source is always the quieter one, true only past the halfway point of a fade; a scene missing from Build Settings can take the room through three `EnvironmentMode`s in one frame, and the fade would then cut a bed at full volume. It now picks the genuinely quieter source.
5. The dock-underground bug under CS-097 above.
6. The load-failure notice's own raycaster sweep was adding a `GraphicRaycaster` to its own canvas and logging advice to fix a prefab that does not exist; it now skips canvases with nothing clickable under them.
One compile risk is isolated on purpose rather than fixed: `UiEventSystemInstaller.EnsureDefaultActions` (the hedge described at the top of the `[TERM]` queue above) still calls two members this session could not verify against the installed package version. See queue item 2 before anything else.

*CS-111 (new, 12 Sep 2026, todo).* `f0dc395`'s own note names this the one thing it deliberately left alone: the hand menu still offers the old Back button in the headset. Hiding it is one line, but the dock's presence in every headset scene was unverified in that session, and stranding a headset user with no way back is worse than a stale button. Needs an editor pass to confirm the dock actually reaches every headset scene before the button comes out.

*CS-090 and CS-076 done together 12 Sep 2026 (unverified in the editor — [CC] track, compile-checked by reading).* They are one feature: GDD §11 puts narration-only mute and the text size **in the utility window**, and the utility window did not exist, so CS-076 had nowhere to land. New file `Assets/scripts/experience/UtilityWindow.cs`, built by `UiPrefabBuilder.BuildUtilityWindow` (menu **Cosmic Simulation → Build UI Prefabs**) into `Assets/prefabs/ui/utility_window_prefab.prefab`, opened by a third small button under the dock or by `U` on the desktop.
*The Figma export has nothing for it.* CS-017 shipped five rounded rects, the tile foot and six icons (passthrough, recenter, help, mute, close, back) — **no utility-window frame and no settings or slider glyph**. The window is therefore built from the spec's tokens (`docs/ui/spec.md` §1–§4: 5 mm padding, 2 mm grid, 6 mm plate radius, the type ramp) and the dock button wears the mute glyph until an `icon_settings.png` is dropped into `Assets/ui/figma/`; the builder logs which it used.
*It is 120 × 102 mm, not the 120 × 50 GDD §8.2 gives it.* Fifty millimetres holds a slider and a close box. Four controls do not fit in it, and the four are what §11 asks for. GDD §8.2 has been corrected to match rather than left describing something else.
*Each control reaches a system that already existed; nothing here is a second mechanism.* Mute goes through `DesktopMenuManager.OnMuteButtonPressed()` when that legacy HUD is in the scene — exactly as `DesktopDock.ToggleMute` already does — and writes the same `GalaxyExplorer.Muted` key directly when it is not, which is the headset case where nothing ever persisted mute before. Narration is a new static `VOManager.NarrationEnabled` gating the one `if (VOEnabled)` in `PlayClip`, so a muted clip is never queued rather than queued and silenced. Text size is a static `InfoPanel.TextScale` folded into the one line of `Place()` that already computes the panel's world scale: the rows of a panel are laid out in fixed canvas units against a fixed width, so raising `fontSize` would reflow the prose into the stat grid, while scaling the panel keeps the signed-off design and simply makes it bigger. The scale slider writes `localScale` on `TransitionManager.CurrentActiveScene` — the project's own answer to "which content root is open", which `ExperienceDirector` maintains on every switch — clamped through the content's `ScaleLimits` when it has one, so the slider cannot reach a size two hands are refused.
*The slider is not uGUI, and could not be.* There is no `TrackedDeviceGraphicRaycaster` in this project (see the class comment on `UiEventSystemInstaller`), so world-space uGUI is unreachable by hand. The rail carries a `BoxCollider` and a `GEInteractable` with no handler of its own, so its pointer events walk up to `UtilityWindow` through `GEInputEvents.ExecuteHierarchy` while each button's own `GEButton` stops the walk there. The drag reads `GEPointer.AttachTransform`, the same source `ManipulationHandler` drags objects with — which is what gives desktop parity for free: `DesktopMouseInput` rides its mouse attach point along the cursor ray while the button is held, so click-drag works with no second implementation. The window is world-space in both modes and places itself in front of the desktop camera rather than beside the dock, because the world dock is parked below the desktop frustum on purpose.
*Three keys become five.* `GalaxyExplorer.Muted` is reused; `GalaxyExplorer.NarrationMuted` and `CosmicSimulation.PanelTextScale` are new, both lazily read so they are right before any `Awake`. The **scale slider is deliberately not persisted**: it is per-experience state, not a setting, and restoring last session's multiplier over a different place would be wrong. It resets to ×1 on every experience change.
*The world dock is missing a control the contract wants, and this only half fixes it.* GDD §8.1 lists Recenter and Help under the dock and says "Mute lives in the utility window", but row F-05 is "Recenter / Mute / Help — each does what it says". Before today the world dock had no mute at any depth and no way to reach a utility window; it now reaches mute in one extra press. A direct mute button on the dock is a design call, not a code one — raised for the owner rather than decided here.
*What [TERM] must check that reading cannot settle.* (1) Re-run **Build UI Prefabs** and confirm `utility_window_prefab` exists and that `dock_prefab` now carries a fourth small button plus a `utilityWindowPrefab` reference — the dock prefab is rebuilt in place, so scene instances must be checked for stale overrides. (2) Poke the rail in the headset and drag it: the knob should follow a fingertip and a hand ray, and the value text should track. (3) On the desktop, confirm the window lands inside the frustum and in front of the near clip plane at 0.5 m — that distance is a guess against an unread camera. (4) Confirm the text at 4.55 mm is legible on a monitor at that distance; if not, raise `desktopDistanceMetres`' sibling offsets rather than the font. (5) Check the panel text size actually changes an open `InfoPanel` while it is visible. (6) Confirm `U` collides with nothing on the Quest's virtual keyboard paths.
*Pre-existing bug found, not fixed (not our file):* `DockPopup.Close()` is wired straight to its close button's `OnClick`, and `GEButton.Click` starts its cool-down coroutine immediately after invoking listeners — on a GameObject the listener has just deactivated, which Unity logs as an error. `UtilityWindow` avoids it by deferring its own close by one frame; `DockPopup` still has it.

*CS-121 (new, 12 Sep 2026, verifier pass) — a regression CS-108 made reachable, not a pre-existing bug.* `UtilityWindow.Place()` is called only at the two points the window opens, never from `Update`; `DockPopup.PlaceAbove()`, `HintCards.Park()` and `SwitchNotice`'s placement are the same shape — each reads `DockController.Instance.transform` once and never again. Concrete: open the utility window with `U`, then pinch the drag bar and carry the dock a metre left — the window stays behind, floating unattached and still interactive, about the same distance from where the dock used to be. GDD §8.2 says it belongs beside the dock. Before CS-108 the dock could not be moved by hand, so this was unreachable; it is new. **The trap for whoever takes the ticket:** `DockController`'s re-facing runs in `LateUpdate`, so during a drag the dock's rotation is a hand-rolled pose for any `Update` that reads it — calling `Place()` from `UtilityWindow.Update` would produce a window that rolls with the wrist while the dock itself stays level. Re-placing on drag *end*, not every frame, avoids it.
*CS-121 done 12 Sep 2026 (unverified in the editor — [CC] track, compile-checked by reading only).* `DockController` now raises a static `Moved` event at the end of every route that puts the dock somewhere new — the drag-release frame in `LateUpdate` (after `FaceThePlayer`, so the pose read is the levelled one and not the wrist's, which is the trap above), `Recenter`, and `MoveTo` — and `UtilityWindow`, `HintCards` and `SwitchNotice` re-place from it, each only while it is actually on screen. The dock's own `DockPopup` is moved by `DockController` calling a new `DockPopup.Reposition()` rather than by the pop-up subscribing, because `DesktopDock` instantiates a second `DockPopup` whose placement is screen-space pixels (`DesktopDock.PlacePopupAbove`) and a world-space re-place would have thrown that one out of frame on every Recenter — that is the one new bug this fix could have introduced and did not. Recenter now drags these four along too, which the old one-shot placement did not. **Needs a headset:** open the utility window with `U`, carry the dock a metre left by the bar, and confirm the window arrives beside it upright rather than rolled; same for a layout pop-up, a replayed hint card and a switch notice. Desktop is parity-only — the world dock parks 0.72 m below a camera that never pitches, so the bar cannot be moused; the reachable desktop path is `R`/Recenter, and there `Place()` still uses its camera-relative branch, so nothing moves.

## Phase 3 — Solar system one-to-one

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-040 | CC | `LayoutPreset` generator for SolarRow and RelativeSize (GDD §4.1 numbers) | CS-020 | done |
| CS-041 | TERM | Create `solar_row_scene` from `solar_system_view_scene`; wire presets and pop-up | CS-040, CS-032 | done |
| CS-042 | CC | `BodyInfo` assets for 10 bodies (from copy deck) | CS-021 | done |
| CS-043 | TERM | Add moons via the `MoonForceSolver` pattern: Ganymede, Callisto, Titan, Mimas, Iapetus (+ optional 5) | CS-032 | doing |
| CS-109 | CC | `Editor/MoonBuilder.cs` (the post-pass CS-043 was waiting on) + two `MoonOrbit.cs` bug fixes found in review | CS-043, CS-041 | done |
| CS-044 | CC | `MoonLabel` (small/large) | — | done |
| CS-045 | TERM | Schematic/Realistic wiring on the orbit model; name labels; Asteroid Belt label — **and give `solar_system_prefab`'s root a `GEInteractable` + `ManipulationHandler(MoveRotateScale)` + `ScaleLimits(Model, 0.4-4 m)`, which it has zero of today** | CS-032 | todo |
| CS-046 | CC | `SunTouchResponse` (brightness pulse + rumble while a hand is inside) | — | done |
| CS-047 | CC | Earth atmosphere rim material (reuse `halo_shader`) | — | done |
| CS-048 | TERM | Two-hand room-scale tuning (Saturn rings around the user) on Link | CS-025, CS-041 | todo |
| CS-049 | TERM | Desktop: number keys in row order, moon clicks; run smoke | CS-041 | todo |


**Phase 3 notes (11 Sep 2026).**
*CS-040 done:* `Assets/scripts/Editor/LayoutPresetBuilder.cs`, menu **Cosmic Simulation → Build Layout Presets**, writes `Assets/data/layouts/{solar_row,relative_size}.asset` and attaches both to `solar_system_planets`, which is what finally makes `HasLayoutChoice` true and wakes the dock pop-up. Spacing is derived rather than tabulated: Solar Row treats the GDD's 25 cm as a straight chord on the 1.1 m arc, Relative Size spaces each body by its own occupied span with a 10% plus 10 cm gap. Verified run — Solar Row 10 bodies, 15 cm each, 1.88 m end to end; Relative Size 0.5 cm to 3 m, 3.49 m end to end, no overlaps, only the Sun lifted to clear the floor.
*Convention this sets, and CS-041 must honour:* a `LayoutSlot.Scale` is **the body's diameter in metres**, so body prefabs have to be normalised to a 1 m diameter for a layout to measure true. Slots are local to a content root sitting at the player's start position on the floor.
*The tightest clearance in the whole layout, worth knowing before touching Saturn:* at Solar Row's 15 cm diameter and a 2.3x ring span, Saturn's rings reach 17.25 cm from its centre and Jupiter's near surface sits at 17.5 cm — **2.5 mm apart**. They do not overlap, but any Saturn model whose rings exceed 2.3x the planet diameter will intersect both neighbours. Fix that by narrowing the ring span, not by widening the GDD's 25 cm pitch.
*One for CS-041 to see coming:* Relative Size puts the Sun's top at y = 3.05 m, through the ceiling of most real rooms in passthrough. That follows the GDD's 3.0 m Sun, so it is the spec's consequence rather than a builder error — but it will be the first thing anyone notices on the Quest.
*CS-044 done:* `MoonLabel` — 5 mm under an orbiting moon, 18 mm bold once it is held, cross-faded. Placement is deliberately copied from `InfoPanel` (world-space follow, camera facing, constant physical size) rather than reinvented.
*CS-047 done as a shader and a material, but nothing renders it yet — see CS-039. Not the way the ticket said, either.* `halo_shader` cannot serve a Fresnel atmosphere rim and no material variant of it can: it has **no view-dependent term at all**. Its glow is a tangent-space normal map (`glow_normal_alpha_texture`) painted onto a purpose-built `*_glow_mesh` submesh inside each planet's original FBX and lit by `_SunDirection` — on the plain 1 m sphere our body prefabs are normalised to it lights a ball, not a limb. It also predates the Quest port: no stereo macros and no `#pragma target`, so under Android multiview it would draw one eye's view into both. So: a new ~40-line shader `Assets/shaders/planet_atmosphere_rim_shader.shader` (`CosmicSimulation/PlanetAtmosphereRim`), per-pixel Fresnel, `Blend SrcAlpha One`, `ZWrite Off`, `Cull Back`, all four stereo macros, `#pragma target 4.5`, no geometry shader, `NearClip.cginc` like its siblings. The rim is per-pixel because a Fresnel term interpolated across a sphere's triangles bands worst exactly at the silhouette.
*Recipe, not a hand-edited `.mat`:* `Assets/scripts/Editor/AtmosphereMaterialBuilder.cs`, menu **Cosmic Simulation → Build Atmosphere Materials**, writes `Assets/materials/earth_atmosphere_material.mat` and resets it to the recipe on every run. Earth's blue is the one `earth_glow_material` already used, so this is not a second blue. **For whoever wires the prefab:** it goes on a sphere child scaled ~1.025 of the body — the rim lives in that gap, and below ~1.01 it vanishes into the surface — with a `SunLightReceiver` on the same child pointing at the Sun. Without a receiver the rim lights evenly all round instead of disappearing, which is the deliberate fallback for the desktop preview and for a body held on its own. `SunLightReceiver` writes to `sharedMaterial`, so re-run the menu item before committing if a play session has left a direction in the asset. Not verified in the editor — no compile, no render.
*CS-046 done, corrected 12 Sep 2026:* `SunTouchResponse` — the Sun brightens and a rumble swells while a hand is inside it. Also answers to controllers, since a Quest user holding controllers has no palm joint, and to the mouse on desktop through the existing focus routing rather than a second raycast loop. It must sit on the same GameObject as the Sun's `ForceSolver`, because that is where `ExecuteHierarchy` stops. **This was rumble-only from 11 Sep until 12 Sep:** its `brightnessProperty` defaulted to `_TouchBrightness`, a shader property that did not exist, so touching the Sun only ever rumbled. `sun_shader` now exposes it (default 0, gains `_TouchBodyGain` 0.7 and `_TouchRimGain` 1.5, arriving per-renderer through a property block so only the touched instance of the four sharing this material brightens), so both halves of F-16 are live in the code — but this half has never been compiled, never rendered, and the two gain values are a guess. The commit that added it (`ecb8a40`) called it CS-095; that id belongs to the shader-stereo verification ticket opened the same day, so the work is recorded here instead — see the ticket id map above. Needs a [TERM] look on device to tune the gains, and a compile pass before either is trusted.
*CS-041 done, and deliberately not the way the ticket said.* It asked for a `solar_row_scene`; with CS-036 in place the experience is a **content prefab** instead, which honours the standing rule against adding scenes. `Assets/scripts/Editor/SolarRowBuilder.cs` lifts the `*_tilt` subtree out of each existing `poi_<id>_prefab` — real meshes and materials, no new geometry — normalises each to a 1 m diameter, and hangs the grabbable pattern off one root per body. `LayoutRig` animates the **home anchors** rather than the bodies, so the force solver stays the single authority on where a body belongs.
*Three departures from the old prefab pattern, each a bug if copied literally:* `ManipulationHandler` and `ScaleLimits` must be on the **same** object, because the handler finds its constraint with `GetComponent` and the constraint measures its own transform — split, every clamp is computed against the wrong scale. `ForceSolver`, not `PlanetForceSolver`, because the subclass forces `GoalScale = Vector3.one` every frame in Root and would flatten both arrangements. And the 6 cm grab minimum is the body's own collider widened, not a second shape, so a body never carries two overlapping interactables.
*The measurement bug this shook out, worth keeping:* the builder first sized each body by the mesh that **reached furthest** from the root. Jupiter's cloud prefab carries decorative surface meshes (`inner_spot`, `double_stream_01`) barely a centimetre across but sitting 33 cm out in model space, so Jupiter measured 67 cm instead of 10 cm and its visible sphere would have been normalised to about a sixth of every other body. Size by the **widest** geometry, not the furthest.
*Measured on the built asset:* ten bodies, spheres 15.00–15.15 cm, y = 1.200 m, on the 1.1 m arc at exactly the positions in `solar_row.asset`.
*CS-060 done — the owner chose to shrink the rings in Solar Row only, keeping the GDD's uniform 25 cm pitch.* `LayoutRig` clamps a ring's scale per layout and animates it with the rest of the move. The rule is derived from the arrangement rather than tabulated, so it follows the pitch if the pitch ever changes. It is stated **per pair**, not per body, and that distinction matters: the obvious per-body form (span must fit the nearest-neighbour distance) also bites in Relative Size, where Saturn and Uranus sit 53.7 cm apart while Saturn's true rings span 56.4 cm — it would have trimmed them 6.6% and broken the other half of the requirement. Per pair, a gap must hold both neighbours' widest extents, a pair that already fits imposes nothing, and two ring systems facing each other share the shortfall. Measured with the rig driven: Solar Row Saturn 33.86 → 25.51 cm and Uranus 29.86 → 22.49 cm, closest surfaces **−6.86 cm → +1.00 cm**; Relative Size unchanged at Saturn's true 56.43 cm. Planet spheres and spacing untouched.
*CS-042 done, by CS-021 rather than by hand:* `CopyImporter` already writes all ten `BodyInfo` assets from `docs/copy/bodies.md`. Verified on disk — ten assets, each with a paragraph and four stats. Nothing further was needed, and the ticket should not be reopened to duplicate them.


## Where this session stopped (12 Sep 2026)

Work was halted mid-wave at the owner's request and everything outstanding is a ticket below. Read this first.

**CS-043 is half-landed and UNVERIFIED.** `Assets/scripts/experience/MoonOrbit.cs` (347 lines) is committed but **has never been compiled** - the session was stopped before the editor pass. Its companion `Assets/scripts/Editor/MoonBuilder.cs` was never written, so nothing creates moons yet and the component is inert. Compile first; expect to fix it. The plan it was written against: MoonBuilder is a **post-pass** that opens `Assets/prefabs/experiences/solar_system_planets_content_prefab.prefab` with `PrefabUtility.LoadPrefabContents`, adds moons under the right bodies and saves it back, so it must run *after* SolarRowBuilder and must not edit `SolarRowBuilder.cs`.

**CS-061 has since been written** (see the Phase 2 note) and, like CS-062, has not been through the editor - compile it. (CS-062 was in the same state and has since been written; see the Phase 2 note, and compile it - it has not been through the editor.)

**CS-065 was dispatched but produced nothing** - no files were written. Start it fresh. The approach it was given: reuse the `DrawStars.cs` / `spiral_stars*` StructuredBuffer + DrawProcedural path rather than a second particle system, since the cosmic web is the same problem with a different point distribution.

**Also uncommitted-but-done:** `ExperienceWiring.InstallSystems` now works on the boot scene where it already sits instead of re-opening it, which is what let it run at all after a play session leaves `main_scene` dirty; and it installs `desktop_dock_prefab`.

*Nothing in this session was pushed without being compiled except `MoonOrbit.cs`, which is called out above.*

**Update, 12 Sep 2026, later the same day — this stop-point note is now history, not the current state.** The wave resumed after this pause. CS-065 was written (see its Phase 5 note; still `doing`, still not compiled). CS-061 and CS-062 were both written and are recorded under Phase 2 as `done (unverified — compile-checked by reading)`, the project's standing way of saying "written, not run." `MoonBuilder.cs` was written this same day and is now CS-109, `done` in the same unverified sense: two real bugs in `MoonOrbit.cs` were found and fixed in review (a fader resolved only in `Awake`, so an early getter lied about a moon's own solver — the same lazy-init trap this project has already hit twice; and `Grow` fighting `FreePlacementSolver`'s restore lerp when a planet went home with its moon still out, both writing the same scale every frame). Six moons ship active (Ganymede, Callisto, Titan, Mimas, Iapetus, and the Moon), three more are built but switched off pending a design call, and Phobos/Deimos were deliberately not built — copy exists for them but GDD §6 does not list them, and the contract wins. Sizes are derived from each moon's own planet, never tabulated, and phases are spread across the full family including the off ones so enabling one never lands it on another. Still nothing has been compiled or run; see the `[TERM]` queue above, step 7, for the one documented risk (a prefab instance nested inside `PrefabUtility.LoadPrefabContents`, used nowhere else in this repo).

**Run 12 Sep 2026, terminal session — the documented risk did not happen.** Nine `MoonOrbit` components; six moons active (Earth/Moon, Jupiter/Ganymede+Callisto, Saturn/Titan+Mimas+Iapetus), three built but switched off (Io, Europa, Enceladus), none skipped. All nine `moon_panel_*` instances exist as nested `info_panel_prefab` instances — the feared "moons with no panels" did not happen. **Gotcha worth keeping:** a nested prefab instance stores its own name in `m_Modifications`, not in `m_Name`, so `grep m_Name: moon_panel_` returns nothing on a perfectly good prefab and looks like a failure. Check `m_Modifications`, or ask the editor, before concluding a nested instance is missing. This is a structural read of the rebuilt prefab, not a play-mode confirmation.
## Phase 4 — Milky Way destinations, nebulae, black hole

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-050 | TERM | Replace galaxy POI cards with `LabelButton` tags (6 + 3 extras on) — **and remove the legacy `CardPOI` / `poi_text_card_*` path once tags replace it**, per roadmap §4.3 and Technical Overview §15, both of which already call it retired | CS-026, CS-032 | todo |
| CS-051 | CC | Nebula overlay renderer: layered-card prefab script + soft-depth-fade shader | — | done |
| CS-052 | TERM | Nebula layer assets ×4 (project textures + Hubble; Higgsfield depth layers) | CS-005 | todo |
| CS-053 | TERM | Sagittarius A\* module: full black + stars, hand-ray, panel, grow-in | CS-032 | todo |
| CS-054 | TERM | Milky Way panel + grow-in; verify all six tags open — **and take a frame-time reading with a nebula overlay open** (the `NEBULA_SOFT_DEPTH` prepass cost CS-051's note asks for; fallback is `requestCameraDepth = false` on the prefabs) | CS-050, CS-052 | todo |
| CS-055 | CC | Desktop tag hover/click | CS-026 | done (verified by inspection, 12 Sep) |

**Phase 4 notes (11 Sep 2026).**
*CS-051 done (unverified in the editor — [CC] track, compile-checked by reading).* Three files: `Assets/shaders/nebula_card_shader.shader` (`CosmicSimulation/NebulaCard`), `Assets/scripts/experience/NebulaOverlay.cs`, `Assets/scripts/Editor/NebulaPrefabBuilder.cs` (menu **Cosmic Simulation → Build Nebula Prefabs**). The builder writes `Assets/prefabs/nebulae/nebula_<id>_prefab.prefab` and `Assets/materials/nebulae/nebula_<id>_card_<0..3>.mat` for each of the seven destination modules and sets each module's `ContentPrefab`, so `ExperienceDirector.OpenDestination` works with no new code path. Re-running replaces both in place.
*Card count is four, deliberately.* Technical Overview 7.6 allows 4–6; every card is a full-coverage alpha-blended quad and the black halo behind it is a fifth transparent layer, so on a Quest 3 overdraw is the binding cost, and four luminance bands already separate wisp from core. 70 cm wide (GDD 4.3), 10 cm apart, 30 cm front to back, ±0.5°/s alternating spin. Count, spacing and spin are all serialized on `NebulaOverlay` and can be tuned live.
*Alpha comes from luminance, not from the texture.* The plates are photographs on black sky with no usable alpha channel (and `pillars_texture.tga`'s alpha is unknown, so it is ignored by default via `_TextureAlphaWeight = 0`). Each card takes a feathered luminance window out of the same plate — front card faint and wide, back card the core — which is the "luminance bands" approach 7.6 already specified and means CS-052 can land better plates without touching any code.
*The one dependency the terminal track must watch:* the intersection fade needs `_CameraDepthTexture`, which the built-in forward renderer only produces on request and which costs a depth prepass. `NebulaOverlay` turns `DepthTextureMode.Depth` and the `NEBULA_SOFT_DEPTH` shader keyword on together in `OnEnable` and puts them back in `OnDisable`, so the Quest pays only while an overlay is open. Nothing else in the project sets `depthTextureMode` (checked), so the restore is safe. With no depth texture the shader reads it as "no occluder" and the card keeps the near and edge fades — never a black or invisible card. **Worth a frame-time reading on device with an overlay open (CS-054);** if the prepass is too expensive, set `requestCameraDepth` false on the prefabs and the overlay still works, minus the intersection fade.
*Also on the prefab, for CS-054:* `SphereCollider` (radius 35 cm) + `GEInteractable` + `ManipulationHandler` + `ScaleLimits (Kind.Nebula, 30 cm – 2 m)`, so the overlay is grabbable and scalable per GDD 4.3. Two-handed manipulation is **MoveScale, not rotate** — the stack billboards itself every frame, so a rotation the player set would be overwritten and the grab would feel broken. **Not built here and still owed by CS-054:** the overlay's own info panel (title + 2 paragraphs from `module.Panel`) and the close-on-pinch-outside / `Esc` behaviour.
*Verification to run in the editor:* **Cosmic Simulation → Build Nebula Prefabs**; the console line reports how many of the seven were built, the plate and its resolution for each, and which were skipped. Any destination whose texture is missing is logged and skipped rather than given an invisible prefab, so a skipped id in that line is the signal, not a silent pass.

**Phase 4 notes (12 Sep 2026, destination cards and place shells).**

*Every Milky Way destination now wears the two-line card.* GDD 8.4 updated in the same commit: name plus a caps second line, width fitted to the longer of the two, 24 mm tall instead of 16. The owner asked for the treatment the two surviving inherited markers used ("Solar System", "Galactic Center") on every destination; those two markers are now switched off by **Cosmic Simulation → Retire Legacy Milky Way Markers** (`LegacyPoiCleanup`), so nothing draws twice. They were missed by CS-050 because neither carries `CardPOI` — both are `poi_prefab_large`, whose POI component is `PlanetPOI`, and the component sits on a child called `POI` rather than on the renamed instance root.

*A bug worth knowing about: **Build Destination Tags had been failing silently for some time.*** `Bind` wrote a serialized field `anchors` that `DestinationTags` did not have, so `FindProperty` returned null, the NRE aborted `Build` before `SaveAsPrefabAsset`, and the prefab kept whatever an older run had left — nine tags, with the three outside galaxies missing entirely. The field now exists, along with the run-time re-hang its comment had always promised: `DestinationTags.LateUpdate` puts every card directly above its point in world space so the leader lands on it however the player has turned the map. Twelve tags build now.

*Cards sit on their own tiers.* Because they billboard, two cards at the same height overlap whenever the player is near the line joining them — distance on the disc does not help. `SpaceOut` gives every card above the plane its own height, outermost lowest, stepping by a card height + 2 mm; the build report lists what moved.

*Place shells on all seven destinations.* `PlaceShellBuilder.Wired` was one entry (Orion) as a proof; it is now all seven, at the owner's request. The earlier astronomical objection — that a shell is meaningless for the compact objects you could not be inside — is recorded in that file's class doc rather than deleted. D-010 is paid for in copy: every shelled destination carries a line saying the surroundings are an impression built from the photograph, and **the builder refuses to attach a shell to a destination whose copy does not say so**, so the two cannot drift.

*Open, and the reason this is not finished:* the owner's note that **the nebulae should be actually 3D, not 2D images**. The shell is a photograph on a dome plus a second copy nearer for parallax, which is exactly the criticism. Next step is a real volumetric layer — point-sprite cloud distributed in three dimensions from the plate's own luminance and colour, in the manner `DrawStars` already renders the galaxies — with the shell kept as the distant backdrop behind it.

## Phase 5 — Andromeda, Galaxies, Cosmic Web

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-063 | TERM | Galaxy sprite atlas (deep-field cutouts + Higgsfield fill) | CS-005 | todo |
| CS-064 | TERM | Galaxies content prefab (no scene — see the drop note under Roadmap §4.4 below; roadmap still names `galaxies_scene`); passthrough allowed; **pinching a sprite gently pushes it, no grab, per GDD §4.6/F-25**; verify | CS-062, CS-063 | todo |
| CS-065 | CC | Cosmic Web generator (Voronoi filaments → point buffer) + violet ramp for star shader | — | done (built 12 Sep, terminal session; play-mode/frame-time still open — CS-066) |
| CS-066 | TERM | Verify `cosmic_web_content_prefab` (built as a content prefab, not the `cosmic_web_scene` the roadmap still names — see the drop note under Roadmap §4.4 below); frame time | CS-065 | todo |
| CS-067 | TERM | Thumbnails for the three new scenes | CS-061, CS-064, CS-066 | todo |
| CS-102 | CC | Andromeda content builder: `StarsData` variant, content prefab, wire the module | CS-036 | done |
| CS-103 | TERM | Run the Andromeda builder, tune the look, verify the tile opens and is grabbable | CS-102 | todo |
| CS-106 | CC | Hand grab does not reach `ManipulationHandler`: forwarder + the three builders | CS-065, CS-102 | done |
| CS-107 | CC | Restore (R, dock Restore) cannot bring back a moved nebula, Cosmic Web or Andromeda | CS-106 | done |
| CS-108 | CC | The dock's drag bar has the same input gap, and would drag itself off the dock | CS-106 | done (unverified — structurally confirmed in the rebuilt prefab; hand/device checks open) |
| CS-108 | CC | The dock's drag bar has the same input gap, and would drag itself off the dock | CS-106 | done |
| CS-118 | CC | `DockController.SetVisible` never toggles the `plate` Image, so a hidden dock still shows its dark plate floating in the room | CS-028 | todo |

**Phase 5 notes (12 Sep 2026).**
*CS-102 done (written and compile-checked by reading only — **no editor was available, so the builder has never been run and none of its output exists yet**).* `andromeda.asset` had **both** `SceneName` empty and `ContentPrefab: {fileID: 0}`, which is the one combination `ExperienceDirector.Switch` refuses outright (CS-036), so the Andromeda tile has been dead by construction since it was created — contract row F-23 could not pass. One new file: `Assets/scripts/Editor/AndromedaBuilder.cs` (menu **Cosmic Simulation → Build Andromeda Content**). Nothing existing was edited.
*What it makes when it is run:* three `StarsData` assets in `Assets/scriptable_objects/star_data_scriptabe_objects/` beside the Milky Way's three, three materials in `Assets/materials/galaxy_materials/`, `Assets/prefabs/experiences/andromeda_content_prefab.prefab`, and the `ContentPrefab` assignment on `andromeda.asset` — the same four-step shape `NebulaPrefabBuilder` and `CosmicWebBuilder` use. **The module's `ContentPrefab` was deliberately not hand-wired in the YAML**: the prefab does not exist yet, and a GUID pointing at nothing is indistinguishable at run time from today's bug while looking fixed in the diff.
*The galaxy is the Milky Way's own renderer, re-parameterised,* exactly as Technical Overview 7.3 prescribed: three `SpiralGalaxy` layers (clouds, dust, stars) on one node, each with a baked `StarsData`, drawn by `DrawStars`. **17 200 points** (9 000 + 5 000 + 3 200) against the Milky Way's 18 720 and the 400 000-sprite frame ceiling in 7.4 — 4.3% of it; ~4 MB of generated YAML, in line with the 4.2 MB the Milky Way's three already cost. Distinguishable at a glance by construction: `MinEllipseScale` 0.10 against 0.02 so the inner disc is empty and the bulge carries the core, `SpiralRotation` 470° against 370° so the arms are wound tight enough to read as rings, half-thickness 5.5% of the radius against the Milky Way's ~19% after its scene stretch, a white-to-blue ramp instead of blue-to-white, and a dust annulus (0.46–1.0 of the radius) the Milky Way has no equivalent of. Deterministic from three fixed seeds, so a re-run with no parameter change writes byte-identical assets.
*Built 12 Sep 2026, terminal session — `andromeda_content_prefab` exists for the first time,* along with its three materials and three `StarsData` assets. This confirms the builder itself runs; CS-103's "tune the look" and "verify grabbable" are still open.
*Two things CS-103 must check first, both inherited rather than introduced.* (1) The `ManipulationHandler`/`IGEPointerHandler` gap recorded above for CS-065 applies here too — Andromeda will be turnable and scalable **on the desktop** (`DesktopMouseInput` right-drag and wheel) and **not by hand** until that forwarder exists. GDD 4.4's "move, tilt, scale with hands" cannot pass before it. (2) `DrawStars.cameraEvent` is a private serialized field and `DrawStars` is added at run time by `SpiralGalaxy.InitializeParticles`, so it cannot be set from a builder: Andromeda draws at `BeforeForwardOpaque`, the default the Milky Way also uses under the same Dimmed environment. If it turns out the skybox or the dim quad paints over the galaxy in a prefab-backed experience, the fix is to expose that field (`CosmicWebRenderer` chose `AfterSkybox` for the same reason) — **not** attempted here, `DrawStars.cs` was owned by another agent.
*CS-065 written but NOT COMPILED — status `doing`, not `done`.* No editor was available in the session that wrote it; every symbol was checked by reading. Compile it before anything else. Four new files: `Assets/scripts/experience/CosmicWebGenerator.cs`, `Assets/scripts/experience/CosmicWebRenderer.cs`, `Assets/shaders/cosmic_web_points_shader.shader` (`CosmicSimulation/CosmicWebPoints`), `Assets/scripts/Editor/CosmicWebBuilder.cs` (menu **Cosmic Simulation → Build Cosmic Web Content**). Four edited: `ScaleLimits.cs`, `ManipulationHandler.cs`, `DesktopMouseInput.cs`, `docs/TECHNICAL_OVERVIEW.md`.
*It feeds the existing star path, it does not duplicate it.* Same `StarVertDescriptor` struct and `StructSize` stride, same `cginc/StarQuad.cginc` six-vertices-per-point expansion, same `CommandBuffer.DrawProcedural` on the main camera. `DrawStars` itself is **not** reused: it is the galaxy's controller and reads `SpiralGalaxy` for tint, ellipse radii and the down-scaled RT path every frame, and a `SpiralGalaxy` stood up to satisfy it would generate a spiral of its own in `Start`. `CosmicWebRenderer` is the same 40 lines of command-buffer lifecycle as `OrbitalTrail.OrbitsRenderer`, which exists for the recorded reason that an immediate-mode draw reaches only one eye.
*The struct is reused with one field repurposed,* and both halves say so: `ellipseDistance`/`curveOffset`/`yOffset` are cylindrical coordinates and `ellipseOffset` carries the 0–1 ramp coordinate. `color` is brightness only — all hue is in the material — so the violet can be retuned without regenerating a point. `_Age` still rotates the volume, which is where the GDD's 0.5°/min drift comes from, in the vertex stage, so it can never fight a two-handed grab.
*Violet ramp = a new shader, not a change to `Galaxy/Stars`.* `spiral_stars_shader` is shipping galaxy content, is the app's hottest vertex program, and carries **none** of the stereo macros Technical Overview 7.1 requires; adding a ramp to it would touch all three facts at once. The new shader has the macros, `#pragma target 4.5`, no geometry stage.
*Point count 120 000,* against 400 000 for the whole frame in 7.4 — reasoning in `CosmicWebBuilder.PointCount`. Not baked: a `StarsData` asset at this size would be ~28 MB of YAML (the galaxy's 8 320 stars are already 1.9 MB), so the cloud is regenerated at run time from the seed, spread over frames at 3 ms each, uploading and drawing each slice as it lands so the web condenses into view.
*Blocking finding for CS-066, and it is not new:* **`ManipulationHandler` does not implement `IGEPointerHandler`**, so `GEInteractable` → `GEInputEvents.ExecuteHierarchy<IGEPointerHandler>` never reaches it. Today it is only ever driven by `ForceSolver`, which forwards to it explicitly. That means the nebula prefabs from CS-051 and the Cosmic Web prefab are **not grabbable by hand**, despite both carrying the component. The fix is a small forwarder component rather than making `ManipulationHandler` implement the interface directly — `ExecuteHierarchy` invokes *every* matching handler on the first object that has one, so a planet carrying both would dispatch twice. Left alone deliberately: it is a shared input-layer change affecting Phase 4 content and wants its own ticket. Desktop mode is covered: `DesktopMouseInput` now right-drags to turn and wheels to scale any `ManipulationHandler` that has no `ForceSolver`, inside its own `ScaleLimits`.
*`ScaleLimits` gained one field, `authoredMetresAtUnitScale`,* because it measures `Renderer` bounds and the web has no `Renderer` at all — without it every limit silently did nothing. Zero keeps the old behaviour, so no existing prefab changes.
*Built 12 Sep 2026, terminal session — `cosmic_web_content_prefab` exists for the first time.* One thing the build needed that this note did not foresee: `cosmic_web_points_shader` had to be added to `m_AlwaysIncludedShaders` in `ProjectSettings/GraphicsSettings.asset`. A `DrawProcedural` shader has no material reference for the build pipeline to find, so without that entry it would be stripped from a player build silently. CS-066's frame-time and play-mode checks are still open.

*CS-106 done (written and compile-checked by reading only — **no editor was available**; nothing below has been compiled or run).* One new file, `Assets/scripts/XR/ManipulationPointerRouter.cs`, and one line in each of the three builders that write the affected prefabs. **Nothing on the planet path was touched:** `GEInteractable`, `GEInputEvents`, `ForceSolver` and `ManipulationHandler` are byte-for-byte unchanged, which is how a body's grab is guaranteed to behave exactly as it did.
*Why a forwarder and not `ManipulationHandler : IGEPointerHandler`.* `ExecuteHierarchy` invokes every enabled handler on the **first** GameObject up the chain that has one and then stops, and the two shapes bodies are built in fail differently. `solar_system_planets_content_prefab` puts `GEInteractable`, `ManipulationHandler` and `ForceSolver` on one GameObject (`body_*`), so the handler would be invoked **twice** per pinch. The `poi_*` prefabs are worse: `ManipulationHandler` and `GEInteractable` are on the mesh (`earth_sphere_mesh`) and `PlanetForceSolver` is on the parent `force_grab_root`, so the walk would stop at the mesh and **the solver would never hear a pinch at all** — no dwell, no tractor beam, no force pull. Both are avoided by keeping the interface off the handler and putting the delivery in a component that is only ever added where there is no `ForceSolver`. The router also self-disables (not merely no-ops) if it ever finds one above it, because `ExecuteHierarchy` skips disabled behaviours, so a misplaced router lets the walk continue instead of swallowing the event.
*Re-runs the [TERM] track owes, in this order:* **Cosmic Simulation → Build Nebula Prefabs** (the seven `Assets/prefabs/nebulae/nebula_*_prefab.prefab` exist and are stale), then **Build Cosmic Web Content** and **Build Andromeda Content** (neither prefab has ever been generated, so those are first runs, not re-runs). All three builders are idempotent. Nothing was hand-edited in prefab YAML.
*Re-run 12 Sep 2026, terminal session, verified by GUID rather than by eye.* All seven `nebula_*_prefab`s went from zero to one reference each to `ManipulationPointerRouter` (guid `c9080a9df18d4c4caaaafc7a7d8cd400`) and `FreePlacementAnchor` (guid `64e1ba70b56d49b3917223e4351fffdd`). They really were stale and ungrabbable before — this code had landed in the repo but never in these committed prefabs. Structural confirmation only; the hand-grab checks in the queue above are still open.
*One thing widened deliberately, and it is one line:* the nebula builder now sets `playGrabSounds` true, which `ManipulationHandler`'s own tooltip already names the nebula overlays as a case for and which the Cosmic Web and Andromeda builders already set. It was inaudible before because nothing could grab an overlay.
*What [TERM] must check that reading cannot settle.* (1) Two hands on one overlay — `GEInteractable` selects in Multiple mode, so both pinches should arrive as separate pointers; confirm a scale, not a fight. (2) Fingertip poke: `XRPokeInteractor` selecting the sphere collider reads as `IsNear`, but whether a poke select fires at all on these radii is a device question. (3) `ExperienceDirector.GrowIn` writes `localScale` every frame for `growInSeconds` — grabbing during the grow-in now has two writers in `Update` with undefined order. (4) On the desktop a left-drag on a nebula or Andromeda will now **move** it (the mouse arrives through the same routing), which is new and is the desktop half of GDD 4.3/4.4's one-hand move; confirm it does not fight the orbit gesture. A left-press on the Cosmic Web plays the grab sound and moves nothing, which is correct — it is `TwoHandedOnly`.
*CS-107 (the restore gap, raised in review and deliberately left out of CS-106).* `DesktopMouseInput.RestoreLayout` looks for `LayoutRig`s and then for `ForceSolver`s; a nebula overlay, the Cosmic Web and Andromeda have neither, so R and the dock's Restore cannot bring one home — already true on the desktop today, since the wheel and right-drag can resize and turn them. It is its own ticket because it needs a home pose that only the spawner knows (`ExperienceDirector.OpenDestination` computes a world position; prefab content is spawned at identity under `experience_content_root`) and because `FreePlacementSolver` — the sanctioned place for "nothing snaps back" — is `[RequireComponent(typeof(ForceSolver))]` and reads solver state throughout, so making it work without one is a design change, not a patch.
*CS-107 done (written and compile-checked by reading only — **no editor was available**; nothing below has been compiled or run).* One new file, `Assets/scripts/experience/FreePlacementAnchor.cs`, plus three call sites in `ExperienceDirector`, two in `DesktopMouseInput` and one line in each of the three content builders.
*The design, and why it is not `FreePlacementSolver`.* That component is `[RequireComponent(typeof(ForceSolver))]`, takes home from the solver's live `RootTransform` and reads solver state in `Update`, `RestoreLayout` and `RestoreImmediate`; there is nothing left of it once the solver is removed. `FreePlacementAnchor` makes the same promise — nothing snaps back, but everything can come home (GDD 5.2, contract F-13) — for content whose home is simply *the pose the spawner put it in*. It stores a **local** TRS, for the reason `FreePlacementSolver` re-reads its root every frame: prefab content hangs off the `ViewLoader`, which the intro moves and rotates to place the experience in the room, so a home remembered in world space would send the object back to where the room used to be. A destination overlay is spawned unparented, where local and world are the same.
*Capture timing is the whole trick.* `ExperienceDirector.GrowIn` writes `localScale` from zero every frame for `growInSeconds`, so a capture taken on the first `LateUpdate` — or by any "remember where you were a moment ago" scheme — would record a fraction of the real size and Restore would shrink the object to it. The capture therefore happens in the director, immediately after `Instantiate` and **before** the grow-in starts, at both spawn sites: the `ContentPrefab` branch of `SwitchRoutine` (Cosmic Web, Andromeda) and `OpenDestination` (the nebulae), which is also the only code that knows the world position a destination belongs at.
*Why the anchor is added at run time as well as by the builders.* `FreePlacementAnchor.CaptureHome(Transform)` walks the spawned content and, for every `ManipulationHandler.HostTransform` with **no `ForceSolver` on it or above it**, adds an anchor if one is missing and then captures. That is the `ManipulationPointerRouter` guard shape, and it can only ever land on an object that something can move and that nothing else brings back. It matters because the three builders' output is stale or absent in the repo: the committed `nebula_*_prefab`s do not even contain CS-106's router yet, and `cosmic_web_content_prefab` / `andromeda_content_prefab` have never been generated. Without the run-time half the fix would be inert until three menu items are run; with it, the builders' `AddComponent` is the durable form and keeps `restoreSeconds` tunable per prefab.
*Solver-backed objects are provably untouched.* `ForceSolver`, `FreePlacementSolver`, `LayoutRig`, `ManipulationHandler` and `GEInteractable` are byte-for-byte unchanged. `DesktopMouseInput.RestoreLayout` keeps both its passes in the same order and still returns early on a rig — the anchor pass is added *above* them, outside both branches, because the rig branch returns. `ExperienceDirector.RestoreEverything` keeps its `ForceSolver` loop exactly and adds the anchor sweep after it. The only prefab the director spawns that carries bodies, `solar_system_planets_content_prefab`, was parsed: all **ten** of its `ManipulationHandler`s sit on the same GameObject as a `ForceSolver` and every `hostTransform` is null, so `CaptureHome` adds **zero** anchors there. The `poi_*` bodies and the moons are never handed to `CaptureHome` at all, and an anchor that ever did find a solver above it logs and disables itself.
*Routes covered, and the one that is not.* `R` → `DesktopMouseInput.RestoreLayout`. Recenter → `DesktopMouseInput.ResetView`, which is where all three desktop routes to that word converge (the `Home` key, `DesktopDock.Recenter`'s button, and the legacy HUD's Reset View). Experience switch → `RestoreEverything`, snapped rather than eased to match the `ResetToRoot` beside it, since the place is being taken apart. The layout pop-up is **not** a route for this content by construction: `DockPopup.Open` refuses a module without `HasLayoutChoice`, and none of these three has layouts. **Still open:** the *world* dock's Recenter button goes to `DockController.Recenter`, which another agent held open this session — it wants the one line `FreePlacementAnchor.RestoreAll();`, without which headset Recenter restores the dock but not a moved overlay. Separately worth noting rather than fixing here: Recenter does not restore solver-backed bodies today either, on any route, which is a live gap against contract F-13 and deserves its own ticket — this ticket deliberately did not change it.
*Deliberately not implemented:* the fourth rule of GDD 5.2, auto-restore of strays after 5 s. It is a body rule, `FreePlacementSolver` already carries a switch to turn it off "for objects meant to be left far away", and all three of these are that: the Cosmic Web is a volume the player stands inside and a nebula can legitimately be pushed past arm's length. The anchor therefore has no `Update` at all.
*What [TERM] must check that reading cannot settle.* (1) Re-run the three builders (below) and confirm each prefab root now carries `FreePlacementAnchor`. (2) Move, turn and scale a nebula, the Cosmic Web and Andromeda, then press `R` and the dock's Recenter in both modes — 0.8 s ease home, no snap, nothing else moves. (3) Press `R` **during** the grow-in: the restore lerp and `GrowIn` both write `localScale` in `Update` with undefined order; both converge on the same value, but it wants an eye on it. (4) Confirm planets and the solar row are visually unchanged by `R` and by a switch. (5) The legacy HUD's Reset View button now also brings this content home — intended, but confirm nothing else rides on that button.

*CS-108 (the dock drag bar).* `dock_prefab`'s `drag_bar` carries `GEInteractable` + `ManipulationHandler` with no `ForceSolver`, so it has exactly the CS-106 gap. It was left alone because fixing it is not one line: the handler's `hostTransform` is unset, so it defaults to the bar and a working grab would drag the bar off the dock instead of moving the dock (GDD 8.1: the bar "moves the whole dock"), and the re-tilt toward the player on release wants a listener in `DockController`, which another agent held open. Fix all three together in `UiPrefabBuilder.BuildDock` plus `DockController`, then re-run the UI prefab builder.
*CS-108 written (compile-checked by reading only, when no editor was available) and since compiled clean and structurally verified, 12 Sep 2026, terminal session.* Four files edited and none added: `Assets/scripts/Editor/UiPrefabBuilder.cs` (the `drag_bar` block in `BuildDock`), `Assets/scripts/experience/DockController.cs` (a drag bar section, `LateUpdate`, two lines in `Start`/`FaceThePlayer`, a palm guard), `Assets/scripts/XR/ManipulationHandler.cs` (a setter on `HostTransform` and a new `ManipulationType` property — no behaviour change to any existing path) and `Assets/scripts/XR/DesktopMouseInput.cs` (one guard in `ManipulatedHandler`). Docs: Technical Overview §5.2 and the audio section. Of the seven checks below, (1) is now answered by the rebuilt prefab's field values; (2) through (7) still need a hand or a device and remain open.
*The bar is a handle, not cargo, and that is the whole of it.* The builder now writes `hostTransform` = the **dock root**, `OneHandedOnly`, `playGrabSounds` true, and adds `ManipulationPointerRouter` — the CS-106 component, not a second path. The router is why a pinch arrives at all; the host is why it moves the dock; one hand is because two on a handle would scale and spin the dock and its millimetre canvas, and the two-handed `MoveRotateScale` the stale prefab carries is exactly the "drags itself off the dock" failure wearing a second hat.
*`DockController` re-asserts the host and the hand count on `Start`* (and lazily, since the router it adds may be used the frame it is added), because the committed `dock_prefab` predates all of it — its YAML still reads `hostTransform: {fileID: 0}`, `manipulationType: 2`, and carries no `playGrabSounds` field at all. That is the CS-107 shape: the builder is the durable form, the run-time line is what makes the fix true before anybody re-runs a menu item. It needed the two new properties on `ManipulationHandler`; both are additive, nothing on the planet path calls either, and the host setter re-captures the grab offsets if a hand is already on it. **The grab sound waited for the rebuild, and the rebuild has since happened:** the committed `dock_prefab` now reads `hostTransform: {fileID: 6230769956266320038}` (the dock root), `manipulationType: 0` and `playGrabSounds: 1`, so the sound is live — `DockController` deliberately does not also play it, which would double it.
*The tilt and the size are held in `DockController.LateUpdate`, not by an exception inside the handler.* The handler's one-handed path writes rotation from the hand pose and its two-handed path writes scale, so the dock would roll with a wrist and could be resized by a hand span. `LateUpdate` runs after every `Update`, so it cannot lose that race the way a second `Update` would: the handler keeps the position (that is the gesture), and `FaceThePlayer()` plus the `localScale` captured at grab keep the rest — on the release frame too, which is GDD 8.1's "re-tilts toward the player". Scale is captured per grab rather than once, so it can never undo a size set between grabs. One behaviour added on purpose: palm-up show/hide is not counted during a drag, because hiding the dock switches the bar's GameObject off and would end the grab mid-placement.
*Desktop parity, and a regression it closes before it exists.* The mouse left-drag needs no new code — it arrives through the same `GEInteractable` → router → handler path, with `GEPointer`'s mouse attach point riding the cursor ray. But pointing the host at the dock also put the dock inside `DesktopMouseInput.ManipulatedHandler`, the desktop stand-in for the **two-handed** turn and scale, where a right-drag would have left the dock rolled over at an angle nothing brings back and a wheel notch would have resized its canvas without limit. That method now skips `OneHandedOnly` handlers, so both fall back to pan and zoom; no shipped prefab is `OneHandedOnly`, so the dock bar is the only thing the guard can touch. Worth saying plainly: the **world** dock parks 0.72 m below the desktop eye line on purpose and a desktop camera never looks down, so in practice a monitor player still uses `DesktopDock`, which is pinned to the screen and has nothing to drag — the mouse drag is parity in the routing, not a gesture anyone needs there.
*Confirmed structurally 12 Sep 2026, terminal session, editor available.* The rebuilt `dock_prefab` reads `hostTransform: {fileID: 6230769956266320038}` (the dock root), `manipulationType: 0`, `playGrabSounds: 1`, and `drag_bar` carries `ManipulationPointerRouter`; `DockController`'s other references survived the rebuild. This is a read of the serialised prefab, not a play-mode pinch — checks (2) through (6) below are still open, and (7) is now known to be worse than it was thought to be.
*What [TERM] must check that reading cannot settle.* (1) **Confirmed, 12 Sep 2026, terminal session:** Build UI Prefabs has been re-run and `drag_bar` comes out with `ManipulationPointerRouter`, `hostTransform: {fileID: 6230769956266320038}` (the dock root), `manipulationType: 0` and `playGrabSounds: 1`; `dock_prefab`'s other references (tiles, the four buttons, the utility window) survived the rebuild. (2) In the headset, pinch the bar and walk the dock around: the dock follows, the bar stays on it, the dock stays level and keeps facing you, and letting go leaves it there (nothing snaps back). If the pinch will not land at all, suspect the collider before the wiring — it is 60 × 8 × 2 **mm**, and the **2 mm is the depth axis** (already five times the 1.6 mm bar it draws), so a near-pinch has to land within about 1 mm of the plate plane; widening the collider belongs in `UiPrefabBuilder` in canvas units, not as a metre constant somewhere. (3) Two pinches on the bar: the second must be ignored, not scale the dock. (4) Palm-up with the other hand mid-drag: the dock must not vanish. (5) Press Recenter after a drag — the dock re-parks and its height is re-sampled. (6) On the desktop, right-drag and wheel over the bar must pan and zoom the view, not spin or resize the dock. (7) **A regression this ticket introduced, not a pre-existing false positive — corrected 12 Sep 2026, verifier pass.** Before CS-108 the drag bar had no `ManipulationPointerRouter`, so `GEInputEvents.ExecuteHierarchy` never reached its `ManipulationHandler` and `OnManipulationStarted` on that handler could not fire; `HintCards.WatchManipulation` was already subscribed to it but had nothing to hear. Adding the router makes it fire. Concrete consequence: on first run, card 1 is `hint_grab` / `DismissOn: Grab` ("pinch to grab... pull a planet toward you"); a player who instead pinches the dock's drag bar sets `_actionDone = true`, the card advances, and `PlayerPrefs` records the hints as seen without a planet ever having been pulled. There is a second, smaller half: `HintCards.OnManipulationEnded(ManipulationEventData _)` ignores its source and unconditionally clears `_held`, so with the `hint_scale` card up and a nebula held in one hand, a pinch-and-release on the drag bar with the other hand clears `_held` and the resize in progress is never measured — so "the Resize card is safe" was true only against the false-positive half, not this false-negative one. See **CS-120** below.

*CS-108 done (written and compile-checked by reading only — **no editor was available**; nothing below has been compiled or run).* Two files: `Assets/scripts/Editor/UiPrefabBuilder.cs` (`BuildDock`) and `Assets/scripts/experience/DockController.cs`. Nothing in `Assets/scripts/XR/` or `Assets/scripts/solver_scripts/` was touched — `ManipulationHandler`, `ManipulationPointerRouter`, `GEInteractable`, `GEInputEvents` and every solver are byte-for-byte unchanged, which is how the body grab path is guaranteed to behave exactly as it did.

*The four parts of the fix.* (1) **Host.** The builder serializes the bar handler's `hostTransform` to the dock root, so the bar is a handle and the dock is what moves; it is authored in the prefab rather than patched at run time because re-running the builder is the whole deployment story for this file. (2) **Mode.** `OneHandedOnly`, not the default `OneAndTwoHanded`/`MoveRotateScale`. GDD §8.1 gives the dock a fixed size and one gesture; more to the point `Recenter` writes position and rotation only, so a dock a two-handed pinch had scaled would have no way back to its authored size and `Recenter` would be quietly broken by a gesture nobody asked for. `twoHandedManipulationType` is set to `MoveRotate` anyway, so a later mode change cannot silently hand the dock a scale. `allowFarManipulation` stays on — the ray is the required alternative to the pinch. `playGrabSounds` goes on, the case its own tooltip names: no `ForceSolver` sounds this grab. (3) **Delivery.** `ManipulationPointerRouter` on the bar, the CS-106 forwarder; nothing above the bar in `dock_prefab` handles pointers, so ending the walk there costs nothing. (4) **Re-tilt.** `DockController.LateUpdate` calls `FaceThePlayer()` every frame of the drag, which is what makes the dock re-tilt and is *also* what stops a turned wrist rolling the plate over — the handler's one-handed branch carries the host's rotation with the pointer, and `LateUpdate` runs after every `Update`, so the rotation written here is the last word and the handler's is never seen. Position is left entirely to the handler.

*How the re-tilt reuses the parking maths rather than copying it.* `Recenter` and `FaceThePlayer` now share both steps: `Flatten(direction, out flat)` (project onto the floor plane, world-forward when degenerate) and `ParkedRotation(flatForward)` (`LookRotation` × `Euler(-tiltDegrees,0,0)`). They differ only in the direction they are handed — the head's own forward on a recentre, the line from the player's head to the dock on a drag — which was already true of the two copies, so this is the same behaviour with one owner. A drag therefore always leaves the dock in exactly the class of pose `Recenter` produces: upright, flattened, tilted 25°, unscaled. The head-relative height sampling added today is untouched and still happens only in `Recenter`.

*Recenter still wins.* `FreePlacementAnchor.RestoreAll()` stays the first statement of `Recenter`, above the camera guard (CS-107), and is not reached by any of this. New: if a hand is still on the bar when `Recenter` runs, the handler is disabled and re-enabled, which clears its pointers — otherwise the pose it captured at the grab would drag the dock straight back out on the next frame. That happens *after* the `TryEyeHeight` early return, so a recentre that parks nothing also takes nothing away. The release of a pointer the handler no longer holds is ignored by `ManipulationHandler.OnPointerUp`, so nothing is left stuck. `Update`'s pending-recentre retry loop also stands down while the bar is held, for the same reason.

*A stale prefab fails safe.* The committed `dock_prefab` predates this and the run-time half deliberately does **not** patch it: `DockController` resolves the bar's handler lazily (never in `Awake` — `Recenter` is called from `Start`), and if its `HostTransform` is not the dock root it logs and **disables the handler**. The failure mode of a builder nobody re-ran is therefore the dead bar we already had, not a bar that tears off the plate.

*Desktop parity — the honest answer: there is none, and none is wanted.* The world dock parks 0.72 m below the eye line 0.75 m in front (`heightFractionOfHead`, and `TryEyeHeight`'s desktop branch measures down from the fixed camera on purpose). Reaching it would need a vertical FOV near 88°, and the desktop `GEPointer` is a camera ray through the cursor — so the bar is not merely awkward on a monitor, it is outside the frustum by construction. That is the entire reason `DesktopDock`'s screen-space mirror exists. The mirror is pinned to a screen corner and has no world pose to drag, so F-06 has no meaning there; `Recenter` is the desktop's dock-placement control and it already exists on the mirror, on `Home` and in the legacy HUD. Adding a drag to the screen mirror would be inventing a feature, not parity.

*Two neighbours, checked because they ride on the dock's transform.* **Hide/show still works after a drag** — `SetVisible` toggles the dock's children, which move with the root. **But `SetVisible` has a pre-existing hole, unrelated to this ticket and deliberately not fixed in it:** it toggles `tile_row`, the drag bar and the four buttons, and **not the `plate` Image**, so a hidden dock still shows its dark plate floating in the room. The fix is a `plate` field on `DockController` toggled with the rest, written by `BuildDock`; it needs its own row. **The utility window** is placed beside the dock by `UtilityWindow.Place()` on open and never again, so it did not follow. It now catches up when the drag ends: `DockController` calls `_utility.Open()`, which on an already-open window re-places it and does nothing else — no sound, no repaint — and is asked only of a window that exists and is open. It does **not** follow continuously during the drag, deliberately: calling `Open()` every frame would also clear `UtilityWindow._closeRequested` and could swallow an × pressed with the other hand mid-drag. Continuous following wants a public `Reposition()` on `UtilityWindow`, which is not this ticket's file. The **layout pop-up** is placed once above the tile that opened it, so it is closed when a drag starts; it is a momentary choice, not a window.

*What [TERM] must check that reading cannot settle.* (1) **Re-run `Cosmic Simulation → Build UI Prefabs` first** — none of this is in the committed prefab, and before that re-run the console should carry `DockController`'s new error about the bar's host. (2) Grab the bar near and far: the whole dock moves, the bar stays on the plate, the dock stays upright and tilted through any wrist roll. (3) The 60 × 8 × 2 mm bar collider on a 0.001-scaled canvas is 6 cm × 8 mm in the room — confirm a fingertip and a ray can actually find something that thin, and widen the collider (not the sprite) if not. (4) Recenter while holding the bar: the dock parks and the grab is dropped. (5) A second hand on the bar must do nothing. (6) Palm-up hide/show on a dragged dock, and the plate bug above. (7) Utility window open, drag, release — it should jump back beside the dock. (8) Confirm the drag does not fight `ExperienceDirector`'s grow-in or anything else that writes the dock transform (nothing should).

## Phase 6 — Audio, narration, intro, branding

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-070 | CC | SFX map: assign existing `ui_*` clips in `AudioServiceProfile` for dock/pop-up/label/grab/whoosh | — | done |
| CS-071 | TERM | 11 TTS placeholder narration clips (Andromeda, Cosmic Web, Galaxies, Helix, Orion, 6 moons) via `moon_vo.ps1` pattern | CS-006 | todo |
| CS-072 | TERM | Final voice for the 11 new clips (Higgsfield voice close to the original narrator; owner approves credits) | CS-071 | todo |
| CS-073 | CC | `MusicController` crossfade by environment mode (3 existing tracks) | CS-023 | done |
| CS-074 | TERM | Keep original intro (logo → Earth placement → galaxy); add two one-time hint cards after placement; land in Milky Way with dock shown | CS-016, CS-032 | todo |
| CS-094 | CC | The hint cards have no runtime at all (`grep HintCard` returns nothing), so contract row F-32 cannot pass: build the card component, the first-run flag and the replay entry point CS-074 and the Help button call | CS-006, CS-062 | done |
| CS-120 | CC | `HintCards`' manipulation watch cannot tell a body or overlay grab from dock furniture: pinching the drag bar now satisfies the `Grab` card without a planet ever being pulled, and releasing it clears `_held` and silently drops an in-progress `Resize` measurement on whatever else is held — scope the subscription to "the handler is part of the dock", not `ManipulationType` | CS-108, CS-094 | done (unverified — [CC] track, compile-checked by reading; both faults are hand-only and need a headset to see) |
| CS-075 | TERM | Branding: logo, app icon, splash (Figma vector; Higgsfield hero art optional) | CS-010 | todo |
| CS-076 | CC | Narration-only mute and panel text-size setting | CS-027 | done (unverified) |
| CS-077 | TERM | Source the two sounds nothing in the project owns (Sun touch rumble, seamless loop ~4 s; grow-in whoosh ~0.6 s; assign rumble on the Sun prefab, whoosh on `AudioId.GrowIn`) **and the ambience beds `AmbienceController` (CS-093) now plays but that do not exist yet** — Cosmic Web, Galaxies, Andromeda, Sagittarius A\*, and (open question against GDD §4.3's "Galaxy ambience") possibly the Milky Way map, so 4 or 5 beds; `Assets/audio/ambience_audio_clips/` holds only the 12 inherited body clips today | CS-070, CS-093 | todo |
| CS-093 | CC | `ExperienceModule.Ambience` is declared and read by nothing: play the open experience's bed, stop it on the way out | CS-020, CS-073 | done |

**Phase 6 notes (12 Sep 2026).**
*CS-073 done (unverified in the editor — [CC] track, compile-checked by reading).* `Assets/scripts/experience/MusicController.cs`: two looping 2D `AudioSource`s it makes itself, crossfaded over 2 s (GDD §9). It keys off a new `EnvironmentController.ModeChanged` — the room state is what the spec names, and it changes in three places the experience does not (the dock's passthrough toggle, a destination overlay opening inside the map, a failed switch handing the room back). The event is raised **only when the effective mode actually changes**, so the `Set(module.Environment)` at the top of every switch cannot restart a bed; a subscriber that binds late reads the new `EffectiveMode` property instead, since nothing is raised for the opening state. `ExperienceDirector` needed no change at all. Re-entering a mode is a no-op, and a toggle flipped back mid-fade brings the outgoing bed back up from where it is rather than restarting it. Nothing is pooled or allocated per switch: two sources for the session, released (stopped, clip cleared) when they reach silence so a streamed clip gives its stream back.
*Not inside `AudioService`, and not through its mixer.* Pooled sources are spatial, are returned when their clip ends, refuse the same clip twice inside 50 ms and expose no per-source ramp — right for a UI blip, wrong for an hour-long bed. `AudioServiceProfile` is untouched (CS-070 owns that file); the profile's `musicAudioMixer` is deliberately **not** used as the output, because its five groups exist to be muted and unmuted by the legacy per-view snapshots and would fight a per-source fade. `MusicController.output` is there for the day a plain Music group exists.

*CS-093 done (unverified in the editor — [CC] track, compile-checked by reading).* `Assets/scripts/experience/AmbienceController.cs`: one looping 2D `AudioSource` it makes itself, faded over 1.5 s, playing the open experience's `Ambience` (GDD §9). `ExperienceDirector` stops the bed at the same beat it stops narration (top of `SwitchRoutine`, both describe the place being left) and starts the new one beside the narration at the bottom, after the grow-in and after the routine's single `yield break`, so an abandoned switch is left silent rather than sounding like a place the player is not in. `Adopt` starts it too, for the same reason it now shows a panel: the start module never goes through a switch. **A module with no `Ambience` is silence, not an error** — `ambience_audio_clips/` holds only the 12 inherited body beds and the four new experience beds do not exist yet, so `PlayBed(null)` is the normal case today and is treated as "stop".
*Not folded into `MusicController`, and this is a judgement call worth re-reading.* Music is keyed to the **room state** (four `EnvironmentMode`s, three tracks) and must *not* restart when the experience changes underneath it; ambience is keyed to the **experience** (seven of them) and must change every time the place does. One component answering to two unrelated events with two unrelated clip tables would be harder to read than two, and the ticket forbade editing `MusicController.cs` in any case. They share a shape — a self-made looping source, faded, outside the `AudioService` pool, straight to the listener so Mute still works — and if a third layer ever appears, a small shared base class is the merge worth doing, not one controller with two jobs.
*One source, not two:* unlike the music this never crossfades. The stop and the start are a scene load and a 0.6 s grow-in apart, so the fade-out is long finished; asking for a new bed while the old is audible is still handled (old fades down, new starts — a dip), and the only way to reach it is to switch places twice inside one fade. Per-body ambience (GDD §9's "3D for bodies") is deliberately **not** here: that layer already exists and is spatial, `PlanetForceSolver` playing its own serialized clip through `AudioService` parented to the body. `BodyInfo.Ambience` is still read by nothing — the body prefabs carry their own clip — which is a data-duplication ticket, not an audio one.
*Levels and ducking are guesses:* 0.35 like the music, and **no duck under narration** (the music's 55 % duck already makes room). Both want a listen — **[TERM]**.
*The mapping is a choice, not spec — decisions D-006, sign-off owed:* Passthrough → `background_music`, Dimmed → `bgm_system`, BlackHalo and FullBlack → `bgm_galaxy` (four modes, three tracks). Ducking under narration is D-007, also owed: 55 % over 0.35 s, one multiplier on our own sources, `narrationDuck = 1` turns it off.
*CS-070 done (unverified in the editor — [CC] track, compile-checked by reading).* Every GDD §9 UI and Interaction slot now plays a clip the project already owns, except the two the GDD itself calls new (Sun rumble, grow-in whoosh) — those are CS-077. Six slots reuse an `AudioId` that already pointed at the right clip rather than growing the enum: `Focus` is the hover tick, `Select` is select/poke-press, `CardSelect`/`CardDeselect` are the info panel opening and closing, `ToolboxShow`/`ToolBoxHide` are the dock's layout pop-up (the toolbox is gone and those two ids had no caller left), `ForceDwell` is the force-pull beam, `ManipulationStart`/`End` are grab-and-hold and release.
*Four ids appended, never inserted* — `PokeRelease` (`ui_touch_deselect`), `DockShow` (`ui_handmenu_appear`), `DockHide` (`ui_handmenu_disappear`), `GrowIn` (no clip yet, deliberately: `PlayClip` no-ops on an id with no clip, so the call site can ship ahead of the sound). The enum's own comment says why: Unity serialises an enum as an int, so inserting a member silently remaps every clip already assigned in the asset.
*Call sites:* `GEButton` now falls back to `AudioId.Select` when its prefab assigned no `clickSound`, so every button in the app clicks rather than the handful that got a clip, and sounds `PokeRelease` when a fingertip lifts off (rays have no such moment). `DockTile` sounds its own hover and select — desktop reaches `Choose()` straight from a uGUI `Button`, bypassing the `GEButton`, and in headset mode both fire in the same frame and `AudioService`'s 50 ms same-clip cool-down collapses them. `LabelButton` sounds its own, being a pointer handler rather than a `GEButton`. `DockPopup` sounds open/close, close only when it was actually open and with no target, because the pooled source would otherwise be parented to the object it is deactivating.
*`ManipulationHandler` gained one opt-in field, `playGrabSounds`, default off,* so nothing changes for existing prefabs. `ForceSolver` already plays grab and release as it enters and leaves its Manipulation state, and `GEInputEvents.ExecuteHierarchy` delivers to **every** enabled handler on the first matching GameObject — both components sit on the same object on a body, so an unguarded emitter here would double them. Turn it on for the things grabbed with no solver: the nebula overlays, the cosmic web, the dock drag bar.
*Three one-line calls belong in files this ticket could not touch* — see the report/commit body: `DockController.SetVisible` (`DockShow`/`DockHide`), `ExperienceDirector.GrowIn` (`GrowIn`), `InfoPanel.Show`/`Hide` (`CardSelect`/`CardDeselect`). Until they land, the dock and panel open silently and the four appended ids have three callers, not six.
*Closed 12 Sep 2026 (`f9f2fa1`), same ticket, later commit — the paragraph above is now history, not the current state.* All three calls landed once the ids they needed were committed. The dock show/hide sound sits under the existing equality check, so a held palm re-asserting the same state stays silent. The grow-in whoosh plays with no target transform, deliberately — it belongs to the arrival, not the object, and a second switch can destroy the object mid-grow while the sound should not be cut off with it; its id still has no clip, so the call ships ahead of the sound and goes live the day the file lands. The panel open/close sound is edge-triggered rather than hooked to `Show`/`Hide` directly, because those are idempotent setters a caller can drive every frame while visibility is a predicate recomputed in the frame loop — hooking the setters would have sounded on every spawn and stayed silent on the one open that actually matters. Not compiled, not heard.
*Pre-existing bug found, not fixed (out of scope):* `GEButton.Click()` plays an authored `clickSound` parented to the button, so a pop-up option button whose own click closes the pop-up cuts its sound off at `SetActive(false)`. No prefab assigns a `clickSound` today, so nothing hits it yet.

*Run 12 Sep 2026, terminal session.* `MusicController` sits on `cosmic_systems` (`m_Enabled: 1`, owner `m_IsActive: 1`, so it will actually run) with three unique clips across the four room states, a 2 s crossfade and a 0.55 narration duck. The legacy `MusicAudioSources` root reads `m_IsActive: 0` — genuinely deactivated with `SetActive(false)`, not merely muted by a mixer snapshot, which is exactly the thing this note below asks someone to confirm. `desktop_dock_prefab` is referenced 23 times in `core_systems_scene`. Not yet heard: the play-mode listening pass (crossfade timing, levels) is still open.
*What the terminal track must do and verify (new [TERM] work):* run **Cosmic Simulation → Install Runtime Systems**, which now adds `MusicController` to `cosmic_systems`, fills its three clips, **and switches off the `MusicAudioSources` object in `core_systems_scene`** — five inherited beds all playing the *same* clip through five snapshot-driven mixer groups, which would otherwise sit underneath every crossfade. Then a play-mode listen: switch Milky Way → Sagittarius A* (Dimmed → FullBlack) and hear a clean 2 s crossfade; poke the passthrough toggle twice quickly and hear no restart; check `MusicController.Instance.CurrentTrack` follows the mode; confirm the levels against −16 LUFS (0.35 on all three tracks is a guess). Clip import settings were read but not exercised: all three are 118 s stereo 48 kHz (three arrangements of one piece, which is why a crossfade between them sits well), `background_music` streams on every platform and the other two stream on Android through a platform override, so no two beds ever share a stream — the only mode pair that maps to the *same* clip (BlackHalo/FullBlack) is short-circuited as a no-op and never plays it twice at once. All three have `preloadAudioData` on and `loadInBackground` off, so the first `Play()` may hitch; that is an import-settings call for the asset track, not this ticket.

*CS-094 done (unverified in the editor — [CC] track, compile-checked by reading only).* Three new files: `Assets/scripts/experience/HintCards.cs`, `Assets/scripts/experience/HintCardSet.cs`, `Assets/scripts/Editor/HintCardBuilder.cs` (menu **Cosmic Simulation → Build Hint Cards**). Nothing existing was edited — the ticket's owned-file list covered most of the input layer, and the design turned out not to need a change there.
*It follows `SwitchNotice`, not the dock.* No prefab, no scene object, no wiring: `HintCards.ShowIfFirstRun()` makes its own `DontDestroyOnLoad` object and builds its own canvas on first use. World-space at the millimetre scale (200 × 120 units = GDD §8.6's 200 × 120 mm) parked in front of the player, or above the dock when one is visible — which is the replay case, since the dock is hidden during onboarding. Desktop is a screen-space panel at `sortingOrder` 65, above `DesktopDock`'s 50 and `SwitchNotice`'s 60. White outlined text, **no plate**, because a plate punches a hole in passthrough (GDD §8.3) and because the intro's own prompts have none.
*The art is the intro's art.* CS-017 exported five rounded rects, a tile foot and six icons — **no hint-card frames**, so there are none to use and the builder says so in the console. The fallback is `onboarding_pull_sprite` and `onboarding_hold_sprite` (white line-art hand, open and pinched, already in the project and already shipped by `onboarding_force_hl2_prefab`), cross-faded on a 3 s loop with a sine drift, and shown twice mirrored for the two-hand card. Drop `hint_grab_01.png`, `hint_grab_02.png`, `hint_scale_*.png` into `Assets/ui/figma/` and re-run the menu item to replace them; nothing else changes.
*A Resources asset, deliberately, against the project's prefab-reference convention.* There is no prefab to hold sprite references, and a sprite referenced only by a `Resources.Load`-less field is not in the player build. `Assets/scriptable_objects/Resources/hint_cards.asset`, loaded by name — the same pattern and the same folder as `AudioServiceProfile`. Copy is parsed out of `docs/copy/hints.md` by the builder, so a writer still owns the words.
*Which card is which is keyed off the heading in hints.md, not off its prose.* "Dismisses on: the first two-hand scale" is a sentence for a person; a table in `HintCardBuilder.Behaviour` maps `hint_grab` → `Grab` and `hint_scale` → `Resize`.
*Every hand path has a mouse path.* Card 1 ends on any `ManipulationHandler.OnManipulationStarted` (the force-pull grab goes through it — `ForceSolver.OnPointerDown` forwards), on a poke or far pinch of the card's own `GEButton`, on a left click or `Space`/`Enter`/`Esc`, or after 6 s. Card 2 ends on the **held object changing size by 12 %** rather than on counting hands: that is true of a two-handed scale *and* of the desktop wheel, which never goes through a `ManipulationHandler` pointer at all (`DesktopMouseInput.HandleWheel`). A 0.5 s settle delay before the baseline is sampled keeps a force-pulled planet's grow-in from answering the card for the player.
*The seen flag is `PlayerPrefs` `CosmicSimulation.HintsSeen`,* the fourth key in the project and the third under that prefix. Set on the way **in**, not out: the intro plays every launch, and a player who takes the headset off mid-hint should not meet the cards again every time. `HintCards.Replay()` bypasses it; `HintCards.Forget()` clears it.
*Run 12 Sep 2026, terminal session.* `hint_cards.asset` now exists at `Assets/scriptable_objects/Resources/hint_cards.asset`; `HintCards.ShowIfFirstRun()` no longer logs an error. Nothing has shown a card in play mode yet.
*What the terminal track owes, beyond compiling it.* (1) Run **Cosmic Simulation → Build Hint Cards** once — until it runs, `ShowIfFirstRun` logs an error and shows nothing. (2) **CS-074** calls `CosmicSimulation.HintCards.ShowIfFirstRun();` from the intro once the Earth pin is placed — `PlacementControl.OnContentPlaced` is the honest moment; the component is created by the call, so there is nothing to wire. (3) The Help buttons, one line each and **not done here** because both files are owned elsewhere: `DesktopDock.ToggleHelp` and `DockController.Start` (whose `helpButton` has no `OnClick` listener at all today — a separate gap) want `CosmicSimulation.HintCards.Replay();`. (4) A play-mode look at the card in both modes: legibility against a bright wall, whether the card's collider sits between the hand and the thing the card says to grab (it parks 28 cm below eye level to avoid it, and a poke that hits the card dismisses it rather than doing nothing), and the two-line wrap of the longer sentence at 200 mm.
*Not done, on purpose.* The dock is not hidden while a card is up (GDD §8.1 asks for it) — that is `DockController.SetVisible` state the hint component should not be fighting over, and during onboarding the dock is not shown anyway. No VO, no sound: GDD §9 gives the cards neither.
*Regression found 12 Sep 2026, verifier pass — CS-108 made this reachable, CS-094 itself has no bug.* `HintCards.WatchManipulation` subscribes to every `ManipulationHandler.OnManipulationStarted` in the scene with no way to tell a body or overlay grab from dock furniture. Before CS-108 the drag bar had no `ManipulationPointerRouter`, so its handler's `OnManipulationStarted` could never fire and the subscription was inert against it; now it fires. A player who pinches the drag bar while card 1 (`hint_grab`, `DismissOn: Grab`) is up satisfies it without ever pulling a planet, and `HintCards.OnManipulationEnded` ignores its source and unconditionally clears `_held`, so releasing the bar mid-`hint_scale` silently drops whatever resize was actually being measured. See **CS-120** above; full detail is in the CS-108 note's check (7) in Phase 5.
*CS-120 done 12 Sep 2026 (unverified in the editor — [CC] track, compile-checked by reading only).* `HintCards` now has one discriminator, `IsDockFurniture(handler)`, which asks whether the handler's transform `IsChildOf(DockController.Instance.transform)`; `WatchManipulation` skips those when it binds and `OnManipulationStarted` re-checks, for a dock that came into existence after a card went up. Deliberately **not** a `ManipulationType` test: one-handed is unique to the drag bar today but it is also the enum's zero value, so any future handler left at the YAML default would silently stop answering the cards. `OnManipulationEnded` now clears `_held` only when the ending source is the object it recorded (`data.ManipulationSource == _held.gameObject`), so a release it cannot attribute leaves the measurement alone. The desktop half is untouched and unaffected — there a left click dismisses any card by design, and the wheel answering `Resize` regardless of what it is over is pre-existing, out of this ticket's scope, and filed nowhere. **Needs a headset:** with card 1 up, pinch the drag bar and confirm the card does *not* advance — it should stay up until a real pull or its own 6 s timeout (the seen flag is written at `Begin`, not by answering a card, so that part of the filing was loose); then with card 2 up, hold a nebula in one hand, pinch and release the bar with the other, and confirm the resize that follows still answers the card.

## Phase 7 — Device pass, performance, release

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-080 | TERM | Quest Link full-flow session with hands; tune reach, dock height, panel sizes | Phase 3–6 | todo |
| CS-081 | TERM | Standalone APK; on-device run; OVR Metrics ≥ 60 fps @ 72 Hz | CS-080 | todo |
| CS-082 | TERM | Performance pass (budgets: Technical Overview §7.4); profile Galaxies and Cosmic Web first | CS-081 | todo |
| CS-083 | TERM | Passthrough alpha audit of new shaders; dim quad ordering | CS-081 | todo |
| CS-084 | CC | Store readiness: privacy policy page, About links, description draft | — | done (drafts only) |
| CS-085 | TERM | Regression: desktop + Quest matrices (GDD §5.3, Technical Overview §12) | CS-082 | todo |
| CS-095 | TERM | Compile and verify the 25 stereo-macro shader fixes per eye on the standalone Android build | — | doing (compile clean and render-mode settled 12 Sep terminal session; per-eye device check still open) |
| CS-096 | CC/TERM | The 8 reachable shaders `docs/SHADER_STEREO_AUDIT.md` could not fix mechanically | CS-095 | done (unverified — repo half; per-eye device check open) |
| CS-086 | TERM | About slate: delete 4 orphaned Microsoft-page `Hyperlink`s (`hl2_for_devs`, `galaxy_explorer`, `original_galaxy_explorer`, `microsoft_services_agreement`), re-point 2 (source code, privacy) — prefab surgery via `PrefabUtility.LoadPrefabContents`, not hand-edited YAML | CS-084 | todo |
| CS-112 | TERM | Decide the fate of the vendored TouchScript TUIO/OSC module (`OSCsharp.dll`, `TUIOsharp.dll`) before submission — inert today (no `TuioInput` reference anywhere) but an automated APK scan could flag the network capability against a privacy policy that says "no network calls"; remove it or record why it stays in `docs/decisions.md` | CS-084 | done (12 Sep terminal session; module removed, compiles clean, D-009) |
| CS-113 | CC | Own the version number in `Quest3ProjectSetup`, the same pattern D-001a already set for the bundle id — no version constant exists anywhere today, `PlayerSettings.bundleVersion` is whatever Unity's default is | CS-002 | done |
| CS-114 | TERM | Show the version on the About slate — no text element for it exists; add one and drop a `LegalNoticeText` (notice `Version`) on it, which fills it from CS-113's constant; no further code needed | CS-113, CS-086 | todo |
| CS-115 | TERM | Verify the exported `AndroidManifest.xml` requests only what `docs/store/PRIVACY_POLICY.md` describes and nothing unexpected (e.g. `RECORD_AUDIO`, `INTERNET`) arrived transitively from a package | CS-084, CS-081 | todo |
| CS-116 | CC | Bundle `License.txt` (the MIT notice) into the build — not confirmed to ship in the APK or be reachable from the About screen today, which is thinner than the licence requires | CS-002 | done (code half; prefab half is CS-117) |
| CS-117 | TERM | About slate licence page: add a `legal_page` group under `links_offset` holding a `legal_text` (`LegalNoticeText`, notice `GalaxyExplorerLicense`) and an `our_notice_text` (notice `ProjectNotice`), fill `AboutSlate.aboutPage` / `licencePage`, and point one link at `AboutSlate.ToggleLicencePage` — CS-116's in-app half, prefab surgery only | CS-116, CS-086 | todo |
| CS-118 | TERM | Stereo instancing for the three `CommandBuffer.DrawProcedural` sites: measure whether BiRP applies its instance multiplier to an app-injected command buffer, then use `CommandBuffer.SetInstanceMultiplier` (**not** a hardcoded `instanceCount: 2`) | CS-095 | doing (answered by research 12 Sep; one BiRP behaviour left to measure on device) |
| CS-100 | CC | *(optional, Phase 7)* Remove TouchScript and switch Active Input Handling to Input System only, once nothing depends on it | CS-061, CS-112 | todo |
| CS-101 | TERM | *(optional, Phase 3)* Re-UV or re-project the Moon and Earth maps so the USGS / Blue Marble upgrades can drop in, or close `dropped` with the reason recorded | CS-005, CS-047 | todo |

**Phase 7 notes (12 Sep 2026).**
*CS-084 moved `todo` → `doing`; it had shipped a real draft while still marked untouched.* `docs/store/ABOUT_COPY.md`, `docs/store/PRIVACY_POLICY.md`, `docs/store/STORE_LISTING.md` and `docs/store/STORE_READINESS_CHECKLIST.md` are all on disk (commit `6e4a6fd`). The privacy policy is built from what the repo actually does, not a template: hand tracking and the passthrough camera are the only sensing features, no app code reads a camera frame, room placement persists only on the dead HoloLens path, three local non-identifying `PlayerPrefs` keys exist, and every Unity cloud service is off in project settings. What could not be established from the repo — where the policy will be hosted, the legal entity's name, whether the TouchScript TUIO module should be removed — is listed as an open question for the owner rather than asserted. The checklist itself proposed ticket ids before this file had rows for them (its own header says so); CS-086, CS-112, CS-113, CS-114 and CS-115 above are those items given the ids they actually landed under here, per the ticket id map. Not done: none of it has been read by anyone but the agent that wrote it, and every placeholder ({SOURCE_URL}, {PRIVACY_URL}, company name) is still a placeholder.

*CS-113 done (unverified — [CC] track, no editor, compile-checked by reading only).* `Quest3ProjectSetup.AppVersion = "0.9.0"` is now the one place the version lives, written to `PlayerSettings.bundleVersion` by `ConfigureProject()` and, because that script never runs it, directly by `Unity6WindowsBuild` as well, so desktop and Quest cannot show different numbers. The Android version code is **derived** from the string (`major*10000 + minor*100 + patch`, so 900) rather than kept as a second number, since Meta only checks that it increases and two hand-maintained numbers drift; `ConfigureVersion` logs an error if the string is not `major.minor.patch` or if minor/patch exceed 99, which would break the ordering. Why 0.9.0 and not 1.0.0: nothing has been submitted and phases 6-7 are open, so 1.0.0 is reserved for the first store build. `ProjectSettings.asset` still reads `bundleVersion: 1.0` / `AndroidBundleVersionCode: 1` and was deliberately left alone — the point of D-001a is that the build script overwrites it. **[TERM] must confirm** that after one *Configure Project* those two lines become `0.9.0` and `900`, and that `Application.version` reads `0.9.0` in play mode.

*CS-116 done in code; the in-app half needs CS-117 (prefab surgery).* `License.txt` at the repository root is the single source of truth and is now copied verbatim to `Assets/Resources/legal/galaxy_explorer_license.txt` by `Quest3ProjectSetup.EnsureLegalNoticesShip()`, which runs inside `ConfigureProject()` and again as a gate in `Quest3Build.BuildApk` — a build whose notice would not ship now aborts instead of producing an undistributable APK. **Resources, not StreamingAssets**: on Android StreamingAssets lives inside the APK and is only readable through `UnityWebRequest`, and an async read is a bad foundation for text that must always be displayable. Our own copyright is a **separate** file, `cosmic_simulation_xr_notice.txt` — Microsoft's notice is not reworded, trimmed or merged with ours. Runtime access is `LegalNotices` (lazy, cached, logs an error and falls back to the attribution line if the resource is missing) and `LegalNoticeText`, a component that writes a chosen notice into a `TMP_Text` so the About slate needs no new code. `AboutSlate` gained `aboutPage`/`licencePage` arrays and `ToggleLicencePage()`, both inert while the arrays are empty, and its fade cache now uses `GetComponentsInChildren<Renderer>(true)` — without that, anything inactive at `Start` never gets its alpha driven and pops in at full opacity. **Not done, and not claimable without a build:** nobody has opened the APK to see the text asset, and there is no version or licence element on the slate yet (CS-114, CS-117).

*CS-096 done (unverified — [CC] half only, no editor, no compiler; read back as a compiler would and nothing else).* The render-mode question this ticket sat on was settled from the package source during the same wave, and it inverts the risk: `OpenXRRenderSettings` declares `MultiPass = 0, SinglePassInstanced = 1`, and the settings asset holds `1` for **Android** and `0` for **Standalone**. So the shipping Quest 3 build is single-pass instanced and **Link is multi-pass** — a missing stereo macro breaks one eye on device and is invisible on Link. Every shader below was treated as shipping-critical on that basis.
**Six of the eight were fixed.** `black_hole_gravitational_lensing_disc_optimized_shader` first, as the audit asked: it gets the vertex macros *and* `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` in `frag`, because the ray march reads `_WorldSpaceCameraPos` per pixel and that uniform is `unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex]` under single-pass instanced — without it both eyes march from the left eye's origin and the disc still looks like a black hole, lensed from the wrong place. Shape copied from `nebula_card_shader`. The four procedural-draw shaders (`spiral_stars`, `spiral_stars_negative`, `spiral_stars_cloud`, `orbital_trail`) all had `vert(uint vid : SV_VertexID)` — a bare parameter with nowhere for an instance id to arrive — so each gained the `struct appdata { uint vid : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID }` that `cosmic_web_points_shader` already uses on the same code path, plus the v2f pair and the frag pair, matched to that reference. In `orbital_trail` the `v2f o = (v2f)0;` was **hoisted to the top of `vert`**, because `UNITY_MATRIX_VP` is built on line three and that is `unity_StereoMatrixVP[unity_StereoEyeIndex]`; the file says so, as the three CS-095 hoists do, since it is exactly the reordering a tidy-up would undo. `occluder_shader` got the semantic fix and the macros together: its `v2f` position is now `SV_POSITION`, not `POSITION`. The old "no parallax to lose" argument for leaving it was wrong under single-pass instanced — without the slice index every fragment lands in eye 0 and only the **left** eye ever fades to black.
**The screen-compose pair was investigated and deliberately not patched, and the reasoning is now in the two files rather than in anyone's head.** `DrawStars.CreateBuffers` computes `_useDownscaledTarget = renderIntoDownscaledTarget && !XRSettings.isDeviceActive`, so the whole render-to-texture chain is off whenever a headset is attached — a 2D `RenderTexture` cannot feed a stereo eye texture. Only pass 0 of `screen_compose_shader`'s four has a caller at all (the two `CommandBuffer.Blit` calls); passes 1–3 have no consumer anywhere. If that path is ever revived for XR the fix is per-eye render-texture arrays in `DrawStars`, not macros here. `screen_clear_shader` is worse than dead-on-XR: its material is bound to a `screenClearMaterial` key that `milky_way_prefab` still serialises but `SpiralGalaxy.cs` **no longer declares**, so nothing reads it in any mode. It is a deletion candidate, not a stereo fix — worth its own ticket rather than a drive-by here.
**`#pragma multi_compile_instancing` (acceptance item 5), one answer applied to all five: it is not load-bearing for stereo.** It gates `INSTANCING_ON` — GPU instancing and per-instance property arrays — while the eye index rides on `STEREO_INSTANCING_ON`, which Unity's compiler adds by itself when the render mode calls for it; `TMP_SDF-Mobile` carries the full macro set with no such pragma and draws correctly in both eyes. So `ring_shader`, `intro_shaders/placement_ring_shader` and the two `intro_placement_object_*` shaders were **left without it** (adding it would also require a `UNITY_TRANSFER_INSTANCE_ID` each, for a variant nothing uses), and `sun_shader.shader` keeps its pragma but its **comment is corrected** — dropping a pragma from a shipping shader gains one variant and risks that material's instancing flag for nothing. The four procedural shaders above did not get it either, for the same reason, which does mean they differ from `cosmic_web_points_shader` in that one line; that shader's copy is vestigial and can go whenever someone is in there.
**Deletions.** The audit's three dead shaders are gone, with both `.shader` and `.shader.meta` removed by `git rm` after a whole-repo grep for each GUID and each shader name (no `Shader.Find`, no builder path, not in Always Included Shaders): `grid_shader`, `intro_earth_placement_shader`, `poi_occlusion_shader`, plus their three materials. `boundry_space_floor_prefab` went with them — it was `grid_material`'s only referrer and nothing at all referenced the prefab, so the four files were a closed dead cluster and deleting the shader without the prefab would have left a dangling material reference behind. That is one file more than the ticket named; say so if it should come back.
**The two XRI sample shaders: refused, because there is nothing to un-reference.** `DepthOnly.shader` and `BiRP_Fresnel.shader` are reached only by materials **inside** `Assets/Samples/XR Interaction Toolkit/3.6.0/`, and no prefab, scene or asset of ours references any of those four materials; the sample scenes are not in `EditorBuildSettings`, so none of it enters a player build. The audit's "reachable" for these two means "a material exists", not "a build scene reaches it". The real ticket is whether the `Assets/Samples` tree should be in the repo at all — an asset-database delete, so [TERM].
**Compile confirmed 12 Sep 2026, terminal session, across the whole project, not just these eight.** `ShaderUtil.GetShaderMessages` was run against every shader under `Assets/` (excluding `Assets/Samples/`): **70 scanned, 0 with errors.** Only three carry warnings, all pre-existing and third party — `TMP_SDF-Mobile` and `TMP_SDF-Mobile SpaceWarp` (a deprecated `enable_d3d11_debug_symbols` pragma) and MRTK's `MixedRealityStandard` (`unknown attribute earlydepthstencil`). Item (1) below is satisfied for the compile half; the per-eye half is unchanged and still open.
**What only a device can settle, and it must be the standalone Android build or Link explicitly forced to Single Pass Instanced — a default Link session is multi-pass and would hand back a false all-clear:** (1) all eight files compile with zero errors and no new warnings; (2) the galactic centre's lensing disc, per eye, with the other closed — the disc must be lensed *differently* in each eye, which is the one case where a wrong eye index still looks plausible, so compare against a screenshot rather than a memory; (3) the galaxy view's three star layers and Andromeda's, which `AndromedaBuilder` builds from the same three shaders by path, per eye; (4) all ten orbital trails, per eye, and specifically that the hoisted `v2f o` did not change the mitre — the trail geometry is built in screen space from `UNITY_MATRIX_VP`, so an error shows as a wrong-width or kinked line, not a missing one; (5) the `LogoFader` quad in `core_systems_scene` — the intro logo fade, and `occluder_material`'s only consumer — blacking out **both** eyes, which is what the `occluder_shader` semantic change buys; (6) **the one thing no amount of reading can decide, and Android cannot be the test for it** — whether a custom `CommandBuffer.DrawProcedural` (five sites: `DrawStars`, `OrbitalTrail`, `CosmicWebRenderer`) has its instance count doubled by Unity under **true single-pass instanced**. Android implements its Single Pass Instanced setting as multiview, where the eye index arrives through `gl_ViewID` and no instance doubling is involved at all, so a clean result on device says nothing here — this has to be checked on a Link session explicitly forced to single-pass instanced. If Unity does not double the count there, every one of these five draws renders one eye only under that mode, and the fix is `instanceCount: 2` in C#, not more macros in the shader — `cosmic_web_points_shader` has the same exposure today. Check that before trusting any of the five.
*CS-118 (new, 12 Sep 2026, terminal session; qualified further after the verifier pass).* Raised because today's render-mode finding puts Android — the shipping platform — on Single Pass Instanced, which makes item (6) above matter more than it did when Link (Multi Pass) was thought to be the risk surface. Five `CommandBuffer.DrawProcedural` call sites use the five-argument overload, where `instanceCount` defaults to 1: `DrawStars.cs:192`, `OrbitalTrail.cs:238`, `CosmicWebRenderer.cs:391`, and by the same pattern `cosmic_web_points_shader`'s two remaining callers. **A passing result on the Android build does not close this ticket**: Android's Single Pass Instanced is multiview under the hood, so no instance doubling question even arises there; the question only exists on true single-pass instanced, which on this project means a Link session with the mode forced rather than left at its Multi Pass default. See the table row in this phase and the dated session note near the end of this file.

**CS-117 — About slate licence page (CS-116's in-app half). Prefab surgery only; no code should be needed.**
Context: the MIT notice now ships inside the player (`Assets/Resources/legal/galaxy_explorer_license.txt`) but nothing on screen shows it. Do this with a `PrefabUtility.LoadPrefabContents` script on `Assets/prefabs/about_slate_prefabs/about_slate_prefab.prefab`, per the working rule. Best done **after CS-086**, which deletes four of the six links and frees the rows at local Y 13, 4 and -2.
Hierarchy as it stands: `about_slate_prefab` → `slate_model_offset` (rotated 180° about Y) + `links_offset`. `links_offset` is `AboutSlate.SlateContentParent`, sits at local `(0, -0.015, -0.001)` with scale `0.002`, and holds eight children: `slate_title` (Y 20.5), `slate_intro_text` (Y 17.5) and the six `link_prefab` instances at Y 13, 10, 7, 4, 1, -2. Each text is a **3D `TextMeshPro`** (RectTransform + MeshRenderer), not `TextMeshProUGUI`, on layer 8 with the shared Selawik material — duplicate an existing one rather than creating a text object from the menu, or the material and layer will be wrong and `AboutSlate`'s fade will not find it.
Steps:
1. Duplicate `slate_intro_text` (the positioning parent **and** its `intro_text` child) → rename to `slate_version_text` / `version_text`, put it on a free row, clear `m_text`, and add `LegalNoticeText` with `notice = Version`. That closes **CS-114** as well; do not type a version number in.
2. Add an empty `legal_page` under `links_offset`, `SetActive(false)`, holding two more duplicated text objects: `legal_text` with `LegalNoticeText` `notice = GalaxyExplorerLicense`, and `our_notice_text` with `notice = ProjectNotice`. Keep them as two elements — Microsoft's notice and ours must not be merged or reworded.
3. Fill `AboutSlate.aboutPage` with `slate_title`, `slate_intro_text`, the surviving links and `slate_version_text`; fill `licencePage` with `legal_page`. Both arrays empty means the toggle is inert, which is the current state.
4. Add a seventh row that calls `AboutSlate.ToggleLicencePage()` — a `link_prefab` duplicate with its `Hyperlink` replaced by whatever the surviving links use for the click, or a `GEButton`. Label it "Licences". It must also close the page again, which `ToggleLicencePage` handles.
Sizing, which could **not** be worked out from the YAML: `intro_text` is `fontSize 6` in a 32×1 rect with auto-sizing off and word wrap on, holding 268 characters. The MIT text is **1082 characters** — four times that — so it will not fit at the same size. Expect to drop the licence page's font, widen the rect, or split it across two pages, and judge it in the headset rather than from numbers.
Checks: open the slate in play mode, toggle to the licence page and back, and confirm (a) the MIT text appears **in full and unaltered**, (b) our notice is a separate block, (c) both fade with the rest of the slate rather than popping in at full opacity — `AboutSlate` now caches renderers with `GetComponentsInChildren<Renderer>(true)` precisely so an inactive page is included, and this is the check that proves it, and (d) closing and reopening the slate lands on the About page, not the licence page.

**CS-095 — Compile and verify the 25 stereo-macro shader fixes per eye on the standalone Android build.**
Context: `docs/SHADER_STEREO_AUDIT.md`. 36 of the 47 project shaders carried **no** stereo macros at all; 25 reachable ones were fixed in the [CC] track on 12 Sep 2026 **without a compiler**. Nothing below has been compiled.
Acceptance:
1. **Compile.** Refresh, then confirm zero shader errors *and* zero new warnings for the 25 files listed in audit §4b. A partially-initialised `v2f` or a misplaced macro shows up here first.
2. **Settle the render mode question — done 12 Sep 2026, terminal session, and it reverses this item's own original reading.** `OpenXRSettings.RenderMode` declares `MultiPass = 0, SinglePassInstanced = 1` (`Library/PackageCache/com.unity.xr.openxr@b5b77b4027be/Runtime/Settings/OpenXRRenderSettings.cs:16-27`), and `Assets/XR/Settings/OpenXR Package Settings.asset` holds `1` for **Android** and `0` for **Standalone**. So Android is Single Pass Instanced (a missing macro breaks one eye on device) and Standalone/Link is Multi Pass (the same bug is invisible there) — the opposite of what this acceptance item and the audit used to say, and exactly what `CLAUDE.md` already said before a stale note claimed otherwise. `CLAUDE.md` needed no correction; the note did. Whether Android should ever move away from Single Pass Instanced (the old CS-082 question) is moot — it already is.
3. **Verify in both eyes on the standalone Android build, or on Link explicitly forced to Single Pass Instanced** — that is where the breakage actually lives; a default Link session is Multi Pass and would hand back a false all-clear. For each of: solar system view (planets, LOD meshes, rings, clouds, halos, Sun + flares + glow card, asteroid belt), galaxy view (centre plane, magic window), galactic centre (Sgr A* glow card), POI cards and marker pins in all three views, the boundary star dome, the intro placement scene (Earth, clouds, depth occluder), the hand menu cards and the About slate — close one eye, then the other, and confirm the object sits at a **different** screen position in each. Identical position in both eyes is the bug still present. Screenshots of before/after are worth keeping for the two worst cases (`planet_shader`, `poi_transparent_shader`).
4. **Confirm nothing regressed on Windows/Link**, which is Multi Pass, where a missing macro is invisible rather than broken — a sanity pass, not the verification; item 3 above is where this bug actually shows.
5. **Answer the `#pragma multi_compile_instancing` question** (audit §4e) and apply one answer to all five affected files, `sun_shader.shader` included — either the pragma is load-bearing and `ring_shader`, `placement_ring_shader` and the two `intro_placement_object_*` shaders get it (each also needing `UNITY_TRANSFER_INSTANCE_ID(v, o)`), or it is not and the Sun's comment is corrected.
Files: the 25 listed in audit §4b. `CLAUDE.md` and `docs/TECHNICAL_OVERVIEW.md` §7.1 needed no correction from step 2 after all — see item 2 above — but were corrected centrally regardless once a stale note had put the wrong reading in both.

**CS-096 — The 8 reachable shaders the audit could not fix mechanically.**
Context: audit §4a. Each was left alone on purpose; none is a drive-by.
- `black_hole_gravitational_lensing_disc_optimized_shader` — reads `_WorldSpaceCameraPos` **per pixel**, so it needs `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` in `frag` as well as the vertex macros. Copy the shape from `nebula_card_shader.shader`. Verify the disc by eye: a wrong eye index here gives a plausible but wrong image. Do this one first — it is the galactic centre's centrepiece.
- `spiral_stars_shader`, `spiral_stars_negative_shader`, `spiral_stars_cloud_shader`, `orbital_trail_shader` — `StructuredBuffer` + `DrawProcedural`, no `appdata` to hang an instance id on. `cosmic_web_points_shader` already solves this on the same code path (`cginc/StarQuad.cginc`); do all four to match it, in one pass, after the cosmic-web work lands.
- `screen_compose_shader`, `screen_clear_shader` — decide first whether the galaxy render-to-texture compose chain runs in stereo at all (Technical Overview §7.1 says the RT path is skipped on XR). If it is dead on XR, say so in §7.1 and close; do not patch four passes on spec.
- `occluder_shader` — its `v2f` position is `POSITION`, not `SV_POSITION`. Change the semantic and add the macros together, with a compiler open.
Also in scope, cheaply: delete the three dead shaders in audit §4c (`grid_shader`, `intro_earth_placement_shader`, `poi_occlusion_shader`) and their orphan materials, and stop referencing the two XRI **sample** shaders that are reachable and unfixed (`DepthOnly`, `BiRP_Fresnel`) rather than patching vendored files.

## Three status corrections (12 Sep 2026, end of the cloud session)

*CS-055 done, and nothing was built for it — it was already satisfied by CS-026 and CS-024, and the
correct outcome is a verified closure rather than an implementation.* Checked end to end rather than
assumed: `Assets/prefabs/ui/label_button_prefab.prefab` carries both a `BoxCollider` and a
`GEInteractable` on its `grow` child (written by `UiPrefabBuilder.BuildLabelButton`), `LabelButton`
declares `IGEPointerHandler, IGEFocusHandler`, and `DesktopMouseInput` raises `RaiseFocusEnter` /
`RaiseFocusExit` on hover and `RaisePointerDown` on click against exactly that interactable. So the
mouse reaches a tag through the same routing the hand ray uses, with no desktop-specific code.
**The one thing that could have broken it was introduced and fixed the same day:** CS-061 installs an
input module, and every world-space UI prefab here carries a `GraphicRaycaster`, so a blanket
`IsPointerOverGameObject()` test would have reported "over UI" while the cursor was over a tag,
skipped the physics raycast, and killed hover and click silently. `UiEventSystemInstaller`
deliberately asks a narrower question — it counts a hit only on a canvas whose `renderMode` is not
`WorldSpace` — which is what keeps this ticket true. Still wants the [TERM] play-mode confirmation in
the queue above; "verified by inspection" is not "seen working".

*CS-065 and CS-084 were left at `doing` by agents that finished mid-wave.* Both landed: the Cosmic Web
generator, renderer, shader and builder are committed (`0c65b57`), and the store pack is four drafts
under `docs/store/` (`6e4a6fd`). CS-084 reads **done (drafts only)** on purpose — the privacy policy,
listing, About copy and checklist are written and sourced from what the repo actually does, but
hosting the policy, supplying real company and contact details, and the Meta age-rating
questionnaire are all owner actions, and the About-slate link surgery is CS-086 [TERM].

## Session note (12 Sep 2026, first terminal run against the live editor)

This was the first session in which `tools/mcp/umcp.js` actually talked to a live Unity
editor rather than a stand-in. It worked the `[TERM]` priority queue from the top and
got through steps 1, 2 and 4 through 10, plus step 21 — the queue's own update note
above says exactly which. Nothing from step 11 onward ran, except that items 22 through
24 were corrected rather than completed (below, and in the queue). **No play-mode
session and no device run happened today.** Everything below was checked structurally
in the editor or in the serialised assets, never seen actually working at runtime — that
distinction matters more here than usual, because several of today's findings read like
"it works" and are not that.

**Compile, twice, zero errors.** The whole 12 Sep wave — about 13,000 lines across
eighteen commits, none of it previously near a compiler — built with zero `error CS`
lines the first time it was run, and again after CS-108's and CS-096's edits landed on
top. This was the wave's single biggest unknown going in.

**The queue's own flagged highest-risk item turned out to be dead.**
`UiEventSystemInstaller.EnsureDefaultActions` (CS-061) compiles as written: Input System
**1.20.0** declares both `public InputActionAsset actionsAsset`
(`InputSystemUIInputModule.cs:2556`) and `public void AssignDefaultActions()` (line
1541). The documented fallback — delete the method and its one call site — is not
needed against this package version.

**Build Nebula Prefabs, re-run, verified by GUID rather than by eye.** All seven
`nebula_*_prefab`s went from zero to one reference each to `ManipulationPointerRouter`
(guid `c9080a9df18d4c4caaaafc7a7d8cd400`) and `FreePlacementAnchor` (guid
`64e1ba70b56d49b3917223e4351fffdd`). They really were stale and ungrabbable before —
the code had landed in the repo but never in these committed prefabs.

**Build Cosmic Web Content, first run ever.** `cosmic_web_content_prefab` did not exist
until today. The build also needed `cosmic_web_points_shader` added to
`m_AlwaysIncludedShaders` in `ProjectSettings/GraphicsSettings.asset` — a
`DrawProcedural` shader has no material reference for the build pipeline to find, so
without that entry it would be silently stripped from a player build.

**Build Andromeda Content, first run ever.** `andromeda_content_prefab` built, along
with its three materials and three `StarsData` scriptable objects.

**MoonBuilder passed, including the one documented risk.** Nine `MoonOrbit`
components; six moons active (Earth/Moon, Jupiter/Ganymede+Callisto,
Saturn/Titan+Mimas+Iapetus), three built but switched off (Io, Europa, Enceladus), none
skipped. The feared "moons with no panels" did not happen — all nine `moon_panel_*`
instances exist as nested `info_panel_prefab` instances. **Gotcha worth keeping:** a
nested prefab instance stores its own name in `m_Modifications`, not in `m_Name`, so
grepping `m_Name: moon_panel_` returns nothing on a perfectly good prefab and looks
like a failure. Check `m_Modifications`, or ask the editor, before concluding a nested
instance is missing — this trap will catch the next person too.

**Build Hint Cards ran.** `hint_cards.asset` now exists at
`Assets/scriptable_objects/Resources/hint_cards.asset`, so `HintCards.ShowIfFirstRun()`
no longer logs an error and shows nothing.

**Wire Scene Panel ran, on the second attempt.** It first failed with "no
`ExperienceDirector` in any open scene" because `core_systems_scene` was not open. It
had to be opened **additively**, never Single — unloading a dirty scene raises a modal
dialog that blocks the relay outright. Once open, `core_systems_scene.unity:1349` now
reads `scenePanelPrefab: {fileID: 284596933589354139, guid:
782d1fee663b8e841b4659df5742eb48}`, which is `info_panel_prefab`.

**Install Runtime Systems, re-run, confirmed rather than assumed.** `MusicController`
sits on `cosmic_systems` (`m_Enabled: 1`, owner `m_IsActive: 1`, so it will actually
run) with three unique clips across the four room states, a 2 s crossfade and a 0.55
narration duck. The legacy `MusicAudioSources` root reads `m_IsActive: 0` — genuinely
deactivated with `SetActive(false)`, not merely muted by a mixer snapshot, which is
exactly what CS-073's note asked someone to confirm. `desktop_dock_prefab` is
referenced 23 times in `core_systems_scene`.

**CS-108 verified in the rebuilt prefab.** `dock_prefab`'s `drag_bar` now carries
`ManipulationPointerRouter`, `manipulationType: 0`, `playGrabSounds: 1`, and a
`hostTransform` resolving to the dock root (`m_Father: 0`) while the handler itself
stays on `drag_bar` — dragging the bar moves the dock, not itself. `DockController`'s
own references (buttons, utility window prefab) survived the rebuild.

**Shaders compile clean, project-wide.** `ShaderUtil.GetShaderMessages` was run across
every shader under `Assets/` (excluding `Assets/Samples/`): **70 scanned, 0 with
errors.** Three carry warnings, all pre-existing and third party — `TMP_SDF-Mobile`
and `TMP_SDF-Mobile SpaceWarp` (a deprecated `enable_d3d11_debug_symbols` pragma) and
MRTK's `MixedRealityStandard` (`unknown attribute earlydepthstencil`). "Zero errors,
zero new warnings" is satisfied for the compile half of CS-095 and CS-096; the per-eye
device half of both is unchanged and still open.

**The render-mode reading was inverted, and this reverses a decision, not just a
ticket note.** Verified from the package source and the settings asset, not inferred:
`OpenXRSettings.RenderMode` is declared `MultiPass = 0, SinglePassInstanced = 1` in
`Library/PackageCache/com.unity.xr.openxr@b5b77b4027be/Runtime/Settings/OpenXRRenderSettings.cs:16-27`
(field default `SinglePassInstanced`), and `Assets/XR/Settings/OpenXR Package
Settings.asset` holds `m_renderMode: 1` under `Android` and `m_renderMode: 0` under
`Standalone` (and `1` under `WebGL`, which does not ship). So **Android — the shipping
Quest 3 build — is Single Pass Instanced**, where a missing stereo macro breaks one eye
on the real device, and **Windows/Link is Multi Pass**, where the same bug is
invisible. `CLAUDE.md`'s original "Android multiview; Windows/Link multi-pass" line
was right all along; the 12 Sep note that called it backwards, and the CS-095 queue
items built on that note (22 through 24 above), were themselves wrong. See **D-008** in
`docs/decisions.md`. One consequence worth restating: the queue's "close-one-eye check
on Link" (old item 23) proves nothing about the shipping build — it has to be the
standalone Android build, or Link explicitly forced to Single Pass Instanced.

**Three things about the harness itself, worth keeping for the next session — none of
them are app bugs.**
`tools/mcp/compile.ps1` had never actually been run before today, and did not work:
`$PSScriptRoot` is empty inside a parameter default — both directly and under
`powershell -File` with a relative path — so it died on the first `Join-Path` before
doing anything. Fixed by resolving the script's own root once in the body and deriving
`$ProjectPath` and the path to `umcp.js` from that, rather than from the parameter
default. It now works, and correctly refuses to run while the editor is in play mode.
Separately: this project was found already sitting in play mode when the session
opened, so the very first compile attempt was refused (exit 3) until
`leave_play_mode.cs` ran — check `playing=` before assuming the editor is idle for you.
`tools/mcp/smoke.cs` had never run either, and did not compile: two helper classes,
`Check` and `Entry`, were declared `private sealed class` at what the runner's namespace
wrapper turns into file scope, where `private` is illegal (`error CS1527`, two sites).
Fixed by making both `internal`. See the CS-098 note above for the rule this leaves
behind for anyone else writing into `tools/mcp/*.cs`.

**This is the honest reason queue steps 11 through 20 did not run today — not a choice
to skip them.** Once `smoke.cs` compiled, play mode itself would not stick through the
relay. `enter_play_mode.cs` returns "PLAY: requested" and the domain reloads as
expected, but the next command still reports `playing=False`, and a `smoke.cs` run is
then refused outright with `UNEXPECTED_ERROR: User interactions are not supported for
MCP tool calls`. A trivial command in the same state succeeds and confirms
`playing=False`, so the refusal is about the play-mode transition itself, not about
`smoke.cs`, which by then compiled cleanly. Tried twice, same result both times. The
likely cause is the fragility `leave_play_mode.cs`'s own comments already flag: setting
`EditorApplication.isPlaying` from inside a command whose assembly the resulting domain
reload then unloads mid-flight means the request can simply be dropped. The editor can
hold play mode fine — it came up already in it when this session opened, which is why
the very first `compile.ps1` run correctly refused with exit 3 — the failure is
specifically in *entering* play mode from a relay command. The practical workaround,
matching the README's own step 1: a human presses Play in the editor, and only then is
`smoke.cs` polled from the terminal; it cannot be driven end to end as written today.
See **CS-119** in Phase 2 above.

**The relay reports NOT-OK on any warning, and this project has two permanent sources
of one.** The TouchScript `PluginImporter` meta-version warnings
(`Assets/external/TouchScript/Plugins/WindowsTouch/*.dll.meta`, "below the supported
minimum (2)") and TMP `CanvasRenderer` warnings fire on nearly every command. A raw
NOT-OK from the relay does not mean the command it ran actually failed — read the log
line behind it before concluding that. (The TouchScript warning is more context for
CS-112.)

**One CS-096 question still cannot be settled by reading, and matters more now than it
did — but Android cannot be the platform that settles it.** Whether a custom
`CommandBuffer.DrawProcedural` gets its instance count doubled under **true**
single-pass instanced. Five call sites use the five-argument overload, where
`instanceCount` defaults to 1: `DrawStars.cs:192`, `OrbitalTrail.cs:238`,
`CosmicWebRenderer.cs:391`, and by the same pattern `cosmic_web_points_shader`'s two
remaining callers. Today's finding puts Android — the shipping platform — on Single
Pass Instanced, but Android implements that mode as multiview, where the eye index
arrives via `gl_ViewID` and no instance doubling is involved at all — so a clean
result on the Android build must not be read as clearing this question. It has to be
checked on a Link session explicitly forced to true single-pass instanced. If Unity
does not double the count there, every one of these draws renders to one eye only
under that mode, and the fix is `instanceCount: 2` in C#, not another shader macro.
Given its own ticket, **CS-118**, [TERM], depends CS-095.

**What is still open, stated plainly so it is not mistaken for finished work.**
Everything from queue step 11 onward: all ten play-mode checks (11 through 20), and
the device pass (27, 28) — not skipped by choice, but blocked by the play-mode harness
gap above (CS-119). Step 3 (confirm exactly one live `EventSystem` after boot) also did
not run, for the same reason. Nothing in this session put a headset on, ran an APK, or
watched anything move on screen.


---

## Session note (12 Sep 2026, terminal session - second half)

Everything below was checked against the live editor or the actual files, not inferred.

**CS-112 done - and it was not inert after all.** The reason it had to be removed rather than
justified: both `OSCsharp.dll` and `TUIOsharp.dll` were marked `Android: enabled: 1` in their
`.meta` plugin settings, and `TuioInput.cs` lived under `Assets/`, so it compiled into the app's
own assembly and referenced them. The network capability therefore **shipped in the APK**; only its
invocation was missing. `docs/store/PRIVACY_POLICY.md` had called it inert, which was true of its
behaviour and misleading about what was in the build. Verified a closed cluster first (nothing in or
out of TouchScript referenced the module, no `.asmdef`, the only external GUID hit was the folder's
own `.meta`), then removed 14 files. Compiles clean. Policy, readiness checklist and D-009 updated;
the checklist had also filed this under the wrong ticket number (CS-090), now corrected.

**CS-039 - the shader half is closed.** `planet_atmosphere_rim_shader.shader` was the one CS-047
shipped without ever compiling. It compiles: `ShaderUtil.ShaderHasError` false, zero messages, one
pass, renderQueue 3000, shader name `CosmicSimulation/PlanetAtmosphereRim`.
`earth_atmosphere_material.mat` exists and points at it, and is referenced by nothing - which is the
wiring half. The design question is settled: the shell belongs on
`Assets/prefabs/poi_prefabs/poi_earth_prefab.prefab`, because `SolarRowBuilder` instantiates
`poi_<id>_prefab` from that folder (`SolarRowBuilder.cs:252`, `:276`) and `solar_system_prefab.prefab`
references it too, so the source prefab reaches both the orbit model and the solar row and survives a
**Build Solar Row Content** re-run. Wiring it into a generated prefab would be wiped by the next rebuild.

**CS-045 confirmed.** `Assets/prefabs/solar_system_prefab.prefab`'s root carries `GEInteractable` = 0,
`ManipulationHandler` = 0, `ScaleLimits` = 0. The orbit model genuinely cannot be grabbed today.

**CS-088 confirmed.** `Assets/prefabs/menu_managers.prefab` still holds 11 `UiWorldPreview` components
and 2 `PlanetPreviewController` components.

**CS-111 confirmed, but the ticket points at the wrong file, and this will cost someone an hour.**
The Back button is `hand_back_button` and it lives in **`Assets/prefabs/hand_menu_offset.prefab`**. It
is **not** in `hand_menu_left_prefab.prefab` or `hand_menu_right_prefab.prefab`, which contain no
back-named object at all - so an operator who reads "the hand menu still offers the old Back button"
will look in the two hand-menu prefabs, find nothing, and mark the ticket stale. It is not stale.
Two siblings of the same retired drill-down navigation sit in `menu_managers.prefab`:
`ggv_back_button` and `desktop_back_button` - the same file CS-088 operates on, so the two tickets
should be done in one pass. And it is not just a GameObject: `HandMenu.cs:12` `_backButton`,
`HandMenu.UpdateButtonsActive(resetIsActive, backIsActive)` (:88-94),
`HandMenuManager.SetMenuAvailability` (:29-35) and `GlobalMenuManager.cs:136` passing
`BackButtonNeedsShowing` all still carry it, so retiring the button means editing that call chain.

**CS-119 - three ways into play mode, all refused.** Recorded so nobody repeats them:
(1) `EditorApplication.isPlaying = true` (`tools/mcp/enter_play_mode.cs`) returns "PLAY: requested",
the domain reloads, and the next command reports `playing=False`.
(2) `ExecuteMenuItem("Edit/Play")` fails with "there is no menu named 'Edit/Play'" on 6000.6.
(3) `EditorApplication.delayCall += () => EditorApplication.EnterPlaymode()` succeeds as a command
and still leaves `playing=False`.
Running `smoke.cs` in that state is refused with `UNEXPECTED_ERROR: User interactions are not
supported for MCP tool calls`, while a trivial command in the same state succeeds - so it is the
play-mode transition, not `smoke.cs`. Note Unity came up **already playing** when the project was
first opened, so the editor holds play mode fine; it is entering it from a relay command that does
not take. Until CS-119 is solved, a human presses Play and only then is `smoke.cs` polled.

**CS-039 done, 12 Sep 2026.** Both halves. The shader CS-047 shipped without ever compiling does
compile (`ShaderUtil.ShaderHasError` false, 0 messages). The wiring is a new reproducible builder,
`Assets/scripts/Editor/AtmosphereShellBuilder.cs` (**Cosmic Simulation > Build Atmosphere Shells**),
which edits `poi_earth_prefab` through `LoadPrefabContents` - the right home, because `SolarRowBuilder`
instantiates `poi_<id>_prefab` and `solar_system_prefab` references it, so one edit reaches both the
orbit model and the Solar Row and survives a rebuild.

Verified from the builder's own report, second run (so idempotency is proven, `earth updated` not
`created`): ratio 1.0250, concentric to 0.000000, parent `.../earth_tilt/axis_rotator`, layer 0,
shell carries **0 colliders and 0 interactables** while the sphere keeps its 1 and 1, and the rim
clears `earth_clouds_mesh` by 0.0003 prefab units (about 1.1 mm at a 15 cm body). Propagated into
`solar_system_planets_content_prefab` by re-running Build Solar Row Content then Build Moons; 9
MoonOrbit components and 9 moon panels survived.

*Two things worth carrying forward.* **The `sharedMaterial` leak was real, not theoretical.**
`SunLightReceiver.LateUpdate` wrote `_SunDirection` straight onto the shared material every frame; in
the orbit model a `Fader` happens to instance the material first, but `SolarRowBuilder.Strip` destroys
every `Fader` and keeps `SunLightReceiver`, so in Solar Row the write landed on the `.mat` on disk. Now
routed through a `MaterialPropertyBlock` behind an **opt-in** `UsePropertyBlock` flag, default false, so
all 94 receivers already authored into prefabs and scenes behave identically - verified: 40 x `false` in
the generated prefab, and in `poi_earth_prefab` 5 x `false` plus the 1 x `true` on the shell. Flipping
the default for every receiver is a real behaviour change and wants its own ticket.

*And a false alarm worth recording so nobody repeats it.* The builder first reported `SHELL IS BURIED`
against `earth_glow_mesh` at 0.0410 (1.118x the sphere), which looked like the 1.025 rim being hidden.
It is not: `earth_glow_mesh` carries `FaceCamera`, so **0.0410 is a billboard card's half-width, not a
shell radius** - comparing the two is a category error, and `SolarRowBuilder.NotGeometry`'s own comment
already says a glow card is "a screen-facing billboard several times the width of the planet behind
it". It could not occlude anyway: `halo_shader` is `Blend SrcAlpha One` with `ZWrite Off` at queue 2070
and the rim is the same blend at 3000, so two additive layers that write no depth commute. The
diagnostic now tests surfaces only and reports light cards as information. Note also that
`earth_glow_material.mat` still serialises `_SRCBLEND`/`_DSTBLEND`/`_ZWRITE` properties orphaned from a
previous shader - `halo_shader` declares none of them, so **reading blend state off that .mat tells you
the opposite of the truth.**

*Cost, for whoever adds the second recipe:* the shell reuses the body's own sphere mesh, so each one is
6,468 verts / 12,288 tris and one draw call. At ten bodies that is ~123k triangles. Technical Overview
7.4 sets no triangle ceiling - it budgets <= 150 draw calls - so draw calls and fill are the numbers to
argue from, and the shells should share one low-poly sphere rather than ten full-density duplicates,
since the rim is a per-pixel Fresnel gradient and only its silhouette shows tessellation.

---

## Research note (12 Sep 2026): scale, navigation and the rendering budget

Full findings in `docs/research/scale_and_rendering.md`. The parts that change decisions:

**CS-118 is answered, and the ticket was asking for the wrong fix.** There are **three** real
`DrawProcedural` sites, not five - `DrawStars.cs:192`, `CosmicWebRenderer.cs:391`,
`OrbitalTrail.cs:238`; the other two "sites" in the old note were prose in comments, and that wrong
count was propagated from CS-096 into this file. On **Android the shipping build is multiview**, where
the Khronos `OVR_multiview` spec has the driver instantiate the draw per view, so `instanceCount` 1 is
**correct** and a hardcoded 2 would double the geometry on the platform that ships. Under **true
single-pass instanced** (a forced Link session) the count must be doubled, because
`UnityInstancing.cginc` derives the eye from the instance id and at count 1 the right eye gets nothing.
So the fix is `CommandBuffer.SetInstanceMultiplier`, which Unity documents as affecting
`DrawProcedural`, gated on `XRSettings.stereoRenderingMode` - neither that property nor
`SetInstanceMultiplier` appears anywhere in `Assets/` today. **The one thing still to measure:** URP is
documented to call the multiplier automatically; BiRP is not, and whether it applies to an app-injected
command buffer at a `CameraEvent` has to be tested on device.

**The budget is maximal, not conservative, and has never been measured.** 400,000 point sprites is
1.6 M triangles across both eyes, which is the whole Quest 3 triangle budget (Meta publishes 1.3-1.8 M).
The Cosmic Web alone claims about 30% of it. Meanwhile `cosmic_web_content_prefab` has never been
profiled (CS-066), `andromeda_content_prefab` has never been looked at on a device (CS-103), and the
Galaxies field - the only existing precedent for drawing many galaxies - does not exist (CS-063/064).
**Sequencing rule for the expansion: land CS-066, CS-103 and CS-064 and take one real OVR Metrics
frame-time and per-eye fill reading off a Quest before any hierarchy or catalogue code is written.**

**Navigation: nested destinations, and the camera never moves.** The mechanism already ships -
`ExperienceDirector.OpenDestination` fades the parent, sizes a halo, captures home, spawns the panel,
plays narration and grows in. Generalising it from "a nebula inside the Milky Way" to "any place inside
any place" needs one parent reference on `ExperienceModule` and a breadcrumb row on the dock, which
keeps GDD 3.1's "the dock is the only navigation" literally true without reviving the Back button
CS-087 retired. There is a commercial reason as well as a comfort one: Meta rates comfort on an app's
default experience, "Comfortable" means apps that generally avoid camera movement, and this app is
Comfortable today - **any camera move between galaxies costs that rating.** The honest cost of the
diorama model is that you never feel a galaxy is bigger than a solar system, since both are about 1.3 m
wide on your table; the mitigations are a comparison layout, growing the child out of the exact point on
the parent it came from, and a known reference object in the copy.

**Galaxy LOD has a clean crossover.** A galaxy on the existing path costs 3 draw calls, 3 instantiated
materials, 3 ComputeBuffers, 17,200 points, ~757 KB. At 19 px/deg per eye a 15 cm galaxy at 1.5 m is
~107 px across, so those 17,200 points are two per pixel. Point cloud above ~0.6 m apparent, sprite
below ~0.2 m, truncated cloud between - but see CS-125, because truncation is unusable until the bake
order is shuffled.

*Good news found while reading:* the additive star path already handles passthrough alpha on purpose -
both point shaders are `Blend One One` but compute `a = dot(rgb, 1)`, so eye-buffer alpha accumulates
with brightness. That matters because the Milky Way runs in Dimmed room with passthrough visible.