// Licensed under the MIT License. See LICENSE in the project root for license information.

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
                    report.AppendLine($"  {spec.Id}: {Size}^3 field, {spec.Model} model -> {path}");
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
                        var density = Mathf.Clamp01(luminance * shape * Mathf.Lerp(0.45f, 1.35f, detail));

                        voxels[index] = new Color32(
                            (byte)(Mathf.Clamp01(colour.r) * 255f),
                            (byte)(Mathf.Clamp01(colour.g) * 255f),
                            (byte)(Mathf.Clamp01(colour.b) * 255f),
                            (byte)(density * 255f));
                    }
                }
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
