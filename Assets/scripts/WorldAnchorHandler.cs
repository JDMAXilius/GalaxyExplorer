// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using HoloToolkit.Unity;
using UnityEngine;

namespace GalaxyExplorer
{
    public class WorldAnchorHandler : SingleInstance<WorldAnchorHandler>
    {
        // Unity 6: UnityEngine.XR.WSA.WorldAnchor (HoloLens) no longer exists, so anchoring only
        // compiles on the legacy WSA runtime. Elsewhere the view is placed but not anchored.
#if UNITY_WSA && !UNITY_2020_1_OR_NEWER
        private UnityEngine.XR.WSA.WorldAnchor anchor;
#endif

        public void CreateWorldAnchor(Vector3 position)
        {
            GameObject sourceObject = GalaxyExplorerManager.Instance.ViewLoaderScript.gameObject;
            sourceObject.transform.position = position;

            // rotate to face camera
            var lookDirection = Camera.main.transform.position - position;
            lookDirection.y = 0;
            var rotation = Quaternion.LookRotation(-lookDirection.normalized);
            sourceObject.transform.rotation = rotation;

#if UNITY_WSA && !UNITY_2020_1_OR_NEWER
            anchor = sourceObject.AddComponent<UnityEngine.XR.WSA.WorldAnchor>();
            if (anchor)
            {
                anchor.OnTrackingChanged += GalaxyWorldAnchor_OnTrackingChanged;
            }
#endif
        }

        public void DestroyWorldAnchor()
        {
#if UNITY_WSA && !UNITY_2020_1_OR_NEWER
            if (anchor != null)
            {
                anchor.OnTrackingChanged -= GalaxyWorldAnchor_OnTrackingChanged;
                DestroyImmediate(anchor);
                anchor = null;
            }
#endif
        }

#if UNITY_WSA && !UNITY_2020_1_OR_NEWER
        #region Callbacks

        private void GalaxyWorldAnchor_OnTrackingChanged(UnityEngine.XR.WSA.WorldAnchor self, bool located)
        {
            // Debug.Log($"WorldAnchorHandler: tracking changed to {(located ? "located":"lost")}");
            GalaxyExplorerManager.Instance.TransitionManager.CurrentActiveScene?.SetActive(located);
        }

        #endregion
#endif
    }
}