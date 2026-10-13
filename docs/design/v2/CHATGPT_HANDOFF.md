# ChatGPT chat — inspect, repair and complete Figma UI 2.0

## Paste into the ChatGPT conversation with Figma connected

> Use the connected Figma tools to inspect the LIVE Cosmos Explorer file https://www.figma.com/design/dx6MxAxPqZ9QBSRJqCCVev/. Cloud Terminal is building the V2 design there. Your task is to inspect its actual work, fix verified discrepancies and complete missing V2 design work using `CHATGPT_REVIEW_TICKETS.md`. First identify the V2 pages, frames and component library; do not assume the repository's pending status describes the live file. Verify whether your tools can WRITE native pages/components/variables, not just read them. Coordinate a write handoff with Terminal before changing nodes it is currently editing. Use the exact eight PNGs and the canonical approved `screens/01-galaxies.png` supplied in this package. Open the images visually; do not rely on descriptions. Preserve one-to-one appearance, editability and the approved navigation/card hierarchy. Inspect source and current exported Figma frame together, list evidence-backed mismatches, fix them, export again and recheck. Reuse correct existing work; do not rebuild the entire file by default. Create missing V2 pages, components, states and prototype links; remove only verified obsolete/duplicate V2 nodes after protecting recoverability and checking references. Never delete original/current-app pages or unrelated user work. Continue all authorized independent fixes without asking for approval of routine edits. Ask only when a real design conflict, ownership collision, authentication requirement or missing write capability blocks a specific action. Record actual file/page/node IDs, before/after captures, ticket outcomes and unresolved limits in `CHATGPT_REVIEW_PROGRESS.md` or an equivalent report attached to this chat. Do not claim completion without visible results and verification. This is Figma correction and completion, not Unity implementation or a new visual direction.

## Give ChatGPT the source files

Repository: https://github.com/JDMAXilius/GalaxyExplorer/tree/main/docs/design/v2

Read this handoff, `CHATGPT_REVIEW_TICKETS.md`, `references.json`, `specs/screens.md`, `specs/components.md`, `specs/design-system.md`, `specs/flows-and-copy.md` and `tokens.json`. Upload the handoff ZIP and source PNGs if ChatGPT cannot access the repository. A visible Figma connection does not guarantee GitHub or local-filesystem access. Download/open each actual image, not a GitHub HTML preview treated as image data. Do not claim an attachment is visible unless it is actually accessible in the conversation.

Exact image links (downloadable originals; image access must be checked):

- Approved main screen: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/01-galaxies.png
- Solar System: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/02-solar-system.png
- Destination preview: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/03-destination-preview.png
- Guide: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/04-guide.png
- Settings: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/05-settings.png
- Comparison: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/06-comparison.png
- Help: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/07-help.png
- Design system: https://raw.githubusercontent.com/JDMAXilius/GalaxyExplorer/main/docs/design/v2/screens/08-design-system.png

Use the hashes/dimensions in `references.json`. Verify the inventory still matches current `main` before work. Old images in `explorations/` are history, not an alternative design selection.

## Non-negotiable visual rules

Narrow side rail; contextual top destination picker; bottom dock exactly Explore / Guide / Room; separate corner Settings. Dark card order is photo → title → description → two fact columns → Listen / Compare. No permanent hand icon; the hand hint exists only during teaching/recovery. No Resize/Return manipulation buttons. Keep clean spacing, type, palette, selected state and imagery from the approved screen. Do not replace the luminous guide with a stock avatar.

The owner has authorized necessary V2 corrections, additions and cleanup. That does not authorize removal of unrelated pages, undiscovered local work, original app references or Terminal's in-flight edits. Check ownership first and inspect references before deleting anything. Prefer a recoverable checkpoint; hidden duplicates are not a substitute for fixing the actual component references.

## Live status and limitations

At preparation, remote `main` remained at `ab8fb127`; no Terminal implementation commits or new Figma node IDs were visible to this preparing session. This is not evidence that Terminal did no live Figma work. The receiving ChatGPT session must discover the current file and record its observation time. Do not invent a live audit from this handoff.

Some generated reference details conflict with written behavior, especially orbit lines while Compare is selected and equal-size depictions. Preserve the canonical visual target and record conflicts before changing affected behavior. Device comfort and actual tracking cannot be validated from Figma screenshots. The native Figma prototype should annotate runtime-only behavior without pretending it is tested.

If the connected Figma tool is read-only, complete inspection and an exact repair report, then identify the missing write capability. Do not call tickets done or offer a regenerated image as if it repaired the Figma file. Use an available supported editor/plugin workflow only after checking its actual capabilities.
