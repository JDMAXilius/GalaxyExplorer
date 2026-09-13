// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using GalaxyExplorer;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Turns each nebula's photograph into a cloud of gas standing in three dimensions, and hangs it on that
    /// destination's prefab.
    ///
    /// <para><b>The problem this solves.</b> Every nebula in the app was a picture: four billboarded cards in
    /// <c>NebulaPrefabBuilder</c>, and then a photograph projected onto a dome in <c>PlaceShellBuilder</c>. Both
    /// are honest and both are flat. Move your head and a picture slides with you, which is the one cue that
    /// tells a player they are looking at a wall rather than standing in a cloud. This builds the actual volume:
    /// tens of thousands of points placed through a real three-dimensional shape, which the player can walk into
    /// and around, and which shows parallax because there is something to have parallax.</para>
    ///
    /// <para><b>Where the third dimension comes from, which is the whole question.</b> A photograph carries two
    /// dimensions of a nebula and throws the third away. Nothing can recover it in general. What can be done -
    /// and what is done here - is to use the shape astronomers already know each object has, and let the
    /// photograph supply everything else:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Shell.</b> A planetary nebula or a supernova remnant is a roughly spherical shell of gas
    /// thrown off a star, expanding outwards. That is not a guess, it is what the object is. So for a pixel at
    /// image radius <c>r</c> (as a fraction of the shell's angular radius) the gas it represents genuinely lies
    /// at depth <c>z = ±R·sqrt(1 - r²)</c>, give or take the shell's thickness - the same geometry that makes
    /// a soap bubble look like a ring. <b>This is derived, not invented.</b> The Helix, the Crab and NGC 1501
    /// are built this way. It also means the deprojection has to be undone in the density, or the model would
    /// double-count: a shell photographed face-on is brightest at its rim because the line of sight passes
    /// through more gas there, so placing points in proportion to image brightness would pile gas onto a rim
    /// that is only bright <i>because</i> of the projection. <see cref="Model.Shell"/> divides it back out.</item>
    ///
    /// <item><b>Bipolar.</b> The Homunculus is two lobes of ejecta thrown out either side of Eta Carinae in
    /// the 1840s, and it is one of the best-documented shapes in the sky. Two shells on an axis, each built as
    /// above.</item>
    ///
    /// <item><b>Cloud.</b> Orion, the Pillars and the Carina complex around Trumpler 14 are irregular clouds
    /// with no symmetry to exploit. Here the depth <b>is</b> modelled and there is no pretending otherwise:
    /// each point's distance front-to-back is drawn about a surface that undulates with a low-frequency noise
    /// field, spread wider where the plate is brighter - on the reasoning that a brighter column is usually a
    /// deeper one. That is a declared convention, in the sense D-010 means, and it is recorded in the baked
    /// asset's <c>Provenance</c> and in the destination's panel copy.</item>
    /// </list>
    ///
    /// <para><b>Colour is never modelled.</b> Every point takes the colour of the pixel it came from. The
    /// plates are real telescope images, so what the player is surrounded by is the real colour of the real
    /// object, arranged into the shape the object is known to have.</para>
    ///
    /// <para><b>Reading the plate.</b> Through a <c>RenderTexture</c> blit rather than
    /// <c>Texture2D.GetPixels</c>, so no importer setting has to be changed: making seven shipping textures
    /// readable would double their memory on the device for the sake of an editor script that runs once.</para>
    ///
    /// <para>Order: <b>Build Nebula Prefabs</b>, then this, then <b>Attach Place Shells</b> - the first rebuilds
    /// the prefab from scratch and would throw away what the other two add. Re-running is safe: the volume node
    /// is removed by name and rebuilt.</para>
    /// </summary>
    public static class NebulaVolumeBuilder
    {
        private const string DestinationFolder = "Assets/data/destinations";
        private const string DataFolder = "Assets/data/nebula_volumes";
        private const string MaterialFolder = "Assets/materials/nebula_volumes";
        private const string ShaderName = "CosmicSimulation/NebulaVolume";
        private const string DustShaderName = "CosmicSimulation/NebulaVolumeDust";
        private const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";
        private const string NodeName = "nebula_volume";

        /// <summary>
        /// The sky beyond the gas: a 360 panorama that is mostly empty black, with sparse separated stars and
        /// one faint distant glow. Generated rather than photographed, because no telescope took a full-sphere
        /// plate of the sky around any of these objects - which is exactly why the nebula's own plate could
        /// never be the dome. Credited in Assets/_sources/CREDITS.md.
        /// </summary>
        private const string SkyFolder = "Assets/Textures/nebulae/domes";

        /// <summary>
        /// Which sky each object stands under. Four panoramas for seven nebulae, chosen by what the object
        /// actually is rather than one per id: two planetary nebulae glow the same teal, a remnant and an
        /// eruption sit in the same warm dust, and the two objects inside the Carina and Eagle star clouds
        /// have a genuinely crowded sky behind them. A sky is the neighbourhood, not the object.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> Skies =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "helix", "sky_teal" },
                { "ngc1501", "sky_teal" },
                { "crab", "sky_crab" },
                { "homunculus", "sky_amber" },
                { "pillars", "sky_starfield" },
                { "trumpler14", "sky_starfield" },
                { "orion", "sky_violet" },
            };

        /// <summary>Working resolution the plate is read at. 512 is ample: the cloud is sampled, not copied.</summary>
        private const int SampleSize = 512;

        /// <summary>
        /// Points per cloud, across all three roles, and the number is the Cosmic Web's: 40 000 in a 5 m volume
        /// is what a room made of points costs, and it is a tenth of the 400 000 the frame is allowed across
        /// both eyes. The roles are still what makes it read - a galaxy place gets its gas, dust and stars from
        /// 8 600 points because each layer does one job at one size - but a volume you stand inside needs the
        /// density of one, not the density of an ornament.
        /// </summary>
        private const int PointCount = 40000;

        /// <summary>What a point is for. Each role is its own buffer, its own material and its own sprite size.</summary>
        private enum Role
        {
            /// <summary>The glow. Large, soft, additive - this is what reads as smoke.</summary>
            Clouds,

            /// <summary>The lanes. Premultiplied dark, subtractive, so the cloud has silhouette and depth.</summary>
            Dust,

            /// <summary>The stars in and around the gas. Small, bright, additive.</summary>
            Stars,
        }

        private readonly struct RoleSpec
        {
            public readonly Role Role;
            public readonly int Count;
            public readonly float SizeScale;
            public readonly bool Subtractive;

            /// <summary>
            /// Half-width of this layer's sprite in metres, written onto the component. The default 0.012 was
            /// chosen for a 70 cm ball; in a 5 m room it is a grain of dust, which is why the gas read as
            /// sparkle rather than as smoke. Gas wants sprites you cannot count, stars want sprites you can.
            /// </summary>
            public readonly float SpriteMetres;

            public RoleSpec(Role role, int count, float sizeScale, bool subtractive, float spriteMetres)
            {
                Role = role;
                Count = count;
                SizeScale = sizeScale;
                Subtractive = subtractive;
                SpriteMetres = spriteMetres;
            }
        }

        /// <summary>
        /// The three roles and their share of the budget, in the proportions the galaxy layers use: the glow is
        /// large and sparse, the dust is fewer still, and the stars are many and small.
        /// </summary>
        private static readonly RoleSpec[] Roles =
        {
            // The sprite sizes are a measured band, not a guess. At 0.012 - the size chosen for a 70 cm ball -
            // a 5 m room reads as sparkle, every grain countable. At 0.075 the grains merge and keep merging:
            // additive light saturates, the Helix's blue and gold both go white, and the whole nebula is milk.
            // 0.03 is where the gas is continuous and still coloured.
            // Fewer, smaller and dimmer than a first instinct says. Twelve thousand sprites at 0.03 through a
            // 5 m sphere is not gas, it is a wall: it closes over the sky in every direction and there is
            // nothing to see past it - no black, no individual stars, no colour, because additive light with
            // nothing between it saturates. Gas has to be something you see *through*.
            new RoleSpec(Role.Clouds,  6000, 3.0f, false, 0.022f),
            new RoleSpec(Role.Dust,    8000, 2.4f, true,  0.028f),
            new RoleSpec(Role.Stars,  20000, 0.35f, false, 0.010f),
        };

        /// <summary>
        /// How far a pixel has to stand above its surroundings to be a star, and how far below to be dust.
        /// Measured against a blur of the plate, which is the cheapest stand-in for "its surroundings".
        /// </summary>
        /// <summary>
        /// How far a pixel has to stand above the *broad* background to be a star. Deliberately small, and
        /// measured against a wide blur rather than a narrow one: a star raises its own neighbourhood, so
        /// against a six-pixel blur even a bright star barely clears its own glow - which is why the first
        /// build gave the Helix 145 stars out of 5,000 and NGC 1501 sixty-five.
        /// </summary>
        private const float StarPeakRatio = 1.05f;
        private const float DustHoleRatio = 1.40f;

        /// <summary>Radius of the blur that defines "surroundings", in sample pixels.</summary>
        private const int NeighbourhoodRadius = 6;

        /// <summary>The wider background a star is judged against.</summary>
        private const int BackgroundRadius = 20;

        /// <summary>Stars scattered through the whole volume, on top of the ones the plate itself shows.</summary>
        private const int FieldStars = 9000;

        /// <summary>Below this share of the plate's peak luminance a pixel is empty sky and seeds nothing.</summary>
        private const float LuminanceFloor = 0.10f;

        /// <summary>How the third dimension is reconstructed for one object.</summary>
        private enum Model
        {
            /// <summary>A roughly spherical shell of ejecta. Depth is derived from the projection.</summary>
            Shell,

            /// <summary>Two lobes on an axis. Each lobe is a shell.</summary>
            Bipolar,

            /// <summary>An irregular cloud. Depth is a declared convention, not a derivation.</summary>
            Cloud,
        }

        private readonly struct Spec
        {
            public readonly string Id;
            public readonly string PlatePath;
            public readonly Model Model;

            /// <summary>
            /// Half the cloud's width in metres. It has been three things. 0.35 was GDD 4.3's 70 cm overlay,
            /// an object held at arm's length. 0.60 matched the galaxy places, which is right for a galaxy you
            /// lean over and still wrong for gas you stand in: at that size the walls are inside your reach and
            /// the whole nebula is one bright smear. 2.50 is the Cosmic Web's own volume - GDD 4.7's 5 m room,
            /// the one place in this app that already surrounds the player - and that is what a nebula is.
            /// </summary>
            public readonly float RadiusMetres;

            /// <summary>
            /// Shell: how thick the shell is as a fraction of its radius. Cloud: how deep the cloud is as a
            /// fraction of its width. Bipolar: the lobe radius as a fraction of the whole.
            /// </summary>
            public readonly float Thickness;

            public Spec(string id, string platePath, Model model, float radiusMetres, float thickness)
            {
                Id = id;
                PlatePath = platePath;
                Model = model;
                RadiusMetres = radiusMetres;
                Thickness = thickness;
            }
        }

        /// <summary>
        /// The seven, with the shape each one is known to have. The plate paths are the same table
        /// <c>NebulaPrefabBuilder</c> and <c>PlaceShellBuilder</c> use.
        /// </summary>
        private static readonly Spec[] Specs =
        {
            // A planetary nebula seen close to face-on: a shell whose near and far walls project onto the same
            // ring. This is the case the shell model was written for.
            new Spec("helix", "Assets/Textures/nebulae/helix_texture.jpg", Model.Shell, 2.50f, 0.22f),

            // A supernova remnant. Not a clean sphere - it is a filamentary cage around a pulsar wind nebula -
            // but it is a genuine expanding shell, and a shell is far closer to the truth than a flat card.
            new Spec("crab", "Assets/Textures/nebulae/crab_texture.jpg", Model.Shell, 2.50f, 0.30f),

            // Another planetary nebula, and a notably round one; its common name is the Oyster.
            new Spec("ngc1501", "Assets/Textures/ngc1501_texture.jpg", Model.Shell, 2.50f, 0.24f),

            // Two lobes either side of Eta Carinae, thrown out in the 1840s eruption.
            new Spec("homunculus", "Assets/Textures/nebulae/homunculus_texture.jpg", Model.Bipolar, 2.50f, 0.34f),

            // Irregular clouds. Depth is convention here, and says so.
            new Spec("orion", "Assets/Textures/nebulae/orion_texture.jpg", Model.Cloud, 2.50f, 0.45f),
            new Spec("pillars", "Assets/Textures/pillars_texture.tga", Model.Cloud, 2.50f, 0.40f),
            new Spec("trumpler14", "Assets/Textures/trumpler_texture.jpg", Model.Cloud, 2.50f, 0.45f),
        };

        private static readonly Dictionary<Model, string> ModelProvenance = new Dictionary<Model, string>
        {
            [Model.Shell] =
                "Depth derived, not invented: the object is a roughly spherical shell of ejecta, so a point at " +
                "image radius r sits at z = +/- R*sqrt(1 - r^2). The limb brightening that projection causes is " +
                "divided back out of the density so the rim is not counted twice. Colour is the plate's own.",

            [Model.Bipolar] =
                "Depth derived from the documented shape: two lobes of ejecta on an axis, each treated as a " +
                "shell as above. Colour is the plate's own.",

            [Model.Cloud] =
                "Depth is a declared convention, not a measurement. The object is an irregular cloud with no " +
                "symmetry to deproject, so each point is placed about a surface undulating with a " +
                "low-frequency noise field, spread wider where the plate is brighter. Only the colour and the " +
                "two-dimensional structure come from the photograph.",
        };

        [MenuItem("Cosmic Simulation/Build Nebula Volumes")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("NebulaVolumeBuilder: leave play mode first — this writes assets.");
                return;
            }

            var shader = Shader.Find(ShaderName);
            var dustShader = Shader.Find(DustShaderName);
            if (dustShader == null)
            {
                Debug.LogWarning($"NebulaVolumeBuilder: shader '{DustShaderName}' not found, so the dust layer " +
                                 "will be skipped and every nebula will be glow and stars only.");
            }
            if (shader == null)
            {
                Debug.LogError($"NebulaVolumeBuilder: shader '{ShaderName}' not found. " +
                               "Assets/shaders/nebula_volume_shader.shader must import without errors first.");
                return;
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogError($"NebulaVolumeBuilder: shader '{ShaderName}' has compile errors. Nothing built, " +
                               "rather than filling a nebula with magenta squares.");
                return;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
            {
                Debug.LogError($"NebulaVolumeBuilder: no point sprite atlas at {AtlasPath}.");
                return;
            }

            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var modules = LoadDestinations();
            var report = new StringBuilder();
            var built = 0;

            foreach (var spec in Specs)
            {
                if (!modules.TryGetValue(spec.Id, out var module))
                {
                    report.AppendLine($"  {spec.Id}: no destination module, skipped.");
                    continue;
                }

                if (module.ContentPrefab == null)
                {
                    report.AppendLine($"  {spec.Id}: module has no ContentPrefab. Run Build Nebula Prefabs first.");
                    continue;
                }

                var plate = ReadPlate(spec.PlatePath, out var plateError);
                if (plate == null)
                {
                    report.AppendLine($"  {spec.Id}: {plateError}");
                    continue;
                }

                try
                {
                    var layers = new List<(RoleSpec Role, NebulaVolumeData Data, Material Material)>();
                    foreach (var role in Roles)
                    {
                        var data = Bake(spec, plate, role);
                        var roleShader = role.Subtractive ? dustShader : shader;
                        if (roleShader == null)
                        {
                            report.AppendLine($"  {spec.Id}: no shader for the {role.Role} layer, skipped.");
                            continue;
                        }

                        layers.Add((role, data, WriteMaterial(roleShader, atlas, spec.Id, role.Role)));
                    }

                    if (Attach(module, spec, layers, report))
                    {
                        built++;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(plate);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"NebulaVolumeBuilder: {built} of {Specs.Length} nebulae given a real volume.\n" +
                      $"  {PointCount:N0} points each — one draw call, {PointCount * 2:N0} triangles an eye.\n" +
                      report);
        }

        [MenuItem("Cosmic Simulation/Remove Nebula Volumes")]
        public static void RemoveAll()
        {
            var modules = LoadDestinations();
            var removed = new List<string>();

            foreach (var spec in Specs)
            {
                if (!modules.TryGetValue(spec.Id, out var module) || module.ContentPrefab == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(module.ContentPrefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (StripVolumes(root) > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        removed.Add(spec.Id);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"NebulaVolumeBuilder: volume removed from {removed.Count} prefab(s): " +
                      $"{(removed.Count == 0 ? "none" : string.Join(", ", removed))}");
        }

        // ---------- reading the plate

        /// <summary>
        /// Copies a plate into a readable <see cref="Texture2D"/> at the working resolution, through the GPU.
        ///
        /// <c>GetPixels</c> would need every one of these seven textures marked Read/Write Enabled, which keeps
        /// a second copy of each in system memory on the device forever, for the sake of a script that runs
        /// once. A blit needs nothing: the GPU already has the texture.
        ///
        /// The caller destroys the result.
        /// </summary>
        private static Texture2D ReadPlate(string path, out string error)
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null)
            {
                error = $"plate {path} is missing.";
                return null;
            }

            var rt = RenderTexture.GetTemporary(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                var copy = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0);
                copy.Apply();

                error = null;
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // ---------- the bake

        /// <summary>
        /// The colour a point of this layer carries. Gas and stars keep the plate's own colour - the stars
        /// brightened towards white, because a star is a star rather than a coloured smudge, and the plate has
        /// already spread its light into the pixels around it. Dust is written premultiplied and dark, with the
        /// opacity in alpha, because its shader darkens with One OneMinusSrcAlpha rather than adding.
        /// </summary>
        private static Color Paint(RoleSpec role, Color plate)
        {
            switch (role.Role)
            {
                case Role.Stars:
                    return Color.Lerp(plate, Color.white, 0.45f);

                case Role.Dust:
                    // Dust is not black, it is the object's own colour with the light taken out of it. At 0.10
                    // every lane in every nebula was the same soot; the Crab's filaments are rust and red and
                    // have to read as rust and red even while they are darkening what is behind them. The
                    // colour is premultiplied - the shader darkens with One OneMinusSrcAlpha - so the hue rides
                    // in the colour and the bite rides in alpha.
                    var tint = plate * 0.34f;
                    return new Color(tint.r, tint.g, tint.b, 0.48f);

                default:
                    // Held back, because this layer overlaps itself: twelve thousand additive sprites through
                    // one volume add up, and at full plate brightness the sum saturates and every colour in the
                    // nebula ends as white. The colour has to survive the stacking, not just the sample.
                    return plate * 0.32f;
            }
        }

        /// <summary>A separable box blur of the luminance field, run once per layer. Edges clamp.</summary>
        private static float[] Blur(float[] source, int edge, int radius)
        {
            var temp = new float[source.Length];
            var outp = new float[source.Length];
            var window = radius * 2 + 1;

            for (var y = 0; y < edge; y++)
            {
                for (var x = 0; x < edge; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        sum += source[y * edge + Mathf.Clamp(x + k, 0, edge - 1)];
                    }

                    temp[y * edge + x] = sum / window;
                }
            }

            for (var x = 0; x < edge; x++)
            {
                for (var y = 0; y < edge; y++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                    {
                        sum += temp[Mathf.Clamp(y + k, 0, edge - 1) * edge + x];
                    }

                    outp[y * edge + x] = sum / window;
                }
            }

            return outp;
        }

        private static NebulaVolumeData Bake(Spec spec, Texture2D plate, RoleSpec role)
        {
            var pixels = plate.GetPixels();

            // Luminance, floored. Rec. 709 because the plates are sRGB images, not physical measurements -
            // this is a perceptual weighting for choosing where to put gas, not photometry.
            var weights = new float[pixels.Length];
            var peak = 0f;
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                var l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                weights[i] = l;
                if (l > peak)
                {
                    peak = l;
                }
            }

            var floor = peak * LuminanceFloor;

            // "Its surroundings", cheaply: a blur of the same luminance field. A star is a pixel standing well
            // above its surroundings; a dust lane is a pixel sitting well below surroundings that are
            // themselves lit. Neither can be told from the pixel alone, which is why one layer could never be
            // three.
            var neighbourhood = Blur(weights, SampleSize, NeighbourhoodRadius);
            var background = role.Role == Role.Stars ? Blur(weights, SampleSize, BackgroundRadius) : neighbourhood;

            // A deterministic seed per destination *and role*, so a rebuild reproduces the same cloud exactly
            // and the three layers do not land on the same points.
            var random = new System.Random(spec.Id.GetHashCode() ^ (int)role.Role * 7919);

            // What this layer is looking for, as a field over the plate, so the sampler can be proportional to
            // it. Built once per layer: the gas follows the light, the stars follow what stands above its
            // surroundings, the dust follows what sits below surroundings that are themselves lit.
            var want = new float[weights.Length];
            var wantPeak = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                var here = weights[i];
                var around = neighbourhood[i];
                float w;
                switch (role.Role)
                {
                    case Role.Stars:
                        w = here - background[i] * StarPeakRatio;
                        break;

                    case Role.Dust:
                        // Only where there is light behind it: a dark pixel in a dark corner is empty sky.
                        w = around > floor * 1.5f ? around - here * (1f / DustHoleRatio) : 0f;
                        break;

                    default:
                        w = here - floor;
                        break;
                }

                want[i] = Mathf.Max(0f, w);
                if (want[i] > wantPeak) wantPeak = want[i];
            }

            if (wantPeak <= 1e-5f)
            {
                wantPeak = 1e-5f;
            }

            var points = new List<StarVertDescriptor>(role.Count);
            var attempts = 0;
            var maxAttempts = role.Count * 60;

            while (points.Count < role.Count && attempts++ < maxAttempts)
            {
                var px = random.Next(SampleSize);
                var py = random.Next(SampleSize);
                var index = py * SampleSize + px;

                // Rejection sampling against *this layer's* own weight field, not against raw brightness.
                // Sharing one field was wrong in a way the counts made obvious: dust wants dark pixels, and a
                // brightness-proportional sampler had already thrown every dark pixel away before the dust
                // test could see it, so four of the seven nebulae came out with fewer than forty dust points.
                var weight = want[index];
                if (weight <= 0f)
                {
                    continue;
                }

                if (random.NextDouble() > weight / wantPeak)
                {
                    continue;
                }

                // Image plane, -1..1, with the pixel jittered inside its own cell so 512x512 does not show as
                // a grid once the player is close enough to see individual grains.
                var u = ((px + (float)random.NextDouble()) / SampleSize) * 2f - 1f;
                var v = ((py + (float)random.NextDouble()) / SampleSize) * 2f - 1f;

                if (!Depth(spec, u, v, weight / Mathf.Max(peak - floor, 1e-4f), random, out var w))
                {
                    continue;
                }

                var local = new Vector3(u, v, w) * spec.RadiusMetres;
                var colour = Paint(role, pixels[index]);

                points.Add(Describe(local, colour, spec.RadiusMetres, random, role.SizeScale));
            }

            // Stars through the whole volume, not only where the plate put them. The plate's stars are the ones
            // that happen to lie in front of or behind this nebula in one photograph; a player standing inside
            // is in a place, and a place has stars in every direction. Without these the sky between the gas is
            // empty and the nebula reads as an exhibit in a dark room rather than as somewhere in a galaxy.
            if (role.Role == Role.Stars)
            {
                for (var i = 0; i < FieldStars; i++)
                {
                    var direction = UnityEngine.Random.onUnitSphere;
                    if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;

                    // Cube-rooted so they are spread evenly through the volume rather than piled at the middle,
                    // and reaching past the gas so there is depth beyond it.
                    var t = (float)random.NextDouble();
                    var distance = Mathf.Lerp(0.45f, 1.35f, Mathf.Pow(t, 1f / 3f));

                    // A star field is not white: it is mostly cool, with a few warm ones.
                    var warm = random.NextDouble() < 0.18;
                    var shade = 0.65f + (float)random.NextDouble() * 0.35f;
                    var colour = warm
                        ? new Color(shade, shade * 0.82f, shade * 0.62f)
                        : new Color(shade * 0.78f, shade * 0.86f, shade);

                    points.Add(Describe(direction * (spec.RadiusMetres * distance), colour,
                                        spec.RadiusMetres, random, role.SizeScale * 0.8f));
                }
            }

            var asset = ScriptableObject.CreateInstance<NebulaVolumeData>();
            asset.points = points.ToArray();
            asset.destinationId = spec.Id;
            asset.plateAssetPath = spec.PlatePath;
            asset.radiusMetres = spec.RadiusMetres;
            asset.Provenance = ModelProvenance[spec.Model];

            var path = $"{DataFolder}/nebula_volume_{spec.Id}_{role.Role.ToString().ToLowerInvariant()}.asset";
            asset.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<NebulaVolumeData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Where this point sits front to back, in the same -1..1 units as <paramref name="u"/> and
        /// <paramref name="v"/>. Returns false to reject the sample entirely, which is how the shell model
        /// divides the projection's limb brightening back out.
        /// </summary>
        private static bool Depth(Spec spec, float u, float v, float brightness, System.Random random,
                                  out float w)
        {
            switch (spec.Model)
            {
                case Model.Shell:
                    return ShellDepth(u, v, spec.Thickness, random, out w);

                case Model.Bipolar:
                {
                    // The lobes lie along the image's vertical axis, which is how the Homunculus is
                    // conventionally printed. Each lobe is its own shell, centred half a radius out.
                    var sign = v >= 0f ? 1f : -1f;
                    var lobeV = (Mathf.Abs(v) - 0.5f) / Mathf.Max(spec.Thickness * 1.6f, 1e-4f);
                    var lobeU = u / Mathf.Max(spec.Thickness * 1.6f, 1e-4f);

                    if (!ShellDepth(lobeU, lobeV, spec.Thickness, random, out w))
                    {
                        return false;
                    }

                    w *= spec.Thickness * 1.6f;
                    w += sign * 0f;   // the lobes are offset across the image, not in depth
                    return true;
                }

                default:
                {
                    // A surface that undulates, rather than a flat slab with noise on it: without the warp the
                    // cloud reads as a sheet however thick it is made, because every part of it is at the same
                    // mean depth and the eye picks that up immediately.
                    var warp = Noise(u * 1.7f, v * 1.7f) - 0.5f;
                    var centre = warp * spec.Thickness * 1.4f;

                    // Brighter columns are usually deeper ones. Declared convention, recorded in Provenance.
                    var spread = spec.Thickness * Mathf.Lerp(0.35f, 1f, brightness);

                    w = centre + Gaussian(random) * spread * 0.5f;
                    return Mathf.Abs(w) <= 1f;
                }
            }
        }

        /// <summary>
        /// Depth on a spherical shell of unit radius, plus its thickness.
        ///
        /// The rejection is the important half. A shell photographed from outside is brightest at its rim
        /// because the line of sight passes through far more gas there - the classic bubble-looks-like-a-ring
        /// effect - and the path length through a thin shell at image radius <c>r</c> goes as
        /// <c>1 / sqrt(1 - r²)</c>. Sampling in proportion to image brightness and then placing the result on
        /// the true shell would therefore pile gas onto the rim twice over. Accepting with probability
        /// <c>sqrt(1 - r²)</c> divides exactly that factor back out, and what is left is the shell's own
        /// density.
        /// </summary>
        private static bool ShellDepth(float u, float v, float thickness, System.Random random, out float w)
        {
            w = 0f;

            var r = Mathf.Sqrt(u * u + v * v);
            if (r >= 1f)
            {
                // Material outside the shell's radius - a photograph always has some. Left near the plane of
                // the sky, thinly, rather than thrown away: it is really there, we simply cannot deproject it.
                w = Gaussian(random) * thickness * 0.35f;
                return Mathf.Abs(w) <= 1f;
            }

            var cos = Mathf.Sqrt(1f - r * r);

            if (random.NextDouble() > cos)
            {
                return false;
            }

            // Near wall or far wall, with equal weight - both are equally there, and which one a given photon
            // came from is exactly the information the photograph threw away.
            var side = random.NextDouble() < 0.5 ? -1f : 1f;
            w = side * cos + Gaussian(random) * thickness * 0.5f;
            return Mathf.Abs(w) <= 1f;
        }

        // ---------- packing

        /// <summary>
        /// Packs one point into the shared <see cref="StarVertDescriptor"/>. The reinterpretation of each field
        /// is documented on <see cref="NebulaVolumeData"/> and at the top of the shader; both have to agree
        /// with this method.
        /// </summary>
        private static StarVertDescriptor Describe(Vector3 local, Color colour, float radius,
                                                   System.Random random, float sizeScale)
        {
            // Cylindrical, because the shader turns the whole cloud with a single add on the angle rather than
            // a matrix - the same trick the Cosmic Web uses.
            var distance = new Vector2(local.x, local.z).magnitude;
            var angle = Mathf.Atan2(local.z, local.x);

            // 0 at the far side, 1 at the near side, for the shader's depth cue. Baked rather than derived from
            // the view, because the player walks around inside this and a view-derived front would swim.
            var depth = Mathf.InverseLerp(-radius, radius, local.z);

            // A little size variation, weighted towards the small: a cloud of identical grains reads as a
            // texture rather than as gas.
            var t = (float)random.NextDouble();
            var size = Mathf.Lerp(0.55f, 1.6f, t * t) * sizeScale;

            // Which cell of the 2x2 sprite atlas. Same convention as the Cosmic Web's points.
            var cell = random.Next(4);
            var uv = new Vector2(cell % 2 == 0 ? 0f : 0.5f, cell < 2 ? 0f : -0.5f);

            return new StarVertDescriptor
            {
                ellipseDistance = distance,
                curveOffset = angle,
                yOffset = local.y,
                ellipseOffset = depth,
                color = new Vector3(colour.r, colour.g, colour.b),
                size = size,
                random = (float)random.NextDouble(),
                uv = uv,
            };
        }

        // ---------- attaching

        /// <summary>
        /// Points the surrounding shell at the deep-sky panorama and wraps it right round.
        ///
        /// <para>The shell was already here and already wrong for this job in one specific way: it mapped the
        /// nebula's own square plate onto a <i>patch</i> of the dome - 0.42 of the azimuth by 0.28 of the
        /// elevation - which is what a photograph pinned to part of the sky looks like, and from inside it was
        /// the whole view at full brightness, a lavender wash with the gas lost in it. A panorama is a
        /// different kind of image: it is meant to wrap, so the spreads go to 1, and it is mostly black, so it
        /// gives the volume what it never had - somewhere to end, individual stars beyond the gas, and dark to
        /// read the colours against.</para>
        /// </summary>
        private static string Sky(GameObject root, string id)
        {
            var shell = root.transform.Find("place_shell");
            if (shell == null) return "none";

            var sky = Skies.TryGetValue(id, out var named) ? named : "deep_sky_panorama";
            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkyFolder}/{sky}.png");
            if (panorama == null) return $"missing {sky}";

            shell.gameObject.SetActive(true);
            var touched = 0;

            // By path, not through the renderers: PlaceShell holds its two materials on the component and
            // builds its dome at run time, so walking the prefab's renderers finds nothing to write to.
            foreach (var suffix in new[] { "sky", "gas" })
            {
                var materialPath = $"Assets/materials/place_shells/place_shell_{id}_{suffix}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || !material.HasProperty("_SpreadU")) continue;

                if (suffix == "gas")
                {
                    // The gas layer goes dark, and this is the image that kept coming back. PlaceShell builds
                    // both its layers at *run time*, so nothing in the prefab shows them and every check in the
                    // editor said the cards were off and the nebula was clean - while in play a second copy of
                    // the plate hung across the middle of the view. There is no job left for it: the gas is
                    // 22,000 points standing around the player now, and a photograph of the same gas painted on
                    // a dome in front of it is the flat thing this whole feature exists to replace.
                    if (material.HasProperty("_PlateGain")) material.SetFloat("_PlateGain", 0f);
                    var clear = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                    clear.a = 0f;
                    if (material.HasProperty("_Color")) material.SetColor("_Color", clear);
                    EditorUtility.SetDirty(material);
                    touched++;
                    continue;
                }

                material.SetTexture("_MainTex", panorama);
                material.SetFloat("_SpreadU", 1f);
                material.SetFloat("_SpreadV", 1f);
                material.SetFloat("_PlateYaw", 0f);
                material.SetFloat("_PlatePitch", 0f);

                // A backdrop, not a light source: the gas in front has to stay the brightest thing.
                if (material.HasProperty("_PlateGain")) material.SetFloat("_PlateGain", 0.55f);
                EditorUtility.SetDirty(material);
                touched++;
            }

            return touched > 0 ? $"{sky} on {touched} material(s)" : "no shell material takes a panorama";
        }

        private static bool Attach(ExperienceModule module, Spec spec,
                                   List<(RoleSpec Role, NebulaVolumeData Data, Material Material)> layers,
                                   StringBuilder report)
        {
            var path = AssetDatabase.GetAssetPath(module.ContentPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                report.AppendLine($"  {spec.Id}: {path} would not open.");
                return false;
            }

            try
            {
                var stripped = StripVolumes(root);

                // The flat cards go dark. They were the nebula when a nebula was a picture opened in front of
                // the map; the player now stands inside the gas itself, and a photograph of it hanging in the
                // same space reads as a poster in the room. Disabled rather than deleted, so the overlay look
                // can be put back by hand if this turns out to be worse.
                var darkened = 0;
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!child.gameObject.activeSelf) continue;

                    // The cards, and the photographic dome with them. The dome was written to stand behind a
                    // 70 cm overlay and it is right for that; from inside it is the entire sky at full
                    // brightness, and it washes the gas out completely - measured, not guessed: the same view
                    // with the dome switched off is the Helix's blue-green interior and orange rim against
                    // black, and with it on everything is a pale lavender haze. A dimmed backdrop may yet be
                    // better than none; that is a look decision for the headset.
                    if (!child.name.StartsWith("card_")) continue;
                    child.gameObject.SetActive(false);
                    darkened++;
                }

                // You cannot pick up the room you are standing in. The prefab root carries a grab sphere and a
                // ManipulationHandler from when a nebula was an overlay you held at arm's length - GDD 4.3's
                // "grabbable and scalable like a body". From inside, that collider is the whole view: every
                // drag lands on it, so the pointer grabs the nebula and the camera never orbits, which is why
                // looking around did nothing. Off, so a drag reaches the view instead.
                var freed = 0;
                foreach (var collider in root.GetComponents<Collider>())
                {
                    if (!collider.enabled) continue;
                    collider.enabled = false;
                    freed++;
                }

                foreach (var handler in root.GetComponents<GalaxyExplorer.XR.ManipulationHandler>())
                {
                    handler.enabled = false;
                }

                // One node per role, under a shared parent, so the three can be hidden or tuned separately and
                // a rebuild replaces the lot by name.
                var parent = new GameObject(NodeName);
                parent.transform.SetParent(root.transform, false);

                // The gas centres on the player when the place opens, so they are standing in it rather than
                // looking at it from a metre away. Only the volume moves: the panel and anything else on the
                // prefab keep the position the place gave them, which is where a player expects to read them.
                parent.AddComponent<CentreOnViewer>();

                var total = 0;
                var tally = new StringBuilder();
                foreach (var layer in layers)
                {
                    var node = new GameObject(NodeName + "_" + layer.Role.Role.ToString().ToLowerInvariant());
                    node.transform.SetParent(parent.transform, false);

                    var volume = node.AddComponent<NebulaVolume>();
                    var so = new SerializedObject(volume);
                    so.FindProperty("data").objectReferenceValue = layer.Data;
                    so.FindProperty("pointsMaterial").objectReferenceValue = layer.Material;
                    so.FindProperty("pointSizeMetres").floatValue = layer.Role.SpriteMetres;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    total += layer.Data.points.Length;
                    tally.Append(tally.Length > 0 ? ", " : string.Empty)
                         .Append($"{layer.Role.Role.ToString().ToLowerInvariant()} {layer.Data.points.Length:N0}");
                }

                var sky = Sky(root, spec.Id);

                PrefabUtility.SaveAsPrefabAsset(root, path);

                report.AppendLine(
                    $"  {spec.Id}: {total:N0} points ({tally}), sky {sky}, {spec.Model} model, radius " +
                    $"{spec.RadiusMetres:0.00} m, plate {Path.GetFileNameWithoutExtension(spec.PlatePath)}" +
                    (stripped > 0 ? $" (replaced {stripped} existing)" : string.Empty) +
                    (darkened > 0 ? $", {darkened} flat layer(s) switched off" : string.Empty) +
                    (freed > 0 ? ", grab collider off so the view can orbit" : string.Empty));

                return layers.Count > 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int StripVolumes(GameObject root)
        {
            var found = root.GetComponentsInChildren<NebulaVolume>(true);
            foreach (var volume in found)
            {
                Object.DestroyImmediate(volume.gameObject);
            }

            // The parent the layers hang under, left behind once its children are gone.
            var parent = root.transform.Find(NodeName);
            if (parent != null)
            {
                Object.DestroyImmediate(parent.gameObject);
            }

            return found.Length;
        }

        private static Material WriteMaterial(Shader shader, Texture2D atlas, string id, Role role)
        {
            var path = $"{MaterialFolder}/nebula_volume_{id}_{role.ToString().ToLowerInvariant()}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = atlas;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Dictionary<string, ExperienceModule> LoadDestinations()
        {
            var byId = new Dictionary<string, ExperienceModule>();

            foreach (var guid in AssetDatabase.FindAssets("t:ExperienceModule", new[] { DestinationFolder }))
            {
                var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(guid));
                if (module != null && !string.IsNullOrEmpty(module.Id))
                {
                    byId[module.Id] = module;
                }
            }

            return byId;
        }

        // ---------- small maths

        /// <summary>A standard normal, by Box-Muller. Clamped, because a four-sigma grain is a bug on screen.</summary>
        private static float Gaussian(System.Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            var value = Mathf.Sqrt(-2f * Mathf.Log((float)u1)) * Mathf.Cos(2f * Mathf.PI * (float)u2);
            return Mathf.Clamp(value, -2.5f, 2.5f);
        }

        /// <summary>
        /// Smooth value noise in 0..1. Unity's own <c>Mathf.PerlinNoise</c> would do, and is deliberately not
        /// used: it is documented as possibly differing between platforms and versions, and this runs once to
        /// produce a committed asset that has to be reproducible.
        /// </summary>
        private static float Noise(float x, float y)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var xf = x - xi;
            var yf = y - yi;

            // Smoothstep, so the field has no creases at the lattice lines.
            var sx = xf * xf * (3f - 2f * xf);
            var sy = yf * yf * (3f - 2f * yf);

            var a = Lattice(xi, yi);
            var b = Lattice(xi + 1, yi);
            var c = Lattice(xi, yi + 1);
            var d = Lattice(xi + 1, yi + 1);

            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Lattice(int x, int y)
        {
            var h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
