// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small Unity helpers the app used from MRTK v2's extension methods.
/// </summary>
public static class GEUnityExtensions
{
    /// <summary>
    /// Marks the object to survive scene loads (only valid on root objects in play mode).
    /// </summary>
    public static void DontDestroyOnLoad(this Object target)
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            return;
        }
#endif
        Object.DontDestroyOnLoad(target);
    }

    public static void SetLayerRecursively(this GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            child.gameObject.SetLayerRecursively(layer);
        }
    }
}
