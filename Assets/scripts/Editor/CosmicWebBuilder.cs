// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the Cosmic Web experience's content prefab and its violet material (GDD 4.7, CS-065).
    ///
    /// Per CS-036 the four sceneless modules open from a <c>ContentPrefab</c> rather than a scene, and
    /// <c>cosmic_web</c> is one of them, so this writes the prefab <c>ExperienceDirector.Switch</c> will
    /// instantiate and points the module asset at it. Re-running replaces the prefab and the material in place,
    /// so anything already referencing them keeps working.
    ///
    /// There is deliberately no baked point asset. <see cref="CosmicWebGenerator"/> is deterministic from its
    /// seed, so the cloud is rebuilt at run time in a few frames; baking it the way <c>SpiralGalaxy</c> bakes
    /// its <c>StarsData</c> would put roughly 28 MB of YAML in the repository for no gain.
    /// </summary>
    public static class CosmicWebBuilder
    {
        private const string ShaderName = "CosmicSimulation/CosmicWebPoints";
        private const string ShaderPath = "Assets/shaders/cosmic_web_points_shader.shader";
        private const string ModulePath = "Assets/data/experiences/cosmic_web.asset";
        private const string AtlasPath = "Assets/Textures/stars_small_atlas.tga";
        private const string MaterialFolder = "Assets/materials/cosmic_web";
        private const string MaterialPath = MaterialFolder + "/cosmic_web_points_material.mat";
        private const string PrefabFolder = "Assets/prefabs/experiences";
        private const string PrefabName = "cosmic_web_content_prefab";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";

        /// <summary>
        /// 120 000 sprites, against the 400 000 the Quest 3 budget allows for a whole frame (Technical
        /// Overview 7.4). Well under, on purpose: the player stands *inside* this volume, so unlike the galaxy
        /// — which is 18 520 points seen from outside — a large share of the web's sprites are within a couple
        /// of metres and their screen area, not their count, is what costs. 120 000 is still six and a half
        /// times the whole Milky Way, and it is 720 000 vertices and 240 000 triangles per view before Android
        /// multiview doubles the vertex stage. CS-066 measures frame time on device and moves this number; it
        /// is one serialized field and one re-run.
        /// </summary>
        private const int PointCount = 120000;

        /// <summary>Half-width of the volume in metres. GDD 4.7 asks for a 5 m volume around the player.</summary>
        private const float VolumeRadius = 2.5f;

        /// <summary>Half-width of one sprite in metres: about 3 px across at the far side of the volume.</summary>
        private const float PointSize = 0.0032f;

        /// <summary>
        /// The grab shape. Not the whole volume: the player is inside it, so a 2.5 m sphere would swallow every
        /// near-pinch in the room and there would be no way to reach the dock. A core the size of a beach ball
        /// is reachable by far ray from anywhere inside the web and leaves everything else pokeable.
        /// </summary>
        private const float GrabRadius = 0.9f;

        // GDD 4.7: 0.5x to 2x of a 5 m volume.
        private const float MinMetres = 2.5f;
        private const float MaxMetres = 10f;

        [MenuItem("Cosmic Simulation/Build Cosmic Web Content")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("CosmicWebBuilder: leave play mode first.");
                return;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                // Loaded by path rather than by Shader.Find: a shader that failed to compile still resolves by
                // name to an error shader, and the prefab would ship pointing at it.
                Debug.LogError($"CosmicWebBuilder: no shader at {ShaderPath}. It must import without errors first.");
                return;
            }

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(ModulePath);
            if (module == null)
            {
                Debug.LogError($"CosmicWebBuilder: no experience module at {ModulePath}. Run Cosmic Simulation -> Import Copy first.");
                return;
            }

            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            var atlas = FindAtlas();
            if (atlas == null)
            {
                Debug.LogWarning($"CosmicWebBuilder: no point sprite atlas at {AtlasPath}; the material will draw white squares.");
            }

            var material = WriteMaterial(shader, atlas);
            EnsureAlwaysIncluded(shader);

            var prefab = WritePrefab(material);
            if (prefab == null)
            {
                Debug.LogError("CosmicWebBuilder: could not save the prefab; the module is unchanged.");
                return;
            }

            var so = new SerializedObject(module);
            so.FindProperty("ContentPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"CosmicWebBuilder: {PrefabFolder}/{PrefabName}.prefab written and assigned to '{module.Id}'.\n" +
                $"  {PointCount:N0} point sprites ({PointCount * 6:N0} vertices) in a {VolumeRadius * 2f:F1} m volume, " +
                $"sprite half size {PointSize * 1000f:F1} mm, grab sphere {GrabRadius:F2} m, " +
                $"scale {MinMetres:F1}-{MaxMetres:F1} m.\n" +
                $"  Material {MaterialPath}; the violet ramp lives there, so it can be retuned without regenerating a point.");
        }

        // ---------- material

        private static Material WriteMaterial(Shader shader, Texture2D atlas)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (atlas != null)
            {
                material.mainTexture = atlas;
            }

            // The web is one procedural draw, never an instanced batch; leaving the flag on would let Unity
            // pick the INSTANCING_ON variant, where unity_ObjectToWorld comes from an array this draw never
            // fills.
            material.enableInstancing = false;

            // The violet ramp (GDD 4.7: glowing violet filaments and brighter nodes). Additive, so these are
            // the light each class of point adds, not a surface colour: the void tracers are barely there, the
            // filaments carry the hue, and the knots run up towards white so they read as brighter rather than
            // merely more saturated.
            material.SetColor("_VoidColor", new Color(0.20f, 0.13f, 0.38f, 1f));
            material.SetColor("_FilamentColor", new Color(0.55f, 0.30f, 1.00f, 1f));
            material.SetColor("_NodeColor", new Color(0.95f, 0.72f, 1.00f, 1f));
            material.SetFloat("_RampMid", 0.55f);
            material.SetColor("_Color", Color.white);

            material.SetFloat("_WSScale", PointSize); // overwritten per frame with the object's world scale
            material.SetFloat("_Age", 0f);
            material.SetFloat("_Shimmer", 0.18f);
            material.SetFloat("_ShimmerSpeed", 0.7f);
            material.SetFloat("_TransitionAlpha", 1f);
            material.SetFloat("_NearFadeStart", 0.15f);
            material.SetFloat("_NearFadeRange", 0.35f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FindAtlas()
        {
            var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (direct != null)
            {
                return direct;
            }

            foreach (var guid in AssetDatabase.FindAssets("stars_small_atlas t:Texture2D"))
            {
                var found = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Nothing in a scene references this material — the prefab is reached through an
        /// <c>ExperienceModule</c> asset — so the shader is stated explicitly rather than left to the build's
        /// reachability analysis. A stripped shader comes back as magenta on the headset and nowhere else.
        /// </summary>
        private static void EnsureAlwaysIncluded(Shader shader)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                Debug.LogWarning($"CosmicWebBuilder: could not open {GraphicsSettingsPath}; add '{ShaderName}' " +
                                 "to Always Included Shaders by hand.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning($"CosmicWebBuilder: no Always Included Shaders list in {GraphicsSettingsPath}; " +
                                 $"add '{ShaderName}' by hand.");
                return;
            }

            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    return;
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // ---------- prefab

        private static GameObject WritePrefab(Material material)
        {
            var root = new GameObject(PrefabName);

            var renderer = root.AddComponent<CosmicWebRenderer>();

            // Two-handed rotate and scale only (GDD 4.7). No move: the volume is meant to stay around the
            // player, and a one-handed drag on something you are standing inside reads as the room sliding.
            var manipulation = root.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            SetEnum(manipulationSo, "manipulationType", (int)ManipulationHandler.HandMovementType.TwoHandedOnly);
            SetEnum(manipulationSo, "twoHandedManipulationType", (int)ManipulationHandler.TwoHandedManipulation.RotateScale);
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
            SetEnum(limitsSo, "kind", (int)ScaleLimits.Kind.Custom);
            SetFloat(limitsSo, "customMinMetres", MinMetres);
            SetFloat(limitsSo, "customMaxMetres", MaxMetres);
            // The web has no Renderer to measure — it is a ComputeBuffer drawn by a command buffer — so its
            // width is stated instead.
            SetFloat(limitsSo, "authoredMetresAtUnitScale", VolumeRadius * 2f);
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            var grab = root.AddComponent<SphereCollider>();
            grab.radius = GrabRadius;

            root.AddComponent<GEInteractable>();

            var rendererSo = new SerializedObject(renderer);
            SetObject(rendererSo, "pointsMaterial", material);
            SetInt(rendererSo, "pointCount", PointCount);
            SetFloat(rendererSo, "volumeRadiusMetres", VolumeRadius);
            SetFloat(rendererSo, "pointSizeMetres", PointSize);
            rendererSo.ApplyModifiedPropertiesWithoutUndo();

            var path = $"{PrefabFolder}/{PrefabName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
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

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var property = Find(so, name);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetObject(SerializedObject so, string name, Object value)
        {
            var property = Find(so, name);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static SerializedProperty Find(SerializedObject so, string name)
        {
            var property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning($"CosmicWebBuilder: '{so.targetObject.GetType().Name}.{name}' no longer exists; " +
                                 "that setting was left at its default.");
            }

            return property;
        }
    }
}
