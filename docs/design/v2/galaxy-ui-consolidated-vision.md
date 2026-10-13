# Consolidated discovery direction

Proposed from the user's two selected images. This is a discovery specification, not approval to modify the Unity app.

## Common shell

Keep the first reference's narrow left navigation rail, contextual top galaxy picker and short Explore / Guide / Room dock. Keep Settings separately in the corner. Use the second reference's dark illustrated information card with concise prose and a richer 2×2 facts grid.

The left rail contains Cosmic Web, Galaxies, Milky Way and Solar System. Treat these as exploration contexts rather than four equal scientific scales. Galactic Center remains reachable from Milky Way destinations; verify access to exoplanet systems before finalizing the complete taxonomy.

The dock is the global entry point. Explore opens or dismisses the explorer panels; closing the explorer does not remove the current object. Guide opens the Cosmic Being controls. Room toggles the room background, with visible on/off state. Settings opens a separate settings surface. Only one secondary utility panel should be open at a time.

Palm gesture summons the same explorer shell and parks it in the room; it should not continuously follow the wrist. Keep the dock as an alternative entry. A hand hint appears during first-use teaching, on Help request or after a failed attempt; it clears after successful use. Exact gesture, dwell and timeout remain to be tested. No permanent hand icon.

## Contextual navigation

| Context | Top/local controls | Content |
|---|---|---|
| Cosmic Web | No destination strip unless there are meaningful destinations | Web and concise scene information |
| Galaxies | Portrait strip of Andromeda, Whirlpool, Pinwheel and Triangulum; selection highlighted | Selected galaxy, photo and facts |
| Milky Way | Local map destinations; selected marker opens preview | Nebulae, Solar System, Galactic Center |
| Solar System | Separate View and Size decisions described below | Planet model or comparison arrangement |

For a larger galaxy catalogue, scroll or page the same strip with large accessible controls; do not shrink every thumbnail until it fits. Preserve current selection and provide equivalent non-voice access to all supported destinations.

## Solar-system views

Separate arrangement from scale, rather than mixing Row and True Size as if they mean the same thing.

- View: Orbits or Compare.
- Compare arrangement: Row is the initial proposal; investigate whether a side-by-side or shallow arc arrangement fits seated reach better.
- Compare size: Equal Size or Relative Size. Relative Size means correct proportions within a chosen model scale, not literal full-sized planets.
- Orbits: independently distinguish schematic spacing from accurate orbital distances if supported. Do not imply that accurate distance means accurate planet size.

Show only the controls appropriate to the active view, below its top object picker. Keep hand gestures responsible for moving, rotating and resizing held objects. Do not put Resize or Return buttons in the main scene.

## Information cards

Updated by the user's Earth-card reference: photograph FIRST, then title, one short paragraph, a thin divider, two aligned fact columns and Listen / Compare. Keep the default card compact and airy; do not add a four-fact grid or a More details row to this visual. Expanded information and dismissal remain separate design decisions. Text size settings must be respected.

Planet facts can include diameter, orbital period, mass and parent system. Galaxy facts can include distance, classification, catalogue designation and region. Nebula previews include photograph, type, distance and explicit Enter; the travel action belongs to the preview, whereas object cards emphasize learning. Moon cards retain the parent relationship. Values and images must come from verified repository data before implementation.

Listen becomes Pause while narrating and returns to Listen when finished. Compare opens a contextual selector for a second suitable object; avoid introducing a persistent comparison dashboard. Keep object comparison and galaxy comparison behavior explicitly defined before coding.

## Remaining surfaces

| Surface | Proposed contents and behavior |
|---|---|
| Guide | Summon/dismiss Being, clear listening/thinking/speaking state, microphone state, suggested questions, stop speaking; no mandatory chat transcript |
| Room | One-action room visibility toggle; clear state and gentle transition; expose further appearance choices in settings only if supported |
| Settings | Sound, narration, text size, microphone selection, Help, About, Quit; reuse dark card styling and consistent close placement |
| Recovery | Put menu back in reach and Restore scene as separately named actions in settings/help; restoring content must not happen merely from summoning the menu |
| Help | Short contextual gesture teaching; hand hint temporary; demonstrate pinch/move and two-hand scale with a way to replay |
| Compare | Choose second body, inspect alongside first, change equal/relative scale if appropriate, leave comparison; facts presented only when requested |
| Destination preview | Integrated photo, short description, relevant facts, Enter, Listen and clear back navigation |
| About | Product version, lineage, imagery/data attribution, privacy and source links |
| Quit | Explicit confirmation with cancel; do not put a large Quit action in exploration |
| Intro | Anchor content, brief gesture teaching, understandable skip; avoid presenting the complete interface before placement is usable |
| Feedback | Loading, unavailable destination, load failure, voice unavailable and tracking loss; distinguish each and provide a relevant recovery action |

## Open design decisions

The consolidated image shows normal exploration with no hand hint. Next visual studies should cover solar-system controls, a destination preview and expanded information, followed by Guide and Settings. Validate panel distances, target sizes, occlusion, seated operation and text over bright passthrough on Quest. Existing runtime, original Figma page and exact gesture behavior have not been validated in this session.
