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
        private const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";
        private const string NodeName = "nebula_volume";

        /// <summary>Working resolution the plate is read at. 512 is ample: the cloud is sampled, not copied.</summary>
        private const int SampleSize = 512;

        /// <summary>
        /// Points per cloud. 28 000 is 56 000 triangles an eye - about 14% of the 400 000-point frame budget in
        /// Technical Overview 7.4, and a destination overlay is the only heavy thing on screen while it is open.
        /// </summary>
        private const int PointCount = 28000;

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

            /// <summary>Half the cloud's width in metres. GDD 4.3 asks for a 70 cm overlay.</summary>
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
            new Spec("helix", "Assets/Textures/nebulae/helix_texture.jpg", Model.Shell, 0.35f, 0.22f),

            // A supernova remnant. Not a clean sphere - it is a filamentary cage around a pulsar wind nebula -
            // but it is a genuine expanding shell, and a shell is far closer to the truth than a flat card.
            new Spec("crab", "Assets/Textures/nebulae/crab_texture.jpg", Model.Shell, 0.35f, 0.30f),

            // Another planetary nebula, and a notably round one; its common name is the Oyster.
            new Spec("ngc1501", "Assets/Textures/ngc1501_texture.jpg", Model.Shell, 0.35f, 0.24f),

            // Two lobes either side of Eta Carinae, thrown out in the 1840s eruption.
            new Spec("homunculus", "Assets/Textures/nebulae/homunculus_texture.jpg", Model.Bipolar, 0.35f, 0.34f),

            // Irregular clouds. Depth is convention here, and says so.
            new Spec("orion", "Assets/Textures/nebulae/orion_texture.jpg", Model.Cloud, 0.35f, 0.45f),
            new Spec("pillars", "Assets/Textures/pillars_texture.tga", Model.Cloud, 0.35f, 0.40f),
            new Spec("trumpler14", "Assets/Textures/trumpler_texture.jpg", Model.Cloud, 0.35f, 0.45f),
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
                    var data = Bake(spec, plate);
                    var material = WriteMaterial(shader, atlas, spec.Id);
                    if (Attach(module, spec, data, material, report))
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

        private static NebulaVolumeData Bake(Spec spec, Texture2D plate)
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

            // A deterministic seed per destination, so a rebuild reproduces the same cloud exactly and a diff
            // of the asset is empty when nothing changed.
            var random = new System.Random(spec.Id.GetHashCode());

            var points = new List<StarVertDescriptor>(PointCount);
            var attempts = 0;
            var maxAttempts = PointCount * 60;

            while (points.Count < PointCount && attempts++ < maxAttempts)
            {
                var px = random.Next(SampleSize);
                var py = random.Next(SampleSize);
                var index = py * SampleSize + px;

                var weight = weights[index] - floor;
                if (weight <= 0f)
                {
                    continue;
                }

                // Rejection sampling against the plate's own brightness, so the cloud's two-dimensional
                // structure is the photograph's and nothing else.
                if (random.NextDouble() > weight / Mathf.Max(peak - floor, 1e-4f))
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
                var colour = pixels[index];

                points.Add(Describe(local, colour, spec.RadiusMetres, random));
            }

            var asset = ScriptableObject.CreateInstance<NebulaVolumeData>();
            asset.points = points.ToArray();
            asset.destinationId = spec.Id;
            asset.plateAssetPath = spec.PlatePath;
            asset.radiusMetres = spec.RadiusMetres;
            asset.Provenance = ModelProvenance[spec.Model];

            var path = $"{DataFolder}/nebula_volume_{spec.Id}.asset";
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
                                                   System.Random random)
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
            var size = Mathf.Lerp(0.55f, 1.6f, t * t);

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

        private static bool Attach(ExperienceModule module, Spec spec, NebulaVolumeData data, Material material,
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

                var node = new GameObject(NodeName);
                node.transform.SetParent(root.transform, false);

                var volume = node.AddComponent<NebulaVolume>();
                var so = new SerializedObject(volume);
                so.FindProperty("data").objectReferenceValue = data;
                so.FindProperty("pointsMaterial").objectReferenceValue = material;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);

                report.AppendLine(
                    $"  {spec.Id}: {data.points.Length:N0} points, {spec.Model} model, radius " +
                    $"{spec.RadiusMetres:0.00} m, plate {Path.GetFileNameWithoutExtension(spec.PlatePath)}" +
                    (stripped > 0 ? $" (replaced {stripped} existing)" : string.Empty));

                return true;
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

            return found.Length;
        }

        private static Material WriteMaterial(Shader shader, Texture2D atlas, string id)
        {
            var path = $"{MaterialFolder}/nebula_volume_{id}.mat";
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
