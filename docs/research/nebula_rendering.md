# How space volumes are actually rendered, and what we should build

**Status:** research and architecture, 13 Sep 2026. Written after the point-sprite nebula was rejected on
look. Sources at the end; where a claim is from memory rather than a source it says so.

---

## 1. The constraint that decides most of this

`ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}`. **This project is on the
Built-in Render Pipeline.** Unity's own answer to "millions of particles, authored by a technical artist" is
**VFX Graph**, and VFX Graph's Shader Graph integration supports **HDRP and URP only** — it is not available
here. Any plan that starts "use VFX Graph with a point cache" is a plan that starts with a pipeline
migration of the whole app.

What *is* native and available on Built-in RP:

- **Compute shaders** filling a `ComputeBuffer`, drawn with `CommandBuffer.DrawProceduralIndirect` — no
  vertex or index buffer, the draw count read from the GPU with no CPU readback. This is the standard path
  and it is what the Cosmic Web already uses here.
- **Fragment-stage raymarching** into an offscreen target, composited back.
- **Shuriken particles** — CPU-simulated, and the wrong tool at these counts.

So the ceiling is not "what does Unity offer"; it is "what can a tile-based mobile GPU afford", which is the
next section.

---

## 2. How the projects worth copying do it

### SpaceEngine — raymarched volumes, rendered small and upscaled

SpaceEngine's nebulae are **not geometry**. A shader casts a ray per pixel and evaluates procedural density
along it: *"no 3D models... a special shader generates a virtual ray through each pixel and decides if there
is a rock along the ray's path on-the-fly by computing special equations"*, and its rocks are described as
*"opaque clumps of a nebula, with no polygons, only math"*. Two production details matter more than the idea:

1. **The volumetric pass runs at low resolution** with **animated noise to break up banding**.
2. **A dedicated upscale pass** — linear, bicubic and Lanczos, with and without sharpening, plus AMD's CAS —
   applied selectively per effect class (galaxies, nebulae, aurora, comet tails, volumetric rings).

That is the shape of every affordable volumetric in a real-time renderer: **march cheap, dither, upscale
well.** It is also the only technique on this list where "make the radius much bigger" costs nothing,
because a field has no point count.

### Academic nebula rendering

*Interactive Visualization and Simulation of Astronomical Nebulae* (arXiv 1204.6132) is the closest
published work to what this app is doing — real nebula data, interactive rates — and is worth reading before
committing to a density model.

### Gaussian splatting — the modern answer, and it has reached VR

Two recent papers bracket it. *Don't Splat your Gaussians* (arXiv 2405.15425) argues volumetric ray-traced
primitives are the right model for **scattering and emissive media** — which is exactly what a nebula is,
and exactly what flat additive billboards are not. *Nebula: city-scale 3D Gaussian Splatting in VR* (arXiv
2512.20495) does it on headsets through **collaborative rendering and accelerated stereo rasterization** —
the stereo part being the hard bit, and the same problem this project hit with its own shaders.

A splat is a point with a **covariance** — an oriented, anisotropic, soft blob — rather than a screen-facing
square. That single difference is most of why splats read as smoke and our sprites read as confetti.

### Point clouds at scale

*Software Rasterization of 2 Billion Points in Real Time* (arXiv 2204.01287) is the reference for the
opposite extreme: at very high counts, a **compute-shader rasteriser beats the hardware pipeline**, because
you control the blending and skip fixed-function overhead. Useful as a ceiling, not as a starting point.

### Quest 3 specifically

Quest 3's XR2 Gen 2 is roughly **twice Quest 2's GPU**, and Meta's own optimisation guidance is blunt about
the trap: most VR developers come from **immediate-mode desktop GPUs** and have not worked with
**tile-based mobile** rendering. On a tiler, large soft overlapping transparent quads are the single most
expensive thing you can draw — it is bandwidth, not triangles. Post-processing that used to be prohibitive
is now *"far more feasible"*, which is what makes a bloom pass affordable, and bloom is what makes stars
look like stars.

---

## 3. Why what we built looks wrong

Diagnosis before prescription. The current nebula is one technique doing three jobs:

| Symptom | Cause |
|---|---|
| Gas reads as confetti, not smoke | Screen-facing squares of uniform shape. No anisotropy, no orientation, no density falloff along the view ray. A splat has covariance; a sprite has a size. |
| Colour goes white where it is thickest | `Blend One One`. Additive light sums past 1 and clips. Real gas **scatters and occludes**; additive can only ever add. |
| Stars are not "every dot noticeable" | No HDR, no bloom. A star is a sub-pixel point source; without bloom it is one dim pixel, and with bloom it is a star. |
| Bigger radius made it thinner | Density fixed at bake time as N points. A field is not points; only a field scales for free. |
| Tuning is a guessing game | Nine hand-set constants with no physical meaning, and every change needs a rebake. |

---

## 4. What to build

**Three tiers by distance, which is what every one of the projects above does, with an HDR pass over all of
it.** Each tier is independently switchable, so none of them is a bet.

### Tier 1 — Near: raymarched density field (the smoke)

A fragment-stage raymarch of a **3D density texture** baked from the nebula's plate and shape model, times
FBM noise for detail the plate cannot carry. SpaceEngine's recipe, adapted:

- Render at **half or quarter resolution** into an offscreen target.
- **24–32 steps**, jittered per pixel with **animated blue noise** to trade banding for grain.
- **Upscale with a sharpening filter**, composite before transparents.
- Emission and extinction in one march, so the gas **occludes** as well as glows — the thing additive
  sprites cannot do at any count.

This is the tier that makes radius free: 9 m or 900 m is the same shader.

### Tier 2 — Mid: anisotropic splats, not sprites (the structure)

Keep the compute-buffer point path, upgrade the primitive:

- **Premultiplied alpha**, not additive — colour survives density.
- **Per-point covariance** (a 2×2 in screen space): oriented, stretched blobs that follow the filament they
  belong to, which is how a splat reads as gas rather than as a dot.
- **Size and count by distance**, fed from an append buffer with frustum and distance culling, drawn
  `DrawProceduralIndirect` so the CPU never sees the count.
- Sorted back-to-front per tile, or order-independent with weighted blending.

### Tier 3 — Far: the dome

What exists now. Correct for the far field, where nothing has parallax anyway.

### Over all three — HDR and bloom

Bright cores above 1.0, a threshold bloom pass, tone-map at the end. **This is what "every star noticeable"
actually requires**, and it is affordable on Quest 3 in a way it was not on Quest 2.

---

## 5. Order of work, and the honest risks

1. **HDR + bloom first.** Smallest change, largest visible difference, and it improves the galaxies and the
   Cosmic Web at the same time.
2. **Tier 2 splats.** Contained: same buffer, same draw call, better primitive and blend.
3. **Tier 1 raymarch behind a toggle.** Prototype on one nebula, measure on device, keep or discard.

Risks worth saying out loud:

- **Raymarching on a tiler is the expensive one.** Half-res plus upscale is the mitigation, and it is still
  the item most likely to fail the 13.9 ms budget. It is a prototype, not a promise.
- **Stereo.** Every custom pass here needs the eye-index macros or it breaks one eye on device and looks
  perfect on Link — this project has already been bitten by exactly that.
- **Built-in RP has no volumetric framework**, so tiers 1 and the bloom pass are ours to write and ours to
  maintain.

---

## Sources

- [SE scripts and raymarched nebulae — SpaceEngine](https://spaceengine.org/news/blog161008/)
- [Volumetric rings public beta — SpaceEngine](https://spaceengine.org/news/blog210611/)
- [General Relativity 3: Volumetric Accretion Disks — SpaceEngine](https://spaceengine.org/news/blog220830/)
- [Interactive Visualization and Simulation of Astronomical Nebulae (arXiv 1204.6132)](https://arxiv.org/pdf/1204.6132)
- [Don't Splat your Gaussians (arXiv 2405.15425)](https://arxiv.org/pdf/2405.15425)
- [Nebula: City-Scale 3DGS in VR (arXiv 2512.20495)](https://arxiv.org/pdf/2512.20495)
- [Software Rasterization of 2 Billion Points in Real Time (arXiv 2204.01287)](https://arxiv.org/pdf/2204.01287)
- [Unity — Graphics.DrawProceduralIndirect](https://docs.unity3d.com/ScriptReference/Graphics.DrawProceduralIndirect.html)
- [Unity — CommandBuffer.DrawProceduralIndirect](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.CommandBuffer.DrawProceduralIndirect.html)
- [Unity — Visual Effect Graph](https://unity.com/features/visual-effect-graph)
- [Working with Shader Graph in the Visual Effect Graph](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/sg-working-with.html)
- [How to Optimize your Quest App with RenderDoc — Meta](https://developers.meta.com/horizon/blog/how-to-optimize-your-oculus-quest-app-w-renderdoc-quest-hardware-and-software-offerings/)
- [Post-processing effects on Meta Quest 3 are "far more feasible"](https://mixed-news.com/en/?p=18992)
- [Point cloud rendering — Shahriar Shahrabi](https://medium.com/realities-io/point-cloud-rendering-7bd83c6220c8)
