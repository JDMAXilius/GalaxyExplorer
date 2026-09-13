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
| `Assets/Textures/nebulae/helix_texture.jpg` | Helix Nebula (NGC 7293), destination `helix` | ESA/Hubble release `heic0307a` — NASA, NOAO, ESA, the Hubble Helix Nebula Team, M. Meixner (STScI), and T.A. Rector (NRAO) — https://esahubble.org/images/heic0307a/ | CC BY 4.0 | 2026-09-11 | Original 8000x8000; centre-cropped square (already square) and downscaled to 2048x2048, JPEG q92 |
| `Assets/Textures/nebulae/orion_texture.jpg` | Orion Nebula (M42), destination `orion` | ESA/Hubble release `heic0601a` — NASA, ESA, M. Robberto (STScI/ESA) and the Hubble Space Telescope Orion Treasury Project Team — https://esahubble.org/images/heic0601a/ | CC BY 4.0 | 2026-09-11 | Original 18000x18000; downscaled to 2048x2048, JPEG q92 |
| `Assets/Textures/nebulae/crab_texture.jpg` | Crab Nebula (M1), destination `crab` — clean replacement for `Assets/Textures/crab_nebula_texture.jpg` (which has a credit line burnt into the corner) | ESA/Hubble release `heic0515a` — NASA, ESA and Allison Loll/Jeff Hester (Arizona State University); acknowledgement Davide De Martin (ESA/Hubble) — https://esahubble.org/images/heic0515a/ | CC BY 4.0 | 2026-09-11 | Original 3864x3864; downscaled to 2048x2048, JPEG q92 |
| `Assets/Textures/nebulae/homunculus_texture.jpg` | Homunculus / Eta Carinae, destination `homunculus` — clean replacement for `Assets/Textures/homunculus_texture.jpg` (burnt-in credit line) | ESA/Hubble release `heic1912a` — NASA, ESA, N. Smith (University of Arizona, Tucson), and J. Morse (BoldlyGo Institute, New York) — https://esahubble.org/images/heic1912a/ | CC BY 4.0 | 2026-09-11 | Original 1779x1805 (no larger release available); centre-cropped square and downscaled to 1024x1024, JPEG q92 |
| `Assets/Textures/galaxies/deep_field_hubble_udf_texture.jpg` | Hubble Ultra Deep Field — source plate for the galaxy sprite atlas (CS-063) | ESA/Hubble release `heic0611b` — NASA, ESA, and S. Beckwith (STScI) and the HUDF Team — https://esahubble.org/images/heic0611b/ | CC BY 4.0 | 2026-09-11 | Original 6200x6200; downscaled to 2048x2048, JPEG q92 |
| `Assets/Textures/galaxies/deep_field_webb_smacs0723_texture.jpg` | Webb's First Deep Field (SMACS 0723) — second source plate for the galaxy sprite atlas | ESA/Webb release `webb-first-deep-field` — NASA, ESA, CSA, and STScI — https://esawebb.org/images/webb-first-deep-field/ | CC BY 4.0 | 2026-09-11 | Original 4537x4630; centre-cropped square and downscaled to 2048x2048, JPEG q92 |

Import settings for the six files above are applied automatically by
`Assets/scripts/Editor/AstronomyTextureImporter.cs` — mip maps on, Read/Write
off, and ASTC 6x6 on Android — so a re-downloaded or re-cropped plate cannot
come back uncompressed and readable. Menu **Cosmic Simulation → Reimport
Astronomy Textures** forces a pass.

## Reused instead of sourced (ticket CS-005)

These rows in the old pending table are already covered by textures inherited
from the MIT project, so nothing was downloaded for them:

| Subject | Existing file | Size |
|---|---|---|
| Pillars of Creation | `Assets/Textures/pillars_texture.tga` | 1774x1751 |
| NGC 1501 | `Assets/Textures/ngc1501_texture.jpg` | 690x691 |
| Trumpler 14 | `Assets/Textures/trumpler_texture.jpg` | 1200x1200 |
| Crab Nebula (old plate, still wired into `poi_magic_window_crab_nebula_material`) | `Assets/Textures/crab_nebula_texture.jpg` | 3864x2923 |
| Homunculus (old plate, still wired into `poi_magic_window_homunculus_nebula_material`) | `Assets/Textures/homunculus_texture.jpg` | 1600x1580 |

## Pending (ticket CS-005)

| Needed for | Subject | Intended source | Note |
|---|---|---|---|
| Planet map (optional upgrade) | Moon | USGS Astrogeology LROC | Deferred. `Assets/Textures/moon_diffuse_speculare_texture.tga` is 4096x2048 but is **not** an equirectangular map — it is an atlas laid out for the custom `moon_model` UVs, with specular in the alpha channel. A USGS LROC map is not a drop-in; swapping it needs a re-UV or a re-projection plus specular repacking, which belongs with the Phase 3 material work, not with sourcing. |
| Planet map (optional upgrade) | Earth | NASA Blue Marble | Deferred for the same reason: `Assets/Textures/earth_diffude_specular_texture.tga` is a 4096x4096 atlas (not 2:1 equirect) with specular in alpha, and there are matching normal/cloud plates keyed to the same layout. |

## Sky panorama generated 13 September 2026

| File | How it was made | Why it is not a photograph |
|---|---|---|
| `Assets/Textures/nebulae/domes/deep_sky_panorama.png` | Higgsfield, `gpt_image_2_5`, 1344 x 576, one prompt asking for at least 80 per cent empty black, sparse separated pinpoint stars and a single faint distant glow. Job `a29004e6-a293-41e7-9b23-268feb3ec587`. | **Generated, not observed, and it is the only part of a nebula place that is.** The gas, the dust, the stars in the volume and every colour in them come from the object's own telescope plate. No telescope has taken a full-sphere plate of the sky *around* any of these objects, so a real one cannot exist - which is exactly why the nebula's own square plate could never be the dome, and why it was being stretched across a patch of sky and washing the gas out. Shared by all seven nebulae: a starfield beyond the gas is not specific to which nebula you are standing in. |

## Narration generated 12 September 2026

The 22 original narration clips are the recorded performance inherited with Microsoft's Galaxy
Explorer under its MIT licence, and are reused unchanged. The 19 clips below did not exist and were
**synthesised**, not recorded.

| | |
|---|---|
| Tool | Higgsfield, model `seed_audio` (Seed Audio 1.0, ByteDance) |
| Voice | Preset "Holden" (`3c9d6053-6334-592c-8997-4e325286af3f`), male, middle-aged |
| Format | 24 kHz mono WAV (the originals are 44.1 kHz - see the note below) |
| Cost | about 16 credits |
| Scripts | the project's own copy, from `docs/copy/moons.md`, `docs/copy/nebulae.md` and the module panels |

**How the voice was chosen, and what it is not.** The original narrator's fundamental frequency was
measured across four of the inherited clips - earth, galactic_center, milky_way and saturn - giving a
median of about **112 Hz**, which is squarely male speech. Four male presets were then generated on the
same test line and measured the same way: Holden 102.6 Hz, Emmett 121.8 Hz, Arthur 151.9 Hz, Reid
175.2 Hz. Holden and Emmett were within 10 Hz either side and the owner chose Holden.

This is a **near match in register, not a clone**. No model was trained on the original recordings and
no attempt was made to reproduce the original performer's identity - that voice belongs to a person,
and synthesising new lines in it for a commercial release is a different act from reusing the MIT
clips. Anyone revisiting this should keep that distinction.

Clips: ganymede, callisto, io, europa, titan, mimas, iapetus, enceladus, phobos, deimos, andromeda,
cosmic_web, galaxies, helix, orion, whirlpool, pinwheel, triangulum, hd110067.

*Known gap:* the generated clips are 24 kHz against the originals' 44.1 kHz. Unity resamples on import
so they play correctly, but a careful listener on good headphones may hear the difference between an
inherited clip and a generated one. Regenerating at a higher sample rate, if the model supports it, is
the fix.
