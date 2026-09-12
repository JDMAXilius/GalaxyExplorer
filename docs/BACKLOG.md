# Cosmic Simulation XR — Ticket backlog

*Derived from `docs/COSMIC_SIMULATION_XR_ROADMAP.md`. Every ticket has a **track**:*
- **[CC] Claude Code** — can be done in the repo alone: scripts, editor tools, data, copy, docs, prefab/scene YAML edits that don't need a live editor to verify. Compile-check by reading; the terminal track verifies in the editor later.
- **[TERM] Terminal** — needs the running Unity editor through the MCP relay (play-mode tests, screenshots, prefab surgery that must be verified live), the Quest device, or the Figma / Higgsfield MCPs. Done in the local Claude Code terminal session with the editor open.

## How to work this backlog (any Claude Code session)
1. Read `CLAUDE.md`, then this file. Pick the **first ticket in your track whose status is `todo` and whose dependencies are `done`**.
2. Set its status to `doing` (edit this file) in your first commit.
3. Do the work following the acceptance line. Keep changes scoped to the ticket's files where possible.
4. Commit with the message `<ID>: <title>` and a short body. Update the ticket to `done` in the same commit, adding one line of notes (what changed, anything the other track must verify).
5. If a [CC] ticket turns out to need the live editor, set status `blocked-term` with a note and move on; the terminal track picks it up.
6. Never push; the owner pushes. Never mark `done` what you could not verify — say so in notes.

Status values: `todo` · `doing` · `done` · `blocked-term` · `blocked-cc` · `dropped`.

---

## Phase 0 — Foundation and rebrand

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-001 | CC | Back up scenes to `Assets/scenes/_backup_original/` | — | done |
| CS-002 | CC | Rebrand: product name, bundle id, About copy | — | done |
| CS-003 | CC | Repo hygiene: `.gitattributes`, `Assets/_sources/CREDITS.md`, `docs/decisions.md` | — | done |
| CS-004 | TERM | Create the Figma file and pages | — | done |
| CS-005 | TERM | Source public-domain imagery (Helix, Orion, Crab, Homunculus, deep field, planet maps) and log credits | CS-003 | todo |
| CS-006 | CC | Write the copy deck (`docs/copy/*.md`) | — | done |
| CS-007 | TERM | Verify in the editor: backups import, build list clean, identity applied; commit generated `.meta` files | CS-001 | done |

**Phase 0 notes (11 Sep 2026).**
*CS-001 done:* five scenes copied to `Assets/scenes/_backup_original/` with a README; the owner's `main_scene - Copy.unity` moved in as `main_scene_user_copy.unity` (kept its `.meta`, so its GUID is unchanged) and removed from the build list. The five copies have no `.meta` yet — Unity generates them on next open; CS-007 commits them.
*CS-003 done:* `.gitattributes` (Unity YAML marked `merge=binary` so a bad auto-merge can't corrupt a scene), `Assets/_sources/CREDITS.md` (licence table + the six assets we have already made + what CS-005 must source), `docs/decisions.md` (D-001..D-005 with rationale).
*CS-006 done:* `docs/copy/` — README (rules: **ASCII only**, the Selawik fonts have no degree sign, en dash or curly quote), `experiences.md` (7 panels), `bodies.md` (10 bodies, paragraph + 4 stats), `moons.md` (11 moons, 6 in scope), `nebulae.md` (7 destination overlays), `hints.md`, `ui.md` (dock, pop-ups, overlay, About, messages).
*Fixed in passing:* `main_scene` and `core_systems_scene` were each listed twice in `EditorBuildSettings`; duplicates removed.
*CS-007 done:* editor verification — six backup scenes import with fresh GUIDs (the owner's copy keeps its original), build list holds exactly the six shipping scenes with no duplicates and no backups, product name and both bundle ids applied. `.meta` files committed.
*CS-004 done:* Figma file **Cosmic Simulation XR** — `https://www.figma.com/design/qWxL0ZGiyI7aRjnQAVISoI` — with pages *Design System* and *Front End - Screens*. It sits in the owner's drafts; move it into a team project when convenient.
*Resolved 11 Sep 2026:* `companyName` was still "Microsoft Corporation"; set to **JDMAXilius** to match the bundle id. It sets the save-data path (`%USERPROFILE%\AppData\LocalLow\<company>\<product>`) and the publisher the store shows. `Quest3ProjectSetup` writes only the identifier, not the company, so this sticks across builds.

## Phase 0/1 note on the build (11 Sep 2026)

A Quest APK build stalls on a modal: **"Unsupported Input Handling on Android"** — Active Input Handling is "Both", which Android does not officially support. We need "Both" (XRI uses the new Input System, TouchScript the old one), and the shipped 111 MB APK was built the same way, so the answer is **Ignore**. Tick *Don't ask again for this session* to keep an unattended build from blocking. If we ever drop TouchScript, switch to Input System only and the warning goes away.

**CS-001** Copy `main_scene`, `core_systems_scene`, `galaxy_view_scene`, `solar_system_view_scene`, `galactic_center_view_scene` (+ `.meta`, keep GUIDs unchanged is *not* possible — generate new meta GUIDs by deleting the copied `.meta` files so Unity regenerates them) into `Assets/scenes/_backup_original/`; add a `README.md` there ("do not edit"); ensure none are in `ProjectSettings/EditorBuildSettings.asset`. *Acceptance:* files present, not in build list, README present.
**CS-002** Done 11 Sep 2026: `productName` "Cosmic Simulation XR", Android/Standalone identifier `com.jdmaxilius.cosmicsimulationxr`; About text already updated. *Remaining (TERM):* confirm Unity accepts the identifier on next open.
**CS-003** `.gitattributes` (`* text=auto`, `*.cs text eol=crlf`, `*.md text eol=lf`, binaries `-text`), `Assets/_sources/CREDITS.md` (table: file, source URL, licence, date, edits), `docs/decisions.md` (log the five decisions of 11 Sep 2026 — see CLAUDE.md). *Acceptance:* files exist; `git status` clean after commit.
**CS-006** Files: `docs/copy/experiences.md` (7 scene panels: title, 2–3 paragraphs, instruction line), `docs/copy/bodies.md` (Sun, 8 planets, Pluto: title, paragraph ≤ 55 words, Diameter/Mass/OrbitalPeriod/DayLength with unit and exponent — values in GDD §6.1), `docs/copy/moons.md` (Moon, Ganymede, Callisto, Titan, Mimas, Iapetus + optional Io, Europa, Enceladus, Phobos, Deimos: name, parent, diameter, orbital period, one sentence), `docs/copy/nebulae.md` (Helix, Crab, Homunculus, Orion + Pillars, NGC 1501, Trumpler 14: title, 2 paragraphs), `docs/copy/hints.md` (two hint cards). Our own words; no text from the reference. *Acceptance:* every entry present; word limits met.

## Phase 1 — Design system and UI (Figma)

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-010 | TERM | Figma tokens page (colour, type, spacing, radii) | CS-004 | done |
| CS-011 | TERM | Figma dock frames + states | CS-010 | done |
| CS-012 | TERM | Figma pop-ups + utility window | CS-010 | done |
| CS-013 | TERM | Figma info panels (body / scene / moon) | CS-010 | done |
| CS-014 | TERM | Figma labels (tag states, moon labels, leader line) | CS-010 | done |
| CS-015 | TERM | Figma desktop HUD (dock mirror, controls overlay, About) | CS-010 | done |
| CS-016 | TERM | Figma hint cards (2) | CS-010 | done |
| CS-017 | TERM | Export SVG/PNG @2× to `Assets/ui/figma/`, write `docs/ui/spec.md` | CS-011…016 | done |
| CS-018 | CC | Sprite import settings script (`Editor/UiSpriteImporter.cs`: UI sprite, no mips, 9-slice per spec) | CS-017 | done |

**Phase 1 notes (11 Sep 2026).** The file has two pages, as the owner asked: *Design System* (read-me, colour, type, space and radius, surfaces) and *Front End - Screens* (A dock, B panels/tags/hints, C three in-headset shots, D desktop, E pop-ups and dock states).
Two conventions the rest of the work depends on:
- **Scale.** Virtual UI is specified in millimetres and drawn at **1 mm = 4 px**. A dock tile is 110 x 62 mm, drawn 440 x 248.
- **Font.** Figma has neither Selawik nor Segoe UI, so the mockups use **Source Sans 3** as a metric stand-in. The app keeps its Selawik SDF assets; match the millimetre sizes, not the family.
CS-011 states are drawn (idle, hover, pressed, active) but are not yet Figma component variants.
*CS-015 done:* board **F - Desktop overlays** carries both panels the desktop needs — the controls overlay (mouse column and keyboard column, every line naming a thing the player does rather than a system) and About, with the Microsoft/MIT credit, the imagery credit and three buttons. Board **D - Desktop** holds the dock mirror.

*CS-017 done:* a third page, **Export - Unity**, holds the atoms actually worth shipping, and they are in `Assets/ui/figma/` — five white nine-slice rounded rects (1, 2, 4, 6, 8 mm), the dock tile foot gradient, and six icons (passthrough, recenter, help, mute, close, back). Editable SVG sources in `Assets/_sources/figma_svg/`; measurements in `docs/ui/spec.md`.
*Why so few files:* the whole UI is rounded rects plus text, so a plate, a tag pill and a pop-up are one white sprite at different tints and radii rather than one sprite each. The tile gradients on board A are placeholders — real tile art is rendered from the scenes (CS-031) — and the black halo is generated in code, so neither is exported. Twelve files, about 21 KB.
*CS-018 done:* `Assets/scripts/Editor/UiSpriteImporter.cs`, an `AssetPostprocessor` over `Assets/ui/figma/`. Sprite, single, no mips, clamp, uncompressed, and a nine-slice border read from the file name (`ui_rounded_r<n>` → border n + 2, so the corner arc sits inside the border and is never stretched). Menu **Cosmic Simulation → Reimport UI Sprites** forces a pass. Verified on all twelve.
*Convention worth keeping:* export atoms are drawn at **8 px per millimetre** (twice the board scale) and imported at **8000 pixels per unit**. A Unity unit is a metre, so a sprite pixel is exactly a millimetre and the numbers in `docs/ui/spec.md` can be used directly when laying out a panel.
*Still to do before a store listing:* the About panel's "Source code" and "Privacy" links must point at our repo and our own privacy page.

Sizes and colours: GDD §8. *Acceptance for CS-017:* every frame exported; spec maps frame → prefab with mm sizes.

## Phase 2 — Core architecture

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-020 | CC | `ExperienceModule`, `BodyInfo`, `LayoutPreset` ScriptableObjects (`Assets/scripts/experience/`) | — | done |
| CS-021 | CC | `CopyImporter` editor menu: `docs/copy/*.md` → SO assets | CS-006, CS-020 | done |
| CS-022 | CC | `ExperienceDirector` (switch over `ViewLoader`, events, restore-on-switch) | CS-020 | done |
| CS-023 | CC | `EnvironmentController` + dim quad shader/material + halo gradient generator | — | done |
| CS-024 | CC | `FreePlacementSolver` (no snap-back, `RestoreLayout`, bounds auto-restore) | CS-020 | done |
| CS-025 | CC | `TwoHandTransformer` (limits per type, smoothing) | — | done |
| CS-026 | CC | `LabelButton` (hover grow/cyan, pinch/click → target) | — | done |
| CS-027 | CC | `InfoPanel` v2 from `PlanetInfoCard` (body/scene/moon variants, `<sup>` mass, outline) | CS-020 | done |
| CS-028 | CC | `DockController`, `DockPopup` scripts (data-bound, poke via `GEButton`/`XRPokeFilter`, grab bar, palm-up show/hide) | CS-020 | done |
| CS-029 | CC | `DesktopDock` + `DesktopMouseInput` additions (`P`, `F2–F8`, label clicks, free placement) | CS-028 | done |
| CS-030 | TERM | Build dock/pop-up/panel prefabs from Figma exports (RunCommand scripts) | CS-017, CS-027, CS-028 | todo |
| CS-031 | TERM | Dock thumbnails: render 7 modules to `Assets/ui/thumbnails/` | CS-030 | todo |
| CS-032 | TERM | Register existing 3 scenes as modules; hide old Back/zoom UI; verify switching in play mode | CS-022, CS-030 | todo |
| CS-033 | TERM | `smoke.cs` harness: switch all modules, panels, place/restore, screenshots, console clean | CS-032 | todo |
| CS-034 | TERM | Quest Link check: dock poke, dim quad, halo, passthrough toggle | CS-032 | todo |

Design contract: GDD §3, §5, §8; architecture: Technical Overview §5.5–5.6, §7.2. *Acceptance:* per roadmap Phase 2 "done when".

**Phase 2 notes (11 Sep 2026).**
*CS-020 done:* `Assets/scripts/experience/` — `ExperienceModule` (dock tile or destination: scene or content prefab, environment mode, panel copy, layouts, audio), `BodyInfo` (body or moon: title, subtitle, paragraph, `Stat[]`, moons, the planet a moon orbits; `Stat.ToRichText()` renders the superscript), `LayoutPreset` (named arrangement + `Find(bodyId)`). Namespace `CosmicSimulation`.
*CS-021 done:* `Assets/scripts/Editor/CopyImporter.cs`, menu **Cosmic Simulation → Import Copy**, parses `docs/copy/*.md` into `Assets/data/{bodies,moons,experiences,destinations,layouts}`. Idempotent: assets are updated in place so references survive. Verified run: 10 bodies, 11 moons attached to Earth/Jupiter/Saturn/Mars, 7 experiences, 7 destinations, none incomplete.
*Gotcha worth keeping:* inside `AssetDatabase.StartAssetEditing()` the database is not refreshed, so `IsValidFolder` reports a folder you just made as missing and `CreateFolder` silently creates `data 1`, `data 2`, … beside it. Create folders through `Directory.CreateDirectory` + one `Refresh` instead.

*CS-022 done:* `ExperienceDirector` — `Switch(module)` clears destinations, restores moved bodies, stops narration, sets the environment, unloads the previous view, loads the new one through `ViewLoaderScript.LoadViewAsync`, grows it in on a cubic ease-out, then starts narration and raises `ExperienceChanged`. Also `OpenDestination`, `ClearDestinations`, `RestoreEverything`.
*CS-023 done:* `EnvironmentController` (dim quad parented to `Camera.main` at 0.06 m, queue 3900 so panels and the dock stay legible; generated 256² radial halo texture; `SpawnHalo`/`Forget`; `SetPassthroughForced` for the dock button) and `BlackHalo` (billboard 1.6× the subject, 0.15 m behind it along the view ray, 0.4 s fade). Verified in play mode on the galactic centre: dimming took mean frame brightness 4.28 → 2.95 with the desktop HUD unaffected, and the halo renders at queue 2990 tracking S2 as it orbits.
*Gotcha worth keeping:* the halo first failed with *"Material … with Shader 'Unlit/Transparent' doesn't have a color property '_Color'"*. `Unlit/Transparent Colored` is not in this project and `Shader.Find` on a built-in that no material references can return null in a player build, so both surfaces now use `Assets/shaders/environment_tint_shader.shader` (`CosmicSimulation/EnvironmentTint`) — a flat tinted transparent shader that also carries the stereo macros Android multiview needs. It is registered under **Always Included Shaders** in `ProjectSettings/GraphicsSettings.asset` so the runtime lookup survives the APK build.
*Gotcha worth keeping:* `stop.cs` leaves play mode; `exit.cs` **quits the editor**. Do not reach for the wrong one. If `EditorApplication.isPlaying` reads stale `True` after a crash-out, set `isPaused = false` then `isPlaying = false` and again inside `delayCall` before compiling.

*CS-024 done:* `FreePlacementSolver` — a body stays where it is left and comes home for three reasons only: the player asked, the experience changed, or it has been out of reach (further than 4 m, or below the floor) for 5 s. Home is `ForceSolver.RootTransform` read live, so a moving layout stays authoritative; the return is a 0.8 s cubic ease-out and then `ResetToRoot()`, so the solver's own machinery unwinds the moons, panel and highlighter. **This replaces `PostManipulationResetter` on anything the player arranges** — that component slides an object back a couple of seconds after *every* release, which is the opposite of GDD 5.2. Verified in play mode.
*CS-025 done, smaller than the ticket assumed:* `ManipulationHandler` **already is** the two-hand transformer (midpoint move, `FromToRotation` rotate, distance-ratio scale, exponential smoothing), so writing a `TwoHandTransformer` would have meant two competing systems. What was missing was the limits, so it gained one hook — `IManipulationScaleConstraint`, resolved once and cached — and `ScaleLimits` implements it. Limits are stated in **metres of real width** (Body 0.05–3, Model 0.4–4, Nebula 0.3–2), not scale factors, because a body's authored scale has no fixed relation to how wide it looks; the object is measured lazily on first clamp, since bodies are authored at orbit size and grown by the force solver before anyone can get two hands on them. Verified against arithmetic on a 1 m cube: caps at 3 m, floors at 0.05 m, passes an in-range value through, stays uniform.
*CS-026 done:* `LabelButton` — the destination tags. Idle, hover (15% larger, cyan fill, text flips dark so it still reads) and selected, plus a leader line that can be pointed at a world position. Uses a `MaterialPropertyBlock` rather than a material instance: several are on screen at once and instancing would cost a draw call each. Hover and click arrive through `GEInputEvents.ExecuteHierarchy`, so hand ray, fingertip and desktop mouse all work with no special cases; a poke fires on pointer-down, a ray on click, with a 0.3 s cool-down. Verified: one pick registered from two rapid clicks.
*CS-027 done:* `InfoPanel` — `PlanetInfoCard` rebuilt to read `BodyInfo` and `ExperienceModule` instead of fields typed into a prefab. One component, three shapes: body (title, subtitle, paragraph, 2×2 stats), scene (paragraphs plus an instruction), moon (name, one line of numbers, one sentence) — the moon variant is chosen from the data, since a moon is just a body that orbits one. No plate on any of them; over passthrough a plate punches a hole in the room, so it is outlined white text. Verified against the real copy: Earth → Body with 4 stats, mass rendering `5.97 x 10<sup>24</sup> kg`; Moon → Moon variant orbiting Earth; Solar System → Scene with 2 paragraphs.
*CS-028 done:* `DockController`, `DockTile`, `DockPopup`. The dock builds itself from `ExperienceDirector.Modules`, so adding an experience is a data change, and it **parks** rather than following the head — a dock that chases you cannot be looked away from. Tile states are movement, not tint (hover lifts 6 mm, poke pushes 4 mm, open keeps a cyan underline), because a tile is mostly photograph. Only modules with more than one layout open a pop-up. Show/hide is palm-up on the left hand, latched so a held palm toggles once.
*CS-029 done:* `P` toggles the room preview and `F2`–`F8` jump to the seven dock tiles in order, counting tiles only so a destination never takes a function key. Label clicks and free placement already work through the existing pointer path plus CS-024.
*Gotcha worth keeping:* a component added with `AddComponent` and used in the same frame has not necessarily had `Awake` run, and a label or body is often bound the frame it is spawned. `LabelButton` and `FreePlacementSolver` both threw on that; both now lazily initialise from every entry point rather than trusting `Awake`.
*Note for CS-040:* the dock data reads 7 tiles, 7 destinations and **0 with a layout choice** — correct for now. `HasLayoutChoice` needs at least two `LayoutPreset` assets on a module, and CS-040 generates them; the three layout pop-ups stay dormant until it lands.

## Phase 3 — Solar system one-to-one

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-040 | CC | `LayoutPreset` generator for SolarRow and RelativeSize (GDD §4.1 numbers) | CS-020 | todo |
| CS-041 | TERM | Create `solar_row_scene` from `solar_system_view_scene`; wire presets and pop-up | CS-040, CS-032 | todo |
| CS-042 | CC | `BodyInfo` assets for 10 bodies (from copy deck) | CS-021 | todo |
| CS-043 | TERM | Add moons via the `MoonForceSolver` pattern: Ganymede, Callisto, Titan, Mimas, Iapetus (+ optional 5) | CS-032 | todo |
| CS-044 | CC | `MoonLabel` (small/large) | — | todo |
| CS-045 | TERM | Schematic/Realistic wiring on the orbit model; name labels; Asteroid Belt label | CS-032 | todo |
| CS-046 | CC | `SunTouchResponse` (brightness pulse + rumble while a hand is inside) | — | todo |
| CS-047 | CC | Earth atmosphere rim material (reuse `halo_shader`) | — | todo |
| CS-048 | TERM | Two-hand room-scale tuning (Saturn rings around the user) on Link | CS-025, CS-041 | todo |
| CS-049 | TERM | Desktop: number keys in row order, moon clicks; run smoke | CS-041 | todo |

## Phase 4 — Milky Way destinations, nebulae, black hole

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-050 | TERM | Replace galaxy POI cards with `LabelButton` tags (6 + 3 extras on) | CS-026, CS-032 | todo |
| CS-051 | CC | Nebula overlay renderer: layered-card prefab script + soft-depth-fade shader | — | todo |
| CS-052 | TERM | Nebula layer assets ×4 (project textures + Hubble; Higgsfield depth layers) | CS-005 | todo |
| CS-053 | TERM | Sagittarius A\* module: full black + stars, hand-ray, panel, grow-in | CS-032 | todo |
| CS-054 | TERM | Milky Way panel + grow-in; verify all six tags open | CS-050, CS-052 | todo |
| CS-055 | CC | Desktop tag hover/click | CS-026 | todo |

## Phase 5 — Andromeda, Galaxies, Cosmic Web

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-060 | CC | Andromeda `StarsData` parameter set + prefab variant of `milky_way_prefab` | — | todo |
| CS-061 | TERM | `andromeda_scene` from `galaxy_view_scene`; verify look | CS-060, CS-032 | todo |
| CS-062 | CC | Galaxy field renderer (`DrawMeshInstanced` billboards, atlas shader, drift) | — | todo |
| CS-063 | TERM | Galaxy sprite atlas (deep-field cutouts + Higgsfield fill) | CS-005 | todo |
| CS-064 | TERM | `galaxies_scene`; passthrough allowed; verify | CS-062, CS-063 | todo |
| CS-065 | CC | Cosmic Web generator (Voronoi filaments → point buffer) + violet ramp for star shader | — | todo |
| CS-066 | TERM | `cosmic_web_scene` from `galactic_center_view_scene`; verify; frame time | CS-065 | todo |
| CS-067 | TERM | Thumbnails for the three new scenes | CS-061, CS-064, CS-066 | todo |

## Phase 6 — Audio, narration, intro, branding

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-070 | CC | SFX map: assign existing `ui_*` clips in `AudioServiceProfile` for dock/pop-up/label/grab/whoosh | — | todo |
| CS-071 | TERM | 11 TTS placeholder narration clips (Andromeda, Cosmic Web, Galaxies, Helix, Orion, 6 moons) via `moon_vo.ps1` pattern | CS-006 | todo |
| CS-072 | TERM | Final voice for the 11 new clips (Higgsfield voice close to the original narrator; owner approves credits) | CS-071 | todo |
| CS-073 | CC | `MusicController` crossfade by environment mode (3 existing tracks) | CS-023 | todo |
| CS-074 | TERM | Keep original intro (logo → Earth placement → galaxy); add two one-time hint cards after placement; land in Milky Way with dock shown | CS-016, CS-032 | todo |
| CS-075 | TERM | Branding: logo, app icon, splash (Figma vector; Higgsfield hero art optional) | CS-010 | todo |
| CS-076 | CC | Narration-only mute and panel text-size setting | CS-027 | todo |

## Phase 7 — Device pass, performance, release

| ID | Track | Title | Depends | Status |
|---|---|---|---|---|
| CS-080 | TERM | Quest Link full-flow session with hands; tune reach, dock height, panel sizes | Phase 3–6 | todo |
| CS-081 | TERM | Standalone APK; on-device run; OVR Metrics ≥ 60 fps @ 72 Hz | CS-080 | todo |
| CS-082 | TERM | Performance pass (budgets: Technical Overview §7.4); profile Galaxies and Cosmic Web first | CS-081 | todo |
| CS-083 | TERM | Passthrough alpha audit of new shaders; dim quad ordering | CS-081 | todo |
| CS-084 | CC | Store readiness: privacy policy page, About links, description draft | — | todo |
| CS-085 | TERM | Regression: desktop + Quest matrices (GDD §5.3, Technical Overview §12) | CS-082 | todo |
