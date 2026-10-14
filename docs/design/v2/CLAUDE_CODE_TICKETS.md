# Claude Code tickets: UI 2.0 follow-ups

Track CC-U2. For the next Claude Code session on `quest3-port` (mirrored to `main`). Written 14 Oct 2026 at the end of the session that built UI 2.0 (`docs/design/v2/UNITY_ROADMAP.md`, per-phase status in `docs/BACKLOG.md` U2-P0 to U2-P10). All start **todo** unless marked.

**Before starting:**
- Read `CLAUDE.md`.
- Use one Unity editor client at a time (`tools/mcp/umcp.js`, `tools/mcp/compile.ps1`). Never edit scripts in play mode.
- Run `git checkout -- Assets/materials` before committing.
- The owner tests live in the Game view while you work. Before the in-app check (Cosmic Simulation > UI 2 > Run Check), ask them to keep hands off.

## CC-U2-001: Verify the Guide's rotating suggestions and transcript (code landed, untested at runtime)

**What landed (last commit of the session):** `Ui2Guide` picks a new question and a new command each time the dock's Guide opens.
- The lists are in `Ui2Guide.Questions`, `Commands` and `RowCommands`.
- The commands map to the guide's real actions: `open_place`, `pull_body`, `restore` (`Assets/scripts/being/Host.cs`, `Assets/Being/Session.cs`).
- Row commands are offered only when the planets stand in a row.
- While the guide is thinking or speaking, the two suggestions are replaced by a transcript box showing "You: <heard>" and the guide's answer (`Being.Heard`, `Being.Said`). Fresh suggestions come back when it returns to listening.

**Check:**
- Open and close the Guide several times: the pair changes each time.
- Ask a question: the transcript shows, then the suggestions return.
- Press a command suggestion: the guide performs it.

`Being.Said` arrives once per answer (`response.output_audio_transcript.done`), not word by word. If the owner wants words to appear as spoken, add a delta event in `Session` (`response.output_audio_transcript.delta`).

**Owner's ask, still open:** the guide's own suggestions. The owner wants the guide to be able to suggest things too. One option is a tool call (`suggest(text)`) that fills the suggestion buttons. Propose before building.

**Proposal (cloud session, 14 Oct; not built, waiting for the owner's yes):**
- **A fourth tool, `suggest`,** with two optional string fields, `question` and `command`, each under 40 characters. The prompt asks the guide to call it at the end of an answer with one follow-up question and one thing it can do (`open_place`, `pull_body` or `restore` in words), never both about the same thing.
- **Silent.** A `suggest` call gets its `function_call_output` like the others but no `Respond()`: today every tool call triggers another response, which would make the guide speak twice.
- **Plumbing:** `Session` parses the two fields (its `Args` only knows `id`), raises `Suggested(question, command)`; `Being` keeps the last pair; `Ui2Guide` shows that pair the next time it returns to listening, then falls back to its rotating lists. A command suggestion runs through the same `IHost.Act` as a spoken command; one that maps to no action is shown as a question instead.
- **Safety:** text only, trimmed and length-capped in `Ui2Guide`; nothing the model sends becomes an action without the player pressing it.
- **Cost:** one extra small tool call per answer, no audio.

**CC-U2-002 and CC-U2-003, cloud session 14 Oct (code, not run):** `Ui2SelfCheck` now clicks a nebula label through `CardPOI.OnPointerDown` and expects the preview on the map. On the orbit view, `PlanetPOI` asks a new `Picked(id)` hook with each of its `BodyIds` before its original scene load: the `poi_<id>_prefab` name first, then the old scene name without `_view_scene` (Mercury's scene name is `MercuryViewmercury_view_scene`, so the prefab name has to come first). `Ui2Explore` answers it from a `bodies` catalog that *Build Shell Prefab* fills with every `BodyInfo` under `Assets/data`: on the orbit view with the card on screen, the card shows that body and nothing travels; picking it again puts the card away; a pulled body's panel outranks it. Anywhere else a known body is consumed without being remembered, because the scene it would load no longer exists. Mars has no `PlanetPOI` on its prefab and cannot be picked yet. Needs *Build Shell Prefab* before it works. Verification: BACKLOG CS-322.

## CC-U2-002: Map nebula labels open the preview card (code landed, untested)

Clicking a nebula label on the Milky Way map goes through `CardPOI`, not `PlanetPOI`, so it travelled straight to the nebula without the preview card. `CardPOI` now asks `PlanetPOI.Preview` first.

**Check:** click a nebula label on the map. The card should show the preview (Distance, Type, Enter nebula, Listen, Back to map), and Enter should travel. Also add a step to `Ui2SelfCheck` that drives `CardPOI` rather than calling `PlanetPOI.Preview` directly.

## CC-U2-003: The 2D card follows every selection in the Solar System

**Owner's ask:** selecting a planet or a moon anywhere in the Solar System updates the 2D information card.

**Row view (`solar_system_planets`):** works for pulled planets. `Ui2Explore.Track` reads the open legacy `InfoPanel`. Verify that pulling a moon off a held planet switches the card to the moon.

**Orbits view (`solar_system`):** not done.
- **What happens now:** a planet there is a `Planet` component forwarding to its `PointOfInterest` (`Assets/scripts/Planet.cs`). `PlanetPOI.OnPointerDown` only reaches the UI hook when the marker has a `Destination` module; otherwise it starts the original Galaxy Explorer scene load (`OnPointerDownRoutine`, `SceneToLoad`).
- **Plan:**
  1. In play mode, list the POIs in the orbit view: names, `SceneToLoad`, whether a `BodyInfo` is reachable.
  2. Add a hook in `PlanetPOI.OnPointerDown` before the scene load (for example a static `Picked` func taking the POI's body id).
  3. Make `Ui2Explore` show that body's `BodyInfo` on the card, with no travel.
- **Body data:** `BodyInfo` assets are in `Assets/data`. Load them by id through a small catalog on the prefab (the builder can collect them), because the orbit scene has no `LayoutRig`.

## CC-U2-004: Card photos from ChatGPT

The owner is sending `docs/design/v2/downloads/ChatGPT-image-tickets.zip` (tickets in `CHATGPT_IMAGE_TICKETS.md`) to ChatGPT. When images come back:
1. Copy them into `Assets/ui/photos/` as `card_<id>.png`.
2. Run Cosmic Simulation > UI 2 > Build Shell Prefab.
3. Log each one in `Assets/_sources/CREDITS.md` with the prompt and tool ChatGPT reports.
4. Delete the stand-ins they replace only if nothing else uses them.

## CC-U2-005: Remaining roadmap items that need the headset or the owner

- **U2-P0:** distance and size test on the Quest. Near or far shell; `Ui2Shell.MetresPerPixel` is a guess.
- **U2-P8:**
  - the placement step;
  - restyling what the palm gesture shows (the owner wants the palm menu kept);
  - the "We cannot see your hands" notice; *code landed 14 Oct (cloud): `XRInputRig.WatchForLostHands`, through `SwitchNotice.Say`; device check BACKLOG CS-324*;
  - running the lessons on device.
- **U2-P4:** restyle the map markers to `Navigation/MapPin` (Figma `71:395`). They keep their legacy look.
- **U2-P9:** Compare for galaxies and moons needs the owner's call on what is compared.
- **U2-P10:** delete the legacy UI (dock, utility window, hand menu prefabs, builders) only after a headset walkthrough and the owner's go-ahead.
  - **Keep:** the in-world `InfoPanel` card beside a pulled body (owner asked to keep it), and `DockController`'s pose and recentre logic (the shell rides it in the headset).
  - **Then rewrite:** the legacy App Check (it expects the old dock and fails while UI 2 is on) or retire it in favour of `Ui2SelfCheck`.

## Facts the next session will want

- **UI 2 is on by default, builds included.** Cosmic Simulation > UI 2 > Turn Off restores the old dock.
- **Rebuild after design changes:** the prefab is generated. Edit `Assets/scripts/ui2/Editor/Ui2Builder.cs`, `Ui2ExploreBuilder.cs`, `Ui2PanelsBuilder.cs` or `Ui2Kit.cs`, then run Build Shell Prefab. Never hand-edit the prefab.
- **Desktop:** the canvas switches to screen space (`Ui2Shell.FitToScreen`), and every group is pinned to its edge. Canvas sorting order 70 keeps it above the room's dim quad (render queue 3900).
- **Intro:** the shell shows only after `ExperienceDirector.IntroComplete`, which is the real end of the intro rather than the 45 s watchdog. The director now waits for the start module's view before adopting, so the app starts on the Milky Way.
- **Sound:** narration starts by itself. The card's Listen plays the selection's own sound: `BodyInfo.Ambience`, else `PlanetForceSolver.AmbientClip`, else the narration.
- **Pulled bodies:** their materials are moved to render queue 3700+ while out (`Ui2Explore.KeepPulledInFront`) so neighbours never paint over them.
- **Figma:** the plan's monthly MCP tool-call limit was reached on 13 Oct. Pictures were cut from `docs/design/v2/screens/` instead.
