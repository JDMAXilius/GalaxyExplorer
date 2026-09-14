using UnityEngine;

namespace CosmicSimulation.Being
{
    public static class BeingActions
    {
        public static void Run(string name, string args)
        {
            switch (name)
            {
                case "open_place":
                    ExperienceDirector.Instance?.Switch(args);
                    break;
                case "pull_body":
                    Object.FindAnyObjectByType<LayoutRig>()?.Find(args)?.Force?.OnPointerDown();
                    break;
                case "restore":
                    FreePlacementAnchor.RestoreAll();
                    foreach (var rig in Object.FindObjectsByType<LayoutRig>(FindObjectsSortMode.None))
                    {
                        rig.Restore();
                    }
                    break;
            }
        }
    }
}
