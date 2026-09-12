// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Switches the inherited Galaxy Explorer menus off inside <c>menu_managers.prefab</c>, so the app draws one
    /// dock instead of three overlapping menus.
    ///
    /// <para><b>What was wrong.</b> Three menu systems shipped in the same scene: the world dock
    /// (<c>dock_prefab</c>), the desktop dock (<c>desktop_dock_prefab</c>), and the legacy HUD inside
    /// <c>menu_managers.prefab</c> — which is really two HUDs, the desktop button row (<c>desktop_menu</c>) and
    /// the gaze-and-commit tool panel (<c>ggv_menu</c>), both saved <b>active</b>. Whether either of those hid
    /// itself at startup depended on code that can throw before it gets there: <c>GGVMenuManager.Start</c>
    /// dereferences <c>GalaxyExplorerManager.Instance.GeFadeManager</c> before it calls its own
    /// <c>HideMenu</c>, and <c>GlobalMenuManager</c> routes a Quest 3 session into <c>HandMenuManager</c>, whose
    /// hand-menu references are null in this prefab. A menu that is only hidden by a line that may never run is
    /// a menu that is sometimes on screen on top of the dock.</para>
    ///
    /// <para><b>Deactivated, not deleted.</b> A previous pass established that this family cannot simply be
    /// removed: <c>PointOfInterest</c> registers planets with <c>CardPOIManager</c>, <c>GalaxyExplorerManager</c>
    /// finds <c>GGVMenuManager</c> by type, <c>GlobalMenuManager</c> and <c>DesktopMenuManager</c> still own the
    /// mute preference and the controls overlay, and every one of these prefabs is referenced by a live scene.
    /// So this switches off the two menu <i>roots</i> and leaves every component, reference and file in place.
    /// The manager components sit on the prefab root, which stays active, so they still run and can still be
    /// found — only their visible furniture is gone.</para>
    ///
    /// <para><b>What deliberately survives.</b> <c>desktop_help_panel</c>, the controls overlay H, F1 and the
    /// dock's Help button open. <c>DesktopMenuManager.SetHelpVisible</c> lifts the <c>desktop_menu</c> root back
    /// on for as long as that overlay is up and puts it down again afterwards, which is why the button row and
    /// the "..." button are switched off individually here as well: without that they would ride back on screen
    /// with the overlay.</para>
    ///
    /// <para>Idempotent: it only writes the prefab when something was actually still on, so running it twice
    /// changes nothing the second time. Roadmap 4.3, CS-087 / CS-088 / CS-111; GDD 3.1 and 5.3.</para>
    /// </summary>
    public static class LegacyMenuCleanup
    {
        private const string PrefabPath = "Assets/prefabs/menu_managers.prefab";

        /// <summary>
        /// The legacy menu furniture, as paths from the prefab root. Order matters only for the log: the two
        /// roots first, then the pieces under <c>desktop_menu</c> that must stay down even when the controls
        /// overlay lifts that root for itself.
        /// </summary>
        private static readonly string[] RetiredPaths =
        {
            "desktop_menu",
            "ggv_menu",
            "desktop_menu/canvas/desktop_dots_button",
            "desktop_menu/canvas/desktop_buttons_parent",
        };

        /// <summary>
        /// Paths this must never switch off, checked after the pass so a future edit to the list above cannot
        /// quietly take the controls overlay with it.
        /// </summary>
        private static readonly string[] KeepReachablePaths =
        {
            "desktop_menu/canvas/desktop_help_panel",
        };

        [MenuItem("Cosmic Simulation/Retire Legacy Menus")]
        public static void RetireLegacyMenus()
        {
            Run(false);
        }

        /// <summary>Reports what the prefab currently holds without writing anything.</summary>
        [MenuItem("Cosmic Simulation/Report Legacy Menus")]
        public static void ReportLegacyMenus()
        {
            Run(true);
        }

        private static void Run(bool reportOnly)
        {
            var what = reportOnly ? "ReportLegacyMenus" : "RetireLegacyMenus";

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"LegacyMenuCleanup: leave play mode first ({what} edits an asset).");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"LegacyMenuCleanup: no prefab at '{PrefabPath}'. Nothing was changed.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError($"LegacyMenuCleanup: '{PrefabPath}' would not open for editing.");
                return;
            }

            var switchedOff = new List<string>();
            var alreadyOff = new List<string>();
            var stillOn = new List<string>();
            var missing = new List<string>();
            var kept = new List<string>();
            var saved = false;

            try
            {
                foreach (var path in RetiredPaths)
                {
                    var target = root.transform.Find(path);
                    if (target == null)
                    {
                        // Not an error: somebody may have finished the surgery and removed the object outright.
                        missing.Add(path);
                        continue;
                    }

                    if (!target.gameObject.activeSelf)
                    {
                        alreadyOff.Add(path);
                        continue;
                    }

                    if (reportOnly)
                    {
                        stillOn.Add(path);
                        continue;
                    }

                    target.gameObject.SetActive(false);
                    switchedOff.Add(path);
                }

                foreach (var path in KeepReachablePaths)
                {
                    var target = root.transform.Find(path);
                    kept.Add(target == null ? path + " (MISSING)" : path);
                }

                if (!reportOnly && switchedOff.Count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out saved);
                }
            }
            finally
            {
                // Always, including after an exception: prefab contents left loaded leak a hidden preview scene
                // that later menu items then open on top of.
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (!reportOnly && switchedOff.Count > 0 && !saved)
            {
                Debug.LogError($"LegacyMenuCleanup: '{PrefabPath}' would not save. Nothing was changed.");
                return;
            }

            var report = $"LegacyMenuCleanup ({what}) on {PrefabPath}\n" +
                         Line("switched off now", switchedOff) +
                         Line("still on", stillOn) +
                         Line("already off", alreadyOff) +
                         Line("not in the prefab", missing) +
                         Line("left reachable", kept);

            if (!reportOnly && switchedOff.Count == 0)
            {
                // The idempotent second run. Said plainly so a re-run does not read as a failure.
                report += "Nothing to do: the legacy menus were already retired.\n";
            }

            Debug.Log(report);
        }

        private static string Line(string label, IReadOnlyList<string> items)
        {
            return items.Count == 0
                ? string.Empty
                : $"  {label}: {string.Join(", ", items)}\n";
        }
    }
}
