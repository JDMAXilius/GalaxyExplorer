# Flows, copy and handoff

## Prototype connections

First use: S28 placement → S29 palm → S30 pinch → S31 two hands → S02 Milky Way. Every lesson supports Skip and can be replayed from S32.

Galaxy exploration: S01 rail Galaxies → portrait selection → selected galaxy/card → Listen state or S14 details. Compare → S12 choose second → S13 pair → Done restores single-object context. Comparing arbitrary galaxies needs a metric/scope decision before implementation; do not silently apply planet-size semantics.

Nested travel: S02 marker → S03 preview → Enter → S33 loading → S04 nebula. Back returns to S02 prior selection. Failed travel → S35 safe recovery. No content → S34 unavailable.

Solar: rail Solar System → S07/S08 view choice → select body S09 → select moon S10 → Listen. Compare S12 → S13. Layout selection must make its effect on already-held bodies explicit.

Guide: dock Guide → S15 → Start listening → S16 when connection needed → S17 → S18 → S19 → S15. Stop controls end the corresponding phase. Permission S21 → OS prompt → S17 or S22. Connection failure → S20. Continue exploring never blocks on the guide.

Settings: gear → S23 → device S24 / Help S32 / About S25 / Quit S26 / Restore S27. Closing returns to the prior view. Room toggles S37/S38 independently of context. Palm summon and Bring menu here relocate the shell without restoring scene content.

## Exact proposed copy

| State/action | Copy |
|---|---|
| Dock | Explore · Guide · Room |
| Solar view | View: Orbits / Compare |
| Comparison size | Size: Equal Size / Relative Size |
| Orbital spacing | Spacing: Schematic / True Distances |
| Palm lesson | Make the menu come to you. Turn an open palm toward you to show Explore. |
| Grab lesson | Pinch to grab. Bring your finger and thumb together and pull a planet toward you. |
| Scale lesson | Two hands to change size. Pinch with both hands and move them apart or together. |
| Guide idle | Ready to help. Ask about what you see. |
| Connecting | Connecting to your guide… |
| Mic request | Use your microphone to ask the guide questions. You can keep exploring without it. |
| Mic unavailable | Your microphone is unavailable. Choose another device or check microphone access. |
| Guide failure | Your guide could not connect. Try again or keep exploring. |
| Loading | Opening {destination}… |
| Unavailable | {destination} is not available yet. Choose another place. |
| Failed | {destination} could not open. Your previous place is still available. |
| Tracking | We cannot see your hands. Bring them into view to continue. |
| Restore | Put everything back? Objects in this place will return to their starting positions. |
| Quit | Quit Cosmic Simulation XR? |

Use the runtime's actual recovery behavior before promising the previous place remains available. The failure copy above is the intended design contract, not evidence of current implementation. Scientific text and values in mockups must be sourced from existing verified content; never ship image-generated claims without review.

## Figma construction rules

Variables: primitives → semantic roles → component aliases. Component names `Navigation/Dock`, `Navigation/RailItem`, `Navigation/DestinationTile`, `Card/Information`, `Card/DestinationPreview`, `Controls/Button`, `Controls/Segmented`, `Controls/ToggleRow`, `Feedback/Hint`, `Feedback/Notice`, `Guide/Status`.

Auto-layout: outer card vertical; image fill 16:9; text stack; divider; fact pair horizontal; actions horizontal. Components use content properties for photo/title/description/labels/values and variants for state. Keep primary/secondary actions aligned; wrap or grow for Larger text instead of shrinking labels.

Create 1440×1024 review frames for screenshot comparison, and separate millimetre-annotated world-space component frames. Desktop frames are distinct layout instances using the same styles. Prototype link names should match screen IDs. Add notes for runtime-only hand behavior rather than faking it with a pointer-only prototype.

## QA before implementation

- Verify text contrast over both bright passthrough and dark space; distinguish selected from hover/focus without color alone.
- Confirm reach and visual angle seated and standing; avoid requiring head turns between a far rail and far card.
- Test ray, poke, controller and desktop actions. Palm gesture must not conflict with object grabbing or OS gestures.
- Test large text, long titles, scientific notation, missing images and device names.
- Cover permission denied, connection failure, tracking loss, loading cancellation and nested back navigation.
- Verify factual content and imagery credits. Generated illustrations are design references, not approved scientific assets.
- Check no permanent hint, no Resize/Return buttons, and exact three-item dock throughout.
- Reconcile proposed comparison, expanded information, improved recovery and catalogue access with current runtime before coding.

## Outstanding

Figma creation/editing is unavailable. All 39 screen/state entries need editable frame/variant placement in Figma; representative concepts in this package are not all 39 screenshots. Native component library, prototype links and device validation remain outstanding. No live Unity run, native Figma verification or production implementation is claimed.
