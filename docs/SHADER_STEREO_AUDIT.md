# Shader stereo audit — every `.shader` in the project

*Written 12 Sep 2026, in a session with **no Unity editor**. Nothing in this document has been compiled.
The fixes described in §4 are mechanical and were read back as a compiler would; they are still unverified.
CS-095 (§6) is the ticket that verifies them.*

---

## 1. What the macros are for, and which render mode actually ships

Under a stereo render mode that draws both eyes in one pass, a shader learns which eye it is drawing
only through four macros:

| Macro | Where | What it does |
|---|---|---|
| `UNITY_VERTEX_INPUT_INSTANCE_ID` | `appdata` | carries the instance/view id into the vertex stage |
| `UNITY_SETUP_INSTANCE_ID(v)` | first line of `vert` | sets `unity_StereoEyeIndex`, which is what indexes `unity_StereoMatrices` and, through them, `unity_ObjectToWorld`, `UNITY_MATRIX_VP` and `_WorldSpaceCameraPos` |
| `UNITY_VERTEX_OUTPUT_STEREO` | `v2f` | reserves `SV_RenderTargetArrayIndex` (instanced) / a blend index (multiview) so the fragment lands in the right slice |
| `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)` | `vert`, after the setup | writes that slice index |
| `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` | first line of `frag` | only needed if the **fragment** stage reads anything eye-dependent |

Without them, a shader compiled with `STEREO_INSTANCING_ON` silently draws **both eyes with eye 0's
matrices** — every object sits at the left eye's position, with no stereo disparity and no depth.

**The render mode in this repo is the opposite of what `CLAUDE.md` says.** `Assets/XR/Settings/OpenXR Package Settings.asset`:

| Build target | `m_renderMode` | Reading |
|---|---|---|
| Android (Quest standalone) | `1` | **Multi Pass** |
| Standalone (Windows / Quest Link) | `0` | **Single Pass Instanced** |

(`OpenXRSettings.RenderMode` is `SinglePassInstanced = 0`, `MultiPass = 1`. Confirm this in the editor —
it is the one fact in this document that could not be checked against the package source, which is not
vendored in the repo. §6 covers it.)

`CLAUDE.md` §Build says "Android multiview; Windows/Link multi-pass", which is backwards. That single
inversion explains why a bug this large went unnoticed: **on-device Quest builds are multi-pass**, where
each eye gets its own draw and the missing macros cost nothing; the breakage is on **Link**, which is
where the least testing happens, and desktop mono, where it cannot appear at all.

Two consequences worth being loud about:

- Link sessions have been rendering ~33 of the project's shaders mono-on-both-eyes. Anyone who said
  "the planets look flat on Link" was right.
- The moment someone flips Android to Single Pass Instanced — which is the standard Quest performance
  win, and what `CLAUDE.md` already claims is in force — the shipping build breaks the same way.
  **Fixing the shaders is a prerequisite for that change, not a follow-up to it.**

---

## 2. Scope: what is audited and what is excluded

78 `.shader` files exist under `Assets/`. **31 are third-party** and are excluded from the fix scope
(they are still listed in §5 for completeness):

| Location | Count | Why excluded |
|---|---|---|
| `Assets/TextMesh Pro/Shaders/` | 18 | Vendored package. The SDF variants we actually use already carry the macros; upgrading TMP is the correct fix path, not editing them. |
| `Assets/Samples/XR Interaction Toolkit/3.6.0/` | 5 | Imported XRI samples. Not referenced by any shipping scene except through sample materials. |
| `Assets/external/TouchScript/` | 7 | Vendored TouchScript, examples and debug shaders. Legacy touchscreen support; none reachable from a build scene. |
| `Assets/third_party/mrtk_assets/.../MixedRealityStandard.shader` | 1 | Vendored MRTK. Already has the macros. |

That leaves **47 project shaders in `Assets/shaders/`** — the audit population.
(The Technical Overview §16 says "43"; it is out of date by four.)

**Reachability** was computed as a transitive closure over asset GUIDs from the six scenes in
`EditorBuildSettings.asset` → prefabs → materials → shaders, plus a manual pass over the three shaders
that are only ever bound at run time or by an editor builder (`Shader.Find` / `AssetDatabase` path load),
which a GUID graph cannot see.

---

## 3. Headline numbers

| | Count |
|---|---|
| `.shader` files under `Assets/` | 78 |
| Third-party / vendored (excluded) | 31 |
| **Project shaders in `Assets/shaders/`** | **47** |
| Had all four stereo macros before this session | 11 |
| — of those, also had `#pragma multi_compile_instancing` | 7 |
| **Missing stereo macros entirely before this session** | **36** |
| — of those, **reachable** from a build scene | **33** |
| — of those, unreachable (dead) | 3 |
| **Fixed in this session** | **25** |
| Still broken and reachable, deliberately not touched | **8** |
| Still broken and unreachable, deliberately not touched | 3 |

The "roughly 36 of ~53" estimate that started this work was right about the broken count and wrong
about the population: 36 of **47**, not of 53.

---

## 4. The table

Legend: **IN** = `UNITY_VERTEX_INPUT_INSTANCE_ID` in appdata · **OUT** = `UNITY_VERTEX_OUTPUT_STEREO` in v2f ·
**SET** = `UNITY_SETUP_INSTANCE_ID` · **INIT** = `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` ·
**EYE** = `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` in frag · **MCI** = `#pragma multi_compile_instancing` ·
**SS** = samples a screen-space texture or reads an eye-dependent uniform in the fragment stage.
State is **after** this session's edits; the "was" column records what it looked like before.

### 4a. Broken and reachable — **still broken**, needs a compiler or a human (8)

| Shader | Reached by | IN/OUT/SET/INIT | EYE | MCI | target | SS | Why not fixed here |
|---|---|---|---|---|---|---|---|
| `black_hole_gravitational_lensing_disc_optimized_shader.shader` | `poi_sagittarius_a_prefab` → galactic centre view | ✗✗✗✗ | ✗ | ✗ | — | **`_WorldSpaceCameraPos` in `frag`** | The ray-march reads the camera position **per pixel**. It needs `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` in `frag` as well as the vertex macros, and the whole disc is a view-dependent integral — the one shader here where a wrong macro produces a plausible-but-wrong image rather than an obviously broken one. Highest-priority human fix. |
| `spiral_stars_shader.shader` | `milky_way_prefab` (galaxy view) | ✗✗✗✗ | ✗ | ✗ | 4.5 | — | `StructuredBuffer` + `CommandBuffer.DrawProcedural`, quads expanded from `SV_VertexID/6` in `cginc/StarQuad.cginc`. Procedural draws have their own instancing story (there is no `appdata` to hang an instance id on) and `cosmic_web_points_shader` — which shares this exact path and *is* correct — is being edited by another agent right now. Fix all four together, once. |
| `spiral_stars_negative_shader.shader` | `milky_way_prefab` | ✗✗✗✗ | ✗ | ✗ | 4.5 | — | as above |
| `spiral_stars_cloud_shader.shader` | `milky_way_prefab` | ✗✗✗✗ | ✗ | ✗ | 4.5 | — | as above |
| `orbital_trail_shader.shader` | all 10 `poi_<planet>_prefab` | ✗✗✗✗ | ✗ | ✗ | 4.5 | — | `StructuredBuffer`; drawn from an `OrbitalTrail` `CommandBuffer` at `AfterForwardAlpha`. Same class of problem as the star renderers. |
| `screen_compose_shader.shader` | `milky_way_prefab` | ✗✗✗✗ | ✗ | ✗ | — | — | **Four passes**, each doing something different, in the galaxy render-to-texture compose chain. Technical Overview §7.1 says the down-scaled RT path is *skipped on XR* and layers draw straight to the eye buffer — so whether this shader runs in stereo at all is a question about `DrawStars`, not about the shader. Answer that first. |
| `screen_clear_shader.shader` | `milky_way_prefab` | ✗✗✗✗ | ✗ | ✗ | — | — | Same chain; a full-screen `ZTest Always` clear with `o.vertex.z = 1`. A screen-aligned clear has no stereo parallax to get wrong, so this is cosmetic unless the RT path is revived. |
| `occluder_shader.shader` | `core_systems_scene` | ✗✗✗✗ | ✗ | ✗ | — | — | Its `v2f` declares `float4 vertex : POSITION`, not `SV_POSITION`. Adding `UNITY_VERTEX_OUTPUT_STEREO` next to a non-`SV_POSITION` output is exactly the edit I cannot verify without a compiler. It is also a full-screen black `Queue=Overlay, ZTest Always` fade quad, which has no parallax to lose. Change the semantic and add the macros in one editor-verified step. |

### 4b. Broken and reachable — **fixed in this session** (25)

All 25 received the identical four-macro treatment modelled on `sun_shader.shader` and
`planet_atmosphere_rim_shader.shader`. No `#pragma target` was changed, no `#pragma multi_compile_instancing`
was added (see §4e), no fragment stage was touched — none of these 25 reads anything eye-dependent per pixel.

| Shader | Reached by | was | now IN/OUT/SET/INIT | MCI | target | Notes |
|---|---|---|---|---|---|---|
| `planet_shader.shader` | 8 planet + 3 moon-set materials, every `poi_*` prefab and `solar_system_planets_content_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | The single most-rendered shader in the app. |
| `planet_earth_shader.shader` | `earth_material` — solar system view **and** the intro placement scene | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `planet_saturn_shader.shader` | `saturn_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | **`v2f o;` hoisted** above `float center = mul(unity_ObjectToWorld, …)`. Commented in the file. |
| `planet_alpha_shader.shader` | 6 `jupiter_planet_cloud_*` materials | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `planet_clouds_shader.shader` | `earth_clouds_material`, `venus_clouds_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `planet_clouds_diffuse_shader.shader` | `jupiter_clouds_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `planet_low_res_shader.shader` | `solar_system_lod_material` — the LOD1 mesh of all 10 bodies | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `planet_rings_shader.shader` | `saturn_rings_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | **`v2f o;` hoisted**; the old first line read `unity_ObjectToWorld` twice. Commented. |
| `planet_rings_basic_shader.shader` | `uranus_rings_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `halo_shader.shader` | 6 `*_glow_material` (Earth, Jupiter, Mars, Saturn, Uranus, Venus) | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `sun_additive_with_fresnel_alpha_shader.shader` | `sun_alpha_material` — Sun, S2, S102 | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `sun_solar_flare_shader.shader` | `sun_flare_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `glow_card_shader.shader` | `sun_glow_material` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | Does a **per-pixel** near-clip (`CalcPixelFromCamera`) off an interpolated world position, not off an eye-dependent uniform — so no `EYE` macro is required. |
| `lens_flare_shader.shader` | `sun_lens_flare_material` — `poi_s2`, `poi_s102` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | **`v2f o;` hoisted** for consistency; nothing eye-dependent was read early here. Commented. |
| `asteroid_ring_shader.shader` | `asteroid_material` → `solar_system_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | **`v2f o;` hoisted** above `mul(unity_ObjectToWorld, v.vertex)`. Commented. |
| `galaxy_plane_shader.shader` | `galaxy_center_plane_material` → `milky_way_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | |
| `poi_sdf_shader.shader` | 15 `poi_text_card_*` materials — every POI card in the app | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `poi_transparent_shader.shader` | 5 `poi_marker_*` materials — every POI pin, leader line and toggle | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | Highest instance count of anything here. |
| `poi_porthole_shader.shader` | `poi_magic_window_material` → `galaxy_pois_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | Reads `unity_WorldToObject` in `vert`; the setup now precedes it. |
| `boundry_space_background_stars_shader.shader` | `boundry_space_background_stars_prefab` → `core_systems_scene` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | Surrounds the player in every experience. |
| `UI_Shader.shader` | 5 `ui_button_card_*` materials → `menu_managers.prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `transparent_with_transition_alpha_shader.shader` | `about_material` → `about_slate_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `intro_logo_symbol_shader.shader` | `intro_logo_text_material` → `into_logo_prefab` → `core_systems_scene` | ✗✗✗✗ | ✓✓✓✓ | ✗ | 4.5 | Cubemap reflection is fed by an interpolated world position, so no `EYE` macro needed. |
| `sagittarius_a_black_hole_glow_card_shader.shader` | `sagittarius_black_hole_glow_card_material` → `poi_sagittarius_a_prefab` | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | |
| `occlusion_shader.shader` | `occlusion_material` → intro placement scene | ✗✗✗✗ | ✓✓✓✓ | ✗ | — | `ColorMask 0`, depth-only. Writes nothing visible but **does** write depth — with the left eye's matrices it was punching a hole in the wrong place in the right eye. |

### 4c. Broken but **unreachable** — deliberately left alone (3)

No material of theirs is referenced by any prefab or scene, and no script names them.

| Shader | Only material | Status |
|---|---|---|
| `grid_shader.shader` | `grid_material` → `boundry_space_floor_prefab` | The prefab is referenced by **nothing** — no scene, no prefab. Dead. |
| `intro_earth_placement_shader.shader` | `intro_earth_placement_material` | Material has zero consumers. Superseded by `intro_shaders/intro_placement_shaders/*`. |
| `poi_occlusion_shader.shader` | `galaxy_magic_window_occlusion_material` | Material has zero consumers. |

Deleting these three is a separate, better ticket than fixing them.

### 4d. Already correct — all four macros **and** `multi_compile_instancing` (7)

| Shader | Reached by | EYE | target |
|---|---|---|---|
| `sun_shader.shader` | `sun_material` | ✗ (not needed) | 4.5 |
| `planet_atmosphere_rim_shader.shader` | `earth_atmosphere_material`, built by `AtmosphereMaterialBuilder` at run time | ✗ (not needed) | 4.5 |
| `nebula_card_shader.shader` | 28 `nebulae/nebula_*_card_*` materials | **✓** | 3.5 |
| `cosmic_web_points_shader.shader` | run-time material, `CosmicWebRenderer` | **✓** | 4.5 |
| `environment_tint_shader.shader` | run-time, `EnvironmentController` via `Shader.Find` | ✗ (not needed) | 3.5 |
| `force_pull_shaders/tractor_beam_shader.shader` | `tractor_beam_line_material` → `tractor_beam_prefab`, on every POI | ✗ (not needed) | 4.5 |
| `intro_shaders/intro_placement_shaders/intro_placement_object_shader.shader` | intro placement scene | ✗ (not needed) | 3.5 |

`nebula_card_shader` is the reference to copy for anything that samples `_CameraDepthTexture`: it has
`UNITY_SETUP_INSTANCE_ID(i)` **and** `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)` as the first two lines
of `frag`, which is what makes the depth fetch land in the right eye's slice.

### 4e. All four macros, **no** `#pragma multi_compile_instancing` — left alone on purpose (4)

| Shader | Reached by | target |
|---|---|---|
| `ring_shader.shader` | `planet_highlight_ring_material` → `planet_highlighter_prefab` → all 13 POI prefabs | 4.5 |
| `intro_shaders/placement_ring_shader.shader` | `placement_ring_prefab` → intro scene | 4.5 |
| `intro_shaders/intro_placement_shaders/intro_placement_object_rim_shader.shader` | intro scene | 4.5 |
| `intro_shaders/intro_placement_shaders/intro_placement_object_ring_shader.shader` | intro scene | 4.5 |

**These are not believed to be broken, and that needs stating because it contradicts a comment already
in the tree.** `sun_shader.shader` carries the comment *"Without this the `STEREO_INSTANCING_ON` variant
is never compiled"*. `STEREO_INSTANCING_ON` is a **built-in stereo keyword** that Unity's shader compiler
adds to every shader itself when the XR render mode calls for it; `#pragma multi_compile_instancing`
gates `INSTANCING_ON`, which is *GPU* instancing (`Graphics.DrawMeshInstanced`, per-instance property
arrays) and is a different axis. The strongest evidence in this repo: `TMP_SDF-Mobile.shader` ships the
full stereo macro set with **no** `multi_compile_instancing` and no `#pragma target`, and renders
correctly in single-pass-instanced XR.

So these four were left untouched rather than "made consistent", because adding the pragma is not free:
it compiles an extra variant per pass, and in three of the four the `v2f` declares
`UNITY_VERTEX_INPUT_INSTANCE_ID` with no matching `UNITY_TRANSFER_INSTANCE_ID(v, o)` in `vert`, so the
`INSTANCING_ON` variant would emit a partially-initialised output struct. That is a compiler's call to
make, not a reader's. **CS-095 should settle it once and the answer applied to all five files including
`sun_shader`** — either the pragma is load-bearing and the other four get it, or it is not and the Sun's
comment should be corrected.

### 4f. Third-party — out of scope (31)

| Shader | Macros | Note |
|---|---|---|
| `TextMesh Pro/Shaders/TMP_SDF.shader` | ✓ | reachable (UI text) |
| `TextMesh Pro/Shaders/TMP_SDF-Mobile.shader` | ✓ | reachable |
| `TextMesh Pro/Shaders/TMP_SDF Overlay.shader` | ✓ | reachable |
| `TextMesh Pro/Shaders/TMP_SDF SSD*.shader`, `* SpaceWarp.shader`, `*-2-Pass.shader` | ✓ | not reachable |
| `TextMesh Pro/Shaders/TMP_SDF-Mobile Masking.shader`, `TMP_SDF-Mobile SSD.shader` | ✗ | not reachable |
| `TextMesh Pro/Shaders/TMP_SDF-Surface.shader`, `TMP_SDF-Surface-Mobile.shader` | ✗ | **surface shaders** — out of scope by rule; not reachable |
| `TextMesh Pro/Shaders/TMP_Bitmap*.shader` (3) | ✗ | not reachable |
| `TextMesh Pro/Shaders/TMP_Sprite.shader` | ✓ | not reachable |
| `Samples/XR Interaction Toolkit/.../Grid.shader`, `UI-NoZTest.shader`, `TunnelingVignette.shader` | ✓ | sample content |
| `Samples/XR Interaction Toolkit/.../DepthOnly.shader` | ✗ | reachable via a sample material; harmless (depth-only, `ColorMask 0`) |
| `Samples/XR Interaction Toolkit/.../BiRP_Fresnel.shader` | ✗ | **surface shader**; reachable via a sample material |
| `external/TouchScript/**` (7) | ✗ | legacy touchscreen; none reachable from a build scene |
| `third_party/mrtk_assets/.../MixedRealityStandard.shader` | ✓ | reachable |

Two XRI sample shaders (`DepthOnly`, `BiRP_Fresnel`) *are* reachable and *are* missing the macros. They
are sample assets we should not be shipping at all; the right ticket is to stop referencing them, not to
patch a vendored file.

---

## 5. What was changed, exactly

25 files under `Assets/shaders/`, each with the same three-or-four-line edit:

1. `UNITY_VERTEX_INPUT_INSTANCE_ID` appended to the `appdata` / `appdata_t` struct;
2. `UNITY_VERTEX_OUTPUT_STEREO` appended to the `v2f` struct;
3. `UNITY_SETUP_INSTANCE_ID(v);` then `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);` as the first two
   statements of `vert`, immediately after the `v2f o;` declaration;
4. in four files the `v2f o;` declaration was **hoisted to the top of `vert`**. In three
   (`planet_saturn`, `planet_rings`, `asteroid_ring`) this was required: the original code read
   `unity_ObjectToWorld` before declaring the output, and `UNITY_SETUP_INSTANCE_ID` has to precede that
   read. In `lens_flare` it was for consistency only — nothing eye-dependent was read early there. Each
   of the four carries a comment saying which case it is, because it is the kind of reordering a later
   editor would undo as tidy-up.

Nothing else was touched: no `#pragma target`, no blend state, no `#pragma multi_compile_instancing`, no
fragment shader, no `.mat`, no `.meta`.

---

## 6. Follow-up tickets

Both are proposed for Phase 7 (device pass), and **CS-095 blocks any attempt to switch Android to
Single Pass Instanced.**

- **CS-095 [TERM] — Compile and verify the 25 stereo-macro shader fixes on Link, both eyes.**
  Full detail in `docs/BACKLOG.md`.
- **CS-096 [CC/TERM] — The 8 reachable shaders the audit could not fix mechanically.**
  Full detail in `docs/BACKLOG.md`.

---

## 7. Reproducing this audit

The scan is three greps per file plus a GUID closure; there is no tool checked in. If it is worth
re-running regularly it belongs as an editor menu item alongside the other `Assets/scripts/Editor/*Builder.cs`
tools, walking `AssetDatabase.FindAssets("t:Shader")` and reporting the same six columns. That is a
better home for it than a doc that goes stale — worth a ticket if a third round of this is ever needed.
