# Roadmap 1 of 3 — Testing on Meta Quest

*Written 4 Oct 2026 from the repository as it stands (`main` at `f18fa1a`) and from current Meta developer
documentation (see Sources). This roadmap is about proving the app on a real headset. Publishing is a separate
roadmap (`ROADMAP_META_HORIZON_STORE.md`); Steam is another (`ROADMAP_STEAM.md`). They are kept apart on purpose.*

Track tags are the backlog's: **[TERM]** needs the editor, the headset or a build (terminal session), **[CC]** is
code in the repo (cloud session), **[OWNER]** is an account, a purchase or a decision only the owner can make.
Ticket numbers **CS-210 to CS-229** are reserved for this roadmap; they are proposals until they are rows in
`docs/BACKLOG.md`.

## 0. Where testing stands today

Read from the backlog and the project files, not assumed:

- **The app has never run as a standalone APK on a Quest.** CS-081 (standalone APK, on-device run) is `todo`.
  Everything on-device so far has been Quest Link sessions from the editor, and the backlog itself says a default
  Link session cannot verify stereo (Link is Multi Pass, the shipping Android build is Single Pass Instanced).
- **CS-095 is still `doing`:** 25 shaders got stereo macros and compile clean, but the per-eye check on the Android
  build has not happened. A shader without the macros renders one eye wrong, and only the device shows it.
- **Device-pass tickets CS-080, CS-081, CS-082 (performance), CS-083 (passthrough alpha audit), CS-085
  (regression matrices) are all `todo`.** The roadmap's Phase 7 and verification matrix (§8) put every "Quest APK"
  column at P7, so no row of that matrix has a device result yet.
- **The build chain exists:** *Cosmic Simulation → Quest 3 → Configure Project* writes IL2CPP, ARM64, min API 32,
  target API 34, Vulkan, ASTC, bundle id `com.jdmaxilius.cosmicsimulationxr`, version 0.9.0 (code 900); *Build APK*
  writes `Builds/Quest3/CosmicSimulationXR.apk`. It stops on the modal *Unsupported Input Handling on Android*
  (answer **Ignore**). The APK is **debug-signed** (`androidUseCustomKeystore: 0`); fine for testing, not for the store.
- **The Cosmic Being changed the device profile.** `Assets/Being/` opens the microphone (`Mic.cs`) and a WebSocket
  to OpenAI (`Realtime.cs`), so the APK now needs `RECORD_AUDIO` and `INTERNET`, Wi-Fi, and an OpenAI key in
  `being_keys.asset` (gitignored). The privacy policy draft still says "no microphone, no network"; that is for the
  store roadmap, but the test plan has to cover the mic permission prompt, no-network behaviour and the echo path.
- **Automation is desktop-only.** `AppCheck.cs` drives the editor with synthetic mouse and keyboard; nothing runs
  on the device. The exploration smoke tooling in `tools/mcp/` talks to the editor, not to a headset.
- **Passthrough on PC is for developers only.** Meta exposes passthrough over Link only behind developer-mode
  toggles, so the real passthrough look (alpha-0 clear, dim quad, the nebula overlays over a room) is also a
  device-only check.

## 1. What "tested" means (exit criteria)

The app is tested when every line below has a device result written into the backlog row that owns it:

| # | Criterion | Measured with |
|---|---|---|
| E1 | Installs, boots to the intro and places content, in a release-configuration APK, with no crash in a 30-minute session | MQDH deploy, logcat |
| E2 | Every row of the Quest matrix (GDD §5.3, Technical Overview §12) passes with hands, on device | Manual run sheet (§3.T2) |
| E3 | 72 fps held in every scene; no scene below 60 for more than one second; stale frames ≈ 0 | OVR Metrics Tool report mode |
| E4 | Both eyes render every shader correctly (CS-095 closed) | Per-eye capture on device |
| E5 | Passthrough default and the three environment modes look right over a real room | Device capture, two lighting conditions |
| E6 | Hands hidden on tracking loss, controllers ↔ hands switch cleanly, system gesture does nothing in-app, pause on system menu | VRC checks Input.6/7/8, Functional.2 (§3.T2) |
| E7 | The being works on Wi-Fi and fails softly without it; mic prompt once; no self-echo through the speaker | §3.T5 |
| E8 | Manifest requests only the permissions the privacy policy will describe; APK under 1 GB; load time acceptable | `aapt dump badging`, file size, stopwatch |

## 2. Bench and tools (do once)

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-210 | [OWNER] | Developer mode on the Quest 3 (developer account, org, headset toggled), USB debugging accepted, Wi-Fi adb paired | `adb devices` lists the headset over USB and Wi-Fi |
| CS-211 | [TERM] | Install **Meta Quest Developer Hub** (deploy, logcat, metrics, Perfetto, casting, release-channel upload), **OVR Metrics Tool** on the headset (HUD + report modes), **RenderDoc for Oculus** (Meta's fork, tile data), Unity **Android Logcat** package in the editor | Each tool shows the headset |
| CS-212 | [TERM] | Optional: **Meta XR Simulator** (an OpenXR runtime on the PC; needs no Unity package with OpenXR) to replay flows without the headset. Note it does not validate stereo or performance | A flow runs in the simulator |
| CS-213 | [CC] | A one-page **run sheet** generated from the GDD §5.3 and Technical Overview §12 matrices: one line per interaction with a pass/fail/notes column, as `docs/release/QUEST_RUN_SHEET.md` | The sheet exists and the terminal uses it in T2 |

## 3. Phases

### T1 — First standalone boot (closes the "never run" gap)

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-214 | [TERM] | Build the APK from the menu; record build time, APK size, and the exported `AndroidManifest.xml` (`aapt dump badging` or the Gradle output). List every `uses-permission` and `uses-feature` | Manifest and size pasted into the row; CS-091 closes with it |
| CS-215 | [TERM] | Deploy with MQDH; boot; time to the first frame of the intro; watch logcat for exceptions, shader compile errors, missing-asset errors | Boots clean; the log is attached |
| CS-216 | [TERM] | **Per-eye check (closes CS-095):** every shader in `docs/SHADER_STEREO_AUDIT.md`, scene by scene, both eyes. Capture by recording from the headset (MQDH capture) and by closing one eye at a time; note any shader that shows only in one eye, swims, or doubles | All 25 fixed shaders plus the 8 hand-fixed ones pass in both eyes |
| CS-217 | [CC] | Fixes for anything T1 finds that is code: shader macros, a feature that must be enabled, a permission that must not ship | Each fix is its own commit, then the terminal rebuilds |

### T2 — Interaction matrix with hands on device (closes CS-080 and the device half of CS-085)

Run the run sheet (CS-213) end to end, standing, in passthrough, then once more in full VR.

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-218 | [TERM] | **Core flow:** intro placement, dock (height from head, Recenter), all 7 experiences, Solar Row / Relative Size layouts, pull a planet, moons appear, pull a moon, card and narration, tap-to-return, restore, Milky Way markers hover → card and click → travel, nebula scenes, black hole, Andromeda, Galaxies (start inside the shell), Cosmic Web, utility window (mute, narration, text size, mic, Quit), help cards | Every line has a device result |
| CS-219 | [TERM] | **VRC-shaped checks** (these are what Meta's reviewers test, so test them now): hands hide when tracking is lost (cover the cameras); pick up controllers mid-session and put them down again (hands ↔ controllers); the system gesture (palm pinch) opens the system menu and does nothing in the app; the system menu pauses audio and narration and the app resumes where it was; boundary breach and return; sleep/wake with the proximity sensor | Pass/fail per check |
| CS-220 | [TERM] | **Reach and comfort:** dock and cards within arm's reach for a seated and a standing player; text legible at the card distance; the being's station in the mid-left as directed (CS-193) | Notes with measured distances; code changes go to [CC] rows |
| CS-221 | [CC] | Fixes from T2. Likely candidates from the code as it stands: pause handling (`OnApplicationPause` stops `AudioService`/`VOManager` and the being's socket), a hand-visibility rule on low confidence if the XR Hands visuals do not already hide, controller fallback where a hand-only path exists | Each fix is its own commit |

### T3 — Passthrough and environment (closes CS-083)

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-222 | [TERM] | Passthrough default: camera clear alpha 0 shows the room; dim quad ordering; halo and black modes; the toggle in the utility window; each nebula overlay and the Sun over a bright room and over a dim room (two sessions). Note any shader that writes alpha where it should not (the "alpha audit") | A capture per mode per lighting condition; failing shaders listed |
| CS-223 | [CC] | Alpha fixes in the shaders the audit names; a passthrough-safe variant where a blend mode leaks | Rebuilt and re-captured by the terminal |

### T4 — Performance (closes CS-082)

Budgets are the Technical Overview's §7.4 (≤ 150 draw calls, ≤ 400k particles total, textures ≤ 2k ASTC, one dim
quad). Quest 3 runs at 72 Hz by default; Meta's VRC.Quest.Performance.1 is 72 fps.

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-224 | [TERM] | **OVR Metrics report per scene:** 60 s in each of the 7 experiences plus one nebula overlay and one pulled planet with moons. Record FPS, GPU %, CPU %, stale frames, tears, CPU/GPU clock levels, temperature. Galaxies and Cosmic Web first (the roadmap's own call) | A table in the row: scene × metric |
| CS-225 | [TERM] | **Where it is slow:** RenderDoc for Oculus capture of the worst two scenes (tile data, overdraw from the point clouds and additive nebula sprites); Perfetto trace of the switch between scenes (the spike) | Hotspots named by draw and by script |
| CS-226 | [CC] | **Cuts:** particle counts per place in the data assets, LOD or distance fade on the galaxy fields, texture import overrides (ASTC, 2k ceiling — *Apply Android Asset Overrides* exists), additive sprite overdraw, any `Update` that allocates. Consider fixed foveated rendering through the Meta OpenXR feature if BiRP supports it in this package version (verify; it may be URP-only) | Each cut measured before/after by the terminal |
| CS-227 | [TERM] | **Soak:** 20 minutes in Galaxies, then 20 in the Solar System with the being awake; watch for thermal throttling (clock level drops), memory growth (`adb shell dumpsys meminfo`), audio glitches | Metrics stay flat; no throttle |

### T5 — The Cosmic Being on device

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-228 | [TERM] | Mic permission prompt appears once and is remembered; summon on Wi-Fi: greeting, listen window, a spoken question answered, "take me to the Crab Nebula" travels; no self-echo through the headset speaker (the 0.6 s echo tail); tap-to-interrupt; dismiss. Then airplane mode: stored greeting plays, no hang, no exception. Then a noisy room | Pass/fail per line; latency to first word noted |
| — | [CC] | Anything found goes to `Assets/Being/` fixes. The **key in the APK** is not a test item; it is the first blocker in the store roadmap and must be solved before any build leaves the bench | — |

### T6 — Release-configuration build and testers

| ID | Track | Task | Done when |
|---|---|---|---|
| CS-229 | [TERM] | A **release-configuration** APK: not a Development Build, IL2CPP Release, managed stripping on, no `Debug.Log` floods, App Check and editor-only code confirmed absent (they are `Editor/` assemblies, confirm by size and by logcat). Re-run T1's boot and T2's core flow on it; builds differ | The release APK passes T1 and the T2 core flow |
| — | [OWNER] | Put the release APK on the **Alpha release channel** for up to a handful of invited testers (this needs the app created in the Developer Dashboard, which is the store roadmap's first step; the two roadmaps meet here and nowhere else) | Two outside testers finish a session and report back |

## 4. Order and parallelism

T1 first; it gates everything. T2, T3 and T4 can interleave in one device session each. T5 any time after T1.
T6 last. The cloud session's work ([CC] rows) is fed entirely by what the device finds, so expect the backlog to
grow during T1 and T2 and shrink during T6.

## 5. Risks

- **Performance on the point clouds.** The galaxies are hundreds of thousands of points in Built-in RP with no LOD.
  If T4 shows Galaxies under 72, the cut is content (fewer points, fade with distance), not a renderer change.
- **Passthrough alpha.** Any additive or transparent shader that writes alpha 1 punches a hole in the room. The
  audit is the only way to find them.
- **The being on battery and Wi-Fi.** A live socket and an open mic cost battery and heat; T4's soak with the being
  awake tells us how much.
- **Debug signing.** Fine for every phase here. The store needs one release keystore, kept out of the repo, used
  for every upload from the first one; that belongs to the store roadmap but decide it before T6 so the release
  APK is already signed the way the store will see it.

## Sources

- Meta VRC guidelines (Performance.1 72 fps; Functional.1–5; Input.6/7/8; Tracking.1; Packaging): https://developers.meta.com/horizon/resources/publish-quest-req/
- Common VRC failures: https://developers.meta.com/horizon/resources/publish-common-vrc-failures/
- OVR Metrics Tool: https://developers.meta.com/horizon/documentation/native/android/ts-ovrmetricstool/
- MQDH performance analyzer, Perfetto, logs: https://developers.meta.com/horizon/documentation/unity/ts-mqdh-logs-metrics/
- Unity testing and performance analysis (RenderDoc for Oculus): https://developer.oculus.com/documentation/unity/unity-perf/
- Meta XR Simulator: https://developers.meta.com/vr/documentation/unity/unity-simulate-xrsim/
- Passthrough over Link is developer-only: https://developers.meta.com/vr/documentation/native/android/mobile-passthrough-over-link/
- Release channels and test users: https://developers.meta.com/vr/resources/publish-release-channels/
