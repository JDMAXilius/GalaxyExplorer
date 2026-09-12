# Galaxies as destinations - parameters for the galaxy builder

*Written 12 September 2026, in the terminal session, after the research agents were stopped. Read the
confidence note below before using any number in this file.*

## Confidence, stated up front

This file was written from established astronomy, **not** from fetched primary sources. The session's
web-search budget was exhausted (200/200) before the galaxy research pass produced anything, so none of
these values carry a citation yet. That matters differently per column:

| Column | Confidence | Why |
|---|---|---|
| Morphological type, arm count, orientation (face-on / edge-on) | **High** | These are textbook and stable. |
| Distance | **Medium** | Several of these have been revised (M51 especially). Treat as approximate. |
| Pitch angle | **Low to medium** | Literature values disagree, sometimes by 10 degrees, and measurement method matters. Good enough to *look* right; not good enough to publish. |
| Colour story, bulge-to-disk | **Medium** | Qualitatively reliable, quantitatively approximate. |
| Black hole masses, physical diameters | **Medium** | Quoted from memory of published figures. |

**Rule for using this file:** it is good enough to build and tune the *look*. It is **not** good enough
for any number that appears in player-facing copy. Every figure that reaches an info panel needs a
citation added first - that is D-010's truthfulness clause, and it is the whole reason the research pass
came before the building. Ticket for that: see CS-127 in the backlog.

---

## The shortlist

Ten, chosen so no two look alike in a headset. The "role" column is the reason each one is on the list;
if a galaxy has to be cut, cut it by role so the set keeps its range.

| # | Galaxy | Role in the set | Type | Arms | Seen as | Distance |
|---|---|---|---|---|---|---|
| 1 | **M51 Whirlpool** | The grand-design face-on spiral | SA(s)bc pec | 2, strong | face-on | ~23 Mly |
| 2 | **M101 Pinwheel** | The big, many-armed, asymmetric disk | SAB(rs)cd | ~5-6, uneven | face-on | ~21 Mly |
| 3 | **NGC 1300** | The textbook bar | SB(rs)bc | 2 from bar ends | intermediate | ~61 Mly |
| 4 | **NGC 4565** | The edge-on knife with a dust lane | SA(s)b? sp | n/a at this angle | edge-on | ~40 Mly |
| 5 | **M104 Sombrero** | The enormous bulge | SA(s)a / S0 | tight, unresolved | near edge-on | ~29 Mly |
| 6 | **M87** | The giant elliptical, and the jet | E0-E1 pec | **none** | n/a | ~53 Mly |
| 7 | **Centaurus A** | The wrecked one: elliptical crossed by dust | S0 pec | none | dust lane edge-on | ~12 Mly |
| 8 | **LMC** | The irregular satellite, and our neighbour | SB(s)m | 1, barely | face-on-ish | ~163 kly |
| 9 | **M33 Triangulum** | Local Group, flocculent, no bar | SA(s)cd | flocculent | intermediate | ~2.7 Mly |
| 10 | **Antennae** | Two galaxies mid-collision, tidal tails | pair, merging | disrupted | n/a | ~45-60 Mly |

Deliberately **not** included, and why: the Cartwheel (a ring galaxy is a striking shape but needs a
generator of its own for one object), M81/M82 (M82's starburst outflow is its whole character and that is
a volumetric effect, not a disk), and NGC 1365 (a second great barred spiral adds a duplicate role once
NGC 1300 is in).

---

## What the builder can and cannot make

The existing path is a spiral-arm particle generator: ellipse families swept through a winding angle,
three layers (stars / clouds / dust-negative), each with a colour ramp. From reading
`AndromedaBuilder`, the parameters that exist are ellipse axes (`XRadii`, `ZRadii`), total winding
(`SpiralRotationDegrees`), arm separation (`ArmSpacingDegrees`), orientation (`TiltDegrees`,
`YawDegrees`), physical width, and the three ramps.

That generator serves entries **1, 2, 3, 4, 5, 9** well. It does **not** serve:

- **M87 (6)** - an elliptical has no disk and no arms. It wants a spheroidal distribution: a de
  Vaucouleurs / Sersic radial profile, near-spherical with mild flattening, one old-yellow population and
  essentially no dust. Plus the jet, which is a separate narrow feature. **Needs a new generator kind.**
- **Centaurus A (7)** - an elliptical body *plus* a dark warped dust lane across it. Elliptical generator
  plus a dust band. **Needs the elliptical kind first.**
- **LMC (8)** - irregular, one stubby bar, no symmetric arms. Could be faked as a very loose one-armed
  spiral with heavy asymmetry, but honestly wants a clumpy distribution seeded from real HII positions.
  **Approximate with the spiral kind, flag as approximate.**
- **Antennae (10)** - two disks plus two enormous tidal tails. The tails are the recognisable feature and
  nothing about an ellipse sweep produces them. **Needs its own kind, and it is the most work of any
  entry here.**

**Recommended build order**, which also matches increasing risk: 1, 2, 9 (pure spirals, prove the
mechanism) -> 3 (bar) -> 4, 5 (high inclination, tests that the dust layer reads at an angle) -> 6
(implement the elliptical kind) -> 7 -> 8 -> 10 last, or dropped if it does not pay for itself.

---

## Per-galaxy parameters

Pitch angle is given as the range I am confident the true value sits in. `ArmSpacingDegrees` is
`360 / armCount`. Winding is expressed as the total angle an arm sweeps from the bulge to the disk edge -
a tighter pitch means more winding for the same disk.

### 1. M51, the Whirlpool

- **Type** SA(s)bc pec. **Arms** 2, unusually well defined - this is the archetype of "grand design".
- **Arm count** 2 -> `ArmSpacingDegrees` 180. **Pitch** ~15-20 deg, so tightly wound: total winding high,
  around 400-500 deg, which is close to Andromeda's existing 470 and a good first test of the refactor.
- **Inclination** ~20 deg from face-on. Tilt it only slightly; the point of M51 is that you see the spiral.
- **Bulge** small-to-moderate; the arms dominate. Bulge-to-disk low, ~0.15.
- **Colour** strongly two-population: blue-white arms studded with **pink HII regions**, a yellow-white
  core, and dark dust lanes on the inner edges of the arms. The pink is not optional - it is what makes
  M51 read as M51 rather than as a blue pinwheel.
- **The companion.** NGC 5195, a small early-type galaxy, sits at the end of the northern arm and is
  physically interacting. **Build it.** M51 without the companion is a different object, and it is cheap:
  a small spheroid, yellow, no arms, about a fifth the diameter, offset along one arm. This is also the
  single best argument for implementing the elliptical generator kind early.

### 2. M101, the Pinwheel

- **Type** SAB(rs)cd. **Arms** many and **asymmetric** - not a clean two-arm design. Effectively 5-6
  traceable arms of unequal strength.
- **Arm count** 5 or 6 -> `ArmSpacingDegrees` 72 or 60. **Pitch** loose, ~20-25 deg, so less total
  winding than M51, ~250-350 deg.
- **Inclination** ~18 deg, essentially face-on.
- **Bulge** small. Bulge-to-disk ~0.1. This is a late-type disk.
- **Diameter** very large - roughly 170,000 ly, comfortably bigger than the Milky Way. Give it a larger
  `WidthMetres` than Andromeda so the difference is felt.
- **Colour** blue overall with **many bright HII knots**, more numerous and scattered than M51's. The
  asymmetry matters: the disk is lopsided, brighter on one side.
- **Honest note:** the generator sweeps symmetric ellipse families, so "asymmetric" will need either
  per-arm strength variation or accepting that this reads more regular than the real thing. Say which in
  the profile comment.

### 3. NGC 1300, the barred spiral

- **Type** SB(rs)bc. The **bar is the feature** - a long straight bar through the centre with two arms
  starting abruptly at its ends.
- **Arm count** 2 -> 180. **Pitch** ~25 deg, fairly open. Winding moderate, ~250-300 deg.
- **Inclination** ~50 deg, a clear intermediate tilt.
- **Bulge** small; there is a distinct inner disk/ring structure at the bar centre.
- **Colour** yellow-orange bar (old stars, little current formation along it), blue arms with HII.
- **The bar is not in the generator.** An ellipse sweep produces arms that spiral in to the centre, not a
  straight bar. Options: add a bar term (a highly elongated, low-winding inner ellipse family), or start
  the arms at a radius and fill the inner region with a separate elongated population. **Either way this
  entry needs a generator feature that does not exist yet** - worth knowing before it is scheduled.

### 4. NGC 4565, the edge-on

- **Type** SA(s)b, seen at ~86-90 deg. **Arms** irrelevant - you cannot see them at this angle.
- **What you see** a razor-thin bright disk, a **boxy/peanut bulge**, and a sharp dark dust lane running
  the full length, slightly offset from the midline.
- **Parameters** arm count and pitch barely matter; what matters is **disk thickness** (very thin) and
  **inclination** (~87 deg). The dust layer must be a thin dark band in the disk plane, not a diffuse
  cloud, and the negative/dust layer has to be the visually dominant one.
- **Colour** yellow-white core, the disk washed out by extinction, the lane near-black.
- **Why it earns a slot** it is the one entry that tests whether the dust-negative layer actually reads as
  extinction rather than as a dark smudge, and it is instantly recognisable.

### 5. M104, the Sombrero

- **Type** SA(s)a, transitional to S0. **Inclination** ~84 deg.
- **The feature** an enormous bright bulge - unusually large for a spiral - wrapped by a **thin, sharply
  defined dust ring** seen almost edge-on. Hence the hat.
- **Arms** tightly wound and unresolved. Pitch very tight, ~5-10 deg. Arm count is nearly moot; use 2 with
  very high winding.
- **Bulge-to-disk** high - the highest of any entry here, ~0.6-0.7. This is the parameter that makes it
  Sombrero and the best test that bulge-to-disk is actually wired.
- **Colour** warm yellow-white bulge, a dark dust ring with a faint blue-white disk behind it. It also has
  a very rich globular cluster population, which could be a sparse bright halo layer.

### 6. M87, the giant elliptical

- **Type** E0-E1, in Virgo. **No disk, no arms, no dust lanes.** Nearly spherical, slightly flattened.
- **Profile** de Vaucouleurs (Sersic n~4): steeply concentrated centre, a smooth featureless envelope
  falling off far out. No structure at all - and the *absence* of structure is the whole point of having
  it in the set.
- **Colour** uniformly warm yellow-orange. An old, red, dust-poor population. No blue, no pink.
- **The jet.** A one-sided relativistic jet from the nucleus, visible optically, extending thousands of
  light years. This is the recognisable feature and it is a separate narrow, slightly bluish feature -
  probably a small dedicated particle stream or a billboard.
- **The black hole** ~6.5 billion solar masses, and it is the one the Event Horizon Telescope imaged in
  2019 - the first direct image of a black hole shadow. That is a real hook for the panel copy, and it
  connects to the app's existing Sagittarius A* experience.
- **Needs the elliptical generator kind.** Do not fake this with a zero-arm spiral; the radial profile is
  the object.

### 7. Centaurus A, NGC 5128

- **Type** S0 peculiar - an elliptical body with a **broad, warped, dark dust lane** straight across it,
  the debris of a merger.
- **Distance** ~12 Mly, one of the nearest active galaxies, so it is large on the sky.
- **Colour** yellow elliptical envelope, a heavy near-black dust band, with **pink star-forming knots**
  inside the lane - the merger is still making stars.
- **Also has a jet**, prominent in radio and X-ray, less so optically. Optional.
- **Build** elliptical kind plus a thick dust band at a tilt, plus HII knots confined to the band. Second
  customer for the elliptical generator, which is the argument for building that kind properly.

### 8. The Large Magellanic Cloud

- **Type** SB(s)m, irregular with a stubby off-centre bar. **Our satellite**, ~163,000 ly away, and the
  closest entry in this set by three orders of magnitude.
- **What you see** a clumpy, asymmetric, blue-ish patch with one poorly defined arm, a bright bar, and the
  **Tarantula Nebula** as a conspicuous single bright knot - the most active star-forming region in the
  Local Group.
- **Parameters** 1 arm, very loose pitch (~30 deg+), low winding, heavy asymmetry and clumpiness.
  Inclination ~35 deg.
- **Honest note** the generator will make this look more organised than it is. Accept it as approximate,
  say so in the profile comment, and consider seeding clumps at real HII positions later. The Tarantula
  should be an explicit bright feature, not left to chance.
- **Why it earns a slot** it is the only entry that is a *satellite of ours*, and it is the one that
  makes the Milky Way feel like it has neighbours rather than standing alone.

### 9. M33, Triangulum

- **Type** SA(s)cd. Third-largest Local Group member, ~2.7 Mly, after Andromeda and the Milky Way.
- **Arms** **flocculent** - many short, patchy, discontinuous arm fragments rather than two long ones. No
  bar. This is a genuinely different arm character from M51 and worth having for contrast.
- **Arm count** effectively many; if the generator needs a number, 4-5 with low arm coherence. **Pitch**
  loose, ~25-40 deg.
- **Inclination** ~54 deg, a clear tilt.
- **Bulge** very small, almost none. Bulge-to-disk ~0.05.
- **Colour** blue, with prominent HII regions - NGC 604 is one of the largest known and could be a named
  bright knot.
- **Pairs naturally with Andromeda** in the same neighbourhood, which is a nice content adjacency given
  Andromeda already exists.

### 10. The Antennae, NGC 4038/4039

- **Type** a merging pair, mid-collision. **The two long tidal tails are the object** - they are what the
  name refers to and they extend far beyond the disks.
- **What you see** two disrupted disks tangled together, brilliant blue super star clusters along the
  contact region, heavy dust, and two vast sweeping tails of stars flung out by the interaction.
- **Parameters** not expressible in the current generator. Two disk generators plus two tail curves
  (each a long, thinning arc of points on a hyperbolic-ish path).
- **Recommendation: schedule this last, or drop it.** It is the most work in the set and the tails need a
  bespoke generator used exactly once. The role it fills - "galaxies collide" - is partly filled by M51
  plus NGC 5195 for far less effort, and M51's interaction is real. Build M51's companion first, then
  decide whether the Antennae still earns its cost.

---

## What this means for the builder

1. **Two generator kinds cover eight of ten entries**: the existing spiral, plus a spheroidal/Sersic
   elliptical kind. Build the elliptical kind - it unlocks M87, Centaurus A *and* M51's companion.
2. **Three features do not exist yet** and each should be a ticket, not a surprise: a **bar** term
   (NGC 1300), a **dust band across an elliptical** (Centaurus A), and **tidal tails** (Antennae).
3. **Bulge-to-disk must be a real parameter**, not a constant. Sombrero at ~0.65 against M33 at ~0.05 is
   the widest spread in the set and the clearest proof the parameter is wired.
4. **Named bright features matter** more than I expected while writing this: the Tarantula in the LMC,
   NGC 604 in M33, M51's HII beads, M87's jet. Several of these galaxies are recognised by one feature
   rather than by their overall shape, so a "named highlight" slot in the profile would earn its keep -
   and it connects to the existing `LabelButton` tag mechanism.
5. **Distances span 163,000 ly to ~60 Mly** - nearly three orders of magnitude. The diorama model means
   every one of them ends up about the same size on the player's table, so **the distance has to be told
   in the panel copy**, and a comparison against the Milky Way's 100,000 ly diameter is the natural
   reference. This is exactly the weakness the scale research flagged.

## Still owed

- **Citations for every number here** before any of it reaches player-facing copy (CS-127).
- **Imagery and licences** - none sourced. NASA imagery is public domain; ESA/Hubble and ESA/Webb are
  CC BY 4.0 and need attribution. Everything used goes in `Assets/_sources/CREDITS.md` per D-003.
- **OpenNGC** is the catalogue to pull structural data from per D-011: it carries Hubble type, position
  angle, axis ratio and magnitude per row, which covers type, inclination proxy and size in one fetch.
