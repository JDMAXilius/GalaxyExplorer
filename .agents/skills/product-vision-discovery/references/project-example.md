# Example: Cosmic Simulation XR UI 2.0

This is a record of the method used in this repository, not a template prescribing this UI for every product. Read current project documents and progress before acting: later sessions can change status and details.

## Starting point

The project was a Unity mixed-reality astronomy experience, Quest 3 primary and Windows desktop secondary. Source inspection distinguished shipping `Assets/scripts` from the disabled/parked `Assets/Cosmic` rework. The supplied Figma page was inaccessible to the preparing session, so the user authorized visual ideation from repository findings. It was not presented as a verified live UI audit.

## Broad exploration

Initial concepts explored a spatial atlas, guided discovery and comparison workbench. A later round added palm compass, portal gallery, minimal object-first controls, field journal and constellation map. The user then compared specific pieces instead of choosing one complete image wholesale.

## Preferences that narrowed the next rounds

- Keep the short Explore / Guide / Room dock and separate Settings corner.
- Keep exploration choices on the side; explore the exact grouping.
- Use tracked-hand manipulation, removing Resize/Return buttons from normal UI.
- Keep narration and comparison actions, concise facts and a clean illustrated card.
- Use a contextual top thumbnail strip for galaxy selection.
- Explore hand-based menu summon; do not leave a hand-hint icon permanently visible.

Five controlled variants retained these choices while testing rail width, horizontal galaxy picker, side expansion and palm access. The user liked one image's narrow rail/selector/dock and another image's information card. Those selected pieces were combined.

## Stronger correction and final image

The user supplied an Earth-card crop to clarify the intended structure: photograph FIRST, then title, prose, two fact columns and Listen / Compare. This superseded the previous title-first/four-fact proposal. A refreshed combined image preserved the navigation and adopted that card order.

Canonical project reference: `docs/design/v2/screens/01-galaxies.png`. Exact dimensions/hashes: `docs/design/v2/references.json`. Earlier generated images remain in `docs/design/v2/explorations/`.

## Extension and transfer

The direction was extended to representative Solar System, destination preview, Guide, Settings, comparison and temporary Help screens, plus a design-system sheet. The package specified 39 screen/state entries separately from the eight curated raster references. It did not claim eight images were 39 completed native frames.

The preparing session lacked a Figma writer. It made 14 Terminal transfer tickets, then 10 ChatGPT live-review/repair tickets for another session with Figma connected. Handoffs carried exact images, hashes, dependency order, live inspection/write-capability checks, editable-component requirements, comparison evidence and scoped cleanup. Repository artifacts were committed/pushed when authorized.

Current project records may now include actual Figma results: inspect `docs/design/v2/FIGMA_PROGRESS.md`, `CHATGPT_REVIEW_PROGRESS.md` and `evidence/`. Do not reuse this historical access blocker as a claim about current capabilities.

## Lessons

Hold accepted choices fixed; vary open ones. Resolve selections using visible image identity and user attachments. A liked icon does not mean it should always be visible. A card correction can be stronger than an earlier global approval. Avoid interpreting visual direction approval as every behavior/content detail being verified. Keep screenshot concepts, native Figma edits and device implementation as separate evidence stages.

Display/download problems were distinct from design problems. Sandbox links failed in the user's mobile app; a repository HTTPS ZIP route was provided after authorization. Generating fresh copies did not prove client rendering would work, and can drift from the canonical source. Prefer reliable attachment/export routes over repeated regeneration.
