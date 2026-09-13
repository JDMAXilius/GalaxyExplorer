# Bodies

The Sun, the eight planets and Pluto, plus the HD 110067 system's star and six
planets. Each has one paragraph (55 words maximum) and four or five stats.
Solar system values are NASA planetary fact sheet figures; HD 110067 values are
NASA Exoplanet Archive figures; diameters are equatorial. Where a mass or day
length has no measurement yet, the value column says so instead of a number.
ASCII only - the importer renders the mass exponent as a superscript from the
`exponent` column, by appending it straight after `value`, which is why every
Sun-to-Pluto mass value ends in "x 10": without it the superscript lands with
no "times ten" before it. **The four HD 110067 masses that have a measured value (star, b, d, f;
CS-132) do not have that suffix** - the pre-existing asset they were ported
from never had it either - so today they render as a bare mantissa with a
superscript stuck on, not scientific notation. Ported verbatim rather than
silently fixed; add " x 10" to those four values if the on-screen result
needs to match the rest.

Stat columns: `label | value | unit | exponent` (exponent blank unless the value
is a mantissa).

---

## sun
**Title:** The Sun
**Subtitle:** OUR STAR
**Kind:** Star

A middle-aged yellow dwarf star holding 99.8 per cent of the solar system's
mass. In its core, hydrogen fuses into helium at fifteen million degrees. The
light that leaves its surface takes eight minutes and twenty seconds to cross
the distance to Earth.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 1,392,700 | km | |
| Mass | 1.989 x 10 | kg | 30 |
| Orbit of the Galaxy | 230 million | years | |
| Day Length | 25.4 | days | |

---

## mercury
**Title:** Mercury
**Subtitle:** THE SMALLEST PLANET

The smallest planet and the closest to the Sun, cratered like the Moon and
holding almost no atmosphere to spread its heat. Sunlit ground reaches 430
degrees Celsius while the night side falls to minus 180. One day there lasts
longer than one of its years.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 4,879 | km | |
| Mass | 3.30 x 10 | kg | 23 |
| Orbital Period | 88 | days | |
| Day Length | 58.6 | days | |

---

## venus
**Title:** Venus
**Subtitle:** EARTH'S SCORCHING TWIN

Almost Earth's twin in size, wrapped in thick carbon dioxide clouds that trap
heat so effectively the surface sits near 465 degrees Celsius, hotter than
Mercury. It turns backwards compared with most planets, and so slowly that its
day outlasts its year.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 12,104 | km | |
| Mass | 4.87 x 10 | kg | 24 |
| Orbital Period | 225 | days | |
| Day Length | 243 | days | |

---

## earth
**Title:** Earth
**Subtitle:** OUR HOME PLANET

The only world known to carry life. Liquid water covers about seventy-one per
cent of the surface, the atmosphere holds the temperature steady, and a magnetic
field turns aside the solar wind. Its unusually large Moon steadies the tilt of
its axis and keeps the seasons regular.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 12,742 | km | |
| Mass | 5.97 x 10 | kg | 24 |
| Orbital Period | 365.25 | days | |
| Day Length | 24 | hours | |

---

## mars
**Title:** Mars
**Subtitle:** THE RED PLANET

Iron oxide in the dust gives Mars its colour. It holds Olympus Mons, the largest
volcano in the solar system, and a canyon system as long as the United States is
wide. Dry riverbeds and buried ice show that water once ran here.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 6,792 | km | |
| Mass | 6.42 x 10 | kg | 23 |
| Orbital Period | 687 | days | |
| Day Length | 24.6 | hours | |

---

## jupiter
**Title:** Jupiter
**Subtitle:** THE LARGEST PLANET

A gas giant with more than twice the mass of every other planet combined. The
Great Red Spot is a storm wider than Earth that has been turning for at least a
century and a half. Ninety-five confirmed moons circle it, among them Europa,
which hides an ocean under its ice.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 139,820 | km | |
| Mass | 1.90 x 10 | kg | 27 |
| Orbital Period | 11.86 | years | |
| Day Length | 9.9 | hours | |

---

## saturn
**Title:** Saturn
**Subtitle:** THE RINGED PLANET

The rings are billions of pieces of ice and rock, from dust grains to boulders,
spread into a sheet only tens of metres thick. The planet itself is less dense
than water. Its moon Titan has rivers and lakes of liquid methane under a thick
nitrogen sky.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 116,460 | km | |
| Mass | 5.68 x 10 | kg | 26 |
| Orbital Period | 29.4 | years | |
| Day Length | 10.7 | hours | |

---

## uranus
**Title:** Uranus
**Subtitle:** THE SIDEWAYS ICE GIANT

Uranus rolls around the Sun on its side, tipped by about ninety-eight degrees,
most likely knocked over long ago by a collision. Each pole spends roughly
forty-two years in sunlight and forty-two in darkness. Methane in its atmosphere
absorbs red light and leaves it pale blue-green.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 50,724 | km | |
| Mass | 8.68 x 10 | kg | 25 |
| Orbital Period | 84.0 | years | |
| Day Length | 17.2 | hours | |

---

## neptune
**Title:** Neptune
**Subtitle:** THE WINDIEST PLANET

The outermost planet was found in 1846 by arithmetic before anyone saw it:
astronomers predicted its position from wobbles in the orbit of Uranus and
pointed a telescope where the numbers said. Its winds are the fastest measured
anywhere in the solar system.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 49,244 | km | |
| Mass | 1.02 x 10 | kg | 26 |
| Orbital Period | 164.8 | years | |
| Day Length | 16.1 | hours | |

---

## pluto
**Title:** Pluto
**Subtitle:** A DWARF PLANET
**Kind:** Dwarf

Counted as the ninth planet from 1930 until 2006, Pluto is now the best known of
the dwarf planets. New Horizons flew past in 2015 and found mountains of water
ice and a vast heart-shaped plain of frozen nitrogen that is still being
resurfaced today.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 2,377 | km | |
| Mass | 1.31 x 10 | kg | 22 |
| Orbital Period | 248 | years | |
| Day Length | 6.4 | days | |

---

## hd110067_star
**Title:** HD 110067
**Subtitle:** THE HOST STAR
**Kind:** Star

A K-type dwarf a little cooler and smaller than the Sun, in Coma Berenices,
about 105 light years away. It is bright enough to keep studying, and old -
somewhere around seven to eight billion years. From any of its six planets it
would look like a warm white disc several times wider than the Sun looks from
Earth.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 1,097,448 | km | |
| Mass | 1.59 | kg | 30 |
| Day Length | Not yet measured | | |

---

## hd110067_b
**Title:** HD 110067 b
**Subtitle:** FIRST IN THE CHAIN

The innermost of six worlds locked into a rhythm: for every two orbits this
planet makes, the next one out makes three, and that pattern continues all the
way to the sixth. Its measured density is far too low for rock, so it almost
certainly carries a deep hydrogen atmosphere with no surface to stand on.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 28,032 | km | |
| Mass | 3.40 | kg | 25 |
| Orbital Period | 9.11 | days | |
| Orbital Distance | 0.0793 | AU | |
| Day Length | Not yet measured | | |

---

## hd110067_c
**Title:** HD 110067 c
**Subtitle:** SECOND IN THE CHAIN

Its size is measured, because it passes in front of its star and blocks a
little light. Its mass is not - only an upper limit is known, so how heavy it
is remains genuinely open.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 30,428 | km | |
| Mass | Not yet measured | | |
| Orbital Period | 13.67 | days | |
| Orbital Distance | 0.1039 | AU | |
| Day Length | Not yet measured | | |

---

## hd110067_d
**Title:** HD 110067 d
**Subtitle:** THIRD IN THE CHAIN

The largest of the six. Its density works out at about a third of Earth's,
which is the signature of a small rocky core wrapped in an enormous envelope of
hydrogen and helium.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 36,340 | km | |
| Mass | 5.09 | kg | 25 |
| Orbital Period | 20.52 | days | |
| Orbital Distance | 0.1362 | AU | |
| Day Length | Not yet measured | | |

---

## hd110067_e
**Title:** HD 110067 e
**Subtitle:** FOURTH IN THE CHAIN

The smallest of the six, and the one the discovery team singled out as possibly
different - it may be the only member of the family without a huge hydrogen
envelope. Its mass has not been measured, so that remains a maybe.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 24,719 | km | |
| Mass | Not yet measured | | |
| Orbital Period | 30.79 | days | |
| Orbital Distance | 0.1785 | AU | |
| Day Length | Not yet measured | | |

---

## hd110067_f
**Title:** HD 110067 f
**Subtitle:** FIFTH IN THE CHAIN

Less dense than Neptune. Whatever this world is made of, most of its volume is
atmosphere, and the solid part of it must be small.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 33,142 | km | |
| Mass | 3.01 | kg | 25 |
| Orbital Period | 41.06 | days | |
| Orbital Distance | 0.2163 | AU | |
| Day Length | Not yet measured | | |

---

## hd110067_g
**Title:** HD 110067 g
**Subtitle:** SIXTH IN THE CHAIN

The outermost known planet, taking about 55 days to go round. Even out here it
receives six times the sunlight Earth does - this system is packed far tighter
than ours, and none of it is in the zone where liquid water could sit on a
surface.

| label | value | unit | exponent |
|---|---|---|---|
| Diameter | 33,218 | km | |
| Mass | Not yet measured | | |
| Orbital Period | 54.77 | days | |
| Orbital Distance | 0.2621 | AU | |
| Day Length | Not yet measured | | |
