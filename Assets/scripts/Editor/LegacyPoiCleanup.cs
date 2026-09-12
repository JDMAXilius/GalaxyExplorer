// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Switches off the two inherited Milky Way markers that were still drawing on top of our own destination
    /// tags, so the map carries one label per place instead of two.
    ///
    /// <para><b>Why these two and no others.</b> CS-050 retired the original app's POI cards by having
    /// <c>CardPOI.Awake</c> deactivate its own marker, which took the five small markers - Crab, Homunculus,
    /// Trumpler 14, NGC 1501, Pillars - out of the map cleanly. It missed two, because neither carries
    /// <c>CardPOI</c>: <c>solar_system</c> inside <c>galaxy_pois_prefab</c> and <c>galactic_center</c> placed
    /// directly in <c>galaxy_view_scene</c> are both <see cref="PlanetPOI"/>, the marker kind that loads a scene
    /// rather than opening a card. So the Milky Way drew nine of our tags plus those two, and the two places
    /// the player is most likely to visit were the two with a label on top of a label.</para>
    ///
    /// <para><b>Both are <c>poi_prefab_large</c> instances, not bare objects</b>, and the <c>PlanetPOI</c> sits
    /// on a child called <c>POI</c> rather than on the renamed instance root. That is why this matches on
    /// <c>GetOutermostPrefabInstanceRoot</c> and not on the component's own GameObject: matching on the
    /// component found two objects both called "POI", neither called <c>galactic_center</c>, and reported
    /// cheerfully that there was nothing to do.</para>
    ///
    /// <para><b>What deactivating them takes with it.</b> <c>ZoomInOut</c> and <c>TransitionManager</c> look
    /// these up with <c>FindObjectsOfType&lt;PlanetPOI&gt;</c> and <c>GetComponentsInChildren&lt;PlanetPOI&gt;</c>,
    /// both of which skip inactive objects, so after this pass those lookups find nothing for the galaxy map.
    /// That is harmless, and it is worth writing down why rather than rediscovering it: those lookups exist to
    /// find the focus collider for the legacy drill-down zoom, and the only thing that ever started that zoom
    /// for these two markers was <c>PlanetPOI.OnPointerDown</c> on the markers themselves. Our tags go through
    /// <see cref="ExperienceDirector"/> instead. The one surviving caller of
    /// <c>TransitionManager.LoadNextScene</c> is <c>IntroFlow</c>, which loads the galaxy view from the intro,
    /// and neither of those two scenes holds a <c>PlanetPOI</c> naming the other - so that lookup returned null
    /// before this change as well.</para>
    ///
    /// <para><b>Why not the same trick on <c>PlanetPOI</c>.</b> A <c>keepLegacyMarkers</c> flag in
    /// <c>PlanetPOI.Awake</c> would be one line and would also switch off the twelve planet markers inside
    /// <c>solar_system_prefab</c> - which are the Solar System view's live interaction surface, carrying
    /// <c>PlanetInfoCard</c>. Those must stay. The two markers retired here are named individually for that
    /// reason, and the pass logs every <c>PlanetPOI</c> it leaves alone so the next person can see that the
    /// omission is a decision.</para>
    ///
    /// <para><b>Deactivated, not deleted</b>, on the same reasoning as <c>CardPOI</c>: both are prefab
    /// instances referenced from shipping content, and a dangling reference in a scene is worse than an
    /// inactive object. Idempotent - it only writes when something was still on.</para>
    /// </summary>
    public static class LegacyPoiCleanup
    {
        private const string PoiPrefabPath = "Assets/prefabs/poi_prefabs/galaxy_pois_prefab.prefab";

        /// <summary>The marker inside the POI prefab, by name under its root.</summary>
        private const string PrefabMarkerName = "solar_system";

        /// <summary>The marker that sits loose in <c>galaxy_view_scene</c>, by name.</summary>
        private const string SceneMarkerName = "galactic_center";

        /// <summary>The scene that one lives in. Named so the pass can say what to open if it is not there.</summary>
        private const string SceneName = "galaxy_view_scene";

        [MenuItem("Cosmic Simulation/Retire Legacy Milky Way Markers")]
        public static void Retire()
        {
            Run(false);
        }

        /// <summary>Reports what is still drawing without writing anything.</summary>
        [MenuItem("Cosmic Simulation/Report Legacy Milky Way Markers")]
        public static void Report()
        {
            Run(true);
        }

        private static void Run(bool reportOnly)
        {
            var what = reportOnly ? "Report" : "Retire";

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"LegacyPoiCleanup: leave play mode first ({what} edits assets).");
                return;
            }

            var log = new List<string>();
            var changed = 0;

            changed += InPrefab(reportOnly, log);
            changed += InScene(reportOnly, log);

            var verb = reportOnly ? "would be switched off" : "switched off";
            Debug.Log($"LegacyPoiCleanup ({what}): {changed} marker(s) {verb}.\n  " +
                      string.Join("\n  ", log));
        }

        // ---------- the one inside the POI prefab

        private static int InPrefab(bool reportOnly, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PoiPrefabPath) == null)
            {
                log.Add($"{PoiPrefabPath}: not found, nothing done.");
                return 0;
            }

            var root = PrefabUtility.LoadPrefabContents(PoiPrefabPath);
            if (root == null)
            {
                log.Add($"{PoiPrefabPath}: would not open for editing.");
                return 0;
            }

            try
            {
                var marker = Find(root.transform, PrefabMarkerName);
                if (marker == null)
                {
                    log.Add($"{PoiPrefabPath}: no '{PrefabMarkerName}' - already removed?");
                    return 0;
                }

                if (!marker.gameObject.activeSelf)
                {
                    log.Add($"{PoiPrefabPath}: '{PrefabMarkerName}' already off.");
                    return 0;
                }

                log.Add($"{PoiPrefabPath}: '{PrefabMarkerName}' ({Kind(marker)}) -> off. " +
                        "The 'Solar System' tag now labels it.");

                if (reportOnly)
                {
                    return 1;
                }

                marker.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PoiPrefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- the one loose in the scene

        /// <summary>
        /// Acts on whatever is already open rather than opening the scene itself.
        ///
        /// <c>EditorSceneManager.OpenScene</c> on a dirty scene puts up a modal save prompt, which deadlocks
        /// the MCP relay this project is driven through - so this follows the pattern <c>ScenePanelWiring</c>
        /// set: find the object in an open scene, and if the scene is not open, say so rather than open it.
        /// </summary>
        private static int InScene(bool reportOnly, List<string> log)
        {
            var open = false;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == SceneName)
                {
                    open = true;
                    break;
                }
            }

            if (!open)
            {
                log.Add($"'{SceneName}' is not open, so '{SceneMarkerName}' was not touched. " +
                        "Open that scene and run this again.");
                return 0;
            }

            // Read before anything is touched: after the first SetActive the scene is dirty by definition,
            // and the question this answers is whether it was dirty beforehand.
            var wasClean = true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var candidate = SceneManager.GetSceneAt(i);
                if (candidate.name == SceneName)
                {
                    wasClean = !candidate.isDirty;
                    break;
                }
            }

            var markers = Object.FindObjectsByType<PlanetPOI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var changed = 0;
            var spared = new List<string>();

            foreach (var marker in markers)
            {
                var scene = marker.gameObject.scene;

                // The component sits on a child called "POI"; the name that identifies the marker is its
                // prefab instance root's. Matching on the component's own GameObject found two objects both
                // called "POI" and neither called "galactic_center", and reported that the marker was not
                // there - which is the kind of clean-looking nothing-happened that hides a job half done.
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(marker.gameObject) as GameObject;
                var target = root != null ? root : marker.gameObject;

                if (target.name != SceneMarkerName || scene.name != SceneName)
                {
                    // Every other PlanetPOI is live content - the twelve planets in the Solar System view
                    // among them - and is deliberately left alone. Named in the log so that is visible.
                    spared.Add($"{scene.name}/{target.name}");
                    continue;
                }

                if (!target.activeSelf)
                {
                    log.Add($"{scene.name}: '{SceneMarkerName}' already off.");
                    continue;
                }

                log.Add($"{scene.name}: '{SceneMarkerName}' (PlanetPOI on '{marker.gameObject.name}') -> off. " +
                        "The 'Galactic Center' tag now labels it.");
                changed++;

                if (reportOnly)
                {
                    continue;
                }

                target.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);

                // Saved only if the scene had nothing else unsaved in it. SaveScene writes the whole file, so
                // a menu item called "Retire Legacy Milky Way Markers" would otherwise quietly commit a light
                // somebody had nudged or a test object they had dropped in. If there was other work in flight,
                // the change is left dirty and the log says to save deliberately.
                if (wasClean)
                {
                    EditorSceneManager.SaveScene(scene);
                    log.Add($"{scene.name}: saved.");
                }
                else
                {
                    log.Add($"{scene.name} had unsaved changes of its own, so it was marked dirty and NOT " +
                            "saved. Save it yourself once you have checked what else is in there.");
                }
            }

            if (spared.Count > 0)
            {
                log.Add($"left alone ({spared.Count} live PlanetPOI): {string.Join(", ", spared)}");
            }

            return changed;
        }

        // ---------- helpers

        /// <summary>
        /// What kind of marker this is, for the log.
        ///
        /// Searched through the children, not just the root: in <c>galaxy_pois_prefab</c> the instance root is
        /// a bare transform and the POI component sits under it. Looking only at the root reported "no POI
        /// component" for a marker that is plainly a <c>PlanetPOI</c>, which would have sent the next reader
        /// hunting for a third kind of marker that does not exist.
        /// </summary>
        private static string Kind(Transform marker)
        {
            if (marker.GetComponentInChildren<PlanetPOI>(true) != null)
            {
                return "PlanetPOI";
            }

            return marker.GetComponentInChildren<CardPOI>(true) != null ? "CardPOI" : "no POI component";
        }

        /// <summary>Depth-first search by name, because the marker's depth in the prefab is not ours to fix.</summary>
        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var hit = Find(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }
    }
}
