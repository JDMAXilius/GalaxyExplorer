# Cloud Terminal handoff — Figma UI 2.0

## Paste this task into Cloud Terminal / Cloud Code

> Work through `docs/design/v2/FIGMA_TICKETS.md` in dependency order. Recreate Cosmic Simulation XR UI Version 2.0 as editable Figma pages, components, styles, variables and linked screen flows. The owner approved the visual direction and requires one-to-one fidelity. Start by opening the actual reference PNGs listed in `references.json`; do not work from prose alone. The canonical approved exploration image is `screens/01-galaxies.png`. All eight curated images are included in this repository. Preserve the current Figma pages and the Unity app. Prefer new `V2 / ...` pages in https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/; a separate file is allowed if access or organization requires it. First verify authenticated read AND write capability, then create editable native nodes using a supported Figma editing tool or plugin bridge. A read-only MCP or exported image is not completion. Continue all unblocked tickets without repeatedly asking for permission. If an actual credential, tool capability, source conflict or missing approval blocks a ticket, report the precise blocker and continue independent tickets. Export resulting frames, compare them with the exact supplied PNGs, fix discrepancies, and record Figma URLs/node IDs and screenshot evidence in `FIGMA_PROGRESS.md`. Do not mark a ticket done without its acceptance evidence. Do not invent scientific assets, change the design, regenerate alternatives or implement Unity code. Complete the prepared V2 screen/state inventory, component library and flow links. Distinguish exact-reference screens from additional derived states. Return the actual Figma file link, page/frame IDs, completed ticket list and any remaining blockers.

## Source and scope

This folder is the complete handoff. Paths are repository-relative unless stated otherwise. Pull current `main` before starting. Work in the existing checkout and preserve local changes; do not create a worktree unless requested. Do not reset an unrelated checkout to obtain these assets.

The actual PNGs travel with the tickets. `references.json` gives SHA-256 hashes, dimensions and roles. `explorations/` is decision history, not a pool of interchangeable alternatives. Never substitute an earlier image because it is easier to reproduce.

Figma file key: `dx6MxAxPqZ9QBSRJqCCVev`. User-provided `node-id=0-1` is an entry point, not the assumed destination for new frames. Discover existing pages before creating V2 pages. Preserve the original/current UI pages. Only operate in the designated V2 area.

## Fidelity contract

One-to-one means faithfully reproducing approved geometry, hierarchy, imagery, type, colors, spacing and visible states in the matching reference viewport. Preserve the narrow side rail, contextual top picker, short Explore / Guide / Room dock, separate settings corner and photo-first dark card. No permanent hand hint and no Resize/Return buttons. Do not replace the companion with a generic avatar or change labels for taste.

Generated screenshots include perspective and scenery. Build native UI with editable text, controls and image fills; the room and celestial render may remain raster scene artwork. Do not claim exact component measurements from guessed perspective. Measure native review frames in pixels and keep world-space millimetres as a separate annotated layout. Reproducing the entire screenshot as one bitmap is useful as a reference frame but does not satisfy editable-design tickets.

For flat screenshot comparison, use the same room/background and crop. Reuse supplied imagery for visual fidelity; use the native Figma editor's crop/mask/asset extraction workflow or supported local extraction, retaining originals. Do not regenerate the approved planets/galaxies or substitute unrelated stock photos. Mark generated scientific imagery as concept artwork and keep production-source replacement outside this transfer task.

Written specifications extend behavior and states beyond what a still image can establish. When they contradict approved visible design, do not silently redesign: record the conflict and obtain a resolution for that affected part. Continue unaffected work. Typical conflicts to inspect: orbit lines in the Compare concept, differing model sizes in Equal Size, and illustrative type-size labels in the design-system image. The specification sheet's text labels are not automatically authoritative measurements.

## Execution and reporting

Use one Figma writer at a time. Query/inspect current state before each mutation; make creation idempotent using page names and saved node IDs. Never duplicate pages on retry or overwrite user modifications. Save progress after each verified ticket. Never print tokens, copy credentials into the repo or request secret values in chat; the owner signs in through the supported authorization flow.

If the Figma MCP cannot create nodes, use an available supported editing/plugin workflow. If none exists, mark write access blocked; do not pretend a successful read establishes write access. A local plugin/bootstrap may be prepared in an isolated helper directory, but the owner must run/install it if the agent cannot reach the Figma editor. No unsupported private APIs or invented tool names.

See `FIGMA_TICKETS.md`, `references.json`, `specs/`, `tokens.json`, and `README.md`. Commit handoff/progress artifacts when appropriate; publishing or claiming deployment is outside this task. Existing authorization covers creating the requested V2 design, not deleting original pages.
