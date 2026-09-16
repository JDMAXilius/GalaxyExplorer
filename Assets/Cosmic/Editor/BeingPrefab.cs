using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cosmic.Editor
{
    public static class BeingPrefab
    {
        public const string PrefabPath = "Assets/Cosmic/Prefabs/being.prefab";
        const string MeshPath = "Assets/Cosmic/Prefabs/being_points.asset";
        const string PointsMaterialPath = "Assets/Cosmic/Prefabs/materials/being_points.mat";
        const string RimMaterialPath = "Assets/Cosmic/Prefabs/materials/being_rim.mat";
        const string SettingsPath = "Assets/Cosmic/Data/Generated/being_settings.asset";
        const string GreetingPath = "Assets/audio/being/being_greeting_audio_clip.wav";
        const string LibraryPath = "Assets/Cosmic/Data/Generated/audio_library.asset";
        const float DiameterMetres = 0.14f;
        const float RimRatio = 1.06f;

        [MenuItem("Cosmic/Build/Being")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Cosmic being: refused in play mode."); return; }
            Content.Missing.Clear();
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var mesh = Points(sphere);
            var pointsMaterial = Material(PointsMaterialPath, "Cosmic/BeingPoints", m =>
            {
                m.SetColor("_BaseColor", new Color(0f, 0.69f, 1f));
                m.SetColor("_VariantColor", new Color(0.071f, 0.422f, 1f));
                m.SetColor("_TouchColor", new Color(0.029f, 0f, 1f));
                m.SetColor("_ActiveColor", Color.white);
                m.SetFloat("_Size", 0.07f);
                m.SetFloat("_Speed", 1f);
                m.SetFloat("_BandExtent", 0.5f);
            });
            var rimMaterial = Material(RimMaterialPath, "Cosmic/BeingRim", m =>
            {
                m.SetColor("_Color", new Color(0.431f, 0.694f, 0.962f));
                m.SetFloat("_Multiplier", 0.15f);
            });
            var settings = Settings();
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null) Content.Miss(LibraryPath);
            Content.Write("being", PrefabPath, root =>
            {
                root.AddComponent<SphereCollider>().radius = DiameterMetres * 0.5f;
                var grab = root.AddComponent<Grabbable>();
                Content.Set(grab, "limits", Grabbable.Limits.Fixed);
                Content.SetArray(grab, "m_Colliders", new List<Object> { root.GetComponent<SphereCollider>() });
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.outputAudioMixerGroup = library != null ? library.voice : null;
                root.AddComponent<BeingLink>();
                root.AddComponent<BeingMic>();
                root.AddComponent<BeingVoice>();
                var points = Child(root.transform, "points", mesh, pointsMaterial, DiameterMetres);
                var rim = Child(root.transform, "rim", sphere, rimMaterial, DiameterMetres * RimRatio);
                var look = root.AddComponent<BeingLook>();
                Content.Set(look, "points", points);
                Content.Set(look, "rim", rim);
                var follow = root.AddComponent<BeingFollow>();
                Content.Set(follow, "settings", settings);
                Content.Set(follow, "grab", grab);
                var being = root.AddComponent<Being>();
                Content.Set(being, "settings", settings);
                Content.Set(being, "look", look);
                Content.Set(being, "grab", grab);
                return true;
            }, out _);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cosmic being -> {PrefabPath}; " + (Content.Missing.Count > 0 ? $"missing: {string.Join(", ", Content.Missing)}" : "nothing missing"));
        }

        static BeingSettings Settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BeingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BeingSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.greeting = AssetDatabase.LoadAssetAtPath<AudioClip>(GreetingPath);
            if (settings.greeting == null) Content.Miss(GreetingPath);
            EditorUtility.SetDirty(settings);
            return settings;
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
            // The shader pushes points out with the voice and the touch, so the bounds allow for it or the being culls at the screen edge.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static float Next(System.Random random) => random.Next() / (float)int.MaxValue;

        static Material Material(string path, string shaderName, System.Action<Material> tune)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) Content.Miss(shaderName);
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
    }
}
