using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cosmic.Editor
{
    public static class Bake
    {
        internal const string Out = "Assets/Cosmic/Data/Generated";
        internal const string MaterialsFolder = "Assets/Cosmic/Prefabs/materials";
        internal const string PointsShaderPath = "Assets/Cosmic/Shaders/Points.shader";
        internal const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";

        static readonly string[] LayerKeywords =
            { "_LAYER_STARS", "_LAYER_CLOUDS", "_LAYER_DUST", "_LAYER_WEB", "_LAYER_NEBULA" };

        [MenuItem("Cosmic/Build/Bake All")]
        public static void BakeAll()
        {
            Galaxies();
            CosmicWeb();
            BakeNebulae.Nebulae();
        }

        [MenuItem("Cosmic/Build/Galaxies")]
        public static void Galaxies()
        {
            if (Playing())
            {
                return;
            }

            int built = 0, clouds = 0, points = 0, wired = 0;
            Folders("galaxies", "points");
            try
            {
                AssetDatabase.StartAssetEditing();
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(PointsShaderPath);
                if (shader == null)
                {
                    Debug.LogError($"Cosmic galaxies: no point shader at {PointsShaderPath}, so nothing was " +
                                   "written rather than five galaxies that draw with a null shader.");
                    return;
                }

                foreach (var (id, fill) in BakeGalaxies.Set)
                {
                    var path = $"{Out}/galaxies/{id}.asset";
                    var galaxy = Find<Galaxy>(path);
                    galaxy.id = id;
                    galaxy.kind = GalaxyKind.SpiralArms;
                    fill(galaxy);
                    for (var i = 0; i < galaxy.layers.Length; i++)
                    {
                        var layer = galaxy.layers[i];
                        layer.shader = shader;
                        var verts = PointSources.Spiral(galaxy, layer);
                        var envelope = PointSources.Envelope(verts, galaxy);
                        var name = $"{id}_{layer.id}";
                        layer.points = Cloud(name, verts, envelope, path,
                                             $"Cosmic/Build/Galaxies: PointSources.Spiral, seed {layer.seed}, " +
                                             $"{verts.Length} points, envelope {envelope:F3}.");
                        galaxy.layers[i] = layer;
                        var dust = layer.layer == GalaxyLayer.Dust;
                        var material = Mat($"points_{name}", Keyword(layer.layer), 1, dust ? 10 : 1, dust ? 2 : 0,
                                           layer.tint * layer.tintMultiplier);
                        if (material != null)
                        {
                            material.SetFloat("_WSScale", layer.worldSpaceScale);
                        }

                        clouds++;
                        points += verts.Length;
                    }

                    EditorUtility.SetDirty(galaxy);
                    built++;
                    wired += Wire(id, galaxy) ? 1 : 0;
                }
            }
            finally
            {
                Done();
            }

            Debug.Log($"Cosmic galaxies: {built} galaxies, {clouds} point clouds, {points} points, " +
                      $"{clouds} materials, {wired} places wired -> {Out}/galaxies");
        }

        [MenuItem("Cosmic/Build/Cosmic Web")]
        public static void CosmicWeb()
        {
            if (Playing())
            {
                return;
            }

            var settings = PointSources.WebSettings.Default;
            var verts = Array.Empty<StarVert>();
            Folders("points");
            try
            {
                AssetDatabase.StartAssetEditing();
                verts = PointSources.Web(settings);
                var cloud = Cloud("cosmic_web", verts, settings.radiusMetres, string.Empty,
                                  $"Cosmic/Build/Cosmic Web: PointSources.Web, seed {settings.seed}, " +
                                  $"{verts.Length} points, radius {settings.radiusMetres:F2} m.");
                var material = Mat("points_cosmic_web", "_LAYER_WEB", 1, 1, 0, Color.white);
                if (material != null)
                {
                    material.SetFloat("_WSScale", 0.0032f);
                    material.SetColor("_VoidColor", new Color(0.20f, 0.13f, 0.38f));
                    material.SetColor("_FilamentColor", new Color(0.55f, 0.30f, 1.00f));
                    material.SetColor("_NodeColor", new Color(0.95f, 0.72f, 1.00f));
                    material.SetFloat("_RampMid", 0.55f);
                    material.SetFloat("_Shimmer", 0.18f);
                    material.SetFloat("_ShimmerSpeed", 0.7f);
                    material.SetFloat("_NearFadeStart", 0.15f);
                    material.SetFloat("_NearFadeRange", 0.35f);
                }

                var place = AssetDatabase.LoadAssetAtPath<Place>($"{Out}/places/cosmic_web.asset");
                if (place == null)
                {
                    Debug.LogError($"Cosmic web: no place at {Out}/places/cosmic_web.asset, so the cloud was " +
                                   "baked but nothing shows it.");
                }
                else
                {
                    place.volume.points = cloud;
                    place.volume.radiusMetres = settings.radiusMetres;
                    place.volume.provenance = cloud.provenance;
                    EditorUtility.SetDirty(place);
                }
            }
            finally
            {
                Done();
            }

            Debug.Log($"Cosmic web: 1 point cloud, {verts.Length} points, 1 material -> {Out}/points");
        }

        static string Keyword(GalaxyLayer layer) => layer == GalaxyLayer.Clouds ? "_LAYER_CLOUDS"
            : layer == GalaxyLayer.Dust ? "_LAYER_DUST" : "_LAYER_STARS";

        static bool Wire(string id, Galaxy galaxy)
        {
            var place = AssetDatabase.LoadAssetAtPath<Place>($"{Out}/places/{id}.asset");
            if (place == null)
            {
                Debug.LogError($"Cosmic galaxies: no place at {Out}/places/{id}.asset, so {id} is baked but " +
                               "nothing opens it.");
                return false;
            }

            place.galaxy = galaxy;
            EditorUtility.SetDirty(place);
            return true;
        }

        internal static PointCloud Cloud(string name, StarVert[] points, float radiusMetres, string source,
                                         string provenance)
        {
            var cloud = Find<PointCloud>($"{Out}/points/{name}.asset");
            cloud.points = points;
            cloud.radiusMetres = radiusMetres;
            cloud.sourceAssetPath = source;
            cloud.provenance = provenance;
            EditorUtility.SetDirty(cloud);
            return cloud;
        }

        internal static Material Mat(string name, string keyword, int srcBlend, int dstBlend, int cull, Color color)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PointsShaderPath);
            if (shader == null)
            {
                Debug.LogError($"Cosmic bake: no point shader at {PointsShaderPath}, so {name} has no material.");
                return null;
            }

            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            foreach (var candidate in LayerKeywords)
            {
                if (candidate == keyword)
                {
                    material.EnableKeyword(candidate);
                }
                else
                {
                    material.DisableKeyword(candidate);
                }
            }

            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            material.SetColor("_Color", color);
            material.SetFloat("_TransitionAlpha", 1f);
            material.SetFloat("_SrcBlend", srcBlend);
            material.SetFloat("_DstBlend", dstBlend);
            material.SetFloat("_Cull", cull);
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static T Find<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        internal static void Folders(params string[] generated)
        {
            var root = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(Path.Combine(root, MaterialsFolder));
            foreach (var sub in generated)
            {
                Directory.CreateDirectory(Path.Combine(root, Out, sub));
            }

            AssetDatabase.Refresh();
        }

        internal static void Done()
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static bool Playing()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            Debug.LogError("Cosmic bake: leave play mode first. Nothing was written.");
            return true;
        }
    }
}
