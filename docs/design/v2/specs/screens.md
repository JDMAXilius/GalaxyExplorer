# Screens and states

The frame IDs below define the proposed Figma inventory. Concept PNGs cover representative screens; the full inventory is specified here and still needs editable Figma construction. Existing runtime functions and new proposals are distinguished in the final column.

| ID | Screen/state | Contents and next actions | Basis |
|---|---|---|---|
| S01 | Galaxy exploration | Four context rail; galaxy portrait strip; selected galaxy; photo-first card; Listen/Compare | Approved visual; new arrangement |
| S02 | Milky Way map | Destination pins; preview on hover/selection; selected marker persists | Existing map, new card |
| S03 | Destination preview | Photo, title, short description, distance/type; Enter/Listen; Back to map | Existing destinations, new arrangement |
| S04 | Nebula interior | Immersive content; concise scene card; back breadcrumb; room toggle remains reachable | Existing experience |
| S05 | Cosmic Web | Content occupies room; concise explanatory card; no meaningless galaxy strip | Existing experience |
| S06 | Galactic Center | Black hole, scene facts and interaction hint only if supported; back to Milky Way | Existing destination |
| S07 | Solar Orbits | Planet strip; View Orbits; Spacing Schematic/True Distances if wired; orbit model | Existing model; regrouping proposed |
| S08 | Solar Compare Row | Planet strip; View Compare; Size Equal/Relative; row, selected body card | Existing row/relative layouts; new shell |
| S09 | Held planet | Photo-first card; hand manipulation; moons revealed; no Resize/Return buttons | Existing handling; new card |
| S10 | Held moon | Photo-first card; parent relationship; diameter/period; same narrative actions | Existing moons; new card |
| S11 | Exoplanet system | Solar-family layout; clear system name and observed/modelled disclosure; non-voice entry in extended system chooser | Existing HD 110067; access redesign proposed |
| S12 | Choose comparison | Current object named; choose second suitable object; Cancel | Proposed comparison workflow |
| S13 | Compare pair | Two objects; equal/relative sizing; compact aligned facts; Change planet/Done | Proposed comparison workflow |
| S14 | Expanded information | Replace card with readable details, extra facts, imagery attribution, observed/modelled explanation; Back | Proposed details state |
| S15 | Guide idle | Companion, Ready to help, Start listening, short prompts, Dismiss | Existing phase plus new controls |
| S16 | Guide connecting | Connecting to your guide; cancel; do not show Listening yet | Existing service readiness |
| S17 | Guide listening | Listening, mic activity, Stop listening; avoid competing narration | Existing phase |
| S18 | Guide thinking | Thinking; quiet activity indicator; Cancel | Existing phase |
| S19 | Guide speaking | Speaking, Stop speaking; optional user-requested transcript | Existing phase; transcript optional |
| S20 | Guide unavailable | Connection unavailable; Try again; Continue exploring | Existing failure; recovery redesign |
| S21 | Microphone permission | Explain why; Enable microphone opens platform flow; Not now | Platform requirement |
| S22 | Microphone denied/missing | Microphone unavailable; select another device/open platform settings; Continue exploring | Existing mic failure cases |
| S23 | Settings | Sound/Narration/Text size/Microphone; Help/About; menu recovery/scene restore; Quit | Existing functions; reorganized |
| S24 | Device chooser | Available devices, current selection, Test level indicator, Done; empty state | Existing microphone selector |
| S25 | About | Version from app metadata, Microsoft/MIT lineage, imagery/data credits, Source/Privacy, Close | Existing About; redesign |
| S26 | Quit confirmation | Quit Cosmic Simulation XR?; Quit/Keep exploring | Existing two-tap action; redesign |
| S27 | Restore confirmation | Put everything back?; Restore/Cancel; explicit current-scene scope | Existing recovery; separation proposed |
| S28 | Placement | Anchor orb and clear place instruction; successful anchor state; accessible skip when supported | Existing intro |
| S29 | First-use palm lesson | Temporary hand hint; Try it/Skip; clear on success | Proposed gesture discovery |
| S30 | Pinch lesson | Pinch to grab; demonstrate; successful grab completes | Existing hint |
| S31 | Two-hand lesson | Two hands to change size; demonstrate; success clears | Existing hint |
| S32 | Help index | Replay palm/pinch/two-hand lessons; controller/desktop equivalents; Back | Existing Help; reorganized |
| S33 | Loading | Destination name, progress when meaningful, leave safe scene available; Cancel if possible | Existing switching |
| S34 | Destination unavailable | Not available yet; Back to places; never treat as crash | Existing notice |
| S35 | Destination failed | Could not open; Retry/Back to places; preserve prior valid selection | Existing notice; recovery proposed |
| S36 | Tracking lost | Move hands into view; do not move/drop held content from invalid poses; controller fallback | Device state requiring runtime design |
| S37 | Room visible | Room toggle on; content/card contrast readable against bright environment | Existing environment mode |
| S38 | Room hidden | Room toggle off; destination lighting applies; labels remain readable | Existing environment mode |
| S39 | Desktop adaptation | Screen-anchored shell, same selected contexts; keyboard/mouse Help | Existing desktop mode; redesigned shell |

## Additional state rules

Only one focused utility panel is open at once. Opening Guide does not unload a place; Settings preserves the selection. Selecting a context opens its valid default/last place and updates local picker. Nested travel keeps a clear Back route. Switching places may restore held objects according to existing runtime behavior; this must be communicated and tested, not silently changed by UI design.

Context headers should say Solar System, Galaxies or Milky Way rather than internal module IDs. Additional galaxy entries page horizontally with readable names. Systems beyond the Solar System must remain accessible without asking the Being; exact extended catalogue grouping is open for content review.

Hand gesture return behavior exists in current code; omit main-screen Return buttons as requested, but teach the behavior accurately in Help. Controller users cannot rely on palm gestures; dock and controller selection remain available. Do not imply a black-hole resize capability works where current code has not wired it.

## Definition of visual completion

Every frame needs actual reviewed content, supported states, clear dismissal and links. All screen entries above need a frame or deliberate variant in Figma before calling the complete screen set designed there. Generated image concepts alone do not meet that definition. Physical sizing and actual hand interactions require Quest validation.
