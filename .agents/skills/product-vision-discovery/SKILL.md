---
name: product-vision-discovery
description: Discover and refine a product and its UI/UX through evidence-grounded visual brainstorming, user feedback, and progressive narrowing. Use for product vision, redesign exploration, design directions, screenshot concepts, or iterative UI discovery before implementation. Preserve approved elements, vary unresolved decisions, and produce a reference-backed design system and actionable handoff when requested.
---

# Product vision discovery

Use this skill to turn an existing product or early idea into an agreed visual and interaction direction. The workflow is shared by Claude Code and Codex. Tools vary by session: inspect actual capabilities, use their supported image/browser/Figma interfaces, and never invent tool calls or claim access you do not have.

**Core method: explore → hear feedback → keep/eliminate → combine → refine → approve → extend → verify.** The user described this as a bubble-sort-like narrowing process. Treat that as an analogy for comparing ideas and reducing uncertainty, not a literal sorting algorithm or score that overrides user judgment.

## 1. Establish the product and current evidence

- Read applicable repository instructions first. Use the existing isolated checkout; do not create a worktree unless requested. Preserve local changes. Keep brainstorming separate from application edits.
- Identify the audience, primary surface, core task, current flows, platform constraints, and scope. Infer reasonable defaults from the repository; ask only about missing information that materially changes the design.
- Inspect current screenshots, supplied Figma frames, or the running product. Read current implementation and recent decisions to distinguish shipping behavior from parked experiments and stale docs.
- If the user says a specific page matches the app one-to-one, inspect that page; do not substitute a different file or infer its appearance from code.
- Check actual source access. If a named source is unavailable, state the gap once and ask for the source or authorization to proceed without it. Continue independent inventory work. Never silently ignore it or claim a visual audit based on indirect evidence.
- Separate observations, hypotheses, proposals, user decisions, and runtime verification. A source-based inventory is useful but is not a screenshot-based UX audit.

Make a short working brief using `references/templates.md`. Record known constraints, the selected surface and intended outcome. Tell the user which assumptions will guide the first round, then proceed within their authorized scope.

## 2. Explore a small set of distinct visual directions

- Use three directions by default; follow a user-requested count such as five. Each initial direction should vary a meaningful information hierarchy, navigation model, interaction model or product framing—not merely its color.
- Choose a natural viewport/aspect ratio for the product. For XR, render a believable first-person spatial view with content, readable controls, comfortable spacing and a visible interaction state. Treat flat pixels and physical sizing separately.
- Give each direction a clear descriptive name. Build a prompt from the brief, real constraints, current design context and actual supplied images. Use available image-generation tools for requested visual concepts. Attach the references through the tool's supported input; do not say they were attached if they were not.
- Generate one independent image per direction. Keep image/result identity, local path and displayed order in the round record. Tool completion order may differ from request order; resolve numbered selections by actual displayed image order or the user's exact attached image.
- Show images directly in the conversation. Do not replace a visual request with descriptions or file paths. Keep text concise so the user can compare the pictures.
- Do not crowd each image with every product capability. Choose a representative primary state. The full screen family comes after the vision converges.
- Do not implement, scaffold an app, rebuild Unity scenes or edit Figma's production design during exploration unless that action is explicitly within the current request.

Image-generation details such as model, tool arguments, concurrency and runtime permissions must follow the current tool/skill contracts. Never assume a named provider exists in the receiving session.

## 3. Turn feedback into an explicit narrowing record

After every substantive feedback message, record:

1. **Keep:** elements the user likes and wants carried forward.
2. **Remove:** elements the user rejected.
3. **Refine:** liked ideas that need an adjustment.
4. **Open:** choices not yet decided.

Capture each preference at component/behavior level. For example: “short three-item dock” is different from approving every element in its source image. A preference for a card's layout does not automatically approve its scientific facts, sizing, interactions or surrounding screen.

- Acknowledge the concrete changes briefly. Avoid reopening answered questions.
- Keep accepted elements consistent in the next round. Vary only the open decisions. If the user asks for another five, make five controlled alternatives within the accepted direction, not five unrelated redesigns.
- Carry removed elements as explicit prompt exclusions so they do not return accidentally.
- Combine selected pieces from different images into a new consolidated image. Show it before implementation. If the user corrects “more like this one,” use their new attachment as the strongest reference for that component.
- Refine placement and behavior as well as appearance. A gesture-hint icon can be liked without being permanently visible. Distinguish normal, teaching, recovery and success states.
- Ask for targeted selection/refinement appropriate to discovery. Do not phrase every selection as permission to build when the user is still brainstorming.

Maintain a compact decision ledger using `references/templates.md`. Never replace a user-approved image with a nearby alternative merely because it is easier to implement.

## 4. Lock a canonical vision

Once the user chooses or approves a combined direction:

- Save the exact canonical image with a stable semantic filename. Preserve all original generated images and keep explorations separate from curated references.
- Record dimensions, source/result identity, approval scope and decision ledger. For file handoffs, hash the image so another agent can verify the exact asset.
- Define the common shell, navigation hierarchy, card order, button roles, local controls, dismissal, feedback and state transitions.
- Distinguish arrangement, physical size, relative proportions and orbital spacing when those concepts are relevant. Do not merge unrelated modes behind ambiguous labels.
- Keep a short list of unresolved behavior/content decisions. Do not imply a still image demonstrates gesture detection, voice access, accurate scientific data or device comfort.
- For additional information the user wants, prefer purposeful expansion/secondary states over making every default card taller. The user’s supplied correction takes priority over this default.

Approval of the design direction is not automatically approval to deploy or change unrelated application behavior. Proceed with implementation only when the user requests it or prior session authorization clearly covers it.

## 5. Extend the agreed vision into a screen family

When asked for the rest of the product, build an inventory before generating more images:

- Core exploration/task screens and nested navigation.
- Information/details, selection and comparison.
- Settings, Help, About, recovery and confirmations.
- First use, permissions, loading, empty, unavailable, failure and success.
- Input/tracking/connection states and accessible alternatives where relevant.
- Secondary platform adaptations without abandoning the agreed hierarchy.

Use the canonical shell and components throughout. Generate representative visuals for meaningfully different screens. Specify additional states explicitly and label derived designs; never call a written inventory a complete set of visual frames.

Define reusable semantic tokens, type/spacing/radius, component sets, default/hover/pressed/focus/disabled/loading, selected/on/off, content variants and auto-layout rules. Text-size changes must reflow rather than clip. Record pixel review sizes separately from physical spatial proposals.

Keep product flows free of internal implementation details. Use truthful data and existing verified imagery where necessary. Generated concepts may establish appearance, but factual content and scientific images require independent verification before production use.

## 6. Transfer to Figma or another design tool only with real capability

- Inspect current callable tools and authentication. Verify read AND write capabilities independently. A plugin mention, installed instruction skill, file link or successful read does not establish a writer.
- Inspect the live file before mutations; repository progress reports may be stale. Preserve original pages. Create a clearly named versioned area or new file within user authorization.
- Prefer pages for Read me, Design system, Components, Layouts, Screens, Flows/states, Prototype and Handoff. Adapt names/number to project size.
- Build editable native text, variables/styles, component sets, image fills, auto-layout and screen instances. Exact screenshots belong in Reference sections; a full-screen bitmap alone is not an editable UI design.
- Make creation idempotent using discovered page names and recorded node IDs. Coordinate one writer per shared area. Do not modify another agent's in-flight nodes without an explicit ownership handoff.
- Inspect exported frames against the actual source images at matching dimensions/state. Compare geometry, hierarchy, type/line breaks, crop, imagery, colors, state and actions. Keep before/after and overlay/diff evidence where available. An arbitrary pixel threshold alone does not prove fidelity.
- Report actual file/page/frame/component/prototype URLs/IDs. Saving local instructions is not a Figma edit; uploading an unused image is not visible placement; a static frame collection is not a connected prototype.
- If write access is blocked, finish independent artifacts and give an actionable handoff. Do not promise native editing capabilities an MCP does not expose or request credentials in chat.

## 7. Create reviewable tickets and carry exact images across agents

When another chat, terminal session or agent will execute the work, include the handoff template from `references/templates.md` and:

- Canonical image, curated images, stable paths, dimensions, hashes and source-access fallback.
- A ready-to-paste task, scope, non-negotiables, owner and dependency order.
- Separate tickets for capability checks, live inspection, foundations, components, canonical frame, screen families, flows, fidelity gate and final handoff.
- Acceptance evidence: editable node IDs, source/current captures, state coverage, verified flow links and known device/runtime limits.
- Explicit permission boundary for cleanup: correct/replace/remove verified obsolete nodes only in the authorized versioned area, check references and preserve recoverability.
- A progress template that starts with real statuses; do not mark planned work done.

Do not claim to have delivered instructions to another chat if you only prepared a prompt. A receiving chat may have Figma access but no repository access; provide exact image attachments or a downloadable package. Repository tickets are not GitHub Issues unless actually created through an authorized tool.

When asked to commit/push, inspect local/remote state and preserve concurrent work. Follow repository branch rules and existing session authorization. Never force-push to solve an unrelated race. Include all intended design assets and verify the remote result; do not claim “everything pushed” without checking.

## 8. Handle image/display/download failures honestly

- If the user cannot see generated images, acknowledge the display issue without blaming them or claiming intentional hiding. Reattach actual image content using a supported tool, not another identical broken link.
- Do not regenerate designs repeatedly merely to solve a download/display problem unless the user asks or approves a fresh rendering; regeneration can change the target.
- Check the real artifact exists and the ZIP integrity/contents. Use the application's supported attachment route when available. Do not equate an existing workspace file with a working client download.
- If sandbox links fail on mobile, use an authorized normal HTTPS artifact route, such as the repository's committed file, and state login requirements accurately. Do not publish private work to a new public service without authorization.
- Never claim an attachment is visible on the user's device or a link works there unless supported by evidence. Acknowledge that you cannot see their app state.

## Completion and quality checks

Finish the currently requested stage, not every possible later stage. A discovery round is complete when requested images were generated/shown and feedback can be gathered. A vision is complete when its canonical image, approval scope and decisions are recorded. A screen/design-system handoff is complete when requested visuals and state/component specifications are prepared with explicit remaining work. Figma completion requires native edits and observed verification. Implementation completion requires appropriate runtime and visual tests.

Final messages should state concrete outcomes, where artifacts are available, what was verified and what remains blocked. Do not claim runtime/device accessibility or scientific correctness from generated imagery alone. Keep limitations brief and tied to observed gaps.

Read `references/project-example.md` for the actual Cosmic Simulation XR narrowing decisions. These are an example of the method, not universal design requirements for other products.
