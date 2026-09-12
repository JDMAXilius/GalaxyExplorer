// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the nebula overlay prefabs — the things <c>ExperienceDirector.OpenDestination</c> instantiates
    /// when the player pinches a destination tag on the Milky Way map (GDD 4.3).
    ///
    /// An overlay is a stack of parallel transparent cards, each showing one luminance band of the same plate,
    /// so the faint outer gas and the bright core sit on different planes and the thing reads as a cloud rather
    /// than a poster. <see cref="NebulaOverlay"/> points the stack at the player and turns the cards;
    /// <c>CosmicSimulation/NebulaCard</c> does the banding and the three fades that hide the seams.
    ///
    /// Four cards, not the six the Technical Overview allows. Every card is a full-coverage alpha-blended
    /// quad and the black halo behind it is a fifth transparent layer, so on a Quest 3 the overlay is already
    /// five layers of overdraw across a 70 cm object at arm's length. Four bands is enough to separate wisp
    /// from core, and the two spare layers buy more frame time than they buy depth.
    ///
    /// The plates are photographs with black sky and no alpha, which is exactly what the luminance banding is
    /// for. CS-052 will replace them with purpose-made layer sets; nothing here has to change when it does,
    /// beyond re-running this and retuning the bands.
    ///
    /// Re-running replaces the prefabs and materials in place, so scene and asset references survive.
    /// </summary>
    public static class NebulaPrefabBuilder
    {
        private const string DestinationFolder = "Assets/data/destinations";
        private const string TextureFolder = "Assets/Textures";
        private const string PrefabFolder = "Assets/prefabs/nebulae";
        private const string MaterialFolder = "Assets/materials/nebulae";
        private const string ShaderName = "CosmicSimulation/NebulaCard";

        private const int CardCount = 4;
        private const float CardWidth = 0.7f;    // GDD 4.3: a nebula opens about 70 cm across
        private const float CardSpacing = 0.1f;  // Technical Overview 7.6: 10-20 cm between planes
        private const float Spin = 0.5f;         // Technical Overview 7.6: +/- 0.5 deg/s

        // Which plate each destination is drawn from. Four were sourced clean in CS-005; the other three are
        // the original project's textures, which is why they sit in a different folder under different names.
        private static readonly Dictionary<string, string> Plates = new Dictionary<string, string>
        {
            { "helix",      "Assets/Textures/nebulae/helix_texture.jpg" },
            { "orion",      "Assets/Textures/nebulae/orion_texture.jpg" },
            { "crab",       "Assets/Textures/nebulae/crab_texture.jpg" },
            { "homunculus", "Assets/Textures/nebulae/homunculus_texture.jpg" },
            { "pillars",    "Assets/Textures/pillars_texture.tga" },
            { "ngc1501",    "Assets/Textures/ngc1501_texture.jpg" },
            { "trumpler14", "Assets/Textures/trumpler_texture.jpg" },
        };

        [MenuItem("Cosmic Simulation/Build Nebula Prefabs")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("NebulaPrefabBuilder: leave play mode first.");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"NebulaPrefabBuilder: shader '{ShaderName}' not found. " +
                               "Assets/shaders/nebula_card_shader.shader must import without errors first.");
                return;
            }

            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var modules = AssetDatabase.FindAssets("t:ExperienceModule", new[] { DestinationFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && m.Kind == ExperienceKind.Destination)
                .OrderBy(m => m.Id)
                .ToArray();

            if (modules.Length == 0)
            {
                Debug.LogError($"NebulaPrefabBuilder: no destination modules in {DestinationFolder}.");
                return;
            }

            var built = new List<string>();
            var skipped = new List<string>();

            foreach (var module in modules)
            {
                var plate = FindPlate(module.Id);
                if (plate == null)
                {
                    // A prefab with no plate would instantiate, grow in and show nothing at all, which is far
                    // harder to diagnose on the headset than a missing destination.
                    skipped.Add(module.Id);
                    Debug.LogWarning($"NebulaPrefabBuilder: no texture for '{module.Id}' " +
                                     $"(expected {Expected(module.Id)}); skipped, its ContentPrefab is unchanged.");
                    continue;
                }

                var prefab = Build(module, plate, shader);
                if (prefab == null)
                {
                    skipped.Add(module.Id);
                    Debug.LogError($"NebulaPrefabBuilder: could not save the prefab for '{module.Id}'.");
                    continue;
                }

                Assign(module, prefab);
                built.Add($"{module.Id} <- {plate.name} ({plate.width}x{plate.height})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"NebulaPrefabBuilder: {built.Count} of {modules.Length} destinations built into {PrefabFolder}. " +
                      $"{CardCount} cards each, {CardWidth * 100f:F0} cm wide, {CardSpacing * 100f:F0} cm apart " +
                      $"({(CardCount - 1) * CardSpacing * 100f:F0} cm front to back), spin +/-{Spin} deg/s.\n" +
                      $"  built: {string.Join("\n         ", built)}\n" +
                      $"  skipped: {(skipped.Count == 0 ? "none" : string.Join(", ", skipped))}");
        }

        // ---------- one nebula

        private static GameObject Build(ExperienceModule module, Texture2D plate, Shader shader)
        {
            var root = new GameObject($"nebula_{module.Id}_prefab");
            var overlay = root.AddComponent<NebulaOverlay>();

            // Grabbable and scalable like a body (GDD 4.3). A sphere around the stack rather than a box: the
            // stack turns to face the player every frame, and a sphere is the only shape that does not change
            // what the hand can reach as it does.
            var grab = root.AddComponent<SphereCollider>();
            grab.radius = CardWidth * 0.5f;

            root.AddComponent<GEInteractable>();

            var manipulation = root.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            manipulationSo.FindProperty("manipulationType").enumValueIndex =
                (int)ManipulationHandler.HandMovementType.OneAndTwoHanded;
            // Move and scale, never rotate: the overlay billboards itself, so a rotation the player set would
            // be overwritten on the next frame and the grab would feel broken.
            manipulationSo.FindProperty("twoHandedManipulationType").enumValueIndex =
                (int)ManipulationHandler.TwoHandedManipulation.MoveScale;
            // Grabbed directly rather than pulled by a ForceSolver, so nothing else plays the grab and release
            // sounds for it (CS-106: until the router below existed, nothing grabbed it by hand at all).
            manipulationSo.FindProperty("playGrabSounds").boolValue = true;
            manipulationSo.ApplyModifiedPropertiesWithoutUndo();

            // Without this the handler above is unreachable: hand and ray selects are routed to
            // IGEPointerHandler, which ManipulationHandler deliberately does not implement, and there is no
            // ForceSolver here to forward them (CS-106).
            root.AddComponent<ManipulationPointerRouter>();

            // And without this one it could be moved but never brought back: Restore reaches bodies through
            // their ForceSolver or their LayoutRig, and an overlay has neither (CS-107). The anchor records the
            // pose OpenDestination spawns it in — the only code that knows where a destination belongs.
            root.AddComponent<FreePlacementAnchor>();

            var limitsSo = new SerializedObject(root.AddComponent<ScaleLimits>());
            limitsSo.FindProperty("kind").enumValueIndex = (int)ScaleLimits.Kind.Nebula; // 30 cm to 2 m
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            var cards = new Transform[CardCount];
            for (var i = 0; i < CardCount; i++)
            {
                cards[i] = BuildCard(root.transform, module.Id, i, plate, shader);
            }

            var overlaySo = new SerializedObject(overlay);
            var array = overlaySo.FindProperty("cards");
            array.arraySize = CardCount;
            for (var i = 0; i < CardCount; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            overlaySo.FindProperty("cardSpacing").floatValue = CardSpacing;
            overlaySo.FindProperty("spinDegreesPerSecond").floatValue = Spin;
            overlaySo.FindProperty("faceThePlayer").boolValue = true;
            overlaySo.FindProperty("requestCameraDepth").boolValue = true;
            overlaySo.ApplyModifiedPropertiesWithoutUndo();

            var path = $"{PrefabFolder}/nebula_{module.Id}_prefab.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// One plane. <paramref name="index"/> 0 is nearest the player: the faintest, widest band of the plate.
        /// The far card is the smallest and holds the core, so the stack converges away from the eye instead of
        /// being a uniform slab.
        /// </summary>
        private static Transform BuildCard(Transform parent, string id, int index, Texture2D plate, Shader shader)
        {
            var t = CardCount == 1 ? 0.5f : (float)index / (CardCount - 1);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"card_{index}";
            Object.DestroyImmediate(quad.GetComponent<Collider>()); // the root's sphere is the only grab shape
            quad.transform.SetParent(parent, false);

            var width = CardWidth * Mathf.Lerp(1.15f, 0.82f, t);
            quad.transform.localScale = new Vector3(width, width, 1f);
            quad.transform.localPosition = new Vector3(0f, 0f, (index - (CardCount - 1) * 0.5f) * CardSpacing);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CardMaterial(id, index, t, plate, shader);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            return quad.transform;
        }

        /// <summary>
        /// The material for one card. Written in place when it already exists, so a hand-tuned band on an
        /// existing nebula is the only thing a re-run costs, and no reference to the material breaks.
        /// </summary>
        private static Material CardMaterial(string id, int index, float t, Texture2D plate, Shader shader)
        {
            var path = $"{MaterialFolder}/nebula_{id}_card_{index}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = $"nebula_{id}_card_{index}" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.mainTexture = plate;

            // Front card: the faint outer wisps, half opacity, over a wide low band. Back card: the core, full
            // opacity, and a high edge above 1 so the brightest pixels are not clipped by the window's shoulder.
            material.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, t)));
            material.SetFloat("_BandLow", Mathf.Lerp(0.05f, 0.62f, t));
            material.SetFloat("_BandHigh", Mathf.Lerp(0.34f, 2f, t));
            material.SetFloat("_BandFeather", 0.12f);
            material.SetFloat("_TextureAlphaWeight", 0f); // the plates' alpha is meaningless; luminance is the source
            material.SetFloat("_RadialStart", 0.55f);
            material.SetFloat("_NearFadeStart", 0.12f);
            material.SetFloat("_NearFadeRange", 0.25f);
            material.SetFloat("_SoftDepth", 0.15f);

            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- plates and wiring

        private static string Expected(string id) =>
            Plates.TryGetValue(id, out var path) ? path : $"a texture named after '{id}' under {TextureFolder}";

        private static Texture2D FindPlate(string id)
        {
            if (Plates.TryGetValue(id, out var path))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (direct != null)
                {
                    return direct;
                }
            }

            // Fall back to a name search, so a plate re-exported with a different extension still lands.
            foreach (var guid in AssetDatabase.FindAssets($"{id} t:Texture2D", new[] { TextureFolder }))
            {
                var found = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void Assign(ExperienceModule module, GameObject prefab)
        {
            var so = new SerializedObject(module);
            so.FindProperty("ContentPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);
        }
    }
}
