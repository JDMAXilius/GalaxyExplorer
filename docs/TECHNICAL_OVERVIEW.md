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
| Stereo | As configured today: Android **Multi Pass**, Windows/Link **Single Pass Instanced** (§7.1) | Not all custom shaders carry the macros — 36 of 47 did not; see `docs/SHADER_STEREO_AUDIT.md` (CS-095) |
| Android player | IL2CPP, ARM64, min API 32, target 34, Vulkan, ASTC, GameActivity, Landscape Left, 4× MSAA | Package id `com.jdmaxilius.cosmicsimulationxr` (renamed 11 Sep 2026; installs alongside the old Galaxy Explorer build) |
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
    experience/  [planned] ExperienceModule, ExperienceDirector, EnvironmentController, LayoutPreset, SwitchNotice
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
tools/mcp/                 Editor harness, outside Assets so Unity never compiles it and it needs no
                           .meta: umcp.js, compile.ps1, smoke.cs, enter/leave_play_mode.cs (§12)
Builds/Quest3/             GalaxyExplorer.apk (git-ignored)
```

---

## 4. Boot and scene flow

**[existing]**
1. `main_scene` holds `GEManagers` (`GalaxyExplorerManager` singleton and the service managers) and loads `core_systems_scene` additively: the XR rig (`ge_xr_rig`: XR Origin, Camera Offset at 0, main camera with `TrackedPoseDriver`, `XRUIInputModule`, `ARCameraManager` disabled by default, `XRInputRig`, AR Session disabled, `ExperienceModeManager`), the `Loader/TouchController/Pivot` content root, menus, fade plane.
2. `IntroFlow` runs the logo → Earth placement → galaxy flow (`FlowManager` timelines). In the editor, `PlayFromViewScene` + `IntroFlow.TryQuickStart()` skip the intro when a view scene is open, anchor content 2 m ahead and load that view (seeding the galaxy in the back stack so Back works).
3. `ViewLoader` loads view scenes additively and keeps a back stack; `TransitionManager` animates content between views (zoom in/out) and toggles `DesktopMouseInput.InputEnabled` during transitions.
4. `GlobalMenuManager` decides which menu buttons show per view; platform switch picks the desktop HUD, GGV menu, or the hand menu (Quest3).

**[planned]** `ExperienceDirector` sits on `GEManagers`, owns the list of `ExperienceModule`s, and calls `ViewLoader.LoadViewAsync` directly (no back stack, no zoom). A module without a `SceneName` opens from its `ContentPrefab` instead, spawned under a content root the director owns; on every switch the director closes all open view scenes but the target and adopts the target if the boot flow already opened it, so scenes cannot pile up. Since CS-092 the director also spawns the open module's own `InfoPanel` (scene variant) from a serialized prefab reference — after the grow-in, destroyed with the previous place — beside a `scene_panel_anchor` under the same content root; a module with no authored copy gets none. `TransitionManager` remains for the fade and for restoring pulled objects on switch. The original intro (logo → Earth placement → galaxy, `IntroFlow` + `FlowManager`) is kept; two one-time hint cards follow placement (`OnboardingManager` reused); the app then lands in the Milky Way module with the dock shown.

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
- `ManipulationPointerRouter` (CS-106): the only thing that delivers a select to a `ManipulationHandler` **no `ForceSolver` owns** — the nebula overlays, the Cosmic Web, Andromeda. `ManipulationHandler` deliberately does not implement `IGEPointerHandler`, and must not be made to: on `solar_system_planets_content_prefab` the handler shares a GameObject with the solver, so it would be invoked twice per pinch, and on the `poi_*` prefabs the handler sits on the mesh with the solver on the parent `force_grab_root`, so the walk would stop at the mesh and the solver would never see a pinch at all. The router goes on the same GameObject as the handler and the `GEInteractable`; if it ever finds a `ForceSolver` on itself or above, it logs and **disables itself** so `ExecuteHierarchy` skips it and the walk reaches the solver unchanged. Near pinch, far ray and poke all arrive through it, and two hands work because `GEInteractable` selects in `Multiple` mode.
- `experience/FreePlacementAnchor` (CS-107): the other half of the same split — the only thing that brings a moved `ManipulationHandler` object **no `ForceSolver` and no `LayoutRig` owns** back home. It remembers a **local** TRS (the frame moves: prefab content hangs off the `ViewLoader`) captured by the spawner, and eases back over 0.8 s. `ExperienceDirector` calls the static `FreePlacementAnchor.CaptureHome(Transform)` immediately after `Instantiate` and **before `GrowIn`**, which drives `localScale` from zero every frame and would otherwise be what got remembered; that call also *adds* an anchor where one is missing, using the `ManipulationPointerRouter` guard (`HostTransform` with no `ForceSolver` on it or above it), so content from a builder nobody has re-run still comes home. `RestoreAll()` is called from `DesktopMouseInput.RestoreLayout` (`R`, above both existing passes, because the rig pass returns) and `ResetView` (every desktop route to Recenter); `RestoreAllImmediate()` from `ExperienceDirector.RestoreEverything`. It has no `Update`: GDD §5.2's stray auto-restore is a body rule and these are objects meant to be pushed away. **The world dock's `DockController.Recenter` still owes it one call.**
- `Solver` / `SolverHandler`: goal-pose smoothing framework (MRTK port); `SolverHandler.TransformTarget` is what a solver follows.
- `experience/UiEventSystemInstaller` (static, `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `SceneManager.sceneLoaded`): guarantees one live `EventSystem` with one `BaseInputModule` (`InputSystemUIInputModule`), because the only authored one — on `main_camera_prefab`, arriving late with `core_systems_scene` — carries no module and every screen-space button was therefore dead. It prefers the app's own `EventSystem`/module over anything it had to create, adds a `GraphicRaycaster` to a screen-space canvas that lacks one, and exposes `IsPointerOverScreenSpaceUi(...)`. **That test is not `EventSystem.IsPointerOverGameObject()`**: every world-space UI prefab carries a `GraphicRaycaster` too, and the blanket version would make the desktop mouse stand down over a destination tag or an info panel and kill the `GEPointer` path. This layer is the only EventSystem user in the project; hand ray, poke and mouse all reach content through `GEInteractable` and physics.

### 5.3 Force-pull state machine **[existing]** — `solver_scripts/ForceSolver.cs`
States: `Root` (parked at `RootTransform`), `Dwell` (far ray hovering; tractor beam fills over `AttractionDwellDuration` = 2 s), `Attraction` (flying to the hand, or to 1 m in front of the camera on desktop/mouse), `Free` (arrived; floating), `Manipulation` (grabbed; `ManipulationHandler` enabled).
Transitions: far focus → Dwell → Attraction → Free; select while Root: near → Manipulation, mouse/null pointer → Attraction(camera), far pointer → Attraction(hand); select while Free/Dwell/Attraction → Manipulation; manipulation end → Free; `ResetToRoot()` → Root. Events `SetToRoot/Dwell/Attract/Manipulate/Free` drive the focus manager, highlighter and audio.
- `PlanetForceSolver`: narration + ambience on Attraction, highlighter ring on Root, moons hidden in Root/Attraction and shown in Free/Manipulation, grows to `TargetEditScaleCm` (8 cm) on pull, `IsAttractionComplete` waits for the scale-up, `EditScale` exposed for desktop wheel limits.
- `MoonForceSolver`: at rest rides its orbit anchor (position, rotation, parent scale) each frame and before render; sets `Moon.KeepVisible` while out.
- **[planned]** `FreePlacementSolver : PlanetForceSolver` — no return to Root on focus-manager conflicts; `RestoreLayout()` tweens to a `LayoutPreset` slot; 25 cm pull size; bounds auto-restore.

### 5.4 Desktop input **[existing]** — `XR/DesktopMouseInput.cs`
Mouse as a far `GEPointer`: hover → focus events; click → pointer down/up; drag on held object moves via a grab point on the ray; right-drag spins a non-Root solver (or turns a two-handed object by its `ManipulationHandler`); wheel scales it (limits relative to `PlanetForceSolver.EditScale`, 0.1×–3×, or the object's own `ScaleLimits`); empty-space drag orbits `CameraController.Pivot`, right-drag pans `EntityToMove`, wheel zooms. `MouseInputEventRouter` stands down when this exists; TouchScript's `CameraController` ignores mouse gestures.

Key map — GDD §5.3, one row each (CS-087): `R` restore the arrangement (`FreePlacementAnchor.RestoreAll()` first, then `LayoutRig.Restore()`, else a `FreePlacementSolver`/`ForceSolver` sweep), `Home` recenter (`DesktopDock.Recenter` → `DockController.Recenter` + `ResetView`, which also runs the anchor sweep), `P` passthrough preview, `Tab` dock show/hide (**read by `DesktopDock` itself**; `DesktopMouseInput` only falls back to folding the legacy button row when there is no dock), `H`/`F1` controls overlay, `Esc` close help → open `DockPopup` → hand-driven `InfoPanel`s + legacy card, `1–9,0`/`M` pull one body (`LayoutRig.Find(id)` → `MoonOrbit.Info.Id` → legacy `UiPreviewTarget.slotId`) and press again to send **that one** home, `F2–F8` switch experience in dock order. There is deliberately no `Backspace`: drill-down navigation is retired (roadmap §4.3) and the desktop HUD's Back button is forced hidden in `DesktopMenuManager.UpdateButtonsActive`.

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
  void Switch(ExperienceModule m);        // restore current, environment, close every other view scene,
                                          // adopt/load the scene OR spawn ContentPrefab, grow-in,
                                          // the module's own scene panel, narration, ambience bed
                                          // (a module with neither warns once and does nothing,
                                          //  and says so once per poke through SwitchNotice)
  void ApplyLayout(LayoutPreset p);
  event Action<ExperienceModule> Changed;

SwitchNotice : MonoBehaviour (makes itself, DontDestroyOnLoad)
  static void NotReady(ExperienceModule m);   // "not built yet" - the refusal, before anything moves
  static void FailedToOpen(ExperienceModule m); // the throw/timeout, after the room came back passthrough
  void Show(string message); void Dismiss();  // builds its own canvas: world-space in the headset
                                          //  (parked above the dock, mm scale, no plate), screen-space
                                          //  above the desktop dock; auto-fades, Escape, or a new place

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
- **[built, CS-094]** `HintCards` + `HintCardSet` (`Assets/scripts/experience/`) — the two one-time cards of GDD §8.6, contract row F-32. Same self-building, prefab-free shape as `SwitchNotice`: world-space canvas on the millimetre scale parked in front of the player (or above the dock on a replay) in the headset, screen-space panel above `DesktopDock` on the monitor, white outlined text and no plate. Copy and art come from a `HintCardSet` in `Assets/scriptable_objects/Resources/hint_cards.asset`, written by **Cosmic Simulation → Build Hint Cards** from `docs/copy/hints.md` — a Resources asset rather than a serialized prefab reference *because* there is no prefab, and a sprite nothing in a scene references is absent from the player build. Art falls back to the intro's own `onboarding_pull`/`onboarding_hold` hand sprites, cross-faded, which is exactly what `onboarding_force_hl2_prefab` does with them on its Timeline. Entry points: `HintCards.ShowIfFirstRun()` (the intro, after placement), `HintCards.Replay()` (Help), `HintCards.Forget()`; the seen flag is `PlayerPrefs` key `CosmicSimulation.HintsSeen`. A card ends on the gesture, on a poke/click/`Space`/`Esc`, or after 6 s.
- **[built, CS-090 + CS-076]** `UtilityWindow` (`Assets/scripts/experience/UtilityWindow.cs`) — the app's only settings surface (GDD §8.2 and §11, contract row F-03): a scale slider for the open experience, mute, narration-only mute, panel text size ×1.0/×1.25/×1.5, and close. Built by `UiPrefabBuilder.BuildUtilityWindow` into `utility_window_prefab` (120 × 102 mm on the millimetre canvas), instantiated on demand by `DockController` as a **sibling** of the dock — a child would inherit the dock's 25° tilt and its 0.001 canvas scale — and opened by a fourth small button under the dock or by `U`. Each control reaches something that already existed rather than duplicating it: mute through `DesktopMenuManager.OnMuteButtonPressed()` where that HUD exists and the same `GalaxyExplorer.Muted` key where it does not (the headset, which never persisted mute before); narration through the new static `VOManager.NarrationEnabled`, gating the queue rather than the audio source; text size through the new static `InfoPanel.TextScale`, folded into the world scale `InfoPanel.Place()` already computes, because the panel's rows are fixed canvas units and a larger `fontSize` would reflow prose into the stat grid; scale by writing `localScale` on `TransitionManager.CurrentActiveScene`, clamped through that content's `ScaleLimits` when it has one. **The slider is not uGUI**: there is no `TrackedDeviceGraphicRaycaster` in this project, so the rail is a `BoxCollider` + `GEInteractable` with no handler of its own, whose events walk up to the window through `ExecuteHierarchy` while each button's `GEButton` stops the walk there; the drag reads `GEPointer.AttachTransform`, which is also what gives desktop parity, since `DesktopMouseInput` rides its mouse attach point along the cursor ray. World-space in both modes: on the desktop it places itself in front of the camera instead of beside the dock, which is parked below that frustum on purpose.
- **[built, CS-062]** `SwitchNotice` — the player-facing half of the CS-037 recovery. A switch that is refused (no scene and no content prefab) or that fails (the loader throws, or the scene never arrives inside the 20 s bound) used to be log-only, so a tile that cannot open was indistinguishable from a dead one. It has no prefab: it builds its own canvas on first use, world-space above the dock in the headset and screen-space above `DesktopDock` on the monitor, white outlined text with no plate. Nothing interactive and no raycaster, so it can neither block a poke nor delay the next switch.

---

## 6. Scenes and prefabs

| Scene | Content root | Key prefabs | Module |
|---|---|---|---|
| `galaxy_view_scene` | `GalaxyContent` | `milky_way_prefab` (DrawStars ×3 layers), `galaxy_pois_prefab` (POI cards → become `LabelButton` tags) | Milky Way |
| `solar_system_view_scene` | `SolarSystemContent/AllContent/HeroView/solar_system_prefab/poi_planet_focus_manager` | `poi_<body>_prefab` ×10, orbit trails, `ForceSolverFocusManager` | Solar System |
| `galactic_center_view_scene` | galactic center content | `poi_sagittarius_a_prefab`, `poi_s2_prefab`, `poi_s102_prefab` | Sagittarius A\* |
| *(no scene)* | `solar_system_planets_content_prefab` — `LayoutRig`, 10 `body_<id>` roots, home anchors, panels | Planets |
| *(no scene)* | `andromeda_content_prefab` — three `SpiralGalaxy` layers on one tilted node, baked `StarsData` each, one-and-two-hand move/rotate/scale, 0.6 m grab sphere | Andromeda |
| *(no scene, planned)* | `galaxy_field_prefab` (instanced billboards) — content prefab once built, per the CS-036 route below, not a `galaxies_scene` | Galaxies |
| *(no scene)* | `cosmic_web_content_prefab` — `CosmicWebRenderer` (procedural point cloud), two-hand rotate/scale, 0.9 m grab sphere | Cosmic Web |

Four of the seven places have **no scene**: since CS-036 `ExperienceDirector` opens a module from its
`ContentPrefab` when it names none, spawning it under a content root that hangs off the `ViewLoader` and
destroying it on the way out. Planets is the first of them (CS-041); Cosmic Web (CS-065) and Andromeda
(CS-102) followed, and Galaxies is the one still outstanding.

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

**Arrangeable body pattern** (`SolarRowBuilder`, the Planets experience). The orbit model's bodies are reused
for their geometry only: the builder lifts each `poi_<id>_prefab`'s `*_tilt` subtree — axial tilt, rotation node,
sphere, clouds, rings, glow, flares — and leaves the POI card, orbit trail, LOD shell, scale controller,
highlighter and moons behind, stripping the interaction components that were on the mesh.
```
solar_system_planets_content_prefab [LayoutRig]
  controller_tracker [ControllerTransformTracker]        one for all ten ForceSolvers
  homes / home_<id>                                       ForceSolver.RootTransform; the layout moves these
  bodies / body_<id>   localScale = diameter in metres
      [SphereCollider(r 0.5) + GEInteractable, ManipulationHandler(host=self, MoveRotateScale),
       ScaleLimits(Body, measureFrom=surface renderer), SolverHandler, ForceSolver, FreePlacementSolver]
      visual   localScale = 1 / measured source diameter  ← normalisation to a 1 m body
        <id>_tilt / axis_rotator / <id>_sphere_mesh, _clouds_mesh, _rings_mesh, _glow_mesh
  panels / panel_<id> [InfoPanel(body=that ForceSolver, target=body root)]
```
Three rules this pattern is built on. **A `LayoutSlot.Scale` is a diameter in metres**, so a body must be 1 m
across at `localScale` 1 — hence the `visual` node, whose scale is measured from the body's own sphere mesh per
axis in mesh space (a world AABB would be inflated by the axial tilt). **Collider, `ManipulationHandler` and
`ScaleLimits` share one GameObject**, because the handler finds its scale constraint with `GetComponent` and
`ScaleLimits` measures the transform it is on. **The plain `ForceSolver`, not `PlanetForceSolver`**: the subclass
pins `GoalScale` to `Vector3.one` in its Root state, which would blow every body up to 1 m, and dereferences a
`PlanetHighlighter` unguarded. `LayoutRig` supplies what is worth keeping — the 25 cm pull-out growth and the
6 cm minimum grab sphere (widened collider radius, not a second shape) — and animates the home anchors between
arrangements, which the force solvers then follow.

---

## 7. Rendering

### 7.1 Pipeline rules **[existing]**
- BiRP, forward, Gamma. 4× MSAA. No post-processing stack.
- **Stereo render mode, as actually configured** (`Assets/XR/Settings/OpenXR Package Settings.asset`): Android = **Multi Pass** (`m_renderMode: 1`), Standalone/Link = **Single Pass Instanced** (`m_renderMode: 0`). This is the opposite of what `CLAUDE.md` §Build says, and it is why a project-wide missing-stereo-macro bug survived unnoticed: on-device Quest builds give each eye its own draw, so only Link shows it. **Full audit of all 78 shaders, with what was fixed and what still needs a compiler: `docs/SHADER_STEREO_AUDIT.md` (CS-095, CS-096).** Verify the macro state of every shader before moving Android to Single Pass Instanced.
- **Every custom shader**: `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` (and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` as the first line of `frag` **if the fragment stage reads anything eye-dependent** — a screen-space texture, `_WorldSpaceCameraPos`; `nebula_card_shader.shader` is the reference); `#pragma target 4.5` max; **no geometry shaders** (quads are expanded in the vertex stage with `SV_VertexID/6`, see `cginc/StarQuad.cginc`); no `only_renderers d3d11`; alpha output honest (passthrough composites on alpha).
- Galaxy: `DrawStars` issues `CommandBuffer`s on the main camera (attach order clouds → negative → stars); on XR the down-scaled RT path is skipped and layers draw straight to the eye buffer.
- Orbits: `OrbitalTrail` `CommandBuffer` at `AfterForwardAlpha`.
- Black hole: `black_hole_gravitational_lensing_disc_optimized_shader` (ray-marched disc, alpha follows brightness) + glow card.
- Sun: `sun_shader`, `sun_solar_flare_shader` (scrolling flares), `lens_flare_shader`. `sun_shader` (`Planets/Sun`, used only by `sun_material`) exposes `_TouchBrightness` 0–1, driven per renderer by `SunTouchResponse` through a `MaterialPropertyBlock`; at 0 it multiplies the disc and rim by exactly 1, so the four prefabs that share the material are untouched unless a hand is inside that Sun. Strength is authored on the material as `_TouchBodyGain` (0.7) and `_TouchRimGain` (1.5).
- Planets: `planet_shader` family with `_SunDirection` set by `SunLightReceiver`; `_TransitionAlpha` + `_SRCBLEND/_DSTBLEND` for fades (`Fader`, `Moon`).

### 7.2 Environment modes **[planned]**
- **Dimmed room:** a camera-parented quad at 0.06 m with an unlit alpha shader (`ZTest Always`, queue 4000-1, writes alpha 0.5 black). It must render before world-space UI (queue 4000) and after all content.
- **Black halo:** quad with a radial-gradient alpha texture (procedural, 256²), billboarded to the camera, queue 3000-10, positioned 0.15 m behind the target along the view vector, scaled 1.6× target diameter.
- **Full black:** `ExperienceModeManager` VR mode + `StarBackgroundManager`, passthrough off.
- Passthrough forced by the dock toggle overrides the module mode until toggled back or the module changes.

### 7.3 New content renderers **[planned]**
- **Nebula overlay** *(built, CS-051)*: **4** alpha planes (the low end of the 4–6 range — each is a full-coverage transparent quad and the black halo behind is a fifth layer, so overdraw is the binding cost on Quest), 70 cm wide, **10 cm** apart along the view axis, each turning about its own normal at ±0.5°/s in alternating directions. Built by `Assets/scripts/Editor/NebulaPrefabBuilder.cs` (**Cosmic Simulation → Build Nebula Prefabs**) into `Assets/prefabs/nebulae/nebula_<id>_prefab.prefab` + `Assets/materials/nebulae/`, and assigned to each destination module's `ContentPrefab`, which is what `ExperienceDirector.OpenDestination` instantiates. Runtime half is `CosmicSimulation.NebulaOverlay` (billboards the stack, spaces and spins the cards). Shader `CosmicSimulation/NebulaCard` (`Assets/shaders/nebula_card_shader.shader`): unlit, `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha` (the separate alpha blend keeps the eye-buffer alpha correct for the passthrough compositor), no ZWrite, target 3.5, stereo macros, no geometry stage. Layers are derived from **one plate by luminance band** — the plates are photographs with no usable alpha, so alpha comes from a feathered luminance window per card, plus a radial edge fade. Two seam fades: a **near fade** (view-space depth, always on) and an **intersection fade** against `_CameraDepthTexture` behind the `NEBULA_SOFT_DEPTH` keyword. `NebulaOverlay` turns that keyword and the camera's `DepthTextureMode.Depth` on together and puts them back on disable, so the depth prepass is paid only while an overlay is open; if the depth texture is missing the shader reads it as "no occluder" and the card simply does not get the intersection fade. CS-052 replaces the plates with purpose-made layer sets; only the band values need retuning. The grab root carries `SphereCollider` + `GEInteractable` + `ManipulationHandler` (MoveScale — never rotate, the stack billboards itself) + `ManipulationPointerRouter` + `FreePlacementAnchor` + `ScaleLimits(Nebula)`; the router and `playGrabSounds` arrived with CS-106 and the anchor with CS-107, so **the seven prefabs must be rebuilt** before an overlay can be grabbed by hand or brought back by `R`.
- **Galaxy field:** `Graphics.DrawMeshInstanced` billboards (≤ 500) with an unlit alpha atlas shader; per-instance tint/rotation/size in a `MaterialPropertyBlock`; drift in a `Job` or simple per-frame rotation of the parent.
- **Cosmic Web** *(generator built, CS-065; not yet compiled or measured — CS-066)*: **120 000** point sprites, not the ~200 k first sketched here and well under the 400 k frame ceiling in §7.4, because the player stands *inside* this volume and blended fill, not point count, is the binding cost. `CosmicSimulation.CosmicWebGenerator` places them on a jittered-grid Voronoi tessellation — the voids are the cells and the matter is on their walls (sheets), edges (filaments) and vertices (clusters), which is the right way round physically. Candidates are *projected* onto those exact planes/lines/points rather than rejection-sampled, then re-measured and dropped if they landed on the extension of a face that does not exist. Deterministic from a seed and **not baked**: a `StarsData` asset this size would be ~28 MB of YAML, so `CosmicSimulation.CosmicWebRenderer` rebuilds it at run time over several frames at 3 ms each, uploading and drawing each slice as it lands. It feeds the **existing** star path unchanged — same `StarVertDescriptor` struct and stride, same `cginc/StarQuad.cginc` six-vertex quad expansion, same `CommandBuffer.DrawProcedural` (at `AfterSkybox`, so opaque depth occludes it and world-space UI still draws over it). Shader `CosmicSimulation/CosmicWebPoints` (`Assets/shaders/cosmic_web_points_shader.shader`): additive, target 4.5, stereo macros, no geometry stage, with the **violet ramp** as three HDR material colours (void → filament → node) driven by a 0–1 coordinate the generator packs into the struct's spare `ellipseOffset` field, so the colour can be retuned without regenerating a point. `Galaxy/Stars` is untouched. The whole volume drifts at the GDD's 0.5°/min through the shader's `_Age` uniform rather than through the transform, so the drift can never fight a two-handed grab. Built by `Assets/scripts/Editor/CosmicWebBuilder.cs` (**Cosmic Simulation → Build Cosmic Web Content**) into `Assets/prefabs/experiences/cosmic_web_content_prefab.prefab` + `Assets/materials/cosmic_web/`, assigned to the `cosmic_web` module's `ContentPrefab` per CS-036. The root is `SphereCollider` + `GEInteractable` + `ManipulationHandler` (TwoHandedOnly, RotateScale, per GDD 4.7) + `ManipulationPointerRouter` (CS-106, without which the two-hand grab never reaches the handler) + `FreePlacementAnchor` (CS-107, without which nothing can bring it back) + `ScaleLimits`.
- **Andromeda** *(generator built, CS-102; never run — CS-103)*: not a `milky_way_prefab` duplicate in the end, but the same recipe rebuilt from its parts, which is cheaper to review and cannot inherit the Milky Way's scene-specific rig. `Assets/scripts/Editor/AndromedaBuilder.cs` (**Cosmic Simulation → Build Andromeda Content**) writes three baked `StarsData` assets, three materials on the existing `Galaxy/Stars`, `Galaxy/StarClouds` and `Galaxy/StarsNeg` shaders, and `Assets/prefabs/experiences/andromeda_content_prefab.prefab`, then sets the `andromeda` module's `ContentPrefab` per CS-036. The prefab is a grabbable root (sphere collider, `GEInteractable`, `ManipulationHandler` on `MoveRotateScale`, `ScaleLimits` on Custom 0.5–3 m with `authoredMetresAtUnitScale` 1.2 — all four on **one** GameObject, per the CS-041 note) over one `andromeda_galaxy` node rotated `Euler(75, 55, 0)` and scaled so the measured star envelope comes out at the GDD's 1.2 m.
  **17 200 points** in three layers — clouds 5 000 (the bulge, which stacks additively into the white core), dust 3 200 (a reddish annulus on the darkening "negative" shader, 0.46–1.0 of the radius), stars 9 000 — against the Milky Way's 18 720 through the same renderer and the 400 000-sprite ceiling in §7.4. Distinguishable from the Milky Way parametrically, not by tint alone: `MinEllipseScale` 0.10 vs 0.02, `SpiralRotation` 470° vs 370°, two arms 180° apart vs 95°, half-thickness 5.5% of the radius, and a white→blue colour ramp instead of the Milky Way's blue-tinged one. All three layers run `renderIntoDownscaledTarget = false`, which is what the headset forces anyway, so desktop and device draw the same galaxy and nothing blits over the camera target. Baked rather than generated at run time (unlike the Cosmic Web) because 17 200 points is ~4 MB of YAML, the same order as the 4.2 MB the Milky Way's three already cost; the generator is deterministic from three fixed seeds so a re-run diffs empty.
  Two inherited gaps CS-103 must confirm: the prefab now also carries `ManipulationPointerRouter` (CS-106), without which nothing delivered a hand or ray select to the handler at all, and `FreePlacementAnchor` (CS-107), without which nothing brought it home again — so the builder has to be re-run before GDD 4.4's "move, tilt, scale with hands" can pass; and `DrawStars.cameraEvent` is private and `DrawStars` is added at run time, so Andromeda draws at the default `BeforeForwardOpaque` rather than the `AfterSkybox` `CosmicWebRenderer` chose.

### 7.4 Performance budgets (Quest 3, 72 Hz, ≥ 60 fps)
≤ 150 draw calls, ≤ 400 k point sprites, ≤ 500 instanced billboards, textures ≤ 2048 ASTC 6×6 (Android overrides applied by `Quest3ProjectSetup`), one dim quad, ≤ 3 world-space canvases visible, no realtime shadows, no per-frame `FindObjectOfType` in hot paths.

---

## 8. UI technology

- World-space uGUI canvases at **0.7 mm per unit** (panels) and **1 mm per unit** (dock); `Canvas.worldCamera` = main camera; `GEButton`/`XRPokeFilter` for non-uGUI 3D buttons (hand menu style). World-space UI is hit with **physics through `GEPointer`**, not by the EventSystem — the `GraphicRaycaster` those prefabs carry is inert as far as gameplay goes (see §5.2, `UiEventSystemInstaller`).
- TMP: Selawik SDF family (`Assets/Fonts`), `TextMeshPro/Distance Field` and `…Overlay`; superscripts via `<sup>` rich text; outline via material preset `Selawik Outline` (to add).
- Figma → Unity: export SVG/PNG @2× into `Assets/ui/figma/`; sprites imported as UI sprites, 9-sliced where flagged in `docs/ui/spec.md`; sizes in the spec are mm, converted by the canvas scale.
- Desktop HUD: `menu_managers.prefab` screen-space canvas (800×600 reference, scale with screen) and `desktop_dock_prefab`. Both are clicked through the EventSystem, which is why `UiEventSystemInstaller` exists; both already carry a `GraphicRaycaster`.

---

## 9. Audio technology

- `AudioService` + `AudioServiceProfile` (SO in `Resources`): `AudioId` enum → clip mapping for UI/interaction sounds; 3D sources pooled (`Pools/`). **`AudioId` is append-only** — Unity serialises an enum as an int, so inserting a member silently remaps every clip already assigned in the asset. A pooled source is parented to the target transform, refuses the same clip twice inside 50 ms, and is cut off if that transform is deactivated; pass no target for a sound that outlives the thing that made it.
- SFX map (CS-070), GDD §9 slot → id → clip: hover tick `Focus`/`ui_default_focus`; select and poke press `Select`/`ui_select`; poke release `PokeRelease`/`ui_touch_deselect`; dock show/hide `DockShow`,`DockHide`/`ui_handmenu_appear`,`_disappear`; layout pop-up `ToolboxShow`,`ToolBoxHide`/`ui_toolbox_show`,`_hide` (original names, repurposed — the toolbox is gone); info panel `CardSelect`,`CardDeselect`/`ui_poi_card_select`,`_deselect`; grab/hold and release `ManipulationStart`,`ManipulationEnd`/`ui_forcegrab_hold`,`_release`; force-pull `ForcePull`/`ui_forcegrab_pull` and its beam `ForceDwell`/`ui_tractor_beam`; grow-in whoosh `GrowIn`/**no clip yet** (CS-077). The Sun's touch rumble is not an `AudioId` at all — it needs a loop, so it is a serialized `rumbleClip` on `SunTouchResponse`, also CS-077.
- Who plays what: `GEButton` (select, falling back to `AudioId.Select` when a prefab assigned no `clickSound`; poke release), `DockTile`, `LabelButton`, `DockPopup`, `ForceSolver` (the whole force/grab/release set, from its state machine). `ManipulationHandler` has an opt-in `playGrabSounds`, **off by default**: `ExecuteHierarchy` delivers to every enabled handler on the first matching GameObject, and on a body the handler shares that object with the `ForceSolver` that already sounds those states. Turn it on only where there is no solver — i.e. wherever `ManipulationPointerRouter` is: nebula overlays, cosmic web, Andromeda, and the dock drag bar once CS-108 makes it work.
- `VOManager`: single narration channel; queue; `allowReplay`; `Stop(true)` on experience switch.
- Music: `MusicController` (`Assets/scripts/experience/`) on the `cosmic_systems` root — two looping 2D `AudioSource`s it makes itself, crossfaded (2 s) on `EnvironmentController.ModeChanged`, one bed per `EnvironmentMode` (mapping: decisions D-006). Re-entering a state never restarts its bed; a state with no clip fades to silence and stops. Ducks to 55 % while `VOManager.IsPlaying`. Not routed through `AudioServiceProfile.musicAudioMixer`: that mixer's groups exist to be muted and unmuted by the legacy per-view snapshots, which would fight a per-source fade. The five inherited beds under `MusicAudioSources` are switched off by *Install Runtime Systems*.
- Ambience: `AmbienceController` (`Assets/scripts/experience/`, CS-093) — one looping 2D `AudioSource` it makes itself, faded over 1.5 s, playing `ExperienceModule.Ambience`. It makes itself on first use (`Resolve()`, `DontDestroyOnLoad`, the `SwitchNotice` pattern), so nothing wires it; an instance dropped into `cosmic_systems` by hand is adopted instead and its levels tuned there. `ExperienceDirector` stops the bed where it stops narration and starts it where it starts narration — after the grow-in, so an abandoned switch stays silent. A module with no clip is silence, not a warning: only the 12 inherited **body** beds exist, and the four experience beds are still owed. Separate from `MusicController` because music follows the *room state* and must not restart when the experience changes, while ambience follows the *experience* and must. Per-body ambience is a different layer and already spatial: `PlanetForceSolver` plays its own clip through `AudioService`, parented to the body.
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

- **Cosmic Simulation → Quest 3 → Configure Project** (`Quest3ProjectSetup.cs`): OpenXR features (Touch, Touch Plus, Hand Interaction, Hand Tracking, Meta Hand Aim, Meta AR Session/Camera, Meta Quest feature on Android), render modes, Android player settings, 4× MSAA, spatializer none, Android asset overrides, automatic validation fixes. **List OpenXR Features** and **Apply Android Asset Overrides** are separate menu items.
- **Cosmic Simulation → Quest 3 → Build APK** (`Quest3Build.cs`) → `Builds/Quest3/CosmicSimulationXR.apk`. Last build: 0 errors, ~112 MB.
- `ConfigureProject()` runs at the head of every build and **overwrites player settings from its own constants** — `AndroidPackageId` above all. Editing `ProjectSettings.asset` alone is not enough; the build will put the old value back. (This bit us on 11 Sep 2026: the APK shipped as `com.jdmaxilius.galaxyexplorer` with the new product name.)
- **Version (CS-113, D-001a extension).** `Quest3ProjectSetup.AppVersion` (`0.9.0`) is the only place the number lives. `ConfigureVersion()` writes it to `PlayerSettings.bundleVersion` — not per-platform, so it is `Application.version` on Quest and desktop alike — and sets `PlayerSettings.Android.bundleVersionCode` to a value **derived** from it (`major*10000 + minor*100 + patch`, so 900); Meta only requires the code to increase, and two hand-maintained numbers drift. Minor and patch must stay below 100 or the ordering breaks; `ConfigureVersion` logs an error if they do not. `Unity6WindowsBuild` sets `bundleVersion` from the same constant because it does not call `ConfigureProject()`.
- **Legal notices (CS-116).** The MIT licence for the inherited Galaxy Explorer code requires its notice to travel with every copy, so the repo-root `License.txt` is not enough. `EnsureLegalNoticesShip()` copies it verbatim to `Assets/Resources/legal/galaxy_explorer_license.txt` on every `ConfigureProject()`, and `Quest3Build.BuildApk` **aborts** if the notice would not ship. Resources, not `StreamingAssets`: on Android StreamingAssets sits inside the APK and is only readable through `UnityWebRequest`. The root file is the source of truth and the shipped copy is regenerated, never edited. Our own copyright is a separate file, `cosmic_simulation_xr_notice.txt` — do not merge, reword or trim either. Runtime: `LegalNotices` (lazy, cached, errors loudly if a resource is missing) and `LegalNoticeText`, which writes a chosen notice into a `TMP_Text` so the About slate needs no code of its own. `AboutSlate.ToggleLicencePage()` swaps its `aboutPage` array for its `licencePage` array; both are empty until the prefab work (CS-117) lands, and the toggle is inert until then.
- A build started from a script stops on the modal **"Unsupported Input Handling on Android"**, because Active Input Handling is "Both" (XRI needs the new system, TouchScript the old). Answer **Ignore**; tick *Don't ask again for this session* for unattended builds. Manifest: VR category, head tracking, `HAND_TRACKING`, `PASSTHROUGH`, supported devices, GameActivity, landscape, SDK 32/34.
- Install: Meta Quest Developer Hub or `adb install -r`.
- Quest Link testing: press Play with the Standalone OpenXR loader; passthrough over Link needs *Developer Runtime Features* + *Passthrough over Meta Quest Link* in the Link app.
- Editor Play without a headset runs desktop mode (`XRSettings.isDeviceActive` false).
- **Play from any view:** open `galaxy_view_scene`, `solar_system_view_scene` or `galactic_center_view_scene` alone and press Play (`PlayFromViewScene`).

---

## 12. Testing and verification

- **Harness: `tools/mcp/`, checked in (CS-098).** It used to live in the session scratchpad and was rebuilt from scratch in at least three sessions before someone noticed. It sits outside `Assets/`, so Unity never compiles it, it needs no `.meta` files, and none of it ships in the APK. Read `tools/mcp/README.md` first.
  - `umcp.js` — dependency-free Node stdio MCP client: `list`, `call <tool> <json>`, `run <file.cs>`. Spawns the relay itself, does the JSON-RPC `initialize` handshake, retries `Unity_RunCommand` through a domain reload ("Unity not detected"), and reads the argument name for the source out of the tool's own `inputSchema` rather than assuming it.
  - `compile.ps1` — refresh, wait out the compile and domain reload, report `error CS####` lines written to `Logs/Editor.log` **since** the refresh. Refuses while play mode is running. Exit 0 clean / 1 errors / 2 timeout / 3 refused.
  - `enter_play_mode.cs`, `leave_play_mode.cs` — named in full deliberately: the old scratchpad pair was `stop.cs` (left play) beside `exit.cs` (quit the editor). Nothing in `tools/mcp/` quits the editor, and nothing in it opens a scene.
  - Still ad hoc per session: synthetic mouse/keyboard events (`InputSystem.QueueStateEvent(Mouse.current, new MouseState{…}.WithButton(...))`, `KeyboardState(Key.X)`) — snippets are in the README — and off-screen prefab renders (preview scene + camera → PNG).
- **Per-phase smoke: `tools/mcp/smoke.cs`.** Walks every `ExperienceModule`: switches to it, checks `Current`, the `EnvironmentController` mode and the underlined dock tile, looks for a bound scene panel, pulls one body to the hand, moves it, `RestoreLayout()`s it and checks it came home, and captures a screenshot per module. Writes `Logs/smoke_report.txt` (verdict on line 2) and `Logs/smoke/NN_<module>.png`; neither is committed. Three shapes are forced by the runner and should not be undone: it will not enter play mode itself (that reloads the domain and unloads the assembly the command is running in), `Execute` returns immediately and the walk is driven from `EditorApplication.update` with progress in `SessionState` (every `run` compiles a fresh assembly, so run it again to poll), and it polls for `ExperienceDirector` and waits out `IntroRunning` rather than concluding — `core_systems_scene` arrives late and `Switch` is refused during the intro.
  - **Verdicts are three-valued on purpose.** A module with neither a scene nor a content prefab being refused is a `PASS`, and so is a module with no authored copy showing no panel — both are `ExperienceDirector` working. Anything it cannot assert is a `SKIP` carrying the reason. The harness prints its own verdict because the relay answers NOT-OK on any warning at all.
  - **Not yet covered:** it drives the director directly rather than poking the dock, so a dead `GEButton` or a mis-wired `XRPokeFilter` would go unnoticed; it pulls one body per module rather than every body; and it does not touch hand input, moons, tags, two-handed scaling or the passthrough toggle. Those are the remaining part of CS-033's line.
- **Device checklist:** hands tracked, dock pokeable, pull/scale/drop, panels legible at 0.75 m, environment modes, ≥ 60 fps (OVR Metrics Tool), passthrough alpha artefacts none, audio spatialised.
- **Regression:** desktop matrix (GDD §5.3) after every phase.

---

## 13. Tooling: MCP usage and gotchas

**Unity relay** (`%USERPROFILE%\.unity\relay\relay_win.exe --mcp --project-path <repo>`): tools `Unity_RunCommand`, `Unity_GetConsoleLogs`, `Unity_Camera_Capture`, scene-view captures. Drive it with `tools/mcp/umcp.js` (§12) — `node tools/mcp/umcp.js list | call <tool> '<json>' | run <file.cs>`. One client at a time: the relay is a single shared connection and two clients deadlock, which is also why only one `unity-editor` agent may run at once.
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
- Write RunCommand scripts **fully qualified with no `using` directives** — the runner wraps the file in a preamble of its own, and a `using` block that lands after it will not compile. Everything in `tools/mcp/` is written that way; copy the style.
- `EditorApplication.EnterPlaymode()` from a RunCommand reloads the domain and unloads the assembly the command is executing in. Queue it on `delayCall`, return immediately, and expect "Unity not detected" for the next few seconds (`tools/mcp/enter_play_mode.cs`).

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
- `UiWorldPreview` planet bar is **retired in code** (CS-087): each tile switches itself off in `OnEnable` before it can build its camera, and `PlanetPreviewController` switches the whole `planet_previews` row off in `Awake`, so the bar costs no camera, no `RenderTexture` and no layer shuffle. Both keep a `keepLegacyBar` toggle for diagnosis. Removing the components and the row is prefab surgery — **CS-088**. Two consequences: `ForceSolver.OnPointerDown` still calls `FindObjectOfType<PlanetPreviewController>()` on every pull and now always fails (delete that block with CS-088), and the bar's side effect of swinging `LightSourcePosition` on a desktop pull is gone — that object exists only in the legacy `solar_system_view_scene`.
- About links still point at Microsoft's privacy/terms pages: six `Hyperlink` instances (`Assets/scripts/Hyperlink.cs`) under `links_offset` in `Assets/prefabs/about_slate_prefabs/about_slate_prefab.prefab` (`hl2_for_devs`, `galaxy_explorer`, `github`, `original_galaxy_explorer`, `privacy`, `microsoft_services_agreement`), each a prefab-override on nested `link_prefab.prefab`. `docs/copy/ui.md`'s About section only calls for two (Source code, Privacy) plus Close, so this is prefab surgery — delete four, re-point two — not a text swap; `CopyImporter` does not touch this file, so hand-authoring in the prefab is the only path today. See `docs/store/STORE_READINESS_CHECKLIST.md` (CS-086).
- Legacy TouchScript remains for touchscreens; mouse is handled by `DesktopMouseInput`. It bundles a TUIO/OSC module that can open a network socket, inert but flagged for a submission decision — CS-112. Removing it and moving Android off Active Input Handling "Both" is optional Phase 7 work — CS-100.
- Two `PlanetPreviewController` components on `menu_managers/desktop_menu/canvas/planet_previews` (one legacy with 20 slots) — remove with the bar (CS-088). Retiring the row switches both off at once, which is why the row is switched off rather than a component destroyed.
- ~~`main_scene - Copy.unity` (user backup) sits in `Assets/scenes`; move to `_backup_original/` in Phase 0.~~ **Stale, struck 12 Sep 2026** — CS-001 already did this: the file is `Assets/scenes/_backup_original/main_scene_user_copy.unity` and nothing named `main_scene - Copy.unity` remains in `Assets/scenes`.

---

## 16. Asset inventory (paths)

**Models** `Assets/models/`: sun, mercury, venus, earth, mars, jupiter (+planet, +cloud), saturn, uranus (+planet), neptune, pluto (+LOD1 each); moon; io, europa, ganymede, callisto; mimas, enceladus, titan, iapetus; phobos, deimos; asteroid; sun flares ×3 (+combined); black-hole glow card; galaxy magic window; boundary star background; unit sphere.
**Textures** `Assets/Textures/`: diffuse/specular + normal for every planet and the Moon; earth clouds normal/alpha, earth emissive; jupiter clouds; saturn rings; uranus rings; moon atlases (jupiter, saturn, mars); sun diffuse/alpha/flare/lens flare; black-hole accretion discs ×3 + glow card; crab, homunculus, pillars, ngc1501, trumpler (2D); galaxy bulb; star atlases; orbit alpha; UI icons `Assets/Textures/icons/` (reset, about, back, help, view, sound on/off, three-dots, start, control up/down); `moon_preview_icon`.
**Shaders** `Assets/shaders/` (47, plus 31 vendored elsewhere under `Assets/`) incl. `cginc/` (NearClip, StarQuad); stereo-macro state of each: `docs/SHADER_STEREO_AUDIT.md`. **Materials** `Assets/materials/` per body + rings + galaxy + UI.
**Audio** `Assets/audio/`: `vo_audio_clips/vo_destinations_audio_clips/` 22 (sun, planets, moon, crab, galactic center, homunculus, milky way, ngc1501, pillars, pluto, s2, s102, sagittarius a, solar system, trumpler); `vo_intro_audio_clips/` 16; `ambience_audio_clips/` 12; `ui_audio_clips/` 13, with `ui_transitions_audio_clips/` 11 nested inside it (not a sibling — verified 12 Sep 2026); `sfx_audio_clips/` 3; `music_audio_clips/` 3.
**Prefabs** `Assets/prefabs/`: `solar_system_prefab`, `milky_way_prefab`, `galaxy_pois_prefab`, `poi_prefabs/poi_<body>_prefab` ×13 incl. s2/s102/sagittarius, `planet_info_card_prefab`, moon prefabs ×10, `planet_highlighter_prefab`, `tractor_beam_prefab`, `menu_managers`, hand menus, `xr/ge_xr_rig`, about slate, placement ring, onboarding manager.
**Fonts** `Assets/Fonts/`: Selawik SDF ×5 weights, Segoe UI SDF ×6.
**Animations** moon show/hide, planet highlight, poi card, onboarding sprites.

Needed (new): nebula layer sets ×4, galaxy sprite atlas, halo gradient (procedural), dim quad material, dock/pop-up/panel/label/HUD/onboarding sprites (Figma), thumbnails ×7 (rendered), logo/app icon, 11 narration clips, 4 ambiences (Cosmic Web, Galaxies, Andromeda, Sagittarius A\* — a 5th, the Milky Way map, is an open question against GDD §4.3's "Galaxy ambience"; see CS-077), 2 SFX (rumble, whoosh; also CS-077).

---

## 17. Roadmap mapping

| Phase | Systems touched |
|---|---|
| 0 | Backups, rename, docs, Figma file, credits |
| 1 | Figma design system and exports |
| 2 | `experience/`, `EnvironmentController`, `DockController`/`DockPopup`, `InfoPanel`, `FreePlacementSolver`, `LabelButton`, `DesktopDock`, module migration, smoke test |
| 3 | `solar_system_planets_content_prefab` + `LayoutRig`, `LayoutPreset`s, all moons, body panels, Sun touch, two-hand tuning |
| 4 | Tags, nebula overlay renderer + assets, halo, Sagittarius A\* module |
| 5 | Andromeda `StarsData`, galaxy field renderer, Cosmic Web generator |
| 6 | Audio map, narration, `MusicController`, onboarding cards, branding, About |
| 7 | Device pass, budgets, passthrough alpha audit, store readiness |
