using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Cosmic.Companion.Editor
{
    public static class BeingPrefab
    {
        public const string PrefabPath = "Assets/Being/being.prefab";
        const string Dir = "Assets/Being/Data/";
        const string MeshPath = Dir + "being_points.asset";
        const string PointsMaterialPath = Dir + "being_points.mat";
        const string RimMaterialPath = Dir + "being_rim.mat";
        const string SettingsPath = Dir + "being_settings.asset";
        const string KeysPath = Dir + "being_keys.asset";
        const string PromptPath = Dir + "being_prompt.txt";
        const string KnowledgePath = Dir + "being_knowledge.txt";
        const string GreetingPath = "Assets/audio/being/being_greeting_audio_clip.wav";
        const string ChimePath = "Assets/Being/Audio/being_listen.wav";
        const string TickPath = "Assets/Being/Audio/being_close.wav";
        const string ScatterModel = "Assets/models/intro_models/intro_placement_models/placement_object_model.fbx";
        const string ScatterMesh = "placement_object_scatter_mesh";
        const string DockPath = "Assets/prefabs/ui/dock_prefab.prefab";
        const float DiameterMetres = 0.2f;
        const float RimRatio = 1.06f;

        [MenuItem("Cosmic Simulation/Build Being")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Being: refused in play mode."); return; }
            Knowledge.Build();
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var source = Scatter() ?? sphere;
            var mesh = Points(source);
            var pointsMaterial = Material(PointsMaterialPath, "Companion/Points", m =>
            {
                m.SetColor("_BaseColor", new Color(0f, 0.69f, 1f));
                m.SetColor("_VariantColor", new Color(0.071f, 0.422f, 1f));
                m.SetColor("_TouchColor", new Color(0.029f, 0f, 1f));
                m.SetColor("_ActiveColor", Color.white);
                m.SetFloat("_Size", 0.07f);
                m.SetFloat("_Speed", 1f);
                m.SetFloat("_BandExtent", source.bounds.extents.y);
            });
            var rimMaterial = Material(RimMaterialPath, "Companion/Rim", m =>
            {
                m.SetColor("_Color", new Color(0.431f, 0.694f, 0.962f));
                m.SetFloat("_Multiplier", 0.15f);
            });
            Cue(ChimePath, new[] { 659.25f, 880f }, 0.09f);
            Cue(TickPath, new[] { 1174.66f }, 0.045f);
            var settings = Settings();
            var keys = Asset<BeingKeys>(KeysPath);

            var root = new GameObject("being");
            root.AddComponent<SphereCollider>().radius = DiameterMetres * 0.5f;
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = settings.spatialBlend;
            audio.minDistance = settings.fullVolumeMetres;
            root.AddComponent<Voice>();
            var points = Child(root.transform, "points", mesh, pointsMaterial, DiameterMetres / source.bounds.size.x);
            var rim = Child(root.transform, "rim", sphere, rimMaterial, DiameterMetres * RimRatio / sphere.bounds.size.x);
            var look = root.AddComponent<Look>();
            Set(look, "points", points, "rim", rim);
            var follow = root.AddComponent<Follow>();
            Set(follow, "settings", settings);
            var being = root.AddComponent<Being>();
            Set(being, "settings", settings, "keys", keys, "look", look);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Wire(DockPath, prefab.GetComponent<Being>());
            AssetDatabase.SaveAssets();
            Debug.Log($"Being -> {PrefabPath} ({source.vertexCount} points, {(source == sphere ? "sphere fallback" : "intro scatter mesh")})");
        }

        static BeingSettings Settings()
        {
            var settings = Asset<BeingSettings>(SettingsPath);
            settings.greeting = AssetDatabase.LoadAssetAtPath<AudioClip>(GreetingPath);
            settings.listenChime = AssetDatabase.LoadAssetAtPath<AudioClip>(ChimePath);
            settings.closeTick = AssetDatabase.LoadAssetAtPath<AudioClip>(TickPath);
            settings.prompt = AssetDatabase.LoadAssetAtPath<TextAsset>(PromptPath);
            settings.knowledge = AssetDatabase.LoadAssetAtPath<TextAsset>(KnowledgePath);
            if (settings.greeting == null) Debug.LogWarning("Being: no greeting clip at " + GreetingPath);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Mesh Scatter()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ScatterModel))
                if (asset is Mesh mesh && mesh.name == ScatterMesh) return mesh;
            return null;
        }

        static Mesh Points(Mesh source)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "being_points" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            var random = new System.Random(0);
            var vertices = source.vertices;
            var positions = new Vector3[vertices.Length * 4];
            var randoms = new List<Vector4>(positions.Length);
            var corners = new List<Vector2>(positions.Length);
            var indices = new int[vertices.Length * 6];
            for (var i = 0; i < vertices.Length; i++)
            {
                var r = new Vector4(Next(random), Next(random), Next(random), Next(random));
                for (var corner = 0; corner < 4; corner++)
                {
                    positions[i * 4 + corner] = vertices[i];
                    randoms.Add(r);
                    corners.Add(new Vector2(corner, 0f));
                }
                var q = i * 4;
                indices[i * 6] = q; indices[i * 6 + 1] = q + 1; indices[i * 6 + 2] = q + 2;
                indices[i * 6 + 3] = q + 2; indices[i * 6 + 4] = q + 1; indices[i * 6 + 5] = q + 3;
            }
            mesh.Clear();
            mesh.indexFormat = positions.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.SetUVs(0, randoms);
            mesh.SetUVs(1, corners);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            // The shader moves points with the voice and the touch, so the bounds allow for it or the being culls at the screen edge.
            mesh.bounds = new Bounds(Vector3.zero, source.bounds.size * 2f);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static float Next(System.Random random) => random.Next() / (float)int.MaxValue;

        static Material Material(string path, string shaderName, Action<Material> tune)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) Debug.LogError("Being: shader missing " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null) material.shader = shader;
            tune(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Renderer Child(Transform parent, string name, Mesh mesh, Material material, float scale)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localScale = Vector3.one * scale;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        static void Set(Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (var i = 0; i < pairs.Length; i += 2) so.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Wire(string prefabPath, Being being)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var component in contents.GetComponents<MonoBehaviour>())
            {
                var so = new SerializedObject(component);
                var property = so.FindProperty("beingPrefab");
                if (property == null) continue;
                property.objectReferenceValue = being;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // A short soft tone, written once: the listening chime and the closing tick are made here, not sourced.
        static void Cue(string path, float[] hz, float noteSeconds)
        {
            if (File.Exists(path)) return;
            const int rate = 24000;
            var note = (int)(noteSeconds * rate);
            var pcm = new short[note * hz.Length];
            for (var n = 0; n < hz.Length; n++)
                for (var i = 0; i < note; i++)
                {
                    var t = i / (float)rate;
                    var env = Mathf.Min(1f, i / (rate * 0.006f)) * (1f - i / (float)note);
                    var s = Mathf.Sin(2f * Mathf.PI * hz[n] * t) * 0.6f + Mathf.Sin(4f * Mathf.PI * hz[n] * t) * 0.15f;
                    pcm[n * note + i] = (short)(Mathf.Clamp(s * env * 0.5f, -1f, 1f) * short.MaxValue);
                }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + pcm.Length * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(pcm.Length * 2);
                foreach (var s in pcm) w.Write(s);
            }
            AssetDatabase.ImportAsset(path);
        }
    }
}
