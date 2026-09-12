# Decision log

One row per decision that shapes the product or the codebase. Newest first.
A decision recorded here overrides anything older in the other documents; when
you change one, update the affected docs in the same commit.

---

## 2026-09-12 — Phase 6

### D-007 · Music ducks under narration — **awaiting owner sign-off**
**Decision (provisional).** While `VOManager` is speaking, music drops to 55 % of
its level over 0.35 s and comes back the same way.
**Why.** GDD §9 says nothing about ducking; it separates the two layers by
mastering alone (−16 LUFS music against −14 narration, 2 dB apart). Two dB is
not much room, and the narration plays from a spatial pooled source at the
camera while the bed is flat 2D, so the bed is the thing that blurs consonants.
The duck is one multiplier on our own two sources — nothing touches the
narration itself — and setting `narrationDuck` to 1 on the component turns it
off entirely.

### D-006 · Which music bed belongs to which room state — **awaiting owner sign-off**
**Decision (provisional).** GDD §9 asks for one ambient track per environment
mode and names the three tracks the project owns, but not which is which. There
are four modes, so one track is used twice:

| Room state | Bed |
|---|---|
| Passthrough | `background_music_audio_clip` |
| Dimmed | `bgm_system_audio_clip` |
| Black halo | `bgm_galaxy_audio_clip` |
| Full black | `bgm_galaxy_audio_clip` |

**Why.** `background_music` is the neutral bed the app has always played, which
suits the state where the player's own room is the backdrop. The dimmed state
covers the solar system, the planets row and Andromeda — the places you are
looking *at* something — and the two dark states put you *in* deep space, so
they share the galaxy bed. Sharing also means opening a nebula inside the Milky
Way map does not change the music under the map.
**Consequence.** The mapping lives in `ExperienceWiring.MusicTracks` and is
written into `MusicController.tracks` in the boot scene; changing it is an
inspector edit, or a one-line edit plus re-running *Install Runtime Systems*.
Levels (0.35 on every track) still need a listening pass against the GDD's
−16 LUFS target.

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

**Extended 12 Sep 2026 (CS-113) — the version number too.** The store-readiness
audit found no version constant anywhere: `bundleVersion` sat at the inherited
`1.0` and `AndroidBundleVersionCode` at `1`, owned by nothing and reverted by
nothing, which is exactly the shape of the bug above waiting to happen again.
`Quest3ProjectSetup.AppVersion` is now the single constant, written to
`PlayerSettings.bundleVersion` on every `ConfigureProject()` (and by
`Unity6WindowsBuild`, which does not call it, so the desktop build shows the
same number). **Starting version `0.9.0`**: the app is content-complete but has
never been submitted, and roadmap phases 6-7 are unfinished, so `1.0.0` is
reserved for the first build that actually goes to the Meta store. Bump the
patch for a fix and the minor when a roadmap phase lands.
The Android version code is **derived** from that string —
`major * 10000 + minor * 100 + patch`, so `0.9.0` is `900` and `1.0.0` is
`10000` — rather than kept as a second number, because Meta refuses an upload
whose code has not increased and two hand-maintained numbers drift. The
consequence is that minor and patch must stay below 100, and that re-uploading
a build without bumping `AppVersion` will be rejected, which is correct.
`Application.version` is what runtime code reads; the constant is editor-only.

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
