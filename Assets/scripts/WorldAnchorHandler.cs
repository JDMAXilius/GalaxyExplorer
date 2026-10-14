// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer
{
    /// <summary>Places the view where the player put it, facing them. (The HoloLens world anchor it once added is gone.)</summary>
    public class WorldAnchorHandler : SingleInstance<WorldAnchorHandler>
    {
        public void CreateWorldAnchor(Vector3 position)
        {
            var source = GalaxyExplorerManager.Instance.ViewLoaderScript.transform;
            source.position = position;

            var lookDirection = Camera.main.transform.position - position;
            lookDirection.y = 0;
            source.rotation = Quaternion.LookRotation(-lookDirection.normalized);
        }
    }
}
