// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Bakes each nebula into a 3D density texture, for the raymarched field that draws it as gas rather than
    /// as points.
    ///
    /// <para><b>Why a field at all, when the points are already there.</b> Points thin out: the same count
    /// through a bigger volume is a sparser volume, which is why every attempt to make the nebula larger made
    /// it emptier. A field has no count. The same texture marched over 9 m or 90 m is the same cost and the
    /// same look, and it is the only representation here that <b>occludes</b> - gas in front genuinely dimming
    /// gas behind - because the march carries transmittance. Points can add light; they cannot take it away.
    /// </para>
    ///
    /// <para><b>What goes in a voxel.</b> RGB is the plate's own colour at that place, as everywhere else in
    /// this feature; A is density. Density comes from the same two things the point bake uses - the plate's
    /// brightness for the two dimensions a photograph has, and the object's known shape for the one it does
    /// not - times an FBM noise so the cloud has structure between the pixels of a 512-wide plate. Without
    /// that noise a marched field is visibly smooth and reads as fog rather than as gas.</para>
    ///
    /// <para>64 cubed is deliberate: 1 MB at RGBA32 and it holds every feature a 512 plate can justify once
    /// the shape model has spread it through depth. 128 cubed is 8 MB per nebula and seven of those is a
    /// number the device will notice.</para>
    /// </summary>
    public static class NebulaFieldBuilder
    {
        private const string Folder = "Assets/data/nebula_fields";
        private const int Size = 64;
        private const string NodeName = "nebula_field";

        /// <summary>
        /// The two-colour palette each nebula is lit with, plus the three structure knobs that decide how much
        /// of its volume is empty.
        ///
        /// <para>Every emission nebula has this structure for the same physical reason: the ionising stars are
        /// inside it, so the gas near them is hot and shows its high-excitation lines - blue and blue-green -
        /// while the gas further out is cooler and recombining, which is red. Painting that ramp explicitly is
        /// what makes a marched field read as a nebula rather than as coloured fog, and it is the single
        /// biggest difference between this and the reference art.</para>
        ///
        /// <para>The plate's own colour is still underneath all of this; <c>colourMix</c> in the component
        /// decides how much the ramp wins.</para>
        /// </summary>
        private readonly struct Palette
        {
            public readonly Color Core;
            public readonly Color Shell;
            public readonly float Floor;
            public readonly float Contrast;
            public readonly float Warp;

            public Palette(Color core, Color shell, float floor, float contrast, float warp)
            {
                Core = core;
                Shell = shell;
                Floor = floor;
                Contrast = contrast;
                Warp = warp;
            }
        }

        private static readonly Dictionary<string, Palette> Palettes = new Dictionary<string, Palette>
        {
            // The Crab's interior is a blue synchrotron glow from the pulsar wind; its cage of filaments is
            // orange, from hydrogen and nitrogen. A high floor because those filaments are a cage with a great
            // deal of nothing between them, which is the whole look.
            ["crab"] = new Palette(new Color(0.45f, 0.70f, 1.70f), new Color(1.60f, 0.45f, 0.25f),
                0.26f, 2.8f, 0.85f),

            // Blue-green oxygen in the middle, red hydrogen at the rim - the Helix's whole appearance.
            ["helix"] = new Palette(new Color(0.30f, 1.20f, 1.10f), new Color(1.50f, 0.35f, 0.30f),
                0.24f, 2.6f, 0.70f),

            ["ngc1501"] = new Palette(new Color(0.50f, 0.95f, 1.40f), new Color(1.10f, 0.55f, 0.35f),
                0.22f, 2.4f, 0.70f),

            // Dust-reddened lobes rather than an ionised shell, so both ends of the ramp are warm.
            ["homunculus"] = new Palette(new Color(1.30f, 1.00f, 0.70f), new Color(1.50f, 0.45f, 0.30f),
                0.20f, 2.2f, 0.65f),

            // Star-forming clouds: a hot blue core around the Trapezium, red hydrogen everywhere else, and a
            // lower floor because these are genuinely filled with gas rather than being hollow shells.
            ["orion"] = new Palette(new Color(0.70f, 0.95f, 1.50f), new Color(1.45f, 0.40f, 0.35f),
                0.18f, 2.2f, 0.90f),

            ["pillars"] = new Palette(new Color(0.85f, 0.95f, 1.20f), new Color(1.30f, 0.65f, 0.35f),
                0.18f, 2.3f, 0.95f),

            ["trumpler14"] = new Palette(new Color(0.75f, 0.95f, 1.60f), new Color(1.35f, 0.50f, 0.35f),
                0.18f, 2.2f, 0.90f),
        };

        private static Palette PaletteFor(string id) =>
            Palettes.TryGetValue(id, out var palette)
                ? palette
                : new Palette(new Color(0.6f, 0.8f, 1.5f), new Color(1.4f, 0.5f, 0.3f), 0.22f, 2.4f, 0.75f);

        [MenuItem("Cosmic Simulation/Build Nebula Fields")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var built = 0;
            var report = new System.Text.StringBuilder();

            foreach (var spec in NebulaVolumeBuilder.AllSpecs)
            {
                var plate = NebulaVolumeBuilder.ReadPlate(spec.PlatePath, out var error);
                if (plate == null)
                {
                    report.AppendLine($"  {spec.Id}: {error}");
                    continue;
                }

                try
                {
                    var texture = Bake(spec, plate);
                    var path = $"{Folder}/nebula_field_{spec.Id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(texture, existing);
                        Object.DestroyImmediate(texture);
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(texture, path);
                    }

                    built++;

                    var baked = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    var attached = Attach(spec, baked);
                    report.AppendLine($"  {spec.Id}: {Size}^3 field, {spec.Model} model, {attached}");
                }
                finally
                {
                    Object.DestroyImmediate(plate);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"NebulaFieldBuilder: {built} field(s) baked at {Size} cubed, {Size * Size * Size * 4 / 1024 / 1024f:0.0} MB each.\n{report}");
        }

        /// <summary>
        /// Puts the baked field on the nebula's prefab, and turns the point gas off where it does.
        ///
        /// <para><b>Why the point layers go.</b> They were three attempts at the same job - continuous gas -
        /// and a sum of sprites cannot do it: however soft and dim the sprite, a few thousand of them read as
        /// a sponge, which is what every look at this has said. The field does that job properly, because it
        /// is continuous and it occludes. What the points are genuinely good at is the thing a field at 64
        /// cubed cannot do at all - individual, resolvable pinpoints - so the stars layer stays and the gas
        /// and dust layers are switched off rather than deleted, which keeps them one click away.</para>
        /// </summary>
        private static string Attach(NebulaVolumeBuilder.Spec spec, Texture3D baked)
        {
            if (baked == null)
            {
                return "not attached (the baked asset would not load)";
            }

            if (!NebulaVolumeBuilder.Destinations().TryGetValue(spec.Id, out var module) || module == null)
            {
                return "not attached (no destination module)";
            }

            if (module.ContentPrefab == null)
            {
                return "not attached (module has no ContentPrefab)";
            }

            var path = AssetDatabase.GetAssetPath(module.ContentPrefab);
            if (string.IsNullOrEmpty(path))
            {
                return "not attached (ContentPrefab is not an asset)";
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Replace by name, so running this twice does not stack two fields on top of each other.
                var existing = root.transform.Find(NodeName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var node = new GameObject(NodeName);
                node.transform.SetParent(root.transform, false);

                // Inactive while it is being assembled, and only switched on once it has its volume.
                //
                // NebulaField is ExecuteAlways and its OnEnable disables the component when it has nothing to
                // march - a reasonable guard that becomes a trap here, because AddComponent raises OnEnable
                // immediately, before Configure has handed it the texture. The component switched itself off
                // and that "off" was what SaveAsPrefabAsset then wrote into the prefab, so every field shipped
                // disabled and the nebula rendered as an empty frame with the point gas already turned off.
                node.SetActive(false);

                var palette = PaletteFor(spec.Id);
                var field = node.AddComponent<NebulaField>();
                field.Configure(baked, spec.RadiusMetres, palette.Core, palette.Shell,
                    palette.Floor, palette.Contrast, palette.Warp);

                // The field has to sit where the gas sits, and the gas centres itself on the player when the
                // place opens. Same component, same reason: otherwise the player stands beside the nebula.
                if (node.GetComponent<CentreOnViewer>() == null)
                {
                    node.AddComponent<CentreOnViewer>();
                }

                // Assembled. Now it may wake up, and OnEnable will find its volume where it expects it.
                field.enabled = true;
                node.SetActive(true);

                var silenced = 0;
                foreach (var volume in root.GetComponentsInChildren<NebulaVolume>(true))
                {
                    var name = volume.gameObject.name.ToLowerInvariant();
                    if (name.Contains("star"))
                    {
                        continue;
                    }

                    if (volume.gameObject.activeSelf)
                    {
                        volume.gameObject.SetActive(false);
                        silenced++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"field attached at {spec.RadiusMetres:0.0} m, {silenced} point gas layer(s) switched off";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Texture3D Bake(NebulaVolumeBuilder.Spec spec, Texture2D plate)
        {
            var pixels = plate.GetPixels();
            var plateSize = plate.width;

            var texture = new Texture3D(Size, Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };

            var voxels = new Color32[Size * Size * Size];

            // Density is accumulated as floats first and normalised at the end.
            //
            // It has to be. Density here is a plate luminance times a shape weight times a noise, and all
            // three are 0..1, so the product lands somewhere around a tenth however bright the object is.
            // Written straight into a byte that is a field whose values are all near zero - and the shader's
            // floor, which is what buys the black space, then subtracts more than the whole signal and the
            // nebula disappears. Measured: the first build of this rendered at a mean of 0.3/255. Normalising
            // makes the floor and contrast knobs mean the same thing for all seven objects, which is the only
            // way one palette table can describe them.
            var densities = new float[Size * Size * Size];
            var brightest = 0f;

            for (var z = 0; z < Size; z++)
            {
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        // -1..1 through the volume.
                        var u = (x + 0.5f) / Size * 2f - 1f;
                        var v = (y + 0.5f) / Size * 2f - 1f;
                        var w = (z + 0.5f) / Size * 2f - 1f;

                        var shape = NebulaVolumeBuilder.ShapeDensity(spec, u, v, w);
                        var index = z * Size * Size + y * Size + x;

                        if (shape <= 0f)
                        {
                            voxels[index] = new Color32(0, 0, 0, 0);
                            continue;
                        }

                        // The plate supplies colour and the two dimensions it has; the shape model supplies
                        // the third. Sampled at the voxel's own place in the image plane, so a voxel behind a
                        // bright filament is that filament's colour rather than an average of the whole plate.
                        var px = Mathf.Clamp(Mathf.RoundToInt((u * 0.5f + 0.5f) * (plateSize - 1)), 0, plateSize - 1);
                        var py = Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * (plateSize - 1)), 0, plateSize - 1);
                        var colour = pixels[py * plateSize + px];

                        var luminance = 0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b;

                        // Structure between the plate's own pixels. Three octaves is enough at 64 cubed; more
                        // would be detail the texture cannot hold.
                        var detail = Fbm(new Vector3(u, v, w) * 3.1f, 3);
                        var density = luminance * shape * Mathf.Lerp(0.45f, 1.35f, detail);

                        densities[index] = density;
                        if (density > brightest) brightest = density;

                        voxels[index] = new Color32(
                            (byte)(Mathf.Clamp01(colour.r) * 255f),
                            (byte)(Mathf.Clamp01(colour.g) * 255f),
                            (byte)(Mathf.Clamp01(colour.b) * 255f),
                            0);
                    }
                }
            }

            // Normalise against the densest voxel, so the field always uses the whole 0..1 range it is given.
            var scale = brightest > 1e-5f ? 1f / brightest : 0f;
            for (var i = 0; i < densities.Length; i++)
            {
                voxels[i].a = (byte)(Mathf.Clamp01(densities[i] * scale) * 255f);
            }

            texture.SetPixels32(voxels);
            texture.Apply(true);
            texture.name = $"nebula_field_{spec.Id}";
            return texture;
        }

        /// <summary>Value noise summed over octaves. Deterministic, and not worth a package dependency.</summary>
        private static float Fbm(Vector3 at, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var frequency = 1f;

            for (var i = 0; i < octaves; i++)
            {
                sum += amplitude * Noise(at * frequency);
                frequency *= 2.03f;
                amplitude *= 0.5f;
            }

            return Mathf.Clamp01(sum);
        }

        private static float Noise(Vector3 at)
        {
            // Unity's Perlin is 2D only, so three slices are combined - cheap, and at 64 cubed the seams a
            // purist would object to are below one voxel.
            var a = Mathf.PerlinNoise(at.x, at.y);
            var b = Mathf.PerlinNoise(at.y, at.z);
            var c = Mathf.PerlinNoise(at.z, at.x);
            return (a + b + c) / 3f;
        }
    }
}
