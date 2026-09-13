# Experience panels

*Each experience carries a **Dock** name and a **Second line** as well as its panel **Title**. They exist because a dock tile is 110 mm wide and a panel title is not: "Galactic Center - Sagittarius A*" is thirty-two characters and used to overrun the tile beside it. The Title is what the panel says; the Dock name is what fits.*

Seven scene panels, plus HD 110067 and the three extra galaxies (Pinwheel,
Triangulum, Whirlpool) added under D-010. Each has a title, two paragraphs (HD
110067 has three, carried over from its own asset), and one instruction line
shown in the secondary colour. ASCII only.

*A **Room** line is optional and is the one field here that is not copy: state it only when the
room is not the `Dimmed` default, values `Passthrough | Dimmed | Halo | Black`. It is parsed
case-insensitively and a value that does not parse is **silently ignored**, leaving the default —
so a full-black place with no Room line renders dimmed and nothing reports it.*

*A **Bodies** or **Destinations** line is also not copy: a comma-separated list of ids this place
opens, each one a heading (`## id`) somewhere in this file or in `nebulae.md`. Bodies point at
`bodies.md`/`moons.md` ids. Wiring runs after every place and body has already been imported, so
order between files does not matter, and an id that resolves to nothing is silently dropped rather
than reported.*

---

## cosmic_web
**Title:** Cosmic Web
**Room:** Black
**Dock:** Cosmic Web
**Second line:** THE LARGEST SCALE

The universe at its largest scale is not spread evenly. Matter gathers along
filaments that run for hundreds of millions of light-years, meeting at dense
knots where clusters of galaxies form, with near-empty voids in between.

Most of this structure is dark matter, which no telescope can see directly. We
map it by the galaxies strung along it, the way dew picks out a spider's web.

**Instruction:** Grab with two hands to turn the web or change its size.

---

## galaxies
**Title:** Galaxies
**Room:** Black
**Dock:** Galaxies
**Second line:** A SKY FULL OF THEM

Every smudge of light here is a galaxy, and each one holds billions of stars.
Hubble and Webb found thousands of them in patches of sky no wider than a grain
of sand held at arm's length.

Their light has been travelling for billions of years, so looking out into a
field like this is also looking a long way back in time.

**Instruction:** Use the passthrough button to put them in your room or in the dark.

---

## milky_way
**Title:** Milky Way
**Dock:** Milky Way
**Second line:** OUR GALAXY
**Destinations:** solar_system, sagittarius_a, helix, crab, homunculus, orion, pillars, ngc1501, trumpler14

Our galaxy is a barred spiral roughly 100,000 light-years across, holding a few
hundred billion stars. The Sun sits in a minor arm about 26,000 light-years out
from the centre.

One lap of the galaxy takes the Sun around 230 million years. The last time it
was here, dinosaurs were new.

**Instruction:** Point at a label and pinch to open that destination.

---

## andromeda
**Title:** Andromeda (M31)
**Dock:** Andromeda
**Second line:** M31

The nearest large galaxy to our own is a spiral of about a trillion stars, 2.5
million light-years away. On a dark night it can be seen without a telescope,
the most distant thing most people ever see with their own eyes.

It is heading toward us at about 110 kilometres per second. In roughly four and
a half billion years the two galaxies will merge.

**Instruction:** Grab with two hands to turn it or change its size.

---

## solar_system
**Title:** Solar System
**Dock:** Solar System
**Second line:** THE ORBITS
**Bodies:** sun, mercury, venus, earth, mars, jupiter, saturn, uranus, neptune, pluto

Eight planets, several dwarf planets, and countless moons, asteroids and comets
orbit one ordinary star. The Sun holds 99.8 per cent of all the mass; everything
else is the remainder.

Sizes and spacing here are simplified. At true scale the planets would be
invisible specks and Neptune would sit far outside your room.

**Instruction:** One hand moves the model, two hands tilt or resize it.

---

## solar_system_planets
**Title:** Solar System Planets
**Dock:** The Planets
**Second line:** SIDE BY SIDE

Every world of the solar system, lined up within reach. In this view they share
a size so you can compare their surfaces.

Switch to relative size to see the truth of it: the Sun fills the room, Jupiter
is a beach ball, and Earth sits in your palm.

**Instruction:** Pinch a planet to lift it out, two hands to grow it, let go to leave it.

---

## sagittarius_a
**Title:** Galactic Center - Sagittarius A*
**Room:** Black
**Dock:** Galactic Center
**Second line:** SAGITTARIUS A*

At the centre of the Milky Way is a black hole of about 4.3 million solar
masses, 26,000 light-years from Earth. Its event horizon measures roughly 25
million kilometres across, some eighteen times the width of the Sun.

The hole itself cannot be seen, only the superheated gas circling it. Light
bends so sharply here that the far side of the disc appears above and below the
shadow. The Event Horizon Telescope photographed it in 2022.

**Instruction:** Walk around it. Two hands change its size.

---

## hd110067
**Title:** HD 110067
**Second line:** SIX WORLDS IN STEP
**Bodies:** hd110067_star, hd110067_b, hd110067_c, hd110067_d, hd110067_e, hd110067_f, hd110067_g

Six worlds around a star slightly cooler than the Sun, 105 light years away,
moving in a rhythm that has held for billions of years. Each planet's year is a
simple fraction of its neighbour's - three orbits to two, over and over, then
four to three for the outer pair. Systems are born like this and almost always
lose it; this one never did.

All six are larger than Earth and far lighter than their size suggests, which
means deep hydrogen atmospheres rather than ground. Nobody has photographed
them and nobody has seen their surfaces, because they may not have any. What
you are looking at is built from measured sizes and orbits - the appearance is
our best reading of the physics, not a picture.

The whole system would fit comfortably inside the orbit of Mercury.

**Instruction:** Pinch a world to lift it out, two hands to grow it, let go to leave it.

---

## pinwheel
**Title:** Pinwheel Galaxy (M101)
**Second line:** M101

A face-on disc around 170,000 light years across - comfortably larger than our
own galaxy - with many arms of uneven strength rather than a tidy pair,
scattered with bright knots where new stars are forming.

It sits about 21 million light years away in Ursa Major. The real galaxy is
noticeably lopsided, brighter and more extended on one side; what you are
holding is more even than that.

**Instruction:** Grab it with two hands to turn it or change its size.

---

## triangulum
**Title:** Triangulum Galaxy (M33)
**Second line:** M33

Our third neighbour. After the Milky Way and Andromeda, this is the largest
galaxy in the Local Group, and at about 2.7 million light years it is close
enough that individual clouds of glowing gas can be picked out inside it.

Its arms are flocculent - short, patchy fragments rather than long sweeping
ones - and it has almost no central bulge. The version here is smoother and
more organised than the real thing, which the generator cannot yet break up.

**Instruction:** Grab it with two hands to turn it or change its size.

---

## whirlpool
**Title:** Whirlpool Galaxy (M51)
**Second line:** M51

Two arms, wound tight and picked out in blue-white star clusters, with pink
knots of glowing hydrogen strung along them like beads. This is the galaxy
most people picture when they hear the word, and we see it almost perfectly
face-on - which is the only reason it looks this way from here.

It is about 23 million light years away, in Canes Venatici. A smaller galaxy,
NGC 5195, is passing through its outskirts and pulling on the northern arm;
that companion is not shown here yet.

**Instruction:** Grab it with two hands to turn it or change its size.
