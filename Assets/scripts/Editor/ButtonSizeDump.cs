// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Text;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The evidence CS-278 asks for: every interactable's collider in world millimetres, what handles it, and
    /// whether a fingertip can select it (an <see cref="XRPokeFilter"/> beside it - the poke interactors refuse
    /// anything without one). Walks the UI prefabs on disk and whatever scenes are open, so it runs in and out
    /// of play mode, and writes the table to <c>Logs/button_sizes.log</c> as well as the console, which keeps
    /// only 200 entries.
    /// </summary>
    public static class ButtonSizeDump
    {
        private const string PrefabFolder = "Assets/prefabs/ui";
        private const string LogPath = "Logs/button_sizes.log";

        /// <summary>Meta's minimum for a direct-touch target, across the face a finger lands on.</summary>
        private const float MinimumMm = 22f;

        [MenuItem("Cosmic Simulation/Verify/Dump Button Sizes")]
        public static void Dump()
        {
            var table = new StringBuilder();
            int total = 0, small = 0, unpokeable = 0;

            foreach (var interactable in Object.FindObjectsByType<GEInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Row(table, "scene", interactable, ref total, ref small, ref unpokeable);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var interactable in contents.GetComponentsInChildren<GEInteractable>(true))
                    {
                        Row(table, Path.GetFileNameWithoutExtension(path), interactable, ref total, ref small, ref unpokeable);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            table.AppendLine($"[SIZE] {total} collider(s): {small} under {MinimumMm} mm, {unpokeable} without a poke filter");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(LogPath, table.ToString());
            Debug.Log(table.ToString());
        }

        private static void Row(StringBuilder table, string context, GEInteractable interactable,
                                ref int total, ref int small, ref int unpokeable)
        {
            var go = interactable.gameObject;
            var handler = go.GetComponentInParent<IGEPointerHandler>(true);
            var handlerName = handler != null ? handler.GetType().Name : "none";
            var poke = go.GetComponent<XRPokeFilter>() != null;

            // A canvas-less UI piece (the dock tile) is only ever instantiated under a millimetre canvas, so it is
            // measured as it will be used rather than at the 1000x its bare prefab reads.
            var scale = go.transform.lossyScale;
            if (go.transform is RectTransform && go.GetComponentInParent<Canvas>(true) == null)
            {
                scale *= 0.001f;
            }

            foreach (var collider in go.GetComponents<Collider>())
            {
                var mm = SizeMm(collider, scale);
                var isSmall = Mathf.Min(mm.x, mm.y) < MinimumMm;
                total++;
                if (isSmall) small++;
                if (!poke) unpokeable++;
                table.AppendLine($"[SIZE] {context}/{PathOf(go)}  {handlerName}  {mm.x:0.#} x {mm.y:0.#} x {mm.z:0.#} mm" +
                                 $"  poke={(poke ? "yes" : "no")}{(isSmall ? "  SMALL" : string.Empty)}");
            }
        }

        private static Vector3 SizeMm(Collider collider, Vector3 scale)
        {
            Vector3 size;
            switch (collider)
            {
                case BoxCollider box:
                    size = box.size;
                    break;
                case SphereCollider sphere:
                    size = Vector3.one * (sphere.radius * 2f);
                    break;
                case CapsuleCollider capsule:
                    var d = capsule.radius * 2f;
                    size = capsule.direction == 0 ? new Vector3(capsule.height, d, d)
                         : capsule.direction == 1 ? new Vector3(d, capsule.height, d)
                         : new Vector3(d, d, capsule.height);
                    break;
                default:
                    size = collider.bounds.size;
                    scale = Vector3.one;
                    break;
            }

            return new Vector3(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)) * 1000f;
        }

        private static string PathOf(GameObject go)
        {
            var path = go.name;
            for (var t = go.transform.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
