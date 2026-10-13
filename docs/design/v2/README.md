# Cosmic Simulation XR — UI Version 2.0

Design package prepared from the user's selected spatial navigation and photo-first dark card. This is a new design version, not the application release version. No Unity source was changed.

**Figma transfer status: blocked.** No Figma connector or authenticated editing tool is available in this session. No Figma page, file, project, component, variable or prototype has been created. These files are a local handoff package; placing screenshots in Figma does not create editable components.

## Proposed Figma file

Name: **Cosmic Simulation XR — UI 2.0**. Prefer a separate file so the current app reference stays intact. If using the existing file, create the same pages with a `V2 /` prefix and leave existing pages untouched.

| Page | Contents |
|---|---|
| 00 — Read me | Scope, approved reference, decisions, status and open questions |
| 01 — Design system | Semantic colors, typography, spacing, radius, imagery, motion and spatial guidance |
| 02 — Components | Main components, properties, interaction states and content variants |
| 03 — Layouts | Quest world-space shell, parked panels, focused utility layout, desktop adaptation |
| 04 — Explore | Galaxy browser, Milky Way, nebula preview/interior, Cosmic Web, Galactic Center, solar-system views, planets/moons, exoplanets |
| 05 — Guide | Idle, connecting, listening, thinking, speaking, denied mic and unavailable service |
| 06 — Settings & help | Settings, microphone, gesture lessons, About, Quit, restore confirmation |
| 07 — Flows & states | Placement, first use, comparison, loading, failure, tracking loss and room toggle |
| 08 — Prototype | Linked representative journeys, annotated proposed behavior |
| 09 — Handoff | Physical sizing, Unity mapping, facts/imagery sources and QA checklist |

## Package

- `specs/design-system.md`: foundations and spatial rules.
- `specs/components.md`: button/component behavior and variant definitions.
- `specs/screens.md`: complete screen/state inventory and screen content.
- `specs/flows-and-copy.md`: flow links, exact copy and review boundaries.
- `tokens.json`: proposed reusable semantic tokens.
- `screens/`: visual concepts, not native editable Figma designs.
- `index.html`: local static review gallery; no application behavior implied.

## Transfer steps when Figma access is available

1. Create the named file/pages. Add the approved reference to Read me.
2. Create variables and styles from tokens. Read text size scales as accessibility preferences, not separate arbitrary font sets.
3. Build editable auto-layout components with the properties/states in the component specification. Use real images as image fills; never use generated screenshots as the actual UI components.
4. Construct editable screen frames from these components, using generated images as visual references alongside them. Each frame carries screen ID and status.
5. Wire the flow connections and annotate hand gesture behavior; normal Figma pointer prototypes cannot validate Quest tracking, reach, stereo or passthrough.
6. Verify every screen, component and flow is visibly placed and reviewable before reporting transfer complete.

## Review boundaries

Image-generated text, object scale and sky-map placement are illustrative. The solar-system concept may show orbit lines while Compare is selected; use the screen specification to separate these modes. Guide prompt buttons are proposed shortcuts, not verified runtime behavior. Existing service credentials are not represented or requested in this package. Scientific imagery, copy and facts must be reconciled with repository data before implementation.

## Earlier exploration

The `explorations/` directory preserves all generated concepts and refinements from this session. The three `galaxy-ui-*.md` files preserve discovery and decision history. `screens/` contains the curated Version 2.0 review set. The ZIP is omitted because it duplicates this directory.

## Cloud Terminal tickets

Start with [TERMINAL_HANDOFF.md](TERMINAL_HANDOFF.md), then execute [FIGMA_TICKETS.md](FIGMA_TICKETS.md). Exact images and hashes are in [references.json](references.json). Record real Figma evidence in [FIGMA_PROGRESS.md](FIGMA_PROGRESS.md). These tickets are repository documents, not automatically created GitHub Issues.
