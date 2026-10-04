# Roadmap 1 of 3 — Testing on Meta Quest

*Written 4 Oct 2026, expanded the same day, from the repository (`main` at `f18fa1a`) and current Meta developer
documentation (Sources at the end). This roadmap proves the app on a real headset. Publishing is
`ROADMAP_META_HORIZON_STORE.md`; Steam is `ROADMAP_STEAM.md`. The three are separate on purpose.*

Track tags are the backlog's: **[TERM]** editor, headset or build (terminal session); **[CC]** code in the repo
(cloud session); **[OWNER]** an account, a purchase or a decision. Tickets **CS-210 to CS-229** are reserved here
and are proposals until they are rows in `docs/BACKLOG.md`.

How to read a ticket below: **Goal** is the one sentence it exists for. **Objectives** are the atomic steps, in
order, each small enough to tick. **Needs** is what must be true before it starts. **Evidence** is what gets
pasted into the backlog row to call it done. **If it fails** is the named fallback, so nobody has to decide one
under pressure.

## 0. Where testing stands today

Read from the backlog and the project files, not assumed:

- **The app has never run as a standalone APK on a Quest.** CS-081 is `todo`. Everything on-device so far has
  been Quest Link sessions from the editor, and the backlog itself says a default Link session cannot verify
  stereo (Link is Multi Pass; the shipping Android build is Single Pass Instanced).
- **CS-095 is still `doing`:** 25 shaders got stereo macros and compile clean; the per-eye check on the Android
  build has not happened. A shader without the macros renders one eye wrong, and only the device shows it.
- **CS-080, CS-081, CS-082, CS-083, CS-085 are all `todo`.** Every "Quest APK" cell of the roadmap's §8 matrix is
  empty.
- **The build chain exists:** *Cosmic Simulation → Quest 3 → Configure Project* writes IL2CPP, ARM64, min API 32,
  target API 34, Vulkan, ASTC, id `com.jdmaxilius.cosmicsimulationxr`, version 0.9.0 (code 900); *Build APK*
  writes `Builds/Quest3/CosmicSimulationXR.apk`; it stops on the modal *Unsupported Input Handling on Android*
  (answer **Ignore**). The APK is **debug-signed** (`androidUseCustomKeystore: 0`). `Quest3Build.cs` has no
  signing code and no development/release switch; `BuildOptions.None` is a release-type build already.
- **The Cosmic Being changed the device profile.** `Assets/Being/Mic.cs` requests `Permission.Microphone` through
  Unity's Android permission callbacks; `Realtime.cs` opens a `ClientWebSocket` with a bearer header;
  `Session.cs` hard-codes `wss://api.openai.com/v1/realtime?model=`. So the APK now needs `RECORD_AUDIO` and
  `INTERNET`, Wi-Fi, and a key in `being_keys.asset` (gitignored).
- **Nothing in the app handles pause.** A search for `OnApplicationPause` and `OnApplicationFocus` across
  `Assets/scripts`, `Assets/Being` and `Assets/Cosmic` finds nothing. Narration keeps talking under the system
  menu until this is written (Meta's VRC.Quest.Functional.2).
- **Automation is desktop-only.** `AppCheck.cs` drives the editor with synthetic input; nothing runs on the device.
- **Passthrough on PC is for developers only** (Meta enables it over Link behind developer-mode toggles), so the
  real passthrough look is a device-only check.

## 1. What "tested" means (exit criteria)

| # | Criterion | Measured with |
|---|---|---|
| E1 | A release-configuration APK installs, boots to the intro, places content, and runs 30 minutes with no crash | MQDH deploy, logcat |
| E2 | Every row of the run sheet (§2, CS-213) passes with hands, on device, in passthrough and in full VR | Run sheet |
| E3 | 72 fps in every scene; never below 60 for more than one second; stale frames ≈ 0; no thermal throttle in 20 min | OVR Metrics report mode |
| E4 | Both eyes render every shader correctly (CS-095 closed) | Per-eye capture |
| E5 | Passthrough default and the three environment modes look right over a real room, bright and dim | Device capture |
| E6 | Hands hide on tracking loss; controllers ↔ hands switch cleanly; system gesture does nothing in-app; system menu pauses audio | VRC-shaped checks |
| E7 | The being works on Wi-Fi, fails softly without it, asks for the mic once, never hears itself | T5 |
| E8 | Manifest lists only the permissions the privacy policy will describe; APK under 1 GB; cold start timed | `aapt dump badging`, size, stopwatch |

## 2. Bench and tools (do once)

**Phase goal:** a headset that any session can deploy to in one command, with every measuring tool installed
before anyone needs it, and one run sheet that every later phase ticks.

### CS-210 [OWNER] Developer access to the headset

- **Goal:** the Quest 3 accepts builds and debugging from the development PC, over USB and over Wi-Fi.
- **Objectives:**
  1. Meta developer account signed in on the PC and on the headset; the organization created (the store roadmap
     reuses it).
  2. Developer mode toggled in the Meta Horizon mobile app for this headset.
  3. USB debugging accepted on the headset ("Always allow from this computer").
  4. Wireless adb paired: `adb tcpip 5555`, then `adb connect <headset-ip>:5555`; confirm with `adb devices`.
  5. Headset Wi-Fi on the same network as the PC (the being and Air Link need it).
- **Needs:** the headset, a USB-C data cable, the Horizon mobile app.
- **Evidence:** `adb devices` output showing the serial twice (USB and Wi-Fi) pasted into the row.
- **If it fails:** stay on USB; wireless is a convenience, not a requirement for any later phase.

### CS-211 [TERM] Tools installed and talking to the headset

- **Goal:** deploy, logs, metrics, traces and GPU captures all work from the terminal session.
- **Objectives:**
  1. **Meta Quest Developer Hub** (MQDH): device shows as connected; try one deploy of any APK and one logcat tail.
  2. **OVR Metrics Tool** installed on the headset from MQDH; enable the HUD once and read FPS in Home.
  3. **RenderDoc for Oculus** (Meta's fork) installed; confirm it lists the headset as a capture target.
  4. **Perfetto** reachable from MQDH's performance analyzer; take a 10 s trace of Home.
  5. Unity **Android Logcat** package added to the project so logs show in the editor as well.
  6. `aapt` (or `apkanalyzer`) on the PATH from the Android SDK the editor installed, for manifest dumps.
- **Needs:** CS-210.
- **Evidence:** one screenshot or log line from each tool.
- **If it fails:** MQDH and `adb` alone cover T1 and T2; metrics and RenderDoc can be installed when T4 starts.

### CS-212 [TERM] Meta XR Simulator (optional)

- **Goal:** replay flows on the PC without the headset when the headset is busy or elsewhere.
- **Objectives:**
  1. Install the simulator; it is an OpenXR runtime on the PC and needs no extra Unity package for an OpenXR
     project.
  2. Select it as the active OpenXR runtime; press Play; confirm head and hand input drive the app.
  3. Note what it cannot do: per-eye shader validation at device resolution, performance, passthrough look.
- **Needs:** nothing.
- **Evidence:** one editor session with the simulator reaching the Solar System.
- **If it fails:** skip; nothing later depends on it.

### CS-213 [CC] The run sheet

- **Goal:** one list of every interaction the app has, so a device session never forgets a line and every
  session is comparable.
- **Objectives:**
  1. Generate `docs/release/QUEST_RUN_SHEET.md` from GDD §5.3 (the key map, which is the interaction list with a
     desktop column) and Technical Overview §12, one row per interaction with columns: step, expected, passthrough
     result, full-VR result, notes.
  2. Group the rows: intro and placement, dock and Recenter, each of the 7 experiences, pull/moons/cards/tap-home,
     Milky Way markers (hover card, click travel), overlays, environment modes, utility window, help cards, the
     being, VRC-shaped checks (CS-219).
  3. Add a header block for the build id, headset OS version, date, tester, and the OVR Metrics summary.
- **Needs:** nothing.
- **Evidence:** the file exists and T2 fills a copy of it.
- **If it fails:** the GDD §5.3 table is the fallback sheet.

## 3. Phases

### T1 — First standalone boot

**Phase goal:** close the "never run on device" gap with hard facts: what the APK asks for, how big it is, how
long it takes to start, what the log says, and whether both eyes see the same world.

#### CS-214 [TERM] Build, measure, read the manifest

- **Goal:** know exactly what the APK contains and requests before it ever reaches a reviewer.
- **Objectives:**
  1. *Configure Project*, then *Build APK*; answer **Ignore** on the input-handling modal; note wall time.
  2. `aapt dump badging Builds/Quest3/CosmicSimulationXR.apk` → paste `package`, `versionCode`, `sdkVersion`,
     `targetSdkVersion`, every `uses-permission` and `uses-feature`.
  3. Expected today: `RECORD_AUDIO` (the being), `INTERNET` (the being), `com.oculus.permission.HAND_TRACKING`
     and the `oculus.software.handtracking` feature (XR Hands), the VR intent category from the Meta feature.
     Anything else is a finding.
  4. File size in MB; Meta's ceiling is 1 GB for the APK.
  5. `apksigner verify --print-certs` → confirm debug signing (expected now; the store roadmap replaces it).
  6. Unzip and confirm `assets/` carries `Resources/legal` text (CS-116) and that no `being_keys` asset is in the
     bundle unless the key was deliberately set for this test build.
- **Needs:** CS-211 (aapt).
- **Evidence:** the badging dump and size in the row; CS-091 closes with it.
- **If it fails:** a build error is a [CC] row with the console text; a wrong permission is CS-237 in the store
  roadmap, noted here and not fixed here.

#### CS-215 [TERM] Deploy, boot, first log

- **Goal:** the app starts on the device and the log is clean enough to trust later results.
- **Objectives:**
  1. Deploy through MQDH (or `adb install -r`).
  2. Start a logcat filtered to `Unity` and the package; launch from the headset's app library (Unknown Sources).
  3. Stopwatch: launch → first intro frame; launch → content placed.
  4. Walk the intro: placement hologram, the two hint cards, the dock appears.
  5. Read the log for `Exception`, `Shader`, `Missing`, `NullReference`, `OpenXR` errors, permission denials.
  6. Leave it idle for 5 minutes; confirm the proximity sensor sleep and wake return to the same state.
- **Needs:** CS-214.
- **Evidence:** the filtered log attached; the two timings.
- **If it fails:** crash on launch → logcat full dump into a [CC] row; black screen with a running app → check
  the camera clear alpha and the OpenXR loader list first (both are set by *Configure Project*).

#### CS-216 [TERM] Per-eye shader check (closes CS-095)

- **Goal:** every shader draws the same world in both eyes under Single Pass Instanced.
- **Objectives:**
  1. Open `docs/SHADER_STEREO_AUDIT.md`; it lists the 25 mechanically fixed shaders and the 8 hand-fixed ones
     with the scene each lives in.
  2. For each scene, look at the content with the left eye closed, then the right: anything that appears in one
     eye only, swims, doubles or is offset is a failure; note the object and the shader.
  3. Record a 30 s clip per scene from the headset (MQDH capture) for the record; note the capture is one eye.
  4. Pay attention to the known-risky families: additive point sprites (galaxies, Cosmic Web, nebulae), the
     intro hologram (`intro_placement_object_shader`), the being's `Points` and `Rim`, the rim/atmosphere shells,
     the dim quad, the orbit lines, TMP overlays.
  5. Mark each shader in the audit file: pass, fail (what), or not reachable in this build.
- **Needs:** CS-215.
- **Evidence:** the audit file updated; CS-095 moves to done or lists the failing shaders.
- **If it fails:** each failing shader → a [CC] row (CS-217). The fix is the stereo macro set
  (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`,
  `UnityStereoScreenSpaceUVAdjust` where screen UVs are used), never a renderer change.

#### CS-217 [CC] Fixes from T1

- **Goal:** everything T1 found that is code is fixed in its own commit and handed back for a rebuild.
- **Objectives:** one row per finding; each row names the shader or file, the device symptom, the fix, and the
  rebuild request to the terminal.
- **Evidence:** commit per fix; the terminal re-runs the failing check only.
- **If it fails:** a shader that cannot be made stereo-safe is replaced by the nearest safe one in the audit's
  "replacement" column.

### T2 — Interaction matrix with hands on device

**Phase goal:** every interaction the GDD promises works with real hands at real reach, and the checks Meta's
reviewers run (hands lost, controllers swapped, system gesture, pause) pass before anyone asks.

#### CS-218 [TERM] Core flow, hands, passthrough then full VR

- **Goal:** the run sheet filled twice, once per environment mode.
- **Objectives:** (each is a run-sheet row; listed here so the sheet is not a surprise)
  1. Intro: placement on the floor, hint cards, dock arrives at `max(0.55 × head height, 0.7 m)`; Recenter.
  2. Dock: all 7 tiles switch; the switch notice; the being button; Help replays the hint cards.
  3. Solar System: Row and Relative Size layouts; pull the Sun and each planet; moons appear for Earth, Mars,
     Jupiter, Saturn, Uranus, Neptune, Pluto; pull a moon; card and narration; tap a pulled body to send it home
     (`ForceSolver` tap: ≤ 0.4 s, ≤ 3 cm); brush a sphere and watch it spring back (`TouchNudge`); `R` restore
     equivalent (dock restore).
  4. Milky Way: hover a marker shows the picture card after a moment and hides 0.35 s after leaving; click
     travels to the nebula; Escape-equivalent returns; all 7 nebulae, the Solar System marker, Sagittarius A*.
  5. Each nebula and the black hole: walk around, two-hand scale within `ScaleLimits`, restore.
  6. Andromeda, Galaxies (starts inside the shell with the card inside), Cosmic Web (two-hand rotate/scale only).
  7. Utility window: scale slider, mute, narration-only mute, text size trio, microphone row and meter, the two-tap
     Quit (confirm it quits the APK cleanly).
  8. Repeat 1–7 in full VR (sky background) and note any difference.
- **Needs:** CS-213, CS-215.
- **Evidence:** the completed run sheet committed under `docs/release/runs/<date>_<build>.md`.
- **If it fails:** every failing row becomes a [CC] row quoting the sheet line; the terminal does not fix code.

#### CS-219 [TERM] VRC-shaped checks

- **Goal:** pass, in advance, the specific behaviours Meta's Virtual Reality Checks test.
- **Objectives:**
  1. **Input.6 hands hidden on low confidence:** cover the headset cameras with a hand for 3 s; the hand visuals
     (in `ge_xr_rig.prefab`) must disappear, and the far ray with them; uncover; they return.
  2. **Input.7 controllers ↔ hands:** mid-session pick up both Touch Plus controllers; the pointer must come from
     the controller aim; put them down and lift the hands; hands resume. Nothing stuck, nothing doubled.
  3. **Input.8 system gesture:** the palm-up pinch opens the system menu; while it is opening, nothing in the app
     must receive a press (watch for a tile switching or a body pulled).
  4. **Functional.2 pause:** open the system menu during narration and during the being's answer: narration and
     the being must stop; music may continue at the ducked level; closing the menu resumes the app where it was.
     (Expected to fail today: no pause handler exists. The pass criterion is the behaviour after CS-238.)
  5. **Functional.3 never stuck:** from every scene, the dock and Recenter are reachable; a card can always be
     closed; the Quit path works.
  6. **Functional.5 tracking:** crouch, lean, step sideways; content stays in the room; no drift after Recenter.
  7. **Tracking.1 play modes:** seated run of the Solar System (dock reachable from a chair); standing; room
     scale (walk around the black hole); note which the store metadata may claim.
  8. **Boundary:** step out of the Guardian; return; the app resumes without a reset.
- **Needs:** CS-218's first pass.
- **Evidence:** pass/fail per item in the run sheet's VRC block.
- **If it fails:** 1, 3, 4 are [CC] rows (CS-221 / CS-238 in the store roadmap); 2 may be an XRI binding fix; 6
  and 8 are usually runtime behaviour and only need a note.

#### CS-220 [TERM] Reach, legibility, comfort

- **Goal:** the numbers behind "feels right": where things are, how big the text is, how far the player reaches.
- **Objectives:**
  1. Measure dock distance and height for a seated (≈1.2 m eye) and a standing (≈1.6 m eye) player; both reach
     the tiles without leaning.
  2. Card distance and font size: the stats row readable at the card's spawn distance without stepping closer.
  3. The being's station: mid-left, whole sphere in view, arm's-length-and-a-half, faces the head as the player
     turns (CS-193's placement, now confirmed on device).
  4. Two-hand scale limits feel right on the nebulae (not too small to lose, not big enough to clip the room).
  5. Any discomfort note: nothing moves the camera, so the only candidates are fast fades and the switch notice.
- **Needs:** CS-218.
- **Evidence:** a short table of distances and font sizes with "ok / change to".
- **If it fails:** each "change to" is a [CC] row with the number.

#### CS-221 [CC] Fixes from T2

- **Goal:** the T2 findings fixed, including the three the code already predicts.
- **Objectives:**
  1. **Pause:** a `MonoBehaviour` on the app root with `OnApplicationPause(true)` → `VOManager.Stop()`,
     `AmbienceController.StopBed()` or a ducked level, the being's `Session` closed or its mic released;
     `OnApplicationPause(false)` → resume music, re-arm the mic if the being is out. (Also the store roadmap's
     CS-238; one implementation.)
  2. **Hands hidden on tracking loss:** if the XR Hands visuals in `ge_xr_rig.prefab` do not already hide on
     `isTracked == false`, add it; also hide `GEPointer`'s ray when its hand is untracked.
  3. **System gesture:** confirm `GEPointer` ignores the pinch while the system gesture is active (XR Hands'
     aim flags expose it through the Meta aim extension); drop the press if so.
  4. The rest as found.
- **Evidence:** commit per fix; terminal re-runs CS-219.
- **If it fails:** none of these is optional for the store; a fix that cannot land blocks submission, not testing.

### T3 — Passthrough and environment

**Phase goal:** the room is part of the experience, as the GDD says: nothing punches a hole in it, the dim quad
sits where it should, and the three modes look intended in a bright room and a dark one.

#### CS-222 [TERM] Passthrough alpha audit on device (closes CS-083)

- **Goal:** find every shader that writes alpha where it should not, and every mode that looks wrong over a room.
- **Objectives:**
  1. Bright room: passthrough default; walk through all 7 experiences; look for black rectangles, haloes with hard
     edges, sprites that occlude the room (alpha 1 where it should be additive), the dim quad's edge.
  2. Open each nebula overlay and the Sun; watch the additive glow against a white wall and a dark corner.
  3. Switch to halo and black modes from the utility window; confirm the camera clear goes to alpha 1 for black
     and the background sky shows; switch back.
  4. Dim room (lights off, one lamp): repeat 1 and 3; note where the content is too bright or too faint.
  5. Capture one clip per mode per lighting; list failing shaders by object name.
- **Needs:** CS-215.
- **Evidence:** the list and the clips.
- **If it fails:** each shader → CS-223 with the blend mode it uses now.

#### CS-223 [CC] Alpha fixes

- **Goal:** the listed shaders stop leaking alpha, without changing their look in full VR.
- **Objectives:** per shader: set `ColorMask RGB` where the blend is additive, or write `alpha 0` for the
  composited layers, or move it behind the dim quad; keep the stereo macros; rebuild by the terminal.
- **Evidence:** CS-222 re-run on the named shaders only.

### T4 — Performance

**Phase goal:** 72 fps in every scene on Quest 3 with evidence per scene, the heaviest two understood at the draw
level, and a 20-minute soak with no throttle.

#### CS-224 [TERM] OVR Metrics report per scene (closes CS-082's measurement half)

- **Goal:** a table of scene × metric from the headset's own counters.
- **Objectives:**
  1. OVR Metrics Tool in report mode; HUD off for the run (the HUD costs frames).
  2. 60 s in each: intro idle, Solar System Row, Solar System with Jupiter pulled and its moons out, Milky Way,
     one nebula overlay, the black hole, Andromeda, Galaxies, Cosmic Web; then the Solar System with the being
     awake and speaking.
  3. Record per scene: average and minimum FPS, stale frames/s, tears, GPU utilisation, CPU utilisation, CPU
     and GPU clock levels, temperature at start and end.
  4. The pass line: average 72, minimum ≥ 60 for no more than 1 s, stale ≈ 0, clocks not pinned at max.
- **Needs:** CS-211, CS-215.
- **Evidence:** the table in the row; the report files kept with the run sheet.
- **If it fails:** the failing scenes go to CS-225 first; no cuts before the capture.

#### CS-225 [TERM] Where the frame goes

- **Goal:** the two worst scenes explained by draw and by script.
- **Objectives:**
  1. RenderDoc for Oculus capture of the worst scene at the worst moment; read the tile overdraw for the point
     clouds and the additive sprites; count draw calls against the ≤ 150 budget.
  2. Perfetto trace across a scene switch (the spike) and 10 s of steady state; find the main-thread hogs
     (`Update` allocations, `Instantiate` storms on switch, the Cosmic Web's run-time rebuild slices at 3 ms each).
  3. Unity Profiler over USB on a development build for the script side if Perfetto is not enough.
- **Needs:** CS-224.
- **Evidence:** a short list: draw or script, cost, scene.
- **If it fails:** a capture that will not attach is usually the Vulkan validation layer or a non-development
  build; try the development build for the profiler only.

#### CS-226 [CC] Cuts

- **Goal:** bring the named scenes under budget by changing content and import settings, not the renderer.
- **Objectives:**
  1. Point counts per place in the data assets and the generator seeds (Milky Way 18 720, Andromeda 17 200,
     Cosmic Web 120 000 by design; the galaxy field is the unknown); add a distance fade or a per-device scale.
  2. Additive sprite size clamps where overdraw dominates (nebulae, the Sun's corona).
  3. Texture overrides: *Apply Android Asset Overrides* already sets ASTC and ≤ 2k; confirm no 4k slipped in.
  4. Any `Update` that allocates; any per-frame `Find`.
  5. Fixed foveated rendering: check whether the Meta OpenXR package exposes it to Built-in RP in this version;
     if URP-only, note and skip.
  6. Refresh rate: leave at 72 (Display Utilities feature is on; do not raise to 90 for v1).
- **Evidence:** before/after numbers from the terminal per cut.
- **If it fails:** a scene that cannot reach 72 ships with a reduced point count on device and the full count on
  desktop; the data asset carries both.

#### CS-227 [TERM] Soak

- **Goal:** proof that 20 minutes of the heaviest content does not throttle or leak.
- **Objectives:**
  1. 20 min in Galaxies, metrics in report mode; then 20 min in the Solar System with the being awake and used
     every two minutes.
  2. `adb shell dumpsys meminfo <package>` at 0, 10, 20 min; PSS must flatten.
  3. Temperature and clock levels at the end; audio glitches noted.
- **Evidence:** the three meminfo numbers and the metrics summary.
- **If it fails:** rising PSS → a [CC] leak hunt with the profiler's memory module; throttle → more CS-226.

### T5 — The Cosmic Being on device

**Phase goal:** the being works on a headset the way it works on the desk: permission, network, echo, latency,
failure modes, noise.

#### CS-228 [TERM] Being device pass

- **Goal:** every path of the being exercised on device, including the ones with no network.
- **Objectives:**
  1. First summon: the Android microphone prompt appears once; accept; later summons do not prompt.
  2. Deny path: fresh install, deny the prompt; the being must still summon, greet from the stored clip, and not
     throw.
  3. Wi-Fi on, key present (test build only): greeting spoken by the model; listen window chime; ask a question;
     answer heard; "take me to the Crab Nebula" travels; "show me Saturn" pulls Saturn; "put everything back"
     restores; tap mid-answer stops it and the cancelled answer does not resume.
  4. Echo: with the headset speakers at default volume, confirm the being never answers its own sentence (the
     0.6 s echo tail); try at maximum volume.
  5. Latency: stopwatch from end of question to first word; note it.
  6. Airplane mode: summon; stored greeting; no hang; the window closes on its own; no exception in logcat.
  7. Noisy room (music playing): the window closes on silence detection or times out; no runaway transcription.
  8. Battery: note percentage drop over 10 minutes with the being awake versus idle.
- **Needs:** CS-215; a key in the asset for this build only.
- **Evidence:** pass/fail per item, latency and battery numbers.
- **If it fails:** code paths → `Assets/Being/` [CC] rows. The key in the APK is not fixed here; the store roadmap's
  M2 replaces it with a token endpoint before any build leaves the bench.

### T6 — Release-configuration build and outside testers

**Phase goal:** the build that testers get is the build the store will get, and two people who did not make the
app finish a session with it.

#### CS-229 [TERM] Release-configuration APK

- **Goal:** a build with nothing in it that should not ship, proven by looking, not by assuming.
- **Objectives:**
  1. Not a Development Build; IL2CPP Release; managed stripping on (`stripEngineCode: 1` is set); no autoconnect
     profiler.
  2. Confirm by size and by log that `AppCheck` and other `Editor/` assemblies are absent (they are editor-only
     by folder; the log must show no `[APP]` lines).
  3. Confirm `Debug.Log` volume is low in a 5-minute session (noise hides real errors in review).
  4. If the store roadmap's CS-236 (release keystore) has landed, build with it so the Alpha channel sees the
     final certificate from the first upload.
  5. Re-run CS-215 and the CS-218 core rows on this build.
- **Needs:** T1–T5 fixes merged.
- **Evidence:** build log, size, signing output, the re-run sheet.
- **If it fails:** differences between the development and release builds are almost always stripping; a
  `link.xml` for the reflected types is the fix.

#### Alpha channel [OWNER]

- **Goal:** two outside testers install from the store client and report back.
- **Objectives:** the app record exists (store roadmap M1); upload the CS-229 APK to the Alpha channel via MQDH;
  invite by email; give them the run sheet's core rows and a feedback form; collect notes.
- **Evidence:** two completed sheets from people who are not us.
- **If it fails:** sideload via MQDH to a borrowed headset; the channel is the better path but not the only one.

## 4. Order and parallelism

T1 first; it gates everything. T2, T3 and T4 can each be one device session and can interleave. T5 any time after
T1. T6 last. The cloud session's [CC] rows are fed entirely by what the device finds, so expect the backlog to
grow during T1 and T2 and shrink during T6.

## 5. Risks

- **Performance on the point clouds.** Hundreds of thousands of additive points in Built-in RP with no LOD. If T4
  shows Galaxies under 72, the cut is content, not a renderer change.
- **Passthrough alpha.** Any additive shader that writes alpha 1 punches a hole in the room. Only the audit finds
  them.
- **Pause.** No handler exists; it fails Functional.2 today. CS-221/CS-238 is small but mandatory.
- **The being on battery and Wi-Fi.** A live socket and an open mic cost battery and heat; T4's soak measures it.
- **Debug signing.** Fine for every phase here; decide the release keystore before T6 so testers see the final
  certificate.

## Sources

- Meta VRC guidelines (Performance.1 72 fps; Functional.1–5; Input.6/7/8; Tracking.1; Packaging): https://developers.meta.com/horizon/resources/publish-quest-req/
- Common VRC failures: https://developers.meta.com/horizon/resources/publish-common-vrc-failures/
- OVR Metrics Tool: https://developers.meta.com/horizon/documentation/native/android/ts-ovrmetricstool/
- MQDH performance analyzer, Perfetto, logs: https://developers.meta.com/horizon/documentation/unity/ts-mqdh-logs-metrics/
- Unity testing and performance analysis (RenderDoc for Oculus): https://developer.oculus.com/documentation/unity/unity-perf/
- Meta XR Simulator: https://developers.meta.com/vr/documentation/unity/unity-simulate-xrsim/
- Passthrough over Link is developer-only: https://developers.meta.com/vr/documentation/native/android/mobile-passthrough-over-link/
- Release channels and test users: https://developers.meta.com/vr/resources/publish-release-channels/
