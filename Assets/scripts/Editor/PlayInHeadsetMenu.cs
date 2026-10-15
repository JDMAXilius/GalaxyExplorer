// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEditor;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Whether editor Play starts OpenXR (<see cref="Launch"/>). Off by default, so Play runs the desktop mode even
    /// while Meta Quest Link is up; tick it to test on the Quest over Link. Read at the start of Play, so change it
    /// before pressing Play. Stored per machine in EditorPrefs.
    /// </summary>
    internal static class PlayInHeadsetMenu
    {
        private const string MenuPath = "Cosmic Simulation/Play in Headset";

        [MenuItem(MenuPath, priority = 0)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(Launch.PlayInHeadsetPref, !EditorPrefs.GetBool(Launch.PlayInHeadsetPref, false));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(Launch.PlayInHeadsetPref, false));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
