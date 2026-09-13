using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cosmic.Editor
{
    public static class Content
    {
        internal const string Generated = "Assets/Cosmic/Data/Generated";
        internal const string MaterialFolder = "Assets/Cosmic/Prefabs/materials";
        internal const string BodyFolder = "Assets/Cosmic/Prefabs/bodies";
        internal const string PlaceFolder = "Assets/Cosmic/Prefabs/places";
        internal const string Textures = "Assets/Textures/";

        const string FieldShader = "Assets/Cosmic/Shaders/Field.shader";
        const string HoleShader = "Assets/Cosmic/Shaders/BlackHole.shader";
        const string GlowShader = "Assets/Cosmic/Shaders/Glow.shader";
        const string HubblePlate = "Assets/Textures/galaxies/deep_field_hubble_udf_texture.jpg";
        const string WebbPlate = "Assets/Textures/galaxies/deep_field_webb_smacs0723_texture.jpg";

        const float RadiansPerSecondPerDegreeMinute = Mathf.Deg2Rad / 60f;
        const float GalaxySizeFactor = 0.4f;
        const float NebulaRadiusMetres = 0.35f;
        const float NebulaWidthMetres = 0.7f;
        const float NebulaPointScale = 0.012f;
        const float NebulaDegreesPerMinute = 0.4f;
        const float WebRadiusMetres = 0.9f;
        const float WebWidthMetres = 5f;
        const float WebPointScale = 0.0032f;
        const float WebDegreesPerMinute = 0.5f;
        const float WebMinMetres = 2.5f;
        const float WebMaxMetres = 10f;
        const float HoleRadiusMetres = 0.35f;
        const float HoleFlattening = 0.85f;
        const float HoleGlowScale = 1.347129f;

        internal static readonly List<string> Missing = new List<string>();

        [MenuItem("Cosmic/Build/Bodies")]
        public static void BuildBodies()
        {
            if (Bake.Playing()) return;
            Missing.Clear();
            Folders(BodyFolder, MaterialFolder);
            int built = 0, triangles = 0, worst = 0;
            var worstId = string.Empty;
            try
            {
                foreach (var body in Load<Body>(Generated + "/bodies"))
                {
                    var path = $"{BodyFolder}/{body.id}.prefab";
                    Write(body.id, path, root =>
                    {
                        Bodies.Assemble(root, body);
                        return true;
                    }, out var tris);
                    built++;
                    triangles += tris;
                    if (tris > worst) { worst = tris; worstId = body.id; }
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Cosmic bodies: {built} prefabs -> {BodyFolder}, {triangles} triangles in total, " +
                      $"heaviest {worstId} at {worst}. " + Report());
        }

        [MenuItem("Cosmic/Build/Places")]
        public static void BuildPlaces()
        {
            if (Bake.Playing()) return;
            Missing.Clear();
            Folders(PlaceFolder, MaterialFolder);
            int built = 0, skipped = 0, triangles = 0;
            try
            {
                foreach (var place in Load<Place>(Generated + "/places"))
                {
                    var path = $"{PlaceFolder}/{place.id}.prefab";
                    if (!Write(place.id, path, root => Fill(root, place), out var tris))
                    {
                        skipped++;
                        continue;
                    }

                    place.content = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    EditorUtility.SetDirty(place);
                    built++;
                    triangles += tris;
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Cosmic places: {built} prefabs -> {PlaceFolder}, {skipped} skipped, {triangles} " +
                      "mesh triangles across them all (point clouds and sprites are procedural). " + Report());
        }

        static bool Fill(GameObject root, Place place)
        {
            switch (place.id)
            {
                case "cosmic_web": return Web(root, place);
                case "galaxies": return DeepField(root);
                case "sagittarius_a":
                case "galactic_center": return Hole(root);
            }

            if (place.galaxy != null) return Spiral(root, place);
            if (place.HasVolume) return Nebula(root, place);
            if (Bodies.Listed(place).Count > 0) return Planets(root, place);
            Debug.Log($"Cosmic places: {place.id} has no galaxy, volume or bodies, so no content prefab was written.");
            return false;
        }

        static bool Spiral(GameObject root, Place place)
        {
            var galaxy = place.galaxy;
            var layers = new List<Points.Layer>();
            var envelope = 0f;
            var count = 0;
            foreach (var spec in galaxy.layers)
            {
                var material = Find($"points_{galaxy.id}_{spec.id}");
                if (spec.points == null || material == null)
                {
                    Debug.Log($"Cosmic places: {place.id} is missing its {spec.id} cloud or material, so it was " +
                              "skipped. Run Cosmic/Build/Bake All first.");
                    return false;
                }

                envelope = Mathf.Max(envelope, spec.points.radiusMetres);
                count += spec.points.Count;
                layers.Add(new Points.Layer
                {
                    cloud = spec.points, material = material, drawIndex = spec.drawIndex, afterOpaque = false,
                    worldSpaceScale = spec.worldSpaceScale, tint = spec.tint, tintMultiplier = spec.tintMultiplier,
                    ellipseRadii = galaxy.RadiiOf(spec),
                });
            }

            Grab(root, galaxy.widthMetres * 0.5f * galaxy.grabRadiusFraction, Grabbable.Limits.Model,
                 galaxy.minScaleMetres, galaxy.maxScaleMetres, galaxy.widthMetres);
            var node = Child(root.transform, "galaxy");
            node.localRotation = Quaternion.Euler(galaxy.discTiltDegrees, galaxy.discYawDegrees, 0f);
            var scale = envelope > 0f ? galaxy.widthMetres / (envelope * 2f) : 1f;
            node.localScale = Vector3.one * scale;
            var points = node.gameObject.AddComponent<Points>();
            points.layers = layers.ToArray();
            points.sizeFactor = GalaxySizeFactor;
            points.velocityRadiansPerSecond = galaxy.velocityMultiplier;
            points.EllipseRadii = new Vector2(galaxy.xRadii, galaxy.zRadii);
            Debug.Log($"Cosmic places: {place.id} galaxy {galaxy.widthMetres:F2} m wide, envelope {envelope:F3}, " +
                      $"node scale {scale:F3}, {layers.Count} layers, {count} points, grab sphere " +
                      $"{galaxy.widthMetres * 0.5f * galaxy.grabRadiusFraction:F2} m.");
            return true;
        }

        static bool Nebula(GameObject root, Place place)
        {
            var material = Find($"points_nebula_{place.id}");
            if (material == null)
            {
                Debug.Log($"Cosmic places: {place.id} has no points_nebula_{place.id} material, so it was skipped. " +
                          "Run Cosmic/Build/Nebulae first.");
                return false;
            }

            Grab(root, NebulaRadiusMetres, Grabbable.Limits.Nebula, 0f, 0f, NebulaWidthMetres);
            Volume(root, "volume", place.volume.points, material, NebulaPointScale, NebulaDegreesPerMinute);
            Debug.Log($"Cosmic places: {place.id} nebula {place.volume.points.Count} points, radius " +
                      $"{place.volume.radiusMetres:F2} m, {NebulaWidthMetres * 100f:F0} cm across at unit scale.");
            return true;
        }

        static bool Web(GameObject root, Place place)
        {
            var material = Find("points_cosmic_web");
            if (place.volume.points == null || material == null)
            {
                Debug.Log("Cosmic places: cosmic_web has no cloud or material, so it was skipped. Run " +
                          "Cosmic/Build/Cosmic Web first.");
                return false;
            }

            Grab(root, WebRadiusMetres, Grabbable.Limits.Model, WebMinMetres, WebMaxMetres, WebWidthMetres);
            Volume(root, "web", place.volume.points, material, WebPointScale, WebDegreesPerMinute);
            Debug.Log($"Cosmic places: cosmic_web {place.volume.points.Count} points, grab sphere " +
                      $"{WebRadiusMetres:F2} m, {WebWidthMetres:F1} m across at unit scale.");
            return true;
        }

        static void Volume(GameObject root, string name, PointCloud cloud, Material material, float pointScale,
                           float degreesPerMinute)
        {
            var points = Child(root.transform, name).gameObject.AddComponent<Points>();
            points.layers = new[]
            {
                new Points.Layer
                {
                    cloud = cloud, material = material, drawIndex = 0, afterOpaque = true,
                    worldSpaceScale = pointScale, tint = Color.white, tintMultiplier = 1f,
                },
            };
            points.sizeFactor = 1f;
            points.velocityRadiansPerSecond = degreesPerMinute * RadiansPerSecondPerDegreeMinute;
        }

        static bool DeepField(GameObject root)
        {
            var hubble = Tex(HubblePlate);
            var webb = Tex(WebbPlate);
            var hubbleMaterial = Plate("field_hubble", hubble);
            var webbMaterial = Plate("field_webb", webb);
            if (hubble == null || webb == null || hubbleMaterial == null || webbMaterial == null)
            {
                Debug.Log($"Cosmic places: galaxies needs both deep-field plates and {FieldShader}, so it was " +
                          "skipped.");
                return false;
            }

            var field = Child(root.transform, "field").gameObject.AddComponent<Field>();
            field.plates = new[] { hubble, webb };
            field.plateMaterials = new[] { hubbleMaterial, webbMaterial };
            var cutouts = new List<Field.Cutout>();
            cutouts.AddRange(Field.Scan(hubble, 0, 8, 8));
            cutouts.AddRange(Field.Scan(webb, 1, 6, 6));
            field.cutouts = cutouts.ToArray();
            Debug.Log($"Cosmic places: galaxies {field.spriteCount} sprites from {cutouts.Count} cutouts across 2 " +
                      $"plates, shell {field.shellInnerMetres:F1}-{field.shellOuterMetres:F1} m.");
            return true;
        }

        static bool Hole(GameObject root)
        {
            var material = Mat("black_hole", HoleShader);
            var glowMaterial = Mat("black_hole_glow", GlowShader);
            if (material == null || glowMaterial == null)
            {
                Debug.Log($"Cosmic places: the galactic centre needs {HoleShader} and {GlowShader}, so it was " +
                          "skipped.");
                return false;
            }

            Bodies.Hole(material, glowMaterial);
            Grab(root, HoleRadiusMetres, Grabbable.Limits.Nebula, 0f, 0f, NebulaWidthMetres);
            var node = Child(root.transform, "black_hole").gameObject.AddComponent<BlackHole>();
            var width = HoleRadiusMetres * 2f;
            var disc = Draw(node.transform, "disc", Builtin("Sphere.fbx"), material,
                            new Vector3(width, width * HoleFlattening, width));
            var glow = Draw(node.transform, "glow", Mesh("Assets/models/sagittarius_a_black_hole_glow_card_model.fbx",
                                                         null), glowMaterial,
                            new Vector3(width * HoleGlowScale, width * HoleGlowScale, -width * HoleGlowScale));
            Set(node, "disc", disc);
            Set(node, "glow", glow);
            Debug.Log($"Cosmic places: galactic centre disc {width:F2} m across, flattened to {HoleFlattening:F2}, " +
                      "no s2/s102 Body assets so no stars orbit it yet.");
            return true;
        }

        static bool Planets(GameObject root, Place place)
        {
            var bodies = Bodies.Listed(place);
            var rig = root.AddComponent<Rig>();
            var orbit = root.AddComponent<Orbit>();
            Set(orbit, "ringMaterial", Bodies.Ring());
            Set(rig, "orbit", orbit);
            rig.Bind(bodies);
            var layout = place.layouts != null && place.layouts.Length > 0 ? place.layouts[0] : null;
            int nested = 0, moons = 0;
            float radius = 0f, height = 0f, gap = float.MaxValue;
            var placed = new List<Vector3>();
            foreach (var body in bodies)
            {
                var anchor = rig.Anchor(body);
                if (layout != null && layout.TryFind(body, out var slot))
                {
                    anchor.localPosition = slot.localPosition;
                    anchor.localRotation = Quaternion.Euler(slot.localEuler);
                    anchor.localScale = Vector3.one * Mathf.Max(0.0001f, slot.scale);
                    radius = Mathf.Max(radius, new Vector2(slot.localPosition.x, slot.localPosition.z).magnitude);
                    height = Mathf.Max(height, slot.localPosition.y);
                    foreach (var other in placed) gap = Mathf.Min(gap, Vector3.Distance(other, slot.localPosition));
                    placed.Add(slot.localPosition);
                }

                nested += Nest(body, anchor, $"body_{body.id}", 1f) ? 1 : 0;
                moons += Bodies.Moons(body, rig, anchor);
            }

            Debug.Log($"Cosmic places: {place.id} {nested} bodies and {moons} moons on {bodies.Count} anchors, " +
                      (layout == null
                          ? "no layout to park them at, so Orbit places them at run time."
                          : $"parked at '{layout.id}': arc radius {radius:F2} m, height {height:F2} m, closest " +
                            $"centres {(placed.Count > 1 ? gap * 100f : 0f):F1} cm."));
            return nested > 0;
        }

        internal static bool Nest(Body body, Transform parent, string name, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{BodyFolder}/{body.id}.prefab");
            if (asset == null)
            {
                Miss($"{BodyFolder}/{body.id}.prefab");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;
            return true;
        }

        static void Grab(GameObject root, float radiusMetres, Grabbable.Limits limits, float minMetres,
                         float maxMetres, float widthMetres)
        {
            root.AddComponent<SphereCollider>().radius = radiusMetres;
            var grab = root.AddComponent<Grabbable>();
            Set(grab, "limits", limits);
            Set(grab, "customMinMetres", minMetres);
            Set(grab, "customMaxMetres", maxMetres);
            Set(grab, "widthAtUnitScaleMetres", widthMetres);
            Set(grab, "keepUpright", false);
            Set(grab, "autoReturn", false);
            var rigidbody = root.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        static Material Plate(string name, Texture2D plate)
        {
            var material = Mat(name, FieldShader);
            if (material == null || plate == null) return null;
            T(material, "_MainTex", plate);
            F(material, "_Exposure", 1.15f);
            F(material, "_Floor", 0f);
            F(material, "_RadialStart", 0.35f);
            F(material, "_NearFadeStart", 0.25f);
            F(material, "_NearFadeRange", 0.45f);
            return material;
        }

        internal static Material Find(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");

        internal static Material Mat(string name, string shaderPath, string keyword = null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                Miss(shaderPath);
                return null;
            }

            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.shaderKeywords = keyword == null ? Array.Empty<string>() : new[] { keyword };
            material.enableInstancing = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static void F(Material material, string name, float value)
        {
            if (Has(material, name)) material.SetFloat(name, value);
        }

        internal static void C(Material material, string name, Color value)
        {
            if (Has(material, name)) material.SetColor(name, value);
        }

        internal static void V(Material material, string name, Vector4 value)
        {
            if (Has(material, name)) material.SetVector(name, value);
        }

        internal static void T(Material material, string name, Texture value)
        {
            if (Has(material, name)) material.SetTexture(name, value);
        }

        static bool Has(Material material, string name)
        {
            if (material == null) return false;
            if (material.HasProperty(name)) return true;
            Miss($"{material.shader.name}.{name}");
            return false;
        }

        internal static Texture2D Tex(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) Miss(path);
            return texture;
        }

        internal static Mesh Mesh(string path, string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Mesh mesh && (name == null || mesh.name == name)) return mesh;
            Miss(name == null ? path : $"{path}:{name}");
            return null;
        }

        internal static Mesh Builtin(string name)
        {
            var mesh = Resources.GetBuiltinResource<Mesh>(name);
            if (mesh == null) Miss(name);
            return mesh;
        }

        internal static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static Renderer Draw(Transform parent, string name, Mesh mesh, Material material, Vector3 scale)
        {
            var node = Child(parent, name);
            node.localScale = scale;
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return renderer;
        }

        internal static void Set(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Miss($"{target.GetType().Name}.{field}");
                return;
            }

            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case bool flag: property.boolValue = flag; break;
                case float number: property.floatValue = number; break;
                case int number: property.intValue = number; break;
                case Enum option: property.enumValueIndex = Convert.ToInt32(option); break;
                case Vector3 vector: property.vector3Value = vector; break;
                case Object reference: property.objectReferenceValue = reference; break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string field, IList<Object> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Miss($"{target.GetType().Name}.{field}");
                return;
            }

            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Every prefab is assembled in a preview scene and saved to a fixed path, so re-running keeps the GUID and every scene reference with it.
        internal static bool Write(string name, string path, Func<GameObject, bool> fill, out int triangles)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            triangles = 0;
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, preview);
                var made = fill(root);
                if (made)
                {
                    triangles = Triangles(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                Object.DestroyImmediate(root);
                return made;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        internal static int Triangles(Transform root)
        {
            var total = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                for (var i = 0; mesh != null && i < mesh.subMeshCount; i++) total += (int)(mesh.GetIndexCount(i) / 3);
            }

            return total;
        }

        internal static List<T> Load<T>(string folder) where T : ScriptableObject
        {
            var found = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) found.Add(asset);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return found;
        }

        internal static void Miss(string what)
        {
            if (!Missing.Contains(what)) Missing.Add(what);
        }

        static string Report() =>
            Missing.Count == 0 ? "Nothing missing." : $"Missing: {string.Join(", ", Missing)}.";

        static void Folders(params string[] folders)
        {
            var root = Directory.GetCurrentDirectory();
            foreach (var folder in folders) Directory.CreateDirectory(Path.Combine(root, folder));
            AssetDatabase.Refresh();
        }
    }
}
