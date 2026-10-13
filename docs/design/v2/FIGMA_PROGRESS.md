# Figma transfer progress

Status: **built, awaiting owner review** (13 Oct 2026, local terminal session). Everything in V2-001 to V2-012 exists in Figma. V2-013 and V2-014 are not closed: fidelity evidence exists for the seven exact-reference screens, but some derived screens still lack proper scene art, and the open items below have no owner response.

File: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/ (existing file; original pages untouched).
Prototype page: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/?node-id=39-21

| Ticket | Status | Evidence / blocker |
|---|---|---|
| V2-001 | done | `use_figma` reads and writes the file. Temporary frame created, read back by id, removed, removal confirmed. |
| V2-002 | done | Eight PNGs opened; SHA-256 and 1487 × 1058 match `references.json`. Measurements below. |
| V2-003 | done | Ten `V2 /` pages; eight labelled reference frames on the Read me page. |
| V2-004 | done | Four variable collections, seven text styles, Foundations specimen `40:44`. Contrast not measured. |
| V2-005 | in review | 23 components/sets with variants (`evidence/components-library.jpg` shows the first 18). Larger-text and long-label check on the Layouts page (`evidence/layouts.jpg`): cards grow from 706 to 780 to 889 px, nothing clipped. |
| V2-006 | in review | S01 `44:409`; `evidence/S01-*`. Deviations under "Open items". |
| V2-007 | in review | S02–S14 on the Explore page (`evidence/explore-S01-S14.jpg`). Exact: S03, S08, S13. Derived frames carry an on-frame note. |
| V2-008 | in review | S15–S22 on the Guide page (`evidence/guide-S15-S22.jpg`). Exact: S17. |
| V2-009 | in review | S23–S27 (`evidence/settings-help-S23-S32.jpg`). Exact: S23. |
| V2-010 | in review | S28–S32, same evidence file. Exact: S29. |
| V2-011 | in review | S33–S39 (`evidence/flows-S33-S39.jpg`), Layouts page (`evidence/layouts.jpg`). |
| V2-012 | in review | 39 frames cloned to the Prototype page, about 290 reactions, ten named flows. Read-back: no frame without an outgoing link. Gaps listed below. |
| V2-013 | in progress | Export, side-by-side and 50% overlay for the seven exact references under `evidence/`. Contrast measured (see Review pass). Not done: separate foreground/background diff, measured bounds for screens other than S01, refreshed per-screen comparisons for S08 and S23. |
| V2-014 | in progress | This file, Handoff page, `manifest.json`. Tickets are not closed pending the owner's review. |

## Pages

| Page | ID |
|---|---|
| V2 / 00 — Read me | `39:13` |
| V2 / 01 — Design system | `39:14` |
| V2 / 02 — Components | `39:15` |
| V2 / 03 — Layouts | `39:16` |
| V2 / 04 — Explore | `39:17` |
| V2 / 05 — Guide | `39:18` |
| V2 / 06 — Settings & help | `39:19` |
| V2 / 07 — Flows & states | `39:20` |
| V2 / 08 — Prototype | `39:21` |
| V2 / 09 — Handoff | `39:22` |

Pages that existed before: `0:1` Design System, `2:2` Layout, `2:3` Components, `16:2` Ideas (Claude), `17:2` Before / Current Unity app, `24:2` Redesign / Options, `28:2` Refine / Keep and fix.

Reference frames (Read me page): `39:32`, `39:34`, `39:36`, `39:38`, `39:40`, `39:42`, `39:44`, `39:46` for references 01 to 08.

## Variables and styles

Collections: `V2 / Primitives` (`VariableCollectionId:40:2`), `V2 / Semantic` (`40:3`, aliases), `V2 / Geometry` (`40:4`), `V2 / Alpha tints` (translucent states as RGBA).

Text styles: `V2/Title` 36/42 Semi Bold, `V2/Heading` 24/30 Medium, `V2/Body` 20/27, `V2/Value` 24/30 Semi Bold, `V2/Label` 17/22, `V2/Caption` 16/20 Medium, `V2/Button` 19/24 Semi Bold.

## Components (`V2 / 02 — Components`)

| Component | ID | Variants and properties |
|---|---|---|
| Controls/Button | `44:128` | Type Primary/Secondary × State Default/Hover/Pressed/Focused/Disabled/Loading; `Label` text, `Show icon` boolean, `Icon` instance swap |
| Controls/IconButton | `44:152` | State ×5 |
| Controls/Segmented | `53:105` | Options 2/3 × Selected, Disabled |
| Controls/ToggleRow | `53:132` | On/Off/Disabled/Focused |
| Controls/ListRow | `64:1172` | Kind ×6; `Title`, `Description`, `Leading icon` |
| Icon/Glyph | `61:497` | 18 names × Tone Primary/OnAccent/Accent |
| Navigation/DockItem | `44:208` | Item ×3 × State Default/Active/Hover/Disabled |
| Navigation/Dock | `44:210` | — |
| Navigation/RailItem | `44:280` | Context ×4 × State Default/Selected/Hover/Focused |
| Navigation/Rail | `44:282` | — |
| Navigation/DestinationTile | `44:315` | Default/Selected/Loading/Unavailable; `Text` |
| Navigation/Breadcrumb | `44:317` | — |
| Navigation/MapPin | `71:395` | Default/Selected/Hover; `Text` |
| Card/FactPair | `44:321` | — |
| Card/Information | `44:407` | Narration Off/Playing/Paused |
| Card/DestinationPreview | `53:273` | Ready/Loading/Unavailable |
| Guide/Status | `53:318` | seven phases |
| Guide/Panel | `64:1392` | eight phases |
| Settings/Panel | `65:282` | — |
| Help/LessonPanel | `65:539` | Lesson Palm/Pinch/Two-hand × Teaching/Completed |
| Feedback/Hint | `53:365` | Gesture ×3 × Active/Completed/Dismissed |
| Feedback/Notice | `53:393` | Progress/Unavailable/Failure/Restored/Tracking |
| Feedback/Confirmation | `53:445` | Quit/Restore × Default/Waiting |

## Screens

"Exact" means built against a reference PNG. All others are derived and reuse a reference scene as a stand-in.

| ID | Screen | Design frame | Prototype frame | Basis |
|---|---|---|---|---|
| S01 | Galaxy exploration | `44:409` | `67:2643` | exact 01 |
| S02 | Milky Way map | `63:228` | `67:2722` | derived |
| S03 | Destination preview | `63:303` | `67:2791` | exact 03 |
| S04 | Nebula interior | `63:376` | `67:2859` | derived |
| S05 | Cosmic Web | `63:452` | `67:2927` | derived |
| S06 | Galactic Center | `63:531` | `67:2996` | derived |
| S07 | Solar Orbits | `63:607` | `67:3064` | derived |
| S08 | Solar Compare Row | `63:701` | `67:3152` | exact 02 |
| S09 | Held planet | `63:792` | `67:3237` | derived |
| S10 | Held moon | `63:875` | `67:3314` | derived |
| S11 | Exoplanet system | `63:950` | `67:3383` | derived |
| S12 | Choose comparison | `63:1029` | `67:3452` | derived |
| S13 | Compare pair | `63:1113` | `67:3534` | exact 06 |
| S14 | Expanded information | `63:1190` | `67:3603` | derived |
| S15 | Guide idle | `64:1394` | `67:3671` | derived |
| S16 | Guide connecting | `64:1453` | `67:3724` | derived |
| S17 | Guide listening | `64:1502` | `67:3767` | exact 04 |
| S18 | Guide thinking | `64:1559` | `67:3818` | derived |
| S19 | Guide speaking | `64:1605` | `67:3858` | derived |
| S20 | Guide unavailable | `64:1653` | `67:3900` | derived |
| S21 | Microphone permission | `64:1703` | `67:3945` | derived |
| S22 | Microphone denied or missing | `64:1753` | `67:3990` | derived |
| S23 | Settings | `65:541` | `67:4035` | exact 05 |
| S24 | Device chooser | `65:655` | `67:4147` | derived |
| S25 | About | `65:720` | `67:4201` | derived |
| S26 | Quit confirmation | `65:789` | `67:4259` | derived |
| S27 | Restore confirmation | `65:916` | `67:4384` | derived |
| S28 | Placement | `65:1043` | `67:4509` | derived |
| S29 | First-use palm lesson | `65:1080` | `67:4545` | exact 07 |
| S30 | Pinch lesson | `65:1125` | `67:4591` | derived |
| S31 | Two-hand lesson | `65:1170` | `67:4637` | derived |
| S32 | Help index | `65:1215` | `67:4683` | derived |
| S33 | Loading | `66:1274` | `67:4746` | derived |
| S34 | Destination unavailable | `66:1351` | `67:4818` | derived |
| S35 | Destination failed | `66:1430` | `67:4892` | derived |
| S36 | Tracking lost | `66:1509` | `67:4966` | derived |
| S37 | Room visible | `66:1525` | `67:5055` | derived |
| S38 | Room hidden | `66:1608` | `67:5134` | derived |
| S39 | Desktop adaptation | `66:1688` | `67:5214` | derived |

Reference 08 (design-system sheet) maps to the Design system and Components pages, not to a screen.

## Prototype

Flows (starting points on the Prototype page): First use (S28), Explore (S01), Guide (S15), Guide failure (S20), Microphone permission (S21), Settings (S23), Destination unavailable (S34), Destination failed (S35), Tracking lost (S36), Desktop (S39).

Read-back after wiring: every frame has at least one outgoing link. Frames with no inbound link, reached only through their own starting point: S11, S20, S21, S22, S34, S35, S36, S39 (S28 is the entry).

Not linked: S11 (the extended system chooser that should lead to it is not designed), narration Playing/Paused (variants exist on the card), the step after Quit. Timed steps stand in for connecting, thinking, loading and tracking recovery. Hand gestures are not simulated.

## V2-002 measurements (canonical frame, 1487 × 1058)

| Region | Bounds (x, y → x, y) |
|---|---|
| Rail | 36, 97 → 150, 683 |
| Breadcrumb text | 594, 40 → 873, 70 |
| Picker tiles | selected 455, 91 → 598, 181; others start at x 608, 750, 893 (134 × 88) |
| Information card (bounding box) | 1083, 118 → 1467, 733 |
| Card photo | 1103, 150 → 1443, 357 |
| Dock | 535, 897 → 950, 990 |
| Settings button | centre 1408, 958, radius 30 |

The room is pixel-aligned across the references to within small differences, so clean backgrounds were composited by patching the rail, picker and control areas from other references. No image was regenerated. The composites are on the Handoff page.

## Visual evidence (`evidence/`)

For S01, S03, S08, S13, S17, S23, S29: `<ID>-figma-export.jpg`, `<ID>-reference-vs-figma.jpg`, `<ID>-overlay-50pct.jpg`. Mean absolute difference per channel against the reference, whole frame: S01 7.6, S08 9.1, S03 7.0, S17 5.2, S23 5.4, S13 8.5, S29 11.2 (out of 255). These numbers include font, perspective and accent differences and are not a pass threshold.

Page overviews: `components-library.jpg`, `explore-S01-S14.jpg`, `guide-S15-S22.jpg`, `settings-help-S23-S32.jpg`, `flows-S33-S39.jpg`, `layouts.jpg`. `S01-scene-background.png` is the composited background.

Device validation: unrun.

## Review pass (13 Oct 2026, second pass)

- **Scene art.** S04, S05, S06, S07 and S38 now use the app's own Unity renders (desktop captures from `Logs/` and `.utmp/shots/`) instead of a reference scene. The Sagittarius A* and Orion renders are low resolution; no interior nebula capture is good enough to use.
- **Copy.** Derived cards use the wording in `docs/copy/experiences.md`, `nebulae.md` and `moons.md` (Milky Way, Cosmic Web, Sagittarius A*, Orion interior, HD 110067, Andromeda details, Moon). Sagittarius A* mass corrected from "4 million" to "4.3 million solar masses".
- **Map pins.** New `Navigation/MapPin` component (`71:395`, Default/Selected/Hover, `Text` property). Instances sit over the rendered labels on S02, S03, S33, S34, S35 and are wired in the prototype.
- **Spec wording.** `Settings/Panel`: About reads "Version and credits"; Restore scene reads "Put objects in this place back". S08 and S36: "View" and "Size" labels outside the controls, "Equal Size / Relative Size". Both depart from the reference images on purpose, following V2-009 and the copy table.
- **S11.** Reachable from a proposed "Other star systems" button on S07.
- **Narration.** Prototype frame `S01b` (`71:4243`) shows the Playing state; Listen and Pause toggle between it and S01.
- **Selected states.** The glow no longer shows through translucent fills; selected rail, dock and tile states are dark with a cyan outline, as in the references. Cards, dock and rail are opaque so the rendered copies underneath do not show through.
- **Contrast** (WCAG ratio, flat colours; table also on the Design system page): primary text on panel 16.9:1, on deep 18.6:1; secondary text on panel 8.2:1, on deep 9.0:1; dark text on accent 11.8:1; accent text on panel 10.8:1; attention 10.5:1; error 10.0:1; divider 2.0:1 (decorative). Text over passthrough is not covered by these numbers.
- **Prototype click-through** (Chrome, Figma prototype view): S01 → rail Milky Way → S02 → Orion pin → S03 → Enter nebula → S33 → timer → S04; dock Guide → S15 → Start listening → S16; settings gear → S23. All navigated. Other flows were verified by reading the links back, not by clicking.

Buttons with no link in the prototype, by design or not yet designed: Compare on galaxy and non-planet cards (needs a metric decision), Listen outside S01, Mic on, Transcript, Quit's final step, About's Close-to-source links.

The per-screen comparison images under `evidence/` for S08 and S23 predate the wording change; the page overviews `explore-S01-S14.jpg` and `flows-S33-S39.jpg` are current.

## Open items

Decisions taken without an owner response (all reversible):

1. **Accent.** The `cyan/400` primitive is `#37E1FD`, sampled from the approved renders. `tokens.json` still says `#6CCFDD`.
2. **Type scale.** Styles follow the canonical frame (36/24/20/17). `tokens.json` lists 28/22/16/14 and is unchanged.
3. **Button radius and photo ratio** follow the canonical frame (18 and pill; about 1.64:1), not the tokens (12; 16:9).
4. **Spec over reference** for the Settings and Solar wording above.

Known deviations and gaps:

5. Selawik is not available in Figma; Inter is used.
6. Reference cards and rails are in perspective; native ones are flat. Where a native card is shorter than the rendered one it is stretched to the reference height.
7. Rendered pin labels and leader lines are still in the scene image under the native pins. The Moon on S09 is raster with a hotspot.
8. S13 shows "Earth diameter / Saturn diameter" as fact labels; the reference stacks name, "Diameter", value.
9. Placement, About, Device chooser, Help index, "Other star systems" and desktop wording are proposed.
10. Icons are hand-written SVG; the gear path is from Lucide (ISC licence).
11. Nothing has been tried on a headset.
