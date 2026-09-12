# UI spec — Cosmic Simulation XR

The numbers a prefab builder needs, taken from the Figma file
`qWxL0ZGiyI7aRjnQAVISoI` (pages **Design System**, **Front End - Screens**, **Export - Unity**).
Design intent and rationale live in `docs/GDD.md` §8; this file is the measurements.

Everything is stated in **millimetres**, because the UI lives in world space and the player reads it at arm's
length. One Unity unit is a metre, so a millimetre is 0.001.

## 1. Colour

Sprites are white or alpha only. Colour arrives as a tint.

| Token | Value | Used for |
|---|---|---|
| `ink/primary` | `#FFFFFF` | titles, stat values, body text |
| `ink/secondary` | `#9EB8C4` | stat labels, instructions, subtitles |
| `accent/cyan` | `#6CCFDD` | hover, selection, dividers, accents |
| `accent/teal` | `#3FB6C9` | orbit rings in the solar system model |
| `surface/plate` | `#0E1418` at 80% | tag pills, dock, desktop panels |
| `surface/deep` | `#060B10` | full-black scenes behind the stars |
| `scrim/room` | `#000000` at 50% | dimming the passthrough room |
| `line/hairline` | `#6CCFDD` at 35% | dividers inside panels |

There is no semantic colour. Nothing in this app is an error or a warning.

`scrim/room` is what `EnvironmentController.dimOpacity` defaults to.

## 2. Type

Selawik in the app (already in `Assets/Fonts/`); Source Sans 3 stands in for it in the Figma file. Sizes are the
cap height the player sees at the panel's design distance.

| Role | Size | Weight | Tracking |
|---|---|---|---|
| Panel title | 16.8 mm | Light | 0 |
| Panel subtitle | 5.6 mm | SemiBold | 5 px |
| Stat value | 8.4 mm | Regular | 0 |
| Stat label | 4.55 mm | SemiBold | 4 px |
| Body | 6.65 mm | Regular | 0 |
| Instruction | 6.65 mm | Regular | 0 |
| Dock tile name | 5.6 mm | SemiBold | 0 |
| Dock tile subtitle | 4.2 mm | Regular | 0 |
| Destination tag | 5 mm | Medium | 0 |
| Held moon name | 18 mm | SemiBold | 0 |

Mass values carry a real superscript — `BodyInfo.Stat.ToRichText()` emits `<sup>`.

## 3. Space and radius

A 2 mm grid. Padding inside a panel is 5 mm. The gap between a body and the panel describing it is 30 mm.

Steps: 2, 4, 6, 8, 12, 16, 24, 30 mm.

| Radius | Applies to |
|---|---|
| 1 mm | drag bar (a pill: radius is half the height) |
| 2 mm | dock tile, small round buttons |
| 4 mm | tag pill |
| 6 mm | dock body, pop-ups, desktop panels |
| 8 mm | hint card |

## 4. Surfaces

Three treatments, and one rule: **text that floats over the room gets no plate, only an outline. Plates are for
controls.**

- **Floating text** — no plate. A 1 px dark outline keeps it legible over a bright room. Body, scene and moon
  panels are all of this kind.
- **Plate** — `surface/plate`, radius 6 mm. Dock, pop-ups, desktop panels, tag pills.
- **Black halo** — a radial fade to black behind a faint object, 1.6× its width, so gas reads against the room.
  Generated in code by `EnvironmentController`, not shipped as a sprite.

## 5. The dock

Reference: board **A - Dock**.

| Property | Value |
|---|---|
| Tile | 110 × 62 mm |
| Gap between tiles | 4 mm |
| Plate | 0.75 m in front of the player, 0.9 m up, tilted 25° toward them |
| Hover | tile lifts 6 mm toward the player and brightens |
| Poke | tile pushes 4 mm in |
| Open scene | keeps a cyan underline, 0.5 mm |
| Drag bar | 60 × 1.6 mm, white at 55%, under the plate |
| Small buttons | 9 mm square, radius 2 mm — Recenter and Help |

Each tile is a **picture of the place** with its **name written on the image**, bottom-left, over a soft dark
foot (`ui_tile_foot`, transparent at the top to 75% black at the bottom, 26 mm tall). Only the three tiles that
offer a layout choice carry a second line: Solar System, Solar System Planets, Galactic Center. The white
**Passthrough** button closes the row.

Tile art is not exported from Figma — the gradients on the board are placeholders. Real thumbnails are rendered
from the scenes themselves (ticket CS-031).

## 6. Panels, tags and hints

Reference: board **B - Panels, tags, hints**.

| Element | Size | Notes |
|---|---|---|
| Body panel | 161 mm wide | title, one paragraph, four stats in two columns |
| Scene panel | 161 mm wide | two paragraphs plus an instruction in `ink/secondary` |
| Moon panel | 120 mm wide | name at 18 mm, then parent / diameter / period on one line |
| Tag, idle | hug, 12 mm tall | `surface/plate`, radius 4 mm |
| Tag, hover | grows 15%, fills `accent/cyan`, text goes to `#0E1418` |
| Tag, selected | `surface/plate` with a 0.5 mm `accent/cyan` outline |
| Hint card | 110 × 60 mm | radius 8 mm, a 14 mm cyan ring, title, one line of body |

A hairline leader runs from a tag to its point on the galaxy. Hint cards appear twice, once, after the intro
places the Earth pin; each clears when the player does the thing, or after six seconds.

## 7. Exported sprites

`Assets/ui/figma/` — twelve files, about 21 KB in total. Import settings are applied automatically by
`Assets/scripts/Editor/UiSpriteImporter.cs`; the editable SVG sources are in `Assets/_sources/figma_svg/`.

Authored at **8 px per millimetre**, imported at **8000 pixels per unit**, so a sprite pixel is a millimetre and
the numbers above can be used directly.

| File | Size | Border | Radius |
|---|---|---|---|
| `ui_rounded_r8.png` | 24² | 10 | 1 mm |
| `ui_rounded_r16.png` | 40² | 18 | 2 mm |
| `ui_rounded_r32.png` | 72² | 34 | 4 mm |
| `ui_rounded_r48.png` | 104² | 50 | 6 mm |
| `ui_rounded_r64.png` | 136² | 66 | 8 mm |
| `ui_tile_foot.png` | 32 × 208 | — | stretched sideways |
| `icon_passthrough.png` | 128² | — | — |
| `icon_recenter.png` | 128² | — | — |
| `icon_help.png` | 128² | — | — |
| `icon_mute.png` | 128² | — | — |
| `icon_close.png` | 128² | — | — |
| `icon_back.png` | 128² | — | — |

The nine-slice border is the radius plus two pixels, so the corner arc sits inside the border and is never
stretched. Sprites are left uncompressed: they are a few kilobytes each and sit right in front of the player's
eyes, where block compression on a rounded corner costs far more to look at than it does to store.

A rounded rect is one white sprite tinted per use, rather than one sprite per colour — the dock plate, a tag
pill and a pop-up are the same file at different tints and radii.
