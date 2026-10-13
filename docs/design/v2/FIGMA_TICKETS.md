# Figma Version 2.0 tickets

Track: TERM owns Figma transfer and visual validation. CODE supports reusable helper tooling only if needed. All tickets start **todo**. This ticket set creates no GitHub Issues automatically. Do not mark tickets complete merely because this file exists.

The owner requires one-to-one visual reproduction. Read `TERMINAL_HANDOFF.md` first. Attach/reference the exact images from `references.json` in every visual ticket. Record results in `FIGMA_PROGRESS.md` with Figma node IDs and exports. Related existing specification: `specs/screens.md` (S01–S39).

## V2-001 — Verify Figma read/write connection

Dependencies: none. Owner: TERM.

- Confirm the authenticated account can inspect file `dx6MxAxPqZ9QBSRJqCCVev` and discover its current page list.
- Identify actual callable tools for page/node/component/variable creation. Read-only MCP does not meet the task.
- In the new V2 area, create and inspect a clearly labelled temporary test frame using the supported writer. Remove only this agent-created temporary frame after verifying the operation.
- If authorization must be completed by the owner, provide the precise supported login action; do not request tokens in chat.

Acceptance: account access, existing page inventory, writer capability and successful write/read-back are documented. Otherwise blocked with an exact reason, no fabricated tool calls. Remaining file preparation can continue.

## V2-002 — Inspect and lock reference assets

Dependencies: none. Owner: TERM.

Open all eight curated PNGs, verify their SHA-256 hashes/dimensions against `references.json`, and inspect them visually. Record the canonical shell/card image and its measured regions. Preserve originals. Identify what is scene artwork versus editable UI, and catalogue crop rectangles needed for photo fills, thumbnail images and companion artwork. Resolve reference/spec conflicts explicitly.

Acceptance: reference inventory, inspected images, region/crop measurements and any conflict notes; no replacement images or inferred reference selection. This ticket is independently executable without Figma.

## V2-003 — Create V2 Figma pages and reference sections

Dependencies: V2-001, V2-002. Owner: TERM.

Create pages from `README.md` with `V2 /` prefix in the existing file, preserving all existing pages. If using an authorized new file, name it `Cosmic Simulation XR — UI 2.0` and record its URL. Place exact original PNGs in clearly labelled Reference sections, including role and filename. Create a Read me scope/status section.

Acceptance: actual file URL, all ten page IDs, visible reference images verified by page inspection. Uploading unattached image assets does not satisfy visible placement.

## V2-004 — Native design-system variables and styles

Dependencies: V2-003. Owner: TERM.

Reference: `screens/08-design-system.png` and canonical shell; inspect actual appearance, then reconcile with `tokens.json` and `specs/design-system.md`. Create native primitives/semantic color variables, font styles, spacing/radius conventions and motion/spatial annotations. Keep pixel review measurements separate from millimetre world-space proposals. Verify font availability and record any substitution before use.

Acceptance: native variables/styles and visible specimens with IDs, screenshots and contrast checks; type hierarchy matches canonical screen. Do not blindly copy illustrative font numbers from generated design-system artwork.

## V2-005 — Editable components and interaction variants

Dependencies: V2-004. Owner: TERM; CODE helper only if needed.

References: canonical `01-galaxies.png`, `08-design-system.png`, `specs/components.md`. Create rail items, dock items, destination thumbnails, photo-first information/preview cards, fact pair, buttons, icon buttons, segmented controls, toggles, device rows, breadcrumbs, hints, notices, confirmations and Guide status. Use native auto-layout and editable text. Add default/hover/pressed/focus/disabled/loading and selected/on/off where relevant.

Acceptance: main component IDs and variant/property matrix, editable instances and verified long-text/Larger-text behavior. Exactly three global dock items, separate Settings, no permanent gesture hint or manipulation controls. Icons and imagery match approved references; no generic substitute companion.

## V2-006 — Recreate the approved exploration frame one-to-one

Dependencies: V2-005. Owner: TERM.

Exact primary image: `screens/01-galaxies.png`. Recreate S01 with native component instances at the reference's actual pixel dimensions. Preserve narrow rail proportions, top galaxy picker/breadcrumb, selected Andromeda, hero composition, photo-first card, description, two facts, Listen/Compare, dock and corner gear. Scene artwork can be a matched raster background; UI must remain editable.

Acceptance: exported Figma frame and comparison evidence. No visible hierarchy, content, typography, geometry or imagery discrepancy remains unexplained. This is the approval anchor for derived screens; do not redesign it.

## V2-007 — Explore family and local modes

Dependencies: V2-006. Owner: TERM.

References: `02-solar-system.png`, `03-destination-preview.png`, `06-comparison.png`. Cover S02–S14 with editable frames or intentional variants: map/preview/interior, Cosmic Web, Galactic Center, Orbits/Compare, Equal/Relative sizes, planet/moon, exoplanet entry, comparison chooser/pair and details. Keep derived frames visibly labelled as derived where no exact source PNG exists.

Acceptance: S02–S14 node mapping, reference-matched representative frames and documented behavior of new modes. Resolve screenshot/spec discrepancies before finalizing those affected states; do not silently mix orbital spacing with relative body size or imply unimplemented gestures work.

## V2-008 — Guide and microphone family

Dependencies: V2-006. Owner: TERM.

Reference: `04-guide.png`. Cover S15–S22: idle, connecting, listening, thinking, speaking, unavailable, permission-needed and denied/missing mic. Provide phase-appropriate controls; mic state differs from connection state. Keep background place intact; no mandatory giant transcript. User-facing copy comes from `specs/flows-and-copy.md`.

Acceptance: all phase/permission node IDs, stable layout through phase transitions and explicit non-voice fallback. Suggested questions are labelled proposed behavior until integrated; no secrets/settings implementation details in the product UI.

## V2-009 — Settings, recovery, About and confirmation

Dependencies: V2-006. Owner: TERM.

Reference: `05-settings.png`. Cover S23–S27 with native controls: master sound, narration, text sizes, microphone chooser, Help, About/credits, Bring menu here, Restore scene, Quit/cancel. Menu recovery must not restore objects. Quit has a confirmation. Use real runtime version metadata in production; UI 2.0 is the design version only.

Acceptance: screen IDs, editable controls and clear routing/dismissal. No size slider added in place of the rejected manipulation controls. Narration status must make master mute understandable.

## V2-010 — Placement, Help and temporary gesture teaching

Dependencies: V2-006. Owner: TERM.

Reference: `07-help.png`. Cover S28–S32: anchor placement, palm lesson, pinch lesson, two-hand lesson, Help index and controller/desktop alternatives. Keep hint temporary; render separate normal, teaching and completed states. Palm summon parks the menu; does not create a second permanent global menu.

Acceptance: replay/skip/success routes, temporary hint visible only in teaching/recovery frames. Gesture thresholds marked for device validation, not asserted by still-image prototype. Diagram and instructions must agree.

## V2-011 — Feedback, room states and desktop layouts

Dependencies: V2-006. Owner: TERM.

Cover S33–S39: destination loading/unavailable/failure, tracking loss, room on/off, desktop adaptation. Give correct recovery actions and avoid false success promises. Keep essential navigation available without microphone or palm gestures. Provide reference-aligned panel layout rules and long-label examples on Layouts page.

Acceptance: state node mapping, no empty/blank paths, no fabricated error-free runtime claim. Desktop preserves navigation and card hierarchy; Quest spatial measurements annotated separately and marked unvalidated.

## V2-012 — Wire native prototype journeys

Dependencies: V2-007, V2-008, V2-009, V2-010, V2-011. Owner: TERM.

Wire the journeys in `specs/flows-and-copy.md`: first use, galaxy selection/narration, nested nebula travel/back, solar view/size choice, choose comparison/Done, Guide phases/failures, Settings/Help/About/Quit/restore and Room on/off. Use contextual action labels. Annotate runtime-only hand interactions rather than claiming pointer simulation validates them.

Acceptance: prototype URLs and traced start-to-end routes with no dead ends. A static collection of frames is not a linked prototype.

## V2-013 — Visual fidelity and editability acceptance gate

Dependencies: V2-012. Owner: TERM.

Export representative reference-backed frames in their actual original dimensions. Inspect source and export side by side, then create overlay/diff evidence using a standard image comparison tool. Compare approved foreground UI and unchanged background separately when raster scene artwork masks UI errors. Measure card/rail/dock bounds, padding, image aspect ratio, line breaks, selection state and controls. Do not declare a universal pixel-diff threshold as proof of fidelity; anti-aliasing can differ.

Check native editable text/variables/component instances/auto-layout and all 39 frame/state mappings. Review close/back recovery, large text, focus/disabled/loading variants, transient hand hint, facts/credits and exact three-item dock. Fix discrepancies, export again and save final evidence. A screenshot pasted as a background can be a reference but fails editability.

Acceptance: no unexplained visual mismatch; source/export/overlay evidence for all eight curated references, plus review evidence for derived states; documented remaining limits for device testing. If component specimen differs from approved screen, resolve it rather than enforcing specimen illustration over final approval.

## V2-014 — Final handoff and close tickets

Dependencies: V2-013. Owner: TERM.

Update `FIGMA_PROGRESS.md` with actual Figma file/page/component/frame IDs, screenshot exports, prototype URLs, comparison reports and ticket status. Attach/embed exact images with implementation tickets for later Unity work. Do not start Unity implementation as part of this transfer. Update local design-package metadata only after observed Figma completion.

Acceptance: owner can open the actual Figma file and see all pages, components and screens; a documented mapping covers S01–S39; reference assets remain attached and hashed; every closed ticket has evidence. Any unfinished work is explicitly blocked/todo, not called complete.
