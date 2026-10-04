# Roadmap 3 of 3 — Publishing on Steam (PC, and PC VR through the Quest)

*Written 4 Oct 2026 from the repository (`main` at `f18fa1a`) and current Steamworks documentation (see Sources;
`partner.steamgames.com` could not be opened from the writing session, so figures come from search excerpts of
those pages and from recent third-party guides, and every duration and fee must be re-read in the Steamworks
dashboard when the step is done). Separate from the Quest testing roadmap and the Meta store roadmap on purpose;
the only shared piece is the being's token endpoint, which is built once (Meta roadmap M2) and reused here.*

Track tags: **[OWNER]** account, money, decision; **[CC]** code; **[TERM]** editor, build, headset.
Ticket numbers **CS-250 to CS-269** are reserved for this roadmap and are proposals until they are backlog rows.

## 0. The question first: can Steam do VR, even for a Meta Quest?

**Yes, as PC VR.** Steam sells VR apps that run on a Windows PC. A Quest owner plays them with the headset acting
as the PC's display, either through Valve's **Steam Link** app on the Quest (wireless, SteamVR is the OpenXR
runtime) or through Meta's own **Quest Link / Air Link** (cable or wireless, Meta's runtime). Our Windows build is
already an OpenXR app (`Quest3ProjectSetup` configures OpenXR for Standalone, Multi Pass), so the same executable
runs under both runtimes.

What carries over the link and what does not, for this app specifically:

| Feature | Steam Link (SteamVR) | Quest Link (Meta runtime) |
|---|---|---|
| Head and controller tracking | Yes | Yes |
| **Hand tracking** | Yes since SteamVR 2.8: the Quest's hands are forwarded as `XR_EXT_hand_tracking`, which is what Unity's XR Hands feature uses | Yes |
| **Passthrough** | **No.** SteamVR has no passthrough extension for apps | **Developer-only.** Meta enables passthrough over Link only behind developer-mode toggles and does not support it for released apps |
| Microphone (the being) | Yes, the Quest mic is forwarded as a PC audio device | Yes |

So on Steam the app is **full-VR by default** (the dark sky), with the passthrough toggle hidden when the runtime
cannot do it, and hands work. The standalone Quest APK is **not** something Steam distributes; the standalone path
is Meta's store. One new wrinkle for later: Valve's **Steam Frame** headset runs native ARM64 Android APKs as well as
streamed PC titles, with Unity + OpenXR + IL2CPP + ARM64 + Vulkan as the recommended configuration, which is exactly
our Android build. That is an optional phase at the end, not part of v1.

Steam also lists **non-VR** apps, and the app has a desktop mouse-and-keyboard mode that the project has always kept
working. The recommended product shape on Steam is therefore **"VR Supported"**: playable flat on any PC, and in VR
with a headset. "VR Only" would turn away the flat players for nothing.

## 1. Where the Steam work stands today

- **A Windows build script exists and is Mono.** `Unity6WindowsBuild.BuildWindows64` writes
  `Builds/Win64/CosmicSimulationXR.exe`, Mono2x because only the Mono module is installed on the build machine; it
  reuses `AppVersion` and ships the legal notices. No menu item; batch mode only.
- **VR or desktop is decided by whether a headset is active**, not by a launch flag: `XRSettings.isDeviceActive` is
  read in `GalaxyExplorerManager`, `XRInputRig`, `DesktopMouseInput`, `DockController`, `ExperienceDirector`. XR
  initialises on start for Standalone (`m_InitManagerOnStart: 1`). On a PC with SteamVR installed but no headset on,
  that start-up can launch SteamVR or stall; Steam's own convention is two launch options the player chooses from.
- **Only Meta profiles are enabled on Standalone.** `CommonFeatures` turns on Oculus Touch, Touch Plus, hand
  interaction and hand tracking; Valve Index, HTC Vive and Windows MR controller profiles are off. Steam buyers have
  those headsets.
- **No Steamworks integration, none required.** Steam does not require the Steamworks SDK to ship; the Steam overlay,
  achievements, cloud saves are optional and the app has no save data to sync.
- **The being's key problem is the same as on Meta.** `being_keys.asset` must not ship; the token endpoint from the
  Meta roadmap (M2) serves both builds. Until it exists, the Steam build either has no being or reads a key the
  player pastes into the settings window themselves (the Windows mic picker is already there).
- **No store assets for Steam.** `docs/store/STORE_LISTING.md` is written for the Quest listing and says the
  desktop mode is "not what ships". For Steam the desktop mode is half the product and the copy must say so.

## 2. What "published" means (exit criteria)

| # | Criterion |
|---|---|
| S1 | Live Steam store page, "VR Supported", Windows, with our own art and copy and the correct VR fields (headsets, input, play area) |
| S2 | Two launch options that work from a cold Steam client: **Play on the desktop** (no XR init, mouse and keyboard) and **Play in VR** (OpenXR), the second verified on SteamVR + Steam Link and on Quest Link |
| S3 | Hands work in VR through Steam Link; controllers work on Index, Vive and Touch; passthrough controls hidden on PC VR |
| S4 | No key in the build; the being uses the token endpoint or is off |
| S5 | Store and build review passed; the required waiting periods served |

## 3. Decisions the owner has to make first

1. **Price**, and whether it matches the Meta price. Steam Direct's fee is **$100 per app**, refunded after
   $1,000 gross.
2. **Being on Steam at launch?** Same three options as on Meta; the endpoint is shared, so once it exists the cost of
   including it here is the cap configuration, not new code.
3. **IL2CPP or Mono.** Mono ships fine on Steam and is what the machine can build today. IL2CPP needs the Windows
   IL2CPP module installed on the build machine and gives smaller, harder-to-read binaries. Recommended: Mono for the
   first upload, IL2CPP before release if the module is installed by then; it changes nothing in code.
4. **Steam Frame** native build: not for v1.

## 4. Phases

### S1 — Make the Windows build a Steam build [CC] then [TERM]

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-250 | [CC] | **Launch flags.** Standalone XR initialisation becomes manual: `-vr` initialises the OpenXR loader, `-desktop` (and no flag) skips it. `GalaxyExplorerManager.Platform` and the `isDeviceActive` checks keep working because the loader simply never starts in desktop mode. A player-visible message if `-vr` finds no runtime | Both flags behave on a PC with SteamVR installed and the headset off |
| CS-251 | [CC] | **Controller profiles on Standalone:** enable Valve Index, HTC Vive and Windows MR interaction profiles for the Standalone group in `Quest3ProjectSetup` (keep Android as is). Check `XRInputRig`'s select and ray bindings are profile-agnostic (XRI actions) | Index and Vive controllers select and grab in a Link session with those profiles active, or at least in Meta XR Simulator with the profile forced |
| CS-252 | [CC] | **Passthrough awareness:** `ExperienceModeManager` hides the passthrough toggle and forces `Mode.VR` when the AR session or camera manager reports unsupported (PC VR runtimes); the utility window and dock reflect it | On SteamVR the toggle is gone and the sky is full; on the Quest APK nothing changes |
| CS-253 | [CC] | **Build entry point:** a menu item *Cosmic Simulation → Windows → Build Steam Build* beside the batch method; writes `Builds/Win64/`, excludes dev scenes, stamps `AppVersion`, refuses if `being_keys.asset` would be included; optional IL2CPP switch (decision 3) | The build runs from the menu and from batch |
| CS-254 | [CC] | **Being on PC:** the token endpoint URL in settings (shared with Meta M2/CS-233); a stored-greeting fallback; the mic picker already in the settings window covers device choice. Steam Link forwards the Quest mic; verify the default device is picked | Being connects on PC with no key in the build |
| CS-255 | [TERM] | **PC VR test pass**, three configurations, the Quest run sheet trimmed to what PC has: (1) SteamVR as runtime, Steam Link on the Quest 3, hands and then Touch Plus controllers; (2) Quest Link with Meta's runtime; (3) desktop, no headset, mouse and keyboard, 1080p and 1440p windows plus fullscreen. Note frame rate at the headset's 72/90 Hz, hand tracking quality over Steam Link, audio device routing, the being | Pass/fail per line, per configuration |
| CS-256 | [CC] | Fixes from CS-255 | Each its own commit |

### S2 — Steamworks account and app [OWNER]

| ID | Task | Done when |
|---|---|---|
| CS-257 | Steamworks partner account: legal name or company, identity, tax interview (W-8/W-9), bank details; pay the **$100 Steam Direct fee**; the new app id is created when the fee clears | App appears in the Steamworks dashboard |
| CS-258 | Note the two **waiting periods** and plan the dates: the earliest release is roughly a month after the fee is paid (sources quote 21 and 30 days; the dashboard shows the real earliest date), and the store page must be public as **Coming Soon for at least two weeks** before release | Release date chosen inside both windows |

### S3 — Store page [OWNER] with [TERM] captures and `asset-smith`

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

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-259 | [TERM] | **Screenshots** from the desktop build at 1920 × 1080 or 2560 × 1440 (the scene camera is clean and flat; no eye compositing needed) plus two from a VR session showing hands | 8–10 candidates |
| CS-260 | [OWNER] + `asset-smith` | Capsules and library art from our own art; sources logged in `Assets/_sources/CREDITS.md` | All sizes above |
| CS-261 | [OWNER] | **Copy for PC:** adapt `docs/store/STORE_LISTING.md` into `docs/store/STEAM_LISTING.md`: the desktop mode is a first-class way to play; VR section says "hands or controllers; Quest through Steam Link or Quest Link; passthrough is a Quest-only feature". System requirements (Windows 10/11 64-bit, a GPU able to hold the headset's refresh rate; measured in CS-255). Tags: Education, Space, VR, Simulation, Relaxing | Copy in the repo and pasted into Steamworks |
| CS-262 | [OWNER] | **Steamworks app config:** Supported platforms Windows; **VR support** fields (headsets: Valve Index, HTC Vive, Oculus/Meta via Link, Windows MR; input: tracked motion controllers, note hand tracking in the description; play area: seated, standing, room-scale); **two launch options**: default "Play on the desktop" (`-desktop`) and a VR launch option (`-vr`, type OpenXR if the dashboard offers it, else "Launch SteamVR App"); content survey (no mature content); privacy policy URL (the same page as Meta, after its rewrite) | Config saved, no field guessed |
| CS-263 | [OWNER] | Submit the **store page for review** (1–5 days) and set it **Coming Soon**; wishlists start | Page public |

### S4 — Build upload and review [TERM] + [OWNER]

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-264 | [TERM] | **Depot and upload:** one Windows 64 depot; upload with SteamPipe (`steamcmd` + an `app_build` script checked into `tools/steam/`, credentials from the environment) or the Steamworks web uploader for a small build; set the build live on the **default branch** only after a `beta` branch install from a second PC works | The app installs and launches both options from the Steam client on a clean machine |
| CS-265 | [OWNER] | Submit the **build for review** (Valve runs it, 1–5 days). VR builds are run by Valve on real hardware, so the VR launch option must start without a Quest-specific runtime present (SteamVR with an Index or Vive) | Approved |
| CS-266 | [OWNER] + [CC]/[TERM] | **Review loop:** each note becomes a backlog row; fix, re-upload, resubmit | Approved; release button available |

### S5 — Release and after

| ID | Task |
|---|---|
| CS-267 | Release when both waiting periods have passed; check the page, the price, the launch options on a fresh install |
| CS-268 | Updates: upload to `beta`, test, set live; keep `AppVersion` moving; the About slate shows it on both stores |
| CS-269 | *(Optional, later)* **Steam Frame native build**: the Android APK built for ARM64/Vulkan/IL2CPP with Valve's Steam Frame package (controller interaction profile), uploaded as a second depot once the Steam Frame developer path is open to us |

## 5. Order

S1 and S2 in parallel (code and forms). S3 as soon as S1's CS-255 has given real screenshots and system
requirements. S4 after S3's page is public and S1 is green. Release no earlier than the later of the two waiting
windows.

## 6. Risks

- **SteamVR hand tracking is forwarded, not native.** It arrives through SteamVR's skeletal input and
  `XR_EXT_hand_tracking`; pinch quality over Steam Link is lower than on-device. CS-255 measures it; if it is poor,
  controllers are the primary PC VR input in the copy and hands are "supported".
- **Valve tests without a Quest.** The VR launch option must run on an Index or Vive with controllers. CS-251 is
  not optional.
- **XR start-up on a desktop-only PC.** Without CS-250, a Steam player with SteamVR installed may get SteamVR
  popping up when they chose the flat game. Fix before the first upload.
- **Two stores, one privacy policy, one endpoint.** Any behaviour change to the being (what is sent, retention) has
  to land in the policy before either store's next update.

## Sources

- Steamworks onboarding: https://partner.steamgames.com/doc/gettingstarted/onboarding
- Steam Direct: https://partner.steamgames.com/steamdirect
- Steamworks Virtual Reality (store settings, launch options): https://partner.steamgames.com/doc/features/steamvr and https://partner.steamgames.com/doc/features/steamvr/settings
- Store graphical assets: https://partner.steamgames.com/doc/store/assets/standard
- Steam Link for Quest hand tracking (SteamVR 2.8, `XR_EXT_hand_tracking`): https://www.uploadvr.com/steam-link-quest-hand-tracking-openxr-steamvr-input/ and https://roadtovr.com/valve-steam-link-quest-hand-tracking-2-8-release/
- Passthrough over Link is developer-only: https://developers.meta.com/vr/documentation/native/android/mobile-passthrough-over-link/
- Steam Frame for Unity (ARM64, IL2CPP, Vulkan, OpenXR): https://partner.steamgames.com/doc/steamframe/engines/unity and https://partner.steamgames.com/doc/steamhardware/steamframe/compatibility
- Publishing guides with current fees and waiting periods (third party): https://www.meshy.ai/blog/how-to-publish-a-game-on-steam and https://www.thegamemarketer.com/insight-posts/how-to-publish-your-game-on-steam-guide
