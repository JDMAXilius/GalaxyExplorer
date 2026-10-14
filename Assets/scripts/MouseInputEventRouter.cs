// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Empty: the mouse reaches every <see cref="GalaxyExplorer.XR.GEButton"/> through DesktopMouseInput's pointer, and the
/// old UnityEngine.Input polling went with Active Input Handling "Both" (CS-354). Kept until CS-369 strips it from prefabs.
/// </summary>
public class MouseInputEventRouter : MonoBehaviour
{
}
