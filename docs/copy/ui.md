# UI text

Every label, tooltip and message. ASCII only. Buttons say what happens, in the
imperative; nothing is called by its internal name.

## Dock tiles

The name is written on the tile's picture. Only the three tiles that offer a
layout choice carry a second line, naming the view you get.

| id | name | second line |
|---|---|---|
| cosmic_web | Cosmic Web | — |
| galaxies | Galaxies | — |
| milky_way | Milky Way | — |
| andromeda | Andromeda | — |
| solar_system | Solar System | Orbital view |
| solar_system_planets | Solar System Planets | Detail view |
| sagittarius_a | Galactic Center | Black hole |

## Dock buttons

| id | label | tooltip |
|---|---|---|
| passthrough_on | Room | Show your room behind the stars |
| passthrough_off | Dark | Hide your room |
| recenter | Recenter | Bring the menu back in front of you |
| mute | Mute | Silence everything |
| unmute | Unmute | Turn the sound back on |
| help | Help | Show the controls |

## Layout pop-ups

| context | option | label | tooltip |
|---|---|---|---|
| solar_system | schematic | Schematic | Even spacing, planets enlarged |
| solar_system | realistic | Realistic | True orbit distances |
| solar_system_planets | row | Solar Row | One line, all the same size |
| solar_system_planets | relative | Relative Size | True sizes next to each other |

## Utility window

| id | label |
|---|---|
| scale | Size |
| text_size | Text size |
| narration | Narration |
| close | Close |

## Desktop controls overlay

**Title:** Controls

**Mouse**
- Click: select, open a card, pull a planet
- Drag: turn the view
- Right-drag: move the view
- Wheel: zoom
- Drag a pulled planet: move it
- Right-drag it: spin it
- Wheel over it: resize it

**Keyboard**
- 1 to 9, 0: pull the Sun through Pluto
- M: pull the Moon
- R: put everything back
- Home: recentre the view
- P: show or hide your room
- Tab: show or hide the menu
- Esc: close a card or this panel
- H or F1: this help

**Footer:** Press H or Esc, or click here, to close.

## About

**Title:** About Cosmic Simulation XR

Cosmic Simulation XR lets you hold the solar system in your own room. Pull a
planet out of the sky, grow it until it fills the space around you, and travel
from the Sun to the edge of the observable universe.

It is built on Galaxy Explorer, the open-source app Microsoft made for HoloLens
and released under the MIT licence. This edition rebuilds it for Meta Quest 3
with hand tracking and passthrough, and keeps a desktop mode for mouse and
keyboard.

Imagery from NASA, ESA and the Hubble Space Telescope. Facts from NASA planetary
fact sheets. NASA does not endorse this app.

**Buttons:** Source code · Privacy · Close

The store-ready version of this block (with version number, MIT attribution
line and placeholder link URLs) is `docs/store/ABOUT_COPY.md`. The live prefab
still carries six Microsoft links, not the two above — see
`docs/store/STORE_READINESS_CHECKLIST.md` (CS-086).

## Messages

| id | text |
|---|---|
| loading | Loading |
| narration_off | Narration off |
| narration_on | Narration on |
| layout_restored | Everything back in place |
| place_not_ready | {0} is not ready yet. It arrives in a later update. |
| place_failed | {0} did not open. Your room is back - pick another place from the menu. |

`{0}` is the tile's name from the table at the top of this file. The two
messages are deliberately different: a place with no content yet has not been
built, which is not a failure and should not read like one, while a place that
was meant to open and could not also has to account for the room reappearing.
Both live as defaults on `SwitchNotice` and are shown by `ExperienceDirector`;
`CopyImporter` does not read this file.
