// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicSimulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Makes the picture on each dock tile.
    ///
    /// A tile is a photograph of the place, so where a place already exists as a scene the picture is rendered
    /// from that scene rather than drawn: open it, frame everything in it, and take the shot. The four places
    /// that are still Phase 4 and 5 work have no scene to photograph, so they get a colour study standing in
    /// for the eventual image, using the same hues the design boards use — recognisably a placeholder, but not
    /// an empty grey rectangle.
    ///
    /// Re-run whenever a scene changes. Output is 440 x 248, the tile at four pixels per millimetre.
    /// </summary>
    public static class ThumbnailBuilder
    {
        private const string Folder = "Assets/ui/thumbnails";
        private const int Width = 440;
        private const int Height = 248;

        // Hues per place, from the design boards: centre, middle, edge.
        private static readonly Dictionary<string, Color[]> Studies = new Dictionary<string, Color[]>
        {
            { "cosmic_web",           new[] { Hex(0x6B47B8), Hex(0x291A57), Hex(0x080817) } },
            { "galaxies",             new[] { Hex(0x4D526B), Hex(0x141724), Hex(0x05050A) } },
            { "milky_way",            new[] { Hex(0xD9E5FF), Hex(0x4D85DB), Hex(0x050A1F) } },
            { "andromeda",            new[] { Hex(0xFFEDD1), Hex(0x8C4D42), Hex(0x0D080D) } },
            { "solar_system",         new[] { Hex(0xFFB84D), Hex(0x73330F), Hex(0x05050A) } },
            { "solar_system_planets", new[] { Hex(0x8CB2EB), Hex(0x294270), Hex(0x05080F) } },
            { "sagittarius_a",        new[] { Hex(0xFFC752), Hex(0x8C3305), Hex(0x050300) } },
        };

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        [MenuItem("Cosmic Simulation/Build Dock Thumbnails")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ThumbnailBuilder: leave play mode first.");
                return;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError($"ThumbnailBuilder: '{SceneManager.GetSceneAt(i).name}' has unsaved changes.");
                    return;
                }
            }

            Directory.CreateDirectory(Folder);
            var returnTo = SceneManager.GetActiveScene().path;

            var modules = AssetDatabase.FindAssets("t:ExperienceModule", new[] { "Assets/data/experiences" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && m.Kind == ExperienceKind.DockTile)
                .ToArray();

            var rendered = 0;
            foreach (var module in modules)
            {
                var path = $"{Folder}/{module.Id}.png";
                byte[] bytes = null;

                if (!string.IsNullOrEmpty(module.SceneName))
                {
                    bytes = RenderScene(module.SceneName);

                    // Some of this content is drawn by command buffers attached to the game camera - the
                    // galaxy's stars are - and a throwaway camera never receives them, so the shot comes back
                    // black. A tile nobody can read is worse than an honest stand-in, so check and fall back.
                    if (bytes != null)
                    {
                        var legible = IsLegible(bytes, out var reading);
                        if (legible)
                        {
                            Debug.Log($"ThumbnailBuilder: '{module.SceneName}' photographed ({reading}).");
                            rendered++;
                        }
                        else
                        {
                            Debug.LogWarning($"ThumbnailBuilder: '{module.SceneName}' rendered too dark to read " +
                                             $"({reading}); its visuals need the game camera. Using a colour study.");
                            bytes = null;
                        }
                    }
                }

                File.WriteAllBytes(path, bytes ?? ColourStudy(module.Id));
            }

            if (!string.IsNullOrEmpty(returnTo))
            {
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            }

            AssetDatabase.Refresh();
            Assign(modules);
            Debug.Log($"ThumbnailBuilder: {rendered} rendered from scenes, {modules.Length - rendered} colour studies.");
        }

        private static void Assign(IEnumerable<ExperienceModule> modules)
        {
            foreach (var module in modules)
            {
                var path = $"{Folder}/{module.Id}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 512;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    continue;
                }

                var so = new SerializedObject(module);
                so.FindProperty("DockThumbnail").objectReferenceValue = sprite;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(module);
            }

            AssetDatabase.SaveAssets();
        }

        // ---------- photographing a scene

        private static byte[] RenderScene(string sceneName)
        {
            var guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
            var scenePath = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName &&
                                     !p.Contains("_backup_original"));

            if (scenePath == null)
            {
                Debug.LogWarning($"ThumbnailBuilder: no scene '{sceneName}'");
                return null;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var bounds = ContentBounds();
            if (!bounds.HasValue)
            {
                Debug.LogWarning($"ThumbnailBuilder: '{sceneName}' has nothing to photograph");
                return null;
            }

            var rig = new GameObject("thumbnail_camera");
            var camera = rig.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 1f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.01f;

            // Back off far enough for the whole thing to fit, then come in slightly: a tile reads better
            // filled than with the subject swimming in empty space.
            var radius = bounds.Value.extents.magnitude;
            var distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.85f;
            camera.farClipPlane = Mathf.Max(1000f, distance * 4f);

            var direction = new Vector3(0.35f, 0.22f, -1f).normalized;
            rig.transform.position = bounds.Value.center - direction * distance;
            rig.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            var bytes = tex.EncodeToPNG();

            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rig);
            return bytes;
        }

        /// <summary>
        /// Is there enough in this picture to recognise a place? Mean brightness is the wrong test on its own,
        /// because space is legitimately almost all black; what matters is whether any appreciable number of
        /// pixels are actually lit.
        /// </summary>
        private static bool IsLegible(byte[] png, out string reading)
        {
            reading = "unreadable";
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(png))
            {
                Object.DestroyImmediate(tex);
                return false;
            }

            var pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);

            var total = 0f;
            var lit = 0;
            foreach (var p in pixels)
            {
                var luma = p.r * 0.299f + p.g * 0.587f + p.b * 0.114f;
                total += luma;
                if (luma > 0.18f)
                {
                    lit++;
                }
            }

            var mean = total / pixels.Length;
            var litFraction = (float)lit / pixels.Length;
            reading = $"mean {mean:F4}, lit {litFraction:P2}";

            // Half a per cent of lit pixels is roughly "there is a visible object in here somewhere".
            return litFraction > 0.005f;
        }

        private static Bounds? ContentBounds()
        {
            Bounds? total = null;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer)
                {
                    continue;
                }

                // Skip anything enormous: a skybox shell or a background quad would swallow the framing.
                if (r.bounds.extents.magnitude > 5000f)
                {
                    continue;
                }

                total = total.HasValue ? Grow(total.Value, r.bounds) : r.bounds;
            }

            return total;
        }

        private static Bounds Grow(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        // ---------- a stand-in for a place that has no scene yet

        private static byte[] ColourStudy(string id)
        {
            if (!Studies.TryGetValue(id, out var stops))
            {
                stops = new[] { Hex(0x4D526B), Hex(0x141724), Hex(0x05050A) };
            }

            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var pixels = new Color[Width * Height];
            var cx = Width * 0.5f;
            var cy = Height * 0.5f;
            var maxR = Mathf.Sqrt(cx * cx + cy * cy);
            var random = new System.Random(id.GetHashCode());

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxR;
                    var colour = d < 0.4f
                        ? Color.Lerp(stops[0], stops[1], d / 0.4f)
                        : Color.Lerp(stops[1], stops[2], (d - 0.4f) / 0.6f);

                    // A dusting of stars, so it reads as sky rather than as a gradient swatch.
                    if (random.NextDouble() < 0.0016)
                    {
                        var b = 0.5f + (float)random.NextDouble() * 0.5f;
                        colour = Color.Lerp(colour, Color.white, b);
                    }

                    pixels[y * Width + x] = colour;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return bytes;
        }
    }
}
