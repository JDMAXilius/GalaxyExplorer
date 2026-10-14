# Code audit and cleanup plan

*14 Oct 2026, cloud session, `main` at `27d6eb5`. Read from source by four passes: measurements over the whole
tree, then three independent readers (shipping runtime; dead and legacy code, traced by asset GUID from the six
build scenes; editor tooling, build and tests). Every claim below has a file and line in the pass reports; the
load-bearing ones were re-checked by hand before they went in here. Nothing was compiled: there is no editor in
the cloud session.*

The goal the owner set: Unity-native, best practice, sound fundamentals, and **less is more** - only the code,
files and features the app needs, each written once.

## 1. Verdict

The newest code is the standard to copy: `Assets/Being` (its own assembly, an `IHost` interface, 1 % comments,
thread-safe hand-off to the main thread), the ScriptableObject data model (`ExperienceModule`, `BodyInfo`,
`LayoutPreset`), `EnvironmentController` (one owner of the room, events only on change) and the switch routine
(bounded waits, `try/finally`). The weight is everywhere else: **three UI stacks run at once**, **every system
finds every other system by searching the scene**, a **24k-line touch library** is shipped to move two
transforms, a **12.6k-line parked rework** compiles into the APK, **four test harnesses and no tests**, and about
**1,500 lines of comments that are history**, not explanation.

## 2. Numbers

| Measure | Now | After the plan (estimate) |
|---|---|---|
| Our C# (`Assets/scripts`, `Being`, `build_scripts`) | 263 files, 59.6k lines | about 42k |
| Vendored C# compiled with ours (TouchScript, DefaultPlayables, FlowManager, Layers) | about 28k lines | about 2k (FlowManager, Layers) |
| Parked rework `Assets/Cosmic` | 55 files, 12.6k lines, 73 MB | 0 (after porting three pieces) |
| Scene searches in runtime code (`Find*ObjectsByType`, `GameObject.Find`) | 102 sites | under 10, none per frame |
| Static singletons / single-slot `Func` hooks | 14 / 3, four singleton styles | about 5 services, one style |
| Comment lines in runtime scope | 14.8 % (4,120), about 40 % of them history | under 7 %, only the *why* |
| Assemblies for our code | 1 (`Assembly-CSharp`, plus `Being`) | 5 (third party, app, editor, kit, tests) |
| Automated tests (Unity Test Framework is installed) | 0 | EditMode + a few PlayMode |
| Repository | 1.1 GB, no LFS, three zips under `docs/` | LFS for binaries; zips out |

## 3. What we do right (keep it, copy it)

- **Data as assets.** Places, bodies, layouts and settings are ScriptableObjects; builders write prefabs from
  them; `Quest3ProjectSetup` writes player settings from constants.
- **Small, decoupled modules.** `Assets/Being` depends on nothing in the app; each app hands it an `IHost`.
- **One owner per concern where it has been done:** `EnvironmentController` for the room, `AudioService` (pooled,
  behind an interface) for playback, `ForceSolver.Bodies` and `ManipulationHandler.Active` as registries.
- **MaterialPropertyBlock** for per-object looks (GalaxyField, PlaceShell, HandLook, Being `Look`).
- **Deterministic generation:** seeded RNG structs, O(n) Fibonacci-sphere placement, baked meshes.
- **Reproducible builds:** signing restored in `finally`, the manifest from an `IAndroidManifestRequirementProvider`,
  the key guard, the legal-notice check.
- **Balanced subscriptions** (`LayoutRig.cs:215-217` is the model) and memoised per-frame queries
  (`UiEventSystemInstaller.IsPointerOverScreenSpaceUi`).
- **The rework's builder pattern** (`Assets/Cosmic/Editor/Content.cs`): build in a preview scene, typed setters,
  a report of what was missing. The seed of the editor kit below.

## 4. What we do wrong

Ranked by what it costs. `S/M/L` is effort.

| # | Problem | Evidence | Fix | Effort |
|---|---|---|---|---|
| W1 | **Three UI stacks live at once.** UI 2.0 hides the legacy docks every `LateUpdate`; the hidden `DockController` still polls the scene every 0.5 s and still opens the legacy settings window on `U`; `HintCards` (1,024 lines) is reachable only from the hidden docks | `Ui2Shell.cs:318-331`, `DockController.cs:847-884` | Finish the cutover: keep a small `DockAnchor`, delete the legacy UI (§6 R4) | M |
| W2 | **Systems find each other by scanning the scene.** 102 `Find*` sites; per-frame ones in the intro gate (`IntroComplete` searches every frame once no intro exists) and the selection poll | `ExperienceDirector.cs:222-233,291`, `Ui2Explore.cs:218,267-335` | `Registry<T>` filled in `OnEnable`/`OnDisable`; resolve once | M |
| W3 | **State inferred by polling, not raised as events.** The 2D card polls the legacy panels every 0.2 s, `InfoPanel` polls `ForceSolver` every frame, settings poll `PlayerPrefs` and `Microphone.devices` every frame (JNI on Android) | `Ui2Explore.cs:306-311`, `InfoPanel.cs:376-415`, `Ui2Settings.cs:215-226,278`, `Ui2Guide.cs:163-170`, `UtilityWindow.cs:153` | `ForceSolver.StateChanged`, a `Selection` service, a cached `Settings` service with a `Changed` event | M |
| W4 | **A material leak.** `renderer.materials` copies every pulled body's materials and never frees them (and breaks batching) | `Ui2Explore.cs:382-400` | Fix depth in the body shaders, or one cached copy per shared material destroyed on teardown | S |
| W5 | **The same job written several times, already disagreeing.** Restore ×6, notices ×3, Escape handling ×4 (one on the legacy `Input` API), microphone meter ×3 (one mishandles the buffer wrap), lazy `Resolve()` singletons ×3, `BorrowFont` ×2 | `DesktopMouseInput.cs:481`, `Ui2Settings.cs:183`, `ExperienceDirector.cs:1563`, `DockController.cs:261,396`, `MicrophoneRow.cs:173`, `Ui2Settings.cs:288` | One `Arrangement.Restore`, one `Notices`, one input action map, one meter | M |
| W6 | **God classes.** `ExperienceDirector` 1,577 lines with six jobs; `Ui2Explore` 1,328 with eight; `DockController` 1,020; `DesktopMouseInput` 887 | the runtime pass | Split along the seams in §6 R3 | M–L |
| W7 | **Vendored weight.** TouchScript (23k lines, 11 MB, four always-included shaders) only lends `CameraController.Pivot`/`EntityToMove`, and it is why input handling is "Both" (the build modal) | `GalaxyExplorerManager.cs:6,67,402`, `TransitionManager.cs:622,883`, `DesktopMouseInput.cs:721,878`, `core_systems_scene` | A 10-line `ViewPivot`; delete TouchScript; Input System only | M |
| W8 | **The parked rework ships.** `Cosmic.Runtime.asmdef` has no platform or define limit, and *Cosmic → Build → Main Scene* re-enables its scene at build index 0, which would make the APK boot the rework | `Assets/Cosmic/Cosmic.Runtime.asmdef`, `Quest3Build.cs:123` | Port three pieces, then delete it (owner decision); until then a `COSMIC_REWORK` define constraint and a scene-0 guard | S |
| W9 | **No tests; four overlapping harnesses.** UTF 1.8 installed, zero tests. `AppCheck` fails with UI 2.0 on; `Ui2SelfCheck` ships in the player; `tools/mcp/smoke.cs` now runs the parked walk; `Cosmic/Editor/Verify.cs` (2,709 lines) re-implements the test runner | the tooling pass | UTF EditMode + PlayMode; retire the rest | M |
| W10 | **Editor builders repeat themselves and dirty scenes.** `Rect()` ×5, load-or-create material ×12, `SerializedObject` setters ×8, a Markdown parser copied three times, three corner formulas for the same nine-slice sprites; builders create in the active scene with no `finally`; UI 2.0 depends on the legacy builder it replaces (`UiPrefabBuilder.Font`) | `Ui2Kit.cs:25`, `Ui2Builder.cs:187`, `CopyImporter.cs:88` = `HintCardBuilder.cs:128` | One `EditorKit` (§6 R6) | M |
| W11 | **Latent bugs found on the way.** `GetComponent<T>() ?? AddComponent<T>()` never adds in the editor (Unity's fake null); the Android asset-override menu scans `Assets/SolarSystem` and `Assets/UI`, which do not exist; seeds from `string.GetHashCode()`; a comment saying shaders need no stereo macros | `ExperienceWiring.cs:155,156,233`, `Quest3ProjectSetup.cs:324,99-101`, `ThumbnailBuilder.cs:287` | Fix each (§6 R0) | S |
| W12 | **No assembly boundaries.** 38k lines of app and 28k of vendored code recompile on every edit; nothing stops input code depending on UI (`PalmPause` reaches into `Ui2Explore`) | no asmdef under `Assets/scripts` | Assemblies with one-way references (§6 R2) | M |
| W13 | **Comments as a diary.** About 1,500 lines of "used to", dates and ticket stories inside code | `DesktopMouseInput.cs:142-152,213-223`, `ExperienceDirector.cs:152-167` | Keep the *why* in one or two lines; history lives in git and `docs/` | S, ongoing |
| W14 | **Licence and repo hygiene.** The proprietary Segoe UI font (`seguisb SDF`) ships in three legacy UI surfaces; 1.1 GB of history with large TGAs and no LFS; zips committed under `docs/` | `menu_managers.prefab`, `hand_menu_offset.prefab`, `intro_earth_placement_scene` | Swap to Selawik (already in the project); LFS for new binaries; zips to releases | S |

## 5. The rules from here on

1. **One owner per concern.** A thing is decided in one place and announced with a C# event; nobody polls it.
2. **Find nothing at run time.** Components register themselves (`Registry<T>`); references are serialized or
   resolved once. No `Find*` in `Update`, `LateUpdate`, coroutines or `InvokeRepeating`.
3. **No allocation per frame.** No LINQ, closures, `ToArray`, `renderer.materials` or `Microphone.devices` on hot paths.
4. **Data, not code, for content.** Copy lives in `docs/copy` and comes in through `CopyImporter`; tuning lives
   on ScriptableObjects.
5. **Assemblies point one way:** third party ← app input ← app experience ← app UI; editor and tests on top.
6. **Every builder is idempotent:** preview scene, `try/finally`, a second run leaves no diff.
7. **A comment says why, in two lines at most.** History goes in the commit message.
8. **Test what has logic.** Pure functions get EditMode tests; flows get one PlayMode test each.
9. **Delete when replaced.** No tombstones, no parked copies, no disabled-but-shipped code.

## 6. The plan

Waves, in order. Each wave compiles and runs on its own. **CC** is the cloud session (code), **TERM** the terminal
(editor, scenes, device), **OWNER** a decision. Ticket numbers **CS-330 to CS-359** are reserved for this plan.

### R0 — Fix what is broken now (CC, small, no design change)

| Ticket | Goal | Objectives | Removes |
|---|---|---|---|
| CS-330 | No material leak on pulled bodies | Replace `renderer.materials` in `Ui2Explore.KeepPulledInFront` with one cached front copy per shared material, destroyed on restore and teardown; then check whether a shader depth fix removes the need | a leak |
| CS-331 | No scene search per frame for the intro | `IntroFlow` registers with the director on `Awake`; the director marks the intro finished once when none exists; drop the 45 s watchdog if `IntroFlow` always raises its end event | about 60 lines |
| CS-332 | `U` does not open the legacy window under UI 2.0 | `DockController.WatchKeyboard` and its 0.5 s poll stand down while `UiTheme.Enabled`; `Ui2Shell` Escape moves to the Input System | — |
| CS-333 | Builders add what is missing | `ExperienceWiring` `?? AddComponent` → `TryGetComponent`; `Seed.Of(id)` (FNV-1a) replaces `string.GetHashCode()` seeds; the asset-override menu's folder list fixed or the menu replaced by the postprocessors (R6) | — |
| CS-334 | The rework cannot ship by accident | `defineConstraints: ["COSMIC_REWORK"]` on both Cosmic asmdefs; both builds refuse when build scene 0 is not `main_scene` | — |
| CS-335 | Test tools stay out of the player | `Ui2SelfCheck` and `FreeLook` behind `UNITY_EDITOR \|\| DEVELOPMENT_BUILD` (or a dev assembly) | 400 lines from the APK |
| CS-336 | Hot paths stop allocating | `ForceSolver` LINQ averages → loops; `GEInputEvents.ExecuteHierarchy` pooled list; `TryGetViewPivot` cached at switch; `HandLook` stops rescanning once both hands are found; `XRInputRig` stops polling subsystems on desktop; obsolete `FindObjectOfType` calls replaced | — |

### R1 — Take out the dead weight (CC deletes, TERM confirms a compile)

| Ticket | Goal | Objectives | Removes |
|---|---|---|---|
| CS-337 | Delete what nothing uses | `DefaultPlayables`, `RotationPlayable`, NuGet files and `.nupkg`, the three tombstones, `Build.cs` (WSA), `LabelTheme` | about 3.5k lines, 50 files |
| CS-338 | Delete the scenes outside the build and what only they use | `development_scenes`, `working_scenes`, `projector_poi_prototype`, `onboarding_sprites_scene` with `ForceTractorShaderTester`, `VisualProjector` and their own models and materials | about 300 lines, dozens of assets |
| CS-339 | Strip the HoloLens era | `WorldAnchorHandler`'s `UNITY_WSA` blocks; `PlatformId.HoloLensGen1`/`Phone` arms; two unused HoloLens voice clips | about 200 lines |
| CS-340 | Trim the samples to what the rig uses | Delete the demo scenes and `DemoAssets` folders the rig never references, one folder per commit with a compile between (TERM) | about 190 files, 27 MB |

### R2 — Foundations the rest builds on (CC)

| Ticket | Goal | Objectives |
|---|---|---|
| CS-341 | Assemblies with one-way references | asmdefs for the vendored folders first (an asmdef cannot reference Assembly-CSharp), then `Cosmic.App` (runtime), `Cosmic.App.Editor`, `Cosmic.EditorKit`, `Cosmic.Tests.EditMode/PlayMode`. Relay scripts that name app types become menu-item wrappers |
| CS-342 | Components announce themselves | `Registry<T>` (static list, `OnEnable`/`OnDisable`) for `ForceSolver`, `LayoutRig`, `FreePlacementAnchor`, `InfoPanel`, `VOManager`; replace the scene scans in scope |
| CS-343 | State changes are events | One `ForceSolver.SetState` raising `StateChanged`; a `Selection` service (pulled bodies, selected body, preview place) fed by it and by markers; `Ui2Explore.Track` and `InfoPanel`'s poll removed; `PlanetPOI.Preview`/`Picked` hooks become `Selection` calls |
| CS-344 | One settings store | Port the rework's `Prefs` as `Settings`: cached mute, narration, text scale, passthrough, microphone, with `Changed`; device list re-read on page open and on focus; replaces `UtilityWindow.Muted` and the per-frame reads |
| CS-345 | One of each shared service | `Notices.Show`, `Arrangement.Restore(immediate)`, one microphone meter (the correct wrap), one input action asset for the GDD 5.3 keys dispatched by `DesktopShortcuts` |
| CS-346 | Tests exist | EditMode: star colour table, layout ring factor, galaxy profile conversions, version code, copy parsing, `Seed.Of`, `PlanetPOI.BodyIds`, the palm-pose predicate (extracted as a pure function), two-hand scale maths, the narration queue policy, every shader has stereo macros and no geometry stage, build scene 0 is `main_scene`, every body id has data. PlayMode (desktop, XR off): boot and skip the intro, the rail and tiles, a marker click, palm pause via `N`. The relay runs them through `TestRunnerApi` |

### R3 — Split the large classes (CC, after R2)

| Ticket | Goal | Objectives |
|---|---|---|
| CS-347 | `ExperienceDirector` does switching only | Out: `IntroGate`, `ContentFramer` (static), `PlacePanelPresenter` (subscribes), `DestinationController`; about 400 lines remain |
| CS-348 | `Ui2Explore` is a view | Out: `Ui2Picker`, `Ui2Card`, `CompareMode`, Listen through `AudioService` (`HoldNarration`); selection from the service; parallel photo arrays become one serialized array |
| CS-349 | `DesktopMouseInput` has three parts | `DesktopPointer` (ray, hover, press), `DesktopCameraRig` (orbit, pan, zoom, home), `DesktopShortcuts` |

### R4 — The UI 2.0 cutover (TERM walkthrough first, then CC, then TERM)

| Ticket | Goal | Objectives | Removes |
|---|---|---|---|
| CS-350 | Keep what UI 2.0 still needs | Extract `DockAnchor` (pose, recentre, drag) and a static `Choices` (layouts, places) from `DockController`; move `beingPrefab` (the dock prefab is the only thing that puts the guide in the build) and the stored-mute boot hook into `Settings`; `Ui2Kit` stops calling `UiPrefabBuilder.Font` | — |
| CS-351 | Delete the legacy UI | 18 scripts (DockTile, DockPopup, DesktopDock, UtilityWindow, MicrophoneRow, GalaxyBar, DesktopMenuManager, GGVMenuManager, GlobalMenuManager, ToolPanel, AboutSlate, Hyperlink, UiWorldPreview, PlanetPreviewController, their builders), 13 prefabs, `HintCards` and `SwitchNotice`'s own canvas, `AppCheck`; `UiPrefabBuilder` keeps only the info panel and label button | about 5.5k lines |
| CS-352 | The palm menu decided | OWNER: keep it restyled on `Ui2Kit`, or retire it now that the open-palm pause and the shell exist. It depends on `GlobalMenuManager`, so it is decoupled either way |

### R5 — Take TouchScript out (CC + TERM)

| Ticket | Goal | Objectives | Removes |
|---|---|---|---|
| CS-353 | No touch library | `ViewPivot` (the two transforms) replaces `CameraController` in three files; the terminal swaps the component in `core_systems_scene`, deletes the gestures and `TouchManager`, drops four always-included shaders; delete the folder | 23k lines, 11 MB |
| CS-354 | Input System only | Move the last `UnityEngine.Input` reads (`Ui2Shell`, `MouseInputEventRouter`) and set Active Input Handling to the Input System: the "Unsupported Input Handling" build modal goes away (closes CS-100) | — |

### R6 — Tooling (CC)

| Ticket | Goal | Objectives | Removes |
|---|---|---|---|
| CS-355 | One editor kit | `Assets.LoadOrCreate/Material/Replace/WritePng`, `Prefabs.Create/Edit` (preview scene, `finally`), `Wire.Set` (a missing field fails), `Ui.Box/Image/Text` (one corner formula), `Copy`, `Seed`; every builder moves onto it | about 1k lines |
| CS-356 | One button rebuilds everything | `[Builder(order)]` + **Rebuild All** via `TypeCache`; the solar row runs its own moon pass; versioned `AssetPostprocessor`s replace the three reimport and override menus; menus regrouped under `Build/`, `Wire/`, `Quest 3/`, `Windows/` | menu sprawl |
| CS-357 | One build entry point | `CosmicBuild.Run(profile)` on Unity 6 Build Profiles: target, scenes and defines per profile; constants, signing, key and notice guards, scene-0 check and `build_info` in one place | about 150 lines |

### R7 — Comments and repository (CC, ongoing)

| Ticket | Goal | Objectives |
|---|---|---|
| CS-358 | Comments say why | Each file touched in R0–R6 loses its history comments; one pass over the rest, worst first (`ExperienceDirector`, `DockController`, `DesktopMouseInput`, `ManipulationPointerRouter`, `StarColor`) |
| CS-359 | A lighter repository | Swap `seguisb SDF` for Selawik where it still ships (licence); Git LFS for new binaries; release zips out of `docs/`; decide whether to rewrite history (OWNER: it changes every clone) |

### Owner decisions

1. **Delete the parked rework `Assets/Cosmic`?** Recommended yes, after porting `Prefs` (CS-344), the in-app
   licence page from `Cosmic/UI/About.cs`, and its "lost object comes home after 5 s" rule. Frees 12.6k lines,
   73 MB, and orphans 126 MB of TGAs.
2. **The palm menu** (CS-352): restyle or retire.
3. **The nebula working scenes** you use in the editor: keep them (they are outside the build), or let them go
   with their five scripts.
4. **Heavy unused assets** (about 340 MB): the 67 MB Jupiter animation PNG, nebula volume data, `3DTile.glb`,
   unused fonts.
5. **Repository history:** LFS from now on only, or a one-off rewrite.

## 7. Order and size

R0 now (a day, cloud). R1 next (a day, cloud, one terminal compile). R2 and R3 together (about a week, cloud,
with terminal play-mode checks). R4 after the owner's headset walkthrough of UI 2.0. R5 any time after R1. R6 in
parallel with R3. R7 rides along. Expected result: about 40 % less of our own code, about 28k fewer vendored
lines, no per-frame scene searches, no polling between systems, tests that run without the relay, and one way
to do each thing.
