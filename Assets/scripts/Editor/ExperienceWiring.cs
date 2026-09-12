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

        // Room state -> music bed (GDD 9 asks for one track per environment mode). There are four states and three
        // tracks, so BlackHalo and FullBlack share the galaxy bed: they are the two that put the player in deep
        // space, and a nebula opened inside the Milky Way map should not change the music under the map.
        // **This mapping is a choice, not spec — it wants owner sign-off.**
        private static readonly (EnvironmentMode mode, string clip)[] MusicTracks =
        {
            (EnvironmentMode.Passthrough, "Assets/audio/music_audio_clips/background_music_audio_clip.wav"),
            (EnvironmentMode.Dimmed,      "Assets/audio/music_audio_clips/bgm_system_audio_clip.wav"),
            (EnvironmentMode.BlackHalo,   "Assets/audio/music_audio_clips/bgm_galaxy_audio_clip.wav"),
            (EnvironmentMode.FullBlack,   "Assets/audio/music_audio_clips/bgm_galaxy_audio_clip.wav"),
        };

        // What the one legacy bed played at before its mixer group attenuated it. A proper loudness pass
        // (GDD 9: -16 LUFS) is a listening job, not a code one.
        private const float MusicVolume = 0.35f;

        // The five legacy beds live under this one object in the boot scene.
        private const string LegacyMusicRoot = "MusicAudioSources";

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

                // intValue, not enumValueIndex: the index is the position in the popup, which only happens to
                // equal the value while EnvironmentMode is numbered contiguously from zero.
                so.FindProperty("Environment").intValue = (int)mode;
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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("ExperienceWiring: leave play mode first.");
                return;
            }

            // Work on the boot scene where it already is. Re-opening it would close whatever else is loaded,
            // and if any of those has unsaved changes Unity raises a modal that blocks the relay outright
            // (see CLAUDE.md) - which is exactly what happens after a play session leaves main_scene dirty.
            var scene = default(Scene);
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.path == BootScene)
                {
                    scene = open;
                    break;
                }
            }

            if (!scene.IsValid())
            {
                // Not open, so we have to open it - and that is only safe while nothing else is dirty.
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var open = SceneManager.GetSceneAt(i);
                    if (open.isDirty)
                    {
                        Debug.LogError($"ExperienceWiring: '{open.name}' has unsaved changes, and the boot scene " +
                                       "is not open. Save or discard, or open core_systems_scene first.");
                        return;
                    }
                }

                scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
            }

            // Scoped to the boot scene's own roots: GameObject.Find searches every loaded scene, and now that
            // we no longer close the others it could pick up a same-named object from a view scene.
            GameObject root = null;
            foreach (var candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == "cosmic_systems")
                {
                    root = candidate;
                    break;
                }
            }

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

            // Music sits beside the EnvironmentController because the beds are per room state (GDD 9), not per
            // experience, and the room state is what that component owns.
            var music = root.GetComponent<MusicController>() ?? root.AddComponent<MusicController>();
            var musicWired = WireMusic(music);

            if (musicWired)
            {
                RetireLegacyMusic(scene);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"ExperienceWiring: systems installed in {scene.name} " +
                      $"({ordered.Length} tiles, {destinations.Length} destinations, " +
                      $"dock={(dock != null ? "yes" : "MISSING")}, desktop={(desktopDock != null ? "yes" : "MISSING")}, " +
                      $"music={(musicWired ? "yes" : "MISSING")}).");
        }

        /// <summary>
        /// Fills an empty track list with the three beds the project has, and reports whether the component can
        /// actually make a sound — which is what the caller uses to decide whether the legacy music may be
        /// switched off. Hand-authored entries win: this only writes when nothing is assigned yet, so re-running
        /// the menu item never undoes a mix decision.
        /// </summary>
        private static bool WireMusic(MusicController music)
        {
            var so = new SerializedObject(music);
            var list = so.FindProperty("tracks");

            // "Already wired" has to mean "has a clip", not "has entries". A run where a clip path was broken
            // used to leave four entries behind with nothing in them; the next run read that as success, retired
            // the legacy music, and left the app permanently silent with nothing in the console to say why.
            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Clip").objectReferenceValue != null)
                {
                    return true;
                }
            }

            // Every clip is resolved before a single entry is written, because a half-written list is worse than
            // an empty one: the caller reads entries as permission to switch the inherited beds off.
            var clips = new AudioClip[MusicTracks.Length];
            var assigned = 0;
            for (var i = 0; i < MusicTracks.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicTracks[i].clip);
                if (clips[i] != null)
                {
                    assigned++;
                }
                else
                {
                    Debug.LogWarning($"ExperienceWiring: no music clip at '{MusicTracks[i].clip}'.");
                }
            }

            if (assigned == 0)
            {
                Debug.LogError("ExperienceWiring: not one music clip loaded, so MusicController was left untouched " +
                               "and the legacy music stays on. Fix the paths above and run this again.");
                return false;
            }

            list.arraySize = MusicTracks.Length;
            for (var i = 0; i < MusicTracks.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);

                // intValue, not enumValueIndex: see WireModules.
                element.FindPropertyRelative("Mode").intValue = (int)MusicTracks[i].mode;
                element.FindPropertyRelative("Clip").objectReferenceValue = clips[i];
                element.FindPropertyRelative("Volume").floatValue = MusicVolume;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Switches off the five inherited music sources. They all play the *same* clip through five mixer groups
        /// that the legacy per-view snapshots faded between; left on awake they would sit underneath every
        /// crossfade, at a level no longer controlled by anything we call. Deactivated rather than deleted, so the
        /// snapshot rig is still there to read.
        /// </summary>
        private static void RetireLegacyMusic(Scene scene)
        {
            var legacy = FindInScene(scene, LegacyMusicRoot);
            if (legacy == null || !legacy.gameObject.activeSelf)
            {
                return;
            }

            legacy.gameObject.SetActive(false);
            Debug.Log($"ExperienceWiring: '{LegacyMusicRoot}' switched off; MusicController now owns the bed.");
        }

        private static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == name)
                    {
                        return candidate;
                    }
                }
            }

            return null;
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
