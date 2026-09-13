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

---

## 5. Decisions taken, 13 Sep 2026 (owner)

- **You arrive in the middle.** No flight in from outside.
- **Proportions match the galaxy places**, not real distances. The Pillars will not be light-years tall.
- **Ambience: researched below, recommendation to follow.**
- **The target is what the galaxy places already achieve**: inside it, stars visible, gas, colour, smoke.

---

## 6. The revised approach, and it is cheaper than section 3 said

Measuring the galaxy places against the nebula volumes inverts the earlier recommendation:

| | Galaxy place (Whirlpool) | Nebula volume (today) |
|---|---|---|
| Points | **8,600** | **28,000** |
| Layers | **3** | **1** |
| Width | 1.1–1.35 m | 0.7 m |
| Reads as | gas, dust and stars | a scatter of dots |

**The nebula already has three times the points and looks worse.** The galaxies do not win on count; they win
because each layer does one job, with its own shader and its own sprite size:

| Role | Count | World sprite | Shader | Job |
|---|---|---|---|---|
| **Clouds** | 250 × 10 = 2,500 | **0.02** (large, soft) | `spiral_stars_cloud_shader` | the glow — this is the smoke |
| **Dust** | 200 × 8 = 1,600 | 0.02, tint `(0.07,0.06,0.06,0.4)`, `IsShadow` | `spiral_stars_negative_shader` | dark lanes that **subtract** light |
| **Stars** | 300 × 15 = 4,500 | **0.008** (small, bright) | `spiral_stars_shader` | the stars |

So the recommendation changes from *"raise 28 k to 120 k and add a billboard system"* to:

> **Rebuild the nebula volume as the same three roles, keeping the derived geometry and the plate's colour.**

Concretely, per nebula:

- **Gas/glow layer** (~3,000 large soft points) — placed by the existing Shell / Bipolar / Cloud model, coloured
  from the plate. This is the smoke, and it is the layer that makes you feel inside something.
- **Dust layer** (~2,000 subtractive points) — seeded where the plate is *dark against bright surroundings*,
  which is what a dust lane is. Orion and the Pillars are mostly dust structure; this is what currently reads
  as "missing".
- **Star layer** (~5,000 small bright points) — seeded from the plate's point-like peaks, plus a thin field
  through the volume so there are stars *around* you, not only behind the gas.

That is **~10,000 points, about a third of today's 28,000**, and three shaders that already ship and already
carry stereo macros. Cheaper on device, and it is the exact look the owner pointed at.

Scale: **1.2 m across**, matching Andromeda's 1.2 m and the three library galaxies' 0.95–1.35 m, with the
player at the centre. Section 2.2's density problem disappears at this size — it was created by the 4 m
proposal, which the owner's answer has now retired.

Remaining unknown, and it is a look question rather than an engineering one: at the centre of a **Shell**
object (Helix, Crab, NGC 1501) the gas is a wall at the shell radius and the middle is genuinely empty.
That is what the object *is*. It may read as standing in a cathedral, or as standing in an empty room with a
painted wall. Helix is the slice to build first for exactly this reason.

---

## 7. Ambience: research and recommendation

Nothing in the app has an ambience bed today — all 46 data assets have `Ambience` unset, which is CS-077.
The wiring exists on both tracks (`AmbienceController`, `ExperienceModule.Ambience`), so this is missing
audio, not missing code.

Four ways to get it, for **18 places** (11 experiences + 7 destinations):

| Approach | What it costs | What it gives |
|---|---|---|
| **A. Generated clips** (Higgsfield, already used for the TTS narration placeholders) | 18 clips to make, review and credit; each a few hundred KB on device; generative models produce short takes, so seamless looping needs a crossfade — which `AmbienceController` already does | Fastest coverage. Per-place character. Needs an owner listen and a credits line each. |
| **B. Procedural drone in-engine** | One small component; a few detuned oscillators and a filtered noise bed on `OnAudioFilterRead`; no audio assets at all | **Never repeats**, zero download weight, and each place can be given its own seed and colour. The obvious risk is that a synthesised drone sounds synthetic. |
| **C. Sonified from the plate** | B, plus a mapping from the nebula's own brightness profile to the drone's spectrum; the volume builder already reads every plate | The sound of *that* object, derived rather than chosen — the same principle the volume geometry already follows. Results are unpredictable until heard. |
| **D. Library / NASA sonifications** | Licence review per asset; NASA material is generally free to use but has to be checked and credited individually | Real, and not ours. Least distinctive. |

**Recommendation: B, seeded per place, with C as the seeding rule for the seven nebulae, and A kept as the
stopgap** if something audible is wanted before the component exists.

The reasoning: 18 seamless beds is a real authoring and memory cost for audio the player is meant to stop
noticing, and a looping clip in a place someone stands in for minutes will be heard to loop. A drone that is
generated never does. Seeding it from the plate costs almost nothing extra once the component exists, and it
keeps the same promise the rest of this feature makes — that what the player is surrounded by came from the
real object rather than from taste.

Two constraints either way: it sits **under narration** (the music already ducks 55 %, and ambience should
duck with it), and it must be **quiet enough to be missed** — an ambience bed that is noticed on purpose is
too loud.
