# Cosmic Simulation XR — Production Roadmap

*Version 1.0 — 11 September 2026. Branch `quest3-port` (pushed to `main`). Reference: the COSMIC XR promo video (86 s), broken down shot by shot on 11 Sep 2026.*

---

## 0. Executive summary

**Goal.** Turn the Galaxy Explorer / Quest 3 port into **Cosmic Simulation XR**: a hand-tracked, passthrough-first space explorer for Meta Quest 3 whose scenes, interactions, navigation and information design match the COSMIC XR reference one-to-one, while keeping the desktop mouse/keyboard mode we already have.

**Approach.** Reuse first. The project already contains a particle galaxy renderer, a full solar system with grab/pull/scale interaction, ten moon models, a black hole shader, a Sun with flares, 22 narration clips, 13 UI sounds, a passthrough/VR switch and a desktop mode. Most of the work is **re-composition and new glue**, not new content. New content is limited to four scenes' worth of assets (Andromeda, Galaxies, Cosmic Web, and 3D nebulae), the dock UI, and new written copy.

**What "one-to-one" means here.** Same scenes, same objects, same interactions, same navigation model, same information layout, same environment behaviour (dimmed room, black halo, full black). It does **not** mean copying their name, logo, artwork or written text — those are theirs. We write our own copy and use NASA/ESA imagery or our own generated assets.

**Shape of the work.** Eight phases, each independently shippable and verified in the editor before moving on. Phases 0–3 produce a playable app with the new navigation and the full solar system; 4–5 add the deep-space scenes; 6–7 are audio, onboarding, device tuning and store readiness.

---

## 1. Principles and constraints

| Principle | What it means in practice |
|---|---|
| **Reuse first** | Check the project → check public-domain sources (NASA, ESA, USGS) → generate with Higgsfield. New assets only when the first two fail. |
| **Less is more** | One data-driven `ExperienceModule` per scene instead of bespoke scene scripts. One panel prefab, one label prefab, one dock. |
| **Keep desktop** | Every hand interaction gets a mouse/keyboard equivalent. Desktop stays testable without a headset. |
| **Work in the original scenes** | Backups are copies under `Assets/scenes/_backup_original/`; the live scenes are the ones we edit. |
| **Derive, don't start over** | New scenes are created by duplicating the closest existing scene/prefab and changing content. |
| **Verify in the editor via MCP** | Every phase ends with play-mode checks (synthetic input + screenshots) before commit. |
| **One commit per task group** | Small, described commits on `quest3-port`; push to `main` on request. |
| **Legal hygiene** | Our own name (Cosmic Simulation XR), logo, copy. Imagery only from public-domain / CC sources or generated. Keep Microsoft's MIT notice for the inherited code. |

---

## 2. Toolchain and MCP usage

| Tool | Connected? | Used for |
|---|---|---|
| **Unity MCP** (relay, `Unity_RunCommand`, console, capture) | Yes | All scene/prefab/asset edits, play-mode tests, screenshots, builds. |
| **Figma MCP** (`get_design_context`, `use_figma`, `create_new_file`, `download_assets`) | Yes | UI source of truth: design tokens, dock, pop-ups, info panels, labels, desktop HUD, onboarding. Export SVG/PNG sprites and read specs back into Unity. |
| **Higgsfield — images** (`generate_image`, `upscale_image`, `remove_background`) | Yes | Textures and sprites we can't source: nebula layers, galaxy sprites, Cosmic Web reference, logo art, dock thumbnails when a render won't do. Equirectangular planet textures only if a NASA/USGS map is missing. |
| **Higgsfield — 3D Scene Builder** (hosted **Blender 5.2**, `scene_builder_3d_*`) | Yes | Any mesh we don't have: layered nebula card rigs, custom flare geometry, logo mesh. Exports GLB → imported into Unity. This is our "Blender MCP". |
| **Higgsfield — audio** (`generate_audio`, `create_voice`) | Yes (credits) | Optional: natural narration to replace TTS placeholders; new SFX. Ask before spending credits. |
| **Windows Speech (local TTS)** | Yes | Free placeholder narration (already used for the Moon). |
| **Web search / fetch, Chrome MCP** | Yes | Sourcing NASA/ESA/USGS public-domain imagery and fact-sheet data; checking licences. |
| **Git / GitHub** | Yes | Version control; `main` on github.com/JDMAXilius/GalaxyExplorer. |

**Asset sourcing order (applies to every asset):**
1. **In the project** (see §6 inventory).
2. **Public domain / open licence**: NASA (public domain), ESA/Hubble (CC BY 4.0), USGS Astrogeology (public domain), Solar System Scope textures (CC BY 4.0). Record the source URL and licence in `Assets/_sources/CREDITS.md`.
3. **Generate**: Higgsfield image → post-process (alpha, tiling, upscale) → import.

---

## 3. Target experience (the one-to-one spec)

### 3.1 Scenes (dock order)

| # | Scene | Environment | Content | Interactions |
|---|---|---|---|---|
| 1 | **Cosmic Web** | Full black | Purple glowing filament volume filling the room; title + 2-paragraph panel | Look around; grab/rotate/scale the volume |
| 2 | **Galaxies** | Black by default; passthrough toggle available | Hundreds of small galaxy sprites scattered through the room; panel | Look around; passthrough button |
| 3 | **Milky Way** | Dimmed room | Particle galaxy with labelled destinations: Helix Nebula, Crab Nebula, Solar System, Galactic Center – Sagittarius A\*, Homunculus Nebula, Orion Nebula; panel | Hover label → grows, turns blue; pinch → destination opens in front of you (nebula in black halo, or scene switch) |
| 4 | **Andromeda** | Dimmed room | Particle galaxy, white core, reddish dust; grows in from a point; panel | Grab/rotate/scale |
| 5 | **Solar System** | Dimmed room | Sun, eight planets + Pluto on teal orbit rings, name labels, Asteroid Belt label; panel with instructions | Two-hand tilt/rotate/scale/move; pop-up **Schematic / Realistic** |
| 6 | **Solar System Planets** | Dimmed room | **Solar Row**: Sun→Pluto, equal size (~15 cm), at chest height; **Relative Size** mode: true proportions | Pinch out a body → grows, panel + moons appear; two-hand scale to room size; release → stays put; pop-up **Solar Row / Relative Size** re-lays out |
| 7 | **Sagittarius A\*** | Full black + stars | Black hole with lensed accretion disk; panel | Hand ray pointing; walk around |

### 3.2 Bodies and moons

Sun · Mercury · Venus · Earth (+ **Moon**) · Mars · Jupiter (+ **Ganymede, Callisto**; Europa/Io available) · Saturn (+ **Titan, Mimas, Iapetus**; Enceladus available) · Uranus · Neptune · Pluto.
Moons float near their planet with a small name; a held moon shows a large bold name.

### 3.3 Information design

- **Body panel:** title, one paragraph, 2×2 grid — **Diameter / Mass / Orbital Period / Day Length**, big numbers, small unit labels, scientific notation for mass.
- **Scene panel:** title + 2–3 paragraphs, the last one an instruction ("grab with two hands to resize…").
- Panels float beside the object, face the viewer, no background plate, several may be open at once.
- **Labels:** small dark rounded tag with a leader line; hover grows/blues; pinch opens.

### 3.4 Interaction model (hands) and desktop equivalents

| Hand interaction | Desktop equivalent |
|---|---|
| Near pinch-grab and hold | Left-drag on object |
| Far pinch via hand ray | Click (pull in front of camera) |
| Two-hand scale / rotate | Wheel = scale, right-drag = rotate |
| Release → object stays where dropped | Same |
| Poke dock tile / pop-up button | Click dock (screen-space HUD version) |
| Hover label → pinch | Hover → click |
| Passthrough toggle | Same button + key `P` |
| Move the dock by its grab bar | Drag HUD dock / `Tab` toggles |
| Restore layout ("Solar Row") | Same + `R` |

### 3.5 Environment behaviour

| Mode | Implementation |
|---|---|
| Passthrough (room) | Camera clear alpha 0 + AR camera (exists) |
| **Dimmed room** | Translucent black quad on the near plane (alpha ≈ 0.5); fades in/out |
| **Black halo** | Radial-gradient black card behind the object, sized to it |
| **Full black** | VR mode + star background (exists) |

---

## 4. Architecture

### 4.1 Runtime modules (new or changed)

```
Assets/scripts/experience/
  ExperienceModule.cs        ScriptableObject: id, display name, dock tile, panel copy,
                             environment mode, content prefab, layout presets, VO clip
  ExperienceDirector.cs      Loads/unloads the active module (additive scene or prefab),
                             drives EnvironmentController, publishes OnExperienceChanged
  EnvironmentController.cs   Passthrough / Dimmed / BlackHalo / FullBlack, fades
  LayoutPreset.cs            Named arrangements (SolarRow, RelativeSize, Schematic, Realistic)

Assets/scripts/interaction/
  FreePlacementSolver.cs     ForceSolver subclass: no snap-back; "restore layout" API
  LabelButton.cs             Hover grow/blue, pinch/click → opens a target module or object
  TwoHandTransformer.cs      Wrapper on ManipulationHandler tuned for room-scale (5 cm–3 m)

Assets/scripts/ui/
  DockController.cs          Tiles from ExperienceModule list, poke + mouse, grab bar,
                             passthrough toggle, pop-up host
  DockPopup.cs               Two-option pop-up bound to a module's LayoutPresets
  InfoPanel.cs               Evolves PlanetInfoCard: body variant (4 stats) & scene variant
  MoonLabel.cs               Small/large name label states

Assets/scripts/desktop/
  DesktopDock.cs             Screen-space mirror of the dock (replaces planet bar)
  DesktopMouseInput.cs       (exists) + free placement, label clicks, P key
```

### 4.2 What stays

`ForceSolver` / `PlanetForceSolver` / `MoonForceSolver`, `ManipulationHandler`, `GEInteractable` routing, `XRInputRig`, `ExperienceModeManager`, `DrawStars` galaxy renderer, `OrbitalTrail`, `AudioService`, `VOManager`, `PlanetInfoCard` (renamed/extended), `DesktopMouseInput`, `PlayFromViewScene`.

### 4.3 What is retired or demoted

- Drill-down navigation (`TransitionManager` zoom in/out, Back button) → kept as an internal transition helper; user navigation goes through the dock.
- POI "card" textures (`poi_text_card_*`) → replaced by `InfoPanel` copy from `ExperienceModule`.
- Planet preview bar → replaced by `DesktopDock`.

### 4.4 Scene strategy

- `main_scene` + `core_systems_scene` stay the bootstrap.
- Each dock entry maps to an additive scene: **existing** `galaxy_view_scene` (Milky Way), `solar_system_view_scene` (Solar System), `galactic_center_view_scene` (Sagittarius A\*); **new, duplicated from existing**: `solar_row_scene` (from solar_system_view_scene), `andromeda_scene` (from galaxy_view_scene), `galaxies_scene`, `cosmic_web_scene` (from galactic_center_view_scene as the "black" template).
- Backups: `Assets/scenes/_backup_original/` copies of `main_scene`, `core_systems_scene` and the three view scenes, made once in Phase 0 and never edited.

---

## 5. Phases

Effort sizes: **S** ≤ half a day, **M** 1–2 days, **L** 3–5 days of focused work. Each phase lists goals → tasks → atomic tasks, assets, tools, and its definition of done.

### Phase 0 — Foundation and rebrand (S–M)

**Goal:** the project is named, backed up, documented and ready for parallel UI and content work.

| ID | Task | Atomic steps | Tool |
|---|---|---|---|
| P0-T1 | Back up scenes | Copy `main_scene`, `core_systems_scene`, `galaxy_view_scene`, `solar_system_view_scene`, `galactic_center_view_scene` (+ `.meta`) to `Assets/scenes/_backup_original/`; exclude folder from build settings; commit | Unity MCP / git |
| P0-T2 | Rebrand | Product name "Cosmic Simulation XR"; decide bundle id (`com.jdmaxilius.cosmicsimulationxr`); About text; window title; placeholder logo | Unity MCP |
| P0-T3 | Repo hygiene | `docs/` (this roadmap, decisions log), `Assets/_sources/CREDITS.md`, `.gitattributes` for LF/CRLF, commit the two stray settings files | git |
| P0-T4 | Figma workspace | Create file "Cosmic Simulation XR — UI"; pages: Tokens, Dock, Pop-ups, Panels, Labels, Desktop HUD, Onboarding | Figma MCP |
| P0-T5 | Asset sourcing kick-off | Download and licence-log: Solar System Scope / NASA planet maps (only where ours are weak), Hubble Helix, Orion, Crab, Homunculus; Hubble deep-field galaxy cutouts | Web / Chrome |
| P0-T6 | Copy deck | `docs/copy/` markdown: our own paragraphs for 7 scenes, 10 bodies, 6 moons, 6 nebulae; numbers from NASA fact sheets | — |

**Done when:** backups exist and are excluded from build; app boots under the new name; Figma file exists; CREDITS.md has every downloaded asset.

---

### Phase 1 — Design system and UI in Figma (M)

**Goal:** every UI surface is designed once in Figma and exported in a form Unity consumes directly.

| ID | Task | Atomic steps |
|---|---|---|
| P1-T1 | Tokens | Colours (white text, cyan accent `#6CCFDD`, tag dark `#0E1418@80%`), type scale (Selawik: title 24, subtitle 8 caps, stat 12–14, label 6.5, body 9.5), spacing 4-pt grid, corner radii, shadows |
| P1-T2 | Dock | Curved 7-tile strip (tile 96×64 with thumbnail + name + subtitle), grab bar, passthrough toggle, three utility buttons (Recenter, Mute, Help), hover/pressed states |
| P1-T3 | Pop-ups | Two-option card (Schematic/Realistic, Solar Row/Relative Size) anchored above a tile; small utility window (scale slider + close) |
| P1-T4 | Info panels | Body variant (title, paragraph, 2×2 stats); Scene variant (title + paragraphs + instruction); width 230 units at 0.7 mm/unit |
| P1-T5 | Labels | Destination tag (idle / hover / selected), moon label small & large, leader line |
| P1-T6 | Desktop HUD | Screen-space dock, controls overlay, About panel with backing plate |
| P1-T7 | Onboarding | 3-step first-run cards (pinch to grab, two hands to scale, poke the dock) |
| P1-T8 | Export | SVG/PNG at 2×; TMP style sheet; write `docs/ui/spec.md` (sizes in mm and canvas units) |

**Tools:** Figma MCP (`use_figma` to build, `get_design_context` / `download_assets` to export). Icons drawn as vectors; thumbnails placeholder until Phase 2 renders them.
**Done when:** all frames exist, exports are in `Assets/ui/figma/`, and spec.md maps each frame to a Unity prefab.

---

### Phase 2 — Core architecture: modules, dock, panels, placement (L)

**Goal:** the new navigation and interaction backbone works with the three existing scenes on desktop and (via Link) on Quest.

| ID | Task | Atomic steps |
|---|---|---|
| P2-T1 | `ExperienceModule` + assets | ScriptableObject; create 7 assets with ids, names, environment mode, scene name, copy from the deck |
| P2-T2 | `ExperienceDirector` | Switch modules (unload current additive scene, load next), fire events, remember last module; keep `ViewLoader` underneath |
| P2-T3 | `EnvironmentController` | Dim quad (near-plane, alpha fade), black-halo card prefab, FullBlack via `ExperienceModeManager`; per-module setting; desktop preview equivalent |
| P2-T4 | Dock (XR) | World-space canvas prefab from Figma export; tiles bound to modules; XRI poke (GEButton/XRPokeFilter, exists); grab bar via ManipulationHandler; passthrough toggle; utility buttons; appear/hide sounds (exist) |
| P2-T5 | Dock pop-ups | `DockPopup` with two `LayoutPreset` buttons; utility window (scale slider) |
| P2-T6 | Dock thumbnails | Editor script renders each module's content to 256×160 PNGs |
| P2-T7 | `InfoPanel` v2 | Body & scene variants from Figma; content from module/body data; side-switching and constant-size logic reused from `PlanetInfoCard`; multiple open at once |
| P2-T8 | `FreePlacementSolver` | Released objects stay; `RestoreLayout()` animates back to preset; integrates with `ForceSolverFocusManager` (allow many out at once) |
| P2-T9 | `TwoHandTransformer` | Scale range 0.05–3 m; rotation with two hands; smoothing tuned |
| P2-T10 | `LabelButton` | Hover grow + cyan, pinch/click opens target; leader line; sounds |
| P2-T11 | Desktop parity | `DesktopDock` (screen-space), label clicks, `P` passthrough, free placement with mouse, help overlay update |
| P2-T12 | Migrate existing scenes | Galaxy/solar/galactic-center scenes register as modules; old Back/zoom hidden from UI |
| P2-T13 | Tests | MCP play-mode script: switch all modules, open panels, place/restore objects, screenshots; console clean |

**Assets:** dock sprites (P1), thumbnails (P2-T6), dim quad material (new, trivial), halo card (new gradient texture, Higgsfield or procedural).
**Done when:** you can travel between the three existing scenes through the dock, panels show our copy, objects stay where dropped, environment modes switch, all on desktop and in play mode.

---

### Phase 3 — Solar system one-to-one (L)

**Goal:** the "Solar System" and "Solar System Planets" scenes match the reference, including every moon and panel.

| ID | Task | Atomic steps |
|---|---|---|
| P3-T1 | `solar_row_scene` | Duplicate `solar_system_view_scene`; `LayoutPreset` **SolarRow** (equal 15 cm bodies in a line at 1.2 m height, 25 cm apart) and **RelativeSize** (true ratios, Sun capped at 2 m, spacing by radius) |
| P3-T2 | Pop-up wiring | Solar Row / Relative Size re-lays out with tween; restore also on `R` |
| P3-T3 | Body panels | 4-stat data for Sun, 8 planets, Pluto from the copy deck; panel appears on pull |
| P3-T4 | Moons | Wire Ganymede, Callisto, Titan, Mimas, Iapetus (+ Europa, Io, Enceladus, Phobos, Deimos optional) using the `MoonForceSolver` pattern; orbit anchors; `MoonLabel` small/large; moon panels (name + 2 facts) |
| P3-T5 | Schematic / Realistic | Reuse the existing realistic-orbit toggle (`OrbitScalePointOfInterest`) behind the pop-up; add planet name labels and Asteroid Belt label |
| P3-T6 | Sun | Keep flare shaders; add touch response (brightness pulse via `_LightAmount` when a hand enters the collider) |
| P3-T7 | Earth | Atmosphere rim: reuse `halo_shader`; cloud layer exists |
| P3-T8 | Two-hand room-scale | Tune `TwoHandTransformer` so Saturn's rings can wrap around the user |
| P3-T9 | Desktop | Number keys map to Solar Row order; wheel scale limits per body (done); click moons |
| P3-T10 | Tests | Pull each body, scale, drop, restore; moons appear; panels correct; screenshots |

**Assets:** all meshes/textures exist. Optional upgrades if quality is short: 4k USGS Moon map, NASA Blue Marble Earth. Moon textures for Jupiter/Saturn moons exist (atlases).
**Done when:** the planet row, relative size, all moons and panels work on desktop and in play mode; Quest Link check of two-hand scaling.

---

### Phase 4 — Milky Way destinations, nebulae, black hole (L)

**Goal:** Milky Way labels open 3D nebulae in a black halo; Sagittarius A\* is a full-black scene.

| ID | Task | Atomic steps |
|---|---|---|
| P4-T1 | Labels | Replace POI markers with `LabelButton` tags: Helix, Crab, Solar System, Galactic Center, Homunculus, Orion (keep Pillars, NGC 1501, Trumpler 14 as extras) |
| P4-T2 | Nebula prefab | Layered-card nebula: 4–6 alpha planes with parallax + slow rotation (built in Higgsfield Scene Builder/Blender → GLB, or procedurally in Unity); grows from a point; sits in a black-halo card |
| P4-T3 | Nebula assets | Crab, Homunculus (project textures → cut into layers); Helix, Orion (Hubble/ESA images → layers via Higgsfield `remove_background` + generate depth layers) |
| P4-T4 | Sagittarius A\* scene | Existing black-hole material; FullBlack environment + star background; hand-ray pointing; panel; grow-in |
| P4-T5 | Milky Way panel and grow-in | Scene panel; galaxy appears from a point |
| P4-T6 | Desktop | Label hover/click with mouse |
| P4-T7 | Tests | Each label opens its nebula; halo sizes; black hole scene; screenshots |

**Assets:** nebula layer textures (2 exist, 2 sourced), halo gradient (P2). Generation budget: ~8 Higgsfield images.
**Done when:** six destinations open; nebulae look volumetric from a walk-around; black hole scene is full black.

---

### Phase 5 — Andromeda, Galaxies, Cosmic Web (L)

**Goal:** the three new deep-space scenes exist and match the reference feel.

| ID | Task | Atomic steps |
|---|---|---|
| P5-T1 | `andromeda_scene` | Duplicate galaxy scene; new `DrawStars` parameter set (white core, warm dust ring, flatter disk, tilt); grow-in; panel; grab/scale |
| P5-T2 | `galaxies_scene` | Billboard scatter system (300–500 sprites, 20+ unique galaxy cutouts, random size/tint/rotation) in a 6 m radius; slow drift; panel; passthrough toggle allowed |
| P5-T3 | Galaxy sprites | Hubble deep-field cutouts (public domain) → alpha → atlas; fill gaps with Higgsfield |
| P5-T4 | `cosmic_web_scene` | Procedural filament point cloud (editor script: 3D Voronoi edges + noise, ~200k points) rendered with the star point-sprite shader in purple palette; FullBlack; panel |
| P5-T5 | Dock thumbnails | Render new scenes |
| P5-T6 | Tests | Load each; performance sample on desktop (frame time), then Quest |

**Assets:** galaxy atlas (sourced/generated), none for Andromeda/Cosmic Web (procedural).
**Done when:** all seven dock tiles work end to end.

---

### Phase 6 — Audio, narration, onboarding, branding (M)

| ID | Task | Atomic steps |
|---|---|---|
| P6-T1 | SFX map | Assign existing UI clips to dock, pop-up, label, grab/release, scene switch (transition clips exist) |
| P6-T2 | Narration | Existing VO for 10 bodies + 7 destinations; new clips for Andromeda, Cosmic Web, Galaxies, Helix, Orion, 6 moons: TTS placeholders now; Higgsfield voice on approval |
| P6-T3 | Music | Keep the three existing tracks; loop per environment |
| P6-T4 | Onboarding | First-run 3 cards from Figma; skip after first session (PlayerPrefs) |
| P6-T5 | Branding | Logo (Figma vector, optional Higgsfield hero art), app icon, splash, About |
| P6-T6 | Accessibility | Mute (done), narration toggle, subtitle option for panels |

---

### Phase 7 — Device pass, performance, release (M–L)

| ID | Task | Atomic steps |
|---|---|---|
| P7-T1 | Quest Link session | Full flow with hands; fix reach distances, dock height, panel sizes |
| P7-T2 | Standalone APK | Build; on-device run; OVR Metrics ≥ 60 fps at 72 Hz |
| P7-T3 | Performance | Budgets: ≤ 150 draw calls, ≤ 400k particles total, textures ≤ 2k ASTC, one dim quad; profile Galaxies and Cosmic Web first |
| P7-T4 | Passthrough polish | Alpha audit of new shaders; dim quad ordering |
| P7-T5 | Store readiness | Privacy policy URL, About links to our repo, screenshots, description |
| P7-T6 | Regression | Desktop matrix + Quest matrix (see §8) |

---

## 6. Asset inventory: have vs. need

### 6.1 Meshes

| Need | Have | Action |
|---|---|---|
| Sun, 8 planets, Pluto (+ LOD1) | Yes (`Assets/models`) | Reuse |
| Moon, Ganymede, Callisto, Titan, Mimas, Iapetus | Yes | Reuse; add Europa, Io, Enceladus, Phobos, Deimos (have) |
| Saturn/Uranus rings | Yes (in prefabs) | Reuse |
| Sun flares (3 models) | Yes | Reuse |
| Black-hole glow card | Yes | Reuse |
| Nebula layered card rig | No | Blender (Higgsfield Scene Builder) or procedural quads |
| Dock/pop-up geometry | No | uGUI world-space (no mesh needed) |
| Halo card | No | Unity quad |

### 6.2 Textures

| Need | Have | Action |
|---|---|---|
| Planet diffuse/normal/specular for all bodies | Yes | Reuse; optional NASA/USGS upgrades for Moon and Earth |
| Moon atlases (Jupiter, Saturn, Mars) | Yes | Reuse |
| Crab, Homunculus nebula | Yes (2D) | Cut into layers |
| Helix, Orion nebula | No | ESA/Hubble (CC BY) → layers |
| Pillars, NGC 1501, Trumpler | Yes | Keep as extra destinations |
| Galaxy sprite atlas (20+) | No | Hubble deep field (PD) + Higgsfield |
| Andromeda | No texture needed | Procedural particles |
| Cosmic Web | No texture needed | Procedural point cloud |
| Halo radial gradient | No | Procedural (script) |
| Dock thumbnails (7) | No | Editor renders |
| UI icons | 4 new + 3 old | Figma vectors |
| Logo / app icon | No | Figma; hero art via Higgsfield |

### 6.3 Shaders and materials

Have: planet (standard, Earth, Saturn, clouds, rings), sun + flares + lens flare, black hole (ray-marched), galaxy point sprites (3 layers), halo, orbital trail, transparent-with-transition, UI. **Need:** dim quad (unlit alpha, trivial), nebula layer (unlit alpha + soft depth fade, small), galaxy billboard (unlit alpha instanced, small). All new shaders must carry the stereo macros and Quest-safe pragmas established in Phase 5 of the port.

### 6.4 Audio

Have: 22 destination VO (all bodies + galaxy destinations), 16 intro VO, 13 UI, 11 transitions, 12 ambiences, 3 music. **Need:** VO for Andromeda, Cosmic Web, Galaxies, Helix, Orion, and 6 moons (11 clips) — TTS first; optional dock/pop-up clicks (can reuse `ui_select`).

### 6.5 Fonts and UI

Have: Selawik SDF family, Segoe UI SDF, TMP set up. **Need:** Figma exports (dock, pop-ups, panels, labels, HUD, onboarding).

### 6.6 Copy (text)

All new, ours: 7 scene panels (2–3 paragraphs), 10 body paragraphs + 4 stats each, 6 moon blurbs, 6 nebula blurbs, 3 onboarding cards, About. Numbers from NASA planetary fact sheets. Lives in `docs/copy/` and is imported into `ExperienceModule` assets.

---

## 7. Data model for content (so copy is not hard-coded)

```
ExperienceModule (SO)
  id, displayName, subtitle, dockThumbnail, sceneName
  environment: Passthrough | Dimmed | BlackHalo | FullBlack
  panel: ScenePanelCopy { title, paragraphs[] }
  layouts: LayoutPreset[]  (name, arrangement)
  narration: AudioClip

BodyInfo (SO, one per body/moon)
  displayName, paragraph
  stats: Diameter, Mass, OrbitalPeriod, DayLength (value, unit, exponent?)
  narration: AudioClip
  moons: BodyInfo[]
```

Editor import script: `docs/copy/*.md` → SO assets (so writers edit markdown, not Unity).

---

## 8. Verification matrix

| Check | Desktop | Quest Link | Quest APK |
|---|---|---|---|
| Dock switches all 7 scenes | P2 | P2 | P7 |
| Pop-ups relayout (Row/Relative, Schematic/Realistic) | P3 | P3 | P7 |
| Pull/grab/scale/drop/restore each body | P3 | P3 | P7 |
| Moons appear, grab, labels | P3 | P3 | P7 |
| Panels correct copy and numbers | P3 | — | P7 |
| Labels open 6 destinations | P4 | P4 | P7 |
| Environment modes (dim/halo/black) | P2 | P2 | P7 |
| Passthrough toggle | P2 | P2 | P7 |
| New scenes load; frame time | P5 | P5 | P7 |
| Audio map, narration replay | P6 | P6 | P7 |
| Onboarding first run only | P6 | P6 | P7 |
| ≥ 60 fps @ 72 Hz | — | — | P7 |

Automation: the existing MCP harness (synthetic mouse/keyboard, screenshots, console filter) extended with a `smoke.cs` that walks every module and body.

---

## 9. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Passthrough dimming not exposed by the OpenXR Meta package | Near-plane translucent quad (composited in-app); verify on device early (P2) |
| Galaxies/Cosmic Web too heavy for Quest | Instanced billboards, point sprites, LOD by distance; budgets in P7; test on device in P5 |
| Nebulae look flat | Layered parallax cards + slow rotation + halo; walk-around test |
| Free placement causes clutter | "Solar Row" restore, `R` key, and per-scene reset on switch |
| Copy/asset licensing | CREDITS.md, PD/CC sources only, our own text |
| Higgsfield credits | Ask before each generation batch; prefer sourced imagery |
| Regressions in desktop mode | Smoke test runs every phase |

---

## 10. Open decisions (need your call)

1. **Bundle id:** keep `com.jdmaxilius.galaxyexplorer` (installed builds update in place) or switch to `com.jdmaxilius.cosmicsimulationxr` (clean identity)?
2. **Extra destinations:** keep Pillars, NGC 1501 and Trumpler 14 in the Milky Way (we have them) or hide them for strict one-to-one?
3. **Narration:** TTS placeholders only, or spend Higgsfield credits on a natural voice for all ~33 clips?
4. **Onboarding:** minimal 3 cards (recommended) or keep the original Earth-placement intro?
5. **Order of Phases 4 and 5:** nebulae first (more reuse) or new scenes first (more visible progress)?

---

## 11. Immediate next steps (Phase 0, on approval)

1. Back up the five scenes → commit.
2. Rebrand product name, About, placeholder logo → commit.
3. Create the Figma file and Tokens page.
4. Start the copy deck and CREDITS.md.
5. Source Helix/Orion/deep-field imagery and log licences.
