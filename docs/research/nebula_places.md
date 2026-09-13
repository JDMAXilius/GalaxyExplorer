# Nebulae as places you stand inside

**Status:** design, 13 Sep 2026. Nothing here is built yet. The research it rests on is in the repository
already, and most of the hard part is done.

---

## 1. What already exists, which is more than it looks

Every one of the seven nebulae already has **three** representations built and committed:

| Layer | Built by | What it is |
|---|---|---|
| `card_0..card_3` | `NebulaPrefabBuilder` | Four billboarded photo cards. Flat. |
| `nebula_volume` | `NebulaVolumeBuilder` | **28,000 points standing in three dimensions**, coloured from the real plate, shaped by the model the object is known to have. |
| `place_shell` | `PlaceShellBuilder` | A sky and a gas dome from the same plate, written so the destination is "somewhere the player is standing in rather than an object they are holding". |

The volume is the important one, and it is better than a first look suggests. The depth is not invented
for four of the seven:

- **Shell** (`helix`, `crab`, `ngc1501`) — a planetary nebula or supernova remnant *is* a roughly spherical
  shell of ejecta, so a pixel at image radius `r` genuinely lies at depth `z = ±R·√(1−r²)`. The builder also
  divides the projection back out, because a shell photographed face-on is brightest at the rim only
  *because* the sight line passes through more gas there — seeding points by brightness without that
  correction would pile gas onto a rim that is an artefact of looking at it.
- **Bipolar** (`homunculus`) — two lobes on an axis, each a shell. One of the best-documented shapes in the sky.
- **Cloud** (`orion`, `pillars`, `trumpler14`) — irregular, no symmetry to exploit. Depth *is* modelled here,
  declared as a convention rather than a derivation, and recorded in the asset's `Provenance`.

Colour is never modelled: every point takes the colour of the pixel it came from. The blues, violets and
greens the owner asked for are already the real object's own light.

The shader `CosmicSimulation/NebulaVolume` carries stereo macros, so it is not one of the shaders that
breaks an eye on device.

**So the gap is not the nebula art. The gap is scale, smoke and flow.**

---

## 2. What the player gets today, and what is wanted

Today a nebula is a **0.7 m ball opened one metre in front of your face** — `RadiusMetres = 0.35`, sized to
GDD 4.3's "70 cm overlay" — floating over the Milky Way map in Halo mode, with the map dimmed behind it.
It is an object you hold.

What is wanted: **you travel to the nebula and stand inside it**, the way Andromeda and the other galaxies
are now places of their own. Gas around you in every direction, with that nebula's own colours, and enough
of it to read as smoke rather than as a scatter of dots.

Three things have to change, and only the third is hard.

### 2.1 Flow — easy, and the same change the galaxies just had

A nebula tag currently calls `ExperienceDirector.OpenDestination(module, position, diameter)`. It should
call `Switch(module)`, exactly as `GalaxyPins` does: the Milky Way unloads, the room goes to `FullBlack`,
and the nebula's content prefab becomes the place. `DestinationTags.Open` already routes on
`SceneName` vs `ContentPrefab`, so this is a routing decision, not new machinery.

The cards come off inside the place. They are the picture the owner wants replaced.

### 2.2 Scale — easy arithmetic, awkward consequences

`RadiusMetres` goes from 0.35 to something the player is inside — call it 3–5 m, matching the Galaxies
sphere at 3 m and the Cosmic Web's own reach.

The consequence is density. The volume holds 28,000 points in a ball 0.7 m across. Spread the *same*
points through a ball 8 m across and the number per unit volume falls by roughly **1,500×**. From inside,
you would be standing in a nearly empty room with a few hundred dots in view. Scaling the prefab up is
therefore not a solution on its own; it is the thing that makes the third problem unavoidable.

### 2.3 Smoke — the actual engineering problem

Being inside a cloud means the gas has to be *continuous* where you look. Four ways to do that, with what
each costs on a Quest 3 against the budgets in Technical Overview §7.4 (**≤ 150 draw calls, ≤ 400 k point
sprites, ≤ 500 instanced billboards, 72 Hz → 13.9 ms**):

| Approach | What it is | Cost | Verdict |
|---|---|---|---|
| **A. More points** | Raise 28 k → 120–200 k and enlarge the sprites | 120 k pts = 240 k tris/eye, inside the 400 k budget while the nebula is the only content. Vertex-bound, predictable, no new shader. | **Yes, as the base layer.** Cheapest honest win. Beyond ~200 k it eats the whole budget. |
| **B. Instanced smoke billboards** | A few hundred large, soft, additive quads, coloured and placed from the same volume data | 1 draw call; the budget explicitly allows 500 instanced billboards. **Fill rate is the risk** — large soft quads overlapping across the view is overdraw, which is what kills Quest frames, not triangles. | **Yes, with a coverage cap.** This is what makes it read as smoke. |
| **C. Raymarched volume** | Bake the plate + shape model into a 3D texture, march it in a fragment shader | The convincing answer, and the expensive one: full-screen marching is typically 2–6 ms on Quest even at half resolution with few steps. Needs its own stereo care. | **Prototype only.** Not the shipping path for seven places. |
| **D. Sorted slice planes** | A stack of camera-facing alpha planes through a 3D texture | Classic and cheap in triangles, brutal in overdraw, and it swims when you turn your head. | **No.** |

**Recommendation: A + B, with the `place_shell` kept as the far backdrop so the gas does not end in a hard
edge, and C kept as an experiment for one object** if the owner wants to see how far it could go.

The reason to prefer A+B is not only cost. Both layers derive from the *same baked volume*, so the smoke
and the points agree about where the gas is — the smoke is the same cloud at a lower frequency. Anything
generated independently would drift from the structure the plate actually shows.

---

## 3. What to build, in order

1. **One nebula end to end, as a vertical slice.** Two candidates, and both should be done before the rest:
   - **Helix** — a Shell, so its geometry is *derived*; being inside a shell of ejecta is the most dramatic
     case and the easiest to judge as right or wrong.
   - **Orion** — a Cloud, the most recognisable object of the seven, and the hardest model. Proving the
     hard case early stops six rebuilds later.
2. **Judge it in the headset.** Density, sprite size, smoke alpha and the backdrop are all look decisions
   and none of them can be settled from a monitor.
3. **Roll out to the remaining five**, which is a table edit in `NebulaVolumeBuilder` plus a rebake.
4. **Flow and copy**: the tag switches instead of overlaying, the panel copy says where you are, and the
   way back is the dock.

Open questions for the owner, which change the work rather than the polish:

- **Is the player at the centre, or flying in from outside?** Standing at the centre of the Helix is not
  where the gas is — the shell is a wall around you at the shell radius. Arriving *outside* it and moving
  in is the more truthful experience and needs travel, not just scale.
- **Does the nebula keep its real proportions?** The Pillars are light-years tall and Trumpler 14 is a
  cluster, not a cloud. Made to the same 4 m radius they will read as the same kind of object, which they
  are not.
- **Sound.** Ambience beds do not exist for any place yet (CS-077). Standing inside a nebula in silence is
  a noticeably worse experience than looking at one in silence.

---

## 4. What this does not need

No new photography, no new colour work, no generator features, and no new shader for the base layer. The
plates are in `Assets/Textures/nebulae/`, the shapes are decided, the points are baked, and the volume
shader already handles stereo. The work is density, a smoke layer, a scale number, and a routing change.
