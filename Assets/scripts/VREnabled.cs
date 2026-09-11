// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

namespace GalaxyExplorer
{
    // Entities to enable in VR mode (opaque headsets, and Quest 3 while in VR rather than passthrough)
    public class VREnabled : MonoBehaviour
    {
        [SerializeField]
        List<GameObject> ActiveInVR = new List<GameObject>();

        void Start()
        {
            ExperienceModeManager.ModeChanged += OnModeChanged;
            UpdateActive();
        }

        void OnDestroy()
        {
            ExperienceModeManager.ModeChanged -= OnModeChanged;
        }

        void OnModeChanged(ExperienceModeManager.Mode mode)
        {
            UpdateActive();
        }

        void UpdateActive()
        {
            var active = ExperienceModeManager.ShowsVRScenery;
            foreach (var item in ActiveInVR)
            {
                if (item != null)
                {
                    item.SetActive(active);
                }
            }
        }
    }
}
