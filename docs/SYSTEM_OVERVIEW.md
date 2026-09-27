# Cosmic Simulation XR — System Overview

*The codebase as it is on 27 September 2026 (branch `quest3-port`, mirrored to `main`, commit `ba2bac45`). Companion to `docs/SIMULATION_GUIDE.md` (what the player gets). It supersedes `docs/TECHNICAL_OVERVIEW.md`, which still marks many built systems "[planned]". Paths are relative to `D:\Documents\Claude\GalaxyExplorer`.*

---

## 0. Read this first

1. **The shipping app is the legacy tree.** That means `Assets/scripts/**` driven by `main_scene` + `core_systems_scene`. The owner decided this on 13 Sep (BACKLOG, around line 546). `Assets/Cosmic/**` is a parked from-scratch rework.
2. **Build Settings still boots the rework.** `ProjectSettings/EditorBuildSettings.asset` lists `Assets/Cosmic/Scenes/main.unity` as scene 0. **Cosmic/Build/Main Scene** (`Assets/Cosmic/Editor/Scene.cs:226-229`) re-inserts it at index 0 each run. `Quest3Build` builds every enabled scene in order, so an APK built today boots the rework, which has none of the new moons. **Fix before shipping:** untick or remove it so `main_scene` is scene 0.
3. **The OpenAI key would ship inside the APK.** `BeingKeys.openAiKey` is serialized into `Assets/Being/Data/being_keys.asset` (gitignored). It reaches the build through the Being prefab and is extractable. A short-lived client-secret endpoint is the proper fix and is not built.

---

## 1. Stack

| Layer | Choice |
|---|---|
| Engine | Unity **6000.6.0f1**, Built-in Render Pipeline, Gamma, forward, 4× MSAA, no post stack |
| XR | OpenXR 1.18.0, Unity OpenXR: Meta 2.6.1, AR Foundation 6.6.2 (passthrough), XR Management 4.7.0 |
| Interaction | XR Interaction Toolkit 3.6.0, XR Hands 1.9.0 |
| Input | Input System 1.20.0; Active Input Handling **Both** (XRI on the new system; TouchScript and legacy on the old) |
| UI | uGUI 2.6.0 with TextMesh Pro; Selawik SDF fonts |
| Other packages | Shader Graph 17.6, Timeline 6.6, Test Framework 1.8 (no test assemblies yet), AI Assistant 2.19.0-pre.2 (the editor MCP relay), AI Navigation 2.0.14 |
| Vendored | TouchScript (`Assets/external`), FlowManager, NuGet, `Assets/Packages/Layers`, MRTK leftovers (`Assets/third_party`) |
| OpenXR features | OculusTouch, MetaQuestTouchPlus, HandInteraction, XR Hands tracking + Meta aim, Meta AR Session, Meta AR Camera (passthrough); Android also MetaQuestFeature + DisplayUtilities (`Quest3ProjectSetup.cs:62-77`) |
| Stereo | Android **Single Pass Instanced**; Standalone/Link **Multi Pass** (D-008, `docs/SHADER_STEREO_AUDIT.md`) |
| Android player | IL2CPP, ARM64, min API 32 / target 34, Vulkan only, ASTC, GameActivity, Landscape Left; package `com.jdmaxilius.cosmicsimulationxr`, version 0.9.0; company JDMAXilius, product "Cosmic Simulation XR" |
| Editor tooling | Unity MCP relay (`tools/mcp`), Blender MCP, Higgsfield MCP (images, voice), Figma MCP; Python 3 (numpy, PIL) |

---

## 2. Repository layout

```
Assets/
  scripts/                 SHIPPING CODE (namespaces GalaxyExplorer + CosmicSimulation)
    *.cs (~100)            Inherited Galaxy Explorer: GalaxyExplorerManager, ViewLoader, TransitionManager,
                           ZoomInOut, IntroFlow, DrawStars, SpiralGalaxy, VOManager, Moon, PlanetInfoCard,
                           ForceSolverFocusManager, CardPOI, PlanetPOI, AboutSlate …
    experience/ (49)       The Cosmic Simulation layer: ExperienceDirector, ExperienceModule, LayoutPreset,
                           LayoutRig, EnvironmentController, DockController, DesktopDock, DockPopup,
                           UtilityWindow, MicrophoneRow, InfoPanel, MoonLabel, MoonOrbit, HintCards,
                           SwitchNotice, FreePlacementSolver/Anchor, ScaleLimits, TouchNudge,
                           SunTouchResponse, MusicController, AmbienceController, GalaxyField, GalaxyPins,
                           GalaxyBar, CosmicWebGenerator/Renderer, Nebula*, PlaceShell, UiEventSystemInstaller
    XR/ (15)               Input layer replacing MRTK: GEInteractable, GEPointer, GEInputEvents,
                           ManipulationHandler, ManipulationPointerRouter, DesktopMouseInput, XRInputRig,
                           Solver/SolverHandler, GEButton, GEPressVisual, Billboard
    solver_scripts/        ForceSolver, PlanetForceSolver, MoonForceSolver, AttachToControllerSolver
    being/Host.cs          The Being's adapter for this tree
    AudioService/          Pooled SFX service + profile
    menu_scripts/ intro_scripts/ controller_tracking_scripts/ Pools/
    Editor/ (43)           Every "Cosmic Simulation/" builder, AppCheck, PlayFromViewScene
  Being/                   The Cosmic Being — assembly Cosmic.Companion (no references) + .Editor
    Being.cs Session.cs Realtime.cs Mic.cs Voice.cs Look.cs Follow.cs IHost.cs BeingSettings.cs BeingKeys.cs
    Shaders/ Points.shader Rim.shader     Data/ settings, keys (gitignored), prompt, knowledge, mesh, mats
    Audio/ listen + close cues            Editor/ BeingPrefab.cs Knowledge.cs      being.prefab
  Cosmic/                  PARKED REWORK (namespace Cosmic; Cosmic.Runtime / Cosmic.Editor; RULES.md)
                           Core/ Content/ Interaction/ UI/ Audio/ Data/(Generated) Prefabs/ Shaders/(16)
                           Scenes/main.unity — 41 runtime + 15 editor scripts; last touched for the Being port
  scenes/                  main_scene, core_systems_scene, view_scenes/{galaxy, solar_system,
                           galactic_center}_view_scene, intro_scenes/intro_earth_placement_scene,
                           nebula_scenes/ (7, not in Build Settings), development_/working_/_backup_original
  prefabs/                 Inherited prefabs (poi_<body>_prefab …), experiences/ (8 content prefabs:
                           andromeda, cosmic_web, galaxies, hd110067, pinwheel, solar_system_planets,
                           triangulum, whirlpool), ui/ (9), xr/ge_xr_rig, nebulae/, generated_bodies/
  data/                    bodies (17) moons (27) destinations (7) experiences (11) layouts (4)
                           nebula_fields (7) nebula_volumes (28) systems (2)
  scriptable_objects/      star_data_* (15 baked StarsData), Resources/ (AudioServiceProfile, hint_cards)
  shaders/ (47)  materials/  Textures/ (moons, galaxies/portraits, nebulae/domes, app_icons)
  audio/ (music 3, ambience 12, vo, sfx, ui, being)  models/moon_shapes/  ui/ (figma exports, thumbnails)
  _sources/CREDITS.md      Every downloaded or generated asset and its licence
  build_scripts/Editor/    Quest3ProjectSetup, Quest3Build, Unity6WindowsBuild, Build (UWP, legacy)
docs/                      SIMULATION_GUIDE, SYSTEM_OVERVIEW (these), BACKLOG, GDD, TECHNICAL_OVERVIEW (stale),
                           COSMIC_SIMULATION_XR_ROADMAP, COSMIC_BEING, SHADER_STEREO_AUDIT, decisions,
                           copy/ (all player-facing text), research/, store/, ui/spec.md
tools/
  mcp/                     umcp.js, compile.ps1, enter/leave_play_mode.cs, smoke*.cs, legacy/app_check.cs,
                           rework/p0…p6_*.cs
  moons/make_moon_maps.py  Procedural moon maps + fill for Charon/Triton's unimaged regions
  parity/check_data.py     Old vs rework data gate        cutover/ inventory + keep/delete lists
Builds/Quest3/CosmicSimulationXR.apk  (gitignored)
```

---

## 3. Scenes and boot

**Build Settings order:** `Assets/Cosmic/Scenes/main.unity` (see §0), then `main_scene`, `core_systems_scene`, `intro_earth_placement_scene`, `galaxy_view_scene`, `solar_system_view_scene`, `galactic_center_view_scene`.

**Boot (shipping tree):**
1. `main_scene` holds `GEManagers`: the `GalaxyExplorerManager` singleton, which picks Quest3 if `XRSettings.isDeviceActive`, else Desktop and adds `DesktopMouseInput`. It loads `core_systems_scene` additively. That scene holds:
   - the `ge_xr_rig`;
   - the `Loader` content root;
   - the `ExperienceDirector` with its module list and `startModule = milky_way`;
   - the dock, and the audio controllers.
2. `IntroFlow` + FlowManager timelines play: logo → Earth placement (`PlacementControl`, `OnboardingManager`) → solar system → galaxy. On finish, `ExperienceDirector.OpenStartModule` adopts the Milky Way the intro left open.
3. **Editor quick start:** Play with exactly one view scene open → `PlayFromViewScene` + `IntroFlow.TryQuickStart` skip the intro and open that view 2 m ahead.

---

## 4. Runtime architecture

### 4.1 Places: `ExperienceDirector` (`Assets/scripts/experience/ExperienceDirector.cs`)
- Owns `ExperienceModule[] modules`: 7 dock tiles plus destinations. It exposes `Current`, `IsSwitching`, `IsShowing`, `IntroRunning`, the static `ExperienceChanged` and `DestinationChanged` events, and `Switch(module | id)`.
- **What a switch does:**
  1. Restores everything and stops narration and ambience.
  2. Sets `EnvironmentController`.
  3. Unloads every other view scene, adopting the target if it is already open.
  4. Destroys the old prefab content and its scene panel.
  5. Loads the new content:
     - **Scene module:** `ViewLoader.LoadViewAsync`, bounded at 20 s; on failure `AbandonSwitch` falls back to passthrough and shows `SwitchNotice.FailedToOpen`.
     - **Prefab module:** instantiated under the director's own content root (hanging off the `ViewLoader`), then `FrameContent` (middle onto the root), `FitArrangement`, `BindRigFraming` and `FreePlacementAnchor.CaptureHome`.
  6. Grows the content in (0.6 s, ease-out).
  7. Shows the scene panel (`InfoPanel`, scene variant), then narration, then the ambience bed.
  8. Raises `ExperienceChanged`.
- **Scene-backed modules:** milky_way, solar_system, sagittarius_a.
- **Prefab-backed modules:** cosmic_web, galaxies, andromeda, solar_system_planets. Destinations (Kind 1) are also prefab-backed: whirlpool, pinwheel, triangulum, hd110067, and the 7 nebulae.
- **Desktop framing** (`FitArrangement`, CS-204):
  - It runs only when no headset is active and the content does not surround the viewer (a `GalaxyField` with `CentresOnViewer`).
  - It measures the content box: a `LayoutRig`'s preset slots (the destination, not the current pose), or else the renderers, excluding particles, lines, sprites and canvases.
  - In camera space it puts the box centre on the line of sight, at the one distance where the widest corner meets `desktopFitMargin` (0.95); the minimum is 0.6 m.
  - View scenes are fitted by `FitViewScene`, after GE's own zoom-in (`ZoomInOut.ZoomInIsDone`). It moves the root's first child (`AllContent`), because `TransformHandler`/`LocationView` pin the scene root every frame.
  - A layout change re-fits over the preset's transition time.
  - `KeepPanelInView` moves an off-screen scene panel to the top middle of the view.
- **Inherited view machinery:** `ViewLoader` (additive loads plus a back stack, which is no longer used for navigation). `TransitionManager` and `ZoomInOut` fade and zoom-scale view scenes; `SceneTransition.GetScalar` sizes each scene to the volume.

### 4.2 Arrangements: `ExperienceModule`, `LayoutPreset`, `LayoutRig`
- `LayoutRig` sits on arrangeable content (The Planets, HD 110067).
  - It keeps a home anchor per body; `ForceSolver.RootTransform` is that anchor. Applying a `LayoutPreset` tweens the anchors, and the solvers follow.
  - It supplies the 25 cm pull-out size and the 6 cm minimum grab sphere.
  - Bounds are capped at 0.5 m per body for framing (`FitSpanCapMetres`), so the 3 m Relative-Size Sun doesn't shrink the rest to specks.
- The dock's pop-up raises `DockController.LayoutRequested`, and the rig applies the chosen layout.

### 4.3 Touch: the force-pull and placement family
- **`ForceSolver`** (`solver_scripts/`), a state machine:
  - States: Root → Dwell (far-ray hover, tractor beam, `AttractionDwellDuration` 2 s) → Attraction → Free ↔ Manipulation.
  - On desktop, a select from Root attracts to 1 m before the camera.
  - Tap-to-home (CS-174): released within 0.4 s and moved less than 3 cm.
  - `MakeRoom` (CS-201) steps aside a free body already at the arrival spot.
  - `SetToRoot` / `Dwell` / `Attract` / `Manipulate` / `Free` events drive audio, highlighter and panels.
- **Specialisations:**
  - `PlanetForceSolver`: narration and ambience on pull, moons hidden at Root, highlighter null-guarded.
  - `MoonForceSolver`: rides its orbit anchor at rest. `ParentPlanet` is set; `FitHeldSize` normalises the held size from the moon's own mesh, since Phobos and Deimos were modelled 100×.
- **`ForceSolverFocusManager`:** one active pull per family. `SameFamily` means a planet and its moons never send each other home.
- **`FreePlacementSolver`:** released bodies stay put. They go home on `R`, Recenter, a place switch, or if they stray beyond 4 m or below the floor for 5 s.
- **`FreePlacementAnchor`:** home for non-body content (galaxies, the web, nebulae), eased back over 0.8 s.
- **`ScaleLimits`:** per-type two-hand ranges — Body 0.05–3 m, Model 0.4–4 m, Nebula 0.3–2 m, or Custom.
- **`TouchNudge`:** the brush-and-spring-back turn, added at run time to every solver and to the Being.
- **`SunTouchResponse`:** drives `_TouchBrightness` on `sun_shader` via a `MaterialPropertyBlock`.

### 4.4 Input
- **`GEInteractable`** (an `XRBaseInteractable`) turns XRI hover and select into GE focus and pointer events. `GEInputEvents.ExecuteHierarchy<T>` walks up from the hit and invokes every enabled `T` on the first object that has one (MRTK semantics).
- **`GEPointer`** wraps a hand, controller or mouse interactor.
- **`ManipulationHandler`** handles one- and two-hand move/rotate/scale. `ManipulationPointerRouter` delivers selects where no `ForceSolver` owns the object (galaxies, web, nebulae, the dock bar).
- **`XRInputRig`** provides palms (the dock's palm-up gesture), controller poses and global pointer-down.
- **`DesktopMouseInput`** (887 lines) makes the mouse a far `GEPointer`:
  - Drag on content moves it; drag on empty space orbits the camera rig; right-drag spins or pans; the wheel scales or flies.
  - It owns the key map in `SIMULATION_GUIDE.md` §8.2.
- **`UiEventSystemInstaller`** guarantees one `EventSystem` + `InputSystemUIInputModule` for the screen-space HUDs. World-space UI is hit by physics through `GEPointer`, never by the EventSystem.

### 4.5 UI
- **`InfoPanel`**: Body, Scene and Moon variants.
  - `Layout()` stacks rows from measured heights.
  - The moon variant shows MOON OF + overflow facts on the subtitle and a 2×2 grid from `BodyInfo.Stats` minus ORBITS.
  - Narration plays on open.
  - Static `TextScale` (×1/1.25/1.5) scales the whole panel.
- **`DockController`** (world): tiles built from the Kind=DockTile modules; buttons for the Being, passthrough, recenter, help and utility.
  - The drag bar is a `ManipulationHandler` hosted on the dock root, `OneHandedOnly`; `LateUpdate` re-faces the player.
  - Palm-up show/hide latches.
- **`DesktopDock`**: screen-space mirror, bottom right; hides the world dock on desktop.
- **`DockPopup`**: layout choices.
- **`UtilityWindow`**: scale slider (a collider rail, not uGUI), mute, narration mute, text size, `MicrophoneRow`, two-tap Quit.
- **`HintCards`, `SwitchNotice`**: self-building `DontDestroyOnLoad` canvases, world-space in the headset and screen-space on desktop.
- **Legacy, still live:** `PlanetInfoCard` on the orbit-scene bodies, `CardPOI` picture cards, the hand menu (About/Back/Mode/Reset), `AboutSlate`, `DesktopMenuManager`'s controls overlay (stale text, CS-089).

### 4.6 Environment and audio
- **`EnvironmentController`:** modes Passthrough / Dimmed / BlackHalo / FullBlack.
  - Dimmed is a camera-parented dim quad (`environment_tint_shader`). FullBlack is `ExperienceModeManager` VR mode + the star background.
  - The passthrough override is `PassthroughForced`; `passthroughOnStart = true` (CS-202).
  - BlackHalo is implemented but no module uses it.
- **`AudioService`:** `AudioId` → clip via `AudioServiceProfile`, pooled 3D sources. `AudioId` is append-only.
- **`VOManager`:** a single narration queue; the static `NarrationEnabled` gates it.
- **`MusicController`:** two crossfading 2D sources, one bed per environment mode (2 s). It ducks to 55 % under narration and exposes a static `Ducked` for the Being.
- **`AmbienceController`:** per-module bed, 1.5 s fade, also `Ducked`. No module has a clip yet.
- **Levels:** music 0.7, narration 0.8. The Being's `Host.Duck(true)` ducks music and ambience and stops the narrator.

---

## 5. The Cosmic Being (`Assets/Being`, ~1,700 lines, assembly `Cosmic.Companion`)

| File | Responsibility |
|---|---|
| `Being.cs` | Phases Idle / Listening / Thinking / Speaking. Press/release decides tap vs drag (0.4 s, 3 cm). Greet on summon, then a 3 s listen window. Tap rules; dismiss. Wires Voice, Mic, Look, Follow, Session. |
| `Session.cs` | The OpenAI Realtime protocol. Its `session.update` sends: PCM16 24 kHz in/out, voice `marin`, model `gpt-realtime`, `gpt-4o-transcribe` transcription in English, server VAD (threshold 0.65, 600 ms silence), and the tools `open_place`, `pull_body`, `restore`. Handles append/clear/`conversation.item.create`/`response.create`/`response.cancel`; a cancelled response is dropped by id. |
| `Realtime.cs` | One `System.Net.WebSockets.ClientWebSocket` to `wss://api.openai.com/v1/realtime`, with a Bearer header, 20 s keepalive, serialised sends, a background receive loop and a main-thread inbox pumped each frame. |
| `Mic.cs` | Unity `Microphone` → resampled 24 kHz PCM16 frames, level meter, remembered device, Android permission. Half-duplex: opens `echoTailSeconds` (0.6 s) after the Being stops speaking. |
| `Voice.cs` | Streamed `AudioClip` fed from a 30 s ring buffer through `PCMReaderCallback`. Carries an odd byte across frames. Start cushion `startBufferSeconds` 0.5; on starvation it holds silence until 0.3 s is buffered (CS-204). On finish it waits out Unity's read-ahead and the DSP buffers before stopping, so the last word is kept. Exposes loudness, four spectrum bands, and `StartLatencyMs` / `ShortReads` diagnostics. |
| `Look.cs` | Drives the point material (`_Density`, `_Inward`, `_Bands`, `_Active`, `_TouchPoint`), spin per phase, the rim glow, the press pop and the reveal fade. |
| `Follow.cs` | Keeps station in the head's yaw+pitch frame (1.4 m out, 0.32 m aside, 0.1 m down) and faces the head. |
| `IHost.cs` | The only contact with the app: `Held`, `Situation()` (place, layout, held body), `Act(name, id)`, `Duck(bool)`, `Click()`. |
| `BeingSettings.cs` | All tuning, on `Data/being_settings.asset`. |
| `BeingKeys.cs` | Key lookup: asset field → `OPENAI_API_KEY` environment variable → project-root `.env` (editor/standalone only). With no key it runs offline, with the recorded greeting. |
| `Shaders/Points.shader`, `Rim.shader` | The body (voice bands deepen an inward fall only) and the rim; stereo macros, target 3.5. |
| `Editor/BeingPrefab.cs` | **Cosmic Simulation/Build Being**: the prefab, synthesised cue WAVs, dock wiring. |
| `Editor/Knowledge.cs` | **Build Being Knowledge**: places and bodies → `being_knowledge.txt`, sent as instructions. |

**Host adapters:** `Assets/scripts/being/Host.cs` (shipping) and `Assets/Cosmic/Being/Host.cs` (rework). Each adds `GEInteractable`, `ManipulationHandler` and `TouchNudge` at summon; maps the tools onto `ExperienceDirector.Switch`, the first `LayoutRig`'s pull, and restore; and implements ducking. `Host.Toggle` keeps `Current` set through the fade.

**Measured (editor, 27 Sep):** the greeting's first audio played 175 ms after its first chunk with 0 short reads (3 before CS-204); a three-sentence answer took 182 ms with 1 rebuffer.

---

## 6. Data model

| Type | Fields |
|---|---|
| `BodyInfo` (SO, `Assets/data/bodies`, `moons`) | Id, DisplayName, Subtitle, Paragraph, `Stat[]`, `Moons[]`, `Orbits`, Narration, Ambience; `IsMoon` |
| `Stat` | Label, Value, Unit, Exponent; `ToRichText()` renders the exponent as `<sup>` |
| `ExperienceModule` (SO, `Assets/data/experiences`, `destinations`) | Id, Kind (DockTile / Destination), DisplayName, SecondLine, DockThumbnail, SceneName, ContentPrefab, Environment, Panel (`ScenePanelCopy`: Title, Paragraphs, Instruction), `Layouts[]`, Narration, Ambience, `Places[]`; `HasLayoutChoice` |
| `LayoutPreset` (SO, `Assets/data/layouts`) | Id, DisplayName, SecondLine, TransitionSeconds (0.8), `LayoutSlot[]` (BodyId, LocalPosition, LocalEuler, Scale = diameter in metres); `Find(id)` |
| Moon specs | `SolarMoonBuilder.Spec` (Planet, Id, Rig, Km, Days, OrbitPlanetRadii, OrbitRadius, TiltDegrees; 26 entries, orbits re-laid each run). `MoonBuilder.Spec` for the Planets view (largest moon 7 cm, 3 cm floor, square-root-compressed periods). `MoonShapes` (deformed sphere meshes for Nix, Hydra, Kerberos, Styx at `Assets/models/moon_shapes/`) |

**Moon stats** (26 non-Earth moons, 27 Sep): ORBITS, DIAMETER, ONE ORBIT, DISTANCE FROM \<PLANET\>, SURFACE GRAVITY, DISCOVERED. Every moon has a two-sentence paragraph. Copy source: `docs/copy/moons.md`.

---

## 7. Content pipelines

**Editor menu "Cosmic Simulation/":**

| Group | Items |
|---|---|
| Quest 3 | Configure Project · Build APK · Apply Android Asset Overrides · List OpenXR Features |
| Verify | App Check (acceptance walk with real mouse events → `Logs/app_check.log`) · Tag Probe |
| Being | Build Being · Build Being Knowledge |
| Copy & wiring | Import Copy (`docs/copy/*.md` → ScriptableObjects) · Wire Experiences · Install Runtime Systems · Wire Narration · Wire Scene Panel |
| UI | Build UI Prefabs · Build Desktop Dock · Build Dock Thumbnails · Reimport UI Sprites · Build Hint Cards · Retire / Report Legacy Menus |
| Layouts & systems | Build Layout Presets · Build System Layouts · Build System Profiles · Check Star Colour Model · Build Solar Row Content · Build System Content |
| Bodies & moons | Build Moons (Planets view) · **Build Solar System Moons** (orbit scene; run Build Moons first) · Build Generic Bodies · Build Exoplanet Content (HD 110067) · Build Atmosphere Materials / Shells · Reimport Astronomy Textures |
| Galaxies | Build All Galaxy Content · Build Galaxy Modules (M51, M101, M33) · Build Galaxies Content (deep field) · Build Galaxy Pins / Choices / Bar / Portraits · Build Andromeda Content · Preview Galaxies |
| Cosmic Web | Build Cosmic Web Content |
| Nebulae | Build Nebula Prefabs / Scenes / Fields / Skies / Volumes · Remove Nebula Volumes · Attach / Remove Place Shells |

The rework has its own **Cosmic/** menu (Build Rig, Main Scene, UI, Layouts, Bodies, Places, Nebulae, Galaxies, Cosmic Web, Bake All; Verify P0–P6).

**Offline tools:**
- `tools/moons/make_moon_maps.py`:
  - Makes 1024×512 sphere-space height-field maps (no seam, no pinched poles) for the 11 moons without global maps, shaded and tinted to each moon's measured albedo.
  - `patch()` downloads the USGS Charon and Triton mosaics and fills their unimaged regions. The fill is fine mottling and craters, levelled per channel to the photographed strip along the seam, and feathered outward; small dark patches inside the photographed area are left alone.
- `tools/parity/check_data.py`: the old-vs-rework data gate.
- `tools/cutover/`: rework cutover inventory.

**Sources and credits** (`Assets/_sources/CREDITS.md`, required for every asset):
- NASA/ESA/USGS imagery (USGS maps for Triton, Charon, Rhea, Dione, Tethys are public domain).
- Generated moon maps, labelled "not observations".
- Higgsfield images for the nebula sky domes and the deep-sky panorama.
- Higgsfield `seed_audio` voice "Holden" for new narration and the Being greeting. These clips are 24 kHz against the originals' 44.1 kHz, a known gap.
- The Being's cues are synthesised in code.

---

## 8. Rendering

| Subject | How |
|---|---|
| Galaxies (Milky Way, Andromeda, M51, M101, M33) | `SpiralGalaxy` bakes `StarsData`; `DrawStars` draws each layer (clouds, dust/negative, stars) with a per-camera `CommandBuffer.DrawProcedural` from a ComputeBuffer, six vertices a quad (`cginc/StarQuad.cginc`, no geometry shaders). Shaders `spiral_stars_shader`, `_cloud_`, `_negative_`. |
| Cosmic Web | `CosmicWebGenerator`: a jittered Voronoi tessellation, 120,000 points projected onto sheets, filaments and nodes, deterministic and rebuilt at run time in 3 ms slices. `CosmicWebRenderer` draws them through the same point path with `cosmic_web_points_shader` (additive; violet ramp from HDR colours; drift via `_Age`). |
| Galaxies deep field | `GalaxyField`: instanced billboards with `galaxy_sprite_shader`, centred on the head. |
| Planets and Sun | `planet_*` family (earth, saturn, rings, clouds, atmosphere rim, alpha, low-res) with `_SunDirection` from `SunLightReceiver`; `sun_shader` (`_TouchBrightness`), `sun_solar_flare_shader`, `lens_flare_shader`, `halo_shader`. |
| Black hole | `black_hole_gravitational_lensing_disc_optimized_shader` (ray-marched disc; alpha follows brightness) + glow card. |
| Nebulae | `place_shell_shader` (head-locked ~60 m sky + ~7 m room-fixed gas shell), `nebula_volume_shader` / `_dust_` (~28k baked points), `nebula_star_shader`, `nebula_field_shader` (marched, off by default), `nebula_card_shader`. |
| Orbits | `OrbitalTrail` command buffers. |
| Room | `environment_tint_shader` (dim quad and halo). |
| The Being | `Points.shader` (a 20 cm point cloud with voice-driven inward fall), `Rim.shader`. |

**Rules:**
- Every custom shader carries the stereo instancing macros.
- No geometry shaders; target at most 4.5.
- Alpha output must be honest, because passthrough composites on it.

**Budgets (Quest 3, 72 Hz, ≥ 60 fps):** ≤ 150 draw calls, ≤ 400k point sprites, ≤ 500 billboards, textures ≤ 2048 ASTC, one dim quad, ≤ 3 world canvases, no realtime shadows. *Not yet measured on device (CS-081, CS-149); whether BiRP's instancing applies to the injected command buffers is open (CS-118).*

---

## 9. Build and test

- **APK:** **Cosmic Simulation → Quest 3 → Build APK**, or batch `-executeMethod GalaxyExplorer.Build.Quest3Build.BuildApk`. It runs Configure Project first, gates on the legal notice files and writes `Builds/Quest3/CosmicSimulationXR.apk`. An Android build raises an "Unsupported Input Handling" modal; answer *Ignore*.
- **Windows:** `Unity6WindowsBuild.BuildWindows64` (Mono) writes `Builds/Win64/CosmicSimulationXR.exe`.
- **Editor automation** (`tools/mcp`, through the Unity AI Assistant relay; one client at a time):
  - Commands: `node tools/mcp/umcp.js list | call | run <file.cs>`.
  - `compile.ps1` refreshes, waits and reads compiler errors from `Logs/Editor.log`. It refuses in Play mode, so leave with `leave_play_mode.cs`.
  - Enter Play by setting `EditorApplication.isPlaying = true`.
  - The runner reports NOT-OK on any warning, and forbids `System.Reflection`, delete and move.
- **Verification:**
  - **App Check** (`Assets/scripts/Editor/AppCheck.cs`, run in Play on `main_scene`). Last run `[APP] DONE 34/36`; the only non-Being failure is the Milky Way tag step.
  - `smoke.cs` is the legacy walk; the rework has its P0–P6 harnesses.
  - There are no EditMode/PlayMode test assemblies.
- **Working rules:**
  - Revert runtime-leaked material changes before committing (`_SunDirection` on the moon and planet materials, `QualitySettings.asset`, `GalaxyExplorer.slnx`).
  - Every change gets a BACKLOG row with its evidence.
  - Every asset gets a CREDITS row.
  - Keys stay project-local, never global.

---

## 10. Known issues and open work

| Area | Item |
|---|---|
| **Ship blockers** | Build Settings boots the rework (§0). The API key is inside the APK (§0). No Quest pass of anything since 27 Sep: performance, per-eye rendering, the Being's `ClientWebSocket` under IL2CPP. |
| Being (CS-200) | One rebuffer mid-answer on long replies; a `ZoomInOut` MissingReferenceException on very quick consecutive switches; `COSMIC_BEING.md` says the listen window is 6 s (it is 3). |
| Places | The Solar System and Galactic Center models can't be resized though their panels say so (CS-045). HD 110067 is reachable only by voice. The Galaxy bar is screen-space only. `NebulaOverlay`, `OpenDestination` and the BlackHalo mode are dead code. The orbit-scene POIs keep legacy `SceneToLoad` targets not in Build Settings. |
| UX | Hint cards not shown on first run (CS-074). Controls overlay text out of date (CS-089). Hand menu still has Back (CS-111). About has no version text (CS-114). Passthrough starts forced on, hiding the dark places' own lighting. |
| Audio | No place ambience beds, grow-in whoosh or Sun rumble (CS-077). New narration is 24 kHz. |
| Rendering | Galaxy re-bake (CS-191). Stereo instancing of command buffers (CS-118). |
| Interaction | A desktop click on the Milky Way's Solar System marker can hit Saturn behind it (CS-195). Moon colliders registered by two interactables (CS-167, blocked). |
| Docs | `TECHNICAL_OVERVIEW.md` and parts of `GDD.md` are stale: they still describe the Schematic/Realistic pop-up, six tags, nebula halo overlays and four nebulae. |
