// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*
 * @author Valentin Simonov / http://va.lent.in/
 */

#if TOUCHSCRIPT_DEBUG

using UnityEngine;

namespace TouchScript.Debugging.GL
{
    public static class DebugHelper
    {
        public static int GetDebugId(Object obj)
        {
            // GetInstanceID() is a compile error in Unity 6.6+; GetHashCode() is also unique per live object.
            return int.MinValue + (obj.GetHashCode() << 10);
        }
    }
}

#endif