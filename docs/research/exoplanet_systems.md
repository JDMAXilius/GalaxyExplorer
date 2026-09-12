# Real exoplanet systems as destinations

Research note for the "other solar systems" idea. Nothing here is a decision; it is the
evidence base for one. Written September 2026.

**Rule this document follows.** Every number is either (a) quoted from a primary source with
a URL, (b) computed by me from sourced inputs, in which case it says so and names the formula,
or (c) marked `UNSOURCED`. Where a value is disputed or has been revised, both values are
given with dates. Where a visual feature is a model or an artist's guess rather than a
measurement, it is labelled. The project's truthfulness rule means the app's own copy must not
present an artist concept as an observation, so the "honest appearance" notes below are the
important part, not the pretty pictures.

## How the numbers were produced

Planet and star parameters come from the **NASA Exoplanet Archive** `pscomppars` table
(composite parameters, one row per planet), queried by TAP on 12 September 2026:
<https://exoplanetarchive.ipac.caltech.edu/TAP/sync?query=select+*+from+pscomppars&format=csv>.
Archive docs: <https://exoplanetarchive.ipac.caltech.edu/docs/TAP/usingTAP.html>. Where a
discovery or revision paper gives a better value, the paper is cited and the difference noted.

Four things are **computed by me**, not quoted:

1. **Star colour.** A Planck spectrum at the star's published effective temperature, integrated
   against the analytic CIE 1931 colour matching functions of Wyman, Sloan and Shirley 2013
   (<https://jcgt.org/published/0002/02/01/>), converted to sRGB and normalised so the brightest
   channel saturates. This is the colour of the star's *continuum*, which is the honest answer to
   "what colour is it" but not to "what colour does it look to a human eye adapted to it" - see
   the white-balance warning in section 4.
2. **Angular diameter of the host star from each planet**, as `2 * atan(R_star / a)`. The Sun from
   Earth is 0.5329 deg by the same formula; the Moon from Earth is 0.5181 deg.
3. **Insolation** `S = L / a^2` in Earth units, and a **visible-band brightness ratio**
   `S * (f_vis_star / f_vis_Sun)` where `f_vis` is the 400-700 nm fraction of a Planck spectrum at
   that temperature. For the Sun `f_vis = 0.367`. This second number matters enormously for red
   dwarfs and is the single most commonly misrepresented thing about them.
4. **Habitable zone boundaries**, from the polynomial of Kopparapu et al. 2013
   (<https://arxiv.org/abs/1301.6674>, coefficients from the paper's Table, valid 2600-7200 K):
   conservative HZ = runaway greenhouse to maximum greenhouse; optimistic HZ = recent Venus to
   early Mars. Checked against the paper's own solar values (0.97, 1.70, 0.75, 1.77 AU) and
   reproduces them. Where a star is outside 2600-7200 K this is stated rather than extrapolated.

Equilibrium temperatures quoted as `Teq(A=0)` are `278.5 * L^0.25 / sqrt(a)` - zero albedo, full
heat redistribution. They are a physics floor for comparison, not a prediction of a surface
temperature, and for any planet with an atmosphere they are wrong by a lot. The archive's own
`pl_eqt` values are quoted separately where present.

---

## 1. The shortlist

Fourteen systems, plus one historical bonus. Distances are Gaia-derived from the archive
(`sy_dist`, parsecs) converted at 3.261564 ly/pc.

| # | System | ly | Constellation | Host(s) | Confirmed planets | Role it fills |
|---|---|---|---|---|---|---|
| 1 | Proxima / Alpha Centauri | 4.24 / 4.37 | Centaurus | M5.5V + G2V + K1V (triple) | 2 around Proxima | **Nearest system**; **multiple star**; red-dwarf HZ |
| 2 | TRAPPIST-1 | 40.5 | Aquarius | M8V | 7 | **Compact resonant chain of terrestrial worlds** |
| 3 | 55 Cancri | 41.0 | Cancer | G8V + M4.5V | 7 (5 + 2) | **Very large number of planets**; lava world; binary |
| 4 | LHS 1140 | 48.9 | Cetus | M4.5V | 2 | **Ocean / water-world candidate** |
| 5 | Beta Pictoris | 64.4 | Pictor | A6V, ~23 Myr | 2 firm (+1 claimed) | **Very young star**; debris disk; direct images |
| 6 | HD 189733 | 64.5 | Vulpecula | K2V + M4V | 1 | **Hot Jupiter with measured colour** |
| 7 | WD 1856+534 | 80.7 | Draco | DC white dwarf, in a triple | 1 | **Star utterly unlike the Sun** (white dwarf) |
| 8 | TOI-700 | 101.5 | Dorado | M2.5V, quiet | 4 | **Red-dwarf HZ candidate** around a non-flaring star |
| 9 | HD 110067 | 104.9 | Coma Berenices | K0V | 6 | Second **resonant chain**, sub-Neptune flavour |
| 10 | K2-18 | 124.0 | Leo | M2.5V | 2 | **The disputed one** - hycean / DMS controversy |
| 11 | HR 8799 | 134.5 | Pegasus | A5V | 4 | **Directly imaged wide-orbit giants** |
| 12 | Kepler-16 | 244.9 | Cygnus | K + M eclipsing binary | 1 | **Circumbinary** - two suns, honestly |
| 13 | PSR B1257+12 | ~1960 (disputed) | Virgo | millisecond pulsar | 3 | **Pulsar** - the first exoplanets ever found |
| 14 | Kepler-90 | 2767 | Draco | F/G dwarf | 8 | **Most planets** - ties our own count |
| 15 | 51 Pegasi | 50.4 | Pegasus | G-type | 1 | *Bonus:* the first hot Jupiter round a Sun-like star |

Sky coordinates, all from the archive `pscomppars` (`rastr`, `decstr`, `glon`, `glat`):

| System | RA | Dec | gal l | gal b |
|---|---|---|---|---|
| Proxima Cen | 14h29m34.43s | -62d40m34.26s | 313.926 | -1.918 |
| TRAPPIST-1 | 23h06m30.33s | -05d02m36.46s | 69.715 | -56.649 |
| 55 Cnc | 08h52m35.24s | +28d19m47.34s | 196.795 | +37.697 |
| LHS 1140 | 00h44m59.67s | -15d16m26.79s | 115.405 | -78.052 |
| bet Pic | 05h47m17.10s | -51d03m58.13s | 258.363 | -30.612 |
| HD 189733 | 20h00m43.71s | +22d42m35.19s | 60.962 | -3.923 |
| WD 1856+534 | 18h57m39.76s | +53d30m32.49s | 83.499 | +20.623 |
| TOI-700 | 06h28m22.97s | -65d34m43.01s | 275.468 | -26.881 |
| HD 110067 | 12h39m21.41s | +20d01m38.42s | 281.013 | +82.380 |
| K2-18 | 11h30m14.43s | +07d35m16.19s | 254.633 | +62.575 |
| HR 8799 | 23h07m28.84s | +21d08m02.54s | 92.765 | -35.576 |
| Kepler-16 | 19h16m18.20s | +51d45m26.02s | 82.785 | +17.366 |
| PSR B1257+12 | 13h00m03.58s | +12d40m56.5s | 311.310 | +75.414 |
| Kepler-90 (KOI-351) | 18h57m44.03s | +49d18m18.45s | 79.269 | +19.256 |
| 51 Peg | 22h57m28.21s | +20d46m08.74s | 90.064 | -34.728 |

Two sky coincidences worth a line of copy: **Kepler-90 and WD 1856+534 sit 4 degrees apart** in
Draco (18h57m44s +49d18m and 18h57m40s +53d31m) - the eight-planet system and the white dwarf
system are in the same telescope field. And **51 Pegasi and HR 8799 are 2.4 degrees apart** in
Pegasus: the first hot Jupiter and the first directly imaged multi-planet system, neighbours.
Both computed from the coordinates above.

---

## 2. Scale reality check

### 2.1 How big each system is

`Span` is the semi-major axis of the outermost confirmed planet. Sun-to-Pluto is 39.48 AU.

| System | Span (AU) | Span / 39.48 | Same linear scale as a 1.5 m solar system | Magnification to reach 1.5 m |
|---|---|---|---|---|
| HR 8799 | 68.0 | 1.72 | 2.58 m | 0.6x (it is *bigger* than ours) |
| Beta Pictoris | 26.0 | 0.659 | 0.99 m | 1.5x |
| Alpha Cen A-B orbit | 23.3 | 0.590 | 0.89 m | 1.7x |
| 55 Cancri A | 5.60 | 0.142 | 0.21 m | 7x |
| Kepler-90 | 0.971 | 0.0246 | 3.7 cm | 41x |
| Kepler-16 | 0.705 | 0.0179 | 2.7 cm | 56x |
| PSR B1257+12 | 0.46 | 0.0117 | 1.8 cm | 86x |
| HD 110067 | 0.262 | 0.00664 | 1.0 cm | 151x |
| TOI-700 | 0.163 | 0.00414 | 6.2 mm | 242x |
| K2-18 | 0.143 | 0.00362 | 5.4 mm | 276x |
| 55 Cnc B | 0.130 | 0.00329 | 4.9 mm | 304x |
| LHS 1140 | 0.0946 | 0.00240 | 3.6 mm | 417x |
| TRAPPIST-1 | 0.0619 | 0.00157 | 2.4 mm | 638x |
| Proxima Centauri | 0.0485 | 0.00123 | 1.8 mm | 814x |
| 51 Pegasi | 0.0520 | 0.00132 | 2.0 mm | 759x |
| HD 189733 | 0.0313 | 0.00079 | 1.2 mm | 1263x |
| WD 1856+534 | 0.0204 | 0.00052 | 0.8 mm | 1935x |

The headline: **ten of these systems are smaller than Mercury's orbit** (0.387 AU). TRAPPIST-1's
seven planets all fit inside 0.062 AU, which is **0.16 of Mercury's orbital radius** - the whole
seven-planet system would sit comfortably inside Mercury's orbit with room to spare on all sides.
Proxima's two planets fit inside 0.049 AU. HD 189733 b's entire orbit, 0.031 AU, is about
**twelve times the Earth-Moon distance** (0.00257 AU) - close enough to a planetary system drawn
at the scale of the Earth-Moon system that it changes what is drawable (section 2.3).

### 2.2 Distance versus size: the actually absurd ratio

`Distance to the system` in AU (1 ly = 63,241.077 AU) against the system's own span. My computation.

| System | Distance (AU) | Span (AU) | distance : span |
|---|---|---|---|
| Proxima Centauri | 2.68e5 | 0.0485 | 5.5 million : 1 |
| TRAPPIST-1 | 2.56e6 | 0.0619 | 41 million : 1 |
| 55 Cancri A | 2.60e6 | 5.60 | 464 thousand : 1 |
| LHS 1140 | 3.09e6 | 0.0946 | 33 million : 1 |
| Beta Pictoris | 4.07e6 | 26.0 | 157 thousand : 1 |
| HD 189733 | 4.08e6 | 0.0313 | 130 million : 1 |
| WD 1856+534 | 5.10e6 | 0.0204 | 250 million : 1 |
| TOI-700 | 6.42e6 | 0.163 | 39 million : 1 |
| HD 110067 | 6.63e6 | 0.262 | 25 million : 1 |
| K2-18 | 7.84e6 | 0.143 | 55 million : 1 |
| HR 8799 | 8.51e6 | 68.0 | 125 thousand : 1 |
| Kepler-90 | 1.75e8 | 0.971 | 180 million : 1 |
| 51 Pegasi | 3.19e6 | 0.0520 | 61 million : 1 |

**Plainly: you cannot draw the journey and the destination at the same scale, and it is not close.**
At a scale where TRAPPIST-1's whole planetary system is 1 metre across, the trip from here to it is
41,000 kilometres - roughly the circumference of the Earth. At a scale where HD 189733 b's orbit is
1 metre across, the trip is 130,000 km, a third of the way to the Moon. There is no room-scale,
building-scale or city-scale presentation in which both are visible. Any travel sequence is a
transition effect, not a journey to scale, and the app should say so.

### 2.3 The one piece of good news: compact systems are *easier* to draw honestly than ours

The hard thing about a to-scale solar system is not the orbit span, it is the ratio of orbit radius
to body diameter. My computation, orbit radius / body diameter, for the innermost planet of each
system:

| System | Innermost planet orbit : its own diameter |
|---|---|
| WD 1856+534 b | 1 : 23 |
| HD 189733 b | 1 : 29 |
| 51 Peg b | 1 : 43 |
| 55 Cnc e | 1 : 97 |
| TRAPPIST-1 b | 1 : 121 |
| LHS 1140 c | 1 : 249 |
| K2-18 c | 1 : 266 |
| HD 110067 b | 1 : 423 |
| Proxima d | 1 : 489 |
| Kepler-90 b | 1 : 663 |
| TOI-700 b | 1 : 870 |
| Beta Pic c | 1 : 2,536 |
| Kepler-16 b | 1 : 9,793 |
| HR 8799 e | 1 : 14,687 |
| **Earth (for comparison)** | **1 : 11,741** |
| **Pluto (for comparison)** | **1 : 2,485,742** |

This is the most useful finding in the whole document. **A true-to-scale orbit diagram of
TRAPPIST-1 is about a hundred times more drawable than a true-to-scale orbit diagram of our own
solar system**, and for HD 189733, 51 Peg and WD 1856+534 it is genuinely drawable: at a 1 metre
orbit radius, HD 189733 b would be a 3.4 cm ball. Nobody has ever seen an honest orbit diagram of
a planetary system, because ours is impossible to draw. Several of these are possible. That is a
real, defensible hook for the feature, and it is the opposite of the usual exaggeration story.

The catch is that this only holds for the innermost planet. Over a full system the ratio degrades:
TRAPPIST-1's outermost planet against the largest body gives 1 : 644, and Kepler-16 b is
1 : 9,793 because its single planet sits at 0.70 AU.

### 2.4 What has to be exaggerated, and the least dishonest way

Three separate exaggerations are usually smuggled into one slider. They should be separated,
because two of them are defensible and one is not.

1. **Orbit span.** Must be magnified per system (see the "magnification to reach 1.5 m" column).
   This is a *uniform* scale factor, so **all relative distances inside the system stay true**.
   Least dishonest form: one factor per system, shown on screen, with a scale bar in AU. The app
   already has a "Realistic spacing" versus "Orbital view" distinction, which is the same idea.
2. **Body size relative to orbit.** This is the dishonest one, and it is the one every space app
   does silently. Recommendation: do not bake it in. Offer it as an explicit, labelled "bodies
   enlarged NNx" toggle that starts off, because for several of these systems it is not needed.
3. **Distance between systems.** Cannot be honest at all (section 2.2). Do not draw it; use a
   transition.

There is a fourth exaggeration specific to this feature: **the host star**. In the existing
Relative Size layout the Sun is 3 m and Earth is 2.75 cm, which is correct - 3 / 0.0275 = 109.1
against a true Sun/Earth diameter ratio of 109.3. That honesty is worth preserving, and the good
news is that it is *easier* for most of these systems than for ours:

| System | Star diameter (km) | Star : smallest planet | Fits the 5 cm - 3 m body ScaleLimits window (60:1)? |
|---|---|---|---|
| **Our solar system** | 1,392,700 | **586 : 1** | **No** - hence Pluto authored at 5 mm |
| WD 1856+534 | 18,244 | 0.14 : 1 (planet is 7.3x the star) | Yes, inverted |
| HD 189733 | 1,044,525 | 6.5 : 1 | Yes, easily |
| 51 Pegasi | 1,657,313 | 9.2 : 1 | Yes, easily |
| HR 8799 | 2,079,830 | 12.6 : 1 | Yes |
| Beta Pictoris | 2,150,552 | 13.5 : 1 | Yes |
| TRAPPIST-1 | 166,010 | 17.3 : 1 | Yes |
| LHS 1140 | 300,684 | 18.6 : 1 | Yes |
| K2-18 | 572,400 | 19.0 : 1 | Yes |
| Proxima Centauri | 196,371 | 22.3 : 1 | Yes |
| HD 110067 | 1,097,448 | 44.4 : 1 | Yes |
| TOI-700 | 586,327 | 50.3 : 1 | Yes, just |
| 55 Cancri A | 1,364,846 | 57.1 : 1 | Yes, just |
| Kepler-16 A | 903,723 | 83.9 : 1 | No |
| Kepler-90 | 1,671,240 | 110.2 : 1 | No |

So **twelve of these fifteen can have a completely honest Relative Size layout inside the existing
5 cm to 3 m clamp**, which our own solar system cannot. Worked example, TRAPPIST-1 with the star at
3 m: b 0.257 m, c 0.253 m, d 0.182 m, e 0.212 m, f 0.241 m, g 0.260 m, h 0.174 m. Every body is
comfortably grabbable and every ratio is true. Compare that with our own Pluto at 5 mm.

WD 1856+534 deserves its own note because it inverts: the planet is **7.3 times the diameter of the
star**. Star at 3 m puts the planet at 21.8 m. The honest layout is the other way round - planet at
50 cm, white dwarf at 6.9 cm - and it is one of the best single images on this whole list.

---

## 3. Where they are in the galaxy

Heliocentric galactic Cartesian coordinates, my computation from the archive's `glon`/`glat` and
distance: X toward the galactic centre, Y toward galactic rotation, Z toward the north galactic
pole. `%gal radius` is distance divided by a 50,000 ly disc radius. The last column is the offset
from the Sun on a galaxy map drawn 1 metre across.

| System | dist (ly) | X (ly) | Y (ly) | Z (ly) | % of galactic radius | mm on a 1 m galaxy map |
|---|---|---|---|---|---|---|
| Proxima Centauri | 4.24 | +2.94 | -3.05 | -0.14 | 0.0085 | 0.042 |
| Alpha Cen AB | 4.37 | +3.13 | -3.05 | -0.05 | 0.0087 | 0.044 |
| TRAPPIST-1 | 40.54 | +7.73 | +20.91 | -33.86 | 0.081 | 0.405 |
| 55 Cancri | 41.05 | -31.09 | -9.38 | +25.10 | 0.082 | 0.411 |
| LHS 1140 | 48.88 | -4.34 | +9.14 | -47.82 | 0.098 | 0.489 |
| 51 Pegasi | 50.43 | -0.05 | +41.45 | -28.73 | 0.101 | 0.504 |
| Beta Pictoris | 64.40 | -11.18 | -54.28 | -32.79 | 0.129 | 0.644 |
| HD 189733 | 64.46 | +31.22 | +56.23 | -4.41 | 0.129 | 0.645 |
| WD 1856+534 | 80.68 | +8.55 | +75.02 | +28.42 | 0.161 | 0.807 |
| TOI-700 | 101.52 | +8.63 | -90.14 | -45.90 | 0.203 | 1.015 |
| HD 110067 | 104.89 | +2.66 | -13.65 | +103.96 | 0.210 | 1.049 |
| K2-18 | 124.03 | -15.14 | -55.08 | +110.09 | 0.248 | 1.240 |
| HR 8799 | 134.52 | -5.28 | +109.28 | -78.26 | 0.269 | 1.345 |
| Kepler-16 | 244.90 | +29.36 | +231.88 | +73.09 | 0.490 | 2.449 |
| PSR B1257+12 | 1956.94 | +325.33 | -370.18 | +1893.87 | 3.91 | 19.57 |
| Kepler-90 | 2766.63 | +486.32 | +2566.18 | +912.41 | 5.53 | 27.67 |

For context, the Sun's own galactocentric radius is 8,178 +/- 13 pc = 26,673 ly (GRAVITY
Collaboration 2019, <https://www.aanda.org/articles/aa/full_html/2019/05/aa35656-19/aa35656-19.html>),
and the disc is conventionally quoted as roughly 100,000 ly across
(<https://science.nasa.gov/universe/galaxies/>).

**Is a galaxy map meaningful here? Honestly, no - and it is worth saying so in the app.** Thirteen
of the sixteen entries are inside 250 ly, which is **0.5 per cent of the galactic radius**. On a
galaxy map one metre wide, thirteen of them land within 2.5 millimetres of the Sun, and Proxima
Centauri lands 42 *microns* away - less than the width of a human hair, and far below the pixel
pitch of a Quest 3 display at any comfortable viewing distance. Only Kepler-90 (2.8 cm) and
PSR B1257+12 (2.0 cm) would separate from the Sun's dot at all.

There are two honest options and one dishonest one:

- **Honest option A - a local bubble map.** Draw a sphere 300 ly across centred on the Sun, with
  the galaxy shown only as a distant backdrop and a direction arrow toward Sagittarius A*. At that
  scale the shortlist spreads out properly: TRAPPIST-1 0.4 mm becomes 13 cm on a 1 m local map.
  This is the recommendation.
- **Honest option B - two linked views.** A galaxy map that shows a single highlighted dot labelled
  "everything on this list is inside this dot", which then zooms to the local bubble. The
  scale-shock is the point, and it is a genuinely good piece of teaching.
- **Dishonest option - spreading them across the galaxy map for legibility.** This is what most
  space apps do. It makes the Milky Way look explored when in fact the entire shortlist occupies a
  region 0.25 per cent of the disc's width, in one direction that is not even toward the centre.

One further real fact worth using: the systems are **not** distributed evenly on the sky, and the
distribution is an artefact of how we looked. Kepler-90 and Kepler-16 are far away because they sit
in the Kepler mission's single fixed field in Cygnus/Draco
(<https://science.nasa.gov/mission/kepler/>). TOI-700 is at high southern latitude because TESS
surveys the whole sky in sectors. TRAPPIST-1, LHS 1140 and K2-18 are nearby M dwarfs because M
dwarfs are where small planets are detectable. A map of known exoplanets is a map of our telescopes'
pointing history, and that is a more interesting thing to tell a player than a fake spiral arm.

---

## 4. Honest appearance: the four mistakes to avoid

Before the per-system notes, four cross-cutting things. All four are computed by me from sourced
stellar parameters using the methods in the header, and all four are routinely got wrong in space
media - including by NASA's own illustrators, who are drawing for impact and say so.

### 4.1 Red dwarf stars are not dim red discs; they are enormous and dazzling

The instinct is that an M dwarf is a feeble red coal. What the numbers say:

| From this planet | Star's angular diameter | vs the Sun from Earth | Insolation (Earth=1) | Visible-light brightness (Earth=1) |
|---|---|---|---|---|
| TRAPPIST-1 b | 5.50 deg | **10.3x** | 4.15 | 0.44 |
| TRAPPIST-1 e | 2.17 deg | 4.1x | 0.65 | 0.068 |
| TRAPPIST-1 h | 1.03 deg | 1.9x | 0.14 | 0.015 |
| Proxima d | 2.61 deg | 4.9x | 1.82 | 0.35 |
| Proxima b | 1.55 deg | 2.9x | 0.64 | 0.12 |
| LHS 1140 b | 1.22 deg | 2.3x | 0.42 | 0.11 |
| TOI-700 d | 1.37 deg | 2.6x | 0.86 | 0.32 |
| K2-18 b | 1.53 deg | 2.9x | 1.24 | 0.46 |
| 55 Cnc e | **32.9 deg** | **61.7x** | 2,665 | 2,413 |
| HD 189733 b | 12.7 deg | 23.9x | 354 | 309 |
| 51 Peg b | 12.2 deg | 22.8x | 506 | 506 |

So from TRAPPIST-1 b the star is a disc **ten times the Sun's width** - it would fill 5.5 degrees
of sky, the width of eleven full Moons side by side - and yet delivers **less than half** Earth's
visible light, because a 2,566 K blackbody puts only 3.9 per cent of its output in the 400-700 nm
band against the Sun's 36.7 per cent. Both facts are true at once, and the second is the one always
dropped. A TRAPPIST-1 sky is a **huge, deep-orange, oppressively close** sun in permanent late
twilight. Not a small red dot, and not bright daylight either.

The extreme case at the other end is **55 Cnc e**, where the star subtends nearly 33 degrees -
sixty-two Suns wide, about the angular size of a football at arm's length but filling a third of
the horizon-to-zenith arc - at 2,400 times Earth's visible brightness.

### 4.2 Star colour: the numbers, and the white-balance trap

Computed blackbody sRGB, normalised so the brightest channel saturates. Read the warning after.

| Star | Teff (K) | Computed sRGB | 400-700 nm fraction | vs Sun |
|---|---|---|---|---|
| Beta Pictoris | 8,039 | `#E2E7FF` (blue-white) | 0.384 | 1.05x |
| HR 8799 | 7,205 | `#F0F0FF` (white, faint blue) | 0.394 | 1.07x |
| Kepler-90 | 6,080 | `#FFF4F3` (white) | 0.379 | 1.03x |
| **the Sun (reference)** | **5,772** | **`#FFF1EA`** | **0.367** | **1.00x** |
| Alpha Cen A | 5,795 | `#FFF1EB` (Sun-identical) | 0.368 | 1.00x |
| 51 Pegasi | 5,761 | `#FFF1EA` (Sun-identical) | 0.366 | 1.00x |
| HD 110067 | 5,266 | `#FFEAD9` (warm white) | 0.337 | 0.92x |
| Alpha Cen B | 5,231 | `#FFEAD8` (warm white) | 0.336 | 0.92x |
| 55 Cnc A | 5,198 | `#FFE9D7` (warm white) | 0.332 | 0.91x |
| HD 189733 A | 5,052 | `#FFE7D2` (pale amber) | 0.321 | 0.87x |
| WD 1856+534 | 4,710 | `#FFE2C5` (pale amber) | 0.289 | 0.79x |
| Kepler-16 A | 4,450 | `#FFDDBA` (amber) | 0.262 | 0.71x |
| 55 Cnc B | 3,286 | `#FFC27F` (orange) | 0.115 | 0.31x |
| Kepler-16 B | 3,311 | `#FFC381` (orange) | 0.119 | 0.32x |
| TOI-700 | 3,459 | `#FFC789` (orange) | 0.138 | 0.38x |
| K2-18 | 3,457 | `#FFC789` (orange) | 0.137 | 0.37x |
| LHS 1140 | 3,096 | `#FFBC74` (deep orange) | 0.092 | 0.25x |
| Proxima Centauri | 2,900 | `#FFB567` (deep orange) | 0.070 | 0.19x |
| TRAPPIST-1 | 2,566 | `#FFA94F` (deep orange-amber) | 0.039 | 0.11x |

**The trap.** These are the colours of the light *as it arrives*, against a D65 white reference.
They are the right values for a shader's emission colour and for how the star looks against black
sky. They are the wrong values for what the *ground* looks like to an observer who lives there,
because a human eye adapts: standing on TRAPPIST-1 e, that star is your only light source, your
visual system white-balances to it, and a white rock looks white, not orange. The famous
"everything is red" illustration of an M-dwarf world is a colorimetric statement dressed up as a
perceptual one. Both renderings are defensible; only one should be labelled "what you would see".
The project should pick one and say which convention it used. My recommendation: unadapted (orange)
for the star seen from space, adapted (neutral) for anything shot as a surface view, with one line
of copy explaining the difference - it is a genuinely interesting thing to teach.

Note also that no main-sequence star is *green*, and none of these is truly "red": even TRAPPIST-1
at 2,566 K computes to a saturated amber-orange, not red. And the Sun computes to `#FFF1EA`, which
is why "yellow sun" is itself a convention rather than a measurement.

### 4.3 In compact systems, the neighbouring planets are genuinely big in the sky

This is real, it is spectacular, and it is under-used. Maximum angular diameter of the nearest
neighbour at conjunction, `2 * atan(R_neighbour / |a1 - a2|)`, against the Moon's 0.518 deg. My
computation; it ignores inclination, so treat as an upper bound at closest approach.

| Standing on | Largest neighbour in the sky | Angular size | vs the Moon |
|---|---|---|---|
| TRAPPIST-1 c | TRAPPIST-1 b | 1.278 deg | **2.5x the Moon** |
| TRAPPIST-1 b | TRAPPIST-1 c | 1.257 deg | 2.4x |
| TRAPPIST-1 d | TRAPPIST-1 c | 0.827 deg | 1.6x |
| TRAPPIST-1 f | TRAPPIST-1 g | 0.661 deg | 1.3x |
| TRAPPIST-1 e | TRAPPIST-1 f | 0.552 deg | 1.1x |
| 55 Cnc e | 55 Cnc b | 0.661 deg | 1.3x |
| HD 110067 b | HD 110067 c | 0.474 deg | 0.9x |
| TOI-700 b | TOI-700 c | 0.504 deg | 1.0x |
| Proxima d | Proxima b | 0.253 deg | 0.5x |
| LHS 1140 c | LHS 1140 b | 0.125 deg | 0.24x |
| Kepler-90 c | Kepler-90 b | 0.426 deg | 0.8x |
| HR 8799 e | HR 8799 d | 0.008 deg | 0.016x (a point) |

So **a sky from TRAPPIST-1 c with a neighbour two and a half times the width of our Moon, showing a
visible disc and phase, is honest** - and it is the single most compelling true image available
from this whole shortlist. Conversely, in HR 8799 and Beta Pictoris the other planets are
**points**, never discs, by four orders of magnitude. Any image showing two giant planets as large
discs in the same frame is an invention.

Caveat: these are maxima at conjunction and assume coplanar orbits. TRAPPIST-1's are close to
coplanar - all seven planets transit, which forces every inclination to within a degree or so of
90 deg as seen from here and therefore forces them to be mutually well aligned (transit-timing
analysis: Agol et al. 2021, <https://arxiv.org/abs/2010.01074>) - so the conjunction geometry does
happen. For non-transiting systems (Proxima, most of 55 Cnc, 51 Peg) the mutual inclinations are
unknown and these numbers are upper bounds only.

### 4.4 Tidal locking: universally assumed, essentially never measured

Every close-in planet on this list is *expected* to be tidally locked or in a spin-orbit resonance,
from tidal-evolution theory. For none of the terrestrial planets here has the rotation period
actually been measured. This matters directly for the app because the existing `BodyInfo` model has
a **Day Length** field, and for these worlds the honest entry is either "same as its year (expected,
not measured)" or "unknown". See section 5.2.

---

## 5. Fitting the existing model - and where it breaks

The existing machinery is a good fit in outline. `BodyInfo` (`Assets/scripts/experience/BodyInfo.cs`)
carries `Id`, `DisplayName`, `Subtitle`, one `Paragraph` and a free-length `Stat[]`, where a `Stat`
is `label | value | unit | exponent`. Because `Stats` is an array rather than four fixed fields,
adding a fifth stat for orbital distance needs no code change - the copy importer
(`Assets/scripts/Editor/CopyImporter.cs`) reads whatever rows the markdown table has. So the
recommended stat set per exoplanet is:

| Label | Unit | Notes |
|---|---|---|
| Diameter | km | Always available where a planet transits; MODELLED otherwise |
| Mass | kg (mantissa + exponent) | Often a minimum mass or an estimate - see below |
| Orbital Period | days, or years past ~500 d | |
| Orbital Distance | AU | New fifth stat; the solar system bodies use "Distance from Earth" style labels |
| Day Length | days | **The problem field** - see 5.2 |

`LayoutSlot.Scale` is a diameter in metres and `ScaleLimits` for a body is 5 cm to 3 m
(`Assets/scripts/Editor/SolarRowBuilder.cs`), which section 2.4 shows is a *looser* constraint for
most of these systems than for our own.

### 5.1 Five places the model does not fit

**1. No measured mass at all.** Six planets on this list have only an upper limit or nothing:
HD 110067 c, e and g (upper limits `<6.3`, `<3.9`, `<8.4` Earth masses - Luque et al. 2023,
<https://arxiv.org/abs/2311.17775>), and 55 Cnc B b and c have masses but **no radius whatsoever**
because they do not transit. The Mass stat needs a legitimate "not yet measured" rendering, and so
does Diameter. A blank is not the same as a zero and the panel must not print "0 kg".

**2. Minimum masses masquerading as masses.** Every radial-velocity-only planet gives `m sin i`,
a *lower bound*, not a mass: Proxima b and d, 51 Peg b, 55 Cnc b/c/d/f, 55 Cnc B b and c. The
archive's `pl_bmasse` column silently mixes true masses, `m sin i` values and mass-radius estimates.
Printing `6.30 x 10^24 kg` for Proxima b as though it were Earth's 5.97 x 10^24 is a real
misstatement: the honest form is "at least 1.06 Earth masses". This is the single most likely way
the feature would end up lying.

**3. Modelled radii dressed as measured diameters.** For non-transiting giants the archive carries
a radius derived from a mass-radius or evolutionary model. 51 Peg b's "1.26 Rjup" and every HR 8799
and Beta Pictoris radius is of this kind. Rendering "179,662 km" for 51 Peg b implies a precision
that does not exist; it is a model output with a factor-of-order uncertainty.

**4. Day Length is meaningless or unknown for most of them.** See 5.2.

**5. No single central star, and centuries-long periods.**
  - **Circumbinary** (Kepler-16): there is no single star at the origin. The primary and secondary
    orbit each other every 41 days with a separation of about 0.22 AU, so the "Sun" slot is two
    bodies that move. Either the layout needs a two-body barycentre node, or Kepler-16 is presented
    only in an orbital view where the pair reads as a close double.
  - **Two stars each with planets** (55 Cancri): A and B are about 1,000 AU apart, which is 180x
    the span of A's own planetary system. They cannot share a view at one scale.
  - **Triple** (Alpha Centauri, and WD 1856+534 which is a white dwarf plus a red-dwarf binary).
  - **No star at all in visible light** (PSR B1257+12): the host is a neutron star ~20 km across.
    At a scale where the planets are visible the star is invisible, and vice versa. Whatever is
    drawn at the centre is a symbol, not a body.
  - **Periods of centuries**: HR 8799 b takes 465 years, c 189 years, d 101 years, e 57 years
    (my conversion from archive `pl_orbper`); Beta Pic d, if real, 91 years. An orbit animation
    that reads as motion for a 88-day Mercury is static for these. They need either a
    time-compression control or a labelled "positions as observed in 20NN" static view - which is
    actually the truthful option, since for HR 8799 we have *directly measured* positions at
    many epochs and have watched them move.

### 5.2 The Day Length problem, concretely

The solar-system copy gives Day Length for all ten bodies because it is measured for all ten. For
exoplanets:

| Category | Systems / planets | Honest Day Length entry |
|---|---|---|
| Expected tidally locked, rotation never measured | all TRAPPIST-1, Proxima b and d, LHS 1140 b and c, TOI-700 b/c/d/e, K2-18 b and c, 55 Cnc e, HD 189733 b, 51 Peg b, WD 1856+534 b, all HD 110067 | "Same as its year (expected, not measured)" or "Unknown" |
| Rotation genuinely unmeasured, not expected locked | HR 8799 b/c/d/e, Beta Pic b/c, Kepler-90 g and h | "Unknown" |
| Pulsar planets | PSR B1257+12 b/c/d | "Unknown" |

**Not one planet on this list has a measured day length.** That is a clean, interesting fact and
the panel should say it rather than invent a number. Recommendation: keep the field, and let it
carry the string "Not yet measured" - the `Stat.Value` is already a string, so no code change is
needed, but `Stat.ToRichText()` will happily append a unit to it, so the importer must be allowed
to leave `Unit` empty for that row.

A second, subtler point: for a tidally locked planet the *solar* day is infinite but the sky is not
static, because the orbit is eccentric and there is libration. TRAPPIST-1 planets have small but
non-zero eccentricities (0.002 to 0.010 from the archive), which produces a slow figure-of-eight
wobble of the star in the sky rather than a fixed point. That is a real, cheap, striking visual and
nobody renders it.

---

## 6. Imagery: licences, and what is data versus illustration

### 6.1 The licences

- **NASA** still images, audio and video are generally **not subject to copyright in the United
  States** and may be used for educational and informational purposes without permission; NASA
  asks to be acknowledged as the source. Exceptions: the NASA insignia and logotype are **not**
  public domain and are protected by law; third-party content licensed by NASA is marked with the
  copyright holder's name and must be cleared separately; identifiable people raise
  privacy/publicity issues for commercial use.
  <https://www.nasa.gov/nasa-brand-center/images-and-media/>
- **ESO** images, videos and music on the public website are under **CC BY 4.0**, reproducible
  without fee provided the credit is clear, visible, complete and not separated from the material
  (format `ESO/Name`). The ESO logo needs written consent; scientific papers and code are **not**
  CC. <https://www.eso.org/public/copyright/>
- **STScI / Hubble and Webb** public-release imagery is credited `NASA, ESA, CSA, STScI` and
  distributed for free non-commercial use with credit; check each release page, because
  Webb/Hubble releases increasingly involve ESA co-ownership with its own terms.
- **ESA** content: ESA operates its own terms (largely CC BY-SA 3.0 IGO for many assets) and these
  are **not** the same as NASA's public domain. Check the individual page.

Practical consequence for this project: NASA artist concepts are the safest asset class legally and
the most dangerous editorially. Log every one in `Assets/_sources/CREDITS.md` per the working rules,
and label each in the app.

### 6.2 The hard truth about exoplanet pictures

**No exoplanet has ever been imaged at surface resolution. Not one.** Every picture of an exoplanet
landscape, cloud band, ocean, continent, lava flow or ring system in existence is an illustration.
The only planets on this list that have been *seen at all* - as unresolved dots of their own light,
separated from their star - are the HR 8799 planets and Beta Pictoris b and c. Even those are single
pixels or small point-spread functions, not discs; their "appearance" is a spectrum, not a picture.

That gives a clean three-tier labelling scheme the app could adopt verbatim:

| Tier | What it means | Examples on this list |
|---|---|---|
| **Observed** | Real photons from the object, resolved from its star | HR 8799 b/c/d/e (Keck, Gemini, VLT/SPHERE, JWST NIRCam); Beta Pic b and c; the Beta Pic debris disk; the Alpha Cen A/B pair |
| **Measured, not pictured** | A real physical property measured spectroscopically or photometrically, but no image | HD 189733 b's blue colour; TRAPPIST-1 b's dayside temperature; transit depths giving every radius on this list |
| **Illustration** | Artist's reconstruction, no imaging data of the object's surface or appearance | Every NASA/ESO/ESA "surface of TRAPPIST-1e" or "Kepler-16b with two suns" image ever released |

The per-system sections below flag which of the famous pictures fall in which tier.

---

# 7. The systems

Each block gives the star, the planets, what they are actually thought to be, the honest appearance,
and a **drop-in data block** in the exact `label | value | unit | exponent` form of
`docs/copy/bodies.md`. Masses are in kg with a mantissa and exponent, as that file does; the Earth
and Jupiter equivalents are given alongside for cross-checking. Diameters are computed from the
archive's radius in Earth radii at 12,742 km per Earth diameter.

<!-- SYS1 -->
<!-- SYS2 -->
<!-- SYS3 -->
<!-- SYS4 -->
<!-- SYS5 -->
<!-- SYS6 -->
<!-- SYS7 -->
<!-- SYS8 -->

## 7.9 HD 110067 - the second resonant chain

**Role: a resonant chain in a different key.** Six sub-Neptunes round a bright K dwarf, locked in a
3:2, 3:2, 3:2, 4:3, 4:3 chain. Where TRAPPIST-1 is seven rocks round a tiny red star, this is six
puffy volatile-rich worlds round a star only slightly cooler than the Sun, and the host is *bright*
(V = 8.42), so it is a system we will keep learning about.

**Star.** K0 V. Archive composite: Teff 5,266 K, R 0.788 Rsun, M 0.798 Msun, log L -0.362 so
L = 0.434 Lsun, V = 8.419, distance 32.159 pc = **104.9 ly**, Coma Berenices. The discovery paper
gives Teff ~5,400 K, M ~0.80 Msun, R ~0.86 Rsun and an age of ~7-8 Gyr (old thin-disc), so
**Teff and radius are mildly disputed** - a 130 K and 0.07 Rsun spread. Archive:
<https://exoplanetarchive.ipac.caltech.edu/TAP/sync> `pscomppars`; paper: Luque et al. 2023, Nature,
<https://arxiv.org/abs/2311.17775>.

- **Computed colour** `#FFEAD9` - warm white, barely distinguishable from the Sun by eye. Visible
  output fraction 0.337 against the Sun's 0.367.
- **Single star.** No companion. A "two suns" view here would be a lie.
- **Not a flare star** in any published sense; it is an old, quiet, metal-normal K dwarf.
- **Conservative HZ 0.663-1.175 AU; optimistic 0.505-1.222 AU** (my Kopparapu computation). **No
  planet is anywhere near it** - the outermost, g, is at 0.262 AU with 6.3 times Earth's insolation.
  This system is emphatically not a habitability story and should not be sold as one.

**Planets.** All six transit, so all six radii are measured. Only b, d and f have measured masses
(HARPS-N/CARMENES radial velocity); **c, e and g have upper limits only**.

| Planet | Radius (Re) | Mass (Me) | a (AU) | Period (d) | Star's disc | Insolation | Teq(A=0) |
|---|---|---|---|---|---|---|---|
| b | 2.200 +/- 0.030 | 5.69 (+1.78/-1.82) | 0.0793 | 9.114 | 5.29 deg (9.9x Sun) | 69.1 | 803 K |
| c | 2.388 +/- 0.036 | **< 6.3** | 0.1039 | 13.674 | 4.04 deg (7.6x) | 40.2 | 701 K |
| d | 2.852 +/- 0.039 | 8.52 (+3.31/-3.25) | 0.1362 | 20.520 | 3.08 deg (5.8x) | 23.4 | 613 K |
| e | 1.940 +/- 0.040 | **< 3.9** | 0.1785 | 30.793 | 2.35 deg (4.4x) | 13.6 | 535 K |
| f | 2.601 +/- 0.042 | 5.04 (+1.89/-1.94) | 0.2163 | 41.059 | 1.94 deg (3.6x) | 9.28 | 486 K |
| g | 2.607 +/- 0.052 | **< 8.4** | 0.2621 | 54.770 | 1.60 deg (3.0x) | 6.32 | 442 K |

Resonance chain, from the paper: Pc/Pb = 1.5003, Pd/Pc = 1.5007, Pe/Pd = 1.5007, then 4/3 and 4/3
for f and g. The paper describes it as a Laplace chain with the generalised Laplace angles near
equilibrium. Eccentricities are not published as free values - the archive carries blanks.

**What they actually are.** Measured: radii, three masses, and therefore three bulk densities -
b 2.94, d 2.02, f 1.58 g/cm3 (archive `pl_dens`; my own computation from the mass and radius
reproduces all three, so they are self-consistent). Earth is 5.51 and Neptune 1.64, so f is
*less dense than Neptune*. Modelled conclusion, quoted from the paper: all the planets
"with the exception perhaps of planet e" must have **large hydrogen-dominated atmospheres** to
explain densities that low. So the honest description is **puffy sub-Neptunes with deep hydrogen
envelopes and no visible surface**, not water worlds and definitely not rocky. Anything in the app
depicting a *surface* here is invented. A banded, hazy, featureless pale disc is the defensible look.

**Honest appearance.** The star is a warm white disc 3 to 5 degrees wide - six to ten Suns - hanging
over cloud decks at 440 to 800 K. Neighbours are visible as small discs but not dramatic ones: the
best case is HD 110067 c seen from b at 0.474 deg, **0.9 times the width of our Moon**, so a real
disc with a real phase. Nothing on this list has an observed atmosphere yet; the system is a prime
JWST target precisely because nobody has looked properly.

**Imagery.** ESA/CHEOPS and NASA released illustrations in November 2023. All are **illustrations**.
There are no images of these planets. The one diagram genuinely worth matching is the paper's own
resonance-chain figure, because the 3:2-3:2-3:2-4:3-4:3 structure is the whole point and it is data.

**Drop-in data block.**

```
## hd110067_b
**Title:** HD 110067 b
**Subtitle:** FIRST IN THE CHAIN

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 28,032 | km | |
| Mass | 3.40 x 10 | kg | 25 |
| Orbital Period | 9.11 | days | |
| Orbital Distance | 0.0793 | AU | |
| Day Length | Not yet measured | | |

## hd110067_c
| Diameter | 30,428 | km | |
| Mass | Not yet measured | | |      <- upper limit only, < 6.3 Earth masses
| Orbital Period | 13.67 | days | |
| Orbital Distance | 0.1039 | AU | |
| Day Length | Not yet measured | | |

## hd110067_d
| Diameter | 36,340 | km | |
| Mass | 5.09 x 10 | kg | 25 |
| Orbital Period | 20.52 | days | |
| Orbital Distance | 0.1362 | AU | |
| Day Length | Not yet measured | | |

## hd110067_e
| Diameter | 24,719 | km | |
| Mass | Not yet measured | | |      <- upper limit only, < 3.9 Earth masses
| Orbital Period | 30.79 | days | |
| Orbital Distance | 0.1785 | AU | |
| Day Length | Not yet measured | | |

## hd110067_f
| Diameter | 33,142 | km | |
| Mass | 3.01 x 10 | kg | 25 |
| Orbital Period | 41.06 | days | |
| Orbital Distance | 0.2163 | AU | |
| Day Length | Not yet measured | | |

## hd110067_g
| Diameter | 33,218 | km | |
| Mass | Not yet measured | | |      <- upper limit only, < 8.4 Earth masses
| Orbital Period | 54.77 | days | |
| Orbital Distance | 0.2621 | AU | |
| Day Length | Not yet measured | | |

## hd110067_star
| Diameter | 1,097,448 | km | |     <- 0.788 Rsun
| Mass | 1.59 x 10 | kg | 30 |      <- 0.798 Msun
| Surface Temperature | 5,266 | K | |
```

Relative Size layout with the star at 3 m: b 0.077, c 0.083, d 0.099, e 0.068, f 0.091, g 0.091 m.
Star-to-smallest ratio 44:1, inside the 5 cm - 3 m clamp. Orbit view needs 151x magnification to
reach 1.5 m.

## 7.14 Kepler-90 - eight planets, all inside Earth's orbit

**Role: the very large number of planets.** Eight confirmed, tying our own count, and the reason it
is famous is that the eighth was found by a neural network trained on Kepler light curves.

**Star.** No published spectral type anywhere in the archive's thirteen reference rows - it is a
late-F or early-G dwarf a little hotter and larger than the Sun. **The parameters are genuinely
disputed**: across references the archive carries Teff from 5,970 K to 6,330 K, radius from 1.054
to 1.290 Rsun, mass from 0.967 to 1.242 Msun, and **age from 0.53 Gyr to 3.55 Gyr**. The composite
row uses Teff 6,080 K, R 1.20 Rsun, M 1.20 Msun (Santerne et al. 2016 / Cabrera et al. 2014) while
the archive's own overview page shows 6,103 K and 1.02 Rsun. Best modern values are probably
Fulton & Petigura 2018 and Berger et al. 2018: Teff ~6,015 K, R 1.19-1.21 Rsun. log L 0.269 gives
L = 1.86 Lsun. Distance 848.25 +/- 10.82 pc = **2,767 ly**, in Draco. Note NASA's own 2017 release
says 2,545 ly - that predates the Gaia distance; the 2,767 ly figure is the current one.
Sources: <https://exoplanetarchive.ipac.caltech.edu/overview/Kepler-90>,
<https://www.nasa.gov/news-release/artificial-intelligence-nasa-data-used-to-discover-eighth-planet-circling-distant-star/>.

- **Computed colour** `#FFF4F3` - white, very slightly cooler than the Sun. Visible fraction 0.379.
- **Single star**, no known companion.
- **Conservative HZ 1.304-2.266 AU; optimistic 1.010-2.356 AU** (my computation). **No planet is in
  it.** The outermost, h, sits at 0.971 AU with twice Earth's insolation.

**Planets.** All eight transit. **The critical honesty flag: only g and h have measured masses**
(from transit-timing variations, 15.0 +/- 1.3 and 203 +/- 16 Earth masses); b, c, i, d, e and f
carry masses *estimated from their radii* by a mass-radius relation and are marked as such by the
archive itself.

| Planet | Radius (Re) | Mass (Me) | a (AU) | Period (d) | Star's disc | Insolation | Teq(A=0) |
|---|---|---|---|---|---|---|---|
| b | 1.31 +/- 0.17 | 2.27 *estimated* | 0.0740 | 7.008 | 8.63 deg (16.2x Sun) | 339 | 1,195 K |
| c | 1.19 +/- 0.14 | 1.81 *estimated* | 0.0890 | 8.719 | 7.18 deg (13.5x) | 234 | 1,090 K |
| i | 1.32 | 2.30 *estimated* | 0.1201 | 14.449 | 5.32 deg (10.0x) | 129 | 938 K |
| d | 2.87 +/- 0.30 | 8.60 *estimated* | 0.3200 | 59.737 | 2.00 deg (3.7x) | 18.1 | 575 K |
| e | 2.66 +/- 0.29 | 7.56 *estimated* | 0.4200 | 91.939 | 1.52 deg (2.9x) | 10.5 | 502 K |
| f | 2.88 +/- 0.52 | 8.65 *estimated* | 0.4800 | 124.914 | 1.33 deg (2.5x) | 8.06 | 469 K |
| g | 7.718 | **15.0 +/- 1.3 measured** | 0.7170 | 210.735 | 0.89 deg (1.7x) | 3.61 | 384 K |
| h | 11.252 | **203 +/- 16 measured** | 0.9706 | 331.603 | 0.66 deg (1.2x) | 1.97 | 330 K |

Eccentricities are near zero for b-f and 0.028-0.029 for g and h (archive).

**The single best fact about this system**, and it is worth a whole card: **all eight planets orbit
inside Earth's orbit**. The outermost has a 332-day year at 0.97 AU. Kepler-90 h at 0.97 AU is a
Saturn-mass planet (0.64 Mjup, 1.00 Rjup) sitting almost exactly where Earth sits. The architecture is our own solar system's ordering - small worlds inside, giants outside - compressed by a
factor of 30. Andrew Vanderburg's own line in the NASA release: "like a mini version of our solar
system... but everything is scrunched in much closer."

**What they actually are.** Measured: eight radii, two masses. Everything else is inference.
b, c and i are 1.2-1.3 Earth radii at 938-1,195 K - **almost certainly airless or nearly so, molten
or baked rock**, but no atmosphere observation exists. d, e and f at 2.7-2.9 Earth radii sit above
the radius valley, so they are **sub-Neptunes with volatile envelopes**, modelled not measured.
g is 7.7 Earth radii at 15 Earth masses, which works out to about **0.18 g/cm3 - extraordinarily
puffy, roughly a fifth the density of water and lighter than balsa wood**. A caution here: the
archive's composite row carries 0.272 g/cm3 for g and 0.912 for h, and neither is consistent with
the radius and mass in the same row (my recomputation gives 0.18 and 0.79). That is a sign the
composite table is mixing a radius from one reference with a mass from another, and it is a good
reason to take densities from a single paper rather than from `pscomppars`. Either way g is one of
the puffiest planets known. h is Saturn-like at roughly 0.8 g/cm3.

**Honest appearance.** No atmospheric observation of any kind: at 2,767 ly with V = 13.9 this system
is far too faint and distant for transmission spectroscopy. Everything about the appearance of these
eight planets is invention. What *is* honest: the star is a Sun-coloured disc, 16 Suns wide from
planet b and still 1.2 Suns wide from h; from planet c the largest neighbour (b) is 0.43 deg,
0.8 of our Moon. There are no direct images and there never will be with current technology.

**Imagery.** The NASA illustration by Wendy Stenzel accompanying the December 2017 release is an
**artist's concept** and NASA labels it as such; it is public domain
(<https://www.nasa.gov/news-release/artificial-intelligence-nasa-data-used-to-discover-eighth-planet-circling-distant-star/>).
The diagram worth matching is the standard Kepler-90-versus-solar-system orbit comparison, because
the compression is the real content.

**Drop-in data block.** Flagging estimated masses is essential here.

```
## kepler90_b
| Diameter | 16,692 | km | |
| Mass | 1.36 x 10 | kg | 25 |      <- ESTIMATED from radius, not measured
| Orbital Period | 7.01 | days | |
| Orbital Distance | 0.0740 | AU | |
| Day Length | Not yet measured | | |

## kepler90_c
| Diameter | 15,163 | km | |
| Mass | 1.08 x 10 | kg | 25 |      <- ESTIMATED
| Orbital Period | 8.72 | days | |
| Orbital Distance | 0.0890 | AU | |

## kepler90_i
| Diameter | 16,819 | km | |
| Mass | 1.37 x 10 | kg | 25 |      <- ESTIMATED
| Orbital Period | 14.45 | days | |
| Orbital Distance | 0.1201 | AU | |

## kepler90_d
| Diameter | 36,570 | km | |
| Mass | 5.14 x 10 | kg | 25 |      <- ESTIMATED
| Orbital Period | 59.74 | days | |
| Orbital Distance | 0.3200 | AU | |

## kepler90_e
| Diameter | 33,894 | km | |
| Mass | 4.51 x 10 | kg | 25 |      <- ESTIMATED
| Orbital Period | 91.94 | days | |
| Orbital Distance | 0.4200 | AU | |

## kepler90_f
| Diameter | 36,697 | km | |
| Mass | 5.17 x 10 | kg | 25 |      <- ESTIMATED
| Orbital Period | 124.91 | days | |
| Orbital Distance | 0.4800 | AU | |

## kepler90_g
| Diameter | 98,343 | km | |
| Mass | 8.96 x 10 | kg | 25 |      <- measured, 15.0 Earth masses
| Orbital Period | 210.74 | days | |
| Orbital Distance | 0.7170 | AU | |

## kepler90_h
| Diameter | 143,373 | km | |
| Mass | 1.21 x 10 | kg | 27 |      <- measured, 203 Earth masses = 0.64 Jupiter
| Orbital Period | 331.60 | days | |
| Orbital Distance | 0.9706 | AU | |

## kepler90_star
| Diameter | 1,671,240 | km | |     <- 1.20 Rsun (disputed, 1.05-1.29)
| Mass | 2.39 x 10 | kg | 30 |      <- 1.20 Msun (disputed, 0.97-1.24)
| Surface Temperature | 6,080 | K | |   <- disputed, 5,970-6,330
```

Relative Size with the star at 3 m: b 0.030, c 0.027, i 0.030, d 0.066, e 0.061, f 0.066, g 0.177,
h 0.257 m. Star-to-smallest is **110:1, outside the 5 cm - 3 m clamp** - one of only three systems
here with our own solar system's problem. Either clamp c at 5 cm and accept a small error, or drop
the star from the Relative Size view and show the eight planets against each other. Orbit view needs
41x magnification to reach 1.5 m, the mildest of any compact system here.

<!-- SYS15 -->

---

## 8. Which to build first, ranked by how much is actually measured

My own assessment, counting measured quantities against modelled ones. "Measured" means a
published value derived from photons from the object: a transit depth, a radial-velocity
amplitude, a transit-timing mass, a spectrum, a direct detection.

| Rank | System | Measured | Modelled or absent | Verdict |
|---|---|---|---|---|
| 1 | **TRAPPIST-1** | 7 radii, 7 masses (TTV), 7 densities, dayside temperatures for b and c, atmosphere constraints for b/c/d/e | surfaces, colours, clouds, rotation | **Build first.** Nothing else comes close for measured completeness, and it fills the marquee role. |
| 2 | **HD 189733** | radius, mass, geometric albedo, day and night temperatures, wind speed, sodium, water/CO2 | cloud structure, "glass rain" | **Build second.** The only planet anywhere whose *colour* is measured. One planet, so cheap. |
| 3 | **HD 110067** | 6 radii, 3 masses, 3 densities, a resonance chain to 4 decimal places | 3 masses, all atmospheres | Clean, modern, uncontroversial, and the resonance is a great interaction. |
| 4 | **Kepler-16** | both stellar masses and radii to high precision (double-lined eclipsing binary), planet mass and radius, coplanarity to 0.5 deg | planet appearance entirely | Best-measured *stars* on the list. Fills the circumbinary role with real numbers. |
| 5 | **LHS 1140** | 2 radii, 2 masses, densities, a JWST atmosphere programme | atmosphere unconfirmed | Good, and its revision history is a teaching asset (see 7.4). |
| 6 | **HR 8799** | four direct detections, spectra, orbital motion observed over ~18 years | radii, true masses, appearance | The only system where the planets have actually been *seen*. Highest honesty ceiling for imagery. |
| 7 | **55 Cancri** | 1 radius (e), 5+2 minimum masses, a JWST secondary-eclipse spectrum of e | 6 radii, most masses' true values, 55 Cnc B entirely | Great story, messy data - masses actively disputed in 2025-2026. |
| 8 | **TOI-700** | 4 radii, stellar quiescence | **all four masses are estimates**, no atmosphere data | Honest but thin. Good HZ role, weak on measurements. |
| 9 | **Beta Pictoris** | 2 direct detections, the disk itself imaged since 1984, an astrometric mass | radii, a third planet in dispute | Superb imagery, unstable numbers. |
| 10 | **K2-18** | radius, mass, multi-epoch transmission spectra | the interesting part is disputed | Build it *as* the dispute, or not at all. |
| 11 | **WD 1856+534** | radius, a 2025 mass, stellar parameters | appearance entirely | One planet, one extraordinary image, cheap to build. |
| 12 | **Kepler-90** | 8 radii, 2 masses | 6 masses, every atmosphere, all appearance | The count is the content. Nothing about how they look is knowable. |
| 13 | **PSR B1257+12** | 3 true masses, 3 orbits | literally everything else | Historically essential, visually a blank. |
| 14 | **Proxima Centauri** | minimum masses, periods, the stars themselves | **no radii at all** (nothing transits) | Nearest and least measured. The irony is worth a card. |
| 15 | **51 Pegasi** | a minimum mass and a period | radius, true mass, appearance | Historically vital, empirically almost bare. |

Two consequences worth noting. **Proxima Centauri is both the nearest system and one of the least
measured**, because neither planet transits, so we have no radius and only a lower bound on mass -
the app's copy should not draw Proxima b as a blue-green Earth twin with a known size. And
**Kepler-90, the eight-planet headliner, has no measurable appearance whatsoever** at 2,767 ly; if
it is built, it should be built as an architecture diagram rather than a set of worlds.

<!-- PART3 -->


