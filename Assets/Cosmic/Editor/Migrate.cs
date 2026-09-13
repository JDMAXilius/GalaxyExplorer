using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cosmic.Editor
{
    // Transitional, and deliberately its own file: the copy deck cannot own an asset reference, so the
    // narration clips, ambience beds and dock thumbnails the old ScriptableObjects hold have to be carried
    // across once, by id, into the generated Place and Body assets. It reads Assets/data, which the cutover
    // deletes (CS-164), so this runs before that and then goes with it - CS-166 removes this file.
    // Cosmic/Import Copy never clears a field it does not own, so a re-import leaves what this wrote.
    public static class Migrate
    {
        const string Generated = "Assets/Cosmic/Data/Generated";
        static readonly string[] OldPlaces = { "Assets/data/experiences", "Assets/data/destinations" };
        static readonly string[] OldBodies = { "Assets/data/bodies", "Assets/data/moons" };

        [MenuItem("Cosmic/Build/Wire Old References")]
        public static void WireOldReferences()
        {
            var places = Load<Place>($"{Generated}/places");
            var bodies = Load<Body>($"{Generated}/bodies");
            var wired = 0;
            var missing = new List<string>();

            foreach (var old in OldAssets(OldPlaces))
            {
                var source = new SerializedObject(old.Value);
                var id = source.FindProperty("Id")?.stringValue;
                if (string.IsNullOrEmpty(id)) continue;
                if (!places.TryGetValue(id, out var place)) { missing.Add($"place {id}"); continue; }
                var target = new SerializedObject(place);
                wired += Copy(source, target, "Narration", "narration");
                wired += Copy(source, target, "Ambience", "ambience");
                wired += Copy(source, target, "DockThumbnail", "thumbnail");
                target.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(place);
            }

            foreach (var old in OldAssets(OldBodies))
            {
                var source = new SerializedObject(old.Value);
                var id = source.FindProperty("Id")?.stringValue;
                if (string.IsNullOrEmpty(id)) continue;
                if (!bodies.TryGetValue(id, out var body)) { missing.Add($"body {id}"); continue; }
                var target = new SerializedObject(body);
                wired += Copy(source, target, "Narration", "narration");
                wired += Copy(source, target, "Ambience", "ambience");
                target.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(body);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Cosmic old references -> {wired} wired across {places.Count} place(s) and {bodies.Count} bodie(s)"
                      + (missing.Count > 0 ? $"; no generated asset for: {string.Join(", ", missing)}" : "; every old id had a generated asset"));
        }

        // A thumbnail is a Sprite on Place and a Texture2D reference in the old asset when the importer made
        // no sprite, so the sub-asset is looked up by path rather than assigned across types.
        static int Copy(SerializedObject source, SerializedObject target, string oldField, string newField)
        {
            var from = source.FindProperty(oldField);
            var to = target.FindProperty(newField);
            if (from == null || to == null || from.objectReferenceValue == null) return 0;
            var value = from.objectReferenceValue;
            if (newField == "thumbnail" && !(value is Sprite))
            {
                var path = AssetDatabase.GetAssetPath(value);
                value = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (value == null) return 0;
            }
            if (to.objectReferenceValue == value) return 0;
            to.objectReferenceValue = value;
            return 1;
        }

        static Dictionary<string, T> Load<T>(string folder) where T : ScriptableObject
        {
            var map = new Dictionary<string, T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var id = new SerializedObject(asset).FindProperty("id")?.stringValue;
                if (!string.IsNullOrEmpty(id)) map[id] = asset;
            }
            return map;
        }

        static IEnumerable<KeyValuePair<string, ScriptableObject>> OldAssets(string[] folders)
        {
            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null) yield return new KeyValuePair<string, ScriptableObject>(path, asset);
                }
            }
        }
    }
}
