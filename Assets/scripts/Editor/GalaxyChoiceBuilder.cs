// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Fills the Galaxies tile's list of places, and makes the outside galaxies places rather than pins.
    ///
    /// <para>A galaxy is somewhere you go, the way Andromeda is - its own content, its own room, its own
    /// narration. M51, M101 and M33 were tags on the Milky Way's map, which said the opposite: that they are
    /// features of our galaxy. They come off that map (see <c>DestinationTagBuilder</c>) and are listed here
    /// instead, under the Galaxies tile, with Andromeda beside them so the list reads as every galaxy we
    /// know rather than as the leftovers.</para>
    ///
    /// <para>The three keep <c>ExperienceKind.Destination</c> on purpose. Kind is what
    /// <c>DockController</c> builds tiles from, and three more tiles would take the dock from seven to ten
    /// and change its curve on both the desktop and the headset. They are opened by
    /// <c>DockController.ChoosePlace</c>, which calls the same <c>ExperienceDirector.Switch</c> a tile does,
    /// so what happens when one opens is identical to Andromeda either way.</para>
    /// </summary>
    public static class GalaxyChoiceBuilder
    {
        private const string Folder = "Assets/data/experiences";
        private const string Host = "galaxies";

        private static readonly string[] Listed = { "andromeda", "whirlpool", "pinwheel", "triangulum" };

        [MenuItem("Cosmic Simulation/Build Galaxy Choices")]
        public static void Build()
        {
            var host = Load(Host);
            if (host == null)
            {
                Debug.LogError($"GalaxyChoiceBuilder: {Folder}/{Host}.asset is missing, so the Galaxies tile has nothing to list.");
                return;
            }

            var places = new System.Collections.Generic.List<ExperienceModule>();
            var missing = new System.Collections.Generic.List<string>();
            foreach (var id in Listed)
            {
                var module = Load(id);
                if (module == null) { missing.Add(id); continue; }
                places.Add(module);
            }

            var serialized = new SerializedObject(host);
            var list = serialized.FindProperty("Places");
            list.arraySize = places.Count;
            for (var i = 0; i < places.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = places[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();

            Debug.Log($"GalaxyChoiceBuilder: the Galaxies tile lists {places.Count} galaxy(ies) - {string.Join(", ", Listed)}"
                      + (missing.Count > 0 ? $"; missing: {string.Join(", ", missing)}" : string.Empty));
        }

        private static ExperienceModule Load(string id) =>
            AssetDatabase.LoadAssetAtPath<ExperienceModule>($"{Folder}/{id}.asset");
    }
}
