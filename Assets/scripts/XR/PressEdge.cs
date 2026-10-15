// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine.InputSystem.Controls;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// A key or button press seen as its one-frame edge or as down now after up last frame, so a hitch or queued
    /// input cannot skip it (CS-365, CS-370). Read it every frame the control matters, or its last state goes stale.
    /// </summary>
    public struct PressEdge
    {
        private bool _wasDown;

        public bool Read(ButtonControl control)
        {
            if (control == null)
            {
                _wasDown = false;
                return false;
            }

            var down = control.isPressed;
            var edge = control.wasPressedThisFrame || (down && !_wasDown);
            _wasDown = down;
            return edge;
        }
    }
}
