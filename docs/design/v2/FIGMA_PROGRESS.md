# Figma transfer progress

Status: **in progress** (13 Oct 2026, local terminal session). V2-001 to V2-004 done, V2-005 built with two open items, V2-006 built and waiting on the owner's read of the logged conflicts. V2-007 onward not started.

Target: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/

| Ticket | Status | Evidence / blocker |
|---|---|---|
| V2-001 | done | Figma MCP `use_figma` reads and writes this file. Page inventory read, temporary frame created on `V2 / 00 — Read me`, read back by id, removed, removal confirmed. |
| V2-002 | done | All eight PNGs opened; SHA-256 and 1487 × 1058 match `references.json`. Regions and conflicts below. |
| V2-003 | done | Ten `V2 /` pages created; eight reference frames with the exact PNGs placed and labelled on the Read me page. |
| V2-004 | done | Variable collections, text styles and a Foundations specimen frame. Contrast checks not yet recorded. |
| V2-005 | in review | Eighteen components/sets built (list below, render in `evidence/components-library.jpg`). Open: per-button icon swap is not a property yet, so Enter nebula, Stop listening and similar show the play or compare icon; Larger-text and long-text behaviour not yet verified. |
| V2-006 | in review | S01 built from component instances at 1487 × 1058. Export, side-by-side and overlay saved under `evidence/`. Deviations are listed under "Decisions requiring resolution". |
| V2-007 | todo | — |
| V2-008 | todo | — |
| V2-009 | todo | — |
| V2-010 | todo | — |
| V2-011 | todo | — |
| V2-012 | todo | — |
| V2-013 | todo | — |
| V2-014 | todo | — |

## Actual Figma locations

File: https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/ (existing file; original pages untouched).

Existing pages before this work: `0:1` Design System, `2:2` Layout, `2:3` Components, `16:2` Ideas (Claude), `17:2` Before / Current Unity app, `24:2` Redesign / Options, `28:2` Refine / Keep and fix.

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

Reference frames on the Read me page: `39:32` (01), `39:34` (02), `39:36` (03), `39:38` (04), `39:40` (05), `39:42` (06), `39:44` (07), `39:46` (08).

Variables: `V2 / Primitives` (`VariableCollectionId:40:2`), `V2 / Semantic` (`40:3`, aliases of primitives), `V2 / Geometry` (`40:4`, spacing, radius, motion), `V2 / Alpha tints` (translucent states as RGBA colours, created with the components).

Text styles: `V2/Title` 36/42 Semi Bold, `V2/Heading` 24/30 Medium, `V2/Body` 20/27, `V2/Value` 24/30 Semi Bold, `V2/Label` 17/22, `V2/Caption` 16/20 Medium, `V2/Button` 19/24 Semi Bold. Foundations specimen frame: `40:44`.

Components on `V2 / 02 — Components`:

| Component | ID | Variants |
|---|---|---|
| Controls/Button | `44:128` | Type Primary/Secondary × State Default/Hover/Pressed/Focused/Disabled/Loading; text property `Label`, boolean `Show icon` |
| Controls/IconButton | `44:152` | State Default/Hover/Pressed/Focused/Disabled |
| Navigation/DockItem | `44:208` | Item Explore/Guide/Room × State Default/Active/Hover/Disabled |
| Navigation/Dock | `44:210` | three DockItem instances |
| Navigation/RailItem | `44:280` | Context ×4 × State Default/Selected/Hover/Focused |
| Navigation/Rail | `44:282` | four RailItem instances |
| Navigation/DestinationTile | `44:315` | State Default/Selected/Loading/Unavailable; text property `Text` |
| Navigation/Breadcrumb | `44:317` | — |
| Card/FactPair | `44:321` | — |
| Card/Information | `44:407` | Narration Off/Playing/Paused |
| Controls/Segmented | `53:105` | Options 2/3 × Selected, plus Disabled |
| Controls/ToggleRow | `53:132` | State On/Off/Disabled/Focused |
| Controls/ListRow | `53:185` | Kind Navigation/Value/Device selected/Device unavailable/Outlined/Hover |
| Card/DestinationPreview | `53:273` | State Ready/Loading/Unavailable |
| Guide/Status | `53:318` | Phase Idle/Connecting/Listening/Thinking/Speaking/Unavailable/Permission needed |
| Feedback/Hint | `53:365` | Gesture Palm/Pinch/Two-hand × State Active/Completed/Dismissed |
| Feedback/Notice | `53:393` | Kind Progress/Unavailable/Failure/Restored/Tracking |
| Feedback/Confirmation | `53:445` | Kind Quit/Restore × State Default/Waiting |

Screens: S01 `44:409` on `V2 / 04 — Explore`. S02–S39: pending. Prototype: pending.

Extracted assets on `V2 / 09 — Handoff`: companion cut-out `42:2` (from `08-design-system.png`), S01 scene background `42:244`.

## V2-002 measurements (canonical frame, pixels at 1487 × 1058)

| Region | Bounds (x, y → x, y) |
|---|---|
| Rail | 36, 97 → 150, 683 |
| Breadcrumb text | 594, 40 → 873, 70 |
| Picker tiles | selected 455, 91 → 598, 181; others start at x 608, 750, 893 (134 × 88) |
| Information card (bounding box of the perspective shape) | 1083, 118 → 1467, 733 |
| Card photo | 1103, 150 → 1443, 357 |
| Dock | 535, 897 → 950, 990 |
| Settings button | circle, centre 1408, 958, radius 30 |

Scene artwork (raster): room, hands, hero galaxy. Product raster: companion orb, rail context images, picker thumbnails, card photo. Everything else is native.

The room is pixel-aligned across the eight references to within small differences, so the S01 background was composited from `01` with the rail and picker areas patched from `04-guide.png` and `06-comparison.png`. No image was regenerated.

## Visual evidence

- `evidence/S01-figma-export.png` — Figma export of S01 at 1487 × 1058.
- `evidence/S01-reference-vs-figma.jpg` — reference left, export right.
- `evidence/S01-overlay-50pct.png` — 50% blend of the two.
- `evidence/S01-scene-background.png` — the composited background.

Editability: S01 is a frame with an image-fill background and seven kinds of component instance; all text is live. Device validation: unrun.

## Decisions requiring resolution

None of these has an owner response yet.

1. **Accent colour.** `tokens.json` and the design-system sheet both say `#6CCFDD`. The rendered pixels in the approved screens are a brighter cyan, about `#37E1FD` on the Listen button. The Figma variable uses the token value, so native accents look duller than the reference. It is one variable to change.
2. **Type scale.** `tokens.json` lists title 28, body 16, value 20, label 14. The canonical frame measures about 36, 20, 24 and 17. Styles follow the canonical frame; `tokens.json` is unchanged.
3. **Font.** Selawik is not available in this Figma account. Inter is used as the documented fallback, so widths and line breaks differ slightly from the renders.
4. **Perspective.** The reference card and rail are drawn in perspective. Native frames are flat; the card sits on the perspective shape's bounding box.
5. **Button radius.** `tokens.json` says 12 for controls. The reference shows about 18 on Listen and a full pill on Compare; components follow the reference.
6. **Card photo ratio.** The spec says 16:9; the canonical card photo measures 340 × 207 (about 1.64:1). The component follows the canonical frame.
7. **Icons.** Dock and settings icons are hand-written SVG; the settings gear path is from Lucide (ISC licence). They are close to, not traced from, the reference.
