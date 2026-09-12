// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the <b>Andromeda</b> experience (GDD 4.4): a second particle galaxy, 1.2 m across, that the player
    /// grabs, tilts and scales, with the scene panel to its right.
    ///
    /// <para><b>Why this exists.</b> <c>Assets/data/experiences/andromeda.asset</c> shipped with an empty
    /// <c>SceneName</c> <i>and</i> a null <c>ContentPrefab</c>. Under the rule CS-036 set,
    /// <c>ExperienceDirector.Switch</c> refuses such a module before anything moves, so one of the seven dock
    /// tiles has been dead by construction. Writing a content prefab and pointing the module at it is the whole
    /// fix — no new code path, no new scene.</para>
    ///
    /// <para><b>A prefab, not a scene.</b> The roadmap once called for an <c>andromeda_scene</c> duplicated from
    /// <c>galaxy_view_scene</c>. CS-036 and CS-041 made that unnecessary and the project has a standing rule
    /// against adding scenes: Andromeda is one object, it has no destinations and no layouts, and a scene would
    /// buy only load time and another thing to keep in sync.</para>
    ///
    /// <para><b>Nothing is invented.</b> The galaxy is the existing <c>SpiralGalaxy</c> + <c>DrawStars</c> +
    /// <c>StarsData</c> path that draws the Milky Way (Technical Overview 7.3), with three baked star sets of
    /// its own. The Milky Way is the same three layers in the same draw order — a cloud layer for the diffuse
    /// glow, a "negative" layer for dust, and the star layer — so this is the Milky Way's own recipe re-shaped,
    /// not a second renderer.</para>
    ///
    /// <para><b>What the baked data actually controls.</b> <c>SpiralGalaxy</c> only generates stars when its
    /// <c>bakedStars</c> is empty; with an asset assigned, every generation-time field (<c>EllipseCount</c>,
    /// <c>SpiralRotation</c>, the curves, the gradients, the star groups) is dead weight at run time. The
    /// shader reconstructs each star's position from the baked <c>curveOffset</c>, <c>ellipseOffset</c>,
    /// <c>ellipseDistance</c> and <c>yOffset</c> plus exactly two live fields, <c>XRadii</c> and
    /// <c>ZRadii</c> (they arrive as <c>_EllipseSize.xy</c>; see <c>cginc/StarPositionCompute.cginc</c>). Those
    /// two therefore have to match what was baked or the galaxy changes shape. The generation-time fields are
    /// still written, to the same values the bake used, so that the design is legible in the Inspector and so
    /// that a lost <c>StarsData</c> asset degrades to something similar rather than to an empty curve
    /// evaluating to zero and a galaxy of nothing.</para>
    ///
    /// <para><b>Departures CS-041 recorded as "a bug if copied literally".</b> <c>ManipulationHandler</c> and
    /// <see cref="ScaleLimits"/> sit on the <i>same</i> GameObject as the collider and the
    /// <c>GEInteractable</c>: the handler finds its scale constraint with <c>GetComponent</c>, and the
    /// constraint measures the transform it is on. Splitting them silently removes the limits.</para>
    ///
    /// <para><b>No grow-in here.</b> <c>ExperienceDirector.GrowIn</c> already scales a spawned content prefab
    /// from zero with a cubic ease-out, which is the GDD's "grows in from a point". A second one would fight
    /// it. <see cref="ScaleLimits"/> survives being measured at scale zero because it is given an authored
    /// width rather than renderers to measure — the galaxy has no <c>Renderer</c> at all, it is a
    /// <c>CommandBuffer.DrawProcedural</c>.</para>
    ///
    /// <para><b>No panel here either.</b> The director puts up the module's own <c>ScenePanelCopy</c> to the
    /// right of the content, which is exactly what GDD 4.4 asks for. An <c>InfoPanel</c> in this prefab would
    /// be a second copy of the same words.</para>
    ///
    /// <para>Re-running replaces the three <c>StarsData</c> assets, the three materials and the prefab in
    /// place, so scene and asset references survive. The star data is regenerated from fixed seeds, so a re-run
    /// with no parameter change produces byte-identical assets and an empty diff.</para>
    /// </summary>
    public static class AndromedaBuilder
    {
        // ---------- where things live

        private const string ModulePath = "Assets/data/experiences/andromeda.asset";
        private const string StarDataFolder = "Assets/scriptable_objects/star_data_scriptabe_objects";
        private const string MaterialFolder = "Assets/materials/galaxy_materials";
        private const string PrefabFolder = "Assets/prefabs/experiences";
        private const string PrefabName = "andromeda_content_prefab";
        private const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";

        // The three star shaders the Milky Way already uses. Loaded by path, not by Shader.Find: a shader that
        // failed to compile still resolves by name to the error shader, and the prefab would ship pointing at it.
        private const string StarShaderPath = "Assets/shaders/spiral_stars_shader.shader";           // Galaxy/Stars
        private const string CloudShaderPath = "Assets/shaders/spiral_stars_cloud_shader.shader";    // Galaxy/StarClouds
        private const string DustShaderPath = "Assets/shaders/spiral_stars_negative_shader.shader";  // Galaxy/StarsNeg

        // ---------- the numbers GDD 4.4 fixes

        /// <summary>GDD 4.4: "1.2 m across". The galaxy node is scaled so that this comes out exact.</summary>
        private const float WidthMetres = 1.2f;

        /// <summary>GDD 4.4: "flatter (tilt 75 deg)". Rotation of the disc plane about X.</summary>
        private const float TiltDegrees = 75f;

        /// <summary>
        /// Not in the GDD, and the one look knob CS-103 is most likely to move. The tilt alone leaves the disc
        /// 15 deg from face-on to a player looking horizontally, which reads as a flat circle; yawing it as well
        /// foreshortens it into the inclined oval Andromeda is recognised by. At 75 deg tilt and 55 deg yaw the
        /// disc normal ends up about 56 deg off the view axis, so the disc presents at roughly a 0.55 axis
        /// ratio. Turning this down rounds it out, turning it up takes it towards edge-on.
        /// </summary>
        private const float YawDegrees = 55f;

        /// <summary>GDD 4.4: "Move, tilt, scale with hands (0.5-3 m)".</summary>
        private const float MinScaleMetres = 0.5f;

        private const float MaxScaleMetres = 3f;

        /// <summary>
        /// The grab sphere, as a fraction of the galaxy's own radius. One-to-one: the player is outside this
        /// object (unlike the Cosmic Web) so a sphere the size of the thing is exactly what the hand and the
        /// mouse ray should find, and it stops short of the scene panel, which the director anchors 0.55 m to
        /// the right of the content root.
        /// </summary>
        private const float GrabRadiusFraction = 1f;

        // ---------- the shape of Andromeda, derived from the Milky Way's
        //
        // Every value below is stated against the corresponding value on milky_way_prefab's three SpiralGalaxy
        // components, because "visibly distinguishable at a glance" is a comparison, not an absolute.

        /// <summary>
        /// Ellipse semi-axes, shared by all three layers so they describe one galaxy. Milky Way: 0.8 / 1.0.
        /// The eccentricity is the mechanism that makes arms — nested ellipses rotated through
        /// <c>SpiralRotation</c> — so it must not go to 1:1, or every ellipse becomes the same circle and the
        /// spiral disappears entirely. Slightly more eccentric than the Milky Way, for a longer arm sweep.
        /// </summary>
        private const float XRadii = 0.72f;

        private const float ZRadii = 1f;

        /// <summary>
        /// Total rotation across the radius range, in degrees. Milky Way: 370 (about one turn). Andromeda's
        /// arms are more tightly wound than ours and read almost as rings, so this is higher.
        /// </summary>
        private const float SpiralRotationDegrees = 470f;

        /// <summary>
        /// Milky Way: 95 deg, which gives it a lopsided pair of arms. Andromeda is a two-fold symmetric grand
        /// design, so the second arm starts opposite the first.
        /// </summary>
        private const float ArmSpacingDegrees = 180f;

        /// <summary>Which layer this is. Decides colour, size and sprite tile; see <see cref="Shade"/>.</summary>
        private enum LayerRole
        {
            /// <summary>The diffuse glow. Concentrated inwards, so it stacks up into the white core.</summary>
            Clouds,

            /// <summary>The warm reddish dust ring, drawn with the darkening "negative" shader.</summary>
            Dust,

            /// <summary>The disc stars: white at the core, bluer at the rim.</summary>
            Stars,
        }

        /// <summary>One <c>SpiralGalaxy</c> layer: what to bake and what to set on the component.</summary>
        private struct LayerSpec
        {
            public LayerRole Role;
            public string Id;
            public string ShaderPath;

            /// <summary>Fixed, so a re-run with no parameter change writes byte-identical assets.</summary>
            public int Seed;

            public int Ellipses;
            public int StarsPerEllipse;
            public int Arms;

            /// <summary>Radial extent, as a fraction of the galaxy radius.</summary>
            public float MinEllipseScale;

            public float MaxEllipseScale;

            /// <summary>Per-star jitter on the radius, so the ellipses do not read as rings.</summary>
            public Vector2 Fuzz;

            /// <summary>Half-thickness at the centre and at the rim, before <see cref="YRange"/>.</summary>
            public float ThicknessAtCentre;

            public float ThicknessAtRim;

            /// <summary>Vertical scale, in the same units as the radius (which runs to 1).</summary>
            public float YRange;

            /// <summary>Sprite size, before the object's world scale. Feeds <c>_WSScale</c>.</summary>
            public float WorldSpaceScale;

            public Color Tint;
            public float TintMultiplier;

            /// <summary>Only meaningful on the downscaled path, which this content does not use.</summary>
            public bool IsShadow;

            /// <summary>
            /// Draw order. <c>SpiralGalaxy.Start</c> waits this many frames before creating its
            /// <c>DrawStars</c>, and <c>DrawStars</c> attaches its command buffers in creation order, so this
            /// is literally which layer is painted first.
            /// </summary>
            public int DrawIndex;
        }

        /// <summary>
        /// The three layers, in draw order. 17 200 points in total against the Milky Way's 18 720 and the
        /// 400 000 point sprites a whole frame is allowed (Technical Overview 7.4) — see the log line the
        /// build prints for the arithmetic.
        /// </summary>
        private static LayerSpec[] Layers() => new[]
        {
            new LayerSpec
            {
                Role = LayerRole.Clouds,
                Id = "clouds",
                ShaderPath = CloudShaderPath,
                Seed = 31415,
                Ellipses = 250,          // Milky Way clouds: 320
                StarsPerEllipse = 10,    // Milky Way clouds: 10
                Arms = 2,
                MinEllipseScale = 0.02f, // Milky Way clouds: 0.02
                MaxEllipseScale = 0.62f, // Milky Way clouds: 1.0 — held in, so this reads as a bulge
                Fuzz = new Vector2(0.92f, 1.08f),
                ThicknessAtCentre = 1f,
                ThicknessAtRim = 0.35f,
                YRange = 0.075f,
                WorldSpaceScale = 0.02f, // Milky Way clouds: 0.02
                Tint = Color.white,
                TintMultiplier = 0.3f,   // Milky Way clouds: 0.3
                IsShadow = false,
                DrawIndex = 0,
            },
            new LayerSpec
            {
                Role = LayerRole.Dust,
                Id = "dust",
                ShaderPath = DustShaderPath,
                Seed = 27182,
                Ellipses = 200,
                StarsPerEllipse = 8,
                Arms = 2,
                MinEllipseScale = 0.46f, // an annulus, not a disc: Andromeda's dust is a ring
                MaxEllipseScale = 1f,
                Fuzz = new Vector2(0.94f, 1.06f),
                ThicknessAtCentre = 0.4f,
                ThicknessAtRim = 0.4f,
                YRange = 0.018f,         // dust sits in a thin plane
                WorldSpaceScale = 0.02f, // Milky Way negative: 0.03
                // The warm red the dock tile already uses for Andromeda (ThumbnailBuilder's #8C4D42), pushed to
                // full value here because this shader multiplies the tint in twice — once as colour, once as
                // the alpha that decides how much of what is behind the dust it takes away.
                Tint = new Color(0.09f, 0.033f, 0.020f, 0.45f),
                TintMultiplier = 1f,
                IsShadow = true,
                DrawIndex = 1,
            },
            new LayerSpec
            {
                Role = LayerRole.Stars,
                Id = "stars",
                ShaderPath = StarShaderPath,
                Seed = 16180,
                Ellipses = 300,          // Milky Way stars: 320
                StarsPerEllipse = 15,    // Milky Way stars: 13
                Arms = 2,
                MinEllipseScale = 0.1f,  // Milky Way stars: 0.02 — Andromeda's inner disc is comparatively empty
                MaxEllipseScale = 1f,
                Fuzz = new Vector2(0.965f, 1.035f),
                ThicknessAtCentre = 1f,
                ThicknessAtRim = 0.1f,
                // GDD 4.4 "flatter". Half-thickness 0.055 against a radius of 1 at the core and a tenth of that
                // at the rim. The Milky Way bakes 0.1 and its scene instance then stretches y by 2.5.
                YRange = 0.055f,
                WorldSpaceScale = 0.008f, // Milky Way stars: 0.005, at a smaller object scale
                Tint = Color.white,
                TintMultiplier = 1f,
                IsShadow = false,
                DrawIndex = 2,
            },
        };

        /// <summary>
        /// Turns of the whole galaxy per unit of time, through the shader's <c>_Age</c>. Milky Way: 0.08.
        /// Slower, because this one is a single object the player is looking at rather than a map they are
        /// working over, and because nothing here may look like it is bouncing or drifting under a grab.
        /// </summary>
        private const float VelocityMultiplier = 0.05f;

        // ---------- colour
        //
        // Andromeda's palette is the dock tile's: ThumbnailBuilder already keys this place to #FFEDD1 warm
        // white, #8C4D42 reddish brown and a near-black edge. Using the same hues keeps the tile honest.

        /// <summary>GDD 4.4: white core, bluer outer arms.</summary>
        private static readonly (float t, Color c)[] DiscRamp =
        {
            (0.00f, new Color(1.00f, 0.93f, 0.82f)), // #FFEDD1
            (0.22f, new Color(1.00f, 0.90f, 0.76f)),
            (0.45f, new Color(0.90f, 0.86f, 0.84f)),
            (0.70f, new Color(0.70f, 0.78f, 0.97f)),
            (1.00f, new Color(0.55f, 0.70f, 1.00f)),
        };

        /// <summary>The bulge: white in the middle, warm at its edge.</summary>
        private static readonly (float t, Color c)[] CloudRamp =
        {
            (0.00f, new Color(1.00f, 0.96f, 0.88f)),
            (0.45f, new Color(0.98f, 0.88f, 0.74f)),
            (1.00f, new Color(0.78f, 0.58f, 0.46f)),
        };

        /// <summary>GDD 4.4: warm reddish dust ring. #8C4D42's hue, at full value.</summary>
        private static readonly (float t, Color c)[] DustRamp =
        {
            (0.00f, new Color(0.85f, 0.44f, 0.36f)),
            (0.55f, new Color(1.00f, 0.56f, 0.46f)),
            (1.00f, new Color(0.86f, 0.48f, 0.44f)),
        };

        /// <summary>Ionised hydrogen in the ring: the warm knots that break an arm up.</summary>
        private static readonly Color HiiColour = new Color(1.00f, 0.52f, 0.34f);

        /// <summary>Young blue stars, which is what makes the outer arms read as blue at all.</summary>
        private static readonly Color BlueGiantColour = new Color(0.62f, 0.78f, 1.00f);

        // Tiles in stars_small_atlas.tga, the same four the Milky Way's star groups use.
        private static readonly Vector2 TilePoint = new Vector2(0.5f, 0f);
        private static readonly Vector2 TilePuffy = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TileRayed = new Vector2(0f, 0.5f);
        private static readonly Vector2 TileCross = new Vector2(0f, 0f);

        // ---------- build

        [MenuItem("Cosmic Simulation/Build Andromeda Content")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("AndromedaBuilder: leave play mode first.");
                return;
            }

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(ModulePath);
            if (module == null)
            {
                Debug.LogError($"AndromedaBuilder: no experience module at {ModulePath}. " +
                               "Run Cosmic Simulation -> Import Copy first. Nothing was written.");
                return;
            }

            var specs = Layers();

            // All three or none. A galaxy missing its dust or its glow is a look bug that nobody would trace
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
                Debug.LogError("AndromedaBuilder: these shaders did not load, so nothing was written and " +
                               $"'{module.Id}' is unchanged:\n  " + string.Join("\n  ", missing) +
                               "\n  They must import without errors first.");
                return;
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
                Debug.LogWarning($"AndromedaBuilder: no star sprite atlas at {AtlasPath}; the layers will draw " +
                                 "white squares. The Milky Way uses this same texture, so check it is still there.");
            }

            // ---- bake the three star sets, and measure what came out

            var stars = new GalaxyExplorer.StarVertDescriptor[specs.Length][];
            var data = new GalaxyExplorer.StarsData[specs.Length];
            var measurements = new Measurement[specs.Length];

            var envelopeRadius = 0f; // the widest a star ever gets from the centre, over a whole revolution
            var total = 0;

            for (var i = 0; i < specs.Length; i++)
            {
                stars[i] = Generate(specs[i]);
                total += stars[i].Length;

                measurements[i] = Measure(stars[i]);
                envelopeRadius = Mathf.Max(envelopeRadius, measurements[i].EnvelopeRadius);

                var path = $"{StarDataFolder}/star_data_andromeda_{specs[i].Id}_scriptable_object.asset";
                data[i] = WriteStars(path, stars[i]);
                if (data[i] == null)
                {
                    Debug.LogError($"AndromedaBuilder: could not write {path}; nothing else was changed and " +
                                   $"'{module.Id}' is unchanged, so its tile still does nothing.");
                    return;
                }
            }

            // ---- the galaxy node's scale, so the GDD's 1.2 m is exact rather than asserted

            if (envelopeRadius <= 0f)
            {
                Debug.LogError("AndromedaBuilder: the generated stars measure zero across, which means the " +
                               "layer parameters produced nothing. Nothing was written.");
                return;
            }

            var galaxyScale = WidthMetres / (envelopeRadius * 2f);

            // ---- materials

            var materials = new Material[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                materials[i] = WriteMaterial(specs[i], shaders[i], atlas);
            }

            // ---- prefab

            var prefab = WritePrefab(specs, data, materials, galaxyScale);
            if (prefab == null)
            {
                Debug.LogError($"AndromedaBuilder: could not save {PrefabFolder}/{PrefabName}.prefab. " +
                               $"The star data and materials were written but '{module.Id}' is unchanged, so " +
                               "its tile still does nothing.");
                return;
            }

            // ---- the one line that makes the tile work (CS-036)

            var previous = module.ContentPrefab;
            var so = new SerializedObject(module);
            var contentPrefab = Find(so, "ContentPrefab");
            if (contentPrefab == null)
            {
                // Everything else is on disk by now, so this has to say so rather than throw: the assets are
                // fine and only the wiring is missing.
                Debug.LogError($"AndromedaBuilder: ExperienceModule has no 'ContentPrefab' field any more, so " +
                               $"'{module.Id}' could not be wired. The star data, the materials and " +
                               $"{PrefabFolder}/{PrefabName}.prefab were written; assign it by hand or fix this " +
                               "builder. Until then the tile still does nothing.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
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
                var doubling = specs[i].Role == LayerRole.Clouds ? 2f : 1f;
                var unit = specs[i].WorldSpaceScale * galaxyScale * 0.4f * doubling * 1000f;

                report[i] =
                    $"{specs[i].Id}: {stars[i].Length:N0} points " +
                    $"({specs[i].Arms} arms x {specs[i].Ellipses} ellipses x {specs[i].StarsPerEllipse}), " +
                    $"radius {m.MinRadius * galaxyScale * 100f:F1}-{m.EnvelopeRadius * galaxyScale * 100f:F1} cm, " +
                    $"half thickness {m.HalfThickness * galaxyScale * 1000f:F1} mm " +
                    $"({m.HalfThickness / Mathf.Max(0.0001f, m.EnvelopeRadius) * 100f:F1}% of its radius), " +
                    $"sprite half size {m.MinSize * unit:F2}-{m.MaxSize * unit:F2} mm";
            }

            Debug.Log(
                $"AndromedaBuilder: '{module.Id}' now opens {PrefabFolder}/{PrefabName}.prefab" +
                (previous == null
                    ? " (it had no ContentPrefab and no SceneName at all, so ExperienceDirector.Switch refused " +
                      "its dock tile outright - CS-036)."
                    : $" (it previously opened '{previous.name}').") + "\n" +
                $"  {total:N0} point sprites in {specs.Length} layers = {total * 6:N0} vertices and " +
                $"{total * 2:N0} triangles per view, before Android multiview doubles the vertex stage. " +
                $"Milky Way, the same renderer: 18,720. Quest 3 frame ceiling: 400,000 point sprites " +
                $"(Technical Overview 7.4), so this is {total / 4000f:F1}% of it.\n" +
                $"  {string.Join("\n  ", report)}\n" +
                $"  measured envelope radius {envelopeRadius:F4} at unit scale -> galaxy node scaled " +
                $"{galaxyScale:F4}, giving {envelopeRadius * 2f * galaxyScale:F3} m across against GDD 4.4's " +
                $"{WidthMetres:F2} m.\n" +
                $"  tilt {TiltDegrees:F0} deg (GDD 4.4) plus {YawDegrees:F0} deg yaw, which presents the disc " +
                $"about {InclinationFromFaceOn():F0} deg off face-on to a player looking along the content " +
                "root's forward axis (0 deg would be a flat circle, 90 deg edge-on).\n" +
                $"  grab sphere radius {WidthMetres * 0.5f * GrabRadiusFraction:F2} m; two hands move, rotate " +
                $"and scale it between {MinScaleMetres:F1} m and {MaxScaleMetres:F1} m " +
                "(ScaleLimits.Kind.Custom: none of Body 0.05-3, Model 0.4-4 or Nebula 0.3-2 is the GDD's 0.5-3).\n" +
                $"  star data: {StarDataFolder}/star_data_andromeda_<clouds|dust|stars>_scriptable_object.asset; " +
                $"materials: {MaterialFolder}/andromeda_<layer>_material.mat.\n" +
                $"  skipped: {(skipped.Count == 0 ? "nothing" : string.Join("; ", skipped))}. " +
                "The grow-in, the panel and the ambience belong to ExperienceDirector, not to this prefab.");
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
        // empty diff, which is the only way a generated 2 MB YAML file is reviewable at all.

        private static GalaxyExplorer.StarVertDescriptor[] Generate(LayerSpec spec)
        {
            var rng = new System.Random(spec.Seed);
            var count = spec.Ellipses * spec.StarsPerEllipse * spec.Arms;
            var stars = new GalaxyExplorer.StarVertDescriptor[count];
            var n = 0;

            for (var arm = 0; arm < spec.Arms; arm++)
            {
                var armOffsetDegrees = ArmSpacingDegrees * arm;

                for (var i = 0; i < spec.Ellipses; i++)
                {
                    // Progression from the centre outwards. Ellipses are evenly spaced in radius and carry the
                    // same number of stars each, so surface density falls as 1/r — which is what puts most of
                    // the light in the middle. The Milky Way is built the same way.
                    var p = i / (float)spec.Ellipses;

                    var offsetRadians = (armOffsetDegrees + SpiralRotationDegrees * p) * Mathf.Deg2Rad;
                    var baseDistance = Mathf.Lerp(spec.MinEllipseScale, spec.MaxEllipseScale, p);

                    for (var j = 0; j < spec.StarsPerEllipse; j++)
                    {
                        stars[n++] = MakeStar(spec, rng, p, offsetRadians, baseDistance);
                    }
                }
            }

            return stars;
        }

        private static GalaxyExplorer.StarVertDescriptor MakeStar(
            LayerSpec spec, System.Random rng, float p, float offsetRadians, float baseDistance)
        {
            var distance = baseDistance * Mathf.Lerp(spec.Fuzz.x, spec.Fuzz.y, Next(rng));

            var thickness = Mathf.Lerp(spec.ThicknessAtCentre, spec.ThicknessAtRim, Mathf.SmoothStep(0f, 1f, p))
                            * spec.YRange;

            var shade = Shade(spec, rng, p);

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

        private static Shading Shade(LayerSpec spec, System.Random rng, float p)
        {
            switch (spec.Role)
            {
                case LayerRole.Clouds:
                    // One puffy blob per point. Small and dense in the middle, where hundreds of them overlap
                    // and the additive blend saturates to the white core GDD 4.4 asks for; larger, warmer and
                    // fainter further out, where they become the bulge's haze.
                    return new Shading
                    {
                        Colour = Ramp(CloudRamp, p) * Mathf.Lerp(1f, 0.45f, p),
                        Size = Mathf.Lerp(1.6f, 3.4f, p) * Mathf.Lerp(0.85f, 1.15f, Next(rng)),
                        Tile = TilePuffy,
                    };

                case LayerRole.Dust:
                {
                    // A ring, not a disc. The band already starts at MinEllipseScale; this puts the weight of
                    // it at about two thirds of the galaxy's radius and lets it fade at both edges, so the lane
                    // has a shape rather than a hard inner rim.
                    var fromPeak = (p - 0.35f) / 0.3f;
                    var bump = Mathf.Exp(-fromPeak * fromPeak);
                    var weight = Mathf.Lerp(0.35f, 1f, bump);

                    return new Shading
                    {
                        Colour = Ramp(DustRamp, p) * weight,
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
                            Colour = Ramp(DiscRamp, p),
                            Size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff,
                            Tile = TilePoint,
                        };
                    }

                    if (roll < 0.93f)
                    {
                        return new Shading
                        {
                            Colour = Ramp(DiscRamp, p),
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
                                Colour = HiiColour,
                                Size = Mathf.Lerp(1.4f, 2.3f, Next(rng)) * falloff,
                                Tile = TileRayed,
                            }
                            : new Shading
                            {
                                Colour = Ramp(DiscRamp, p),
                                Size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff,
                                Tile = TilePoint,
                            };
                    }

                    return p > 0.3f
                        ? new Shading
                        {
                            Colour = BlueGiantColour,
                            Size = Mathf.Lerp(1.1f, 1.9f, Next(rng)) * falloff,
                            Tile = TileCross,
                        }
                        : new Shading
                        {
                            Colour = Ramp(DiscRamp, p),
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

        /// <summary>
        /// How far off face-on the disc presents, in degrees, for a player looking along the content root's
        /// Z axis.
        ///
        /// The disc normal starts at +Y. <c>Quaternion.Euler(tilt, yaw, 0)</c> applies X then Y, so the normal
        /// goes to <c>(sin(tilt) sin(yaw), cos(tilt), sin(tilt) cos(yaw))</c> and the component along Z — which
        /// is what decides the foreshortening — is <c>sin(tilt) cos(yaw)</c>. Reported rather than assumed,
        /// because the tilt alone would leave Andromeda looking like a flat circle and nobody would notice from
        /// the constants that it had.
        /// </summary>
        private static float InclinationFromFaceOn()
        {
            var alongView = Mathf.Sin(TiltDegrees * Mathf.Deg2Rad) * Mathf.Cos(YawDegrees * Mathf.Deg2Rad);
            return Mathf.Acos(Mathf.Clamp01(Mathf.Abs(alongView))) * Mathf.Rad2Deg;
        }

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
        private static Measurement Measure(GalaxyExplorer.StarVertDescriptor[] stars)
        {
            var result = new Measurement { MinRadius = float.MaxValue, MinSize = float.MaxValue };
            var major = Mathf.Max(XRadii, ZRadii);
            var minor = Mathf.Min(XRadii, ZRadii);

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
        /// One material per layer. Andromeda gets its own rather than sharing the Milky Way's three, so that
        /// retuning one galaxy cannot move the other — they run the same shaders on purpose, and those shaders
        /// are already reachable from <c>galaxy_view_scene</c> through <c>milky_way_prefab</c>, so nothing here
        /// needs adding to Always Included Shaders.
        ///
        /// Almost every property is overwritten each frame by <c>DrawStars.Update</c> from the
        /// <c>SpiralGalaxy</c> component (<c>_Color</c>, <c>_WSScale</c>, <c>_Age</c>, <c>_EllipseSize</c>,
        /// <c>_TransitionAlpha</c>); what is genuinely authored here is the sprite atlas. The rest is written
        /// so the material previews as something rather than as black.
        /// </summary>
        private static Material WriteMaterial(LayerSpec spec, Shader shader, Texture2D atlas)
        {
            var path = $"{MaterialFolder}/andromeda_{spec.Id}_material.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = $"andromeda_{spec.Id}_material" };
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
            LayerSpec[] specs, GalaxyExplorer.StarsData[] data, Material[] materials, float galaxyScale)
        {
            var root = new GameObject(PrefabName);

            // The parts the player touches, all on one GameObject. CS-041: ManipulationHandler looks for its
            // scale constraint with GetComponent and ScaleLimits measures the transform it is on, so the
            // collider, the interactable, the handler and the limits cannot be split across the hierarchy.
            var grab = root.AddComponent<SphereCollider>();
            grab.radius = WidthMetres * 0.5f * GrabRadiusFraction;

            root.AddComponent<GEInteractable>();

            var manipulation = root.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            SetEnum(manipulationSo, "manipulationType",
                (int)ManipulationHandler.HandMovementType.OneAndTwoHanded);
            // GDD 4.4: "Move, tilt, scale with hands". Unlike the nebula overlays — which billboard themselves
            // and would overwrite any rotation the player set on the next frame — rotation is wanted here, so
            // this is the only mode of the six that covers all three verbs.
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

            var limits = root.AddComponent<ScaleLimits>(); // requires the handler above, so it is added after
            var limitsSo = new SerializedObject(limits);
            // None of Body (5 cm - 3 m), Model (40 cm - 4 m) or Nebula (30 cm - 2 m) is GDD 4.4's 0.5-3 m, and
            // picking the nearest would quietly change the spec, so the metres are stated.
            SetEnum(limitsSo, "kind", (int)ScaleLimits.Kind.Custom);
            SetFloat(limitsSo, "customMinMetres", MinScaleMetres);
            SetFloat(limitsSo, "customMaxMetres", MaxScaleMetres);
            // There is no Renderer to measure: the galaxy is a ComputeBuffer drawn by a command buffer. Stating
            // the width is also what lets the limits survive being asked during the director's grow-in, which
            // starts at local scale zero.
            SetFloat(limitsSo, "authoredMetresAtUnitScale", WidthMetres);
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            // The disc itself, under the grabbable root so that the tilt is an offset the player's rotation
            // adds to rather than something a grab immediately destroys.
            var galaxy = new GameObject("andromeda_galaxy");
            galaxy.transform.SetParent(root.transform, false);
            galaxy.transform.localRotation = Quaternion.Euler(TiltDegrees, YawDegrees, 0f);
            galaxy.transform.localScale = Vector3.one * galaxyScale;

            // Added in draw order: SpiralGalaxy.Start waits `index` frames before creating its DrawStars, and
            // DrawStars attaches the layers' command buffers in creation order.
            for (var i = 0; i < specs.Length; i++)
            {
                Configure(galaxy.AddComponent<GalaxyExplorer.SpiralGalaxy>(), specs[i], data[i], materials[i]);
            }

            var path = $"{PrefabFolder}/{PrefabName}.prefab";
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
            GalaxyExplorer.SpiralGalaxy layer, LayerSpec spec, GalaxyExplorer.StarsData data, Material material)
        {
            // --- live
            layer.XRadii = XRadii;
            layer.ZRadii = ZRadii;
            layer.bakedStars = data;
            layer.lastBakedStarsCount = data != null && data.stars != null ? data.stars.Length : 0;
            layer.baseMaterial = material;
            layer.worldSpaceScale = spec.WorldSpaceScale;
            layer.velocityMultiplier = VelocityMultiplier;
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
            layer.SpiralRotation = SpiralRotationDegrees;
            layer.enableSecondArm = spec.Arms > 1;
            layer.secondArmStartOffsetDeg = ArmSpacingDegrees;
            layer.YRange = spec.YRange;
            layer.FuzzySideScale = spec.Fuzz;

            layer.centerToRimStarSize = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, spec.Role == LayerRole.Stars ? 0.55f : 1f));

            layer.centerToRimVerticalOffset = new AnimationCurve(
                new Keyframe(0f, spec.ThicknessAtCentre),
                new Keyframe(1f, spec.ThicknessAtRim));

            layer.centerToRimGradient = GradientFrom(RampFor(spec.Role));

            layer.StarGroups = new List<GalaxyExplorer.SpiralGalaxy.StarDistributionGroup>
            {
                new GalaxyExplorer.SpiralGalaxy.StarDistributionGroup
                {
                    GroupName = spec.Id,
                    Percentage = 100f,
                    UseColorRange = false,
                    ColorRange = GradientFrom(RampFor(spec.Role)),
                    UVOffset = spec.Role == LayerRole.Stars ? TilePoint : TilePuffy,
                    SizeMultiplierRange = spec.Role == LayerRole.Stars
                        ? new Vector2(0.62f, 1f)
                        : new Vector2(1.6f, 3.4f),
                    SizeIsAbsolute = spec.Role != LayerRole.Stars,
                    RandomEllipseScaleOffset = 0f,
                },
            };
        }

        private static (float t, Color c)[] RampFor(LayerRole role)
        {
            switch (role)
            {
                case LayerRole.Clouds: return CloudRamp;
                case LayerRole.Dust: return DustRamp;
                default: return DiscRamp;
            }
        }

        /// <summary>A <see cref="Gradient"/> holding the same colours as one of the ramps above.</summary>
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
                Debug.LogWarning($"AndromedaBuilder: '{so.targetObject.GetType().Name}.{name}' no longer exists; " +
                                 "that setting was left at its default.");
            }

            return property;
        }
    }
}
