// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Generates a galaxy from a <see cref="GalaxyProfile"/>: three baked star sets, three materials, a
    /// grabbable content prefab, and the one field on the galaxy's <c>ExperienceModule</c> that makes its tile
    /// or its map tag open something.
    ///
    /// <para><b>Nothing here is invented.</b> The galaxy is the existing <c>SpiralGalaxy</c> + <c>DrawStars</c>
    /// + <c>StarsData</c> path that draws the Milky Way (Technical Overview 7.3). The Milky Way is the same
    /// three layers in the same draw order — a cloud layer for the diffuse glow, a "negative" layer for dust,
    /// and the star layer — so this is the Milky Way's own recipe re-shaped per galaxy, not a second
    /// renderer.</para>
    ///
    /// <para><b>What the baked data actually controls.</b> <c>SpiralGalaxy</c> only generates stars when its
    /// <c>bakedStars</c> is empty; with an asset assigned, every generation-time field (<c>EllipseCount</c>,
    /// <c>SpiralRotation</c>, the curves, the gradients, the star groups) is dead weight at run time. The
    /// shader reconstructs each star's position from the baked <c>curveOffset</c>, <c>ellipseOffset</c>,
    /// <c>ellipseDistance</c> and <c>yOffset</c> plus exactly two live fields, <c>XRadii</c> and <c>ZRadii</c>
    /// (they arrive as <c>_EllipseSize.xy</c>; see <c>cginc/StarPositionCompute.cginc</c>). Those two therefore
    /// have to match what was baked or the galaxy changes shape. The generation-time fields are still written,
    /// to the same values the bake used, so that the design is legible in the Inspector and so that a lost
    /// <c>StarsData</c> asset degrades to something similar rather than to an empty curve evaluating to zero
    /// and a galaxy of nothing.</para>
    ///
    /// <para><b>A prefab, not a scene.</b> The project has a standing rule against adding scenes. A galaxy is
    /// one object with no destinations and no layouts, and a scene would buy only load time and another thing
    /// to keep in sync. CS-036 and CS-041 made the duplicated-scene approach unnecessary.</para>
    ///
    /// <para><b>Departures CS-041 recorded as "a bug if copied literally".</b> <c>ManipulationHandler</c> and
    /// <see cref="ScaleLimits"/> sit on the <i>same</i> GameObject as the collider and the
    /// <c>GEInteractable</c>: the handler finds its scale constraint with <c>GetComponent</c>, and the
    /// constraint measures the transform it is on. Splitting them silently removes the limits.</para>
    ///
    /// <para><b>No grow-in, no panel, no reset here.</b> <c>ExperienceDirector.GrowIn</c> already scales a
    /// spawned content prefab from zero with a cubic ease-out, which is the GDD's "grows in from a point"; a
    /// second one would fight it. The director also puts up the module's own <c>ScenePanelCopy</c> to the right
    /// of the content, so an <c>InfoPanel</c> in the prefab would be a second copy of the same words. And
    /// <see cref="FreePlacementAnchor"/> rather than <c>PostManipulationResetter</c>, because a galaxy the
    /// player has arranged must stay where it was put.</para>
    ///
    /// <para><b>Point budget.</b> Andromeda bakes 17,200 point sprites against the Milky Way's 18,720 and the
    /// 400,000 a whole frame is allowed (Technical Overview 7.4) — about 4.3 % each. A dozen galaxies at that
    /// size would be roughly 206,000 points, over half the frame ceiling, which is why
    /// <c>ExperienceDirector</c> opens one destination at a time and why that stays true: the budget here is
    /// per-galaxy on purpose, and a profile is free to spend more than Andromeda does only because it will
    /// never be on screen beside another one. The per-galaxy figure and its share of the ceiling are printed
    /// by every build.</para>
    ///
    /// <para>Re-running replaces the <c>StarsData</c> assets, the materials and the prefab in place, so scene
    /// and asset references survive. The star data is regenerated from fixed seeds, so a re-run with no
    /// parameter change produces byte-identical assets and an empty diff — which is the only thing that makes a
    /// generated multi-megabyte YAML array reviewable at all.</para>
    /// </summary>
    public static class GalaxyBuilder
    {
        // ---------- where things live (the same folders for every galaxy; the file names carry the id)

        private const string StarDataFolder = "Assets/scriptable_objects/star_data_scriptabe_objects";
        private const string MaterialFolder = "Assets/materials/galaxy_materials";
        private const string PrefabFolder = "Assets/prefabs/experiences";
        private const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";

        // The three star shaders the Milky Way already uses. Loaded by path, not by Shader.Find: a shader that
        // failed to compile still resolves by name to the error shader, and the prefab would ship pointing at it.
        public const string StarShaderPath = "Assets/shaders/spiral_stars_shader.shader";           // Galaxy/Stars
        public const string CloudShaderPath = "Assets/shaders/spiral_stars_cloud_shader.shader";    // Galaxy/StarClouds
        public const string DustShaderPath = "Assets/shaders/spiral_stars_negative_shader.shader";  // Galaxy/StarsNeg

        // Tiles in stars_small_atlas.tga, the same four the Milky Way's star groups use.
        private static readonly Vector2 TilePoint = new Vector2(0.5f, 0f);
        private static readonly Vector2 TilePuffy = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TileRayed = new Vector2(0f, 0.5f);
        private static readonly Vector2 TileCross = new Vector2(0f, 0f);

        // ---------- entry points

        /// <summary>
        /// Builds every profile in <see cref="GalaxyProfiles"/>. Carries on past a galaxy that fails, so one
        /// bad profile does not hide the state of the rest, and says at the end which ones did not land.
        /// </summary>
        [MenuItem("Cosmic Simulation/Build All Galaxy Content")]
        public static void BuildAll()
        {
            if (RefuseDuringPlay())
            {
                return;
            }

            var profiles = GalaxyProfiles.All();
            var built = new List<string>();
            var failed = new List<string>();
            var points = 0;

            foreach (var profile in profiles)
            {
                if (Build(profile))
                {
                    built.Add(profile.Id);
                    points += profile.TotalPoints;
                }
                else
                {
                    failed.Add(profile.Id);
                }
            }

            Debug.Log(
                $"GalaxyBuilder: built {built.Count} of {profiles.Length} galaxies ({string.Join(", ", built)})" +
                (failed.Count == 0 ? "." : $"; failed: {string.Join(", ", failed)} - see the errors above.") + "\n" +
                $"  {points:N0} point sprites across all of them, which would be {points / 4000f:F1}% of the " +
                "400,000-point frame ceiling (Technical Overview 7.4) if they were ever open at once. They are " +
                "not: ExperienceDirector opens one destination at a time, so the number that matters is the " +
                "largest single galaxy, not this sum.");
        }

        /// <summary>
        /// Builds one galaxy by id. The scripted entry point — a relay <c>RunCommand</c> can call
        /// <c>GalaxyBuilder.Build("andromeda")</c> without a menu item existing for it.
        /// </summary>
        public static bool Build(string id)
        {
            var profile = GalaxyProfiles.Find(id);
            if (profile == null)
            {
                Debug.LogError($"GalaxyBuilder: no galaxy profile with id '{id}'. Known: {GalaxyProfiles.Ids()}. " +
                               "Nothing was written.");
                return false;
            }

            return Build(profile);
        }

        /// <summary>
        /// Builds one galaxy. Returns false and writes nothing on any problem it can see up front; once it
        /// starts writing, a failure says exactly how far it got.
        /// </summary>
        public static bool Build(GalaxyProfile profile)
        {
            if (RefuseDuringPlay())
            {
                return false;
            }

            if (profile == null)
            {
                Debug.LogError("GalaxyBuilder: null profile. Nothing was written.");
                return false;
            }

            var label = string.IsNullOrEmpty(profile.DisplayName) ? profile.Id : profile.DisplayName;

            // Validated before anything is loaded, so an unimplemented GalaxyKind or a mistyped radius is a
            // refusal that names the field rather than a half-built galaxy on disk.
            var problems = profile.Problems();
            if (problems.Length > 0)
            {
                Debug.LogError($"GalaxyBuilder: the profile for '{label}' cannot be built, so nothing was " +
                               "written:\n  " + string.Join("\n  ", problems));
                return false;
            }

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(profile.ModulePath);
            if (module == null)
            {
                Debug.LogError($"GalaxyBuilder: no experience module at {profile.ModulePath} for '{label}'. " +
                               "Run Cosmic Simulation -> Import Copy first. Nothing was written.");
                return false;
            }

            var specs = profile.Layers;

            // All layers or none. A galaxy missing its dust or its glow is a look bug that nobody would trace
            // back to a shader that did not import, whereas a refusal names the file.
            var missing = new List<string>();
            var shaders = new Shader[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                shaders[i] = AssetDatabase.LoadAssetAtPath<Shader>(specs[i].ShaderPath);
                if (shaders[i] == null)
                {
                    missing.Add($"{specs[i].Id} <- {specs[i].ShaderPath}");
                }
            }

            if (missing.Count > 0)
            {
                Debug.LogError("GalaxyBuilder: these shaders did not load, so nothing was written and " +
                               $"'{module.Id}' is unchanged:\n  " + string.Join("\n  ", missing) +
                               "\n  They must import without errors first.");
                return false;
            }

            Directory.CreateDirectory(StarDataFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            // Anything the run did not do, carried all the way into the summary line. A skip that is only a
            // warning higher up the console is a skip nobody sees.
            var skipped = new List<string>();

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
            {
                skipped.Add($"the sprite atlas ({AtlasPath} is missing, so every layer will draw white squares; " +
                            "the Milky Way uses the same texture, so check it is still there)");
                Debug.LogWarning($"GalaxyBuilder: no star sprite atlas at {AtlasPath}; the layers will draw " +
                                 "white squares. The Milky Way uses this same texture, so check it is still there.");
            }

            // ---- bake the star sets, and measure what came out

            var stars = new GalaxyExplorer.StarVertDescriptor[specs.Length][];
            var data = new GalaxyExplorer.StarsData[specs.Length];
            var measurements = new Measurement[specs.Length];

            var envelopeRadius = 0f; // the widest a star ever gets from the centre, over a whole revolution
            var total = 0;

            for (var i = 0; i < specs.Length; i++)
            {
                stars[i] = Generate(profile, specs[i]);
                total += stars[i].Length;

                measurements[i] = Measure(profile, stars[i]);
                envelopeRadius = Mathf.Max(envelopeRadius, measurements[i].EnvelopeRadius);

                var path = profile.StarDataPath(StarDataFolder, specs[i].Id);
                data[i] = WriteStars(path, stars[i]);
                if (data[i] == null)
                {
                    Debug.LogError($"GalaxyBuilder: could not write {path}; nothing else was changed and " +
                                   $"'{module.Id}' is unchanged, so it still opens nothing.");
                    return false;
                }
            }

            // ---- the galaxy node's scale, so the profile's width is exact rather than asserted

            if (envelopeRadius <= 0f)
            {
                Debug.LogError($"GalaxyBuilder: '{label}' measures zero across, which means the layer " +
                               "parameters produced nothing. Nothing further was written.");
                return false;
            }

            var galaxyScale = profile.WidthMetres / (envelopeRadius * 2f);

            // ---- materials

            var materials = new Material[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                materials[i] = WriteMaterial(profile, specs[i], shaders[i], atlas);
            }

            // ---- prefab

            var prefab = WritePrefab(profile, data, materials, galaxyScale);
            if (prefab == null)
            {
                Debug.LogError($"GalaxyBuilder: could not save {PrefabFolder}/{profile.PrefabName}.prefab. " +
                               $"The star data and materials were written but '{module.Id}' is unchanged, so it " +
                               "still opens nothing.");
                return false;
            }

            // ---- the one line that makes the tile work (CS-036)

            var previous = module.ContentPrefab;
            var so = new SerializedObject(module);
            var contentPrefab = Find(so, "ContentPrefab");
            if (contentPrefab == null)
            {
                // Everything else is on disk by now, so this has to say so rather than throw: the assets are
                // fine and only the wiring is missing.
                Debug.LogError("GalaxyBuilder: ExperienceModule has no 'ContentPrefab' field any more, so " +
                               $"'{module.Id}' could not be wired. The star data, the materials and " +
                               $"{PrefabFolder}/{profile.PrefabName}.prefab were written; assign it by hand or " +
                               "fix this builder. Until then the tile still does nothing.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return false;
            }

            contentPrefab.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ---- report, measured rather than restated

            var report = new string[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var m = measurements[i];

                // DrawStars multiplies worldSpaceScale by the galaxy's lossy scale and then by a constant 0.4
                // on the non-downscaled path (Clamp01(4 * max(.1, |camDir.y| * .1)) can only ever come out at
                // 0.4), and the cloud shader doubles it again. Reported in millimetres of real width, because
                // that is the number that decides whether a star is a pixel or a blob on the headset.
                var doubling = specs[i].Role == GalaxyLayerRole.Clouds ? 2f : 1f;
                var unit = specs[i].WorldSpaceScale * galaxyScale * 0.4f * doubling * 1000f;

                report[i] =
                    $"{specs[i].Id}: {stars[i].Length:N0} points " +
                    $"({profile.ArmCount} arms x {specs[i].Ellipses} ellipses x {specs[i].StarsPerEllipse}), " +
                    $"radius {m.MinRadius * galaxyScale * 100f:F1}-{m.EnvelopeRadius * galaxyScale * 100f:F1} cm, " +
                    $"half thickness {m.HalfThickness * galaxyScale * 1000f:F1} mm " +
                    $"({m.HalfThickness / Mathf.Max(0.0001f, m.EnvelopeRadius) * 100f:F1}% of its radius), " +
                    $"sprite half size {m.MinSize * unit:F2}-{m.MaxSize * unit:F2} mm";
            }

            Debug.Log(
                $"GalaxyBuilder: '{module.Id}' now opens {PrefabFolder}/{profile.PrefabName}.prefab" +
                (previous == null
                    ? " (it had no ContentPrefab at all, so ExperienceDirector.Switch refused it outright - " +
                      "CS-036)."
                    : $" (it previously opened '{previous.name}').") + "\n" +
                $"  {total:N0} point sprites in {specs.Length} layers = {total * 6:N0} vertices and " +
                $"{total * 2:N0} triangles per view, before Android multiview doubles the vertex stage. " +
                $"Milky Way, the same renderer: 18,720. Quest 3 frame ceiling: 400,000 point sprites " +
                $"(Technical Overview 7.4), so this is {total / 4000f:F1}% of it.\n" +
                $"  {string.Join("\n  ", report)}\n" +
                $"  measured envelope radius {envelopeRadius:F4} at unit scale -> galaxy node scaled " +
                $"{galaxyScale:F4}, giving {envelopeRadius * 2f * galaxyScale:F3} m across against the " +
                $"profile's {profile.WidthMetres:F2} m.\n" +
                $"  {profile.ArmCount} arms {profile.ArmSpacingDegrees:F0} deg apart, winding " +
                $"{profile.WindingDegrees:F0} deg over the disc's radial range, which is a pitch angle of about " +
                $"{profile.PitchAngleDegrees:F1} deg.\n" +
                $"  tilt {profile.DiscTiltDegrees:F0} deg plus {profile.DiscYawDegrees:F0} deg yaw, which " +
                $"presents the disc about {profile.PresentedInclinationDegrees:F0} deg off face-on to a player " +
                "looking along the content root's forward axis (0 deg would be a flat circle, 90 deg " +
                "edge-on).\n" +
                $"  grab sphere radius {profile.WidthMetres * 0.5f * profile.GrabRadiusFraction:F2} m; two " +
                $"hands move, rotate and scale it between {profile.MinScaleMetres:F1} m and " +
                $"{profile.MaxScaleMetres:F1} m.\n" +
                $"  star data: {profile.StarDataPath(StarDataFolder, "<layer>")}; " +
                $"materials: {profile.MaterialPath(MaterialFolder, "<layer>")}.\n" +
                $"  skipped: {(skipped.Count == 0 ? "nothing" : string.Join("; ", skipped))}. " +
                "The grow-in, the panel and the ambience belong to ExperienceDirector, not to this prefab.");

            return true;
        }

        private static bool RefuseDuringPlay()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            Debug.LogError("GalaxyBuilder: leave play mode first.");
            return true;
        }

        // ---------- star generation
        //
        // The shader rebuilds a star's position from four baked numbers (curveOffset, ellipseOffset,
        // ellipseDistance, yOffset) and two live ones (XRadii, ZRadii); see
        // Assets/shaders/cginc/StarPositionCompute.cginc, which is the definition these four are written
        // against. Measure() below reads them back in the same terms rather than trusting the constants.
        //
        // Every draw here is deterministic: one System.Random per layer, seeded from the spec, consumed in a
        // fixed order. A re-run with no parameter change therefore rewrites byte-identical assets and shows an
        // empty diff, which is the only way a generated 2 MB YAML file is reviewable at all. Any change to the
        // order of the Next() calls below rewrites every galaxy's star data, so treat that order as an
        // interface, not an implementation detail.

        private static GalaxyExplorer.StarVertDescriptor[] Generate(GalaxyProfile profile, GalaxyLayerSpec spec)
        {
            switch (profile.Kind)
            {
                case GalaxyKind.SpiralArms:
                    return GenerateSpiral(profile, spec);

                default:
                    // Unreachable: GalaxyProfile.Problems() refuses an unimplemented kind before anything is
                    // written. Kept as the backstop for a future caller that skips the validation.
                    throw new System.NotSupportedException(
                        $"GalaxyBuilder has no generator for GalaxyKind.{profile.Kind} " +
                        $"(profile '{profile.Id}'). Only SpiralArms is implemented.");
            }
        }

        private static GalaxyExplorer.StarVertDescriptor[] GenerateSpiral(
            GalaxyProfile profile, GalaxyLayerSpec spec)
        {
            var rng = new System.Random(spec.Seed);
            var count = spec.Ellipses * spec.StarsPerEllipse * profile.ArmCount;
            var stars = new GalaxyExplorer.StarVertDescriptor[count];
            var n = 0;

            for (var arm = 0; arm < profile.ArmCount; arm++)
            {
                var armOffsetDegrees = profile.ArmSpacingDegrees * arm;

                for (var i = 0; i < spec.Ellipses; i++)
                {
                    // Progression from the centre outwards. Ellipses are evenly spaced in radius and carry the
                    // same number of stars each, so surface density falls as 1/r — which is what puts most of
                    // the light in the middle. The Milky Way is built the same way.
                    var p = i / (float)spec.Ellipses;

                    var offsetRadians = (armOffsetDegrees + profile.WindingDegrees * p) * Mathf.Deg2Rad;
                    var baseDistance = Mathf.Lerp(spec.MinEllipseScale, spec.MaxEllipseScale, p);

                    for (var j = 0; j < spec.StarsPerEllipse; j++)
                    {
                        stars[n++] = MakeStar(profile, spec, rng, p, offsetRadians, baseDistance);
                    }
                }
            }

            return stars;
        }

        private static GalaxyExplorer.StarVertDescriptor MakeStar(
            GalaxyProfile profile, GalaxyLayerSpec spec, System.Random rng,
            float p, float offsetRadians, float baseDistance)
        {
            var distance = baseDistance * Mathf.Lerp(spec.Fuzz.x, spec.Fuzz.y, Next(rng));

            var thickness = Mathf.Lerp(spec.ThicknessAtCentre, spec.ThicknessAtRim, Mathf.SmoothStep(0f, 1f, p))
                            * spec.YRange;

            var shade = Shade(profile, spec, rng, p);

            return new GalaxyExplorer.StarVertDescriptor
            {
                // Where the star sits on its ellipse. The shader adds _Age to this, which is what makes the
                // whole galaxy turn without anything touching a transform — so a grab can never fight the spin.
                curveOffset = Next(rng) * Mathf.PI * 2f,
                ellipseOffset = offsetRadians,
                ellipseDistance = distance,
                yOffset = (Next(rng) * 2f - 1f) * thickness,
                color = new Vector3(shade.Colour.r, shade.Colour.g, shade.Colour.b),
                uv = shade.Tile,
                size = shade.Size,
                random = Next(rng),
            };
        }

        private struct Shading
        {
            public Color Colour;
            public float Size;
            public Vector2 Tile;
        }

        /// <summary>
        /// Colour, sprite size and atlas tile for one point.
        ///
        /// <para>The per-role formulas below are the shared grand-design recipe, not per-galaxy tuning: what a
        /// profile varies is the ramps they sample, the radial range they are spread over and the dust ring's
        /// peak. The literal probabilities (86 % plain points, 7 % puffy, 2.5 % ionised knots, 3.5 % blue
        /// giants) and the radii the last two are gated to are deliberately <i>not</i> per-galaxy: they are a
        /// population mix nobody has a published number for, so exposing them would invite guessing.</para>
        /// </summary>
        private static Shading Shade(
            GalaxyProfile profile, GalaxyLayerSpec spec, System.Random rng, float p)
        {
            var palette = profile.Palette;

            switch (spec.Role)
            {
                case GalaxyLayerRole.Clouds:
                    // One puffy blob per point. Small and dense in the middle, where hundreds of them overlap
                    // and the additive blend saturates to a white core; larger, warmer and fainter further out,
                    // where they become the bulge's haze.
                    return new Shading
                    {
                        Colour = Ramp(palette.Clouds, p) * Mathf.Lerp(1f, 0.45f, p),
                        Size = Mathf.Lerp(1.6f, 3.4f, p) * Mathf.Lerp(0.85f, 1.15f, Next(rng)),
                        Tile = TilePuffy,
                    };

                case GalaxyLayerRole.Dust:
                {
                    // The band already starts at MinEllipseScale; this puts the weight of it at the profile's
                    // DustRingPeak and lets it fade at both edges, so the lane has a shape rather than a hard
                    // inner rim.
                    var fromPeak = (p - spec.DustRingPeak) / Mathf.Max(0.0001f, spec.DustRingWidth);
                    var bump = Mathf.Exp(-fromPeak * fromPeak);
                    var weight = Mathf.Lerp(0.35f, 1f, bump);

                    return new Shading
                    {
                        Colour = Ramp(palette.Dust, p) * weight,
                        Size = Mathf.Lerp(1.4f, 3f, Next(rng)) * weight,
                        Tile = TilePuffy,
                    };
                }

                default:
                {
                    // The disc. Mostly plain points, with three small populations that give an arm texture:
                    // puffy stars, warm ionised knots out in the ring, and blue giants which are the actual
                    // reason the outer arms read blue.
                    var falloff = Mathf.Lerp(1f, 0.55f, p);
                    var roll = Next(rng);

                    if (roll < 0.86f)
                    {
                        return new Shading
                        {
                            Colour = Ramp(palette.Disc, p),
                            Size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff,
                            Tile = TilePoint,
                        };
                    }

                    if (roll < 0.93f)
                    {
                        return new Shading
                        {
                            Colour = Ramp(palette.Disc, p),
                            Size = Mathf.Lerp(0.9f, 1.5f, Next(rng)) * falloff,
                            Tile = TilePuffy,
                        };
                    }

                    if (roll < 0.965f)
                    {
                        // Star-forming knots belong to the ring, not to the bulge.
                        return p > 0.4f
                            ? new Shading
                            {
                                Colour = palette.HiiKnot,
                                Size = Mathf.Lerp(1.4f, 2.3f, Next(rng)) * falloff,
                                Tile = TileRayed,
                            }
                            : new Shading
                            {
                                Colour = Ramp(palette.Disc, p),
                                Size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff,
                                Tile = TilePoint,
                            };
                    }

                    return p > 0.3f
                        ? new Shading
                        {
                            Colour = palette.BlueGiant,
                            Size = Mathf.Lerp(1.1f, 1.9f, Next(rng)) * falloff,
                            Tile = TileCross,
                        }
                        : new Shading
                        {
                            Colour = Ramp(palette.Disc, p),
                            Size = Mathf.Lerp(0.9f, 1.5f, Next(rng)) * falloff,
                            Tile = TilePuffy,
                        };
                }
            }
        }

        private static float Next(System.Random rng) => (float)rng.NextDouble();

        private static Color Ramp((float t, Color c)[] ramp, float d)
        {
            d = Mathf.Clamp01(d);

            for (var i = 1; i < ramp.Length; i++)
            {
                if (d <= ramp[i].t)
                {
                    var span = Mathf.Max(0.00001f, ramp[i].t - ramp[i - 1].t);
                    return Color.Lerp(ramp[i - 1].c, ramp[i].c, (d - ramp[i - 1].t) / span);
                }
            }

            return ramp[ramp.Length - 1].c;
        }

        // ---------- measuring what was generated

        private struct Measurement
        {
            /// <summary>Largest distance from the centre any star reaches over a whole revolution.</summary>
            public float EnvelopeRadius;

            public float MinRadius;
            public float HalfThickness;
            public float MinSize;
            public float MaxSize;
        }

        /// <summary>
        /// Measures the bake rather than restating the constants that produced it, so that a mistake in the
        /// parameters shows up in the build log as a wrong number instead of passing silently.
        ///
        /// The envelope is time-invariant and exact: the shader animates <c>curveOffset</c>, so over one
        /// revolution a star sweeps its whole ellipse, whose furthest point from the centre is
        /// <c>ellipseDistance * max(XRadii, ZRadii)</c> whatever the ellipse's own rotation is.
        /// </summary>
        private static Measurement Measure(GalaxyProfile profile, GalaxyExplorer.StarVertDescriptor[] stars)
        {
            var result = new Measurement { MinRadius = float.MaxValue, MinSize = float.MaxValue };
            var major = Mathf.Max(profile.XRadii, profile.ZRadii);
            var minor = Mathf.Min(profile.XRadii, profile.ZRadii);

            foreach (var star in stars)
            {
                result.EnvelopeRadius = Mathf.Max(result.EnvelopeRadius, star.ellipseDistance * major);
                result.MinRadius = Mathf.Min(result.MinRadius, star.ellipseDistance * minor);
                result.HalfThickness = Mathf.Max(result.HalfThickness, Mathf.Abs(star.yOffset));
                result.MinSize = Mathf.Min(result.MinSize, star.size);
                result.MaxSize = Mathf.Max(result.MaxSize, star.size);
            }

            if (stars.Length == 0)
            {
                result.MinRadius = 0f;
                result.MinSize = 0f;
            }

            return result;
        }

        // ---------- assets

        /// <summary>
        /// Writes a <c>StarsData</c> asset, overwriting the array of an existing one rather than recreating the
        /// asset, so its GUID and every reference to it survive a re-run.
        /// </summary>
        private static GalaxyExplorer.StarsData WriteStars(string path, GalaxyExplorer.StarVertDescriptor[] stars)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GalaxyExplorer.StarsData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GalaxyExplorer.StarsData>();
                asset.stars = stars;
                AssetDatabase.CreateAsset(asset, path);
                return AssetDatabase.LoadAssetAtPath<GalaxyExplorer.StarsData>(path);
            }

            asset.stars = stars;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// One material per layer per galaxy. Each galaxy gets its own rather than sharing the Milky Way's
        /// three, so that retuning one galaxy cannot move another — they run the same shaders on purpose, and
        /// those shaders are already reachable from <c>galaxy_view_scene</c> through <c>milky_way_prefab</c>, so
        /// nothing here needs adding to Always Included Shaders.
        ///
        /// Almost every property is overwritten each frame by <c>DrawStars.Update</c> from the
        /// <c>SpiralGalaxy</c> component (<c>_Color</c>, <c>_WSScale</c>, <c>_Age</c>, <c>_EllipseSize</c>,
        /// <c>_TransitionAlpha</c>); what is genuinely authored here is the sprite atlas. The rest is written
        /// so the material previews as something rather than as black.
        /// </summary>
        private static Material WriteMaterial(
            GalaxyProfile profile, GalaxyLayerSpec spec, Shader shader, Texture2D atlas)
        {
            var path = profile.MaterialPath(MaterialFolder, spec.Id);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = $"{profile.Id}_{spec.Id}_material" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (atlas != null)
            {
                material.mainTexture = atlas;
            }

            // Procedural draws, never an instanced batch: leaving instancing on would let Unity pick the
            // INSTANCING_ON variant, where unity_ObjectToWorld comes from an array this draw never fills.
            material.enableInstancing = false;

            material.SetColor("_Color", spec.Tint * spec.TintMultiplier);
            material.SetFloat("_WSScale", spec.WorldSpaceScale);
            material.SetFloat("_TransitionAlpha", 1f);
            material.SetVector("_TxScale", new Vector4(1.5f, 1.8f, 0f, 0f)); // as the Milky Way's three are set

            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- prefab

        private static GameObject WritePrefab(
            GalaxyProfile profile, GalaxyExplorer.StarsData[] data, Material[] materials, float galaxyScale)
        {
            var specs = profile.Layers;
            var root = new GameObject(profile.PrefabName);

            // The parts the player touches, all on one GameObject. CS-041: ManipulationHandler looks for its
            // scale constraint with GetComponent and ScaleLimits measures the transform it is on, so the
            // collider, the interactable, the handler and the limits cannot be split across the hierarchy.
            var grab = root.AddComponent<SphereCollider>();
            grab.radius = profile.WidthMetres * 0.5f * profile.GrabRadiusFraction;

            root.AddComponent<GEInteractable>();

            var manipulation = root.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            SetEnum(manipulationSo, "manipulationType",
                (int)ManipulationHandler.HandMovementType.OneAndTwoHanded);
            // "Move, tilt, scale with hands". Unlike the nebula overlays — which billboard themselves and would
            // overwrite any rotation the player set on the next frame — rotation is wanted here, so this is the
            // only mode of the six that covers all three verbs.
            SetEnum(manipulationSo, "twoHandedManipulationType",
                (int)ManipulationHandler.TwoHandedManipulation.MoveRotateScale);
            SetBool(manipulationSo, "allowFarManipulation", true);
            // Grabbed directly rather than pulled by a ForceSolver, so nothing else plays the grab and release
            // sounds for it.
            SetBool(manipulationSo, "playGrabSounds", true);
            manipulationSo.ApplyModifiedPropertiesWithoutUndo();

            // Without this the handler above is unreachable: hand and ray selects are routed to
            // IGEPointerHandler, which ManipulationHandler deliberately does not implement, and there is no
            // ForceSolver here to forward them (CS-106).
            root.AddComponent<ManipulationPointerRouter>();

            // And without this one the galaxy could be moved, tilted and scaled but never brought back: Restore
            // reaches bodies through their ForceSolver or their LayoutRig, and this has neither (CS-107). Home is
            // the pose the director spawns it in, recorded before the grow-in.
            root.AddComponent<FreePlacementAnchor>();

            var limits = root.AddComponent<ScaleLimits>(); // requires the handler above, so it is added after
            var limitsSo = new SerializedObject(limits);
            // None of Body (5 cm - 3 m), Model (40 cm - 4 m) or Nebula (30 cm - 2 m) is a galaxy's range, and
            // picking the nearest would quietly change the spec, so the metres are stated.
            SetEnum(limitsSo, "kind", (int)ScaleLimits.Kind.Custom);
            SetFloat(limitsSo, "customMinMetres", profile.MinScaleMetres);
            SetFloat(limitsSo, "customMaxMetres", profile.MaxScaleMetres);
            // There is no Renderer to measure: the galaxy is a ComputeBuffer drawn by a command buffer. Stating
            // the width is also what lets the limits survive being asked during the director's grow-in, which
            // starts at local scale zero.
            SetFloat(limitsSo, "authoredMetresAtUnitScale", profile.WidthMetres);
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            // The disc itself, under the grabbable root so that the tilt is an offset the player's rotation
            // adds to rather than something a grab immediately destroys.
            var galaxy = new GameObject(profile.GalaxyNodeName);
            galaxy.transform.SetParent(root.transform, false);
            galaxy.transform.localRotation =
                Quaternion.Euler(profile.DiscTiltDegrees, profile.DiscYawDegrees, 0f);
            galaxy.transform.localScale = Vector3.one * galaxyScale;

            // Added in draw order: SpiralGalaxy.Start waits `index` frames before creating its DrawStars, and
            // DrawStars attaches the layers' command buffers in creation order.
            for (var i = 0; i < specs.Length; i++)
            {
                Configure(galaxy.AddComponent<GalaxyExplorer.SpiralGalaxy>(),
                    profile, specs[i], data[i], materials[i]);
            }

            var path = $"{PrefabFolder}/{profile.PrefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// Sets up one <c>SpiralGalaxy</c> layer.
        ///
        /// The fields split in two. <c>XRadii</c>, <c>ZRadii</c>, the tint, <c>worldSpaceScale</c>,
        /// <c>velocityMultiplier</c>, <c>baseMaterial</c> and <c>bakedStars</c> are live: <c>DrawStars</c> feeds
        /// them to the shader every frame. Everything else — the ellipse counts, the spiral rotation, the
        /// curves, the gradient, the star groups — is only ever read by <c>SpiralGalaxy.CreateStarsContent</c>,
        /// which does not run at all while <c>bakedStars</c> holds an asset. Those are written anyway, to the
        /// values this bake used, for two reasons: they are how the design reads in the Inspector, and if the
        /// baked asset is ever lost the fallback then produces something similar instead of evaluating an empty
        /// AnimationCurve to zero and generating a galaxy with no stars in it.
        /// </summary>
        private static void Configure(
            GalaxyExplorer.SpiralGalaxy layer, GalaxyProfile profile, GalaxyLayerSpec spec,
            GalaxyExplorer.StarsData data, Material material)
        {
            // --- live
            layer.XRadii = profile.XRadii;
            layer.ZRadii = profile.ZRadii;
            layer.bakedStars = data;
            layer.lastBakedStarsCount = data != null && data.stars != null ? data.stars.Length : 0;
            layer.baseMaterial = material;
            layer.worldSpaceScale = spec.WorldSpaceScale;
            layer.velocityMultiplier = profile.VelocityMultiplier;
            layer.tint = spec.Tint;
            layer.tintMult = spec.TintMultiplier;
            // Flat: the tint must not change as the player walks round the galaxy, or a two-handed turn would
            // look like it was also dimming the thing.
            layer.verticalTintMultiplier = Vector2.one;
            layer.index = spec.DrawIndex;
            layer.TransitionAlpha = 1f;

            // Straight into the eye buffer, never into the downscaled targets. The headset forces this anyway
            // (DrawStars turns the downscaled path off whenever an XR device is active, because the stereo eye
            // textures cannot feed the plain 2D render textures), and matching that on the desktop means the
            // two platforms show the same galaxy. It also keeps this content from blitting over the whole
            // camera target, which is what the Milky Way's compose pass does inside its own scene.
            layer.renderIntoDownscaledTarget = false;
            layer.screenComposeMaterial = null;
            layer.referenceQuad = null;

            // Describes the layer truthfully. Inert while renderIntoDownscaledTarget is false — it only selects
            // a different sprite-size falloff on the compose path — but wrong if it said otherwise.
            layer.isShadow = spec.IsShadow;

            // --- generation-time only, from here down. (FuzzySideScale is the one borderline case: DrawStars
            // does push it to the material every frame, but none of the three star shaders declares
            // _FuzzySideScale, so nothing reads it.)
            layer.EllipseCount = spec.Ellipses;
            layer.StarsPerEllipse = spec.StarsPerEllipse;
            layer.MinEllipseScale = spec.MinEllipseScale;
            layer.MaxEllipseScale = spec.MaxEllipseScale;
            layer.SpiralRotation = profile.WindingDegrees;
            // The fallback path only ever models two arms, so a three- or four-armed profile degrades to two
            // here. That is a fallback for a lost asset, not the shipping picture, and saying so beats writing
            // a number that pretends otherwise.
            layer.enableSecondArm = profile.ArmCount > 1;
            layer.secondArmStartOffsetDeg = profile.ArmSpacingDegrees;
            layer.YRange = spec.YRange;
            layer.FuzzySideScale = spec.Fuzz;

            layer.centerToRimStarSize = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, spec.Role == GalaxyLayerRole.Stars ? 0.55f : 1f));

            layer.centerToRimVerticalOffset = new AnimationCurve(
                new Keyframe(0f, spec.ThicknessAtCentre),
                new Keyframe(1f, spec.ThicknessAtRim));

            layer.centerToRimGradient = GradientFrom(profile.Palette.For(spec.Role));

            layer.StarGroups = new List<GalaxyExplorer.SpiralGalaxy.StarDistributionGroup>
            {
                new GalaxyExplorer.SpiralGalaxy.StarDistributionGroup
                {
                    GroupName = spec.Id,
                    Percentage = 100f,
                    UseColorRange = false,
                    ColorRange = GradientFrom(profile.Palette.For(spec.Role)),
                    UVOffset = spec.Role == GalaxyLayerRole.Stars ? TilePoint : TilePuffy,
                    SizeMultiplierRange = spec.Role == GalaxyLayerRole.Stars
                        ? new Vector2(0.62f, 1f)
                        : new Vector2(1.6f, 3.4f),
                    SizeIsAbsolute = spec.Role != GalaxyLayerRole.Stars,
                    RandomEllipseScaleOffset = 0f,
                },
            };
        }

        /// <summary>A <see cref="Gradient"/> holding the same colours as one of the palette's ramps.</summary>
        private static Gradient GradientFrom((float t, Color c)[] ramp)
        {
            var colourKeys = new GradientColorKey[Mathf.Min(ramp.Length, 8)];
            for (var i = 0; i < colourKeys.Length; i++)
            {
                colourKeys[i] = new GradientColorKey(ramp[i].c, ramp[i].t);
            }

            var gradient = new Gradient();
            gradient.SetKeys(colourKeys, new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });

            return gradient;
        }

        // ---------- serialized property helpers
        //
        // Named, not indexed: a renamed private field then shows up as one warning naming the field, instead of
        // a null reference in the middle of a half-built prefab.

        private static void SetEnum(SerializedObject so, string name, int value)
        {
            var property = Find(so, name);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var property = Find(so, name);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var property = Find(so, name);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static SerializedProperty Find(SerializedObject so, string name)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning($"GalaxyBuilder: '{so.targetObject.GetType().Name}.{name}' no longer exists; " +
                                 "that setting was left at its default.");
            }

            return property;
        }
    }
}
