# Discovery artifacts and handoff templates

Keep artifacts proportional to the task. Use an existing project design folder when available; do not overwrite another workflow's instructions. A small brainstorm may need one brief/ledger; a full handoff benefits from separate files.

## Brief

```markdown
# Product vision brief
Product / audience:
Primary surface and input:
Core user outcome:
Current product and reference sources:
Observed behavior:
Constraints and user preferences:
Out of scope for this stage:
Accessible sources / named access gaps:
Reasonable assumptions:
Open question that materially changes direction:
```

## Round and decision ledger

```markdown
# Round N
Goal / open decision being tested:
Displayed image order: [result identity → actual path → name]
Actual visual references attached:
Keep:
Remove:
Refine:
Open:
User's approval scope:
Next round: preserve ...; vary ...
Canonical image if selected:
```

## Reference manifest

```json
{
  "canonical": "screens/approved-main.png",
  "approval_scope": "Navigation and photo-first card; gestures unvalidated",
  "images": [
    {
      "path": "screens/approved-main.png",
      "width": 1440,
      "height": 1024,
      "sha256": "calculate-from-actual-file",
      "role": "canonical approved screen"
    }
  ]
}
```

Populate real dimensions/hash from the file; the sample values are not proof of an image's actual size. Preserve originals. Do not invent image identity or approval.

## Screen/component inventory

```markdown
| ID | Screen/state | Source or derived | Components/actions | Next/back route | Verification |
|---|---|---|---|---|---|
| S01 | Main task | Exact image path | ... | ... | Unrun / evidence link |

| Component | Content properties | Interaction variants | Layout/reflow rules |
|---|---|---|---|
| Information card | Image/title/prose/facts | ... | ... |
```

Use screen IDs and real coverage. A component variant can satisfy an intentionally equivalent state only when documented; do not count non-existent frames.

## Execution ticket

```markdown
## ID — Concrete outcome
Owner:
Dependencies:
Exact image / component source:
Current observed state and capture:
Work:
Protected work / permitted cleanup:
Acceptance checks:
Evidence required:
Status: todo / in progress / verified / blocked
Actual result and node/artifact links:
```

Tickets should be executable by the receiving agent and include source access. Explicitly distinguish planned, performed and verified actions.

## Ready-to-paste receiving-agent prompt

```text
Inspect the current product/design and execute the attached tickets in dependency order.
Open the exact approved reference images before making changes. Preserve accepted choices;
do not choose a new visual direction. Verify actual tool read/write capabilities and inspect
the live state; do not assume repository status describes it. Coordinate write ownership with
any active agent. Reuse correct work, repair observed discrepancies and add missing screens,
components and states within the authorized scope. Do not alter unrelated/original pages.
Export matching frames, compare with exact sources and fix discrepancies. Record actual
node IDs, before/after evidence, completed tests and remaining blockers. Continue independent
work when a specific prerequisite is blocked. Do not claim images pasted into frames are
editable components, or instructions written on disk are live tool changes.
```

## Progress / completion report

```markdown
Observed live state / time:
Actual tools and write ownership:
Canonical reference and comparison dimensions:
Ticket status and evidence:
Changes made / verified cleanup:
Native file/page/component/frame/prototype links:
Source vs export evidence:
Derived-state coverage:
Source/spec conflicts and actual resolutions:
Unrun device/runtime checks:
Concrete remaining blockers:
```

## Suggested directory

```text
design/<version>/
  brief.md
  decisions.md
  references.json
  screens/                 curated exact source images
  explorations/            earlier directions and revisions
  specs/                   tokens, components, states, copy and flows
  tickets.md
  progress.md
  evidence/                actual exports and comparisons
```
