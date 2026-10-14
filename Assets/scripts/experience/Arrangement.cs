// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CosmicSimulation
{
    /// <summary>The one "put it all back" (CS-345): R, Settings' Restore and the being's "restore" all come here.</summary>
    public static class Arrangement
    {
        /// <summary>Content with no solver or rig comes home always; bodies only when <paramref name="bodies"/>.</summary>
        public static void Restore(bool bodies = true)
        {
            // First and unconditional: non-body content has no solver or rig, and the rig branch returns (CS-107).
            FreePlacementAnchor.RestoreAll();
            if (!bodies)
            {
                return;
            }

            // The rig animates anchors and walks pulled bodies home together; don't snap bodies behind its back.
            var rigged = false;
            foreach (var rig in Registry<LayoutRig>.Active)
            {
                rig.Restore();
                rigged = true;
            }

            if (rigged)
            {
                return;
            }

            // An experience without a rig: each body's own solver eases it home, the path a tap takes (CS-174).
            foreach (var solver in Registry<ForceSolver>.Active)
            {
                solver.ReturnHome();
            }
        }
    }
}
