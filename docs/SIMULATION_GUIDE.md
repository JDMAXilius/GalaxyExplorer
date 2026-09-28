# Cosmic Simulation XR — The Complete Simulation Guide

*As built on 27 September 2026 (branch `quest3-port`, mirrored to `main`, commit `ba2bac45`). This describes what the app actually does today, on the Meta Quest 3 and on a Windows desktop, verified against the code. Its companion, `docs/SYSTEM_OVERVIEW.md`, explains how it is built. Where the older `docs/GDD.md` disagrees with this guide, this guide describes the build and the GDD describes the intent.*

---

## 1. What it is

**Cosmic Simulation XR** is a mixed-reality tour of the universe that happens in your room. You pull planets out of a row and hold them, read what they are, walk around a black hole, stand inside a nebula and float inside the cosmic web. A voice guide made of light, the **Cosmic Being**, answers questions and takes you places.

| | |
|---|---|
| **Platforms** | Meta Quest 3 / 3S (primary: hands, controllers, passthrough). Windows desktop (secondary: mouse and keyboard, used for testing and demos). |
| **Genre** | Educational exploration. No score, no fail state, no end. |
| **Session** | 5–20 minutes; every place is one press away. |
| **Lineage** | Microsoft's open-source Galaxy Explorer (MIT), ported from HoloLens to Quest 3 and extended. Credited in About. |
| **Truthfulness rule** | Real bodies use real data and real imagery (NASA, ESA, USGS). Anything modelled rather than observed says so on its card. |

**Design pillars:** direct touch; your room is the stage; scale you can feel; every object teaches; no dead ends.

---

## 2. The gameplay flow

### 2.1 Launch and intro
1. **Platform is chosen automatically.** If a headset is active the app runs in Quest mode; otherwise it runs in desktop mode with the mouse as a pointer.
2. **Logo** (about 5 s), with the original music.
3. **Earth placement.**
   - *Quest:* a placement ball appears about 2 m ahead and half a metre down. Narration explains what to do; with hands tracked a short hand tutorial plays, otherwise the controller narration. After the first line you can force-pull the ball, then confirm. This anchors all content in your room.
   - *Desktop:* placement is skipped; content is released two seconds after the onboarding narration ends.
4. **The solar system, then the Milky Way grow in.** The intro ends on the galaxy and the app **lands in the Milky Way place** with the dock shown.
5. **Skipping.** Choosing any place while the intro runs skips it: content is anchored 2 m ahead and the chosen place opens as soon as the galaxy has loaded. If the intro cannot yet be skipped, a notice says *"Just a moment – the introduction is still running…"*. A 45-second watchdog ends the intro if it stalls.

### 2.2 A typical session
Milky Way map → hover the **Crab Nebula** marker to see its picture card, pinch it → you stand inside the Crab → dock: **The Planets** → pull **Saturn**, grow it with two hands, pull **Titan** from its orbit and read its card → tap Saturn to send it home → dock: **Solar System** → pull **Pluto**, then **Charon** → dock: **Galactic Center** → walk round the black hole → summon the **Cosmic Being** and ask *"why does Saturn have rings?"* → *"take me to the Cosmic Web"*.

### 2.3 Leaving
There is no end state. Quit from the Settings window (two taps). Recenter tidies everything back to its layout at any time.

---

## 3. The places

The dock holds seven places. Another eleven places are **destinations**, reached from inside a place (or by asking the Being). Every place opens with a **grow-in** (0.6 s, from a point), sets the room lighting, shows its **scene panel** (title, paragraphs, instruction), and plays its narration.

**Room lighting (environment modes):**

| Mode | What you see |
|---|---|
| **Passthrough** | Your room as it is. |
| **Dimmed** | Your room at about half brightness behind the content. |
| **Full Black** | Your room is gone; a star field surrounds you. |

> **Important:** the dock's passthrough button now **starts on** (owner's choice, 27 Sep), which keeps your room visible in every place until you turn it off. Turn it off to see each place's own lighting (for example, the Galactic Center and Cosmic Web in full black).

### 3.1 Dock places (in dock order; desktop keys F2–F8)

| Key | Tile (second line) | Lighting | What it is |
|---|---|---|---|
| F2 | **Cosmic Web** (THE LARGEST SCALE) | Full Black | You stand inside the largest structure in the universe. |
| F3 | **Galaxies** (A SKY FULL OF THEM) | Full Black | A deep field of galaxies around you, with pins to four real galaxies. |
| F4 | **Milky Way** (OUR GALAXY) | Dimmed | Our galaxy as a map, with markers to nine destinations. |
| F5 | **Andromeda** (M31) | Dimmed | Our neighbour galaxy, held in the room. |
| F6 | **Solar System** (THE ORBITS) | Dimmed | The orbit model: planets moving on their orbits, every moon selectable. |
| F7 | **The Planets** (SIDE BY SIDE) | Dimmed | Every body at arm's reach; grab, grow, compare. Two layouts. |
| F8 | **Galactic Center** (SAGITTARIUS A\*) | Full Black | The black hole at the heart of the Milky Way. |

#### Cosmic Web
- **You see:** 120,000 glowing violet points arranged the way matter is in the universe. The empty voids are the cells; matter sits on their walls (sheets), edges (filaments) and corners (clusters). The whole volume drifts very slowly. You are inside it.
- **You can:** look and walk around. Grab it with **two hands** to turn it or change its size.
- **Panel:** *"Grab with two hands to turn the web or change its size."*
- **On desktop:** opens exactly as in the headset (you start inside it); left-drag looks around.

#### Galaxies
- **You see:** 300–500 galaxy images (spirals, ellipticals, edge-on discs) filling a sphere around your head, drifting about 1° a second. You start in the middle.
- **Galaxy pins:** four labelled pins at each galaxy's real direction in the sky, with its portrait: **Andromeda (M31)**, **Whirlpool (M51)**, **Pinwheel (M101)** and **Triangulum (M33)**. Choosing one opens that galaxy.
- **Galaxy bar (desktop):** a row across the top of the screen with the same four live galaxies plus seven names marked as coming (NGC 1300, NGC 4565, Sombrero, M87, Centaurus A, LMC, Antennae). *It is screen-space, so it is not visible in the headset yet.*
- **Panel:** *"Use the passthrough button to put them in your room or in the dark."*

#### Milky Way (the destination map)
- **You see:** a spinning particle galaxy with a warm core and blue-white arms, and **nine markers** in the original Galaxy Explorer style, each an angled leader line up to a name.
- **Seven nebula markers:** Crab, Pillars of Creation, Homunculus, NGC 1501, Trumpler 14, Helix, Orion.
  - **Hover** (hand ray or mouse): a picture card opens at the marker's foot with the photograph, distance from the Sun, size and age. It closes a moment after you leave.
  - **Pinch / click:** travels to that nebula.
- **Two large markers:** **SOLAR SYSTEM / OUR NEIGHBORHOOD** and **GALACTIC CENTER / BLACK HOLE** open those places.
- **Panel:** *"Point at a label and pinch to open that destination."*
- *The map itself cannot be moved or resized by hand.*

#### Andromeda (M31)
- **You see:** a second particle galaxy (17,200 points in three layers): flatter than the Milky Way, a white core, a reddish dust ring, bluer outer arms.
- **You can:** move it with one hand; turn and resize it with two (0.5–3 m).

#### Solar System (the orbit model)
- **You see:** the Sun with the planets and Pluto on thin teal orbit lines, planets moving along them, with name labels and an asteroid belt.
- **Pull a planet:** it flies to you, its card opens, its narration plays, and **its moons appear** around it.
- **Pull a moon** of the planet you are holding: the moon comes out with its own card and narration. The planet stays out beside it (a planet and its moons count as one family, so pulling one never sends the other home).
- **Moons in this scene:** Earth — Moon; Mars — Phobos, Deimos; Jupiter — Io, Europa, Ganymede, Callisto; Saturn — 7; Uranus — 5; Neptune — 3; Pluto — 5 (see §4.2).
- **Panel:** *"One hand moves the model, two hands tilt or resize it."* — *note: the whole model cannot be moved or resized yet (CS-045).*
- **On desktop:** the whole system opens centred with the outer orbits reaching the edges of the screen.

#### The Planets (Solar Row / Relative Size)
- **You see:** the Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune and Pluto in one straight line facing you, each rotating on its true axial tilt. Earth has clouds and atmosphere; Saturn and Uranus have rings; the Sun has an animated surface and flares.
- **Two layouts** (the tile's pop-up):
  - **Solar Row** — *"One line, all the same size."* Every body the same size, side by side.
  - **Relative Size** — true proportions: the Sun is enormous (it stands at the edge of the view), Jupiter a beach ball, Earth fits in your palm, Pluto a pea. Small bodies keep a larger invisible grab sphere so you can still pinch them.
  - Switching animates everything back into the chosen layout over 0.8 s.
- **Interactions:** everything in §4.1. All 27 moons are here too.
- **On desktop:** the row runs from one edge of the screen to the other; the scene panel sits above it.

#### Galactic Center (Sagittarius A\*)
- **You see:** a black hole — a dark shadow ringed by a bright photon ring, with an accretion disc that bends up and over the shadow (gravitational lensing), warm orange to white — in a dense star field. Nearby stars S2 and S102 are marked.
- **You can:** walk around it; the lensing changes with your viewpoint.
- **Panel:** *"Walk around it. Two hands change its size."* — *note: resizing is not wired in this scene.*

### 3.2 Destinations (no dock tile)

| Destination | Reached from | Lighting | What it is |
|---|---|---|---|
| **Crab Nebula** (supernova remnant, 6,500 ly) | Milky Way marker | Full Black | You stand inside it |
| **Helix Nebula** (planetary nebula, 650 ly) | Milky Way marker | Full Black | " |
| **Homunculus Nebula** (eruption cloud, 7,500 ly) | Milky Way marker | Full Black | " |
| **NGC 1501** (4,240 ly) | Milky Way marker | Full Black | " |
| **Orion Nebula** (star-forming region, 1,344 ly) | Milky Way marker | Full Black | " |
| **Pillars of Creation** (6,500 ly) | Milky Way marker | Full Black | " |
| **Trumpler 14** (star cluster, 8,980 ly) | Milky Way marker | Full Black | " |
| **Whirlpool Galaxy (M51)** | Galaxies pin / bar | Dimmed | A galaxy in the room; two-hand turn and resize |
| **Pinwheel Galaxy (M101)** | Galaxies pin / bar | Dimmed | " |
| **Triangulum Galaxy (M33)** | Galaxies pin / bar | Dimmed | " |
| **HD 110067** (SIX WORLDS IN STEP) | **Only by asking the Cosmic Being** | Dimmed | A real star with six planets in resonance, in *Row* and *Relative Size* layouts, pullable like the Planets |

**Inside a nebula:** the nebula's centre is placed on your head, so you are standing in it.
- A far sky sphere carries the photograph.
- A gas shell a few metres across stays fixed in the room, so you get depth as you move.
- About 28,000 baked 3D points form the cloud.
- Every nebula except the Pillars has a central star with diffraction spikes.
- The panel's last paragraph says honestly: *"What surrounds you is an impression built from the photograph…"*.

You can resize the nebula with two hands.

---

## 4. Everything you can touch

### 4.1 Planets and the Sun (The Planets, Solar System, HD 110067)

| Action | Quest | Desktop | What happens |
|---|---|---|---|
| **Pull** | Pinch it; or point the hand ray at it for 2 s (a tractor beam fills) | Click it | It flies out to you (desktop: in front of the camera), grows, its **card** opens, its **narration** plays, its **moons** fade in orbiting it. |
| **Make room** | automatic | automatic | If another pulled body is already where this one arrives, that one steps aside so they never overlap. |
| **Hold and move** | Pinch and move | Left-drag it | It follows smoothly. |
| **Resize** | Two hands apart/together | Mouse wheel over it | 5 cm to 3 m. Past about 1.5 m, Saturn's rings surround you. |
| **Turn** | Two hands | Right-drag it | Free rotation. |
| **Let go** | Release | Release | It stays exactly there, still rotating, card and moons with it. |
| **Send home** | Tap it (a pinch released within 0.4 s without moving it) | Click it (or press its number key again) | A planet returns to its layout place with its moons; a moon returns to its orbit. |
| **Brush** | Sweep a fingertip or ray across it | Sweep the mouse across it | It turns a little with the stroke and springs back. Works on every body, moon and the Being. |
| **Touch the Sun** | Put a hand inside it | Hover it | Its surface brightens (a rumble is designed but not yet sourced). |
| **Restore** | Recenter button | `R` / `Home` | Everything returns to the layout. |

**Strays:** a body you leave more than 4 m away or below the floor for 5 s returns home by itself. Otherwise nothing ever snaps back on its own.

**Keyboard pulls (desktop):** `1`–`9`, `0` = Sun, Mercury … Pluto; `M` = the Moon. Press again to send it home.

### 4.2 Moons (27)

| Planet | Moons |
|---|---|
| Earth | Moon |
| Mars | Phobos, Deimos |
| Jupiter | Io, Europa, Ganymede, Callisto |
| Saturn | Mimas, Enceladus, Tethys, Dione, Rhea, Titan, Iapetus |
| Uranus | Miranda, Ariel, Umbriel, Titania, Oberon |
| Neptune | Triton, Proteus, Nereid |
| Pluto | Charon, Styx, Nix, Kerberos, Hydra |

- Moons are hidden until their planet is pulled, then orbit it with a small name label (large and bold while held).
- Uranus's moons orbit its 98°-tilted equator; Pluto's orbit its 120° one; Saturn's its 26.7° one.
- **Shapes:** Pluto's small moons (Nix, Hydra, Kerberos, Styx) are not round. They are stretched to their measured New Horizons axes, and Kerberos is two-lobed.
- **Surfaces:**
  - **Real maps:** Moon, Phobos, Deimos, the Galilean moons, Titan, Mimas, Enceladus, Iapetus, Rhea, Dione, Tethys (NASA/USGS).
  - **Real maps with generated fill** for the regions no spacecraft photographed: Charon, Triton.
  - **Generated surfaces**, labelled as such in the credits because no global map exists: Miranda, Ariel, Umbriel, Titania, Oberon, Proteus, Nereid, Styx, Nix, Kerberos, Hydra.
- Each moon has its own narration (same narrator voice as the new place narrations).

### 4.3 Other things you can touch
- **Galaxies (Andromeda, M51, M101, M33), Cosmic Web, nebulae:** one hand moves, two hands turn and resize, each within its own limits. They stay where you leave them; **Recenter / `R`** eases them back over 0.8 s.
- **Milky Way markers:** hover for a card, pinch or click to travel.
- **Galaxy pins:** pinch or click to open that galaxy.
- **The Cosmic Being:** grab and carry it, brush it, tap it (§6).
- **Name labels:** `L` shows or hides all body name labels (desktop).

---

## 5. Cards and panels

All cards float beside what they describe, face you, keep a constant physical size, fade in over 0.35 s, and use white outlined text with no backing plate, so they read over your room. Their rows are laid out from measured text heights, so long titles never collide with the text below.

| Card | Shows | When |
|---|---|---|
| **Planet / Sun** | Title, subtitle in caps, a paragraph, a 2×2 grid: **Diameter, Mass** (true superscript, e.g. 5.97 × 10²⁴ kg), **Orbital Period, Day Length** | While the body is out |
| **Moon** | Title; **MOON OF \<PLANET\>** and **DISCOVERED \<year, by whom\>** on the caps line; a two-sentence description; a 2×2 grid: **Diameter, One Orbit, Distance from \<planet\>, Surface Gravity** | While the moon is out; its narration plays when it opens |
| **Scene panel** | Place title, paragraphs, an instruction line | After each place grows in; on desktop, moved to the top of the view if the place fills the screen |
| **Milky Way picture card** | Photograph, distance, size, age | While hovering a nebula marker |

**Text size** is ×1.0, ×1.25 or ×1.5 in Settings; it scales the whole card so the layout holds.

---

## 6. The Cosmic Being (voice guide)

**What it is.**
- A 20 cm sphere of blue points of light, like the intro Earth's hologram without the Earth.
- It floats about 1.4 m away, a little left of centre and a little below eye level, and follows you gently.
- It speaks with a natural voice (OpenAI Realtime, voice *marin*) and understands English.

**Summon / dismiss:** the Being button on the dock (either dock), or `C` on the desktop. It fades in and out over 0.6 s.

**Its states, each with its own look:**

| State | Look | Meaning |
|---|---|---|
| **Idle** | Slow spin, faint rim | Waiting |
| **Listening** | A breathing brighter rim that reacts to your voice | Talk now |
| **Thinking** | Fast spin, pulsing rim | Working out an answer |
| **Speaking** | The points fall inward in time with its voice; rim bright with loudness | Answering |

**Flow.**
- **Arrival:** it greets you out loud, then listens for **3 s**, then goes idle if you say nothing.
- **Tap it** (pinch, poke or click, short and still):
  - when idle, it starts listening without speaking (a soft chime);
  - when listening, it stops;
  - when thinking or speaking, it is interrupted and listens again.
- **Speaking to it:** stop talking and it answers.
- **While it is awake:** music and ambience drop to 55 % and the narrator stops, so it is the loudest thing in the room.
- **Echo guard:** it does not hear itself; the microphone reopens 0.6 s after it stops speaking.

**What it can do for you (tools):**

| Say | It does |
|---|---|
| *"Take me to the Crab Nebula / Andromeda / HD 110067…"* | Opens any of the 18 places, including ones with no tile |
| *"Show me Saturn"* | Pulls that body out of the row (The Planets, HD 110067) |
| *"Put everything back"* | Restores the layout |
| Any question about space or what is in front of you | Answers from its knowledge of every place and body in the app |

**Handling:** grab and carry it anywhere; it stays where you leave it. Brush it and it turns and springs back.

**Microphone:** choose the device in Settings; a live level bar shows it hears you.

**Offline:** with no network or key it plays its recorded greeting and waits.

---

## 7. The dock and settings

### 7.1 The dock
| | Quest (world dock) | Desktop (screen dock, bottom-right) |
|---|---|---|
| **Where** | 0.75 m ahead at about chest height, tilted 25° toward you; it does not follow your head | Bottom-right of the screen |
| **Tiles** | Seven place tiles, each a picture of the place with its name; the open place is underlined in cyan | Same seven tiles |
| **Layout pop-up** | The Planets tile offers **Solar Row / Relative Size**, 40 mm above the tile; the active one is filled cyan | Same |
| **Buttons** | Passthrough (room on/off), Recenter, Help, Settings, Being | Passthrough, Recenter, Settings, Help, Being |
| **Move it** | Pinch the drag bar under it; it re-tilts to face you on release | — |
| **Show / hide** | Hold your **left palm up** for 0.5 s | `Tab` (remembered) |

- **Recenter:** re-parks the dock in front of you and brings every moved thing back to its layout.
- **Help:** replays the two hint cards: *"Pinch to grab"* and *"Two hands to resize"*. On desktop it also opens the controls overlay.

### 7.2 Settings window
Opens from the Settings button (or `U` on desktop): beside the dock in the headset, in front of the camera on desktop.
- **Scale:** resize the whole current place, 0.5×–2×.
- **Sound:** *Sound on / Mute* and *Narration on / off* (both remembered).
- **Text size:** ×1.0 / ×1.25 / ×1.5 (remembered).
- **Microphone:** ‹ System default / each device › with a live level bar and *"Speak to check it…"* (remembered).
- **Quit Cosmic Simulation:** takes two taps within 3 s.

### 7.3 Legacy hand menu (Quest)
The original Galaxy Explorer hand menu still appears when your palm faces you. It offers **About** (credits and licences), **Back**, **Mode** and **Reset**. *Back is slated for removal (CS-111).*

---

## 8. Controls reference

### 8.1 Quest 3 (hands; controllers use the same ray and select)
| Action | Gesture |
|---|---|
| Grab / pull near | Pinch on the object |
| Pull from afar | Point the hand ray at a body; pinch, or hold 2 s |
| Move | Hold the pinch and move |
| Resize and turn | Pinch with both hands, move apart or twist |
| Send a body home | Tap it (quick pinch, no movement) |
| Press a tile or button | Poke it with a fingertip, or ray + pinch |
| Show / hide the dock | Left palm up, 0.5 s |
| Hover a marker or pin | Point the ray at it |

### 8.2 Desktop (mouse and keyboard)
| Input | Does |
|---|---|
| Left click | Press, travel, pull a body in front of the camera; on a pulled body, send it home |
| Left drag | On a pulled body: move it. On empty space: orbit the view round the place (inside a place you are surrounded by, look around) |
| Right drag | On a pulled body: spin it. On a galaxy / web / nebula: turn it. Else: pan the view |
| Wheel | Over a pulled body: resize (0.1–3×). Over a galaxy / web / nebula: resize within its limits. Else: fly forward/back |
| Hover | Highlight; open marker cards; stands in for a hand inside the Sun |
| `F2`–`F8` | The seven places in dock order |
| `1`–`9`, `0` / `M` | Pull or send home Sun…Pluto / the Moon |
| `R` | Restore the layout and everything moved |
| `Home` | Recenter: reset the camera, re-park the dock, restore moved content |
| `C` | Summon / dismiss the Cosmic Being |
| `L` | Show / hide name labels |
| `P` | Passthrough preview (the room dim and black backdrop) |
| `Tab` | Show / hide the dock |
| `U` | Settings window |
| `H` / `F1` | Controls overlay (*its text is out of date — CS-089*) |
| `Esc` | Close the topmost thing: overlay, pop-up, destination, panels, cards |

On desktop every place opens **centred and sized to the screen**: rows run edge to edge; models and galaxies fill the view; places you stand inside (Galaxies, Cosmic Web, nebulae) are left around you.

---

## 9. Sound

| Layer | Behaviour |
|---|---|
| **Music** | One bed per room lighting (room / dimmed / dark), crossfaded over 2 s when the lighting changes; ducks to 55 % under narration and while the Being is awake. |
| **Narration** | A narrator introduces each place after it grows in (The Planets place itself has none) and each body or moon when pulled; replaces anything queued; stops when you switch. Narration can be turned off on its own. |
| **Body ambience** | A pulled body plays its own quiet 3D ambience. |
| **Place ambience** | Designed per place; **no beds are assigned yet** (CS-077). |
| **Interface** | Hover ticks, presses, grabs, releases, force-pull beam, dock and pop-up sounds. The grow-in whoosh and the Sun's touch rumble have no clip yet (CS-077). |
| **The Being** | Its own streamed voice, louder than everything else; soft chimes for listen and close. |

---

## 10. Honest status: what is not there yet

| Item | Status |
|---|---|
| **Quest testing** | Everything above is verified in the Unity editor on desktop. No full device pass yet (Quest build, performance, per-eye rendering, the Being's network on Android). |
| **API key in the build** | The Being's OpenAI key would be packed into the APK; needs a short-lived key service before a public release (see `SYSTEM_OVERVIEW.md` §0). |
| **Hint cards on first run** | Only shown from Help; not yet shown automatically (CS-074). |
| **Solar System and Galactic Center resizing** | Their instructions promise it; not wired (CS-045). |
| **HD 110067** | Reachable only through the Being; no tile or marker. |
| **Galaxy bar in the headset** | Desktop only. |
| **Place ambience, whoosh, Sun rumble** | No clips (CS-077). |
| **Controls overlay text** | Out of date (CS-089). |
| **Being** | Occasional one short pause mid-answer; one rare error on very quick place switches. |
| **Passthrough on by default** | Hides the dark places' own lighting until toggled. |
