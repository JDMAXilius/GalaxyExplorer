# Decision log

One row per decision that shapes the product or the codebase. Newest first.
A decision recorded here overrides anything older in the other documents; when
you change one, update the affected docs in the same commit.

---

## 2026-09-11 — Phase 0 kick-off decisions

### D-005 · Phase 4 runs before Phase 5
**Decision.** Build the Milky Way destinations, nebula overlays and the black hole
(Phase 4) before Andromeda, Galaxies and Cosmic Web (Phase 5).
**Why.** Phase 4 is mostly re-composition of assets we already own, so it lands
sooner and exercises the overlay and environment systems that Phase 5 depends on.

### D-004 · Keep the original intro, add two hint cards
**Decision.** The original Galaxy Explorer intro stays: logo, Earth-pin placement
(which anchors content in the room), then the galaxy. Two one-time hint cards
follow placement — "pinch to grab" and "two hands to resize" — styled like the
intro's own prompts. No separate onboarding sequence.
**Why.** The intro is finished, narrated content that also performs the room
anchoring; replacing it with cards would throw away work and a good first
impression. The hints cover the two gestures the reference app teaches.
**Supersedes.** The three-card onboarding proposed in the roadmap v1.0.

### D-003 · Original narration reused; new clips are TTS until Phase 6
**Decision.** The 22 recorded narration clips in the project are used as they are.
The 11 clips the new content needs (Andromeda, Cosmic Web, Galaxies, Helix,
Orion, and six moons) get Windows Speech placeholders now, and a natural voice
chosen to sit close to the original narrator at Phase 6.
**Why.** The recorded voice is the best asset we have and costs nothing. Only the
gaps need filling, and nothing downstream blocks on how they are voiced.

### D-002 · The three extra Milky Way destinations stay on
**Decision.** Pillars of Creation, NGC 1501 and Trumpler 14 ship alongside the six
destinations the reference shows (Helix, Crab, Solar System, Galactic Center,
Homunculus, Orion). Nine in total.
**Why.** They are finished content with professional narration already in the
project. Hiding them for strict parity would remove value for no gain.
**Trade-off.** The Milky Way map differs from the reference by three labels; the
GDD records this as intentional.

### D-001a · The rename lives in the build script, not just Project Settings
**Found 11 Sep 2026, after the first build.** `Quest3ProjectSetup.ConfigureProject()` runs at the start of every
build and sets the Android package id from its own constant, so the first
renamed APK still shipped as `com.jdmaxilius.galaxyexplorer` under the new
name. The constant is now `com.jdmaxilius.cosmicsimulationxr`, and the editor
menus moved from "Galaxy Explorer" to "Cosmic Simulation" with the APK renamed
to `CosmicSimulationXR.apk`.
**Rule.** Identity and player settings are owned by `Quest3ProjectSetup`. Change
them there; Project Settings alone will be overwritten by the next build.

### D-001 · Renamed to Cosmic Simulation XR
**Decision.** Product name "Cosmic Simulation XR"; bundle id
`com.jdmaxilius.cosmicsimulationxr` (was `com.jdmaxilius.galaxyexplorer`).
**Why.** The identity should match the product before any store listing exists;
renaming later is disruptive.
**Consequence.** A new build installs alongside the old Galaxy Explorer build on
the headset rather than over it. Uninstall the old one to avoid confusion.

---

## Earlier (during the Quest 3 port)

| Date | Decision | Why |
|---|---|---|
| 2026-09-11 | Remove MRTK v2, build a thin XRI layer in `Assets/scripts/XR` | MRTK 2.x has no Unity 6 support; MRTK3's XRI3 line is pre-release |
| 2026-09-11 | OpenXR + Unity OpenXR: Meta, staying on the Built-in Render Pipeline | 43 custom BiRP shaders; URP would mean rewriting all of them |
| 2026-09-11 | Passthrough by default with a VR toggle | Matches the original HoloLens intent and the reference app |
| 2026-09-11 | Keep a desktop mouse/keyboard mode | Lets the app be tested and demonstrated without a headset |
| 2026-09-11 | Work on branch `quest3-port`, mirror to `main` | One line of history; `main` is what other machines and cloud sessions pull |
