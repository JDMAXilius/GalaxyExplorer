// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using CosmicSimulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Points the experience modules at the scenes that exist and puts the runtime systems into the boot scene.
    ///
    /// Three of the seven places are already built as scenes, inherited from Galaxy Explorer: the galaxy map,
    /// the orbit model and the galactic centre. The other four are Phase 4 and 5 work and have no scene yet, so
    /// their modules stay sceneless and their tiles will simply do nothing until there is somewhere to go.
    ///
    /// Room state per module comes from GDD 3.4, not from taste: the map-like places dim the room so the
    /// content reads without shutting the player in, and the black hole goes fully dark.
    /// </summary>
    public static class ExperienceWiring
    {
        private const string BootScene = "Assets/scenes/core_systems_scene.unity";

        // module id -> the scene that already exists for it
        private static readonly (string id, string scene, EnvironmentMode mode)[] Wiring =
        {
            ("milky_way",            "galaxy_view_scene",           EnvironmentMode.Dimmed),
            ("solar_system",         "solar_system_view_scene",     EnvironmentMode.Dimmed),
            ("sagittarius_a",        "galactic_center_view_scene",  EnvironmentMode.FullBlack),
            ("solar_system_planets", null,                          EnvironmentMode.Dimmed),
            ("andromeda",            null,                          EnvironmentMode.Dimmed),
            ("cosmic_web",           null,                          EnvironmentMode.FullBlack),
            ("galaxies",             null,                          EnvironmentMode.FullBlack),
        };

        // The order the tiles appear on the dock, left to right (GDD 8.1).
        private static readonly string[] DockOrder =
        {
            "cosmic_web", "galaxies", "milky_way", "andromeda",
            "solar_system", "solar_system_planets", "sagittarius_a",
        };

        [MenuItem("Cosmic Simulation/Wire Experiences")]
        public static void WireModules()
        {
            var modules = LoadModules();
            var wired = 0;

            foreach (var (id, scene, mode) in Wiring)
            {
                if (!modules.TryGetValue(id, out var module))
                {
                    Debug.LogWarning($"ExperienceWiring: no module '{id}'");
                    continue;
                }

                var so = new SerializedObject(module);
                so.FindProperty("SceneName").stringValue = scene ?? string.Empty;
                so.FindProperty("Environment").enumValueIndex = (int)mode;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(module);

                if (scene != null)
                {
                    wired++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"ExperienceWiring: {wired} modules point at a scene, {Wiring.Length - wired} await one.");
        }

        [MenuItem("Cosmic Simulation/Install Runtime Systems")]
        public static void InstallSystems()
        {
            // Opening a scene while another is dirty raises a modal that blocks the relay, so refuse rather
            // than hang. See CLAUDE.md.
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty)
                {
                    Debug.LogError($"ExperienceWiring: '{open.name}' has unsaved changes. Save or discard first.");
                    return;
                }
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ExperienceWiring: leave play mode first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);

            var root = GameObject.Find("cosmic_systems");
            if (root == null)
            {
                root = new GameObject("cosmic_systems");
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            var environment = root.GetComponent<EnvironmentController>() ?? root.AddComponent<EnvironmentController>();
            var director = root.GetComponent<ExperienceDirector>() ?? root.AddComponent<ExperienceDirector>();

            // Modules in dock order, so the director's list and the dock agree without a second ordering.
            var byId = LoadModules();
            var ordered = DockOrder.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
            var destinations = byId.Values
                .Where(m => m.Kind == ExperienceKind.Destination)
                .OrderBy(m => m.DisplayName)
                .ToArray();

            var so = new SerializedObject(director);
            var list = so.FindProperty("modules");
            list.arraySize = ordered.Length + destinations.Length;
            for (var i = 0; i < ordered.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
            }

            for (var i = 0; i < destinations.Length; i++)
            {
                list.GetArrayElementAtIndex(ordered.Length + i).objectReferenceValue = destinations[i];
            }

            // Start where the original app starts, so the intro still leads somewhere familiar.
            so.FindProperty("startModule").objectReferenceValue = byId.TryGetValue("milky_way", out var start) ? start : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            var dockAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/ui/dock_prefab.prefab");
            var dock = root.GetComponentInChildren<DockController>(true);
            if (dock == null && dockAsset != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(dockAsset, root.transform);
                instance.name = "dock";
                dock = instance.GetComponent<DockController>();
            }

            var popupAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/ui/dock_popup_prefab.prefab");
            if (dock != null && popupAsset != null)
            {
                var dockSo = new SerializedObject(dock);
                var popupProp = dockSo.FindProperty("popup");
                if (popupProp.objectReferenceValue == null)
                {
                    var popupInstance = (GameObject)PrefabUtility.InstantiatePrefab(popupAsset, dock.transform.parent);
                    popupInstance.name = "dock_popup";
                    popupInstance.SetActive(false);
                    popupProp.objectReferenceValue = popupInstance.GetComponent<DockPopup>();
                    dockSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // The desktop mirror. The world dock parks below eye level, which reads correctly in a headset and
            // is simply off-screen on a monitor, so without this a desktop player cannot reach the dock at all.
            // It hides itself when an XR device is present, so it costs nothing in the headset.
            var desktopAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/ui/desktop_dock_prefab.prefab");
            var desktopDock = root.GetComponentInChildren<DesktopDock>(true);
            if (desktopDock == null && desktopAsset != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(desktopAsset, root.transform);
                instance.name = "desktop_dock";
                desktopDock = instance.GetComponent<DesktopDock>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"ExperienceWiring: systems installed in {scene.name} " +
                      $"({ordered.Length} tiles, {destinations.Length} destinations, " +
                      $"dock={(dock != null ? "yes" : "MISSING")}, desktop={(desktopDock != null ? "yes" : "MISSING")}).");
        }

        private static System.Collections.Generic.Dictionary<string, ExperienceModule> LoadModules()
        {
            return AssetDatabase.FindAssets("t:ExperienceModule", new[] { "Assets/data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && !string.IsNullOrEmpty(m.Id))
                .GroupBy(m => m.Id)
                .ToDictionary(g => g.Key, g => g.First());
        }
    }
}
