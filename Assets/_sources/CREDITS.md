# Asset credits and licences

Every asset that did not ship with the original Galaxy Explorer project gets a
row here: what it is, where it came from, under what licence, and what we did to
it. Add the row **in the same commit that adds the asset** (working rule in
`CLAUDE.md`).

## Licence notes

| Source | Licence | Attribution required |
|---|---|---|
| Microsoft Galaxy Explorer (the project we forked) | MIT | Yes — kept in `LICENSE` and the About panel |
| NASA (nasa.gov, images.nasa.gov, science.nasa.gov) | Public domain | Not required; we credit anyway |
| NASA/ESA Hubble (esahubble.org) | CC BY 4.0 | Yes — "NASA, ESA and <team>" |
| ESA/Webb (esawebb.org) | CC BY 4.0 | Yes |
| USGS Astrogeology | Public domain | Not required; we credit anyway |
| Solar System Scope textures | CC BY 4.0 | Yes — "Solar System Scope (solarsystemscope.com)" |
| Higgsfield generations (ours) | Owned by the account holder | No |
| Windows Speech synthesis (ours) | Owned output | No |

NASA does not endorse this app; NASA imagery is used only as imagery.

## Assets added by us

| File | What | Source | Licence | Date | Edits |
|---|---|---|---|---|---|
| `Assets/audio/vo_audio_clips/vo_destinations_audio_clips/vo_destination_moon_audio_clip.wav` | Moon narration (placeholder) | Windows Speech (Zira), script written by us | Ours | 2026-09-11 | 44.1 kHz mono, normalised on import |
| `Assets/Textures/moon_preview_icon.png` | Moon icon for the desktop bar | Rendered in-project from `moon_model.fbx` + `moon_material` | Ours (derived from the MIT project) | 2026-09-11 | 256x256, alpha, lit from upper left |
| `Assets/Textures/icons/sprite_button_help.png` | Help icon | Drawn by us (System.Drawing) | Ours | 2026-09-11 | 256x256 white line art |
| `Assets/Textures/icons/sprite_button_view.png` | Recenter icon | Drawn by us | Ours | 2026-09-11 | 256x256 |
| `Assets/Textures/icons/sprite_button_sound_on.png` | Unmuted icon | Drawn by us | Ours | 2026-09-11 | 256x256 |
| `Assets/Textures/icons/sprite_button_sound_off.png` | Muted icon | Drawn by us | Ours | 2026-09-11 | 256x256 |
| `Assets/ui/figma/ui_rounded_r{8,16,32,48,64}.png` | Nine-slice rounded plates, 1/2/4/6/8 mm | Drawn by us in Figma `qWxL0ZGiyI7aRjnQAVISoI`, page **Export - Unity** | Ours | 2026-09-11 | White only, tinted at runtime; 8 px per mm |
| `Assets/ui/figma/ui_tile_foot.png` | Dark foot under a dock tile name | Drawn by us in Figma, same page | Ours | 2026-09-11 | 32x208, transparent to 75% black |
| `Assets/ui/figma/icon_{passthrough,recenter,help,mute,close,back}.png` | Dock and control icons | Drawn by us in Figma as SVG, same page | Ours | 2026-09-11 | 128x128 white line art; SVG sources in `Assets/_sources/figma_svg/` |

## Pending (ticket CS-005)

Imagery still to source, with the intended origin:

| Needed for | Subject | Intended source |
|---|---|---|
| Nebula overlay | Helix Nebula (NGC 7293) | NASA/ESA Hubble |
| Nebula overlay | Orion Nebula (M42) | NASA/ESA Hubble |
| Nebula overlay (upgrade) | Crab Nebula (M1) | NASA/ESA Hubble — project texture exists |
| Nebula overlay (upgrade) | Homunculus (Eta Carinae) | NASA/ESA Hubble — project texture exists |
| Galaxies field | 20+ galaxy cutouts | Hubble Ultra Deep Field / Webb deep field |
| Planet map (optional upgrade) | Moon | USGS Astrogeology LROC |
| Planet map (optional upgrade) | Earth | NASA Blue Marble |
