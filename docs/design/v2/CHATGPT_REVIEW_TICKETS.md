# ChatGPT review-and-fix tickets

Track CHAT. These are repository tickets intended for the separate ChatGPT chat with its own Figma connection. They are not automatically delivered to another conversation or created as GitHub Issues. All start todo. Read `CHATGPT_HANDOFF.md`. Existing TERM tickets remain in `FIGMA_TICKETS.md`; reference them rather than duplicating completed work.

## CHAT-V2-001 — Inspect live work and establish ownership

Dependencies: none.

Inspect current file/page/node inventory. Identify V2 versus original/current-app pages. Record which frames/components Terminal has created, current selection, actual modification state if tools expose it, and what remains absent. Read Terminal's latest report if supplied; timestamp observations. Verify actual read and write tool capabilities.

Before mutations, establish the specific V2 pages/nodes ChatGPT owns while Terminal continues elsewhere, or have Terminal pause writes in those areas. Do not assume a stalled report or quiet chat is a write handoff. Until resolved, inspect and prepare fixes without mutation.

Acceptance: live inventory with file/page/node URLs, capability result and explicit ownership boundary. No claim of live inspection based solely on repository docs. Read-only/coordination blockers remain visible.

## CHAT-V2-002 — Inspect exact source images and capture current frames

Dependencies: none for source inspection; CHAT-V2-001 for live capture.

Open all eight PNGs listed in `references.json`, confirm dimensions/hash where supported, inspect actual pixels, and bind each to its intended live frame. Capture/export current Figma frames at corresponding reference dimensions and states. Reject blank, cropped, loading or wrong-frame captures. Include source/current pairs in the report.

Acceptance: all eight source images accessible and inspected; matched live captures or named missing-frame blockers. Missing source access is not permission to substitute another image. Preserve exact supplied assets.

## CHAT-V2-003 — Repair canonical exploration screen

Dependencies: CHAT-V2-001, CHAT-V2-002, write ownership.

Reference: `screens/01-galaxies.png`. Measure and repair narrow side rail, context label/icons, top thumbnails/breadcrumb, selected Andromeda, content silhouette/placement, photo-first card, typography/line breaks, two facts, Listen/Compare, short dock and corner Settings. Remove unsolicited extra controls from this V2 frame; do not delete unrelated assets/components.

Acceptance: source/before/after comparison, measured region checks, no unexplained visual discrepancies and native editable controls. Original imagery must not be regenerated to hide a mismatch. The corrected frame is the anchor for later fixes.

## CHAT-V2-004 — Repair variables, styles and component library

Dependencies: CHAT-V2-003.

Inspect actual native variables/styles/component sets and their use. Correct colors, type, radius, spacing, image crops and states to align with canonical approved appearance. Repair photo-first cards, rail, dock, tiles, buttons, segmented choices, toggles, hints, notices and Guide status. Resolve swapped icons, missing labels, inconsistent companion assets and broken instance links. Include hover/pressed/focus/disabled/loading and selected/on/off states.

Acceptance: actual native IDs, state/property matrix, editable auto-layout and consistent instances. Run text-size/long-title checks. Do not treat component-board illustrative font numbers as an override of the approved screen. Maintain compatible instance references when replacing main components.

## CHAT-V2-005 — Complete and correct exploration screens

Dependencies: CHAT-V2-004.

Inspect and reconcile S02–S14. Fix representative solar system, destination and comparison frames against `02`, `03` and `06` PNGs. Add genuinely missing map, interior, web, center, moon, exoplanet, chooser and detail states using approved components. Explicitly separate view, arrangement, relative size and orbital-distance accuracy; do not imply they are equivalent. Keep nested Back routes and supported non-voice destination access.

Acceptance: every S02–S14 state mapped to an actual frame/variant; before/after evidence for changed existing frames and source comparisons for reference-backed frames. Additional screens labelled derived. No silent decision on source/spec conflicts.

## CHAT-V2-006 — Complete and correct Guide states

Dependencies: CHAT-V2-004.

Reference `04-guide.png`; inspect S15–S22. Correct phase labels/actions, mic status, companion appearance and utility layout. Add absent idle/connecting/listening/thinking/speaking/failure/permission states. Keep manual exploration available. Suggested prompt buttons must be documented as proposed shortcuts, not verified service integration.

Acceptance: native state variants/frames, appropriate stop/retry/continue actions, unchanged exploration content and source-matched listening frame. No missing-mic dead end.

## CHAT-V2-007 — Complete and correct Settings, Help and recovery

Dependencies: CHAT-V2-004.

References `05-settings.png` and `07-help.png`; inspect S23–S32. Fix settings rows, text sizes, device chooser, About/credits, restore and quit confirmation. Repair transient gesture teaching and its completion/skip/replay states. Menu summon/Bring menu here must not restore objects. Remove permanent hand hints from normal exploration. Controller/desktop access cannot depend on palm gestures.

Acceptance: S23–S32 actual IDs and visual evidence; exact representative references matched; no unlabeled icon-only essentials or irreversible one-tap quit. Gesture instructions and diagrams agree, with runtime validation limitations recorded.

## CHAT-V2-008 — Complete feedback, environment and layouts

Dependencies: CHAT-V2-004.

Inspect S33–S39 plus Layouts page. Repair loading, unavailable, failure and tracking-loss feedback, Room on/off, desktop adaptation and panel-placement annotations. Preserve accurate recovery claims; do not promise a prior scene remains available without verifying intended behavior. Ensure bright-room text contrast and expanded text do not clip.

Acceptance: all state IDs and layout frames, documented recovery paths, adequate focus/contrast checks and explicit unrun device validation. No permanent extra global buttons or incompatible fourth dock item.

## CHAT-V2-009 — Fix prototype links and controlled cleanup

Dependencies: CHAT-V2-005 through CHAT-V2-008.

Trace first-use, galaxy, nested travel, solar, comparison, Guide and Settings flows. Correct dead/misdirected actions and missing dismissal. Remove verified obsolete or duplicate V2 frames/components only after checking incoming prototype/instance references, protecting recovery, and confirming Terminal is not editing them. Preserve approved references and original pages.

Acceptance: working native prototype links, actual prototype URL and per-flow start/end evidence. Cleanup log identifies removed node IDs and reason; no broken instances or disconnected routes after cleanup. If recovery cannot be established, leave removal blocked while completing unaffected fixes.

## CHAT-V2-010 — Re-export, verify and report completion

Dependencies: CHAT-V2-009.

Export all eight reference-backed frames/specimens and review source/current side by side. Produce overlay/diff evidence where available; distinguish scene raster from foreground UI and anti-aliasing differences. Recheck geometry, imagery, copy, typography, crop, selected states and all 39 screen/state mappings. Fix remaining mismatches and export again. Confirm editability, styles/variables, native components and prototype links rather than relying on visual flattening.

Acceptance: actual Figma file/page/frame/component URLs/IDs, before/after images and concise evidence for each ticket. State exactly which frames are one-to-one, which are derived and which remain blocked. Device and Unity behavior are unverified until tested separately. Do not mark completion merely because tickets, screenshots or a local handoff file exist.
