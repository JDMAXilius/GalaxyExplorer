# Component library and buttons

Use Figma component sets with explicit variant properties. Instances inherit typography and semantic variables. Auto-layout cards must grow for text-size preference. Image fills use aspect fit/crop consciously, not stretched images.

| Component | Properties/variants | Behavior |
|---|---|---|
| Button | primary / secondary / ghost; default / hover / pressed / focused / disabled / loading; label; leading icon | Primary cyan with dark text; secondary outline; focus outline independent of hover; loading preserves width; disabled cannot activate and has reason when needed |
| Icon button | default / hover / pressed / focused / disabled; accessible name | Settings, Close, Back; large physical hit area; show temporary tooltip if target unclear |
| Dock item | Explore / Guide / Room; active; hover; pressed; disabled | Explore/Guide select context; Room toggles independent on/off state; never mark Room like a separate page |
| Explore rail item | context; selected; default / hover / pressed / focused | Icon + readable label; one selected context; selection indicated by shape/underline and color |
| Destination tile | image; title; selected; ready / loading / unavailable | Selection changes current destination; unavailable never opens blank scene; do not shrink tiles to fit many entries |
| Segmented control | label; options; selected option; enabled | Only mutually exclusive choices; full hit area; category label outside segments |
| Information card | planet / moon / galaxy / scene; collapsed / details; narration off / playing / paused | Photo first, title, paragraph, two relevant facts, actions; details replaces rather than nests card |
| Destination preview | photo; facts; ready / unavailable / loading | Enter is primary, Listen supporting; return to map remains explicit |
| Fact pair | two labels/values; long value; units | Align labels/values; wrap unit; preserve real superscript and scientific notation |
| Toggle row | on / off / disabled / focused | Label remains stable, state visible; settings affects exactly the labelled scope |
| Device row | current device; selected / unavailable | Device chooser opens separate focused panel; no internal technical IDs in user flow |
| Guide status | idle / connecting / listening / thinking / speaking / unavailable / permission-needed | Text and distinct visual state; microphone state separate from service status |
| Hint | palm / pinch / two-hand; active / completed / dismissed | Temporary; clears on successful action; replay from Help |
| Notice | progress / unavailable / failure / restored / tracking | Concise cause and relevant action; no misleading success toast |
| Confirmation | quit / restore; default / waiting | Consequential action and Cancel; modal interaction only while visible |
| Breadcrumb | current context; nested destination | Parent is actionable; current item not a redundant button |

## Action inventory

Explore: show/hide explorer without unloading content. Guide: open companion controls. Room: show/hide passthrough; clear on/off state. Settings: open settings without moving objects.

Listen: start narration for selected content; label becomes Pause during narration, Resume if explicitly paused. Compare: start choosing a second object; disabled for unsupported comparisons with a short explanation. Enter nebula: perform destination transition only when ready. Back to map: unload nested destination and return to prior map selection.

View: Orbits / Compare. In Compare, Size: Equal Size / Relative Size. In Orbits, Spacing: Schematic / True Distances if supported; this must not imply that body scale is also accurate. Arrange row versus shallow arc is still a device-layout decision, not another scale mode.

Start listening: begin deliberate guide interaction; Stop listening: stop microphone capture. Stop speaking: interrupt guide output. Try again: reconnect after service failure. Suggested question: proposed shortcut; label and dispatch behavior require implementation review.

Bring menu here: relocate navigation only. Restore scene: explicit confirmation, restore current scene content without navigation side effects. Sound: master audio; Narration: narration preference within master audio. Large text: 1.25×; Larger: 1.5×. Help: replay lessons. About: product attribution. Quit: confirmation.

Close/Back/Dismiss must be available to ray and controller input, and via Esc on desktop. Omitting a close icon from a clean photo-first card does not mean making dismissal impossible: selecting empty space/Explore can collapse it, and a close affordance appears when appropriate. Device testing must establish that this is discoverable before implementation is accepted.
