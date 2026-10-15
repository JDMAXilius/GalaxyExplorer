# Quest VR plan — everything on the headset, the microphone first

15 Oct 2026, cloud session. Owner's ask: start testing on the Meta Quest, make sure everything the app does on the
desktop can be done in the headset, decide whether the 2D UI needs to become 3D for VR, and make the headset's own
microphone the default because it was not working. This plan rests on two pieces of work done the same day: a
read-only audit of the code (feature parity, UI 2.0 in the headset, the microphone path, device-only risks) and web
research into how Quest developers and other projects handle the same problems. Tickets are Phase 11 in
`docs/BACKLOG.md` (CS-371 onward). It sits beside [ROADMAP_QUEST_TESTING.md](ROADMAP_QUEST_TESTING.md), which
covers the release gate; this plan covers what has to change before that gate is worth running.

## 1. Where things stand

**Most of the app already works in the headset.** The input layer is shared: the mouse is a far `GEPointer`, and a
hand ray, a poke, a controller and the mouse all reach content through `GEInteractable` and physics. UI 2.0 is
already a world-space panel in the headset (it rides on the parked dock's pose), and every one of its 75 controls
carries a collider and a `GEButton`, so the rail, the card, Compare, the picker, Settings, Help and the Guide all
answer a hand ray. Pulling, holding, throwing and returning bodies, moons, passthrough, narration pause and Quit all
have headset paths.

**What does not**, from the audit:

| Gap | Effect in the headset |
|---|---|
| Microphone path (section 2) | The guide can look alive and hear nothing |
| No reaction to the Meta recenter; the shell is world-locked with no follow | Turn round and the menu is behind you; only the palm menu's Dock brings it back |
| Notices live inside the shell frame, which is hidden during the intro and place switches | Tracking-lost and failure notices are lost exactly when they matter |
| `L` (labels) has no headset path; scene places (Milky Way, Solar System, Sagittarius A*) cannot be moved or scaled by hand | Desktop-only actions |
| Palm menu under UI 2.0: About opens the legacy slate, Dock plays a show and a hide sound, Reset is legacy-scoped | Inconsistent, partly old copy |
| The being stands 1.4 m away, behind a 1 m shell; pulled bodies draw above the shell | Can be hidden, or paint over the menu |
| The shell's text is 16-19 px (about 0.7-0.85° at 1 m) and the text-size setting does not reach it | Small, not adjustable |
| The shell is 1.15 m wide at about 1 m: out of poke reach, so ray only | Run sheet row C5 assumes poke everywhere |
| Without microphone permission the whole guide is closed, although typed suggestions need no microphone | No guide at all for a player who says no |

## 2. The microphone

**Most likely causes**, from the audit and the research, most likely first:

1. **The release APK may have no `INTERNET` permission.** It was left to Unity's automatic detection, which does
   not reliably see the guide's `ClientWebSocket`. The guide then greets from its stored line, opens the
   microphone and never hears anything: it looks exactly like a dead microphone.
2. **Over Quest Link the default device is the PC's, not the headset's.** With no saved choice, the app opened the
   system default. In a Windows player over Link the headset microphone is `Headset Microphone (Oculus Virtual
   Audio Device)`, which Windows does not make the default. This fits "I'm not sure which one it is".
3. **The capture can die on pause and was never restarted.** Taking the headset off pauses the app (the Meta menu is a
   focus loss instead); Android can tear the input stream down, `GetPosition` stops moving, and the app kept the dead
   clip because it was still "armed".
4. **"Don't ask again" was not explained.** On Android 11+ a second Deny counts as "don't ask again"; the request
   then returns at once with no dialog, and nothing told the player that only the headset's settings could turn the
   microphone back on.
5. **Turn detection may be too strict for the built-in microphone** (OpenAI's server VAD at 0.65 with a 3 s window
   that only the server's `speech_started` keeps open).
6. Smaller ones: the Settings level meter could end the guide's capture on the shared device; no key in the APK
   (the being runs offline); a system-level microphone mute.

**On the headset the microphone is `"Android audio input"`.** Unity lists three devices on Quest: `Android audio
input`, `Android camcorder input` and `Android voice recognition input`, presets of the same microphone; the last
two fail to start on some Quest 3s. Open-source Quest projects pick `Android audio input` by name.

**Fixed in code now (CS-371, needs the device to confirm, CS-372):**

- `INTERNET` is forced in the Android player settings that every build writes.
- The default device is the headset's own microphone unless the player picks another: `Android audio input` on the
  Quest, the Oculus virtual device over Link. Settings > Microphone shows that first row as **Automatic** and names
  the device it is using.
- The guide releases the microphone when the app pauses or, in the headset, loses focus (the Meta menu), and takes it
  again on return; a capture whose write head
  stops for 2 s is restarted (up to three times), and each step writes a `Mic:` line to logcat with the device list,
  the device chosen and its sample rate.
- "Don't ask again" is recognised: the Guide and Settings tell the player to turn the microphone on in the headset's
  settings for the app, and a fresh install no longer says the opposite.
- The listen window stays open while the microphone hears sound (capped at 10 s), and every "heard nothing" close
  logs the microphone's peak level and the VAD threshold, so one device run tells us whether to tune the threshold.
- The Settings meter no longer ends the guide's capture.

**Not done, deliberately:**

- **Echo cancellation.** Unity's microphone on Android uses no echo-cancelling source, and Quest support for
  Android's `AcousticEchoCanceler` is unconfirmed. The guide is already half-duplex (it listens only after its own
  voice and an echo tail), which is the standard workaround. A native capture plugin is only worth it if the owner
  wants to interrupt the guide mid-sentence.
- **The VAD threshold.** 0.65 may be deliberate against the guide hearing itself through the speakers; tune it from
  the logged peaks (CS-372), not blind.

## 3. "Should the 2D UI become 3D for VR?"

**Recommendation: no rebuild. Spatialise the existing panels properly.** Meta's own system UI, Galaxy Explorer on
HoloLens 2 and the astronomy apps we found all use flat panels placed well in space, not 3D widgets. What makes a
panel good VR UI is placement, size by angle, how it is reached, and what happens when the player moves, and that
is where the gaps are. Meta's numbers:

| | Meta guidance | UI 2.0 today |
|---|---|---|
| Distance for ray use | 0.8-3 m (avoid 0.5-0.8 m) | about 1 m. Fine for ray |
| Distance for poke | 0.42-0.46 m | out of reach |
| Placement | about 1 m, slightly below the eyes; world-locked, summoned back rather than chasing the head | world-locked at about 1 m; no recenter, no recall but the palm menu |
| Targets | 22 mm minimum, 12 mm apart | 28-31 mm smallest. Fine |
| Text | comfortable at about 0.6-0.75° and up; Regular or heavier | 0.7-0.85° for most text. Borderline; not adjustable |
| Feedback | hover and pressed states and a sound for every press, because hands have no haptics | hover from GE focus; sounds partly |
| Over passthrough | opaque or dark backplates; no pure white | opaque #181D24 plates. Fine |

**So the UI work is:** honour the Meta recenter and give the player a way to call the menu back
(CS-374, CS-375); move notices out of the shell so they show in the headset when the shell is hidden
(CS-376); size by angle and let the text-size setting reach the shell (CS-377); keep the being and pulled bodies
from hiding or overdrawing it (CS-380); finish the palm menu under UI 2.0 (CS-379). Keep one set of panels with a
desktop and a headset presentation, which is already the architecture (`Ui2Shell` switches render mode on the
desktop only).

**Card beside the body.** The research and the reference apps put a body's facts next to the body, world-locked and
turned to the player. UI 2.0's card is in the shell instead. That is a design change, not a fix, so it is listed as
an owner decision (section 5), not a ticket.

**Later, only if the device says so:** if text is soft on device, move the shell onto an OpenXR composition layer
(sharper text, but layers ignore depth, so a planet can no longer pass in front of the menu).

## 4. Roadmap

| Wave | What | Who | Tickets |
|---|---|---|---|
| **Q0 — Make the guide hear (now)** | Microphone and network fixes in code; then the first device diagnosis: `aapt` shows `INTERNET` and `RECORD_AUDIO`, the key is in the APK, logcat shows the `Mic:` lines, a question round-trips, pause mid-listen and talk again, deny / deny twice / turn back on in settings, and over Link the Oculus device is chosen | Cloud, then terminal | CS-371 (done in code), CS-372 |
| **Q1 — A device test pass that tells the truth** | Fix the run-sheet rows that would fail as written and add the missing ones; then one full pass on the APK with logcat saved, one eye at a time (stereo), OVR Metrics, a controller-only pass | Cloud, then terminal | CS-381, CS-382, with CS-179, CS-216, CS-229 |
| **Q2 — The menu stays with the player** | Meta recenter; call-back when the menu is out of view; notices that show in the headset; palm menu under UI 2.0 | Cloud, then terminal | CS-374, CS-375, CS-376, CS-379 |
| **Q3 — Comfort and legibility** | Size and text by angle (measure first: U2-P0's headset distance test); text size reaches the shell; being and pulled bodies clear of the shell | Owner decision, cloud, terminal | CS-377, CS-380, U2-P0 |
| **Q4 — Parity** | Headset paths for the labels toggle and for moving and scaling scene places, or accept them as desktop-only; the guide usable without a microphone (suggestions) | Owner decision, cloud | CS-373, CS-378 |

Q0 and Q1 come first because nothing after them can be judged without a trustworthy device run. Q2 to Q4 can run in
parallel once the owner has answered section 5.

## 5. Owner decisions

1. **How the menu is reached in the headset.** (a) Ray-first at about 1 m, as now (recommended: it matches Meta's ray
   band and the current layout), or (b) poke-first at 0.45 m, which means a smaller, closer panel.
2. **When the menu is behind you.** (a) It stays put; the palm menu's Dock and the Meta recenter bring it back
   (recommended, Meta's preference for world-locked UI), or (b) it drifts back into view after a few seconds.
3. **The body card.** Keep it in the shell (now), or show it beside the pulled body as well.
4. **Desktop-only actions.** Accept the labels toggle and moving or scaling the scene places as desktop-only, or add
   headset paths (CS-378).
5. **Palm menu.** Restyle it on `Ui2Kit` or retire it (CS-352, already open).

## 6. Sources

The research report (with links to Meta's design guidelines, Unity's manual, Unity issue tracker entries,
Meta and Unity forum threads and the open-source Quest projects cited above) was produced in the cloud session on
15 Oct 2026; its key sources: Meta hands UI best practices (developers.meta.com/vr/design/hands-ui-best-practices),
Meta MR design guidelines (developers.meta.com/horizon/design/mr-design-guideline), Unity Android permissions
(docs.unity3d.com/Manual/android-RequestingPermissions.html), the Unity discussion on Quest `GetPosition` stuck at 0
after AAudio teardown, the Meta forum thread on Quest 3 built-in microphone recording, and Unity's XRI UI setup
(docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/ui-setup.html).
