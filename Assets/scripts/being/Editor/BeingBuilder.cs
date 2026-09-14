using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CosmicSimulation.Being.EditorTools
{
    public static class BeingBuilder
    {
        private const string PrefabPath = "Assets/prefabs/being/cosmic_being_prefab.prefab";
        private const string SettingsPath = "Assets/data/being/cosmic_being_settings.asset";
        private const string HologramPath = "Assets/materials/being/being_hologram.mat";
        private const string RimPath = "Assets/materials/being/being_rim.mat";
        private const string IntroHologram = "Assets/materials/intro_materials/intro_placement_object_material.mat";
        private const string IntroRim = "Assets/materials/intro_materials/intro_placement_object_rim_material.mat";
        private const string VoiceMixer = "Assets/audio/audio_mixers/vo_audio_mixer.mixer";
        private const string DockPath = "Assets/prefabs/ui/dock_prefab.prefab";
        private const string DesktopDockPath = "Assets/prefabs/ui/desktop_dock_prefab.prefab";
        private const float Diameter = 0.14f;

        [MenuItem("Cosmic Simulation/Build Cosmic Being")]
        public static void Build()
        {
            Folder("Assets/prefabs/being");
            Folder("Assets/materials/being");
            Folder("Assets/data/being");

            var settings = Asset<BeingSettings>(SettingsPath);
            var hologram = Material(HologramPath, IntroHologram, m => m.SetFloat("_Size", 0.03f));
            var rimMaterial = Material(RimPath, IntroRim, m => m.SetFloat("_Multiplier", 0.15f));
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            var root = new GameObject("cosmic_being_prefab");
            root.AddComponent<SphereCollider>().radius = Diameter * 0.5f;
            root.AddComponent<GEInteractable>();
            var anchor = root.AddComponent<BeingAnchor>();
            root.AddComponent<BeingLink>();
            root.AddComponent<BeingMic>();
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            var groups = AssetDatabase.LoadAssetAtPath<AudioMixer>(VoiceMixer)?.FindMatchingGroups("Master");
            source.outputAudioMixerGroup = groups != null && groups.Length > 0 ? groups[0] : null;
            root.AddComponent<BeingSpeaker>();

            var points = Child(root.transform, "hologram", sphere, hologram, Diameter);
            var placement = points.AddComponent<PlacementObject>();
            placement.SourceMesh = sphere;
            placement.Speed = 2f;

            var rim = Child(root.transform, "rim", sphere, rimMaterial, Diameter * 1.06f);
            var visual = root.AddComponent<BeingVisual>();
            Set(visual, "hologram", placement, "rim", rim.GetComponent<Renderer>());

            var being = root.AddComponent<CosmicBeing>();
            Set(being, "settings", settings, "visual", visual);
            Set(anchor, "settings", settings);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Wire(DockPath, prefab);
            Wire(DesktopDockPath, prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"BeingBuilder: wrote {PrefabPath}.");
        }

        private static GameObject Child(Transform parent, string name, Mesh mesh, Material material, float scale)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localScale = Vector3.one * scale;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }

        private static Material Material(string path, string sourcePath, System.Action<Material> tune)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(sourcePath));
            tune(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void Wire(string prefabPath, GameObject beingPrefab)
        {
            var dock = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (dock == null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var component in contents.GetComponents<MonoBehaviour>())
            {
                var so = new SerializedObject(component);
                var property = so.FindProperty("beingPrefab");
                if (property != null)
                {
                    property.objectReferenceValue = beingPrefab.GetComponent<CosmicBeing>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void Set(Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (var i = 0; i < pairs.Length; i += 2)
            {
                so.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Folder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
        }
    }
}
