# Cosmic Simulation XR — project guide for Claude Code

Unity 6000.6 (Built-in RP) mixed-reality app for Meta Quest 3 (hands, passthrough) with a desktop mouse mode. Forked from Microsoft's Galaxy Explorer (MIT). Branch `quest3-port`, mirrored on `main` at github.com/JDMAXilius/GalaxyExplorer.

## Read these first
1. `docs/COSMIC_SIMULATION_XR_ROADMAP.md` — the phased plan (what to do next).
2. `docs/GDD.md` — the design contract (every scene, object, interaction, UI, audio). One-to-one with the reference app.
3. `docs/TECHNICAL_OVERVIEW.md` — architecture, systems, pipelines, tooling, asset inventory.
Update the relevant doc in the same commit as any behaviour change.

## Working rules
- Reuse before sourcing before generating. Log every downloaded/generated asset in `Assets/_sources/CREDITS.md`.
- Our name, copy and art only: never use the reference app's name, logo, artwork or text.
- Keep desktop mode working; every hand interaction gets a mouse/keyboard equivalent.
- Commit per task group on `quest3-port` with a plain-language message. Push to `main` only when asked.
- Don't commit `Builds/`, `.utmp/`, user settings. Revert runtime-leaked material values before committing.
- Edit prefabs through reproducible `PrefabUtility.LoadPrefabContents` scripts when practical.

## Driving Unity (MCP relay)
- Relay: `%USERPROFILE%\.unity\relay\relay_win.exe --mcp --project-path <repo>`; tools `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`. A small Node stdio client (`umcp.js`: `list`, `call <tool> <json>`, `run <file.cs>`) and `compile.ps1` (refresh + wait + errors from `Logs/Editor.log`) lived in the session scratchpad; recreate if missing.
- RunCommand code: `internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) { … } }`.
- Runner limits: no `System.Reflection`, no asset delete/move (use project menu items + `EditorApplication.ExecuteMenuItem`); NOT-OK is reported on any logged warning even when the command succeeded; "Unity not detected" = domain reload, retry; `Image` resolves to `Unity.AI.Image` — write `UnityEngine.UI.Image`.
- Never `EditorSceneManager.OpenScene` when a scene may be dirty (modal dialog blocks the relay). Enter Play with the open scenes; `PlayFromViewScene` quick-starts when exactly one view scene is open.
- Never edit scripts during Play mode; confirm `isPlaying=false` first.
- Synthetic input: `InputSystem.QueueStateEvent(Mouse.current, new MouseState{position=…}.WithButton(MouseButton.Left,true))`, `KeyboardState(Key.X)`; screenshots via `ScreenCapture.CaptureScreenshot`.
- `Object.GetInstanceID()` doesn't compile in 6000.6.

## Build
Menu **Galaxy Explorer → Quest 3**: *Configure Project*, *Build APK* (→ `Builds/Quest3/GalaxyExplorer.apk`), *List OpenXR Features*, *Apply Android Asset Overrides*. Android multiview; Windows/Link multi-pass. All custom shaders need stereo macros, no geometry shaders, target ≤ 4.5.

## Key code
- Input routing: `Assets/scripts/XR/` (`GEInteractable`, `GEPointer`, `GEInputEvents.ExecuteHierarchy`, `ManipulationHandler`, `DesktopMouseInput`, `XRInputRig`).
- Force-pull: `Assets/scripts/solver_scripts/ForceSolver.cs` (Root → Dwell → Attraction → Free → Manipulation), `PlanetForceSolver`, `MoonForceSolver`.
- Cards: `PlanetInfoCard` on every `poi_<body>_prefab`; desktop HUD `DesktopMenuManager`; passthrough/VR `ExperienceModeManager`; audio `AudioService` + `VOManager`.
- Body prefab pattern and moon pattern: Technical Overview §6.
