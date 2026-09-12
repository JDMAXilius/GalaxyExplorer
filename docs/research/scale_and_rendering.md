# Scale, navigation and rendering budget for many galaxies and many planetary systems

*Research note, 12 September 2026. Commissioned before planning the "other galaxies / other
solar systems" work. No code, prefab, scene or shader was changed to write this. Sources are
cited inline and collected in section 7. Where a claim is community lore rather than
documented, it says so.*

This is research, not a plan and not a ticket. It answers three questions: how to handle scale
and travel, what can actually be drawn on a Quest 3, and where the line between hand-authored
and procedural content falls. It is grounded in this repo - `ExperienceDirector`, `DrawStars`,
`SpiralGalaxy`, `CosmicWebRenderer`, `AndromedaBuilder`, `spiral_stars_shader` - not in a
generic Unity project.

---

## 1. Recommendations for this codebase

**1. Do not build a coordinate hierarchy. Build a content hierarchy.**
This app has no large world coordinates and does not need any. Every experience hangs off the
`ViewLoader` (`ExperienceDirector.ContentRoot()`), lives between 0.05 m and 4 m
(`ScaleLimits`), and GDD 11 states flatly: "No locomotion, no forced camera motion; grow-ins
only." Floating origin, logarithmic depth, dual-float and scaled-space dual cameras are all
answers to a problem this project does not have and should never acquire. The hierarchy the
owner wants (local universe -> galaxy -> star system -> planet) is a hierarchy of *content*,
and every level of it should arrive at the same physical size, in the same place in the room,
on the same table.

**2. The navigation metaphor: "same table, new diorama". Nested destinations, not nested
navigation.**
Extend the mechanism that already exists - `ExperienceDirector.OpenDestination(module,
position, diameter)` - from "a nebula opens inside the Milky Way" to "any place can open
inside any place". Poking a galaxy in the local-universe field does not move the player and
does not zoom: the field fades to 20 percent (exactly what a nebula tag does today), a halo
appears, and the chosen galaxy grows in at 1.2-1.4 m across, one metre in front, at the same
apparent size the Milky Way and Andromeda already occupy. Poking a star in that galaxy swaps
in that system's orrery at 1.6 m, the same size the Solar System already occupies. Scale is
communicated by the panel, the label and the narration - never by the camera. This is the
project's existing idiom stated as a rule, and it is the only metaphor that costs nothing in
comfort. Detail and argument in 2.5.

**3. Express the hierarchy as dock state, not as a back stack.** Drill-down was retired
deliberately (roadmap 4.3; there is no `Backspace` key by design, and `DesktopMenuManager`
forces the Back button hidden). Do not reintroduce it. Give the dock a breadcrumb row - `Local
Universe > Milky Way > Solar System` - where each crumb is a poke target of the same kind as a
tile. That keeps GDD 3.1's "the dock is the only navigation" literally true, makes the nesting
legible, and gives "go back up" a target without a stack. `ExperienceModule` already has
`Kind = DockTile | Destination`; a parent reference is the only field this needs.

**4. The rendering bet: one hero point cloud, everything else an instanced billboard.**
The `DrawProcedural` point-cloud path is a hero renderer and nothing else. Budget one, at most
two simultaneous. The local-universe field should be `Graphics.DrawMeshInstanced` billboards
against an atlas - which is already what GDD 4.6 and Technical Overview 7.3 specify for the
Galaxies deep field (300-500 sprites, <= 500 instanced billboards). The right move is not a
new renderer: it is to build CS-063/CS-064 as specified, then reuse that field as the
local-universe level of the hierarchy. Detail in 3.2 and 3.4.

**5. LOD by vertex count is already free, but the bake order has to change first.**
`DrawStars` draws `starCount * 6` vertices out of a `StructuredBuffer`; lowering that number
draws fewer points from the same buffer with no reallocation, which is exactly what
`CosmicWebRenderer` already does with `_visible`. But `SpiralGalaxy.GenerateEllipses` emits
inner ellipse to outer ellipse and concatenates the second arm *after* the first, so
truncating today deletes the outer arms and one whole arm. Shuffle the bake order in
`AndromedaBuilder` (and in any future galaxy builder) and truncation becomes a free, correct
LOD.

**6. CS-118: the mechanism you want is `CommandBuffer.SetInstanceMultiplier`, not
`instanceCount: 2`.** Best evidence says instanceCount 1 is *correct* on the shipping Android
build (multiview replicates the draw in the driver) and *wrong* on a true single-pass-instanced
D3D session. Hardcoding 2 would fix Link and cost Quest double the geometry. Full reasoning and
the exact test in 3.6. Also: there are three `DrawProcedural` call sites, not five - the ticket
text is wrong.

**7. Hero destinations are hand-authored; the field is procedural from real bulk data.**
A defensible split, and the one other apps use: 5-10 hand-tuned hero galaxies and 5-10 hero
systems with real copy, real narration and a real plate; a background field generated from
OpenNGC (CC BY-SA 4.0, and the only catalogue that hands you morphology, position angle, axis
ratio and magnitude in one flat CSV) and the NASA Exoplanet Archive TAP service. Detail in 4.

**8. Licence warning that needs to reach the owner before any data work starts: Gaia DR3
catalogue data is CC BY-NC 3.0 IGO - non-commercial.** It cannot ship in a paid app without
separate clearance from ESA. Use HYG / AT-HYG (CC BY-SA 4.0) instead. See 4.2.

**What I would not attempt on a Quest 3**, stated plainly here and argued in section 5:
- Any camera translation between places. Not a fly-through, not a warp, not a "pull yourself
  toward the galaxy" gesture that moves the rig.
- A continuous zoom slider spanning orders of magnitude. The Oculus Best Practices Guide
  advises against zoom effects outright.
- More than about two simultaneous `DrawProcedural` galaxies.
- A true-scale coordinate space of any kind, with or without a floating origin.
- Per-galaxy real imagery at 2K. Twenty plates at 2048 ASTC 6x6 is roughly 50 MB of texture;
  the whole APK is 112 MB today. One atlas, or nothing.
- Additive point clouds composited over passthrough at any size that fills the view.

---

## 2. Question 1 - scale and travel

### 2.1 What this codebase actually does today

Worth stating precisely, because it decides most of the answer.

- **Nothing is far from the origin.** `ExperienceDirector.ContentRoot()` parents all
  prefab-backed content under the `ViewLoader`, which the intro places about 2 m ahead via
  `WorldAnchorHandler.CreateWorldAnchor`. `ScaleLimits` caps bodies at 0.05-3 m, models at
  0.4-4 m, nebulae at 0.3-2 m. The largest authored volume is the Galaxies 6 m sphere
  (GDD 4.6) and the Cosmic Web 5 m volume (GDD 4.7). Maximum coordinate magnitude in the
  entire app is single-digit metres.
- **There is exactly one level of nesting already, and it works.**
  `ExperienceDirector.OpenDestination` instantiates a `Kind = Destination` module's
  `ContentPrefab` at a position the caller supplies (the tag the player pinched), records its
  home with `FreePlacementAnchor.CaptureHome` *before* the grow-in, sets the environment,
  spawns a halo sized to the object, spawns that destination's own `InfoPanel`, plays its
  narration and grows it in over 0.6 s. `ClearDestinations` tears it down and restores the
  parent place's environment. The nebulae opened from the Milky Way map use this today.
- **There is no camera motion anywhere.** `TransitionManager`'s zoom survives only as an
  internal helper (roadmap 4.3). `DesktopMouseInput` orbits a pivot on the desktop; in the
  headset the rig is the player's head and nothing writes to it except `Recenter`.
- **Scale is already a first-class, player-driven gesture.** `ManipulationHandler` two-hand
  scale plus `ScaleLimits` means the player already grows and shrinks galaxies with their
  hands. This is the app's native verb for scale.

So the app is, architecturally, already a diorama viewer. The question is not how to add a
coordinate hierarchy. It is how to add depth to the content hierarchy without breaking the one
thing that makes the app comfortable.

### 2.2 How shipped space software handles 10+ orders of magnitude

Concrete techniques, with what each is actually for.

**Scaled space plus local space (Kerbal Space Program).** Two parallel coordinate spaces. Real
physics objects live in the flight scene at true metres; a parallel `ScaledSpace` hierarchy
holds low-poly meshes of every celestial body shrunk by a fixed factor, and that is what the
map-view camera renders. The ratio is documented with a worked example: Jool has a 6,000,000 m
radius and a 1,000-unit scaled-space mesh, so 1:6000
(https://github.com/NathanKell/RealSolarSystem/wiki/Scaled-Space). The KSP API reference
confirms `ScaledSpace` exists as the rendering-scale counterpart to the physical
`CelestialBody` (https://anatid.github.io/XML-Documentation-for-the-KSP-API/).

**Floating origin plus velocity zeroing (KSP's Krakensbane).** The KSP API docs state the
problem in one sentence: "The physics simulation has problems if vessels move too fast relative
to the underlying reference frame used by the simulation, or get too far from the origin of the
coordinate system"
(https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_krakensbane.html). Two
mechanisms: `setOffset(Vector3d)` moves every unrailed vessel by a position offset - the
floating origin proper - and `GetFrameVelocity()` exposes a frame velocity that is subtracted
from every physics object, so a ship at 3000 m/s looks stationary to the solver. Squad's own
devnote for KSP 1.2 names them as two distinct tunable subsystems and describes tuning their
engagement thresholds
(https://kerbaldevteam.tumblr.com/post/150703936094/devnote-tuesday-12-pre-release-the-bug-hunt-and).
Community reports say a floating origin was later added to scaled space *as well*, to kill
map-view jitter - the same disease treated twice, in both spaces.

**64-bit hierarchical addressing plus dual-float (Elite Dangerous).** The best-sourced case.
Frontier engineers, quoted in 80.lv: "A 64-bit integer number can store the x, y, z coordinate
of a sector of space, the sector layer (sectors come as part of an eight-layer octree), the ID
of the star-system within the sector and the ID of the body within the star-system." On
precision: "To ensure consistent visuals and gameplay on the screen and between users they need
millimeter precision, but the input values for the noise functions depend on the point on the
surface of the world relative to the planet's center, which can be of the scale of tens of
billions of millimeters." Their fix, both paths named: "We have written alternate libraries to
create the functions in 64 bit - i.e. double precision and dual-float precision. The former is
native 64-bit handling floating point numbers and the latter is emulated 64-bit functionality
using two 32-bit floats." And generation is explicitly top-down hierarchical: "the smallest
details on planets are informed by the results of planet-scale information generation, which
are informed by star-system scale information, which are informed by galactic information"
(https://80.lv/articles/generating-the-universe-in-elite-dangerous).

**Logarithmic depth buffer (Outerra).** Brano Kemen's numbers: a conventional 32-bit
perspective depth buffer gives about four decades of usable range; a logarithmic one gives
about nine. 16-bit log depth suffices for planetary scale, 24-bit for cosmic scale. Measured
cost of the per-fragment-correct version was 8-10 percent of GPU time; the vertex-only
approximation was unmeasurable
(https://outerra.blogspot.com/2012/11/maximizing-depth-buffer-range-and.html).

**Reverse-Z.** A different half of the same problem: it fixes how precision is *distributed*
for a fixed far plane, where log depth is what lets you push the far plane out nine orders of
magnitude in the first place. NVIDIA's "Visualizing Depth Precision" measures a zero error rate
for reversed-Z with a float32 depth buffer, against non-trivial error for every other
combination tested (https://developer.nvidia.com/blog/visualizing-depth-precision/). Godot
documents adopting it engine-wide (https://godotengine.org/article/introducing-reverse-z/).

**Scene-graph re-rooting (Dungeon Siege, GDC 2003).** The foundational talk for "there is no
world space". Scott Bilas: the world is chopped into Siege Nodes, 4x4 m or 4x8 m terrain tiles,
each its own coordinate space, connected by doors. Position is not a `Vector3` but a 4-tuple
`SiegePos = (x, y, z, node)` - an offset inside one node. When the frustum crosses a boundary
the target node changes and the engine performs "the Space Walk", accumulating transforms
outward through the door graph to bring everything visible into one consistent frame for that
frame. The design thesis is worth quoting because it is the honest one: "Gigantic continuous
world != numerical stability. Increasing distance leads to quantized space."
(https://www.gamedevs.org/uploads/the-continuous-world-of-dungeon-siege.pdf)

**Camera-relative rendering (Unity's own).** HDRP does this by default. Unity's docs state the
problem - "GameObject coordinates become increasingly less precise the further the GameObject
is from the origin of the Scene" - and the fix: "replaces the world origin with the position of
the Camera ... translates GameObjects and Lights by the negated world space Camera position
before any other geometric transformations affect them ... then sets the world space Camera
position to 0"
(https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@13.1/manual/Camera-Relative-Rendering.html).
**BiRP does not have this.** It is an HDRP feature, gated on `ShaderConfig.cs`.

**Procedural detail instead of more precision (SpaceEngine).** A useful counter-example.
SpaceEngine's dev blog says "the level of detail of 1 meter per pixel is the fundamental
limitation of the SE engine" for terrain, and rather than extend numeric precision they layer a
procedural detail-texture system on top, reaching "a resolution down to 1 millimeter per pixel"
(https://spaceengine.org/news/blog171120/). The claim that SpaceEngine uses 128-bit fixed point
for top-level universe coordinates is **community lore** - it did not survive attempts to source
it to the developer's own words.

**Scale-model, no precision problem at all (Titans of Space, Universe Sandbox).** The two most
directly analogous prior-art VR apps both solve 10+ orders of magnitude by *refusing to
represent them*. Titans of Space renders everything at 1:1,000,000 - Earth is a 12.7 m sphere -
and Drash's own account in Voices of VR #75 gives both reasons: "it was an iterative process,
trial and error ... Not just an artistic decision, but also just technical limits of the Unity
engine ... You can't have things that are too big while also having this small little cockpit in
front of you." He explicitly moved *away* from a multi-camera dual-scale trick to keep correct
stereo disparity in the headset, landing on treating every celestial body as a literal toy-size
object (http://voicesofvr.com/75-drash-on-updating-titans-of-space-for-dk2/). Universe Sandbox's
VR mode made this a gesture: pinch-scale "up to fit a galaxy in their hand, or bringing it back
to 1:1 scale to walk next to objects like the Saturn V rocket"
(https://universesandbox.com/blog/2016/04/vr-now-available/).

**And a warning worth reading.** Universe Sandbox discontinued VR support in 2025. Their own
words: VR player count was small and declining, and "the lack of players made the
disproportionately high VR maintenance cost hard to justify"
(https://universesandbox.com/blog/2025/02/future-of-vr-on-universe-sandbox/). The shipped
commercial app closest to this project's premise concluded that scale-spanning VR was not worth
maintaining.

**Fixed-viewpoint panoramas (NASA Exoplanet Travel Bureau).** Sidesteps the problem entirely:
each world is a 360-degree panoramic skybox, not a continuous flight-through
(https://exoplanets.nasa.gov/alien-worlds/exoplanet-travel-bureau/). Eyes on Exoplanets uses
relatable-unit framing instead - distance as travel time by car or plane
(https://science.nasa.gov/tutorials/eyes-on-exoplanets-tutorial/), and Eyes on the Solar System
sizes spacecraft next to a human, a school bus or a football stadium
(https://www.jpl.nasa.gov/news/explore-the-solar-system-with-nasas-new-and-improved-3d-eyes/).
**This is the single most transferable idea in the whole survey**: NASA's own answer to
"communicate 10 orders of magnitude" is a known reference object and a relatable unit, in copy,
not a camera move. This project already has the surface for it - `InfoPanel` and the `Stat`
struct with its `exponent` field.

**Sky-sphere (Star Chart, Sky Guide).** All stars painted on the inside of a sphere of
arbitrary radius centred on the observer, because at naked-eye scale relative depth is not
observable. Neither app has published anything about its internals; this is generic
planetarium-software convention, **not** a sourced claim about either product.

### 2.3 Which of those are available or sensible in BiRP on a Quest 3

| Technique | Available in BiRP on Quest 3? | Sensible here? |
|---|---|---|
| Floating origin / origin rebasing | Yes, it is app-level code | No. Solves a problem this app does not have |
| Camera-relative rendering | **No.** HDRP-only in Unity | Not needed |
| Logarithmic depth buffer | Only by writing it into every shader's vertex stage; the per-fragment-correct form needs an `SV_Depth` output | **No.** It would touch all 47 custom shaders, each then needing re-auditing for stereo (`docs/SHADER_STEREO_AUDIT.md`), and it disables early-Z on a tile GPU - the worst thing you can do to an Adreno |
| Reverse-Z | Engine-level, not exposed in BiRP | No |
| Nested coordinate frames / re-rooting | Yes - and the `ViewLoader` content root already is one | Yes, as *content* nesting. Already half-built |
| Scaled-space + local-space dual cameras (KSP) | Technically possible, but a second camera in XR means a second stereo pass and a manual composite; under single-pass instanced it also means correctly binding both slices of the eye texture array | **No.** Drash abandoned exactly this for stereo-correctness reasons, and it doubles fill cost on the platform that is fill-bound |
| Sprite <-> mesh / sprite <-> point-cloud LOD | Yes | **Yes.** This is the recommendation (3.4) |
| Scale model at a fixed ratio | Yes | **Yes.** Already what the app is |

The single-pass-instanced caveat matters for anything touching projection or depth. Android is
Single Pass Instanced, implemented as multiview; every custom shader needs
`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`,
`UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` as the
first line of `frag` if the fragment stage reads anything eye-dependent. Two further documented
traps:

- **Built-in RP plus Shader Graph plus single-pass instanced is a known-broken combination.**
  Unity's own manual says "Unity doesn't support single-pass instanced rendering in the built-in
  render pipeline when using Shader Graph", and also not with deferred rendering
  (https://docs.unity3d.com/Manual/SinglePassStereoRendering.html). This project is BiRP,
  forward, hand-written CGPROGRAM shaders - the one supported configuration. Do not introduce
  Shader Graph for any of this work.
- **Render-target array slices.** The most common cause of "only the left eye renders" for a
  custom command-buffer draw into an intermediate target is binding slice 0 instead of the whole
  array: you need `SetRenderTarget(rt, 0, CubemapFace.Unknown, -1)`
  (https://issuetracker.unity3d.com/issues/xr-commandbuffer-dot-drawmesh-only-renders-to-the-left-eye-when-using-single-pass-instanced-mode).
  `DrawStars` already dodges this by design: `CreateBuffers` sets
  `_useDownscaledTarget = renderIntoDownscaledTarget && !XRSettings.isDeviceActive`, so on a
  headset the intermediate-target path is skipped entirely and every layer draws straight into
  the eye buffer. Any new galaxy renderer must keep that property.

### 2.4 Float precision: the actual numbers

float32 is 1 sign bit, 8 exponent bits and 23 stored mantissa bits, so 24 bits of significand
once the implicit leading 1 is counted - about 7.2 decimal digits, machine epsilon 2^-24 which
is about 5.96e-8 (https://en.wikipedia.org/wiki/Single-precision_floating-point_format). For a
value whose magnitude falls in [2^k, 2^(k+1)), the gap between representable values - the ULP -
is exactly 2^(k-23). In metres:

| Distance from origin | ULP (smallest representable step) |
|---|---|
| 1 m | 0.12 micrometres |
| 4 m (this app's practical maximum) | 0.48 micrometres |
| 1 km | 61 micrometres |
| 8.2 km (2^13) | about 1 mm |
| 131 km (2^17) | about 1.6 cm |
| 1,049 km (2^20) | 12.5 cm |
| 8,389 km (2^23, roughly Earth's radius) | 1 m |
| 1 AU (2^37) | 16.4 km |
| 1 light year (2^53) | about 1.07e9 m |

**Where it visibly breaks down.** Unity community measurement puts animated-mesh vertex jitter
onset at roughly 2,000-5,000 units from the origin, worsening through 10,000, with the working
rule of thumb being sub-millimetre precision out to about 10,000 m and sub-centimetre by about
100,000 m
(https://discussions.unity.com/t/floating-point-errors-and-large-scale-worlds/698299). Unity
also emits a first-party editor warning recommending you bring world coordinates "within a
smaller range"
(https://discussions.unity.com/t/unity-warning-due-to-floating-point-precision-limitations-it-is-recommended-to-bring-the-world-coordinates-of-the-gameobject-within-a-smaller-range/1577274),
and there is a filed Unity bug titled exactly "Camera is jittering when at large world
coordinates in VR"
(https://issuetracker.unity3d.com/issues/camera-is-jittering-when-at-large-world-coordinates-in-vr).

**Why VR breaks earlier than a monitor.** Two compounding reasons, and the mechanism is worth
understanding rather than taking on faith. First, stereo: the two eye cameras sit about 63 mm
apart and the brain fuses depth from their *relative* offset, so an absolute positional error
that is invisible in mono becomes an eye-to-eye mismatch - doubled or warped depth - in stereo.
Human stereo acuity is on the order of 5-10 arcseconds, which at 1 m viewing distance is a
lateral shift of roughly 25-50 micrometres; by the ULP table above, that threshold is crossed
somewhere around 800 m to 1.7 km from the origin. Second, 6DoF head tracking adds small
high-frequency deltas (millimetre head sway) on top of whatever large base position the rig sits
at, at 72-120 Hz - which is precisely the worst case for float32, a small delta added to a large
base where the delta approaches the local ULP and is quantized into visible stutter. This second
half is synthesised from forum consensus rather than one primary citation; treat the mechanism
as well-understood and the exact onset distance as empirical.

**Conclusion for this project.** Content lives inside 4 m. ULP there is half a micrometre -
about three orders of magnitude of headroom before the 1 mm line, and roughly 200x headroom
before the stereo-acuity line. **This app has no float precision problem, and the design should
be written so that it never acquires one.** Which is the same as saying: never represent a real
astronomical distance as a world-space coordinate. Keep them in `double` fields on a
ScriptableObject, format them into `InfoPanel` copy, and let the renderer only ever see
room-scale numbers.

### 2.5 The interaction question, and the recommendation

This is the part that matters most, so it gets the argument in full.

**What the comfort guidance actually says.**

Meta defines the hazard: "Vection is a visually induced phenomenon that occurs during slide
locomotion in fully immersive experiences, where the user perceives movement through visual cues
even when physically stationary"
(https://developers.meta.com/horizon/design/locomotion-comfort-usability/). Its Comfort page
adds a mixed-reality-specific mitigation this project gets for free: "Show the real-world
environment at the edges of the user's field-of-view ... helps ground the user, especially
during movement or rapid scene changes"
(https://developers.meta.com/horizon/design/comfort/). The Dimmed-room mode the app already uses
for four of seven experiences *is* that grounding.

The Oculus Best Practices Guide (v.310-30000-02, 2017) is more specific, and two of its rules
decide this question:

- "Do not use any head-bob or changes in orientation or position of the camera that were not
  initiated by the real-world motion of the user's head." And: "User and camera movements should
  never be decoupled."
- On scale versus zoom, which is the crux: "A single global scale on the entire head model is
  fine (e.g. to convert feet to meters, or to shrink or grow the player), but do not scale head
  motion independent of interpupillary distance (IPD)." Against: "Zooming in or out with the
  camera can induce or exacerbate simulator sickness ... We advise against using 'zoom' effects
  until further research and development finds a comfortable and user-friendly implementation."

So the guidance is not "scale change is dangerous". It is: **a uniform global rescale is
sanctioned; a continuous camera zoom is explicitly discouraged.** That distinction is the whole
design.

The BPG also documents, in 2017, the exact pattern being proposed here - and calls it god mode:
"An alternative take on the 'teleportation' model pulls the user out of first-person view into a
'god mode' view of the environment with the player's avatar inside it. The player moves the
avatar to a new position, then returns to first-person view from the new perspective." That is
Worlds in Miniature, which Stoakley, Conway and Pausch published at CHI 1995 as "a hand-held
miniature copy of the virtual environment"
(https://www.cs.cmu.edu/~stage3/publications/95/conferences/chi/paper.html), with a companion
SIGGRAPH 1995 paper whose title is the mechanism: "Navigation and Locomotion in Virtual Worlds
via Flight into Hand-Held Miniatures". Wingrave, Haciahmetoglu and Bowman later showed plain WIM
"failed to work in worlds with tasks at various levels of scale" and that adding scaling and
scrolling fixed it with "no significant hit in user performance"
(https://ieeexplore.ieee.org/document/1647500/) - which is directly the multi-level problem
here.

Two further findings that shape the transition:

- **Cuts beat animated transitions for comfort.** "Scene Transitions and Teleportation in
  Virtual Reality and the Implications for Spatial Awareness and Sickness" found animated
  transitions "increased sickness ... when compared to simpler transitions, like Cut", while the
  animated cue improved spatial orientation (https://ieeexplore.ieee.org/document/8554159/). The
  BPG blesses the fade convention outright: "Some games fade to black to convey the player
  falling asleep or losing consciousness, and then have them awaken somewhere else as part of
  the narrative. These conventions can be carried over to VR with little issue."
- **Peripheral flow is the driver, which is why vignettes work.** Fernandes and Feiner (3DUI
  2016, best paper) reduced sickness with a "subtle dynamic field-of-view modification" that
  "partially obscure[s] each eye's view with a virtual soft-edged cutout", "without decreasing
  the participants' sense of presence ... and without the majority of the participants even
  being aware of the intervention"
  (https://www.cs.columbia.edu/2016/combating-vr-sickness/images/combating-vr-sickness.pdf).
  Google Earth VR ships this as comfort mode; Cloudhead call theirs the "Vection Portal"
  (https://www.gamedeveloper.com/design/lessons-learned-from-five-years-of-vr-locomotion-experiments).
  **This project needs none of it, because it has no locomotion to vignette.** Worth knowing
  only as the cost of the alternative.

**And the store consequence.** Meta's comfort ratings: "Comfortable" apps "generally avoid
camera movement, player motion, or disorienting content and effects"; "Moderate" apps "might
incorporate some camera movement, player motion, or occasionally disorienting content and
effects" (https://www.meta.com/help/quest/331713305046406/). The rating is assigned on the app's
*default* experience. **Cosmic Simulation XR is Comfortable today. Any camera move between
galaxies costs that rating**, and with it the new-user audience Meta steers toward Comfortable
apps. That is a commercial argument, not only a design one, and it belongs in the decision.

**The recommendation: "same table, new diorama".**

State one rule and hold it at every level of the hierarchy:

> Travelling to a place never moves the player. The place arrives in front of them, at a size
> the room can hold, on the same table the last place stood on. The distance you travelled is
> told to you, not flown through.

Concretely, and using only mechanisms that exist:

1. **Constant apparent size.** Every level arrives at the size its peers already use - a galaxy
   at 1.2-1.4 m (Milky Way 1.4 m, Andromeda 1.2 m), a system orrery at 1.6 m (Solar System), a
   body at 25 cm on pull. The player then rescales it by hand within `ScaleLimits`, which is the
   app's existing verb for scale and is a uniform rescale of the *object*, not of the head model
   - so it does not even engage the BPG's IPD caveat.
2. **The transition is the existing fade plus grow-in.** `TransitionManager`'s fade, then
   `ExperienceDirector.GrowIn`'s 0.6 s cubic ease-out from zero. That is a cut with a soft
   landing: the comfortable end of the transition literature, and already shipped.
3. **Opening a child place is `OpenDestination`, generalised.** The parent fades to 20 percent
   rather than unloading - which is exactly what GDD 4.3 already specifies for a nebula overlay
   ("the galaxy fades to 20 percent, a black halo appears 1.0 m in front of the player, and the
   nebula grows inside it from a point to about 70 cm across"). Nesting deeper means letting a
   `Destination` module itself have destinations. `ExperienceModule` needs one field: a parent
   reference.
4. **Scale is copy, not camera.** The distance and size of each place is a line in the panel and
   a line of narration, with a reference object. This is NASA's own approach in Eyes on
   Exoplanets and Eyes on the Solar System, and the `Stat { double value; string unit; int
   exponent }` struct in `BodyInfo` already exists to carry it.
5. **The hierarchy shows on the dock as a breadcrumb, never as a Back button.** See
   recommendation 3.

**Why this fits this app's idiom specifically.** The app's existing sentence is "the place
appears in front of you and you grab it". Every mechanism the owner would need is already built
and already tested against that sentence: `OpenDestination` for nesting, `GrowIn` for arrival,
`ScaleLimits` plus `ManipulationHandler` for player-driven scale, `FreePlacementAnchor` for
bringing a pushed-away place home, `EnvironmentController.SpawnHalo` for focusing attention on a
child place inside a parent, `SwitchNotice` for a place that is not built yet. A camera-move
metaphor would need all of that *and* a locomotion system, a comfort vignette, a rest frame, an
opt-in settings tier, and a probable downgrade to Moderate. The diorama metaphor needs a parent
field on a ScriptableObject and a breadcrumb row.

**The honest cost.** A diorama hierarchy does not convey scale viscerally. You will never feel
how much bigger a galaxy is than a solar system, because both are 1.3 m wide on your table. That
is a real loss and it should be an explicit, recorded decision rather than a side effect. The
mitigations are all cheap and none of them move the camera: a *comparison* layout (the Relative
Size preset already proves the pattern for bodies - do the same for galaxies), a nesting
animation where the child grows out of the exact point on the parent it came from, and copy that
names a reference the player already knows.

---

## 3. Question 2 - the rendering budget

### 3.1 The budget, and what one galaxy actually costs

Technical Overview 7.4: at 72 Hz and at least 60 fps, no more than 150 draw calls, 400,000 point
sprites, 500 instanced billboards, textures 2048 ASTC 6x6, one dim quad, three visible
world-space canvases, no realtime shadows.

Meta's own published Quest 3 figures are looser: 200-300 draw calls for a busy simulation,
400-600 medium, 700-1000 light, and a triangle budget of 1.3-1.8 million; minimum 72 fps for
interactive apps (https://developers.meta.com/horizon/documentation/unity/unity-perf/). So the
project's 150 is conservative against Meta's number and should stay that way - it is a budget
for a mixed-reality app that also composites passthrough.

Meta's draw-call cost analysis is worth internalising, because it prices the exact thing a
multi-galaxy field would do: switching shaders costs 175 percent more draw-call time than the
baseline, switching materials with the same shader costs 64 percent more, and redrawing the same
object costs only about 25 percent of drawing a different one
(https://developers.meta.com/horizon/documentation/unity/po-draw-call-analysis/). That is the
whole case for instancing: identical shader, identical material, one call.

**What a galaxy costs on the path that exists today.** Measured by reading `AndromedaBuilder.cs`,
`SpiralGalaxy.cs`, `DrawStars.cs` and `StarVertDescriptor.cs`:

| Per galaxy on the `DrawProcedural` path | Cost |
|---|---|
| `SpiralGalaxy` layers | 3 (clouds, dust, stars) |
| `DrawStars` components | 3, each added at runtime by `InitializeParticles` |
| `ComputeBuffer`s | 3 |
| Materials | 3, each `Instantiate`d - so no batching is possible, by construction |
| `CommandBuffer.DrawProcedural` calls | **3** |
| Points | 17,200 (Andromeda) / 18,720 (Milky Way) |
| Vertices submitted | 103,200 (17,200 x 6) |
| Triangles, both eyes | 68,800 (2 per point, x2 views) |
| Buffer memory | about 757 KB (44-byte `StarVertDescriptor` x 17,200) |
| Repo cost | about 4 MB of baked `StarsData` YAML |
| Per-frame CPU | about 10 material property writes per layer per frame, in `DrawStars.Update` |

Three ceilings fall out of that, and the tightest one is not the one written in 7.4:

- **Draw calls:** 150 / 3 = 50 galaxies, if galaxies were the only thing drawing. They are not.
- **Point sprites:** 400,000 / 17,200 = **23 galaxies**.
- **Triangles, which nobody appears to have computed before:** 17,200 points x 2 triangles x 2
  views = 68,800 triangles per galaxy. Meta's Quest 3 budget of 1.3-1.8 million gives **19-26
  galaxies**. Note what that means for the existing ceiling: 400,000 point sprites is 1.6
  million triangles across both eyes, which is *the entire Quest 3 triangle budget*. The 400 k
  figure in 7.4 is therefore not conservative at all once stereo is counted - it is the absolute
  maximum, and the Cosmic Web's 120,000 points already spends 30 percent of it.

Two scaling problems in the code itself, both of which bite well before 20 galaxies:

- `DrawStars.AttachAllInOrder` walks the **static** `Instances` list and removes then re-adds
  every command buffer on every camera whenever a new drawer appears. With 60 drawers (20
  galaxies x 3 layers) that is quadratic churn on spawn - in the exact frame the player pokes a
  tile.
- `DrawStars.Update` writes about ten material properties per layer per frame. Twenty galaxies
  is 600 property writes per frame before anything is drawn.

Neither is fatal, both are real, and neither has ever been measured, because **nothing on this
path has been profiled on a device**: CS-066 (Cosmic Web frame time) and CS-103 (Andromeda) are
both `todo`, and `cosmic_web_content_prefab` has never been generated.

### 3.2 Drawing tens to low hundreds of galaxies

| Technique | Draw calls | Fill / overdraw | Memory | Verdict |
|---|---|---|---|---|
| **Instanced billboards, atlas, `DrawMeshInstanced`** | 1 per 1023 instances - so **1** for 300-500 | Lowest of the four. One quad per galaxy, one blend per covered pixel, coverage bounded by the sprite's own size | One 2048 ASTC 6x6 atlas, about 2.5 MB with mips | **This is the bet.** Already specified in GDD 4.6 and Technical Overview 7.3 |
| **Per-galaxy point cloud, `DrawProcedural` + `StructuredBuffer`** | 3 per galaxy | Worst. Sub-pixel additive quads: full triangle setup and tile binning per point, no coverage benefit | 757 KB buffer plus about 4 MB YAML per galaxy | Hero only. One, maybe two simultaneous |
| **Octahedral impostors** | 1 per impostor material, instanceable | Similar to billboards, plus a view-direction-dependent atlas fetch and optional depth reconstruction | Much larger: an octahedral atlas per subject, multiplied by albedo/normal/depth | **No.** They solve parallax and silhouette for close-range 3D geometry. A galaxy at 10 cm has no silhouette to preserve, and the memory is the wrong shape. No authoritative writeup could be sourced in this pass, which is its own reason not to commit |
| **Baked cubemap / panorama** | 1, effectively free | Lowest possible: one cheap shader over a large area | One cubemap, 6 faces | **Yes, for the true background.** No parallax, not grabbable, not pokeable - so it cannot be the interactive field, but it is the right answer for whatever lies beyond the interactive shell |

Why billboards win here specifically, beyond the draw-call arithmetic: Quest 3's Adreno 740 is a
tile-based renderer with 8 GB of LPDDR5 at roughly 68 GB/s
(https://en.wikipedia.org/wiki/Meta_Quest_3), and Meta's own draw-call analysis ranks shader
complexity above vertex cost as "the thing to be concerned with when you measure GPU time". OVR
Metrics Tool reports "Average Fill Percentage per Eye" and splits "Percentage Time Shading
Fragments" from "Percentage Time Shading Vertices"
(https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/) - the existence of a
dedicated per-eye fill metric is itself the clearest signal about where Meta expects the
bottleneck to be.

But note the nuance that matters for *this* content: a point cloud is not classically fill-rate
bound. Its cost is triangle setup, tile binning and per-fragment blend on primitives that may
cover less than one pixel - and on a tiler that is the same bandwidth pipeline, paid at the worst
possible ratio of setup cost to covered pixels. "Quest is fill-bound, not vertex-bound" is true,
and it still does not rescue a field of sub-pixel additive quads.

One hard constraint on the billboard route: `Graphics.DrawMeshInstanced` caps at **1023 instances
per call**, requires `Material.enableInstancing`, and - importantly - "does not further frustum
or occlusion cull individual instances after combining them"
(https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstanced.html), so off-screen
instances still cost. At 300-500 galaxies that is one call and no culling needed. At 5,000 it
would be five calls and a CPU pre-cull.

### 3.3 How many simultaneous `DrawProcedural` galaxies before it falls over

Geometry says 19-26 (3.1). Fill and setup say far fewer. The reasoning:

Take Andromeda at its authored 1.2 m: 17,200 points, each a quad whose half-size is
`star.size * _WSScale`, where `_WSScale` is multiplied by the galaxy's own `lossyScale` in
`DrawStars.Update`. Summed quad area comes out at roughly half the area of the galaxy's own disc
- so within its own footprint a hero galaxy is only about 0.5x to 1x overdraw. That is cheap, and
it is why one hero galaxy is comfortable.

Now shrink it to the 3-25 cm the GDD asks of a deep-field galaxy. The quads shrink with it, so
the *ratio* holds - but each quad is now well under a pixel. Quest 3's panels are 2064x2208 per
eye over roughly 110 degrees, about 19 pixels per degree; a 15 cm galaxy at 1.5 m subtends 5.7
degrees, about 107 pixels across, with a disc area of roughly 9,000 pixels. Seventeen thousand
points over 9,000 pixels is two points per pixel. You are paying 34,400 triangles of setup and
binning per eye to fill 9,000 pixels that a single quad would have filled.

**So: budget one simultaneous point-cloud galaxy, allow two, and treat three as a thing to be
measured before it is promised.** That is not a precision-limited number, it is a
setup-and-bandwidth one, and it wants CS-066 and CS-103 to produce a real frame time before
anyone believes it.

### 3.4 The cheap LOD story, and where the crossover sits

The good news is that the LOD mechanism is already in the code. The draw is
`DrawProcedural(matrix, material, 0, MeshTopology.Triangles, starCount * 6)` - a *vertex count*.
Lower it and you draw fewer points from the same buffer, with no reallocation, no second asset
and no shader change. `CosmicWebRenderer` already does exactly this: it tracks `_visible` and
draws `_visible * 6` while the web is still being generated, which is how the volume condenses
into view over about a second.

**The blocker, and it is a real one.** `SpiralGalaxy.GenerateEllipses` loops ellipses
inner-to-outer, with `progression = i / EllipseCount` driving the radius from `MinEllipseScale`
to `MaxEllipseScale`, and when `enableSecondArm` is set the second arm's points are `Concat`ed
*after* all of the first arm's. So truncating the vertex count today would cut the outer arms off
and, past the halfway point, delete an entire spiral arm. The fix is one line in the builder:
shuffle the point array deterministically before baking. Then truncation thins uniformly and LOD
is free. This is also why `CosmicWebRenderer`'s progressive reveal looks right and a galaxy's
would not - the web generator mixes structure types per candidate, so its buffer is already
roughly uniform.

**The crossover, derived rather than guessed.** Using 19 pixels per degree per eye:

| Apparent size at 1.5 m | Angular size | Footprint | Representation |
|---|---|---|---|
| under about 0.2 m | under about 8 deg | under about 150 px | **Sprite.** More than one point per pixel; the cloud is strictly worse than a billboard and costs about 100x the setup |
| about 0.2 m to 0.6 m | about 8-23 deg | about 150-430 px | **Truncated cloud**, 2,000-6,000 points, or a sprite. Test both; prefer the sprite if it reads |
| over about 0.6 m | over about 23 deg | over about 430 px | **Full cloud.** Hero territory, and where the GDD already puts the Milky Way (1.4 m) and Andromeda (1.2 m) |

Which restates recommendation 4: the point cloud is the renderer for the place you are *in*, and
a sprite is the renderer for every place you can *see*. The hierarchy makes that natural - there
is only ever one place you are in.

### 3.5 Transparency, overdraw and passthrough

**The fill arithmetic.** 2064 x 2208 per eye is 4.557 megapixels; both eyes at 72 Hz is 656
megafragments per second for **one** full-coverage layer. Meta's store requirements demand the
declared refresh rate be met (VRC.Quest.Performance.1) and recommend running at no less than 85
percent render scaling for the majority of the experience (VRC.Quest.Performance.4)
(https://developers.meta.com/horizon/resources/publish-quest-req/) - so you cannot buy your way
out with render scale. **No published Meta numeric overdraw ceiling could be found**; the
commonly repeated "2-3x" figure did not survive sourcing in this pass. Treat it as folklore and
measure with OVR Metrics Tool's per-eye fill percentage instead.

The practical consequence: every full-coverage transparent layer is a whole 656 Mfrag/s of
budget. The project already knows this and priced it correctly once - Technical Overview 7.3
chose **4** nebula planes rather than 6 explicitly because "each is a full-coverage transparent
quad and the black halo behind is a fifth layer, so overdraw is the binding cost on Quest". That
reasoning is right and should be applied to galaxy fields too: a 6 m sphere of 500 sprites around
the player is not one layer, it is however many sprites the ray through any given pixel crosses.

**Passthrough alpha.** With `OVRPassthroughLayer` active the camera is set to
`CameraClearFlags.SolidColor` with `backgroundColor = Color.clear` and no skybox
(https://developers.meta.com/horizon/documentation/unity/unity-passthrough-gs/), and the XR
compositor composites the app's eye buffer against the passthrough layer using that alpha
channel. **The alpha channel is the compositing signal.** CLAUDE.md's rule - "alpha output honest
(passthrough composites on alpha)" - is exactly right.

Good news, found by reading the shaders: **the additive star path already handles this
deliberately.** `spiral_stars_shader` and `cosmic_web_points_shader` are both `Blend One One`,
but both compute alpha from luminance - `color.a = dot(color.xyz, 1)` and
`saturate(dot(rgb, 1.0))` respectively - so under an additive blend the eye-buffer alpha
accumulates with brightness, and a bright star reads as opaque to the compositor while empty
space stays transparent. That is the correct behaviour, and it matters because the Milky Way runs
in **Dimmed room**, which means passthrough is visible behind it today. The nebula card shader
goes further and uses a separate alpha blend - `Blend SrcAlpha OneMinusSrcAlpha, One
OneMinusSrcAlpha` - which is the textbook-correct form and the pattern any new transparent
content should copy.

Practical guidance for the new work:
- Any new galaxy billboard shader should use the nebula card's separate alpha blend, not straight
  additive, unless it is genuinely a star field.
- Additive over passthrough has a perceptual problem as well as a compositing one: additive
  content added to bright real-world video washes out. No explicit Meta guidance on this could be
  found (flagged as a gap), so it wants a device check. GDD 4.6 already defaults Galaxies to full
  black with passthrough as a toggle - that default is the safe one and should stay.
- `CosmicWebRenderer` draws at `AfterSkybox` so opaque depth occludes it and world-space UI draws
  over it. `DrawStars.cameraEvent` is private and `DrawStars` is added at runtime, so Andromeda
  draws at the default `BeforeForwardOpaque`. That inconsistency is already recorded against
  CS-103; it becomes a correctness problem rather than a cosmetic one the moment several galaxies
  and a dim quad have to sort against each other.

### 3.6 CS-118 - does `DrawProcedural` need its instance count doubled?

**First, a correction to the ticket.** There are **three** `DrawProcedural` call sites in the
project, not five: `DrawStars.cs:192`, `OrbitalTrail.cs:238`, `CosmicWebRenderer.cs:391`. The
other two references the ticket counts are prose in comments (`AndromedaBuilder.cs:52`,
`CosmicWebGenerator.cs:15`). All three use the five-argument overload, so `instanceCount`
defaults to 1.

**Second, the mechanism nobody had named.** Unity has a documented API for exactly this:
`CommandBuffer.SetInstanceMultiplier`, which "add[s] a command to multiply the instance count of
every draw call by a specific multiplier ... If you set the multiplier to 2, a command that draws
one instance, instead draws two." Default 1. Its documentation explicitly lists the calls it
affects - `CommandBuffer.DrawMesh`, `DrawMeshInstanced`, `Graphics.DrawMeshInstanced`,
**`CommandBuffer.DrawProcedural`**, `Graphics.DrawProcedural`, and Unity's own internal draws -
and explicitly excludes the indirect variants
(https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.SetInstanceMultiplier.html).

Unity's manual page on single-pass instanced rendering and custom shaders then says how it is
used: "URP automatically doubles the instance count for regular draw calls in relevant render
passes, by calling `CommandBuffer.SetInstanceMultiplier`", and "For indirect draw calls, you must
manually double the instance count contained in your compute buffer to support single-pass
instancing" (https://docs.unity3d.com/Manual/SinglePassInstancing.html).

**Third, why the answer differs between Android and Link, which is what the ticket suspected.**

Under **true single-pass instanced** (D3D11/D3D12, which is what a Link session forced to
`SinglePassInstanced` gets), the eye index is derived from the instance id -
`UnityInstancing.cginc` computes the eye from the low bit of the instance id and the real
instance as the id shifted down
(https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityInstancing.cginc).
`UNITY_VERTEX_OUTPUT_STEREO` reserves an `SV_RenderTargetArrayIndex` output, and
`UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` writes the eye index into it to select the slice. With
`instanceCount` 1, `instanceID` is 0, the eye index is 0, every vertex targets slice 0, and **the
right eye gets nothing.** So on true SPI the count genuinely must be doubled. It also follows
that doubled data must be *interleaved* (L, R, L, R), not concatenated - which is fine here,
because the eye index is derived arithmetically and nothing per-eye is read out of the buffer.

Under **multiview** (`GL_OVR_multiview2` / `VK_KHR_multiview`, which is how Android implements the
mode and therefore what the shipping Quest 3 build runs), the replication happens beneath the
API. The Khronos `OVR_multiview` spec says the driver "handles instantiating the same draw call
for each view", with `gl_ViewID_OVR` letting the shader differentiate per view
(https://registry.khronos.org/OpenGL/extensions/OVR/OVR_multiview.txt). Meta's own Enable
Multiview documentation is unambiguous about the application-side consequence: "objects are
rendered once to the left eye buffer, then duplicated to the right buffer automatically" and "the
render thread will dispatch half as many draw calls, as each draw call will affect both left and
right eye buffers"
(https://developers.meta.com/horizon/documentation/unity/enable-multiview/). One app-submitted
draw, unmodified. `instanceCount` 1 is correct.

This project's own code already encodes that understanding, in a comment that predates the
ticket. `Assets/shaders/cginc/StarQuad.cginc`: "Geometry shaders used to do this, but Meta
Quest's GPU driver does not support them together with multiview stereo."

**Fourth, the one thing that could not be sourced.** URP is documented to call
`SetInstanceMultiplier` itself. **No equivalent documented statement exists for the Built-in
Render Pipeline**, and specifically none about whether BiRP's multiplier state applies to a
command buffer the app injects through `Camera.AddCommandBuffer` at a `CameraEvent`. Because
`SetInstanceMultiplier` is itself described as a *command recorded into a buffer*, it is entirely
plausible that a user buffer does not inherit it. There is also historical evidence that
`DrawProcedural` plus single-pass stereo was outright broken in the Unity 5.4-5.5 era, with a
filed bug and a user workaround of "a hack allowing us to call DrawProcedural twice (for left and
right eye)" (https://discussions.unity.com/t/single-pass-stereo-with-command-buffers/635511) -
historical, not current guidance, but it establishes that this path has been wrong before.

**So, the answer, with confidence levels:**

- On the **shipping Android build (multiview)**: `instanceCount` 1 is correct and no change is
  needed. High confidence - the Khronos spec and Meta's own documentation agree, and the
  project's own multiview comment corroborates.
- On a **Link session forced to true single-pass instanced**: the draw needs a multiplier of 2,
  or only the left eye will render. High confidence, from the `UnityInstancing.cginc` mechanism.
- **The fix, when it is confirmed, should be `commandBuffer.SetInstanceMultiplier(...)` and not
  `instanceCount: 2`.** This is the load-bearing recommendation of this section. A hardcoded 2
  would be correct on Link and would double the geometry on Quest, where the driver already
  replicates - turning a one-eye bug into a 2x triangle cost on the platform that actually
  ships. The multiplier API is the backend-aware form. If a manual value proves unavoidable, gate
  it on `XRSettings.stereoRenderingMode`, which distinguishes `SinglePassInstanced` from
  `SinglePassMultiview`; nothing in `Assets/` reads that property today.
- **Whether any change is needed at all in BiRP depends on the undocumented question above and
  must be measured, not reasoned.** The test is small and unambiguous: in a Link session with the
  Standalone render mode set to `SinglePassInstanced` (it is `m_renderMode: 0`, Multi Pass, in
  `Assets/XR/Settings/OpenXR Package Settings.asset` today), open the galaxy view and capture
  each eye separately. If the right eye is missing the stars, BiRP is not applying the multiplier
  to our buffer and it must be set explicitly. Then repeat on the Android build to confirm the
  multiview path is unaffected by whatever was added. Both halves are required; the ticket is
  right that an Android pass alone does not close it.

One further note for whoever does this: the `appdata` struct in `spiral_stars_shader` already
carries `UNITY_VERTEX_INPUT_INSTANCE_ID`, and the comment in it explains why - "under single-pass
instanced stereo the eye index arrives as the instance id, and `UNITY_SETUP_INSTANCE_ID` reads it
off the input struct. A bare `uint vid : SV_VertexID` parameter gives it nowhere to arrive." That
is correct and is a precondition for the doubling to work at all. The shader side of this is
done; only the C# side is open.

---

## 4. Question 3 - procedural generation at content scale

### 4.1 The minimum honest data

**Per galaxy**, to draw something recognisable and not a lie:

1. **Morphological type** (Hubble or de Vaucouleurs stage: E0-E7, S0, Sa-Sd, SBa-SBd, Irr) -
   picks the template.
2. **Position angle** - rotates the projected template to match the sky.
3. **Inclination**, or the major/minor axis ratio it comes from - squashes a face-on template
   into the right ellipse. For a thin disc, `inclination = arccos(b/a)`.
4. **Angular major-axis diameter** - sets apparent size.
5. **Colour**, or a B-V / surface-brightness proxy - spirals bluer, ellipticals redder.
6. **Distance or redshift** - places it in depth, and gives the panel its number.

Mapped onto what this codebase's generator already takes, the fit is good. `AndromedaBuilder`
already parameterises `XRadii` / `ZRadii` (which is the axis ratio), `TiltDegrees` and
`YawDegrees` (inclination and position angle), `SpiralRotationDegrees` and `ArmSpacingDegrees`
(arm winding and count, which is what a Hubble stage encodes), a colour ramp per layer, and
`WidthMetres`. **A real galaxy's catalogue row maps almost one-to-one onto the parameters
`AndromedaBuilder` already exposes.** That is the strongest argument that a data-driven galaxy
builder is a modest extension rather than a new system.

**Per planetary system**: host star `st_teff`, `st_rad`, `st_mass`, `st_spectype`; system
distance `sy_dist`; and per planet `pl_orbsmax`, `pl_orbper`, `pl_orbeccen`, `pl_orbincl`,
`pl_rade`, `pl_bmasse`. That is enough for an orrery of the kind `solar_system_view_scene`
already draws, plus honest panel stats. It is emphatically *not* enough for a planet's
*appearance* - almost no exoplanet has a known albedo, let alone a map - which is a
content-honesty constraint, covered in section 6.

### 4.2 Where the bulk data is, and what the licences say

| Source | Endpoint | Format | Licence | Notes |
|---|---|---|---|---|
| **OpenNGC** | https://github.com/mattiaverga/OpenNGC | Semicolon-delimited CSV, one flat file | **CC BY-SA 4.0** | **The single best file for this job.** Columns confirmed from the live header: `Name; Type; RA; Dec; Const; MajAx; MinAx; PosAng; B-Mag; V-Mag; J-Mag; H-Mag; K-Mag; SurfBr; Hubble; Pax; Pm-RA; Pm-Dec; RadVel; Redshift; ...; M; NGC; IC; Common names; ...`. That is items 1-6 of 4.1 in one row. Roughly 13,000-14,000 NGC+IC objects, so bright and large objects only. Verify the exact count before budgeting against it |
| **NASA Exoplanet Archive** | `https://exoplanetarchive.ipac.caltech.edu/TAP/sync?query=<ADQL>&format=csv` | CSV / TSV / JSON / VOTable | US government data; **acknowledgement required, not a CC licence** | Tables `ps` (one row per planet per reference; filter `default_flag=1`) and `pscomppars` (one row per planet, best value per parameter). About 6,366 confirmed planets as of early Sept 2026. The old `nph-nstedAPI` is retired. Acknowledgement text at https://exoplanetarchive.ipac.caltech.edu/docs/acknowledge.html |
| **HYG / AT-HYG** | https://codeberg.org/astronexus/hyg and .../athyg | CSV | **CC BY-SA 4.0** for current versions (**CC BY-SA 2.5** for v2/v3 - version matters) | HYG v4.1 is Hipparcos + Yale BSC + Gliese, about 119,600 stars in v3. AT-HYG is Tycho-2 plus Gaia DR3 distances, about 2.5 million stars, and crucially **astronexus publishes it under CC BY-SA**, having already done the redistribution work over Gaia |
| **Gaia DR3** | https://gea.esac.esa.int/tap-server/tap (ADQL) | VOTable / CSV | **CC BY-NC 3.0 IGO - NON-COMMERCIAL** | See the warning below. 1.8 billion rows; the standard subset is an ADQL cut such as `WHERE phot_g_mean_mag < 10 AND parallax_over_error > 5` |
| **HyperLeda** | http://atlas.obs-hp.fr/hyperleda/leda/fullsql.html | SQL box, text/CSV export | **No published open licence** - acknowledgement policy only | Deeper and larger than OpenNGC for the same fields (`type`, `pa`, `incl`, `logd25`, `bt`, `modbest`). Get written clearance from Lyon Observatory before bulk-embedding in a commercial build |
| **NASA/IPAC NED** | https://ned.ipac.caltech.edu/Documents/Guides/Interface/TAP | TAP / ADQL | US government data; acknowledgement required | Per-object queries rather than one bulk file |
| **VizieR** | https://vizier.cds.unistra.fr/ , `cdsclient` | TAP, many formats | "Free of usage in a scientific context"; **per-catalogue copyright follows the original publisher** | No blanket licence. Read each catalogue's ReadMe (https://cds.unistra.fr/vizier-org/licences_vizier.html) |
| **Galaxy Zoo** | https://data.galaxyzoo.org/ | CSV / FITS / VOTable | No machine-readable licence found; citation required | Crowd-verified morphology detail (bar strength, arm winding, mergers) beyond a single Hubble code. GZ2's recommended table is 239,695 galaxies |

**The licence warning that needs to reach the owner first.** Gaia DR3 catalogue data is **CC
BY-NC 3.0 IGO** - Attribution-NonCommercial - not CC BY-SA as is widely assumed
(https://www.cosmos.esa.int/web/gaia-users/license,
https://creativecommons.org/licenses/by-nc/3.0/igo/deed.en). It cannot be baked into a paid app
without pursuing separate commercial clearance under ESA's archive terms. Do not conflate this
with ESA's *outreach imagery*, which is CC BY-SA 3.0 IGO - a different thing. **Use AT-HYG (CC
BY-SA 4.0) wherever Gaia-derived star positions are wanted**; astronexus has already done that
redistribution and publishes under BY-SA.

**Imagery**, which this project already sources correctly per CS-005: ESA/Hubble
(https://esahubble.org/copyright/), ESO (https://www.eso.org/public/copyright/) and NOIRLab
(https://noirlab.edu/public/copyright/) all converge on **CC BY 4.0**, commercial use expressly
permitted, with logo and identifiable-person carve-outs and no implied endorsement. NASA's own
media is generally not copyrighted, with the insignia excluded
(https://www.nasa.gov/nasa-brand-center/images-and-media/). Every required credit line differs
slightly, which is why `Assets/_sources/CREDITS.md` records the specific source per file.

### 4.3 The hand-authored / procedural line

**Where other apps draw it.** Elite Dangerous is the clearest case: the Stellar Forge generates
about 400 billion systems procedurally from a hierarchical, top-down rule set, while the systems
of the inhabited bubble are placed from real catalogue data and carry authored content.
SpaceEngine generates on the order of 10 trillion galaxies procedurally while overlaying real
catalogue objects where catalogues exist. NASA's own tools invert it: Eyes on Exoplanets is
entirely data-driven from one archive, and the Exoplanet Travel Bureau hand-authors a small
number of artist-interpreted panoramas and says so.

**The recommendation for this project**, in the terms the project already uses:

| Tier | Count | How it is made | What it gets |
|---|---|---|---|
| **Hero** | 5-10 galaxies, 5-10 systems | Hand-tuned `ExperienceModule` assets; parameters authored per subject into a data-driven successor to `AndromedaBuilder`; a real plate from ESA/Hubble or ESO | Real copy in `docs/copy/`, real narration, a rendered thumbnail, an ambience bed, an `InfoPanel`, the point-cloud renderer, full grab and scale |
| **Named field** | 100-300 galaxies | Generated at build time from OpenNGC; one instanced billboard each, sprite chosen by `Hubble` type, squashed by `MinAx/MajAx`, rotated by `PosAng`, tinted by `B-Mag - V-Mag` | A name label on hover and a one-line stat generated from the CSV row. No narration, no bespoke copy |
| **Anonymous field** | the rest | Procedural, no catalogue row at all | Nothing. It is scenery, and it must be visually distinguishable from the named tier or the app is quietly lying |

Two rules that make this honest rather than decorative:

- **Never present generated geometry as a photograph of that object.** A procedural spiral built
  from a real galaxy's type, inclination and position angle is an honest *diagram* of it; it is
  not a picture of it. The panel copy has to say which it is. This is the same distinction the
  project already draws when it uses a real ESA/Hubble plate for a nebula overlay versus a
  procedural Cosmic Web.
- **Anonymous filler must be visibly a field, not a catalogue.** The current GDD already gets
  this right - 6.4 describes the Galaxies deep field as "anonymous sprites" on purpose. If the
  owner wants that field to become named and travelable, that is a change to the GDD's one-to-one
  contract and to `docs/copy/`, and it should be a recorded decision rather than a drift.

**Build-time, not run-time.** The generation should be an editor step in the shape the project
already uses - `AndromedaBuilder`, `CosmicWebBuilder`, `NebulaPrefabBuilder` and
`SolarRowBuilder` are all deterministic menu items that write assets and then wire a module's
`ContentPrefab`. A catalogue-driven galaxy builder is the fifth of those, not a new pattern. The
one place run-time generation is already justified is the Cosmic Web, and the reason recorded in
`CosmicWebRenderer`'s class comment is a good one: 120,000 points would be a 28 MB YAML asset. By
that same test, 300 billboard instances are trivially a run-time array, and 20 hero galaxies at
about 4 MB of `StarsData` each is 80 MB of repository - which is a real reason to prefer a
compact binary format or run-time generation for anything past the first few heroes.

---

## 5. What I would not attempt on a Quest 3

Collected from the three sections, in rough order of how strongly I would argue it.

1. **Any camera translation between places.** It costs the Comfortable rating, it needs a
   locomotion system, a vignette, a rest frame and an opt-in tier, and it contradicts GDD 11 and
   the app's whole idiom. The Oculus BPG's "User and camera movements should never be decoupled"
   is the short version.
2. **A continuous zoom that spans orders of magnitude.** "We advise against using 'zoom' effects
   until further research and development finds a comfortable and user-friendly implementation" -
   Oculus BPG. Player-driven two-hand scale of an *object* is a different thing and is already
   shipped.
3. **A true-scale coordinate space, with or without a floating origin.** Nothing in the app needs
   a coordinate larger than about 4 m, and a `double` field on a ScriptableObject plus a line of
   panel copy is the correct home for a real astronomical distance.
4. **A logarithmic depth buffer.** It would touch every custom shader's vertex stage, each of
   which would need re-auditing against `docs/SHADER_STEREO_AUDIT.md`, and the
   per-fragment-correct form kills early-Z on a tile GPU.
5. **A scaled-space / local-space dual-camera rig.** A second stereo camera doubles fill cost on
   the platform that is fill-bound, and requires manually binding both slices of the eye texture
   array. Titans of Space abandoned this exact approach for stereo-correctness reasons.
6. **More than two simultaneous `DrawProcedural` point-cloud galaxies** - and not even two until
   CS-066 and CS-103 produce a measured frame time.
7. **Octahedral impostors.** They solve silhouette and parallax for close 3D geometry; a 10 cm
   galaxy has neither. The memory cost is the wrong shape, and no authoritative reference could be
   sourced, which is its own argument against committing.
8. **Per-galaxy 2K real imagery.** Twenty plates at 2048 ASTC 6x6 is roughly 50 MB of texture (a
   2048 square at 3.56 bits per texel is about 1.87 MB, about 2.5 MB with mips) against a 112 MB
   APK. One atlas.
9. **Shader Graph for any of this.** Unity's manual: single-pass instanced is not supported in
   BiRP with Shader Graph.
10. **Full-coverage additive layers over passthrough.** The nebula overlay is already capped at 4
    planes plus a halo for exactly this reason.

---

## 6. Risks

**The biggest one, and the one the owner should hear before anything is committed: the
performance budget this whole plan would be built on has never been measured.**

Technical Overview 7.4's numbers are an assertion, not a measurement. Specifically, as of today:
`cosmic_web_content_prefab` has never been generated and its 120,000 points have never been
compiled or profiled (CS-066, `todo`). `andromeda_content_prefab` has never been built and its
builder has never been run (CS-103, `todo`). The Galaxies field - the one existing precedent for
drawing many galaxies, and the thing recommendation 4 wants to extend - has not been built at all
(CS-063, CS-064, `todo`). No per-eye verification has been done on an Android build, which is the
only configuration where the missing-stereo-macro class of bug is visible
(`docs/SHADER_STEREO_AUDIT.md`). And section 3.1 of this note found, by arithmetic that nobody
appears to have done before, that the 400,000-point ceiling in 7.4 is **the entire Quest 3
triangle budget** once both stereo views are counted - meaning the existing budget is not
conservative, it is maximal, and the Cosmic Web alone already spends 30 percent of it.

Adding an unbounded content axis - "many other galaxies and many other planetary systems" - on
top of five unverified renderers is the risk. The cheap insurance is a sequencing rule rather
than a smaller plan: **land CS-066, CS-103 and CS-064, and get one real OVR Metrics frame-time
and per-eye fill reading off a Quest, before a single line of hierarchy or catalogue code is
written.** That one measurement converts every number in section 3 from arithmetic into a budget,
and it is the difference between planning and guessing.

Four smaller risks, each concrete:

- **CS-118 is still open and this plan multiplies it.** Three `DrawProcedural` sites today; a
  galaxy hierarchy could mean dozens. If the BiRP instance-multiplier question in 3.6 resolves
  the wrong way, every one of them is a one-eye bug, and the platform where it is visible is the
  one that ships. Resolve it at three call sites, not thirty. And resolve it with
  `SetInstanceMultiplier`, not a hardcoded 2, or Quest pays double geometry to fix Link.
- **The GDD is a one-to-one contract and this changes it.** GDD 3.2 names seven experiences. GDD
  6.4 says the Galaxies field is "anonymous sprites". Turning it into named, travelable
  destinations changes the contract, the copy pipeline (`docs/copy/`), the narration set (22
  reused clips plus 11 TTS placeholders, decision D-003), the thumbnails, and the ambience beds -
  four of which are already owed and unwritten. None of that is hard; all of it is work that does
  not look like work in a plan.
- **Content honesty at scale.** Almost no exoplanet has a known appearance. A procedurally
  textured planet presented beside a NASA map of Mars in the same visual language is a quiet lie,
  and this project's own rules - "our name, copy and art only", the CREDITS discipline - are the
  ones that would be broken. Generated bodies need a visual and textual register that
  distinguishes them from observed ones, decided before anything is generated.
- **Repository weight.** Baked `StarsData` is roughly 4 MB of YAML per galaxy. Twenty heroes is
  80 MB of text assets in git. `CosmicWebRenderer`'s class comment already articulates the
  correct test for when to bake and when to generate; apply it before the first extra hero, not
  after the fifth.

One last thing worth saying plainly, because the survey turned it up and it would be dishonest to
bury it: **Universe Sandbox - the closest shipped commercial analogue to this app - killed its VR
mode in February 2025**, citing a small and declining VR player count against "disproportionately
high VR maintenance cost"
(https://universesandbox.com/blog/2025/02/future-of-vr-on-universe-sandbox/). That is not an
argument against building this. It is an argument for the cheapest possible version of it: the
hierarchy that reuses `OpenDestination`, the field that reuses `DrawMeshInstanced`, the data that
comes from one CSV, and the hero count that stays in single digits.

---

## 7. Sources

**Meta and Oculus, first-party**
- Locomotion comfort and usability - https://developers.meta.com/horizon/design/locomotion-comfort-usability/
- Locomotion best practices - https://developers.meta.com/horizon/design/locomotion-best-practices/
- Comfort - https://developers.meta.com/horizon/design/comfort/
- Oculus Best Practices Guide, v.310-30000-02 (2017) - https://static.oculus.com/documentation/pdfs/intro-vr/latest/bp.pdf
- Comfort ratings - https://www.meta.com/help/quest/331713305046406/
- App policies - https://developers.meta.com/horizon/policy/app-policies/
- Unity performance budgets (draw calls, triangles, fps) - https://developers.meta.com/horizon/documentation/unity/unity-perf/
- Draw call cost analysis - https://developers.meta.com/horizon/documentation/unity/po-draw-call-analysis/
- VRC Quest performance requirements - https://developers.meta.com/horizon/resources/publish-quest-req/
- Enable Multiview - https://developers.meta.com/horizon/documentation/unity/enable-multiview/
- Single Pass Stereo / Stereo Instancing - https://developers.meta.com/horizon/documentation/unity/unity-single-pass/
- Passthrough getting started (camera clear and alpha) - https://developers.meta.com/horizon/documentation/unity/unity-passthrough-gs/
- Application SpaceWarp - https://developers.meta.com/horizon/documentation/unity/unity-asw/
- OVR Metrics Tool - https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/

**Unity**
- Single-pass instanced rendering and custom shaders - https://docs.unity3d.com/Manual/SinglePassInstancing.html
- Single-pass stereo rendering (support matrix; Shader Graph and deferred exclusions) - https://docs.unity3d.com/Manual/SinglePassStereoRendering.html
- `CommandBuffer.SetInstanceMultiplier` - https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.SetInstanceMultiplier.html
- `Graphics.DrawMeshInstanced` (1023 cap, no per-instance culling) - https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstanced.html
- HDRP camera-relative rendering - https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@13.1/manual/Camera-Relative-Rendering.html
- `UnityInstancing.cginc` (eye index from instance id) - https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityInstancing.cginc
- Only-left-eye with a custom command buffer, and the array-slice fix - https://issuetracker.unity3d.com/issues/xr-commandbuffer-dot-drawmesh-only-renders-to-the-left-eye-when-using-single-pass-instanced-mode
- VR jitter at large world coordinates - https://issuetracker.unity3d.com/issues/camera-is-jittering-when-at-large-world-coordinates-in-vr
- Float precision and large-scale worlds (community measurements) - https://discussions.unity.com/t/floating-point-errors-and-large-scale-worlds/698299
- Unity's large-world-coordinates editor warning - https://discussions.unity.com/t/unity-warning-due-to-floating-point-precision-limitations-it-is-recommended-to-bring-the-world-coordinates-of-the-gameobject-within-a-smaller-range/1577274
- BiRP + Shader Graph + multiview unsupported (case #1348241) - https://discussions.unity.com/t/shadergraph-builtin-render-pipeline-single-pass-instanced-multiview-is-it-working/899231
- Historical DrawProcedural single-pass stereo breakage - https://discussions.unity.com/t/single-pass-stereo-with-command-buffers/635511
- Indirect draws and x2 instance count in practice - https://discussions.unity.com/t/how-to-renderprimitivesindirect-for-single-pass-stereo-with-instance-count-x2-gracefully/1636324

**Graphics techniques**
- Khronos `OVR_multiview` - https://registry.khronos.org/OpenGL/extensions/OVR/OVR_multiview.txt
- Outerra, maximizing depth buffer range and precision - https://outerra.blogspot.com/2012/11/maximizing-depth-buffer-range-and.html
- NVIDIA, visualizing depth precision (reverse-Z) - https://developer.nvidia.com/blog/visualizing-depth-precision/
- Godot, introducing reverse-Z - https://godotengine.org/article/introducing-reverse-z/
- Bilas, The Continuous World of Dungeon Siege, GDC 2003 - https://www.gamedevs.org/uploads/the-continuous-world-of-dungeon-siege.pdf
- IEEE 754 single precision - https://en.wikipedia.org/wiki/Single-precision_floating-point_format
- ASTC bit rates - https://en.wikipedia.org/wiki/Adaptive_scalable_texture_compression
- Meta Quest 3 hardware - https://en.wikipedia.org/wiki/Meta_Quest_3

**Shipped space software**
- KSP scaled space, 1:6000 - https://github.com/NathanKell/RealSolarSystem/wiki/Scaled-Space
- KSP `Krakensbane` API reference - https://anatid.github.io/XML-Documentation-for-the-KSP-API/class_krakensbane.html
- Squad devnote, floating origin and Krakensbane thresholds - https://kerbaldevteam.tumblr.com/post/150703936094/devnote-tuesday-12-pre-release-the-bug-hunt-and
- Elite Dangerous, generating the universe (Frontier engineers quoted) - https://80.lv/articles/generating-the-universe-in-elite-dangerous
- SpaceEngine terrain engine upgrade #3 - https://spaceengine.org/news/blog171120/
- SpaceEngine FAQ - https://spaceengine.org/manual/faq/
- Universe Sandbox, VR now available (pinch-scale) - https://universesandbox.com/blog/2016/04/vr-now-available/
- Universe Sandbox, the future of VR (discontinued) - https://universesandbox.com/blog/2025/02/future-of-vr-on-universe-sandbox/
- Universe Sandbox, next-gen graphics update - https://universesandbox.com/blog/2024/11/next-gen-graphics-update/
- Voices of VR #75, Drash on Titans of Space (1:1,000,000; the stereo reasoning) - http://voicesofvr.com/75-drash-on-updating-titans-of-space-for-dk2/
- NASA Eyes on the Solar System - https://www.jpl.nasa.gov/news/explore-the-solar-system-with-nasas-new-and-improved-3d-eyes/
- NASA Eyes on Exoplanets tutorial - https://science.nasa.gov/tutorials/eyes-on-exoplanets-tutorial/
- NASA Exoplanet Travel Bureau - https://exoplanets.nasa.gov/alien-worlds/exoplanet-travel-bureau/

**VR comfort research and prior art**
- Stoakley, Conway, Pausch, Virtual Reality on a WIM, CHI 1995 - https://www.cs.cmu.edu/~stage3/publications/95/conferences/chi/paper.html
- Pausch et al., Flight into Hand-Held Miniatures, SIGGRAPH 1995 - https://www.cs.cmu.edu/~stage3/publications/95/conferences/siggraph/paper.html
- Wingrave, Haciahmetoglu, Bowman, Scaled and Scrolling WIM, 2006 - https://ieeexplore.ieee.org/document/1647500/
- Fernandes and Feiner, subtle dynamic FOV modification, 3DUI 2016 - https://www.cs.columbia.edu/2016/combating-vr-sickness/images/combating-vr-sickness.pdf
- Scene transitions and teleportation, spatial awareness and sickness - https://ieeexplore.ieee.org/document/8554159/
- Prothero et al. 1999, independent visual background - https://www.semanticscholar.org/paper/19d6b196039068907e80488a2483c58b9dcae1ea
- Interrante et al., Seven League Boots, 3DUI 2007 - https://www-users.cse.umn.edu/~interran/3dui07.pdf
- Cloudhead Games, five years of VR locomotion experiments - https://www.gamedeveloper.com/design/lessons-learned-from-five-years-of-vr-locomotion-experiments
- Google Daydream Labs on locomotion - https://www.blog.google/products/daydream/daydream-labs-locomotion-vr/
- Apple HIG, spatial layout - https://developer.apple.com/design/human-interface-guidelines/spatial-layout
- VIRUP, the Virtual Reality Universe Project - https://arxiv.org/abs/2110.04308

**Catalogues and licences**
- OpenNGC - https://github.com/mattiaverga/OpenNGC
- NASA Exoplanet Archive TAP - https://exoplanetarchive.ipac.caltech.edu/docs/TAP/usingTAP.html
- NASA Exoplanet Archive acknowledgement - https://exoplanetarchive.ipac.caltech.edu/docs/acknowledge.html
- NASA Exoplanet Archive PS columns - https://exoplanetarchive.ipac.caltech.edu/docs/API_PS_columns.html
- HYG - https://codeberg.org/astronexus/hyg (archived mirror https://github.com/astronexus/HYG-Database)
- AT-HYG - https://codeberg.org/astronexus/athyg , https://astronexus.com/projects/at-hyg-details
- Gaia archive TAP - https://gea.esac.esa.int/tap-server/tap
- **Gaia data licence, CC BY-NC 3.0 IGO** - https://www.cosmos.esa.int/web/gaia-users/license , https://creativecommons.org/licenses/by-nc/3.0/igo/deed.en
- Gaia credit and citation - https://gea.esac.esa.int/archive/documentation/GDR3/Miscellaneous/sec_credit_and_citation_instructions/
- HyperLeda SQL - http://atlas.obs-hp.fr/hyperleda/leda/fullsql.html ; acknowledgement https://leda.univ-lyon1.fr/acknowledge.html
- NASA/IPAC NED TAP - https://ned.ipac.caltech.edu/Documents/Guides/Interface/TAP ; acknowledgement https://ned.ipac.caltech.edu/node/7
- VizieR licences - https://cds.unistra.fr/vizier-org/licences_vizier.html
- Galaxy Zoo data - https://data.galaxyzoo.org/
- ESA/Hubble copyright, CC BY 4.0 - https://esahubble.org/copyright/
- ESO copyright, CC BY 4.0 - https://www.eso.org/public/copyright/
- NOIRLab copyright, CC BY 4.0 - https://noirlab.edu/public/copyright/
- NASA images and media guidelines - https://www.nasa.gov/nasa-brand-center/images-and-media/

**Known gaps in this research**, stated so nobody treats them as settled: no published Meta
numeric overdraw ceiling was found; no explicit Meta guidance on premultiplied alpha or on
additive blending over passthrough was found; no current Quest 3 per-app memory ceiling figure was
found; no authoritative octahedral-impostor reference was retrieved; whether BiRP applies its
instance multiplier to an app-injected command buffer is undocumented and must be measured;
SpaceEngine's 128-bit fixed-point coordinate claim, KSP2's floating-origin implementation, and the
Star Chart / Sky Guide sky-sphere description are community lore or generic convention only. Both
research passes exhausted their web-search budgets, so the items in this paragraph would benefit
from a second pass.
