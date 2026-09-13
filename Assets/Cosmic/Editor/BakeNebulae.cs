using UnityEditor;
using UnityEngine;

namespace Cosmic.Editor
{
    internal static class BakeNebulae
    {
        const int SampleSize = 512;
        const float RadiusMetres = 0.35f;

        struct Spec
        {
            public string id, plate;
            public PointSources.NebulaShape shape;
            public float thicknessRatio;
        }

        static readonly Spec[] All =
        {
            S("helix", "Assets/Textures/nebulae/helix_texture.jpg", PointSources.NebulaShape.Shell, 0.22f),
            S("crab", "Assets/Textures/nebulae/crab_texture.jpg", PointSources.NebulaShape.Shell, 0.30f),
            S("ngc1501", "Assets/Textures/ngc1501_texture.jpg", PointSources.NebulaShape.Shell, 0.24f),
            S("homunculus", "Assets/Textures/nebulae/homunculus_texture.jpg", PointSources.NebulaShape.Bipolar,
              0.34f),
            S("orion", "Assets/Textures/nebulae/orion_texture.jpg", PointSources.NebulaShape.Cloud, 0.45f),
            S("pillars", "Assets/Textures/pillars_texture.tga", PointSources.NebulaShape.Cloud, 0.40f),
            S("trumpler14", "Assets/Textures/trumpler_texture.jpg", PointSources.NebulaShape.Cloud, 0.45f),
        };

        [MenuItem("Cosmic/Build/Nebulae")]
        public static void Nebulae()
        {
            if (Bake.Playing())
            {
                return;
            }

            int baked = 0, points = 0, wired = 0;
            Bake.Folders("points");
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var spec in All)
                {
                    var plate = Read(spec.plate);
                    if (plate == null)
                    {
                        Debug.LogError($"Cosmic nebulae: plate {spec.plate} is missing, so {spec.id} was not " +
                                       "baked and its place keeps whatever volume it already had.");
                        continue;
                    }

                    var pixels = plate.GetPixels();
                    Object.DestroyImmediate(plate);
                    var luminance = new float[pixels.Length];
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        luminance[i] = 0.2126f * pixels[i].r + 0.7152f * pixels[i].g + 0.0722f * pixels[i].b;
                    }

                    var seed = PointSources.Seed(spec.id);
                    var verts = PointSources.Nebula(luminance, SampleSize, SampleSize, pixels, spec.shape,
                                                    spec.thicknessRatio, seed);
                    var provenance = $"Cosmic/Build/Nebulae: PointSources.Nebula {spec.shape}, thickness " +
                                     $"{spec.thicknessRatio:F2}, seed {seed}, {verts.Length} points from " +
                                     $"{spec.plate} at {SampleSize} px.";
                    var cloud = Bake.Cloud($"nebula_{spec.id}", verts, RadiusMetres, spec.plate, provenance);
                    WriteMaterial(spec.id);
                    baked++;
                    points += verts.Length;
                    wired += Wire(spec, cloud, provenance) ? 1 : 0;
                }
            }
            finally
            {
                Bake.Done();
            }

            Debug.Log($"Cosmic nebulae: {baked} volumes, {points} points, {baked} materials, {wired} places " +
                      $"wired -> {Bake.Out}/points");
        }

        static void WriteMaterial(string id)
        {
            var material = Bake.Mat($"points_nebula_{id}", "_LAYER_NEBULA", 1, 1, 0, Color.white);
            if (material == null)
            {
                return;
            }

            material.SetFloat("_WSScale", 0.012f);
            material.SetFloat("_DepthDim", 0.55f);
            material.SetColor("_DepthTint", new Color(0.05f, 0.06f, 0.12f, 1f));
            material.SetFloat("_Shimmer", 0.12f);
            material.SetFloat("_ShimmerSpeed", 0.5f);
            material.SetFloat("_NearFadeStart", 0.12f);
            material.SetFloat("_NearFadeRange", 0.3f);
        }

        static bool Wire(Spec spec, PointCloud cloud, string provenance)
        {
            var place = AssetDatabase.LoadAssetAtPath<Place>($"{Bake.Out}/places/{spec.id}.asset");
            if (place == null)
            {
                Debug.LogError($"Cosmic nebulae: no place at {Bake.Out}/places/{spec.id}.asset, so {spec.id} is " +
                               "baked but nothing shows it.");
                return false;
            }

            place.volume.points = cloud;
            place.volume.radiusMetres = RadiusMetres;
            place.volume.plateAssetPath = spec.plate;
            place.volume.provenance = provenance;
            EditorUtility.SetDirty(place);
            return true;
        }

        // Through a blit so the plates keep their importer settings; Read/Write Enabled would double their memory on device.
        static Texture2D Read(string path)
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (source == null)
            {
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
                copy.ReadPixels(new Rect(0f, 0f, SampleSize, SampleSize), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static Spec S(string id, string plate, PointSources.NebulaShape shape, float thicknessRatio) =>
            new Spec { id = id, plate = plate, shape = shape, thicknessRatio = thicknessRatio };
    }
}
