// Licensed under the MIT License. See LICENSE in the project root for license information.
//
// TEMPORARY - CS-274 palm axis readout. Remove once DockController.palmAxis is confirmed on device.
//
// Logs, once per second per tracked hand, the dot product of each palm-joint axis with the line from the
// palm to the eyes. The owner holds a palm toward his face and then away; whichever axis reads near +1
// facing and near -1 turned away is the palm normal DockController.PalmNormal should use.
//
//   adb logcat -d -s Unity | grep PalmProbe
//
// Lives on the XR rig root (added by tools/mcp/rig_hand_look.cs). Touches nothing in the app.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    public class PalmAxisProbe : MonoBehaviour
    {
        private const string Tag = "[PalmProbe]";
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }

            _next = Time.unscaledTime + 1f;

            var rig = XRInputRig.Instance;
            var cam = Camera.main;
            if (rig == null || cam == null)
            {
                Debug.Log($"{Tag} rig={(rig != null)} camera={(cam != null)}");
                return;
            }

            Report("L", rig.LeftHandTracked ? rig.LeftPalm : null, cam.transform.position);
            Report("R", rig.RightHandTracked ? rig.RightPalm : null, cam.transform.position);
        }

        private static void Report(string hand, Transform palm, Vector3 head)
        {
            if (palm == null)
            {
                Debug.Log($"{Tag} {hand} untracked");
                return;
            }

            var toHead = head - palm.position;
            if (toHead.sqrMagnitude < 1e-4f)
            {
                Debug.Log($"{Tag} {hand} palm at head");
                return;
            }

            toHead.Normalize();
            Debug.Log($"{Tag} {hand} up={Vector3.Dot(palm.up, toHead):+0.00;-0.00} " +
                      $"fwd={Vector3.Dot(palm.forward, toHead):+0.00;-0.00} " +
                      $"right={Vector3.Dot(palm.right, toHead):+0.00;-0.00} " +
                      $"dist={toHead.magnitude:0.00}");
        }
    }
}
