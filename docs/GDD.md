# Cosmic Simulation XR — Game Design Document

*Version 1.1 — 12 September 2026. Companion documents: `docs/TECHNICAL_OVERVIEW.md` (how it is built) and `docs/COSMIC_SIMULATION_XR_ROADMAP.md` (when). This document is the contract for the experience: if a behaviour is not described here, it is not in scope.*

> **Scope changed on 12 September 2026, by the owner, deliberately.** Version 1.0 of this document was a *one-to-one* contract with the COSMIC XR reference app, and that clause is now **lifted**. The app is no longer bounded by the reference's seven places. It is being extended with other galaxies and other real planetary systems as places the player can see and travel to — see **section 4.8** and `docs/decisions.md` **D-010**.
>
> Two things survive the change and are not negotiable by it. **Truthfulness:** new places are built from real published data, and where an appearance is modelled or an artist's impression rather than observed, the copy says so. **Our own words and art only:** nothing is taken from the reference app.
>
> The "not described here, not in scope" rule still holds — it is what keeps the project honest. It now means *extend this document first*, not *do not extend the app*. A future session finding content beyond the original seven should read this note rather than treat it as scope creep and remove it.

---

## 1. Overview

| | |
|---|---|
| **Title** | Cosmic Simulation XR |
| **One line** | Touch the universe in your room: pull the planets out of the sky, hold the Moon, stand inside a nebula, and walk around a black hole — in your own space, with your own hands. |
| **Platform** | Meta Quest 3 / 3S (primary, hand tracking, passthrough). Windows desktop (secondary, mouse and keyboard, for testing and demos). |
| **Genre** | Educational mixed-reality exploration. No fail states, no scoring. |
| **Session** | 5–20 minutes. Everything is reachable within two interactions. |
| **Audience** | Curious adults and families; classroom demos; XR showcases. Reading age 12+. |
| **Reference** | The COSMIC XR promo video (86 s), decomposed shot by shot. We match its scenes, objects, interactions, navigation and information layout. We do not use its name, logo, artwork or written text. |
| **Lineage** | Built on Microsoft's open-source Galaxy Explorer (MIT), ported to Quest 3 (branch `quest3-port`). Credit retained in About. |

### 1.1 Design pillars

1. **Direct touch.** Nothing is behind a controller abstraction. Pinch to take, two hands to grow, poke to choose.
2. **Your room is the stage.** Passthrough first. Objects live at real scale in the player's space; the room dims or vanishes only when the subject needs darkness.
3. **Scale you can feel.** A planet is 15 cm in your hand or 3 m across in your living room. Relative Size shows the truth.
4. **Every object teaches.** Pick anything up and it tells you what it is, in one paragraph and four numbers.
5. **No dead ends.** The dock takes you anywhere in one poke. Released objects stay where you put them; one button tidies up.

---

## 2. Player journey

### 2.1 First launch (the original intro, extended)
1. The app opens in passthrough with the original Galaxy Explorer intro: the logo appears, then the player places the Earth pin on their floor (this anchors all content in the room), then the Milky Way grows in. Narration and music are the original recordings.
2. After placement, two **hint cards** appear once, one after the other, in the intro's visual style: **"Pinch to grab"** (animated hand pinching a planet) and **"Two hands to resize"**. Each dismisses on doing the action or after 6 s.
3. The dock fades in at waist height. The player is in the **Milky Way** experience with everything reachable from the dock.
4. Hints are not shown again (stored preference); they can be replayed from the dock's **Help** button. The intro itself plays on every launch, as in the original, and can be skipped by pinching the logo.

### 2.2 A typical session
Pull Earth out of the row → it grows, the Moon appears orbiting it, Earth's panel opens → scale Earth to a metre with two hands → grab the Moon and read its label → poke **Sagittarius A\*** on the dock → the room goes black and the black hole appears → walk around it → poke **Solar System** → tilt the orbit model with both hands → poke **Cosmic Web** → stand inside the filaments.

### 2.3 Leaving
There is no end state. Closing the app or poking the dock's **Recenter** restores the default layout next time.

---

## 3. World structure

### 3.1 The dock (hub)
A single floating strip of seven tiles is the only navigation. Poking a tile switches the active experience. Two tiles carry a two-option pop-up. The dock also holds the passthrough toggle and three utility buttons. See §8.1.

### 3.2 The seven experiences

| # | Experience | Room state | One-line purpose |
|---|---|---|---|
| 1 | Cosmic Web | Full black | Stand inside the largest structure in the universe |
| 2 | Galaxies | Black (toggle to room) | A deep field of galaxies scattered through your space |
| 3 | Milky Way | Dimmed room | Our galaxy as a map, with labelled destinations you can open |
| 4 | Andromeda | Dimmed room | Our neighbour galaxy, held in the room |
| 5 | Solar System | Dimmed room | The orbit model: order, motion, rings, labels |
| 6 | Solar System Planets | Dimmed room | Every body at arm's reach; grab, grow, compare |
| 7 | Sagittarius A\* | Full black + stars | Face the black hole at the galaxy's centre |

### 3.3 Navigation rules
- Switching experiences unloads the previous one; objects the player pulled out are returned as part of the unload.
- Each experience appears with a **grow-in** (scale from a point over 0.6 s, ease-out). No fly-through transitions.
- The dock stays where it is across switches. The player can move it by its grab bar; **Recenter** brings it back in front of them.
- Destinations opened from the Milky Way (nebulae) are **overlays** within the Milky Way experience, not separate experiences; closing one returns to the map.

### 3.4 Environment modes

| Mode | What the player sees | Used by |
|---|---|---|
| **Passthrough** | The room as it is | Onboarding; Galaxies when toggled |
| **Dimmed room** | The room at ~50 % brightness | Milky Way, Andromeda, Solar System, Solar System Planets |
| **Black halo** | Dimmed room plus a soft black disc behind the object | Nebula overlays |
| **Full black** | Star field, no room | Cosmic Web, Galaxies (default), Sagittarius A\* |

The **passthrough toggle** on the dock forces Passthrough or returns to the experience's default. Fades take 0.4 s.

---

## 4. Experiences in detail

Each experience is specified by: what you see, placement and scale, objects, interactions, panel, audio, desktop behaviour, and acceptance criteria.

### 4.1 Solar System Planets (Solar Row / Relative Size)

**What you see.** Ten bodies in a gentle arc at chest height: Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto. In **Solar Row** every body is 15 cm in diameter, 25 cm apart centre-to-centre, 1.2 m above the floor, on an arc of radius 1.1 m centred on the player's start position. In **Relative Size** the bodies keep their order but take true proportions with the Sun at 3.0 m (Jupiter 30 cm, Saturn 25 cm, Uranus 11 cm, Neptune 10.6 cm, Earth 2.75 cm, Venus 2.6 cm, Mars 1.5 cm, Mercury 1.05 cm, Pluto 0.5 cm), spaced so no body overlaps; small bodies keep a 6 cm invisible grab sphere so they can still be pinched.

**Objects.** All ten bodies rotate slowly on their true axial tilt. Earth has a cloud layer and atmosphere rim; Saturn and Uranus have rings; the Sun has animated surface, rim flares and a glow. Moons are hidden until their planet is pulled out.

**Interactions.**
- **Pinch a body** (near or by hand ray): it lifts out of the row toward the hand, grows to 25 cm, its **panel** opens beside it, and its **moons** fade in orbiting it with small labels.
- **Hold and move**: the body follows the hand with light smoothing.
- **Two hands**: scale between 5 cm and 3 m and rotate freely. Scaling Saturn past 1.5 m lets its rings surround the player.
- **Release**: the body stays exactly where it was let go, still rotating. Its panel and moons stay.
- **Pinch a moon**: it lifts out; its label switches to the large style; its own short panel opens.
- **Touch the Sun** (hand inside its surface): the surface brightens and a low rumble swells while the hand stays.
- **Pop-up (Solar Row / Relative Size)**: all bodies animate back into the chosen layout over 0.8 s; moons return to their planets; panels close.
- **Recenter**: same as re-selecting the current layout.

**Panel (body variant).** Title, one paragraph (≤ 55 words), and four stats: Diameter, Mass, Orbital Period, Day Length. See §6 for values and §8.3 for layout.

**Audio.** Grab, hold, release sounds; narration clip for the body starts on pull (replayable); the body's ambience loops quietly while it is out; the Sun's touch rumble.

**Desktop.** Bodies in the row are clickable: click pulls in front of the camera; drag moves; right-drag spins; wheel scales; `1–9, 0` pull Sun→Pluto; `M` the Moon; `R` restores the layout; the HUD dock offers the two layouts.

**Acceptance.** Ten bodies present and named; both layouts measurable to the numbers above; every body and listed moon can be pulled, moved, scaled, released and stays; panels show correct copy and numbers; layout restore returns everything.

### 4.2 Solar System (orbit model)

**What you see.** The Sun at the centre with eight planets and Pluto on thin teal elliptical orbit rings, each planet a small sphere (1–3 cm) with a floating name label; an **Asteroid Belt** label between Mars and Jupiter; the whole model about 1.6 m across, tilted 20° toward the player, 1.0 m in front at 1.1 m height. Planets move along their orbits at a visible pace (Earth completes an orbit in ~60 s; others scaled by true period).

**Modes (pop-up).** **Schematic**: even orbit spacing, planets enlarged for visibility. **Realistic**: true relative orbit radii (Neptune at 30× Earth's) with the model auto-fitted to 2.4 m; inner planets cluster near the Sun. Switching animates over 1.0 s with the existing transition sounds.

**Interactions.** One hand moves the whole model; two hands tilt, rotate and scale it (0.4–4 m). Pinching a planet on its orbit opens that planet's panel and highlights its orbit; it does not detach it (detaching belongs to the Planets experience). Labels face the player.

**Panel (scene variant).** "Solar System" — two paragraphs and an instruction paragraph ("Grab with one hand to move, two hands to tilt or resize. Use the dock to switch between schematic and realistic spacing.").

**Audio.** Soft orbit-model ambience; label focus tick; mode-switch transition sounds.

**Desktop.** Left-drag orbits the view, right-drag pans, wheel zooms; click a planet for its panel; HUD dock pop-up switches mode.

**Acceptance.** All ten bodies on rings with labels; belt label; both modes; two-hand manipulation within limits; panel copy.

### 4.3 Milky Way (destination map)

**What you see.** A particle spiral galaxy 1.4 m across, tilted 55°, floating at 1.2 m height, 1.2 m in front, with a warm yellow core and blue-white arms. Six **destination tags** float above points on the disc with thin leader lines: **Helix Nebula**, **Crab Nebula**, **Solar System**, **Galactic Center – Sagittarius A\***, **Homunculus Nebula**, **Orion Nebula**. Three further tags from the original app — **Pillars of Creation**, **NGC 1501**, **Trumpler 14** — ship on as well (decision of 11 Sep 2026); nine destinations in total.

**Interactions.**
- Hover a tag (hand ray or near finger): it grows 15 % and turns cyan; a focus tick plays.
- Pinch a tag: **Solar System** and **Galactic Center** switch to those experiences. Nebula tags open a **nebula overlay**: the galaxy fades to 20 %, a black halo appears 1.0 m in front of the player, and the nebula grows inside it from a point to ~70 cm across. A close pinch anywhere outside the halo, or the dock, returns to the map.
- One hand moves the galaxy; two hands tilt and scale (0.5–3 m).

**Nebula overlay.** A layered, slowly rotating 3D cloud (see Technical Overview §7.6) with its own short panel (title + 2 paragraphs). Grabbable and scalable like a body. Four nebulae ship: Helix, Crab, Homunculus, Orion.

**Panel (scene variant).** "Milky Way" — two paragraphs and an instruction ("Point at a label and pinch to open a destination.").

**Audio.** Galaxy ambience; tag hover tick; nebula grow-in whoosh; narration for the galaxy and each nebula.

**Desktop.** Hover/click tags with the mouse; overlay closes with `Esc` or click outside.

**Acceptance.** Six tags, correct positions, hover and open behaviours; four nebula overlays open and close; galaxy manipulable.

### 4.4 Andromeda

**What you see.** A second particle galaxy, 1.2 m across, flatter (tilt 75°), white core, warm reddish dust ring, bluer outer arms, growing in from a point. Panel to its right.

**Interactions.** Move, tilt, scale with hands (0.5–3 m). No destinations.

**Panel.** "Andromeda (M31)" — two paragraphs and an instruction.

**Audio.** Distinct ambience; narration.

**Acceptance.** Distinguishable from the Milky Way at a glance; manipulation; panel.

### 4.5 Sagittarius A\* (galactic centre)

**What you see.** Full black with a star field. A black hole 0.9 m across at eye height, 1.5 m in front: a dark shadow ringed by a bright photon ring, an accretion disc that bends up and over the shadow (gravitational lensing), warm orange to white. Panel to the right.

**Interactions.** Hand ray hover highlights nothing (it is not grabbable) but the panel follows the player's side. The player walks around it; the disc lensing updates with viewpoint. Two-hand scale is allowed (0.4–2.5 m).

**Panel.** "Galactic Center – Sagittarius A\*" — two paragraphs.

**Audio.** Deep drone ambience; narration.

**Desktop.** Orbit the view; wheel zoom.

**Acceptance.** Lensing visible from all sides; black environment; panel.

### 4.6 Galaxies (deep field)

**What you see.** 300–500 small galaxy sprites (spirals, ellipticals, edge-on discs; 20+ unique images, random tint and rotation) filling a 6 m sphere around the player, sizes 3–25 cm, with a slow collective drift (1°/s). Default room state is full black; the passthrough toggle shows them in the room.

**Interactions.** Look around; walk; passthrough toggle. Pinching a galaxy sprite gently pushes it (no grab).

**Panel.** "Galaxies" — two paragraphs; anchored 1.2 m in front at eye height and follows the player's turn with lag.

**Audio.** Airy ambience; narration.

**Acceptance.** Sprite count, spread and drift; toggle; panel.

### 4.7 Cosmic Web

**What you see.** Full black. A 3D network of glowing violet filaments and brighter nodes filling a 5 m volume around the player, faint particles between them, slow rotation of the whole volume (0.5°/min) and a subtle shimmer.

**Interactions.** Look and walk; two-hand rotate and scale of the volume (0.5×–2×).

**Panel.** "Cosmic Web" — two paragraphs.

**Audio.** Sub-bass ambience; narration.

**Acceptance.** Filament look at arm's length and at 3 m; performance target met (see Technical Overview §14).

---

## 5. Interaction design

### 5.1 Hand model (Quest)
- **Near pinch-grab** (index–thumb within 3 cm of an object): the object attaches to the pinch point; released on un-pinch.
- **Far pinch (hand ray)**: a ray from the hand shows a soft cursor; pinch selects. For grabbable bodies a far pinch **pulls** the object to the hand (existing force-pull with a 2 s dwell beam retained as the alternative: gaze-free dwell on the ray for 2 s also pulls).
- **Two-hand transform**: a second pinch on the same object switches to scale + rotate around the midpoint. Scale limits per object type (bodies 0.05–3 m; models 0.4–4 m; nebulae 0.3–2 m).
- **Poke**: fingertip pressing a dock tile, pop-up button, or onboarding card; visual press depth 4 mm; haptics none (hands).
- **Hover**: ray or fingertip proximity grows labels/tiles and plays a tick.
- **Release**: object stays; small settle sound.

### 5.2 Rules
- Many objects may be out at once. Nothing snaps back on its own.
- **Restore** (layout pop-up, Recenter, or `R`) returns the current experience's objects to their layout.
- Switching experience restores the previous one.
- Objects that leave a 4 m radius from the player, or drop below the floor, are restored automatically after 5 s.

### 5.3 Desktop model (mouse + keyboard)

| Action | Input |
|---|---|
| Select / open / pull | Left click |
| Move held object | Left drag |
| Spin object | Right drag on it |
| Scale object | Wheel over it |
| Orbit view | Left drag on empty |
| Pan view | Right drag on empty |
| Zoom view | Wheel on empty |
| Pull Sun … Pluto | `1`–`9`, `0` |
| Pull the Moon | `M` |
| Show / hide name labels | `L` |
| Restore layout | `R` |
| Recenter view | `Home` |
| Passthrough toggle (preview) | `P` |
| Dock show/hide | `Tab` |
| Help overlay | `H` / `F1` |
| Utility window (scale, mute, narration, text size) | `U` |
| Close panel / overlay | `Esc` |
| Switch experience | HUD dock click or `F2`–`F8` |

---

## 6. Object catalogue

### 6.1 Bodies (panel stats; NASA planetary fact sheets)

| Body | Diameter | Mass | Orbital period | Day length | Moons shown |
|---|---|---|---|---|---|
| Sun | 1,392,700 km | 1.989 × 10³⁰ kg | 230 million yr (galactic) | 25.4 days (equator) | — |
| Mercury | 4,879 km | 3.30 × 10²³ kg | 88 days | 58.6 days | — |
| Venus | 12,104 km | 4.87 × 10²⁴ kg | 225 days | 243 days | — |
| Earth | 12,742 km | 5.97 × 10²⁴ kg | 365.25 days | 24 hours | Moon |
| Mars | 6,779 km | 6.42 × 10²³ kg | 687 days | 24.6 hours | (Phobos, Deimos optional) |
| Jupiter | 139,820 km | 1.90 × 10²⁷ kg | 11.86 years | 9.9 hours | Ganymede, Callisto (+ Io, Europa optional) |
| Saturn | 116,460 km | 5.68 × 10²⁶ kg | 29.4 years | 10.7 hours | Titan, Mimas, Iapetus (+ Enceladus optional) |
| Uranus | 50,724 km | 8.68 × 10²⁵ kg | 84.0 years | 17.2 hours | — |
| Neptune | 49,244 km | 1.02 × 10²⁶ kg | 164.8 years | 16.1 hours | — |
| Pluto | 2,377 km | 1.31 × 10²² kg | 248 years | 6.4 days | — |

Body rotation speeds in-app: one visible turn per 60 s for planets, 120 s for the Sun; direction and tilt true (Venus retrograde, Uranus on its side).

### 6.2 Moons

| Moon | Parent | Diameter | Orbital period | In-app orbit radius (parent = 25 cm) |
|---|---|---|---|---|
| Moon | Earth | 3,474 km | 27.3 days | 45 cm |
| Ganymede | Jupiter | 5,268 km | 7.2 days | 40 cm |
| Callisto | Jupiter | 4,821 km | 16.7 days | 52 cm |
| Io (opt.) | Jupiter | 3,643 km | 1.8 days | 30 cm |
| Europa (opt.) | Jupiter | 3,122 km | 3.6 days | 34 cm |
| Titan | Saturn | 5,150 km | 15.9 days | 48 cm |
| Mimas | Saturn | 396 km | 0.9 days | 28 cm |
| Iapetus | Saturn | 1,469 km | 79 days | 60 cm |
| Enceladus (opt.) | Saturn | 504 km | 1.4 days | 32 cm |

Moon sizes when orbiting: 3–7 cm (scaled to parent, floor 3 cm). Held moon grows to 20 cm. Moon panel: name, parent, diameter, orbital period, one sentence.

### 6.3 Nebulae (Milky Way overlays)

| Nebula | Type | Distance | Source imagery |
|---|---|---|---|
| Helix Nebula (NGC 7293) | Planetary nebula | 650 ly | Hubble/ESA (CC BY) |
| Crab Nebula (M1) | Supernova remnant | 6,500 ly | Project texture + Hubble |
| Homunculus Nebula | Bipolar (Eta Carinae) | 7,500 ly | Project texture + Hubble |
| Orion Nebula (M42) | Star-forming region | 1,344 ly | Hubble/ESA (CC BY) |

Also shipped, from the original app: Pillars of Creation, NGC 1501, Trumpler 14 (existing textures and narration).

### 6.4 Galaxies
Milky Way (map), Andromeda (M31), and the Galaxies deep field (anonymous sprites).

### 6.5 Sagittarius A\*
Mass 4.3 million Suns; 26,000 ly; event horizon ≈ 25 million km; imaged 2022 (EHT).

---

## 7. Environment and atmosphere

- **Dimmed room:** black overlay at 50 % opacity, fades 0.4 s. Virtual objects are unaffected.
- **Black halo:** radial gradient disc (opaque centre → transparent edge) 1.6× the object's diameter, behind it relative to the player, always facing them.
- **Full black:** the app's VR mode with the star background; passthrough off.
- **Star field:** existing background stars for full-black scenes; density tuned per scene (Sagittarius A\* dense, Cosmic Web sparse).
- **Grow-in:** every experience and overlay scales from 0 → 1 in 0.6 s (cubic ease-out) with a soft whoosh.
- **Lighting:** bodies are lit by a virtual Sun direction consistent within an experience (in the Planets row the Sun object is the light source: bodies show a day/night side facing it).

---

## 8. UI design

All UI is designed in Figma (file *Cosmic Simulation XR — UI*) and exported; sizes below are physical (mm) for world-space UI and canvas units for the desktop HUD.

### 8.1 Dock
- **Placement:** 0.75 m in front of the player, 0.9 m above the floor, curved (radius 1.2 m), tilted 25° up toward the face. Recenter re-places it.
- **Tiles (7):** 110 × 62 mm, 4 mm apart. Each tile is a **picture of the place** (rendered from the scene) with its **name written on the image**, bottom-left, over a soft dark foot. Only the three tiles that offer a layout choice carry a second line: Solar System "Orbital view", Solar System Planets "Detail view", Galactic Center "Black hole". States: idle, hover (raise 6 mm, brighten), pressed (depress 4 mm), active (cyan underline along the bottom edge).
- **Order:** Cosmic Web · Galaxies · Milky Way · Andromeda · Solar System · Solar System Planets · Galactic Center.
- **Passthrough button:** a white tile closing the row, dark camera glyph over the word **Passthrough**; it reads as the one control that changes the room rather than the place.
- **Under the dock:** a 60 mm white drag bar centred below it moves the whole dock (it re-tilts toward the player), and three small square buttons sit at the right end — **Settings**, **Recenter** and **Help**. Mute lives in the utility window, which the Settings button opens.
- **Show/hide:** palm-up on the left hand for 0.5 s toggles the dock (Quest); `Tab` on desktop. The dock hides during onboarding cards.

### 8.2 Pop-ups
- Appear 40 mm above the tile that owns them, 240 × 90 mm, two equal buttons with icon + label: **Schematic / Realistic** (Solar System), **Solar Row / Relative Size** (Planets). The active option is filled cyan.
- A small **utility window** (120 × 102 mm) sits beside the dock and holds four controls and an × to close: a **scale slider** for the current experience, **Mute**, **narration-only mute**, and the **text size** (×1.0 / ×1.25 / ×1.5, §11). It opens from the settings button under the dock, or with `U` on the desktop, and closes with the dock. *(It was drawn at 120 × 50 mm for the slider alone; §11's three further controls do not fit in 50 mm of height, and the slider must reach every place, not only the three that offer a layout pop-up — so it is taller and belongs to the dock rather than to the pop-up.)*

### 8.3 Info panels
- **Body variant:** width 161 mm (230 units × 0.7 mm). Title 17 mm cap-height equivalent (24 units), subtitle small caps (8 units, cyan), paragraph 9.5 units, divider, 2 × 2 stat grid: label 6.5 units caps, value 12 units, unit suffix small. Mass renders as `5.97 × 10²⁴ kg` with a true superscript.
- **Scene variant:** same width; title, 2–3 paragraphs (last is an instruction in the secondary colour).
- **Moon variant:** 120 mm wide; title, one line of stats, one sentence.
- **Behaviour:** no background plate; text has a 1 px dark outline for legibility over passthrough; the panel sits on the side of the object nearer the player's view centre, 30 mm off its edge, faces the player, keeps constant physical size, fades 0.35 s. Several can be open.

### 8.4 Labels
- **Destination tag:** dark rounded card (radius 4 mm), **60 × 24 mm minimum**, carrying two lines — the name in white 5 mm, and under it a second line in caps, 3.6 mm, in the secondary ink; a thin leader line runs to the point. The card's **width is fitted to whichever line is longer** and never goes below 60 mm. Hover: +15 % scale, cyan fill, both lines darken. Selected: cyan outline.
  - *Changed from the original 60 × 16 mm single-line pill (v1.2, 12 Sep 2026).* Two of the inherited Milky Way markers — Solar System and Galactic Center — carried a name and a category line, the owner asked for that treatment on every destination, and those two legacy markers were then retired so nothing draws twice. Two lines do not fit in 16 mm.
  - **Every card above the disc sits on its own height**, ordered outermost-lowest in steps of a card height + 2 mm. The cards billboard, so two at the same height overlap whenever the player is near the line joining them, however far apart they are on the map.
- **Body name label (orbit model):** plain white text 6 mm, no pill.
- **Moon label:** small (5 mm text) under the moon while orbiting; large (18 mm bold) while held.

### 8.5 Desktop HUD
- Bottom-right dock mirror (7 tiles + passthrough preview + Recenter/Mute/Help), `Tab` toggles, first-run open.
- Controls overlay (`H`/`F1`) listing §5.3.
- About panel with backing plate: credits (Microsoft Galaxy Explorer, MIT), imagery credits, privacy link, version.

### 8.6 Hint cards
Two cards, 200 × 120 mm, shown once after the intro's Earth placement, each with a looping 3 s animation and one line of text; auto-advance on the action or after 6 s. Styled to match the original intro prompts: the same white line-art hand, cross-faded, over white outlined text with no plate. A card can also be dismissed directly — poke or pinch it in the headset, click it or press `Space`/`Esc` on the desktop — so a player who has understood it does not have to wait the six seconds out. Replayed from Help; the "seen" flag is a stored preference.

### 8.7 Visual system
- **Type:** Selawik (semilight for titles, regular body, semibold labels) — already in the project.
- **Colour:** white text `#FFFFFF`, secondary `#9EB8C4`, cyan accent `#6CCFDD`, tag/plate `#0E1418` at 80 %, orbit ring teal `#3FB6C9`.
- **Motion:** 0.35–0.8 s, cubic ease-out; nothing bounces.

---

## 9. Audio design

| Layer | Behaviour | Clips |
|---|---|---|
| **Music** | One ambient track per environment mode; crossfade 2 s | 3 existing tracks |
| **Ambience** | Per experience and per pulled body; loops; 3D for bodies | 12 existing (Sun, planets, Moon) + new for Cosmic Web, Galaxies, Andromeda, black hole |
| **Narration** | Starts when a body is pulled or an experience opens; replays each time; stops on switch; **Mute** silences all, narration toggle silences only voice | 22 existing destination clips; new: Andromeda, Cosmic Web, Galaxies, Helix, Orion, 6 moons |
| **UI** | Hover tick, select, poke press/release, dock show/hide, pop-up, panel open | existing `ui_*` set |
| **Interaction** | Grab, hold loop, release, force-pull beam, Sun touch rumble, grow-in whoosh | existing `ui_forcegrab_*`, `ui_tractor_beam`; new rumble + whoosh |

Spatialisation: Unity 3D panning (no plugin). Loudness target −16 LUFS music, −14 narration.

Which of the three tracks plays in which environment mode is not fixed here; the working mapping (four modes, three tracks, black halo and full black share one) is decision D-006, pending sign-off. Music ducks to 55 % while narration speaks — decision D-007, not a requirement of this document.

---

## 10. Copy and content

- **Tone:** plain, concrete, one striking fact per object. No exclamation marks. ≤ 55 words per body paragraph; scene panels ≤ 2 × 45 words plus a ≤ 20-word instruction.
- **Sources:** NASA planetary fact sheets, NASA/ESA mission pages. All copy is written by us; nothing is transcribed from the reference.
- **Location:** `docs/copy/*.md` (one file per experience; bodies and moons in `bodies.md`), imported into ScriptableObjects by an editor script. Writers edit markdown only.
- **Localisation:** English first; strings kept out of code so a second language is a copy folder.

---

## 11. Accessibility and comfort

- Seated and standing both supported; dock height adapts to the head height at recenter (0.55 × head height, min 0.7 m).
- No locomotion, no forced camera motion; grow-ins only.
- Left- and right-hand symmetric; every hand action has a ray alternative.
- Mute, narration-only mute, and a **text-size** setting (panels ×1.0/×1.25/×1.5) in the utility window. Text size scales the whole panel rather than the font alone, so the layout the panel was designed at is preserved. All three are remembered between sessions; the scale slider is not, since it describes a place rather than a preference.
- Passthrough available in every experience via the toggle for players who prefer to see the room.

---

## 12. Feature list (one-to-one contract)

| ID | Feature | Acceptance |
|---|---|---|
| F-01 | Dock with 7 tiles | All tiles switch experiences; active state shown |
| F-02 | Dock pop-ups | Schematic/Realistic and Solar Row/Relative Size work and animate |
| F-03 | Utility window | Scale slider and text-size setting affect the current experience |
| F-04 | Passthrough toggle | Forces room view in any experience; returns to default |
| F-05 | Recenter / Mute / Help | Each does what it says; Help replays onboarding |
| F-06 | Dock grab bar | Dock moves and re-tilts; Recenter restores |
| F-07 | Dock show/hide | Palm-up gesture (Quest) / Tab (desktop) |
| F-08 | Solar Row layout | 10 bodies, 15 cm, arc, chest height |
| F-09 | Relative Size layout | True proportions, Sun 3 m, no overlaps |
| F-10 | Pinch-pull a body | Near and far; grows to 25 cm; panel + moons appear |
| F-11 | Two-hand scale/rotate | Limits per type; smooth |
| F-12 | Free placement | Released objects stay; auto-restore only out of bounds |
| F-13 | Restore layout | Pop-up / Recenter / `R` returns everything |
| F-14 | Body panels | 10 bodies, correct copy and 4 stats, superscript mass |
| F-15 | Moons | Moon, Ganymede, Callisto, Titan, Mimas, Iapetus orbit, grab, labels, panels |
| F-16 | Sun touch | Brightens and rumbles while a hand is inside |
| F-17 | Orbit model | Rings, motion, labels, Asteroid Belt |
| F-18 | Orbit model modes | Schematic and Realistic |
| F-19 | Orbit model manipulation | One hand move, two hands tilt/scale |
| F-20 | Milky Way map | Particle galaxy, 6 destination tags |
| F-21 | Tag hover/select | Grow + cyan; pinch opens |
| F-22 | Nebula overlays | Helix, Crab, Homunculus, Orion in black halo; grabbable; close |
| F-23 | Andromeda | Distinct galaxy, manipulable, panel |
| F-24 | Sagittarius A\* | Lensed black hole in full black; panel |
| F-25 | Galaxies | 300+ sprites, drift, black default, toggle |
| F-26 | Cosmic Web | Filament volume, full black, two-hand rotate/scale |
| F-27 | Environment modes | Dim, halo, black; 0.4 s fades |
| F-28 | Grow-in transitions | Every experience and overlay |
| F-29 | Narration | Per body/experience; replays; stops on switch |
| F-30 | Ambience & music | Per experience; crossfades |
| F-31 | UI sounds | Hover, press, grab, release, whoosh |
| F-32 | Intro + hints | Original intro plays; two hint cards after placement, first run only, replay from Help |
| F-33 | Desktop parity | Every row in §5.3 works |
| F-34 | About | Credits, licences, privacy link, version |
| F-35 | Performance | ≥ 60 fps at 72 Hz on Quest 3 in every experience |

---

## 13. Out of scope (v1)
Multiplayer; voice commands; controller-specific UI (controllers work through the same ray/select path but get no bespoke affordances); user-generated content; real-time astronomical data; languages other than English.

---

## 14. Glossary
**Experience** — one of the seven dock destinations. **Overlay** — a nebula opened inside the Milky Way. **Body** — the Sun, a planet, Pluto or a moon. **Layout** — a named arrangement of an experience's objects. **Restore** — return objects to the layout. **Dock** — the floating tile menu. **Panel** — the floating text card. **Tag** — a destination label in the Milky Way. **Dim / Halo / Black** — the environment modes.
