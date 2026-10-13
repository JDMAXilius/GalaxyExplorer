# GalaxyExplorer UI discovery

Source inspection on 13 October 2026. This is a product inventory and a set of hypotheses, not a visual audit: Unity and fresh runtime captures are unavailable. The supplied Figma file dx6MxAxPqZ9QBSRJqCCVev could not be accessed (proxy returned 403). The repository's older UI spec refers to a different file, qWxL0ZGiyI7aRjnQAVISoI; do not treat them as the same design.

## Product and audience

Cosmic Simulation XR is educational mixed-reality exploration for curious adults, families, classroom demos and XR showcases. Quest 3/3S is primary; Windows mouse/keyboard is secondary. The core experience is discovering real places, holding and resizing planets, comparing scale, reading facts, and talking to a luminous companion. Preserve the distinction between observed imagery and modelled impressions.

## Current UI inventory

| Surface | Current responsibility | Grounding |
|---|---|---|
| Launch and placement | Logo, anchor content in the room, intro, skip into a chosen place | docs/SIMULATION_GUIDE.md §2; intro scripts |
| World dock | Seven place tiles, layout popups, room mode, recenter, settings, help, Being, reset and about | Assets/scripts/experience/DockController.cs |
| Palm menu | Mode, Reset, About and bring Dock to the player | docs/BACKLOG.md CS-275 and CS-277, 6 October |
| Desktop dock | Screen-space mirror of shared navigation | Assets/scripts/experience/DesktopDock.cs |
| In-world destination markers | Preview and enter nebulae; choose galaxies; solar-system/center shortcuts | docs/SIMULATION_GUIDE.md §3 |
| Object information | Body, scene and moon panels; narration; facts | Assets/scripts/experience/InfoPanel.cs |
| Settings window | Size, sound, narration, text size, microphone, quit confirmation | Assets/scripts/experience/UtilityWindow.cs |
| Cosmic Being | Questions and voice navigation | Assets/Being; Assets/scripts/being/Host.cs |
| Feedback | Hints, notices for loading or unavailable places, hover/press feedback | experience/HintCards.cs; SwitchNotice.cs; XR/GEPressVisual.cs |

The dock covers Cosmic Web, Galaxies, Milky Way, Andromeda, Solar System, The Planets and Galactic Center. Nested destinations include nebulae, other galaxies and HD 110067. The guide describes HD 110067 as voice-only; confirm this against runtime before designing its new entry point.

## Representative flows to capture

1. First launch → place Earth → discover Milky Way → open dock.
2. Choose The Planets → choose a layout → pull Saturn → inspect Titan → return both.
3. Milky Way → preview a nebula → enter → return to the map.
4. Summon Being → microphone/listening feedback → ask a question → voice travel.
5. Seated use → bring dock closer → change text size → recover displaced objects.
6. Repeat essential flows with desktop input.

## Rebuild hypotheses

These are questions to validate, not observed usability defects.

- Give global navigation, object actions and app settings distinct roles. The current dock, palm menu, markers and voice route offer multiple paths; decide which is primary and which is a shortcut.
- Explore a spatial atlas organized by scale and place type. Seven equally prominent tiles mix scales, individual destinations and alternate views of the same system.
- Put layout/comparison actions beside their current subject, with visible active state.
- Separate bringing the menu back from returning objects home. DockController already distinguishes BringToPlayer from Recenter, which also restores content.
- Make voice guidance discoverable without making any destination depend on voice alone.
- Retain deliberate placement and seated comfort. Current dock defaults are 0.8 m ahead, 70% of eye height, with a 0.7 m floor; it stays parked instead of following the head.
- Validate text over bright passthrough, reach, ray/poke target sizes, occlusion, tracking loss and one-handed operation on device.

## Three directions to discuss before mockups

1. Spatial atlas: a compact scale navigator, expandable destination browser and local object cards. Best hypothesis for self-directed exploration.
2. Guided journeys: short thematic tours with a free-explore exit and Being as guide. Best hypothesis for first-time family/classroom demos.
3. Exploration workbench: comparison tray, object facts and local controls. Best hypothesis for deeper planet learning, with a risk of cluttering the room.

Potential combination: atlas for navigation, optional guided journeys for onboarding, and workbench tools only while comparing objects. Choose after inspecting the supplied Figma frames and current runtime screens.

## Implementation boundaries and evidence

- Assets/scripts is shipping. Assets/Cosmic is a parked rebuild; its main scene is disabled in EditorBuildSettings.
- Older GDD, UI spec and September system guide have stale statements. October source and backlog changes take precedence for current behavior.
- Existing parity checker executed: 829 values checked, 87 differences between shipping and parked rebuild; layout-only check exited 1 with 40 differences across 112 values. Missing moon data and straight-row versus arc layouts mean the parked rebuild is not a safe content baseline without reconciliation. This does not show the shipping app fails.
- Git read access worked. Node 24.19.0 and Python 3.12.14 are available. No Unity editor, relay or Unity licensing-variable names were found. No app build or functional smoke run was possible.
- Repository working tree remained clean. No application code, prefab, dependency or lockfile changed.

Next evidence needed: screenshots/PDF of the supplied Figma file, then current desktop and headset captures when a licensed Unity 6000.6.0f1 environment is available. Review the flows before selecting a visual direction or implementing the rebuild.
