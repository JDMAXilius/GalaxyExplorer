# ChatGPT image tickets: photos for the 2D information card

Track CHAT-IMG. For the separate ChatGPT chat that generates images. These are repository tickets, not GitHub Issues, and nothing delivers them automatically. All start **todo**. Written 14 Oct 2026 by the terminal session at the owner's request.

## What these images are for

The UI 2.0 information card (Figma `Card/Information`, node `44:407`; screens S01, S08) opens with a photo across its top. In Unity the card picks `Assets/ui/photos/card_<id>.png` for the selected place or body. Today most of those are missing or are stand-ins (renders of the app's 3D models, a cropped portrait), and they do not match the approved look. These tickets ask for one new image per place and per planet.

These images go on the **2D card only**. They are not textures, not the dock or rail pictures, and not the picker tiles. The picker tiles are a separate job.

## Shared spec (applies to every ticket)

**Look.** Match the photos on the cards in the approved concept images: `docs/design/v2/screens/01-galaxies.png` (Andromeda card), `02-solar-system.png` (Earth card), `03-destination-preview.png` (Orion card). That means a photographic space render, deep black space with a fine natural star field, the subject lit and saturated but believable, soft glow, and no lens flares.

**Frame.**
- **Size:** 1376 × 828 px PNG (the card photo is 342 × 207 design px, about 1.66 : 1; delivered at four times that).
- **Subject:** centred, filling about 70–80 % of the height. The card crops a few pixels from every edge and rounds the corners, so leave a little margin.
- **Background:** dark space edge to edge. No vignette to grey, no gradient banding.

**Never in the image:**
- text, labels, logos or watermarks
- borders, rounded corners, frames, UI or hands
- the room or any other part of the concept scene behind the panels

**Accuracy.**
- **Recognisable:** each object must be recognisable as itself, with the features listed in its ticket.
- **Labelled as illustration:** these are illustrations, not observations, and the credits will say so. Do not imitate a specific NASA, ESA or observatory photograph closely enough to pass as it.
- **Facts:** where a ticket gives a fact (rings, colour, the Great Red Spot), do not contradict it.

**Delivery.**
- **File name:** `card_<id>.png` exactly as given in each ticket.
- **Batches:** deliver in batches or all at once.
- **What to report:** for each image, the prompt used, the tool and model, and whether it was edited after generation. The terminal session needs this to log the image in `Assets/_sources/CREDITS.md` as the project requires.

**Done when**
- the file is the right size and name;
- it carries no text or frame;
- the owner has looked at it next to the matching concept card and accepted it.

Once delivered, the terminal session copies the images into `Assets/ui/photos/`, rebuilds the shell prefab (Cosmic Simulation > UI 2 > Build Shell Prefab) and logs the credits. No code change is needed: `card_<id>` already takes priority over every other picture.

## Places

### CHAT-IMG-001 — Milky Way (`card_milky_way.png`)
Our galaxy seen from outside, three-quarter view: a barred spiral with a short bright central bar, two major arms and fainter minor arms, a warm yellow-white core and blue-white arms with pink star-forming knots and dark dust lanes. It must read as "our galaxy", not as Andromeda: keep the visible bar. Owner priority: the current card has no proper picture.

### CHAT-IMG-002 — Solar System (`card_solar_system.png`)
The orbit view: the Sun bright at centre-left, thin faint orbit rings in perspective, a few planets as small lit discs along them, with Saturn's rings and Jupiter's bands readable even at small size. Not to scale, and it does not need to be: this is the "Orbits" view.

### CHAT-IMG-003 — The Planets, row view (`card_solar_system_planets.png`)
All eight planets plus Pluto lined up left to right at equal size, as in the app's Equal Size row: Mercury, Venus, Earth, Mars, Jupiter, Saturn with its rings, Uranus (pale cyan), Neptune (deep blue), Pluto (small, beige). Sun at far left, partly cropped. Even spacing.

### CHAT-IMG-004 — Cosmic Web (`card_cosmic_web.png`)
The large-scale structure of the universe: glowing violet and blue filaments meeting at bright nodes (galaxy clusters), dark voids between them, a fine haze of tiny galaxies along the filaments. Close in spirit to the rail picture in `01-galaxies.png` (purple web), but as a deep wide field rather than a sphere.

### CHAT-IMG-005 — Galaxies, the sphere of galaxies (`card_galaxies.png`)
Many distant galaxies of different types and orientations scattered through deep space, as in a deep-field image: spirals face-on and edge-on, small ellipticals, a few reddish distant ones. No single galaxy dominates.

## Galaxies

### CHAT-IMG-006 — Andromeda (`card_andromeda.png`)
Large spiral, strongly inclined (seen at a steep angle, an elongated oval), bright yellowish core, blue outer arms with dust lanes. Include its two small companions: a compact one close to the disc and a fuzzy elliptical a little further off. The existing card was cropped from the concept image; replace it so all cards share one quality.

### CHAT-IMG-007 — Whirlpool (`card_whirlpool.png`)
Face-on grand-design spiral with two sweeping arms, with its smaller yellowish companion galaxy at the tip of one arm, joined to it.

### CHAT-IMG-008 — Pinwheel (`card_pinwheel.png`)
Face-on spiral, large and somewhat lopsided, with many loose, patchy arms full of pink star-forming regions and a small bright core.

### CHAT-IMG-009 — Triangulum (`card_triangulum.png`)
Smaller, loose spiral, mildly inclined, with a faint small core, flocculent arms and prominent pink nebula knots. Less grand than Andromeda.

## Planets and the Sun

Each one is a single body, centred, lit from the upper left with a visible terminator. The night side fades into space; it is not a flat black disc.

### CHAT-IMG-010 — The Sun (`card_sun.png`)
Orange-yellow disc with granulated surface, a few dark sunspots, a soft corona glow and a small prominence at the limb.

### CHAT-IMG-011 — Mercury (`card_mercury.png`)
Grey, heavily cratered, no atmosphere.

### CHAT-IMG-012 — Venus (`card_venus.png`)
Featureless pale yellow-cream cloud deck with faint banding. No surface visible.

### CHAT-IMG-013 — Earth (`card_earth.png`)
The Americas facing the viewer, white clouds, thin blue atmospheric rim, with the Moon small in the top-right corner, as on the S08 card. Replace the existing crop for consistency.

### CHAT-IMG-014 — Mars (`card_mars.png`)
Rust red, dark surface markings, a white polar cap, thin haze at the limb.

### CHAT-IMG-015 — Jupiter (`card_jupiter.png`)
Cream and brown bands with the Great Red Spot in the southern hemisphere.

### CHAT-IMG-016 — Saturn (`card_saturn.png`)
Pale gold banded planet with its rings tilted toward the viewer, the Cassini division visible, and the planet's shadow falling on the rings. Frame it wide enough that the rings are not cut off.

### CHAT-IMG-017 — Uranus (`card_uranus.png`)
Pale cyan, almost featureless, with faint thin rings nearly perpendicular to the orbit (the planet is tipped on its side).

### CHAT-IMG-018 — Neptune (`card_neptune.png`)
Deep blue with faint bands, a dark storm spot and a few white high clouds.

### CHAT-IMG-019 — Pluto (`card_pluto.png`)
Beige and reddish-brown with the large pale heart-shaped plain on the visible face.

## Not in these tickets

- **Remaining places:** the Galactic Center, the nebulae, HD 110067 and its planets, and the 27 moons. These keep their current pictures until the owner asks.
- **Picker tiles:** the 134 × 88 thumbnails across the top. They can be cut from these images afterwards, so do not generate them separately.
