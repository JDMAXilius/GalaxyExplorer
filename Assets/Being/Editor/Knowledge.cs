using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cosmic.Companion.Editor
{
    public static class Knowledge
    {
        public const string Path = "Assets/Being/Data/being_knowledge.txt";
        static readonly string[] PlaceDirs = { "Assets/data/experiences", "Assets/data/destinations" };
        static readonly string[] BodyDirs = { "Assets/data/bodies", "Assets/data/moons" };

        [MenuItem("Cosmic Simulation/Build Being Knowledge")]
        public static void Build()
        {
            var sb = new StringBuilder("# Places in the simulation\n\n");
            var places = 0;
            foreach (var so in Assets(PlaceDirs))
            {
                var id = Text(so, "Id");
                if (id.Length == 0) continue;
                sb.Append("## ").Append(Text(so, "DisplayName")).Append(" (id: ").Append(id).Append(")\n");
                var second = Text(so, "SecondLine");
                if (second.Length > 0) sb.Append(second).Append('\n');
                foreach (var p in Strings(so, "Panel.Paragraphs")) sb.Append(p).Append('\n');
                sb.Append('\n');
                places++;
            }
            sb.Append("# Bodies\n\n");
            var bodies = 0;
            foreach (var so in Assets(BodyDirs))
            {
                var id = Text(so, "Id");
                if (id.Length == 0) continue;
                sb.Append("## ").Append(Text(so, "DisplayName")).Append(" (id: ").Append(id).Append(")\n");
                var subtitle = Text(so, "Subtitle");
                if (subtitle.Length > 0) sb.Append(subtitle).Append('\n');
                sb.Append(Text(so, "Paragraph")).Append('\n');
                var stats = new List<string>();
                var array = so.FindProperty("Stats");
                for (var i = 0; array != null && i < array.arraySize; i++)
                {
                    var stat = array.GetArrayElementAtIndex(i);
                    var exponent = stat.FindPropertyRelative("Exponent")?.intValue ?? 0;
                    var unit = stat.FindPropertyRelative("Unit")?.stringValue ?? "";
                    stats.Add($"{stat.FindPropertyRelative("Label")?.stringValue}: {stat.FindPropertyRelative("Value")?.stringValue}{(exponent == 0 ? "" : "^" + exponent)} {unit}".Trim());
                }
                if (stats.Count > 0) sb.Append(string.Join("; ", stats)).Append('\n');
                sb.Append('\n');
                bodies++;
            }
            File.WriteAllText(Path, sb.ToString().TrimEnd() + "\n");
            AssetDatabase.ImportAsset(Path);
            Debug.Log($"Being knowledge -> {Path}: {places} places, {bodies} bodies, {sb.Length} characters");
        }

        static IEnumerable<SerializedObject> Assets(string[] dirs)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", dirs))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return new SerializedObject(asset);
            }
        }

        static string Text(SerializedObject so, string field) => so.FindProperty(field)?.stringValue?.Trim() ?? "";

        static IEnumerable<string> Strings(SerializedObject so, string field)
        {
            var array = so.FindProperty(field);
            for (var i = 0; array != null && array.isArray && i < array.arraySize; i++)
            {
                var s = array.GetArrayElementAtIndex(i).stringValue?.Trim();
                if (!string.IsNullOrEmpty(s)) yield return s;
            }
        }
    }
}
