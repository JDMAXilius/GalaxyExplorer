# Quest run sheet

*CS-213. One line per thing the app promises on a headset, so every device session covers the same ground and
two sessions can be compared. Written 14 Oct 2026 against `main` at `f1ae335` (UI 2.0 on by default, hand
tracking waves 1–3, the voice guide on OpenAI Realtime). When the app changes, change this sheet in the same
commit. Revised 15 Oct 2026 (CS-381) against the Quest VR plan, `QUEST_VR_PLAN.md`: rows corrected to what the code does
today, and rows added for the microphone fixes (CS-371) and the gaps the plan lists. Every row marked *(known gap)*
names the ticket that closes it.*

**How to use it.** Copy this file to `docs/release/runs/<yyyy-mm-dd>_<version>_<what>.md`, fill the header,
then mark each row in the **P** (passthrough) and **V** (full VR: Room → full dark) columns with `ok`, `FAIL`,
`skip` or `n/a`, and write what you saw in **Notes**. A `FAIL` becomes a [CC] row in `docs/BACKLOG.md` quoting
the row id (`F4`, `J7`...). Rows marked *(known gap)* are expected to fail today; they are on the sheet so the
day they pass is noticed.

## Header

| Field | Value |
|---|---|
| Date, tester | |
| Build | `Build APK` / `Build Store APK`; `AppVersion`; commit |
| Install | sideload (`adb install -r`) / release channel (Alpha, RC) |
| Headset, Horizon OS version | Quest 3 / 3S; |
| Room | bright / dim; standing / seated; play area size |
| Input used | hands / Touch Plus / both |
| Voice guide | key present (bench only) / token endpoint / none |
| OVR Metrics summary | average and minimum FPS per scene (full table in CS-224) |

**Before you start.** Uninstall any sideloaded copy before installing from a channel (CS-311). Run
`adb logcat -G 16M` (the default 256 KiB buffer loses the app log in minutes, CS-302) and start a filtered logcat.
**After you finish**, run `adb shell am broadcast -a com.oculus.vrpowermanager.automation_disable` so the
proximity sensor is not left overridden (CS-319), and save the log beside the sheet.

## A. Install and boot

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| A1 | `aapt dump badging` on the APK | Permissions: `RECORD_AUDIO`, `INTERNET`, hand tracking; nothing else unexplained; Quest 3/3S only; target API 34 | CS-214, CS-237 | | | |
| A2 | Launch from the library while in passthrough Home | The room stays visible through loading; no black frame before the intro | CS-315 | | | |
| A3 | Time launch → first intro frame, and launch → content placed | Write both times | CS-215 | | | |
| A4 | Logcat during boot | No `Exception`, shader or missing-asset errors | CS-215 | | | |
| A5 | Leave it idle 5 min, take the headset off and on | Sleeps and wakes to the same place, nothing restarted | CS-215 | | | |

## B. Intro and first use

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| B1 | Rest a hand ray on the placement orb without pinching | Nothing happens: the orb comes only on a pinch | wave 3 | | | |
| B2 | Pinch the orb, carry it, release on the floor | Content placed where released; Start visible from the first moment and stays until pressed | wave 3, 856740e | | | |
| B3 | Pinch the logo during the intro | The intro skips | CS-291, CS-304 | | | |
| B4 | Finish the intro | The UI 2.0 shell appears only after the intro really ends, on the Milky Way | U2-P2 | | | |
| B5 | First-use lessons (Help) | Lessons play once on first run; Help replays them | U2-P8 | | | |
| B6 | Drop a hand out of view while holding a planet; separately, lower both hands for 5 s | The grab lets go and "We cannot see your hands…" shows; with hands lowered it shows once, not again within a minute | U2-P8, CS-324 | | | |

## C. The UI 2.0 shell

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| C1 | Look at the shell from where you stand | Fully in view, readable without leaning; note its distance and size | U2-P0 | | | |
| C2 | Rail: Cosmic Web, Galaxies, Milky Way, Solar System | Each opens its place; the selected one is marked | U2-P2 | | | |
| C3 | Dock: Explore, Guide, Room | Each opens its panel or changes the room | U2-P2 | | | |
| C4 | Settings (gear) | Settings panel opens and closes | U2-P6 | | | |
| C5 | Pinch each shell button with the ray; then step forward until the shell is within arm's reach and poke a few with a fingertip | The ray works everywhere. The shell is about 1 m away and 1.15 m wide, out of poke reach from where you start, so poke needs stepping forward; the ray is the expected path (plan section 5, decision 1). Poke working at arm's length is not a failure to report. Note the distance at which poke starts to work | CS-276, CS-377 | | | |
| C6 | Notices (e.g. pick a place that refuses) | The notice bar shows the message and clears | U2-P4 | | | |
| C7 | Press the Meta button to recenter, then look at the shell and the dock | *(known gap)* Today the content and the menu do not follow the recenter: they stay where they were in the room. After CS-374 the dock re-parks in front of you. Write what moved | CS-374 | | | |
| C8 | Turn 180° so the menu is behind you; open the palm menu and press Dock | *(known gap)* The menu stays where it was (world-locked) and nothing says it is behind you. The palm menu's Dock brings it in front of you; that is the way back until CS-375 decides on a cue or a drift back | CS-375 | | | |
| C9 | Cause a notice while the intro runs, and again during a place switch (for example lower both hands, or pick a place that refuses, while the switch is under way) | *(known gap)* Today the notice is not shown: it lives in the shell frame, which is hidden during the intro, during switches and for the 1.5 s afterwards. After CS-376 it shows below gaze. Note whether it showed | CS-376 | | | |
| C10 | Pull a planet, then another, with the shell open; look where the being stands | Nothing hides the shell and nothing paints over it. Today the being stands 1.4 m away, behind the 1 m shell, and a pulled body can draw over the shell, so this may fail; write which body, where, and from which side | CS-380 | | | |

## D. Cosmic Web and Galaxies

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| D1 | Rail → Cosmic Web | The web surrounds you; no picker, no View chip | U2-P5 | | | |
| D2 | Two air pinches with nothing held, spread and turn | The web scales and turns about the point between your hands, not about its far centre | CS-286, CS-305 | | | |
| D3 | Rail → Galaxies | You start inside the shell of galaxies, with the card inside it | CS-199 | | | |
| D4 | Picker: next, previous, a tile | Andromeda, Whirlpool, Pinwheel, Triangulum open | U2-P3 | | | |
| D5 | The card on a galaxy | Photo, title, two facts; Listen plays its sound | U2-P3 | | | |

## E. Milky Way and the nebulae

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| E1 | Hover a nebula marker with the ray | Its picture card opens after a moment and closes 0.35 s after you leave | CS-170 | | | |
| E2 | Pinch a nebula label | The preview card: Distance, Type, Enter nebula, Listen, Back to map; you stay on the map | CC-U2-002 | | | |
| E3 | Back to map, then Enter on another | Back closes the preview; Enter travels | U2-P4 | | | |
| E4 | Inside a nebula, Back to map | Returns to the Milky Way | U2-P4 | | | |
| E5 | Walk around the map, look at labels near the edges | No label fades out while nothing is in front of it, and every visible label takes a click | CS-297, CS-320 | | | |
| E6 | The Solar System and Galactic Centre markers | Preview, then Enter travels | CS-170 | | | |
| E7 | All seven nebulae: Crab, Helix, Homunculus, Orion, Pillars, NGC 1501, Trumpler 14 | Each opens and looks right | P4 | | | |
| E8 | Two air pinches inside a nebula | It scales about the hands | CS-305 | | | |

## F. Solar System

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| F1 | Rail → Solar System | Opens on Orbits | U2-P5 | | | |
| F2 | Orbits: pinch a planet, Mercury included | The 2D card shows it and nothing travels; pinching it again puts the card away | CC-U2-003, CS-322 | | | |
| F3 | *(known gap)* Orbits: pinch Mars | Nothing: its marker has no click script yet | CS-322 | | | |
| F4 | View chip: Equal Size, Relative Size, Other star systems | Each arrangement; HD 110067 under Other star systems | U2-P5 | | | |
| F5 | Pull a planet (pinch, or ray dwell outside the intro) | It comes to the hand and grows; its in-world card and narration open; the 2D card follows | P3, U2-P3 | | | |
| F6 | Moons appear on the pulled planet; pull a moon | Moon card and narration; the 2D card switches to the moon | CS-202, CS-203 | | | |
| F7 | Brush a sphere with a fingertip | It turns a little with the stroke and springs back | CS-172 | | | |
| F8 | Tap a pulled body (short pinch, no move) | It goes home; a moon goes back to its planet | CS-174 | | | |
| F9 | Throw a pulled body (release while moving fast) | It goes home; a slow release leaves it where it is | CS-290, CS-303 | | | |
| F10 | Two-hand pinch on a held body; and one hand holding plus a pinch in the air with the other | Both scale it about itself | CS-282, CS-296 | | | |
| F11 | Compare: a body, Compare, pick another, Equal and Relative, Done | Two bodies side by side at both scales | U2-P9 | | | |
| F12 | Settings → Restore scene | Everything goes back | U2-P6 | | | |

## G. Hands

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| G1 | Look at your hands with one eye closed, then the other | Outlined hands in both eyes | CS-271, CS-095 | | | |
| G2 | Close a pinch slowly | The rim warms as the pinch closes, only then | CS-298, CS-321 | | | |
| G3 | Put a hand between your eye and a planet | The planet is hidden behind your real hand in P; not in V. Galaxy star clouds stay visible over the hand (expected) | CS-288, CS-323 | | n/a | |
| G4 | Cover the headset cameras for 3 s | Hands and their rays disappear and return | CS-283 | | | |
| G5 | Pick up both controllers mid-session, then put them down | Pointer moves to the controllers and back to the hands; nothing stuck selected | VRC Input.7 | | | |
| G6 | Palm toward the face | The palm menu opens beside that hand; closing the hand hides it | wave 3 | | | |
| G7 | Palm menu: Dock, Reset, About | Dock wears the dock glyph and brings the dock (the shell rides it) in front of you; Reset and About work | CS-306, CS-322 | | | |
| G8 | Open palm held out toward the content for half a second | Narration (or the card's Listen) pauses with a notice; again resumes from the same word | CS-289, CS-323 | | | |
| G9 | System gesture (palm-up pinch) | The system menu opens; nothing in the app is pressed | VRC Input.8 | | | |

## H. Sound and narration

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| H1 | Narration on arriving in a place | Starts by itself | U2-P3 | | | |
| H2 | Card → Listen, Listen again | Plays the selection's own sound, pauses, resumes | U2-P3 | | | |
| H3 | Open the system menu during narration and during a Listen | All audio stops; closing the menu resumes where it was | CS-283, VRC Functional.2 | | | |
| H4 | Settings → Sound off and on, Narration off and on | Each mutes only its own audio | U2-P6 | | | |

## I. Voice guide

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| I1 | Fresh install. Open the Guide and press Enable microphone | The system microphone prompt comes from Enable microphone, once. Unity may also prompt at launch, before the Guide is opened: write in Notes which happened (at launch, at Enable microphone, or both) | CS-228, CS-371, CS-372 | | | |
| I2 | Deny the prompt once (fresh install) | *(known gap)* Until CS-373 lands the guide does not open at all without microphone permission: no being, no greeting, no suggestions. The Guide panel shows the Microphone message with Enable microphone instead. After CS-373 it opens, greets from the stored clip, says it cannot hear, and the typed suggestions still work | CS-228, U2-P7, CS-373 | | | |
| I3 | Guide on Wi-Fi: greeting, ask a question | Status reads listening, thinking, speaking; the transcript shows You: and the answer | CC-U2-001 | | | |
| I4 | Open and close the Guide several times | The suggested question and command change each time; pressing a command does it | CC-U2-001 | | | |
| I5 | "Take me to the Crab Nebula", "show me Saturn", "put everything back" | Travels, pulls, restores | CS-200 | | | |
| I6 | Speakers at full volume while it answers | It never answers its own voice | CS-228 | | | |
| I7 | Tap it while it talks | It stops and listens; the cancelled answer does not resume | CS-200 | | | |
| I8 | Airplane mode, open the Guide | Stored greeting, an "unavailable" status, no hang | CS-228 | | | |
| I9 | Latency: end of question to first word | Write the seconds | CS-228 | | | |
| I10 | On the first Guide use after permission, read logcat (`adb logcat -s Unity AudioRecord AudioFlinger`) | A line `Mic: armed '...' at N Hz; devices: ...` appears, and the device it names is `Android audio input` (not the camcorder or voice recognition presets). Copy the line, with N and the device list, into Notes | CS-371, CS-372 | | | |
| I11 | While the Guide is listening, take the headset off, wait 10 s, put it on; then open the Meta system menu while it is listening, close it, and ask again | The guide hears the next question each time. A `Mic:` restart line in logcat is fine; a silent, dead microphone is the failure. Narration and the guide's voice stop while you are away (H3) | CS-371 | | | |
| I12 | Fresh install: deny the prompt, press Enable microphone, deny again. Then turn the microphone on in the headset's settings for the app and press Try again | After the second deny the Guide says microphone access is off for this app and to turn it on in the headset's settings (button: Try again), not the first-time wording. Once it is on, Try again starts the microphone and the guide hears you. It does not wait 30 s for nothing after the second deny | CS-371, CS-372 | | | |
| I13 | Build with no key, or with Wi-Fi off, and open the Guide with permission given | The Guide shows Unavailable (Try again, Keep exploring), not a listening state with a dead microphone. Check the APK has `INTERNET` (A1) so this is the key or the network, not the manifest | CS-371 | | | |
| I14 | Mute the microphone at system level (the headset's own mic mute), then ask the guide | Observe and write down what happens (nothing heard? a stuck listening state? a message?). No fix is expected; this row is to find out what the player would see | CS-371 | | | |

## J. Settings and room

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| J1 | Text size: the three sizes | The card and the in-world InfoPanels resize. The shell's own text (rail, dock, Settings, Guide) does not change; that is a known gap, not a failure | U2-P6, CS-377 | | | |
| J2 | Microphone device chooser | Cycles devices; the level bar moves when you speak | U2-P6 | | | |
| J3 | About from Settings (UI 2.0), and About from the palm menu | Settings > About shows the version and the source link only; credits and licence notices are not there yet. The palm menu's About opens the old slate instead, which still carries Microsoft links (known gap). Write what each shows | CS-086, CS-114, CS-117, CS-379 | | | |
| J4 | Quit | Asks once to confirm; quits cleanly to Home | U2-P6 | | | |
| J5 | Room: passthrough, dimmed, full dark | Each mode looks intended; no black rectangles, hard-edged haloes or sprites hiding the room | CS-083, CS-222 | | | |
| J6 | Repeat J5 in a dim room | Content not too bright or too faint | CS-222 | | | |
| J7 | Fresh install, before the Guide has ever been used: Settings > Microphone | The page says microphone access has not been given yet and to open Guide and choose Enable microphone (not the "turned off in settings" wording). The first row is Automatic and its note names the device it would use (`Using Android audio input`). The level meter does not cut off the guide's capture later | CS-371 | | | |

## K. Review checks (what Meta's reviewers try)

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| K1 | From every place, reach the rail, the dock and Settings | Never stuck: a way out is always on screen | VRC Functional.3 | | | |
| K2 | Crouch, lean, step sideways, turn around | Content stays put in the room; no drift | VRC Functional.5 | | | |
| K3 | Seated run of the Solar System | Everything reachable from a chair | VRC Tracking.1 | | | |
| K4 | Step out of the boundary and back | The app resumes without a reset | — | | | |
| K5 | 30 minutes of normal use | No crash, no freeze | VRC Functional.1 | | | |

## L. Performance (quick read; the full table is CS-224)

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| L1 | OVR Metrics HUD in each place for 30 s | 72 fps held; write the lowest minimum and where | CS-224, VRC Performance.1 | | | |
| L2 | A scene switch | Write how long the hitch is | CS-225 | | | |
| L3 | Galaxies for 20 min | No clock throttling; temperature at the end | CS-227 | | | |

## M. Desktop smoke (Windows build, `-desktop` or no flag)

| Id | Step | Expected | Ticket | Result | Notes |
|---|---|---|---|---|---|
| M1 | Launch with SteamVR installed and the headset off | SteamVR never starts | CS-250, CS-255 | | |
| M2 | Mouse: click, drag, orbit, pan, wheel, `Home` | GDD 5.3 behaves | GDD 5.3 | | |
| M3 | Keys: `1`–`0`, `M`, `L`, `R`, `P`, `Tab`, `H`, `U`, `C`, `N`, `Esc`, `F2`–`F8` | Each does what GDD 5.3 says | GDD 5.3 | | |
| M4 | Release build with `.env` beside the exe | The guide does not connect (no key read outside the editor and Development Builds) | CS-308, CS-322 | | |

## N. Per eye, controllers and desktop-to-headset parity

*Added 15 Oct 2026 (CS-381). Default Quest 3 builds are Single Pass Instanced and Link is Multi Pass, so a Link
session cannot show a one-eye fault; these rows need the APK on the headset.*

| Id | Step | Expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| N1 | In every place (Cosmic Web, Galaxies, Milky Way, a nebula, Solar System, Sagittarius A*), with the shell open, close the left eye, then the right | Everything seen with both eyes is there with each eye alone: bodies, orbit lines, star clouds, labels, the shell, hands and rays, the placement orb. Nothing missing, doubled, offset or black in one eye. Write the place and what vanished | CS-216, CS-271 | | | |
| N2 | A controller-only pass: put the hands away, use Touch controllers for the whole sheet's core path (intro, rail, dock, Settings, a pull, a throw, Guide, Quit) | Everything the hands do works from a controller ray and trigger. Rows needing a palm or a pinch (G2, G6, G8) have a controller equivalent or are noted as hand-only | VRC Input.7 | | | |
| N3 | Scene places: try to move and to scale the Milky Way, the Solar System and Sagittarius A* by hand | *(known gap)* Desktop can; the headset has no path (scene places get no placement handler). Write what happens. Cosmic Web and nebulae scale with two air pinches (D2, E8) | CS-378 | | | |

**Parity sweep.** One row per key in `DesktopShortcuts.cs`. Do the desktop action in a Windows build (M3), then do
its headset path here and mark the headset result. `N` (narration pause) and `U` (utility window) are handled in
other files; their headset paths are G8 and C4.

| Id | Desktop key and action | Headset path expected | Ticket | P | V | Notes |
|---|---|---|---|---|---|---|
| N4 | `R`: restore the layout | Settings (gear) > Restore scene, then confirm (F12) | U2-P6 | | | |
| N5 | `Home`: recenter the view | Settings > Bring menu here, or the palm menu's Dock. The Meta button recenter does not move the menu yet (C7) | CS-374, CS-375 | | | |
| N6 | `C`: summon or dismiss the Cosmic Being | Dock > Guide (I3) | CC-U2-001 | | | |
| N7 | `L`: show or hide name labels | *(known gap)* No headset path. A Settings switch is proposed | CS-378 | | | |
| N8 | `P`: passthrough toggle | Dock > Room (J5). On the headset passthrough is the Room setting; the key is a desktop preview | U2-P2 | | | |
| N9 | `Tab`: fold the legacy dock row | Desktop only (the legacy HUD does not exist in the headset); mark `n/a` | — | | | |
| N10 | `H` / `F1`: controls overlay | Settings > Help (B5). Under UI 2.0 the key still opens the legacy overlay on desktop | CS-370 | | | |
| N11 | `Esc`: close the topmost panel or overlay | Each panel's own close or Back control (K1); nothing in the headset needs a key to get out | VRC Functional.3 | | | |
| N12 | `F2`-`F8`: switch experience | Rail and Dock tiles (C2, C3) | U2-P2 | | | |
| N13 | `1`-`9`, `0`: pull the Sun to Pluto | Pinch or ray-dwell on the body (F5), or ask the guide: "show me Saturn" (I5) | P3 | | | |
| N14 | `M`: pull the Moon | Pull the Earth, then pinch its moon (F6) | CS-202 | | | |
