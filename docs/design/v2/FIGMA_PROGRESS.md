# Figma transfer progress

Status: **built, awaiting owner review** (13 Oct 2026, local terminal session). Everything in V2-001 to V2-012 exists in Figma. V2-013 and V2-014 are not closed: fidelity evidence exists for the seven exact-reference screens, but contrast is unmeasured, derived screens use stand-in scenes, and the open items below have no owner response.

File: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/ (existing file; original pages untouched).
Prototype page: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/?node-id=39-21

| Ticket | Status | Evidence / blocker |
|---|---|---|
| V2-001 | done | `use_figma` reads and writes the file. Temporary frame created, read back by id, removed, removal confirmed. |
| V2-002 | done | Eight PNGs opened; SHA-256 and 1487 × 1058 match `references.json`. Measurements below. |
| V2-003 | done | Ten `V2 /` pages; eight labelled reference frames on the Read me page. |
| V2-004 | done | Four variable collections, seven text styles, Foundations specimen `40:44`. Contrast not measured. |
| V2-005 | in review | 22 components/sets with variants (`evidence/components-library.jpg` shows the first 18). Larger-text and long-label check on the Layouts page (`evidence/layouts.jpg`): cards grow from 706 to 780 to 889 px, nothing clipped. |
| V2-006 | in review | S01 `44:409`; `evidence/S01-*`. Deviations under "Open items". |
| V2-007 | in review | S02–S14 on the Explore page (`evidence/explore-S01-S14.jpg`). Exact: S03, S08, S13. Derived frames carry an on-frame note. |
| V2-008 | in review | S15–S22 on the Guide page (`evidence/guide-S15-S22.jpg`). Exact: S17. |
| V2-009 | in review | S23–S27 (`evidence/settings-help-S23-S32.jpg`). Exact: S23. |
| V2-010 | in review | S28–S32, same evidence file. Exact: S29. |
| V2-011 | in review | S33–S39 (`evidence/flows-S33-S39.jpg`), Layouts page (`evidence/layouts.jpg`). |
| V2-012 | in review | 39 frames cloned to the Prototype page, about 290 reactions, ten named flows. Read-back: no frame without an outgoing link. Gaps listed below. |
| V2-013 | in progress | Export, side-by-side and 50% overlay for the seven exact references under `evidence/`. Not done: contrast measurement, separate foreground/background diff, measured bounds for screens other than S01. |
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

## Open items

Decisions taken without an owner response (all reversible):

1. **Accent.** The `cyan/400` primitive is `#37E1FD`, sampled from the approved renders. `tokens.json` still says `#6CCFDD`.
2. **Type scale.** Styles follow the canonical frame (36/24/20/17). `tokens.json` lists 28/22/16/14 and is unchanged.
3. **Button radius and photo ratio** follow the canonical frame (18 and pill; about 1.64:1), not the tokens (12; 16:9).

Known deviations and gaps:

4. Selawik is not available in Figma; Inter is used.
5. Reference cards and rails are in perspective; native ones are flat. Where a native card is shorter than the rendered one it is stretched to the reference height.
6. Map pins (S02, S03) and the Moon (S09) are raster in the scene art.
7. S23 keeps the reference wording "Version 2.0" and "Recenter the simulation view"; the spec wants real version metadata and a restore that is separate from menu recovery.
8. S08 keeps the reference label "Size Equal"; the copy table says "Size: Equal Size / Relative Size".
9. S13 shows "Earth diameter / Saturn diameter" as fact labels; the reference stacks name, "Diameter", value.
10. Facts and copy on derived cards (Milky Way, Cosmic Web, Sagittarius A*, Moon, HD 110067) and the Placement, About, Device chooser, Help index and desktop wording are proposed and not checked against repository content.
11. Derived screens have no approved scene art (nebula interior, Cosmic Web, Galactic Center, orbit model, dark scene).
12. Icons are hand-written SVG; the gear path is from Lucide (ISC licence).
13. In S23 the rendered dock sits slightly lower than the native one and shows faintly beneath it.
