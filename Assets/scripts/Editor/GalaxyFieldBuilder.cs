using System.Collections.Generic;
using System.IO;
using CosmicSimulation;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the <b>Galaxies</b> experience: a shell of several hundred galaxies scattered around the
    /// player, seen from outside at a distance.
    /// <para>
    /// The galaxies are not invented and not generated. They are <b>cut out of two real deep-field
    /// photographs</b> already in the project - Hubble's Ultra Deep Field and Webb's SMACS 0723 - by
    /// slicing each plate into a grid and handing every cell to a sprite as a UV rect. Every shape the
    /// player sees is therefore a real galaxy that a real telescope recorded. That is both the cheapest
    /// option (no new texture asset, two materials, one draw call per plate) and the only one that does not
    /// require us to make up what a galaxy looks like.
    /// </para>
    /// <para>
    /// <b>Why a grid rather than hand-picked cutouts.</b> Hand-picking the prettiest galaxies would mean
    /// someone deciding which parts of a photograph count, and would quietly over-represent big bright
    /// spirals - a deep field is mostly small faint smudges, and that is the honest impression. The grid
    /// takes what is there. Cells that are nearly empty sky are dropped by the <c>Floor</c>/<c>Gain</c>
    /// contrast window rather than by taste.
    /// </para>
    /// </summary>
    public static class GalaxyFieldBuilder
    {
        private const string ModulePath = "Assets/data/experiences/galaxies.asset";
        private const string PrefabFolder = "Assets/prefabs/experiences";
        private const string PrefabName = "galaxies_content_prefab";
        private const string MaterialFolder = "Assets/materials/galaxy_materials";
        private const string ShaderPath = "Assets/shaders/galaxy_sprite_shader.shader";

        /// <summary>
        /// The two deep fields, and how finely each is sliced. Hubble's UDF is the deeper, busier plate so
        /// it takes the finer grid; Webb's SMACS field has larger, more separated objects and a foreground
        /// cluster, so a coarser grid keeps whole galaxies inside a cell instead of cutting them in half.
        /// </summary>
        private static readonly (string path, int cols, int rows, string credit)[] Plates =
        {
            ("Assets/Textures/galaxies/deep_field_hubble_udf_texture.jpg", 8, 8,
                "NASA/ESA Hubble Ultra Deep Field"),
            ("Assets/Textures/galaxies/deep_field_webb_smacs0723_texture.jpg", 6, 6,
                "NASA/ESA/CSA Webb SMACS 0723"),
        };

        private static readonly string[] Panel =
        {
            "Every point of light around you is a galaxy, and every one of them holds somewhere between a " +
            "hundred million and a trillion stars. They are not stars - they are the places stars live.",

            "These shapes are real. They are cut from two long-exposure photographs: Hubble's Ultra Deep " +
            "Field and Webb's view of the SMACS 0723 cluster, both of which stared at a patch of apparently " +
            "empty sky and found it crowded. The faintest of them are among the most distant things ever " +
            "recorded, and their light has been travelling for most of the age of the universe.",

            "Grab with two hands to turn the field or change its size.",
        };

        [MenuItem("Cosmic Simulation/Build Galaxies Content")]
        public static void Build()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"GalaxyFieldBuilder: no shader at {ShaderPath}. Nothing is built, because a " +
                               "field of magenta squares is worse than no field.");
                return;
            }

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(ModulePath);
            if (module == null)
            {
                Debug.LogError($"GalaxyFieldBuilder: no experience module at {ModulePath}.");
                return;
            }

            Directory.CreateDirectory(MaterialFolder);

            var materials = new List<Material>();
            var cutouts = new List<GalaxyCutout>();

            for (var p = 0; p < Plates.Length; p++)
            {
                var plate = Plates[p];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(plate.path);
                if (texture == null)
                {
                    Debug.LogError($"GalaxyFieldBuilder: missing plate {plate.path}; skipped. The field will " +
                                   "be built from whatever plates did load.");
                    continue;
                }

                materials.Add(Material(shader, texture, plate.path));

                // A grid of UV cells. A small inset keeps a cell off its neighbour's edge, so a galaxy
                // clipped by the cell boundary does not show a hard seam against the next sprite.
                const float inset = 0.06f;
                var cellW = 1f / plate.cols;
                var cellH = 1f / plate.rows;
                var index = materials.Count - 1;

                for (var y = 0; y < plate.rows; y++)
                {
                    for (var x = 0; x < plate.cols; x++)
                    {
                        cutouts.Add(new GalaxyCutout
                        {
                            Plate = index,
                            UvRect = new Rect(
                                (x + inset) * cellW,
                                (y + inset) * cellH,
                                cellW * (1f - 2f * inset),
                                cellH * (1f - 2f * inset)),

                            // The plates are square crops, so a cell is as square as its grid.
                            Aspect = (cellW * texture.width) / Mathf.Max(1f, cellH * texture.height),

                            // The contrast window that turns a photograph into a sprite with a usable
                            // alpha: everything below Floor is empty sky and becomes transparent, and Gain
                            // lifts what is left. Deep-field sky is not black - it carries a faint glow -
                            // so a floor of zero would render every cell as a dim grey tile.
                            Floor = 0.16f,
                            Gain = 2.6f,
                        });
                    }
                }
            }

            if (materials.Count == 0)
            {
                Debug.LogError("GalaxyFieldBuilder: no plates loaded, so there is nothing to build.");
                return;
            }

            var prefab = WritePrefab(materials, cutouts);
            if (prefab == null)
            {
                return;
            }

            var so = new SerializedObject(module);
            so.FindProperty("ContentPrefab").objectReferenceValue = prefab;
            so.FindProperty("Environment").enumValueIndex = (int)EnvironmentMode.FullBlack;

            var panel = so.FindProperty("Panel");
            panel.FindPropertyRelative("Title").stringValue = "Galaxies";
            var paragraphs = panel.FindPropertyRelative("Paragraphs");
            paragraphs.arraySize = Panel.Length - 1;
            for (var i = 0; i < Panel.Length - 1; i++)
            {
                paragraphs.GetArrayElementAtIndex(i).stringValue = Panel[i];
            }

            panel.FindPropertyRelative("Instruction").stringValue = Panel[Panel.Length - 1];
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"GalaxyFieldBuilder: {PrefabFolder}/{PrefabName}.prefab written and assigned to " +
                      $"'galaxies'.\n" +
                      $"  {materials.Count} plate material(s), {cutouts.Count} cutouts, 420 sprites drawn " +
                      $"from them.\n" +
                      $"  Cost: one draw call per plate material, not per galaxy - so {materials.Count} " +
                      "draw calls for the whole field.\n" +
                      "  Credits for CREDITS.md: " + string.Join("; ", CreditLines()));
        }

        private static IEnumerable<string> CreditLines()
        {
            foreach (var plate in Plates)
            {
                yield return plate.credit;
            }
        }

        private static Material Material(Shader shader, Texture2D texture, string platePath)
        {
            var name = Path.GetFileNameWithoutExtension(platePath).Replace("_texture", string.Empty);
            var path = $"{MaterialFolder}/{name}_sprites_material.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject WritePrefab(List<Material> materials, List<GalaxyCutout> cutouts)
        {
            var root = new GameObject(PrefabName);

            try
            {
                var field = root.AddComponent<GalaxyField>();
                var so = new SerializedObject(field);

                var plates = so.FindProperty("plateMaterials");
                plates.arraySize = materials.Count;
                for (var i = 0; i < materials.Count; i++)
                {
                    plates.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
                }

                var cuts = so.FindProperty("cutouts");
                cuts.arraySize = cutouts.Count;
                for (var i = 0; i < cutouts.Count; i++)
                {
                    var element = cuts.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Plate").intValue = cutouts[i].Plate;
                    element.FindPropertyRelative("Aspect").floatValue = cutouts[i].Aspect;
                    element.FindPropertyRelative("Floor").floatValue = cutouts[i].Floor;
                    element.FindPropertyRelative("Gain").floatValue = cutouts[i].Gain;

                    var rect = element.FindPropertyRelative("UvRect");
                    rect.rectValue = cutouts[i].UvRect;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                // The same interaction surface every other content prefab carries, in the same order
                // CosmicWebBuilder adds them: ScaleLimits requires the handler, so it comes after.
                var manipulation = root.AddComponent<ManipulationHandler>();
                var handlerSo = new SerializedObject(manipulation);
                SetEnum(handlerSo, "manipulationType", 2);   // OneAndTwoHanded
                handlerSo.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<ManipulationPointerRouter>();
                root.AddComponent<FreePlacementAnchor>();

                var limits = root.AddComponent<ScaleLimits>();
                var limitsSo = new SerializedObject(limits);
                SetEnum(limitsSo, "kind", (int)ScaleLimits.Kind.Custom);
                limitsSo.ApplyModifiedPropertiesWithoutUndo();

                // The player stands inside this shell, so the grab sphere has to be something a hand can
                // reach rather than the shell's own 3 m radius - the same reasoning the Cosmic Web uses.
                var grab = root.AddComponent<SphereCollider>();
                grab.radius = 0.6f;
                grab.isTrigger = true;

                root.AddComponent<GEInteractable>();

                Directory.CreateDirectory(PrefabFolder);
                var path = $"{PrefabFolder}/{PrefabName}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    Debug.LogError($"GalaxyFieldBuilder: could not save {path}.");
                }

                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetEnum(SerializedObject so, string name, int value)
        {
            var property = so.FindProperty(name);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }
    }
}
