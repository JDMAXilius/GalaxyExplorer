// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicSimulation
{
    /// <summary>
    /// Mouse control for looking around a place on a monitor, with nothing else loaded.
    ///
    /// <para>This is a <b>test harness</b>, not the app's input. The app routes the mouse through
    /// <c>DesktopMouseInput</c>, which needs <c>GalaxyExplorerManager</c>, the camera controller, the pointer
    /// and the whole boot behind it. That is the right thing for the product and far too much to stand up when
    /// the question is only "does this nebula look right from inside". So: drop this on a camera in an
    /// otherwise empty scene, press Play, and look.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Left drag</b> — turn your head.</item>
    /// <item><b>Right drag</b> — slide sideways and up (pan).</item>
    /// <item><b>Wheel</b> — move forward and back through the gas.</item>
    /// <item><b>R</b> — back to the middle, however lost you get.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class FreeLook : MonoBehaviour
    {
        [SerializeField] private float degreesPerScreen = 220f;
        [SerializeField] private float panMetresPerScreen = 4f;
        [SerializeField] private float metresPerNotch = 0.35f;

        private Vector3 _home;
        private Quaternion _homeRotation;
        private float _yaw, _pitch;

        private void Start()
        {
            _home = transform.position;
            _homeRotation = transform.rotation;
            var euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var delta = mouse.delta.ReadValue();

            if (mouse.leftButton.isPressed)
            {
                _yaw += delta.x / Screen.width * degreesPerScreen;
                _pitch = Mathf.Clamp(_pitch - delta.y / Screen.height * degreesPerScreen, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            if (mouse.rightButton.isPressed)
            {
                var pan = -transform.right * (delta.x / Screen.width * panMetresPerScreen)
                          - transform.up * (delta.y / Screen.height * panMetresPerScreen);
                transform.position += pan;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // The wheel reports either notches or raw 120s depending on the platform; both end up sane.
                var notches = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
                transform.position += transform.forward * (notches * metresPerNotch);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                transform.SetPositionAndRotation(_home, _homeRotation);
                var euler = _homeRotation.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            }
        }
    }
}
