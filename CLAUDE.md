# Cosmic Simulation XR — project guide for Claude Code

Unity 6000.6 (Built-in RP) mixed-reality app for Meta Quest 3 (hands, passthrough) with a desktop mouse mode. Forked from Microsoft's Galaxy Explorer (MIT). Branch `quest3-port`, mirrored on `main` at github.com/JDMAXilius/GalaxyExplorer.

## Read these first
1. `docs/COSMIC_SIMULATION_XR_ROADMAP.md` — the phased plan (what to do next).
2. `docs/GDD.md` — the design contract (every scene, object, interaction, UI, audio). One-to-one with the reference app.
3. `docs/TECHNICAL_OVERVIEW.md` — architecture, systems, pipelines, tooling, asset inventory.
4. `docs/BACKLOG.md` — the ticket queue. **Start here to pick work.** Two tracks: **[CC]** tickets can be done in the repo alone; **[TERM]** tickets need the live Unity editor (MCP relay), the Quest, or the Figma/Higgsfield MCPs and are done in the local terminal session. Follow the workflow at the top of that file (claim → do → verify → commit `<ID>: <title>` → mark done with notes).
Update the relevant doc in the same commit as any behaviour change.

## Who does which half (owner's direction, 14 Sep 2026)

The two tracks are two sessions, and they hand work to each other as tickets in `docs/BACKLOG.md`.

- **The terminal session owns the editor.** Everything through the Unity MCP tools: running the builders, prefab
  and asset work, play-mode runs, captures, APKs, the device. It writes **[CC]** tickets for anything that needs
  real code and does not write the framework itself.
- **The cloud session owns the code.** Framework, architecture and C# under `Assets/scripts/` and `Assets/Cosmic/`.
  It writes **[TERM]** tickets for anything that has to be built, run or seen in the editor.

So a finding from a play-mode run becomes a [CC] row, and a [CC] row that lands becomes a [TERM] row to verify.
Neither track marks the other's row done.

## The crew (`.claude/agents/`)
Work is dispatched to specialists rather than done one file at a time. For anything spanning more than one ticket or track, hand the goal to **`orchestrator`** and let it plan waves.

| Agent | Owns |
|---|---|
| `orchestrator` | Plans a wave from the backlog and dispatches the rest in parallel |
| `unity-code` | C# runtime and editor scripts — the [CC] track |
| `unity-editor` | The live editor via the MCP relay — the [TERM] track |
| `scene-builder` | The content of an experience: bodies, orbits, labels, overlays |
| `ui-figma` | Figma design and the export pipeline into `Assets/ui/` |
| `asset-smith` | Imagery, textures, audio, and the credits log |
| `verifier` | Adversarial review of a diff before it is committed |
| `scribe` | Backlog notes, docs, decisions, commit messages |

Two rules: **only one `unity-editor` at a time** — the relay is a single shared connection and two will deadlock — and **run `verifier` over the combined diff** before committing a wave. Everything else parallelises; dispatch a wave as several `Agent` calls in one message.

## Decisions locked (11 Sep 2026)
Bundle id `com.jdmaxilius.cosmicsimulationxr`; the three extra Milky Way destinations (Pillars, NGC 1501, Trumpler 14) stay on; reuse the 22 original narration clips, TTS placeholders for the 11 new ones, natural voice at Phase 6; keep the original intro and add two hint cards; Phase 4 before Phase 5.

## Working rules
- Reuse before sourcing before generating. Log every downloaded/generated asset in `Assets/_sources/CREDITS.md`.
- Our name, copy and art only: never use the reference app's name, logo, artwork or text.
- Keep desktop mode working; every hand interaction gets a mouse/keyboard equivalent.
- Commit per task group on `quest3-port` with a plain-language message. Push to `main` only when asked.
- Don't commit `Builds/`, `.utmp/`, user settings. Revert runtime-leaked material values before committing.
- Edit prefabs through reproducible `PrefabUtility.LoadPrefabContents` scripts when practical.

## Driving Unity (MCP relay)
- Relay: `%USERPROFILE%\.unity\relay\relay_win.exe --mcp --project-path <repo>`; tools `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`. The tooling is now checked in at **`tools/mcp/`** (outside `Assets/`, so Unity never compiles it into the build) — `umcp.js` (`list`, `call <tool> <json>`, `run <file.cs>`), `compile.ps1` (refresh + wait + errors from `Logs/Editor.log`), `smoke.cs`, `enter_play_mode.cs` / `leave_play_mode.cs`, and a README. Stop recreating it in a scratchpad; read `tools/mcp/README.md` first. **All of it has now been run against the real relay** (12 Sep 2026): `umcp.js run`, `compile.ps1`, console reads, scene-view captures and play mode all work.
- RunCommand code: `internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) { … } }`.
- Runner limits: no `System.Reflection`, no asset delete/move (use project menu items + `EditorApplication.ExecuteMenuItem`); NOT-OK is reported on any logged warning even when the command succeeded; "Unity not detected" = domain reload, retry; `Image` resolves to `Unity.AI.Image` — write `UnityEngine.UI.Image`.
- Never `EditorSceneManager.OpenScene` **in Single mode** when a scene may be dirty (modal dialog blocks the relay); `OpenSceneMode.Additive` never prompts and is the safe way to bring a scene in.
- **Entering Play: assign `EditorApplication.isPlaying = true`.** `delayCall += EnterPlaymode()` never fires through the relay — the command reports success and nothing happens, which is what CS-119 was. `isPlaying` reads false on the same frame; poll it. `tools/mcp/enter_play_mode.cs` does this correctly.
- `PlayFromViewScene` quick-starts (skipping the intro) when **exactly one *view* scene is open**, whatever else is open beside it. It used to require exactly one scene in total, which never happens in practice — so it silently never armed, the intro ran with a view scene it had not placed, stalled, and `ExperienceDirector.Switch` then refused every dock tile and every destination tag for the rest of the session with no error anywhere. If nothing in the app responds, check `ExperienceDirector.IntroRunning` first.
- Never edit scripts during Play mode; confirm `isPlaying=false` first.
- Synthetic input: `InputSystem.QueueStateEvent(Mouse.current, new MouseState{position=…}.WithButton(MouseButton.Left,true))`, `KeyboardState(Key.X)`; screenshots via `ScreenCapture.CaptureScreenshot`.
- `Object.GetInstanceID()` is obsolete in 6000.6 and the runner treats the warning as an error: use **`GetEntityId()`**. It returns a composite id (`156609:6912`), which `Unity_Camera_Capture`'s integer `cameraInstanceID` will not take — that tool only reliably captures the **scene view** (call it with `{}`). Note the scene view does not use the game camera's clear colour, so a passthrough build captures against the editor's light background, not black.
- The relay refuses some APIs outright with *"User interactions are not supported for MCP tool calls"* — `ScreenCapture.CaptureScreenshot` is one. Frame the scene view at the player's eye (`SceneView.pivot`/`rotation` from `Camera.main`) and capture that instead.

## Build
Menu **Cosmic Simulation → Quest 3**: *Configure Project*, *Build APK* (→ `Builds/Quest3/CosmicSimulationXR.apk`), *List OpenXR Features*, *Apply Android Asset Overrides*. `Quest3ProjectSetup.ConfigureProject()` runs first on every build and **writes player settings from its own constants** (bundle id, SDK levels, graphics API) — change identity there, not only in Project Settings, or the build silently reverts it. An unattended build stops on the modal *"Unsupported Input Handling on Android"* (we need Active Input Handling "Both"): answer **Ignore**. **Stereo mode, confirmed 12 Sep 2026 against the package source: Android (the shipping build) is Single Pass Instanced; Windows/Link is Multi Pass.** `OpenXRSettings.RenderMode` is declared `MultiPass = 0, SinglePassInstanced = 1` (`OpenXRRenderSettings.cs:16-27` in the package cache — the package *is* vendored there), and `Assets/XR/Settings/OpenXR Package Settings.asset` stores `m_renderMode: 1` for Android, `0` for Standalone. So the original "Android multiview; Windows/Link multi-pass" line was right, and the 12 Sep note claiming it was backwards was itself wrong — see `docs/SHADER_STEREO_AUDIT.md`. This matters because most shaders here had no stereo macros at all, which **breaks one eye on-device** and is **invisible on Link**: a default Link session cannot verify stereo work. All custom shaders need stereo macros, no geometry shaders, target ≤ 4.5.

## Key code
- Input routing: `Assets/scripts/XR/` (`GEInteractable`, `GEPointer`, `GEInputEvents.ExecuteHierarchy`, `ManipulationHandler`, `DesktopMouseInput`, `XRInputRig`).
- Force-pull: `Assets/scripts/solver_scripts/ForceSolver.cs` (Root → Dwell → Attraction → Free → Manipulation), `PlanetForceSolver`, `MoonForceSolver`.
- Cards: `PlanetInfoCard` on every `poi_<body>_prefab`; desktop HUD `DesktopMenuManager`; passthrough/VR `ExperienceModeManager`; audio `AudioService` + `VOManager`.
- Body prefab pattern and moon pattern: Technical Overview §6.
