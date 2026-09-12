# Decision log

One row per decision that shapes the product or the codebase. Newest first.
A decision recorded here overrides anything older in the other documents; when
you change one, update the affected docs in the same commit.

---

## 2026-09-12 — Phase 7 (terminal session)

### D-010 — The one-to-one contract with the reference app is lifted
**Owner decision, 12 September 2026, explicit.** "It doesn't matter if it doesn't follow the GDD... we want to expand more."

Until now the GDD's preamble bound the app to a one-to-one reproduction of the COSMIC XR reference: seven places, matching objects, matching interactions. D-002 had already added three extra Milky Way destinations within that frame. This goes further: **other galaxies, and other real planetary systems inside our own galaxy, as places the player can see and travel to.**

Why record it rather than just build it. The clause was load-bearing in both directions — it is why several tickets were closed as out of scope, and it is the sentence a future session would cite while deleting new content it did not recognise. GDD section 4.8 and this entry exist so that cannot happen quietly.

What does **not** change:

- **Truthfulness.** New places come from real published data (NASA Exoplanet Archive, ESA/Hubble, ESA/Webb, NED). Where an appearance is modelled or an artist's impression rather than measured, the copy says so. This is the reason the research phase came before any building.
- **Our own name, copy and art.** Unchanged from D-001.
- **The interaction idiom.** Expansion reuses the existing `ExperienceModule.Kind = Destination` and `ExperienceDirector.OpenDestination` path — a tag in a parent view that opens a place in front of you, which you grab and scale. It does **not** reintroduce the drill-down navigation retired in CS-087, and it does not add camera locomotion between places: flying the viewer between galaxies is both a comfort problem in VR and a different app from the one this is.
- **The Quest 3 budget.** Technical Overview section 7.4 still binds. New content that cannot hold frame rate is not shipped because it is interesting.

Consequence for the roadmap: this adds a phase beyond Phase 7 rather than reopening Phases 4 and 5, which still owe their existing tickets. The expansion is planned as its own set of tickets so the release path is not blocked by it.

### D-009 - Remove the vendored TouchScript TUIO/OSC module rather than justify it
CS-112 offered a choice: remove the module or record why it stays. **Removed.**

The deciding fact is one the privacy policy did not have. The policy said the module
"takes no action and opens no port in this build", which is true of its behaviour, and
concluded it was inert. But both libraries were marked `Android: enabled: 1` in their
`.meta` plugin settings, and `TuioInput.cs` sat under `Assets/`, so it compiled into the
app's own assembly and referenced them. The capability therefore **shipped in the APK**;
only its invocation was absent. That is precisely the shape an automated store scan
flags, and it sat against a policy whose first line promises no network connection.

Nothing was lost. The module was a closed cluster: no script inside TouchScript outside
the module referenced it, nothing in the project referenced `TuioInput`, TouchScript has
no assembly definition tying the folder in, and the only "external" GUID reference was
the folder's own `.meta`. 14 files removed; the project compiles clean.

This does not pre-empt CS-100, which asks whether TouchScript should go entirely and
Active Input Handling move to Input System only. It removes the one part of it with a
capability we would have had to explain, and leaves the touchscreen support that CS-100
is actually about.

### D-008 · The OpenXR render-mode reading was backwards; Android is Single Pass Instanced
**Finding, not a choice — recorded as a decision because it reverses one already on
record.** Android, the shipping Quest 3 build, runs **Single Pass Instanced**.
Windows/Link runs **Multi Pass**.
**Evidence.** `OpenXRSettings.RenderMode` is declared
`MultiPass = 0, SinglePassInstanced = 1` in
`Library/PackageCache/com.unity.xr.openxr@b5b77b4027be/Runtime/Settings/OpenXRRenderSettings.cs:16-27`
(field default `SinglePassInstanced`), and the settings asset,
`Assets/XR/Settings/OpenXR Package Settings.asset`, holds `m_renderMode: 1` under
`m_Name: Android` and `m_renderMode: 0` under `m_Name: Standalone` (`m_Name: WebGL` is
also `1`, moot since it does not ship). Read directly from the package cache and the
asset during a terminal session with the live editor open, not inferred from either
file's surrounding code.
**What this reverses.** A `[CC]` session on 12 Sep, with no editor available, read the
same two sources by eye and got the assignment backwards — it recorded Android as
Multi Pass and Standalone as Single Pass Instanced, and on that basis flagged
`CLAUDE.md`'s original line ("Android multiview; Windows/Link multi-pass") as wrong.
`CLAUDE.md` was right the whole time; the note correcting it was the error, and it had
propagated into `docs/BACKLOG.md`'s `[TERM]` priority queue (items 22 through 24, since
rewritten) and into CS-095's acceptance criteria (since corrected in the same file).
**Why it matters.** A missing stereo macro is a broken eye under Single Pass Instanced
and an invisible bug under Multi Pass. With the reading corrected, **Android is where a
missing macro actually breaks something**, and Windows/Link — the platform every prior
verification step aimed at — is the one where the same bug hides. Any future
close-one-eye check for a stereo-macro fix has to run against the standalone Android
build, or against Link with the render mode explicitly forced to Single Pass Instanced;
a default Link session proves nothing about what ships.
**Consequence.** CS-095 and CS-096's device-verification steps now name Android (or a
forced-SPI Link) rather than a default Link session. No shader code changes as a result
of this entry alone — CS-096's fixes were already written on the (correct) assumption
that the shipping platform was the one at risk, because that ticket's own author read
the same two sources correctly on 12 Sep, ahead of the queue note above it.

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
