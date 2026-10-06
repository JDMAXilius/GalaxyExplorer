# Roadmap — Hand tracking

*Written 6 Oct 2026 from the repository (`quest3-port` at `fd566a51`, plus the uncommitted working tree), the
first standalone run on the owner's Quest 3 the same day, a file-level audit of the rig and every interactable,
and current Unity and Meta documentation (Sources at the end). It covers everything a player does with bare
hands. Device testing in general is `release/ROADMAP_QUEST_TESTING.md`; publishing is
`release/ROADMAP_META_HORIZON_STORE.md`.*

Track tags are the backlog's: **[TERM]** editor, headset or build (terminal session); **[CC]** code in the repo
(cloud session); **[OWNER]** a decision. Tickets **CS-270 to CS-299** are reserved here and are proposals until
they are rows in `docs/BACKLOG.md`.

How to read a ticket: **Goal** is the one sentence it exists for. **Objectives** are the steps, in order.
**Needs** is what must be true first. **Evidence** is what gets pasted into the backlog row to call it done.
**If it fails** is the named fallback.

## 0. Where hands stand today

### What the owner saw on the headset (6 Oct, build of 16:38, debug-signed, sideloaded)

- The intro ran. The orb could be moved "but sometimes it did not take"; Start was hard to press.
- No dock anywhere. Two small palm tiles ("About", "Room") were the only UI in view.
- No drawn hands.
- Black surroundings instead of the room.
- The test started with controllers in hand. The headset log shows the app switching to hands as soon as both
  were put down (`ModalityTransitionsPolicyManager: Switching to Hands only`, interaction profile
  `ext/hand_interaction_ext`), so hand tracking itself is on and working.
- Frame rate was 72–73 fps throughout (`VrApi` lines), so none of this is lag.

### What the files say (verified by reading; nothing below was observed on device unless stated)

- **Input path.** Each hand has an `XRPokeInteractor` and a `NearFarInteractor` (`Assets/prefabs/xr/ge_xr_rig.prefab`);
  `GEInteractable` turns XRI hover/select into app events. Controllers use the same path; there is no
  controller-only feature.
- **Select is strict.** Pinch presses at value 1.0 and releases at 0.9. A closed fist also selects (`graspFirm`
  is bound to Select).
- **Poke works on almost nothing.** Both hand poke interactors require an `XRPokeFilter`. Only the legacy hand
  menu's four buttons, the intro Start button and three buttons in the old POI prefab have one. The dock, its
  pop-up, the settings window, hint cards, galaxy pins and the being do not. GDD §5.1 and §8.1 say they do.
- **Pointing turns the far ray off.** `PokeGestureDetector` sets `enableFarCasting = false` when the index is
  extended and the other fingers are curled — the pose a player adopts to press a tile. With no poke filter on
  the tile, that hand can then neither poke nor ray it.
- **Some content is outside the hand masks.** The far caster queries layers 0, 5 and 31; the near caster layer 0
  only. Orbit-model planet colliders are on layer 12 and the Milky Way label collider on layer 11. The mouse
  queries everything but layer 2, so desktop testing never showed this.
- **The dock hides on a palm gesture whose axis was never confirmed.** `DockController` toggles when the left
  palm "faces up" for 0.5 s, with `palmAxis = Up` and a tooltip saying *confirm on device (CS-034)*. In the
  OpenXR joint convention the palm joint's +Y is the back of the hand, so this most likely fires on a resting
  palm-down hand. Inferred, not yet confirmed on device.
- **The dock parks low and stays there:** 0.75 m ahead, at max(0.55 × eye height, 0.7 m), tilted 25°, placed once.
- **Targets are small.** Recenter, Help, Settings and Being are 9 × 9 mm; the drag bar is 60 × 8 mm; pop-up and
  window close buttons are 9 × 9 mm. Meta's guidance is at least 22 × 22 mm with 12 mm between targets.
- **Hands are wired to draw, faintly.** The XR Hands `HandVisualizer` drives the sample hand mesh with
  `xr_hand_material` (built-in Standard, Fade, alpha 0.35) and a depth-only pass queued *after* it. No scene
  has a light. Over passthrough that is likely near-invisible.
- **Two owners of passthrough.** The dock's passthrough button goes through `EnvironmentController`. The legacy
  hand menu's Mode button ("VR"/"Room") calls `ExperienceModeManager.Toggle()` directly, so the next
  environment call undoes it, opaque sky shells never hear about it, and VR mode is saved to PlayerPrefs and
  re-applied to the next launch's whole intro. The Mode button's label names the mode it switches *to*: seeing
  "Room" means the app was in VR mode at that moment. Why it was in VR on a first install is not established.
- **Two menus.** The legacy palm-toward-face hand menu and the dock's left-palm toggle both exist.
- **Authored off:** all seven nebula prefabs ship with grab collider and `ManipulationHandler` disabled.
- **Not implemented for any input:** pinch the logo to skip (GDD §2.1); move the whole orbit model or Milky Way
  map (CS-045); push a galaxy sprite (CS-064); two-hand scale of Sagittarius A*.
- **Found in the same run, not a hand issue:** `CosmicSimulation/NebulaStar` and `CosmicSimulation/NebulaField`
  are missing from the build (reached only through `Shader.Find`), and the failure is logged about 576 times a
  second because `OnEnable` re-subscribes `Camera.onPreRender` after `EnsureRenderer` has disabled the component.

### What already works with hands, per the code

Ray + pinch pull of row bodies and moons; dwell pull (2 s, 1 s on the intro orb, gated until the first narration
ends); near grab within 12 cm; one-hand move; two-hand move, rotate and scale; tap-to-send-home (≤ 0.4 s,
≤ 3 cm); brush; palm inside the Sun; dock tiles, pop-up, settings and the scale slider by pinch; dock drag bar;
galaxy pins; Andromeda and the other galaxies by one or two hands; the being (summon, tap, carry); pinch at
nothing to close cards; system-gesture guard (`MetaSystemGestureDetector`) and joint smoothing are in the rig.

## 1. What "done" means (exit criteria)

| # | Criterion | Measured with |
|---|---|---|
| H1 | With both controllers down, a first-time player reaches a planet's card without help | Owner run, timed |
| H2 | The room is visible in every place whenever passthrough is the chosen mode, from the first frame of the intro | Device walk of all seven tiles |
| H3 | Both hands are drawn as a translucent outlined hand, identical in both eyes, hidden on tracking loss | Per-eye check, CS-219 |
| H4 | The dock is in view after the intro and can be called back from anywhere with one gesture; it never hides by accident in a 10-minute session | Device run, log count of toggles |
| H5 | Every button can be pressed by fingertip poke *and* by ray + pinch; no target under 22 × 22 mm | Run sheet + prefab size dump |
| H6 | Everything the mouse can select, a hand can select | Layer/mask dump, device walk |
| H7 | Orb grab and Start succeed 10 times out of 10 | Device run |
| H8 | VRC Input.4, Input.7 and Input.8 pass | CS-219 |
| H9 | Desktop mouse mode still passes App Check | `Logs/app_check.log` |

## 2. Order and dependencies

```
Phase 1  See         CS-270 → CS-271, CS-272 → CS-273        (one build)
Phase 2  Menu        CS-274 → CS-275 → CS-276, CS-277, CS-278, CS-279
Phase 3  Grab        CS-280, CS-281, CS-282, CS-283
Phase 4  Verify      CS-284, CS-285                           (gates everything after)
Phase 5  New         CS-286 … CS-291
Phase 6  Optional    CS-292, CS-293
```

Phases 1–3 fix what is already designed and documented. Nothing in Phase 5 starts until Phase 4 passes: adding
gestures on top of unreliable basics makes both harder to debug. Every [CC] ticket that lands produces a [TERM]
row to verify on device, per `CLAUDE.md`.

## 3. Owner decisions

Taken 6 Oct 2026: the owner asked for the recommended defaults and for the work to start ("less is more").

| # | Decision | Taken |
|---|---|---|
| D-H1 | One menu or two: keep the legacy palm menu beside the dock? | One. Reset and About move onto the dock; the palm menu is retired on Quest (CS-277) |
| D-H2 | How the dock is called | Palm toward your face on either hand for 0.5 s brings the dock to that hand's side; no gesture ever *hides* it (CS-275) |
| D-H3 | Hand look | Translucent body, bright rim, cool white-blue; faint in passthrough, stronger in full VR (CS-271) |
| D-H4 | Should a closed fist select? | No. Pinch only (CS-281) |
| D-H5 | Pose shortcuts in Phase 5 | Open palm held toward the content = pause/resume narration. Nothing else until players ask (CS-289) |
| D-H6 | Add Meta's XR SDK for microgestures and controller + hand together | Not for 1.0: nothing in Phases 1–5 needs it, and it is a second input stack beside XRI (CS-292). Revisit if a timeline control arrives |

Wave 1 (6 Oct) implements CS-270, CS-271, CS-272, CS-275, CS-276, CS-277, CS-278, CS-279, CS-280, CS-281 and
CS-283 in code, then CS-273, CS-274 and the [TERM] halves on the headset.

## Phase 1 — See the room and your hands

**Phase goal:** the next build shows the room and two outlined hands from the first frame, with no error spam.

### CS-270 [CC] One owner for passthrough

- **Goal:** there is exactly one place that decides whether the room is visible, and it is never VR by accident.
- **Objectives:**
  1. `ExperienceModeButton` calls `EnvironmentController.TogglePassthrough()` instead of
     `ExperienceModeManager.Toggle()`.
  2. `ExperienceModeManager` stops persisting VR to PlayerPrefs; `EnvironmentController.PassthroughForced` is
     the saved preference, default on.
  3. `EnvironmentController` applies its mode in `Start`, before the intro, so the logo, orb and Start run in
     passthrough.
  4. `PlaceShell` and every other listener keep hiding on effective mode Passthrough (no change expected).
  5. Update App Check: the six "room goes FullBlack/Dimmed" assertions are stale now that passthrough is on by
     default; assert both states of the toggle instead.
- **Needs:** nothing.
- **Evidence:** grep showing one caller of `ExperienceModeManager.SetMode`; App Check log.
- **If it fails:** keep the Mode button, but make it call both and delete the PlayerPrefs write.

### CS-271 [CC] Outlined hand shader and material

- **Goal:** a hand that reads clearly over a real room and over black, correct in both eyes.
- **Objectives:**
  1. New `Assets/shaders/hand_outline_shader.shader`, hand-written ShaderLab, queue Transparent, two passes:
     depth prepass (`ZWrite On`, `ColorMask 0`), then `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, colour =
     low base alpha + `rimColor * pow(1 - saturate(dot(N, V)), rimPower)`.
  2. Stereo macros in both passes (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`,
     `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`), no geometry shader, target ≤ 4.5.
  3. Properties for base alpha, rim colour, rim power; two presets (passthrough, full VR) switched on
     `EnvironmentController.ModeChanged`.
  4. Replace `xr_hand_material`'s shader; remove the sample `DepthOnly` slot (its pass is now inside the shader
     and in the right order).
  5. Reference the shader from a material on the rig prefab so it cannot be stripped. Never `Shader.Find`.
- **Needs:** D-H3.
- **Evidence:** shader compiles for Android Vulkan; entry added to `docs/SHADER_STEREO_AUDIT.md`.
- **If it fails:** draw nothing in passthrough (the real hand shows through) and the outline only in full VR.

### CS-272 [CC] Nebula shaders reach the build, and fail once

- **Goal:** nebula stars and fields draw on device, and a missing shader costs one log line.
- **Objectives:**
  1. `NebulaStar` and `NebulaField` take a serialized `Shader` reference set by their builders, with
     `Shader.Find` only as an editor fallback. Same treatment for `Bloom`.
  2. In `OnEnable`, subscribe to `Camera.onPreRender` only if `EnsureRenderer` left the component enabled.
  3. Audit the remaining runtime `Shader.Find` calls (`EnvironmentController` is already covered by Always
     Included Shaders).
- **Needs:** nothing.
- **Evidence:** `adb logcat -s Unity` over 60 s in a nebula with zero "shader … not found" lines.
- **If it fails:** add the shaders to Always Included Shaders from `Quest3ProjectSetup`, as `CosmicWebBuilder`
  already does for its own.

### CS-273 [TERM] Build, install, look

- **Goal:** prove Phase 1 on the headset.
- **Objectives:**
  1. Rebuild prefabs touched by CS-271; build APK; `adb install -r`.
  2. Controllers down. Walk intro and all seven tiles with passthrough on; toggle it off and on in each.
  3. Close each eye in turn on the hands and on every place.
  4. Cover the cameras with a hand: hands must disappear, not freeze.
  5. Capture `screencap` per place (passthrough shows black in captures; note that in the row).
- **Needs:** CS-270, CS-271, CS-272.
- **Evidence:** the checklist ticked, captures, 60 s of logcat.
- **If it fails:** file the specific failure as a [CC] row; do not start Phase 2 on a build with one eye wrong.

## Phase 2 — Find the menu and press it

**Phase goal:** the dock is where the player expects, stays there, and every control on it yields to a fingertip.

### CS-274 [TERM] Confirm the palm axis on device

- **Goal:** know which axis of the palm joint points out of the palm, instead of inferring it.
- **Objectives:**
  1. Temporary on-screen readout of `Vector3.Dot(palm.up, Vector3.up)` for both hands.
  2. Record the value palm-up, palm-down and palm-toward-face.
  3. Close CS-034's palm line with the result.
- **Needs:** CS-273.
- **Evidence:** the three numbers per hand.
- **If it fails:** compute the palm normal from wrist, index-metacarpal and little-metacarpal joints, which
  does not depend on the joint's own axes.

### CS-275 [CC] The dock comes to you and never hides by accident

- **Goal:** a player who has walked away or looked elsewhere gets the dock back with one deliberate gesture.
- **Objectives:**
  1. Replace the left-palm-up *toggle* with a *summon*: palm toward the face (either hand) held 0.5 s re-parks
     the dock beside that hand, facing the player. Use the axis from CS-274.
  2. Remove gesture-driven hiding. Add a close control to the dock; summon shows it again.
  3. Raise the default park height (start at 0.70 × eye height) and re-park automatically when the intro ends.
  4. Suppress the summon while a body is held or the drag bar is grabbed, and during the system gesture.
  5. Keep `Tab` on desktop.
- **Needs:** CS-274, D-H2.
- **Evidence:** device run: 10 minutes, zero unrequested hides in the log; summon works from behind.
- **If it fails:** keep the dock always visible and add a "bring dock here" tile to the palm menu.

### CS-276 [CC] then [TERM] Poke works on everything

- **Goal:** GDD §5.1 becomes true: any button yields to a fingertip.
- **Objectives:**
  1. `UiPrefabBuilder` adds an `XRPokeFilter` (direction −Z of the face, 45°) beside every `GEButton` it builds:
     dock tiles, passthrough, the four under-dock buttons, pop-up options and close, settings buttons, slider.
  2. Runtime-built buttons get one too: `HintCards`, `LabelButton`/`GalaxyPins`, the being's `GEInteractable`.
  3. A press travel affordance (`XRPokeFollowAffordance` or the existing 6 mm lift) so the press is visible.
  4. [TERM] Re-run *Build UI Prefabs*, rebuild, and poke every control on device.
- **Needs:** CS-273.
- **Evidence:** guid search listing every button prefab with a poke filter; device checklist.
- **If it fails:** set `m_RequirePokeFilter: 0` on the hand poke interactors and rely on `GEButton`'s own
  poke-down handling.

### CS-277 [CC] One menu

- **Goal:** retire the legacy palm menu without losing what it does.
- **Objectives:**
  1. Add Reset and About to the dock's under-row; Back is already superseded (CS-111).
  2. Remove the Mode button (the dock's passthrough button is the control after CS-270).
  3. Disable `HandMenuManager` on the Quest 3 platform branch of `GlobalMenuManager`.
- **Needs:** CS-270, D-H1.
- **Evidence:** device run showing no palm tiles; Reset and About reachable from the dock.
- **If it fails:** keep the palm menu, restyled, as the *only* place for Reset/About and remove them from scope.

### CS-278 [CC] then [TERM] Targets a finger can hit

- **Goal:** no control smaller than Meta's minimum.
- **Objectives:**
  1. Under-dock and close buttons 9 × 9 → at least 24 × 24 mm, 12 mm apart; drag bar 60 × 8 → 120 × 24 mm.
  2. Settings buttons to at least 24 mm tall; slider track 24 mm tall.
  3. Colliders may exceed the visual by up to 4 mm a side where the art cannot grow.
  4. [TERM] Rebuild prefabs; dump every `GEButton` collider's world size to the log and paste it.
- **Needs:** CS-276.
- **Evidence:** the size dump with nothing under 22 mm.
- **If it fails:** grow colliders only and leave the art.

### CS-279 [CC] The pointing hand keeps its ray

- **Goal:** extending a finger toward a far tile does not switch the ray off.
- **Objectives:**
  1. Far casting is disabled by `PokeGestureDetector` only while the fingertip is within 0.3 m of a pokeable
     collider; otherwise it stays on.
  2. Hover and press feedback on every button for both poke and ray: colour, 6 mm lift, tick, and a distinct
     press sound (hands have no haptics).
- **Needs:** CS-276.
- **Evidence:** device run: point at a tile 1 m away, ray stays; move within reach, ray yields to poke.
- **If it fails:** remove the `PokeGestureDetector` far-cast hooks entirely.

## Phase 3 — Grab reliably

**Phase goal:** if a mouse can pick it up, so can a hand, first time.

### CS-280 [TERM] Everything selectable is on a layer hands query

- **Goal:** orbit-model planets and Milky Way labels answer to a hand ray.
- **Objectives:**
  1. Add layers 11 and 12 to the far caster mask and layer 12 to the near caster mask on all four interactors,
     as rig overrides.
  2. Dump every `GEInteractable` collider's layer against the masks; list anything still outside.
  3. Pull Earth in the Solar System place by hand on device.
- **Needs:** CS-273.
- **Evidence:** the dump with an empty "unreachable" list; device confirmation.
- **If it fails:** move the colliders to layer 0 instead and check nothing else depended on layers 11/12.

### CS-281 [CC] Pinch that takes the first time

- **Goal:** the orb and bodies grab on the first pinch.
- **Objectives:**
  1. Press threshold 1.0 → 0.8, release 0.9 → 0.6, on both hands' Select and UI Press readers.
  2. Unbind `graspFirm`/`graspValue` from Select (D-H4).
  3. Intro orb: lift the dwell gate so the ray can pull before the narration ends; review the ~0.54 m collider
     against the ring and Start button so the orb does not swallow rays meant for Start.
  4. Start button: appears as soon as the orb is placed, 0.5 m in front of the player at chest height, not at
     the ring edge.
- **Needs:** CS-273.
- **Evidence:** device run: 10 grabs of 10; 10 Start presses of 10, by poke and by pinch.
- **If it fails:** log `pinchStrengthIndex` at each failed grab to see whether the threshold or the hit is at fault.

### CS-282 [TERM] Nebulae and the Cosmic Web can be held

- **Goal:** the guide's "resize the nebula with two hands" is true.
- **Objectives:**
  1. Enable the `SphereCollider` and `ManipulationHandler` on the seven nebula prefabs; add
     `ManipulationPointerRouter` where missing.
  2. Cosmic Web: the player stands inside its collider, so a ray from inside never hits it. Give it a grab
     handle outside the volume, or accept two-hand pinch-in-air while it is the open place.
- **Needs:** CS-273.
- **Evidence:** device run per nebula.
- **If it fails:** [CC] row for an "open place" two-hand gesture that needs no collider hit.

### CS-283 [CC] Hands stay honest when tracking is poor

- **Goal:** VRC Input.4 and Input.5, and no stuck grabs.
- **Objectives:**
  1. On tracking lost: hide the hand, hide its ray, release anything it holds where it is.
  2. On focus lost (system menu): hide hands, ignore input, pause narration (shares the pause handler the
     store roadmap needs for Functional.2).
  3. Confirm `MetaSystemGestureDetector` disables both interactors on that hand (it is wired; verify).
- **Needs:** CS-271.
- **Evidence:** CS-219's checks pass.
- **If it fails:** none; these are required for submission.

## Phase 4 — Verify

### CS-284 [TERM] Hands run sheet on device

- **Goal:** every row of §0's "already works" list and every Phase 1–3 fix is seen working, hands only.
- **Objectives:**
  1. Write the sheet from the audit's inventory (one row per action, passthrough and full VR columns).
  2. Run it bright and dim; seated and standing; left- and right-handed.
  3. Record reach and comfort notes against Meta's ranges (touch UI 42–46 cm; ray 0.8–3 m).
- **Needs:** Phases 1–3.
- **Evidence:** the filled sheet. This is CS-218 with hands-only scope; close both together.
- **If it fails:** each failed row becomes a ticket; Phase 5 waits.

### CS-285 [CC] App Check walks the pointer layer the way a hand does

- **Goal:** a regression in masks, poke filters or thresholds fails in the editor, not on the owner's head.
- **Objectives:**
  1. Assert every `GEButton` has a poke filter and a collider ≥ 22 mm.
  2. Assert every `GEInteractable` collider's layer is in the hand far mask.
  3. Drive one grab, one poke and one two-hand scale through XRI's simulated hand interactors.
- **Needs:** CS-276, CS-278, CS-280.
- **Evidence:** App Check log with the new rows passing.
- **If it fails:** keep assertions 1 and 2, which need no simulation.

## Phase 5 — New things hands can do

**Phase goal:** interactions the reference shows and this stack supports without a new SDK. Each is independent.

### CS-286 [CC] Move and scale the whole place

- **Goal:** pinch empty space with both hands to move, turn and resize the open place.
- **Objectives:** root `GEInteractable` + `ManipulationHandler` per place (this is CS-045, widened to every
  place); scale clamped to the settings slider's 0.5×–2×; one-hand pinch on empty space still closes cards.
- **Needs:** CS-284. **Evidence:** device run in three places. **If it fails:** keep the slider as the only scale.

### CS-287 [CC] Panels you can put where you want

- **Goal:** info cards and the settings window have a grab handle and stay where they are left.
- **Objectives:** handle ≥ 120 × 24 mm on each panel; panel re-faces the player on release, no roll; position
  remembered for the session; Recenter brings them back.
- **Needs:** CS-278. **Evidence:** device run. **If it fails:** panels follow the dock as now.

### CS-288 [CC] Real hands in front of planets

- **Goal:** a hand between the eye and a planet hides the planet, as it would a real object.
- **Objectives:** the depth prepass from CS-271 rendered before opaque content (queue < 2000) in passthrough;
  measure the cost; setting to turn it off.
- **Needs:** CS-271. **Evidence:** device capture, frame time before and after. **If it fails:** leave it off.

### CS-289 [CC] One pose shortcut

- **Goal:** open palm held toward the content pauses and resumes narration.
- **Objectives:** import the XR Hands *Gestures* sample; one `StaticHandGesture` with a 0.4 s hold; ignored
  while pinching, holding, or during the dock summon pose; a visible confirmation.
- **Needs:** D-H5, CS-275. **Evidence:** device run with no false triggers in 10 minutes. **If it fails:** drop it.

### CS-290 [CC] Fling to send home

- **Goal:** throwing a body away returns it to its orbit, with the release velocity as the cue.
- **Objectives:** read release velocity in `ManipulationHandler`; above a threshold, `ResetToRoot()`; below it,
  stay where left, as now.
- **Needs:** CS-281. **Evidence:** device run. **If it fails:** keep tap-to-return only.

### CS-291 [CC] Pinch the logo to skip

- **Goal:** GDD §2.1 as written.
- **Objectives:** `GEInteractable` + handler on the logo; pinch or poke ends the 5 s timer.
- **Needs:** CS-276. **Evidence:** device run. **If it fails:** none needed; any dock tile already skips.

## Phase 6 — Optional, each needs a decision

### CS-292 [OWNER] Meta XR SDK: microgestures and controller + hand together

- **Goal:** decide whether thumb tap and thumb swipes (left, right, forward, backward) are worth a second
  input stack.
- **Notes:** needs Meta XR Core SDK v74+ beside XRI; no Unity package exposes them. Meta states passthrough,
  Multimodal and Wide Motion Mode cannot all be on together. The natural use here is stepping a timeline,
  which the app does not have yet.
- **Recommendation:** defer past 1.0.

### CS-293 [TERM] Wide Motion Mode when it leaves pre-release

- **Goal:** hands stay plausible at the edge of the cameras' view.
- **Notes:** arrives in XR Hands 1.10 with OpenXR Plugin 1.19, both pre-release on 6 Oct 2026. Fast Motion
  Mode comes with it and is a poor fit: Meta warns it adds jitter to direct touch.
- **Recommendation:** revisit when both are released; do not ship on pre-release packages.

## Risks

- **Built-in pipeline and hand shaders.** Unity's sample hand materials are Shader Graph, which does not
  support Single Pass Instanced on the Built-in pipeline. CS-271 is hand-written for that reason. Do not swap
  the sample materials back in.
- **Prefab churn.** CS-276 and CS-278 rebuild five UI prefabs; fileID churn is expected (see CS-150).
- **Desktop.** Every change keeps a mouse or key equivalent (`CLAUDE.md`). CS-275 must not touch `Tab`.
- **Unconfirmed inferences in §0:** the palm axis, how visible the current hand is, whether layer-11/12
  colliders are truly unreachable, and why the 6 Oct run was in VR mode. CS-273, CS-274 and CS-280 settle them.

## Sources

- XR Interaction Toolkit 3.6: Hands Interaction Demo, Near-Far Interactor, XR Grab Interactable, HandMenu —
  https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/samples-hands-interaction-demo.html
- XR Hands 1.9: hand data, Meta Hand Tracking Aim, static gestures —
  https://docs.unity3d.com/Packages/com.unity.xr.hands@1.9/manual/hand-data/xr-hand-access-data.html
- OpenXR Plugin 1.18: Hand Interaction Profile, Hand Mesh Data —
  https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/features/handinteractionprofile.html
- Unity Meta OpenXR 2.6: passthrough, scene setup —
  https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.6/manual/features/camera/passthrough.html
- Meta: hands UI best practices, interaction types, limitations —
  https://developers.meta.com/horizon/design/hands-ui-best-practices/
- Meta: microgestures, Multimodal, Wide Motion Mode —
  https://developers.meta.com/horizon/documentation/unity/unity-microgestures/
- Meta: Quest VRCs (Input.4, Input.7, Input.8, Functional.14) —
  https://developers.meta.com/horizon/resources/publish-quest-req/
