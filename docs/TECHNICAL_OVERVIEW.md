# Cosmic Simulation XR — Technical Overview

*Version 1.0 — 11 September 2026. Companion to `docs/GDD.md` (what) and `docs/COSMIC_SIMULATION_XR_ROADMAP.md` (when). Written so a fresh Claude Code session can continue the work: read `CLAUDE.md` first, then this, then the GDD section for the feature at hand.*

---

## 1. Purpose and how to use this document

This is the map of the codebase as it is **today** (end of the Quest 3 port, branch `quest3-port`, mirrored on `main`) plus the systems the roadmap adds. Sections marked **[existing]** describe code in the repo; **[planned]** describes what the roadmap builds and where it goes. Every path is relative to the repo root `D:\Documents\Claude\GalaxyExplorer`.

---

## 2. Stack

| Layer | Choice | Notes |
|---|---|---|
| Engine | Unity **6000.6.0f1** | Built-in Render Pipeline (BiRP). Colour space Gamma. |
| XR runtime | OpenXR **1.18.0**, Unity OpenXR: Meta **2.6.1** (AR Foundation 6.6 for passthrough) | Standalone (Quest Link) and Android (APK) |
| Interaction | XR Interaction Toolkit **3.6.0**, XR Hands **1.9.0** | Near-Far interactors, poke, hand joints |
| Input | Input System **1.20.0**, Active Input Handling **Both** | XRI needs the new system; TouchScript and some legacy paths use `Input` |
| UI | uGUI **2.6.0** (TextMesh Pro built in), Selawik SDF fonts | World-space canvases in XR; screen-space HUD on desktop |
| XR management | XR Plug-in Management **4.7.0** | Loader per platform set by `Quest3ProjectSetup` |
| Editor tooling | Unity AI Assistant **2.19.0-pre.2** (its MCP relay), Figma MCP, Higgsfield MCP | See §13 |
| Stereo | Android: **Multiview (Single-Pass Instanced)**; Windows/Link: **Multi-pass** | All custom shaders carry stereo macros |
| Android player | IL2CPP, ARM64, min API 32, target 34, Vulkan, ASTC, GameActivity, Landscape Left, 4× MSAA | Package id `com.jdmaxilius.galaxyexplorer` (rename pending decision) |
| Platforms | `PlatformId.Quest3 = 5` when `XRSettings.isDeviceActive`, else `Desktop` | Detected at start; Quest3 uses VR scale factors + hand menu |

---

## 3. Repository layout

```
Assets/
  scripts/                 99 root scripts (original app) + folders below
    XR/                    Our XRI layer (14): GEInteractable, GEPointer, GEInputEvents, GEButton,
                           GEPressVisual, ManipulationHandler, Solver, SolverHandler, Billboard,
                           Orbital, XRInputRig, XRTypes, DesktopMouseInput, GEUnityExtensions
    solver_scripts/        ForceSolver, PlanetForceSolver, MoonForceSolver, PlacementForceSolver,
                           AttachToControllerSolver
    menu_scripts/          GlobalMenuManager, DesktopMenuManager, HandMenu(+Manager), GGVMenuManager,
                           ExperienceModeButton
    AudioService/          AudioService (singleton), AudioServiceProfile (SO), IAudioService, AudioId…
    intro_scripts/         IntroFlow, placement, logo
    controller_tracking_scripts/ ControllerTransformTracker
    Editor/                PlayFromViewScene
    experience/  [planned] ExperienceModule, ExperienceDirector, EnvironmentController, LayoutPreset
    interaction/ [planned] FreePlacementSolver, LabelButton, TwoHandTransformer
    ui/          [planned] DockController, DockPopup, InfoPanel, MoonLabel
    desktop/     [planned] DesktopDock
  build_scripts/Editor/    Quest3ProjectSetup, Quest3Build, Build, Unity6WindowsBuild
  prefabs/                 solar_system_prefab, milky_way_prefab, galaxy_pois_prefab, menu_managers,
                           poi_prefabs/ (poi_<body>_prefab, planet_info_card_prefab), xr/ge_xr_rig,
                           moon prefabs, tractor_beam_prefab, planet_highlighter_prefab, hand menus
  scenes/                  main_scene, core_systems_scene, view_scenes/{galaxy, solar_system,
                           galactic_center}_view_scene, intro_scenes/, development_scenes/,
                           working_scenes/, _backup_original/ [planned]
  shaders/ (43) cginc/     planet, sun, black hole, spiral_stars ×3, orbital_trail, halo, UI…
  materials/ Textures/ models/ audio/ animations/ animation_controllers/ Fonts/
  third_party/mrtk_assets/ Preserved MRTK shader/mesh assets (MIT)
  scriptable_objects/Resources/AudioServiceProfile.asset
  XR/Settings/             OpenXR package settings
  _sources/    [planned]   CREDITS.md for downloaded imagery
docs/                      ROADMAP, GDD, TECHNICAL_OVERVIEW, copy/ [planned], ui/spec.md [planned]
Builds/Quest3/             GalaxyExplorer.apk (git-ignored)
```

---

## 4. Boot and scene flow

**[existing]**
1. `main_scene` holds `GEManagers` (`GalaxyExplorerManager` singleton and the service managers) and loads `core_systems_scene` additively: the XR rig (`ge_xr_rig`: XR Origin, Camera Offset at 0, main camera with `TrackedPoseDriver`, `XRUIInputModule`, `ARCameraManager` disabled by default, `XRInputRig`, AR Session disabled, `ExperienceModeManager`), the `Loader/TouchController/Pivot` content root, menus, fade plane.
2. `IntroFlow` runs the logo → Earth placement → galaxy flow (`FlowManager` timelines). In the editor, `PlayFromViewScene` + `IntroFlow.TryQuickStart()` skip the intro when a view scene is open, anchor content 2 m ahead and load that view (seeding the galaxy in the back stack so Back works).
3. `ViewLoader` loads view scenes additively and keeps a back stack; `TransitionManager` animates content between views (zoom in/out) and toggles `DesktopMouseInput.InputEnabled` during transitions.
4. `GlobalMenuManager` decides which menu buttons show per view; platform switch picks the desktop HUD, GGV menu, or the hand menu (Quest3).

**[planned]** `ExperienceDirector` sits on `GEManagers`, owns the list of `ExperienceModule`s, and calls `ViewLoader.LoadViewAsync` / `UnLoadView` directly (no back stack, no zoom). `TransitionManager` remains for the fade and for restoring pulled objects on switch. Intro is replaced by the three onboarding cards (`OnboardingManager` reused) and the app boots into the Planets module.

---

## 5. Runtime architecture

### 5.1 Managers **[existing]**
- `GalaxyExplorerManager` (singleton): platform id, scale factors, references to `TransitionManager`, `ViewLoaderScript`, `CardPoiManager`, `VoManager`, `CameraControllerHandler`; adds `DesktopMouseInput` on desktop.
- `ExperienceModeManager`: Passthrough (camera clear alpha 0 + `ARCameraManager` on) vs VR (star background, opaque clear); persisted in `PlayerPrefs`; `ShowsVRScenery` consumed by `VREnabled` / `StarBackgroundManager`.
- `AudioService` (singleton; profile from `Resources/AudioServiceProfile`): `PlayClip(AudioId|clip, out source, transform, PlayOptions)`, pooled `PoolableAudioSource`.
- `VOManager`: narration queue; `PlayClip(clip, delay, allowReplay, replaceQueue, blocksProgress)`; `Stop(clearQueue)`; `CurrentClip`. Body narration uses `allowReplay:true, replaceQueue:true`.
- `CardPOIManager`: open/close of the galaxy POI cards; subscribes to `GEInputEvents.GlobalPointerDown`.
- `ForceSolverFocusManager` (per solar prefab): collects `PlanetForceSolver`s in children; enforces one active pull; `ResetAllForceSolvers()`.

### 5.2 Input routing layer **[existing]** — `Assets/scripts/XR`
- `GEInteractable` : `XRBaseInteractable` + `IFarAttachProvider`. One per collider GameObject (filters colliders to its own object after `base.Awake`). Raises pointer/focus events into the hierarchy:
  - `RaisePointerDown/Up(GEPointer, clicked)` → `ExecuteHierarchy<IGEPointerHandler>` (OnPointerDown/Up/Clicked) and `GEInputEvents.RaiseGlobalPointerDown`.
  - `RaiseFocusEnter/Exit` → `IGEFocusChangedHandler.OnBeforeFocusChange` → `IGEFocusHandler.OnFocusEnter/Exit` → `OnFocusChanged`.
- `GEInputEvents.ExecuteHierarchy<T>`: walks from the hit object up its parents, invoking every **enabled** `T` on the first GameObject that has one (MRTK semantics). Disabled behaviours are skipped, so a disabled `PlanetPOI` lets events pass to the parent.
- `GEPointer`: wraps an `IXRInteractor` (hands/controllers) or the mouse (`CreateMouse(attach, rayOrigin)`); `Handedness`, `CanCastFar`, `IsNear(interactable)`, `IsMouse`, `FocusTarget`, `IsFocusLocked`, `RayOrigin`.
- `XRInputRig`: palms from `XRHandSubsystem`, controller transforms, global clicks; disables tracked devices on desktop.
- `GEButton` + `GEPressVisual` + `XRPokeFilter`: pokeable/clickable buttons with `OnClick` UnityEvents (hand menu, intro confirm, desktop HUD via mouse).
- `ManipulationHandler`: one/two-hand move/rotate/scale on a `hostTransform`; `OnManipulationStarted/Ended`; used by `ForceSolver` in the Manipulation state. `PostManipulationResetter` optionally animates rotation/scale/position back after release.
- `Solver` / `SolverHandler`: goal-pose smoothing framework (MRTK port); `SolverHandler.TransformTarget` is what a solver follows.

### 5.3 Force-pull state machine **[existing]** — `solver_scripts/ForceSolver.cs`
States: `Root` (parked at `RootTransform`), `Dwell` (far ray hovering; tractor beam fills over `AttractionDwellDuration` = 2 s), `Attraction` (flying to the hand, or to 1 m in front of the camera on desktop/mouse), `Free` (arrived; floating), `Manipulation` (grabbed; `ManipulationHandler` enabled).
Transitions: far focus → Dwell → Attraction → Free; select while Root: near → Manipulation, mouse/null pointer → Attraction(camera), far pointer → Attraction(hand); select while Free/Dwell/Attraction → Manipulation; manipulation end → Free; `ResetToRoot()` → Root. Events `SetToRoot/Dwell/Attract/Manipulate/Free` drive the focus manager, highlighter and audio.
- `PlanetForceSolver`: narration + ambience on Attraction, highlighter ring on Root, moons hidden in Root/Attraction and shown in Free/Manipulation, grows to `TargetEditScaleCm` (8 cm) on pull, `IsAttractionComplete` waits for the scale-up, `EditScale` exposed for desktop wheel limits.
- `MoonForceSolver`: at rest rides its orbit anchor (position, rotation, parent scale) each frame and before render; sets `Moon.KeepVisible` while out.
- **[planned]** `FreePlacementSolver : PlanetForceSolver` — no return to Root on focus-manager conflicts; `RestoreLayout()` tweens to a `LayoutPreset` slot; 25 cm pull size; bounds auto-restore.

### 5.4 Desktop input **[existing]** — `XR/DesktopMouseInput.cs`
Mouse as a far `GEPointer`: hover → focus events; click → pointer down/up; drag on held object moves via a grab point on the ray; right-drag spins a non-Root solver; wheel scales it (limits relative to `PlanetForceSolver.EditScale`, 0.1×–3×); empty-space drag orbits `CameraController.Pivot`, right-drag pans `EntityToMove`, wheel zooms; keys: `Home` recenter, `Backspace` back, `R` reset, `Esc` close, `Tab` menu, `H`/`F1` help, `1–9,0` pull bodies by preview slot, `M` Moon. `MouseInputEventRouter` stands down when this exists; TouchScript's `CameraController` ignores mouse gestures. **[planned]** adds `P`, `F2–F8`, label clicks, HUD dock.

### 5.5 Experience layer **[planned]**
```
ExperienceModule : ScriptableObject
  string id; string displayName; string subtitle; Sprite dockThumbnail;
  string sceneName;                       // additive scene to load
  EnvironmentMode environment;            // Passthrough | Dimmed | BlackHalo | FullBlack
  ScenePanelCopy panel;                   // title + paragraphs[] (+ instruction)
  LayoutPreset[] layouts;                 // e.g. SolarRow, RelativeSize
  AudioClip narration; AudioClip ambience;

ExperienceDirector : MonoBehaviour (GEManagers)
  IReadOnlyList<ExperienceModule> Modules; ExperienceModule Current;
  void Switch(ExperienceModule m);        // restore current, fade, unload, load, environment, grow-in, narration
  void ApplyLayout(LayoutPreset p);
  event Action<ExperienceModule> Changed;

EnvironmentController : MonoBehaviour (rig)
  void Set(EnvironmentMode m, float fade = .4f);   // dim quad alpha, VR mode, halo pool
  BlackHalo SpawnHalo(Transform target, float diameter);
  bool PassthroughForced { get; set; }             // dock toggle

LayoutPreset : ScriptableObject
  string name; LayoutSlot[] slots;        // (bodyId, localPosition, localScale, rotation)
  static LayoutPreset SolarRow(...);      // generated by editor script from GDD numbers
```
`BodyInfo : ScriptableObject` (displayName, paragraph, stats {Diameter, Mass, OrbitalPeriod, DayLength} as `Stat {double value; string unit; int exponent}`, narration, `BodyInfo[] moons`). Bodies reference their `BodyInfo`; `InfoPanel` renders from it.

### 5.6 UI layer
- **[existing]** `PlanetInfoCard` (world-space canvas + `CanvasGroup`; data-driven title/subtitle/facts/description; side-switching, constant size, fades with body state and transitions) on every body prefab; `DesktopMenuManager` (bottom-right HUD; Help/Mute/Recenter/Reset/Back/About; controls overlay; mute in `PlayerPrefs`, `AudioListener.volume`); `UiWorldPreview` planet bar (to be replaced); `AboutSlate`; hand menu prefabs (`hand_menu_offset`, `GEButton`s).
- **[planned]** `InfoPanel` (rename/extend `PlanetInfoCard`: body/scene/moon variants, superscript mass, outline material), `DockController` (world-space canvas from Figma export; tiles bound to modules; `XRPokeFilter` buttons; grab bar = `ManipulationHandler` on the dock root; palm-up show/hide via `XRInputRig` palm normal), `DockPopup`, `MoonLabel`, `DesktopDock`.

---

## 6. Scenes and prefabs

| Scene | Content root | Key prefabs | Module |
|---|---|---|---|
| `galaxy_view_scene` | `GalaxyContent` | `milky_way_prefab` (DrawStars ×3 layers), `galaxy_pois_prefab` (POI cards → become `LabelButton` tags) | Milky Way |
| `solar_system_view_scene` | `SolarSystemContent/AllContent/HeroView/solar_system_prefab/poi_planet_focus_manager` | `poi_<body>_prefab` ×10, orbit trails, `ForceSolverFocusManager` | Solar System |
| `galactic_center_view_scene` | galactic center content | `poi_sagittarius_a_prefab`, `poi_s2_prefab`, `poi_s102_prefab` | Sagittarius A\* |
| `solar_row_scene` [planned, from solar] | same prefabs, `LayoutPreset`s | Planets |
| `andromeda_scene` [planned, from galaxy] | `andromeda_prefab` (DrawStars params) | Andromeda |
| `galaxies_scene` [planned] | `galaxy_field_prefab` (instanced billboards) | Galaxies |
| `cosmic_web_scene` [planned, from galactic center] | `cosmic_web_prefab` (procedural point cloud) | Cosmic Web |

**Body prefab pattern** (`poi_earth_prefab`, the template for all bodies):
```
poi_earth_prefab
  Earth [Fader, OrbitUpdater] / Trail [OrbitalTrail] / Earth_LOD1
  POI [PlanetPOI(disabled), BillboardLine, PoiResizer…] / FaceCamera / CardDescription, UI_POI
  force_grab_root [SolverHandler, PlanetForceSolver, Planet, Fader]
    earth_scale_controller [PlanetOffsetScaleController] / earth_tilt / axis_rotator /
      earth_sphere_mesh [SphereCollider, ManipulationHandler(host=force_grab_root),
                         PostManipulationResetter, UiPreviewTarget, GEInteractable]
      earth_clouds_mesh, earth_glow_mesh
    earth_moon [Animator, Moon(visualRoot=moon_force_grab_root)] / … / moon_orbit_offset (anchor)
    planet_highlighter_prefab (hover ring)
  moon_force_grab_root [SolverHandler, MoonForceSolver(parentPlanet, orbit), Fader]
    moon_scale_controller / moon_mesh [SphereCollider, ManipulationHandler, PostManipulationResetter,
                                       UiPreviewTarget(slot 10), GEInteractable]
    planet_highlighter_prefab
  earth_info_card, moon_info_card [PlanetInfoCard]
```
New moons follow `moon_force_grab_root` exactly (script `buildmoon.cs` pattern: add collider + `ManipulationHandler` + `PostManipulationResetter` + `GEInteractable` to the mesh, a `MoonForceSolver` root with copied tuning, a highlighter, a `UiPreviewTarget`, and a card).

---

## 7. Rendering

### 7.1 Pipeline rules **[existing]**
- BiRP, forward, Gamma. 4× MSAA. No post-processing stack.
- **Every custom shader**: `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`; `#pragma target 4.5` max; **no geometry shaders** (quads are expanded in the vertex stage with `SV_VertexID/6`, see `cginc/StarQuad.cginc`); no `only_renderers d3d11`; alpha output honest (passthrough composites on alpha).
- Galaxy: `DrawStars` issues `CommandBuffer`s on the main camera (attach order clouds → negative → stars); on XR the down-scaled RT path is skipped and layers draw straight to the eye buffer.
- Orbits: `OrbitalTrail` `CommandBuffer` at `AfterForwardAlpha`.
- Black hole: `black_hole_gravitational_lensing_disc_optimized_shader` (ray-marched disc, alpha follows brightness) + glow card.
- Sun: `sun_shader`, `sun_solar_flare_shader` (scrolling flares), `lens_flare_shader`.
- Planets: `planet_shader` family with `_SunDirection` set by `SunLightReceiver`; `_TransitionAlpha` + `_SRCBLEND/_DSTBLEND` for fades (`Fader`, `Moon`).

### 7.2 Environment modes **[planned]**
- **Dimmed room:** a camera-parented quad at 0.06 m with an unlit alpha shader (`ZTest Always`, queue 4000-1, writes alpha 0.5 black). It must render before world-space UI (queue 4000) and after all content.
- **Black halo:** quad with a radial-gradient alpha texture (procedural, 256²), billboarded to the camera, queue 3000-10, positioned 0.15 m behind the target along the view vector, scaled 1.6× target diameter.
- **Full black:** `ExperienceModeManager` VR mode + `StarBackgroundManager`, passthrough off.
- Passthrough forced by the dock toggle overrides the module mode until toggled back or the module changes.

### 7.3 New content renderers **[planned]**
- **Nebula overlay:** 4–6 alpha planes, 10–20 cm apart along the view axis, each slowly rotating (±0.5°/s), soft-depth fade shader (unlit, `Blend SrcAlpha OneMinusSrcAlpha`, no ZWrite); layers derived from one image by luminance bands + Higgsfield-generated depth variants.
- **Galaxy field:** `Graphics.DrawMeshInstanced` billboards (≤ 500) with an unlit alpha atlas shader; per-instance tint/rotation/size in a `MaterialPropertyBlock`; drift in a `Job` or simple per-frame rotation of the parent.
- **Cosmic Web:** editor script generates ~200 k points along 3D Voronoi cell edges + node clusters → `ComputeBuffer` rendered by the star point-sprite shader with a violet ramp; whole volume rotates via a matrix uniform.
- **Andromeda:** `milky_way_prefab` duplicate with a new `StarsData` set: flatter distribution, white core colour, dust ring band colour, higher tilt.

### 7.4 Performance budgets (Quest 3, 72 Hz, ≥ 60 fps)
≤ 150 draw calls, ≤ 400 k point sprites, ≤ 500 instanced billboards, textures ≤ 2048 ASTC 6×6 (Android overrides applied by `Quest3ProjectSetup`), one dim quad, ≤ 3 world-space canvases visible, no realtime shadows, no per-frame `FindObjectOfType` in hot paths.

---

## 8. UI technology

- World-space uGUI canvases at **0.7 mm per unit** (panels) and **1 mm per unit** (dock); `Canvas.worldCamera` = main camera; `XRUIInputModule` for poke/ray on uGUI; `GEButton`/`XRPokeFilter` for non-uGUI 3D buttons (hand menu style).
- TMP: Selawik SDF family (`Assets/Fonts`), `TextMeshPro/Distance Field` and `…Overlay`; superscripts via `<sup>` rich text; outline via material preset `Selawik Outline` (to add).
- Figma → Unity: export SVG/PNG @2× into `Assets/ui/figma/`; sprites imported as UI sprites, 9-sliced where flagged in `docs/ui/spec.md`; sizes in the spec are mm, converted by the canvas scale.
- Desktop HUD: `menu_managers.prefab` screen-space canvas (800×600 reference, scale with screen).

---

## 9. Audio technology

- `AudioService` + `AudioServiceProfile` (SO in `Resources`): `AudioId` enum → clip mapping for UI/interaction sounds; 3D sources pooled (`Pools/`).
- `VOManager`: single narration channel; queue; `allowReplay`; `Stop(true)` on experience switch.
- Music: `MovableAudioSource`/scene sources today; **[planned]** `MusicController` crossfade by `EnvironmentMode`.
- Mute: `AudioListener.volume` (all) — **[planned]** narration-only mute via `VOManager` gain.
- Import: clips > 20 s **Streaming** (Android override by `Quest3ProjectSetup`), Vorbis q≈0.7; short UI clips Decompress On Load.
- Placeholder narration: Windows Speech (Zira, 44.1 kHz mono WAV) via `moon_vo.ps1` pattern; real voice via Higgsfield `generate_audio` on approval.

---

## 10. Content and asset pipeline

### 10.1 Sourcing order and credits
1. Project assets (§16). 2. Public domain / CC: NASA (PD), ESA/Hubble (CC BY 4.0), USGS Astrogeology (PD), Solar System Scope (CC BY 4.0). 3. Higgsfield generation. Every downloaded or generated asset gets a line in `Assets/_sources/CREDITS.md` (file, source URL, licence, date, edits).

### 10.2 Naming and import
- Files `lowercase_snake_case` with a type suffix: `_texture`, `_material`, `_model`, `_prefab`, `_audio_clip`, `_shader`, `_scene`.
- Textures: max 2048 (hero 4096 desktop / 2048 Android), ASTC 6×6 Android, sRGB except normals/data; mipmaps on for world textures, off for UI.
- Meshes: Read/Write off, no animation import unless used, scale factor 1, meters.
- Prefabs: variants over copies where the base is shared (all `poi_<body>_prefab`s are hand-built; new moons copy the Earth-moon pattern).

### 10.3 Copy pipeline **[planned]**
`docs/copy/{experience}.md` and `bodies.md` (front-matter-free tables) → `Editor/CopyImporter.cs` (menu *Cosmic Simulation/Import Copy*) → updates `ExperienceModule` and `BodyInfo` assets. Writers never open Unity.

### 10.4 Figma pipeline
Design in the Figma file → `get_design_context` for specs → `download_assets` for SVG/PNG → `Assets/ui/figma/` → prefab wiring scripts (RunCommand) build/refresh dock, pop-up, panel, label and HUD prefabs from the exports.

### 10.5 Higgsfield / Blender pipeline
- Images: `generate_image` (prompt + reference) → `remove_background` / `upscale_image` → PNG → import. Equirectangular planet maps only if a NASA map is missing.
- 3D: Higgsfield **3D Scene Builder** (hosted Blender 5.2, `scene_builder_3d_run_python`) → `scene_builder_3d_get_glb` → import GLB via Unity's glTF importer (add `com.unity.cloud.gltfast` if not present) → re-material with project shaders. Used for the layered nebula rig and any bespoke mesh.
- Audio: `generate_audio` for ambience/SFX/narration on request; loudness-normalised before import.

---

## 11. Build and deploy

- **Galaxy Explorer → Quest 3 → Configure Project** (`Quest3ProjectSetup.cs`): OpenXR features (Touch, Touch Plus, Hand Interaction, Hand Tracking, Meta Hand Aim, Meta AR Session/Camera, Meta Quest feature on Android), render modes, Android player settings, 4× MSAA, spatializer none, Android asset overrides, automatic validation fixes. **List OpenXR Features** and **Apply Android Asset Overrides** are separate menu items.
- **Galaxy Explorer → Quest 3 → Build APK** (`Quest3Build.cs`) → `Builds/Quest3/GalaxyExplorer.apk`. Last build: 0 errors, ~111 MB. Manifest: VR category, head tracking, `HAND_TRACKING`, `PASSTHROUGH`, supported devices, GameActivity, landscape, SDK 32/34.
- Install: Meta Quest Developer Hub or `adb install -r`.
- Quest Link testing: press Play with the Standalone OpenXR loader; passthrough over Link needs *Developer Runtime Features* + *Passthrough over Meta Quest Link* in the Link app.
- Editor Play without a headset runs desktop mode (`XRSettings.isDeviceActive` false).
- **Play from any view:** open `galaxy_view_scene`, `solar_system_view_scene` or `galactic_center_view_scene` alone and press Play (`PlayFromViewScene`).

---

## 12. Testing and verification

- **Harness:** `umcp.js` (stdio MCP client for the Unity relay) with `run <file.cs>` executing a `CommandScript : IRunCommand` in the editor; `compile.ps1` (refresh + wait + compiler errors from `Logs/Editor.log`); templates for play/stop/state, synthetic mouse/keyboard events (`InputSystem.QueueStateEvent(Mouse.current, new MouseState{…}.WithButton(...))`, `KeyboardState(Key.X)`), game-view screenshots (`ScreenCapture.CaptureScreenshot`), off-screen renders of prefabs (preview scene + camera → PNG). These live in the session scratchpad; recreate from `CLAUDE.md` if missing.
- **Per-phase smoke (planned `smoke.cs`):** switch every module, open every panel, pull/scale/drop/restore every body and moon, open every tag, toggle passthrough, capture screenshots, assert console has no errors.
- **Device checklist:** hands tracked, dock pokeable, pull/scale/drop, panels legible at 0.75 m, environment modes, ≥ 60 fps (OVR Metrics Tool), passthrough alpha artefacts none, audio spatialised.
- **Regression:** desktop matrix (GDD §5.3) after every phase.

---

## 13. Tooling: MCP usage and gotchas

**Unity relay** (`%USERPROFILE%\.unity\relay\relay_win.exe --mcp --project-path <repo>`): tools `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`, scene-view captures.
RunCommand template:
```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) { /* editor code; result.Log("...") */ }
}
```
Rules learned the hard way:
- The runner forbids `System.Reflection` and asset delete/move (“User interactions are not supported”) → put those in project editor menu items and call `EditorApplication.ExecuteMenuItem`.
- It reports **NOT-OK** whenever any warning was logged (TMP CanvasRenderer noise) even on success; “Unity not detected” means a domain reload is in progress — retry.
- Never `EditorSceneManager.OpenScene` while a scene may be dirty: the modal save dialog blocks all MCP calls. Enter play with the open scenes instead.
- Never edit scripts while in Play mode; always confirm `isPlaying=false` first.
- In RunCommand code `Image` resolves to `Unity.AI.Image`; write `UnityEngine.UI.Image`.
- Play mode writes runtime values into shared materials (`about_material`, `earth_clouds`, Jupiter clouds): revert before committing.
- `Object.GetInstanceID()` is a compile error in 6000.6 (use `GetEntityId`/`GetHashCode`).

**Figma MCP:** `create_new_file`, `use_figma` (build frames), `get_design_context`/`get_metadata` (read specs), `download_assets` (export). **Higgsfield MCP:** `generate_image`, `remove_background`, `upscale_image`, `generate_audio`, `scene_builder_3d_*` (Blender). **Chrome MCP / WebFetch:** sourcing and licence checks.

---

## 14. Conventions

- **C#:** namespace `GalaxyExplorer` (app) / `GalaxyExplorer.XR` (input layer) / `CosmicSimulation` (new experience layer); `PascalCase` types and public members, `_camelCase` private fields, `[SerializeField] private` over public fields, XML doc summary on every new class, no `FindObjectOfType` in `Update`.
- **Prefab edits:** through `PrefabUtility.LoadPrefabContents` scripts committed alongside (so changes are reproducible), not by hand where avoidable.
- **Serialized names:** keep MRTK-era field names where components were GUID-swapped (`SolverHandler`, `ManipulationHandler`, `hostTransform`, …) so existing data stays valid.
- **Git:** branch `quest3-port`; one commit per task group with a plain-language message; `main` is pushed only on request; never commit `Builds/`, `.utmp/`, user settings.
- **Docs:** roadmap/GDD/tech overview are the source of truth; update them in the same commit as a behaviour change.

---

## 15. Known issues and technical debt

- Original intro/placement flow and hand-menu buttons remain until the dock replaces them (Phase 2).
- Galaxy POI cards (`CardPOI`, `poi_text_card_*` textures) are legacy; tags replace them (Phase 4).
- `UiWorldPreview` planet bar renders each body with a live camera; replaced by `DesktopDock` thumbnails (Phase 2).
- About links still point at Microsoft's privacy/terms pages; replace before any store submission.
- Legacy TouchScript remains for touchscreens; mouse is handled by `DesktopMouseInput`.
- Two `PlanetPreviewController` components on `planet_previews` (one legacy with 20 slots) — remove with the bar.
- `main_scene - Copy.unity` (user backup) sits in `Assets/scenes`; move to `_backup_original/` in Phase 0.

---

## 16. Asset inventory (paths)

**Models** `Assets/models/`: sun, mercury, venus, earth, mars, jupiter (+planet, +cloud), saturn, uranus (+planet), neptune, pluto (+LOD1 each); moon; io, europa, ganymede, callisto; mimas, enceladus, titan, iapetus; phobos, deimos; asteroid; sun flares ×3 (+combined); black-hole glow card; galaxy magic window; boundary star background; unit sphere.
**Textures** `Assets/Textures/`: diffuse/specular + normal for every planet and the Moon; earth clouds normal/alpha, earth emissive; jupiter clouds; saturn rings; uranus rings; moon atlases (jupiter, saturn, mars); sun diffuse/alpha/flare/lens flare; black-hole accretion discs ×3 + glow card; crab, homunculus, pillars, ngc1501, trumpler (2D); galaxy bulb; star atlases; orbit alpha; UI icons `Assets/Textures/icons/` (reset, about, back, help, view, sound on/off, three-dots, start, control up/down); `moon_preview_icon`.
**Shaders** `Assets/shaders/` (43) incl. `cginc/` (NearClip, StarQuad). **Materials** `Assets/materials/` per body + rings + galaxy + UI.
**Audio** `Assets/audio/`: `vo_audio_clips/vo_destinations_audio_clips/` 22 (sun, planets, moon, crab, galactic center, homunculus, milky way, ngc1501, pillars, pluto, s2, s102, sagittarius a, solar system, trumpler); `vo_intro_audio_clips/` 16; `ambience_audio_clips/` 12; `ui_audio_clips/` 13 + `ui_transitions_audio_clips/` 11; `sfx_audio_clips/` 3; `music_audio_clips/` 3.
**Prefabs** `Assets/prefabs/`: `solar_system_prefab`, `milky_way_prefab`, `galaxy_pois_prefab`, `poi_prefabs/poi_<body>_prefab` ×13 incl. s2/s102/sagittarius, `planet_info_card_prefab`, moon prefabs ×10, `planet_highlighter_prefab`, `tractor_beam_prefab`, `menu_managers`, hand menus, `xr/ge_xr_rig`, about slate, placement ring, onboarding manager.
**Fonts** `Assets/Fonts/`: Selawik SDF ×5 weights, Segoe UI SDF ×6.
**Animations** moon show/hide, planet highlight, poi card, onboarding sprites.

Needed (new): nebula layer sets ×4, galaxy sprite atlas, halo gradient (procedural), dim quad material, dock/pop-up/panel/label/HUD/onboarding sprites (Figma), thumbnails ×7 (rendered), logo/app icon, 11 narration clips, 4 ambiences, 2 SFX (rumble, whoosh).

---

## 17. Roadmap mapping

| Phase | Systems touched |
|---|---|
| 0 | Backups, rename, docs, Figma file, credits |
| 1 | Figma design system and exports |
| 2 | `experience/`, `EnvironmentController`, `DockController`/`DockPopup`, `InfoPanel`, `FreePlacementSolver`, `LabelButton`, `DesktopDock`, module migration, smoke test |
| 3 | `solar_row_scene`, `LayoutPreset`s, all moons, body panels, Sun touch, two-hand tuning |
| 4 | Tags, nebula overlay renderer + assets, halo, Sagittarius A\* module |
| 5 | Andromeda `StarsData`, galaxy field renderer, Cosmic Web generator |
| 6 | Audio map, narration, `MusicController`, onboarding cards, branding, About |
| 7 | Device pass, budgets, passthrough alpha audit, store readiness |
