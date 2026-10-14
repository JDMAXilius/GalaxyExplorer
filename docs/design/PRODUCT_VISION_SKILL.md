# Product vision discovery — Claude and Codex

This repository now contains the reusable workflow developed during our UI 2.0 discovery: ground in the product, generate visual alternatives, capture feedback, preserve accepted choices, narrow open decisions, combine/refine the image, then extend and verify the design.

## Invoke in this repository

**Codex:** `$product-vision-discovery Help us discover and refine the product UI. Start with the current references and keep us in brainstorming until we approve a canonical direction.`

**Claude Code:** `/product-vision-discovery Help us discover and refine the product UI. Start with the current references and keep us in brainstorming until we approve a canonical direction.`

If the active session does not auto-discover new skills, start a fresh session in this repository or explicitly ask it to read the appropriate `SKILL.md`. A discovery skill provides instructions, not an image generator or authenticated Figma connection. The agent must use tools actually available in its session and report any missing capability.

## Install for other projects

Copy the entire `product-vision-discovery` directory, including `references/`:

- For Codex, put it under the other repository's `.agents/skills/`.
- For Claude Code, put it under the other repository's `.claude/skills/`.

Project-local installation is portable and requires no global setup. Follow the receiving client's current documentation if a personal/global skill is preferred; global paths and policies can differ. Do not copy project secrets or unrelated agent settings.

## Files and maintenance

- Codex canonical source: `.agents/skills/product-vision-discovery/`.
- Claude mirror: `.claude/skills/product-vision-discovery/`.
- `SKILL.md`: complete workflow and evidence/authorization boundaries.
- `references/templates.md`: brief, decision ledger, source manifest, screen/component inventory, tickets and handoff.
- `references/project-example.md`: how the Cosmic Simulation XR process actually narrowed.

Both packages are byte-identical copies, avoiding symlink problems on Windows. When editing the skill, update the Codex source and synchronize the entire Claude mirror in the same commit; verify every file matches. Do not maintain divergent provider-specific processes accidentally.

## The behavior to expect

The agent should show real images, keep approved elements fixed, record Keep/Remove/Refine/Open after substantive feedback, and stop treating every refinement as a request to build. It should preserve the exact approved image through native design and implementation handoffs. When another agent performs the work, it should receive exact assets, scoped tickets and acceptance evidence—not vague design prose.

The skill does not authorize deleting original pages, overwriting another agent's work, publishing private designs, pushing without authorization or claiming Figma/device verification from screenshots alone.
