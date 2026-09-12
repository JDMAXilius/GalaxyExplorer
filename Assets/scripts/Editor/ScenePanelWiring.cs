// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Hands <see cref="ExperienceDirector"/> the prefab it spawns an experience's own panel from.
    ///
    /// The project's convention is a serialized prefab reference filled by an editor menu item — the same way
    /// the dock, the pop-up and the desktop dock are installed — rather than a <c>Resources.Load</c> or a
    /// <c>Shader.Find</c>-style lookup by name, neither of which survives a player build with any certainty.
    /// This is a separate menu item only because <c>ExperienceWiring.InstallSystems</c> was being edited by
    /// someone else the day the panel landed; the two should be one call, and folding this in is a one-line
    /// change there (see the CS-077 note in docs/BACKLOG.md).
    ///
    /// It never opens a scene. Opening one while another is dirty raises a modal that blocks the MCP relay
    /// outright (CLAUDE.md), so this works on what is already loaded and says so when the boot scene is not.
    /// </summary>
    public static class ScenePanelWiring
    {
        private const string PanelPrefabPath = "Assets/prefabs/ui/info_panel_prefab.prefab";

        [MenuItem("Cosmic Simulation/Wire Scene Panel")]
        public static void WireScenePanel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ScenePanelWiring: leave play mode first.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var prefab = asset != null ? asset.GetComponent<InfoPanel>() : null;
            if (prefab == null)
            {
                Debug.LogError($"ScenePanelWiring: no InfoPanel at '{PanelPrefabPath}'. Run " +
                               "Cosmic Simulation/Build UI Prefabs first.");
                return;
            }

            var directors = Object.FindObjectsByType<ExperienceDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (directors.Length == 0)
            {
                Debug.LogError("ScenePanelWiring: no ExperienceDirector in any open scene. Open " +
                               "core_systems_scene and run this again.");
                return;
            }

            var wired = 0;
            foreach (var director in directors)
            {
                var so = new SerializedObject(director);
                var property = so.FindProperty("scenePanelPrefab");
                if (property == null)
                {
                    continue;
                }

                if (property.objectReferenceValue == prefab)
                {
                    continue;
                }

                property.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();

                var scene = director.gameObject.scene;
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                wired++;
            }

            Debug.Log($"ScenePanelWiring: {wired} of {directors.Length} director(s) updated; the rest already " +
                      "pointed at the panel.");
        }
    }
}
