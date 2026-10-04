# Roadmap 3 of 3 — Publishing on Steam (PC, and PC VR through the Quest)

*Written 4 Oct 2026, expanded the same day, from the repository (`main` at `f18fa1a`) and current Steamworks
documentation (Sources at the end; `partner.steamgames.com` could not be opened from the writing session, so
figures come from search excerpts of those pages and from recent third-party guides, and every duration and fee
must be re-read in the Steamworks dashboard when the step is done). Separate from the Quest testing roadmap and
the Meta store roadmap on purpose; the only shared piece is the being's token endpoint, built once (Meta M2) and
reused here.*

Track tags: **[OWNER]** account, money, decision; **[CC]** code; **[TERM]** editor, build, headset. Tickets
**CS-250 to CS-269** are reserved here and are proposals until they are backlog rows. Each ticket has a **Goal**,
atomic **Objectives**, **Needs**, **Evidence** and **If it fails**.

## 0. The question first: can Steam do VR, even for a Meta Quest?

**Yes, as PC VR.** Steam sells VR apps that run on a Windows PC. A Quest owner plays them with the headset acting
as the PC's display, either through Valve's **Steam Link** app on the Quest (wireless; SteamVR is the OpenXR
runtime) or through Meta's **Quest Link / Air Link** (cable or wireless; Meta's runtime). Our Windows build is
already an OpenXR app (`Quest3ProjectSetup` configures OpenXR for Standalone, Multi Pass), so the same executable
runs under both runtimes.

| Feature | Steam Link (SteamVR) | Quest Link (Meta runtime) |
|---|---|---|
| Head and controller tracking | Yes | Yes |
| **Hand tracking** | Yes since SteamVR 2.8: the Quest's hands are forwarded as `XR_EXT_hand_tracking`, which is what Unity's XR Hands feature uses | Yes |
| **Passthrough** | **No.** SteamVR has no passthrough extension for apps | **Developer-only.** Meta enables it over Link behind developer-mode toggles and does not support it for released apps |
| Microphone (the being) | Yes, forwarded as a PC audio device | Yes |

So on Steam the app is **full-VR by default** (the dark sky), with the passthrough toggle hidden when the runtime
cannot do it, and hands work. The standalone Quest APK is **not** something Steam distributes; that is Meta's store.
A later option: Valve's **Steam Frame** headset runs native ARM64 Android APKs as well as streamed PC titles, with
Unity + OpenXR + IL2CPP + ARM64 + Vulkan as the recommended configuration, which is our Android build. Optional
phase at the end, not v1.

Steam also lists **non-VR** apps, and this app has a desktop mouse-and-keyboard mode the project has always kept
working. The recommended product shape is **"VR Supported"**: playable flat on any PC, and in VR with a headset.
"VR Only" would turn away the flat players for nothing.

## 1. Where the Steam work stands today

- **A Windows build script exists, Mono, batch-only.** `Unity6WindowsBuild.BuildWindows64` writes
  `Builds/Win64/CosmicSimulationXR.exe`; Mono2x because only the Mono module is installed on the build machine;
  reuses `AppVersion`, ships the legal notices. No menu item.
- **VR or desktop is decided by whether a headset is active**, not by a flag. `XRSettings.isDeviceActive` is read in
  `GalaxyExplorerManager` (sets `Platform`), `XRInputRig`, `DesktopMouseInput`, `DockController`, `UtilityWindow`,
  `ExperienceDirector`, `DrawStars`. XR initialises on start for Standalone (`m_InitManagerOnStart: 1`), and no code
  calls `XRGeneralSettings`/`InitializeLoader` manually. On a PC with SteamVR installed and the headset off, start-up
  can launch SteamVR or stall; Steam's convention is launch options the player chooses from.
- **Only Meta profiles are enabled on Standalone.** `CommonFeatures` turns on Oculus Touch, Touch Plus, hand
  interaction and hand tracking; Valve Index, HTC Vive and Windows MR controller profiles are off
  (`com.unity.openxr.feature.input.valveindex`, `htcvive`, `microsoftmotioncontroller` are `m_enabled: 0`).
- **No Steamworks integration, none required.** The Steam overlay, achievements and cloud saves are optional; the
  app has no save data.
- **The being's key problem is the same as on Meta.** `being_keys.asset` must not ship; the token endpoint (Meta
  M2, CS-232/233) serves both builds. The settings window already has a microphone row (`MicrophoneRow.cs`).
- **No Steam store assets or copy.** `docs/store/STORE_LISTING.md` is written for the Quest listing and says the
  desktop mode "is not what ships". For Steam the desktop mode is half the product.

## 2. What "published" means (exit criteria)

| # | Criterion |
|---|---|
| S1 | Live Steam store page, "VR Supported", Windows, our own art and copy, correct VR fields (headsets, input, play area) |
| S2 | Two launch options that work from a cold Steam client: **Play on the desktop** (no XR init) and **Play in VR** (OpenXR), the second verified on SteamVR + Steam Link and on Quest Link |
| S3 | Hands work in VR through Steam Link; controllers work on Index, Vive and Touch; passthrough controls hidden on PC VR |
| S4 | No key in the build; the being uses the token endpoint or is off |
| S5 | Store page and build review passed; both waiting periods served |

## 3. Decisions the owner has to make first

| # | Decision | Recommendation | What changes if not |
|---|---|---|---|
| D1 | Price | Match the Meta price (free for v1 if Meta is free); the Steam Direct fee is $100 per app, refunded after $1,000 gross | — |
| D2 | Being on Steam at launch | Yes, through the shared endpoint, once Meta M2 exists | Without it: the being is off in the Steam build (`BeingSettings` flag), and the mic row stays for later |
| D3 | IL2CPP or Mono | Mono for the first upload (what the machine can build); IL2CPP before release if the Windows IL2CPP module is installed by then; no code changes either way | Mono binaries are easier to decompile; irrelevant with no secrets in the build |
| D4 | Steam Frame native build | Not v1 | — |

## 4. Phases

### S1 — Make the Windows build a Steam build

**Phase goal:** one executable with two honest launch modes, controllers from every PC VR vendor, a sky instead of
a room when passthrough does not exist, no key, and a test pass on three real configurations.

#### CS-250 [CC] Launch flags

- **Goal:** `-vr` starts XR; `-desktop` (or no flag) never touches it.
- **Objectives:**
  1. In `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`, Standalone's `m_InitManagerOnStart` → 0 (Android keeps 1).
  2. `Launch.cs` (runs first by script execution order): read `Environment.GetCommandLineArgs()`; on `-vr`,
     `XRGeneralSettings.Instance.Manager.InitializeLoaderSync()` then `StartSubsystems()`; on failure show a
     player-visible line ("No VR runtime found; starting on the desktop") and continue in desktop mode.
  3. Editor: keep the editor behaving as today (a serialized "editor starts XR" toggle, default true), so Link
     sessions are unchanged.
  4. Confirm every `isDeviceActive` reader still works: with the loader never started it is false, which is the
     desktop path they already take.
  5. Quit path: `Launch` stops subsystems and deinitialises the loader on `OnApplicationQuit` so SteamVR does not
     keep the app listed as running.
- **Needs:** nothing.
- **Evidence:** on a PC with SteamVR installed and the headset off, `-desktop` never launches SteamVR; `-vr` launches
  it or shows the line.
- **If it fails:** XR Management refuses manual init on this version → fall back to two executables? No: use the
  `-xr-disable`-style approach of removing the loader list at runtime via `XRGeneralSettings.Manager.loaders.Clear()`
  before start; document which one landed.

#### CS-251 [CC] Controller profiles on Standalone

- **Goal:** Index, Vive and Windows MR controllers select and grab exactly like Touch.
- **Objectives:**
  1. `Quest3ProjectSetup`: a `StandaloneOnlyFeatures` list with `ValveIndexControllerProfile`,
     `HTCViveControllerProfile`, `MicrosoftMotionControllerProfile`; enabled for the Standalone group only.
  2. `XRInputRig` bindings: confirm select, activate, aim and grip come from XRI's generic actions
     (`<XRController>{LeftHand}/select` style), not Oculus-specific usages; fix any hard-coded path.
  3. The dock and card pokes: confirm the poke point exists for a controller (tip of the controller) as it does
     for the index finger.
- **Needs:** nothing.
- **Evidence:** in a Link or SteamVR session with the Touch controllers, nothing regresses; with the Meta XR
  Simulator or a borrowed Index, select and grab work. Final proof is Valve's own review run.
- **If it fails:** a profile that will not bind → the generic `KHR Simple Controller` profile as the floor.

#### CS-252 [CC] Passthrough awareness

- **Goal:** on a runtime without passthrough the toggle is gone and the sky is on; on the Quest nothing changes.
- **Objectives:**
  1. `ExperienceModeManager`: a `PassthroughAvailable` property from the AR subsystem descriptor (the
     `ARCameraManager` has no subsystem, or `ARSession.state` reports unsupported) and from `isDeviceActive`;
     on desktop with no headset it is also false.
  2. When false: force `Mode.VR`, clear alpha 1, hide the utility window's passthrough control and the dock's,
     ignore the `P` key, and skip the preference.
  3. The intro placement still works (it does not depend on passthrough).
- **Evidence:** SteamVR session: no toggle, sky on; Quest APK: unchanged (testing roadmap CS-218).

#### CS-253 [CC] Build entry point

- **Goal:** a menu item and batch method that make the Steam build and refuse a wrong one.
- **Objectives:**
  1. `SteamBuild.cs` with *Cosmic Simulation → Windows → Build Steam Build* and `BuildSteam()` for batch.
  2. Scenes from `EditorBuildSettings` enabled only; `AppVersion` stamped; `EnsureLegalNoticesShip()`.
  3. Refuse if `Assets/Being/Data/being_keys.asset` exists in the project (it is gitignored but a local file would
     ship) unless `-allow-dev-key` is passed for bench builds.
  4. `-il2cpp` switch (D3) that sets the backend when the module is present and fails loudly when it is not.
  5. Output to `Builds/Steam/win64/`; write `build_info.txt` with version, commit and backend beside the exe.
- **Evidence:** the build runs from the menu and from batch; the info file is correct.

#### CS-254 [CC] Being on PC

- **Goal:** the being connects on PC through the endpoint, picks the right microphone, and can be switched off.
- **Objectives:**
  1. The endpoint URL and the `beingEnabled` flag on `BeingSettings` (CS-233's work reused; nothing new in
     `Realtime.cs`).
  2. Default microphone: the system default (Steam Link and Quest Link both expose the headset mic as a device);
     the `MicrophoneRow` lets the player change it and persists via `Prefs.Microphone`.
  3. Audio output: the being's 3D source follows the headset in VR and the camera on the desktop; confirm the
     mixer route is the same.
  4. The desktop `C` key and the HUD dock button both summon it (already true; confirm after CS-250).
- **Evidence:** PC session with no key in the build: greeting, question, answer, tool call.

#### CS-255 [TERM] PC VR and desktop test pass

- **Goal:** the run sheet on three real configurations, with numbers.
- **Objectives:**
  1. **SteamVR + Steam Link on the Quest 3:** install Steam Link on the headset; SteamVR as the OpenXR runtime;
     launch `-vr`; hands (pinch select, grab, two-hand scale, brush, tap-home); then Touch Plus controllers; dock,
     cards, markers, all 7 experiences; the being with the forwarded mic; frame rate at 72 and 90 Hz from the
     SteamVR frame timing overlay; note hand-tracking quality compared with on-device.
  2. **Quest Link (Meta runtime):** same sheet; passthrough must be hidden (CS-252) even though the runtime could
     do it in developer mode.
  3. **Desktop, no headset:** `-desktop`; 1920 × 1080 and 2560 × 1440 windowed, fullscreen; the full GDD §5.3 key
     map; the HUD dock; Solar Row framing (CS-171); resize mid-session; alt-tab and back.
  4. **Wrong-configuration checks:** `-vr` with SteamVR installed and the headset off (the message, then desktop);
     `-vr` with no OpenXR runtime at all; `-desktop` with the headset on (must stay flat).
  5. **System requirements:** the GPU and CPU used, and the lowest settings that held 72 Hz, go into the store copy.
- **Needs:** CS-250–254 merged and a Steam build from CS-253.
- **Evidence:** three completed sheets under `docs/release/runs/`; the frame timings; the hardware line.
- **If it fails:** every failing row → CS-256 with the configuration named.

#### CS-256 [CC] Fixes from CS-255

- **Goal:** the configuration-specific failures fixed one commit each.
- **Evidence:** the terminal re-runs the failing configuration's rows only.

### S2 — Steamworks account and app

**Phase goal:** a Steamworks partner account that can receive money, an app id, and release dates that respect
both waiting periods.

#### CS-257 [OWNER] Partner account, fee, app id

- **Goal:** the app exists in Steamworks.
- **Objectives:**
  1. Steamworks partner sign-up: legal name or company, address, identity verification.
  2. Tax interview (W-8 or W-9 as applicable) and bank details; payouts need both even for a free app that may
     later charge.
  3. Pay the **$100 Steam Direct fee**; the app id appears when it clears (card payments are quick; bank transfers
     take days).
  4. Note the app id in `docs/store/STEAM_LISTING.md`.
- **Evidence:** the app in the Steamworks dashboard.
- **If it fails:** identity or tax mismatches are the usual hold; the dashboard names the field.

#### CS-258 [OWNER] Dates

- **Goal:** a release date that both of Valve's windows allow.
- **Objectives:**
  1. Read the dashboard's "earliest release date" after the fee clears (sources quote 21 and 30 days from the
     fee; the dashboard shows the real one).
  2. The store page must be public as **Coming Soon for at least two weeks** before release.
  3. Pick a release date after both; put the page-publish date two weeks earlier; put the build-review submission
     at least a week before release (reviews take 1–5 days and may bounce once).
- **Evidence:** the three dates in the listing doc.

### S3 — Store page

**Phase goal:** a page that sells the flat mode and the VR mode honestly, with every VR field set so Steam shows
the right badges and runs the right review.

Steam's asset set (re-check the Steamworks page when uploading):

| Asset | Size |
|---|---|
| Header capsule | 920 × 430 |
| Small capsule | 462 × 174 |
| Main capsule | 1232 × 706 |
| Vertical capsule | 748 × 896 |
| Library capsule / hero / logo | 600 × 900 / 3840 × 1240 / 1280 × 720 PNG |
| Screenshots | at least 5, 1920 × 1080 or larger, 16:9, real in-app imagery, no overlays |
| Trailer | 1920 × 1080 H.264, 30 fps, through the video manager |
| Page background (optional) | 1438 × 810 |

#### CS-259 [TERM] Screenshots

- **Goal:** 8–10 captures that show both ways to play.
- **Objectives:** desktop build at 2560 × 1440: one per experience, Jupiter with moons and card, a nebula; two
  from a VR session showing hands (SteamVR mirror or the Meta Link mirror, cropped to 16:9); no HUD overlays.
- **Evidence:** PNGs in `docs/store/assets/steam/`.

#### CS-260 [OWNER] + `asset-smith` Capsules and library art

- **Goal:** the capsule family from the same key art as Meta's, re-cropped; the small capsule's wordmark legible
  at 120 × 45.
- **Evidence:** every size exported; sources in `Assets/_sources/CREDITS.md`.

#### CS-261 [OWNER] Copy for PC

- **Goal:** `docs/store/STEAM_LISTING.md`, adapted from the Quest listing, telling the PC truth.
- **Objectives:**
  1. Desktop mode as a first-class way to play (mouse and keyboard, the key map in a short list).
  2. VR section: hands or controllers; Quest through Steam Link or Quest Link; Index, Vive, WMR with controllers;
     "passthrough is a Quest-only feature; on PC VR the app shows a night sky".
  3. The being: one honest paragraph (voice questions answered by an AI; needs the internet; microphone).
  4. System requirements from CS-255 item 5; Windows 10/11 64-bit; "VR requires a SteamVR-compatible headset".
  5. Tags: Education, Space, VR, Simulation, Relaxing, Exploration; no "Early Access" unless the owner wants it.
- **Evidence:** the file; pasted into Steamworks.

#### CS-262 [OWNER] Steamworks app configuration

- **Goal:** every field set so Steam shows "VR Supported" and launches the right mode.
- **Objectives:**
  1. Supported platforms: Windows.
  2. **VR support** fields: headsets Valve Index, HTC Vive, Oculus/Meta (via Link), Windows Mixed Reality; input
     "tracked motion controllers" (hand tracking goes in the description; Steam has no field for it); play area
     seated, standing, room-scale.
  3. **Launch options:** default "Play on the desktop" → `-desktop`; a VR launch option → `-vr`, typed OpenXR if the
     dashboard offers it, otherwise "Launch SteamVR App" (either tells Steam to show the VR fields).
  4. Content survey: no mature content; no account required; privacy policy URL (the same page as Meta's, after
     CS-234).
  5. Depot: one Windows 64 depot (CS-264 fills it).
- **Evidence:** the configuration saved; the store page preview shows the VR badge.

#### CS-263 [OWNER] Store page review → Coming Soon

- **Goal:** the page public and collecting wishlists on CS-258's date.
- **Objectives:** submit the page for review (1–5 days); fix any note; set Coming Soon; share the link.
- **Evidence:** the public URL.

### S4 — Build upload and review

**Phase goal:** the build installs and launches both options from a clean Steam client, and passes Valve's run.

#### CS-264 [TERM] Depot and upload

- **Goal:** the Steam build on a `beta` branch, installed on a second PC from the Steam client.
- **Objectives:**
  1. `tools/steam/app_build.vdf` and `depot_build.vdf` checked in with the app id and depot id; credentials from
     the environment; the content root `Builds/Steam/win64/`.
  2. Upload with `steamcmd +login <user> +run_app_build app_build.vdf +quit` (or the Steamworks web uploader for a
     small build); confirm the build id in the dashboard.
  3. Set it live on a `beta` branch; install on a second PC; both launch options work; the About slate shows the
     version.
  4. Only then set the build live on the **default** branch for review.
- **Needs:** CS-253 build, CS-257 app id.
- **Evidence:** the build id; the second-PC install note.
- **If it fails:** an upload refused for a missing executable path → the launch option's executable field must
  match the depot's file exactly.

#### CS-265 [OWNER] Build review

- **Goal:** Valve's reviewer runs both launch options and passes them.
- **Objectives:** submit the build for review (1–5 days); remember Valve tests VR on its own hardware (Index or
  Vive with controllers), which is why CS-251 exists; the desktop option must also run with no headset.
- **Evidence:** approved.

#### CS-266 [OWNER] + [CC]/[TERM] Review loop

- **Goal:** each note is one row, one fix, one re-upload, one resubmission.
- **Evidence:** approved; the release button available.

### S5 — Release and after

#### CS-267 [OWNER] Release

- **Goal:** live on the planned date with nothing wrong on the page.
- **Objectives:** press release when both windows have passed; install fresh on a clean machine; check price,
  launch options, the About slate's links and version; post the launch note.

#### CS-268 [TERM] Updates

- **Goal:** every update follows beta → test → default.
- **Objectives:** bump `AppVersion`; build; upload to `beta`; the three-configuration smoke rows; set live.

#### CS-269 [TERM] Optional later: Steam Frame native build

- **Goal:** the Android build on Steam Frame as a second depot.
- **Objectives:** Valve's Steam Frame Unity package (controller interaction profile); ARM64 / IL2CPP / Vulkan as
  today; a Steam Frame depot; Valve's performance assessment overlay criteria met.
- **Needs:** Valve's developer path open to us; D4 revisited.

## 5. Order

S1 and S2 in parallel (code and forms). S3 as soon as CS-255 has given real screenshots and system requirements.
S4 after S3's page is public and S1 is green. Release no earlier than the later of the two waiting windows.

## 6. Risks

- **SteamVR hand tracking is forwarded, not native.** It arrives through SteamVR's skeletal input and
  `XR_EXT_hand_tracking`; pinch quality over Steam Link may be lower than on-device. CS-255 measures it; if it is
  poor, controllers are the primary PC VR input in the copy and hands are "supported".
- **Valve tests without a Quest.** The VR launch option must run on an Index or Vive with controllers. CS-251 is not
  optional.
- **XR start-up on a desktop-only PC.** Without CS-250, a Steam player with SteamVR installed may get SteamVR
  popping up when they chose the flat game. Fix before the first upload.
- **Two stores, one privacy policy, one endpoint.** Any behaviour change to the being lands in the policy before
  either store's next update.

## Sources

- Steamworks onboarding: https://partner.steamgames.com/doc/gettingstarted/onboarding
- Steam Direct: https://partner.steamgames.com/steamdirect
- Steamworks Virtual Reality (store settings, launch options): https://partner.steamgames.com/doc/features/steamvr and https://partner.steamgames.com/doc/features/steamvr/settings
- Store graphical assets: https://partner.steamgames.com/doc/store/assets/standard
- Steam Link for Quest hand tracking (SteamVR 2.8, `XR_EXT_hand_tracking`): https://www.uploadvr.com/steam-link-quest-hand-tracking-openxr-steamvr-input/ and https://roadtovr.com/valve-steam-link-quest-hand-tracking-2-8-release/
- Passthrough over Link is developer-only: https://developers.meta.com/vr/documentation/native/android/mobile-passthrough-over-link/
- Steam Frame for Unity (ARM64, IL2CPP, Vulkan, OpenXR): https://partner.steamgames.com/doc/steamframe/engines/unity and https://partner.steamgames.com/doc/steamhardware/steamframe/compatibility
- Publishing guides with current fees and waiting periods (third party): https://www.meshy.ai/blog/how-to-publish-a-game-on-steam and https://www.thegamemarketer.com/insight-posts/how-to-publish-your-game-on-steam-guide
