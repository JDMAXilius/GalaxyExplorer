// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// The desktop view (CS-172, CS-349): drag orbits, right-drag pans, the wheel dollies, Home goes back. Only the
    /// camera's rig root moves, never content, so experiences are untouched and headset sessions are left alone.
    /// </summary>
    public class DesktopCameraRig : MonoBehaviour
    {
        [SerializeField] private float orbitDegreesPerScreen = 200f;
        [SerializeField] private float panMetersPerScreen = 1.5f;
        [SerializeField] private float zoomMetersPerNotch = 0.25f;

        [SerializeField]
        [Tooltip("How far the view may pitch up or down from its home, in degrees, so a drag cannot roll the " +
                 "camera over the top.")]
        private float maxPitchDegrees = 85f;

        private Camera _camera;
        private Transform _rig;
        private Pose _rigHome;
        private float _orbitPitch;

        private bool _viewDefaultsStored;
        private Pose _pivotDefault, _entityDefault;

        public static DesktopCameraRig Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Orbit(Vector2 delta)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            var pivot = OrbitPivot();

            // FreeLook's convention, which the nebula scenes use.
            var yaw = delta.x / Screen.width * orbitDegreesPerScreen;
            var pitch = -delta.y / Screen.height * orbitDegreesPerScreen;
            var clamped = Mathf.Clamp(_orbitPitch + pitch, -maxPitchDegrees, maxPitchDegrees) - _orbitPitch;
            _orbitPitch += clamped;

            // Yaw about world up, then pitch about camera right: that pair never introduces roll.
            rig.RotateAround(pivot, Vector3.up, yaw);
            rig.RotateAround(pivot, _camera.transform.right, clamped);
        }

        public void Pan(Vector2 delta)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            var cameraTransform = _camera.transform;
            rig.position -= (cameraTransform.right * delta.x + cameraTransform.up * delta.y) / Screen.height * panMetersPerScreen;
        }

        public void Zoom(float notches)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            rig.position += _camera.transform.forward * (notches * zoomMetersPerNotch);
        }

        public void ResetView()
        {
            // Every desktop Recenter route funnels here (Home, DesktopDock.Recenter, legacy Reset View), so this is
            // where non-body content comes home (F-13, CS-107). DockController.Recenter needs the same in the headset.
            CosmicSimulation.FreePlacementAnchor.RestoreAll();

            // Back to where the intro left the camera (CS-172).
            var rig = Rig();
            if (rig != null)
            {
                rig.SetPositionAndRotation(_rigHome.position, _rigHome.rotation);
                _orbitPitch = 0f;
            }

            if (TryGetViewTransforms(out var pivot, out var entity))
            {
                entity.SetLocalPositionAndRotation(_entityDefault.position, _entityDefault.rotation);
                pivot.SetLocalPositionAndRotation(_pivotDefault.position, _pivotDefault.rotation);
            }
        }

        /// <summary>The camera's rig root, remembered as home the first time it is moved. Null in a headset.</summary>
        private Transform Rig()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            {
                return null;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return null;
                }
            }

            if (_rig == null)
            {
                _rig = _camera.transform.root;
                _rigHome = new Pose(_rig.position, _rig.rotation);
                _orbitPitch = 0f;
            }

            return _rig;
        }

        /// <summary>
        /// The open content's centre; for a nebula centred on the viewer that is the camera, so the drag becomes a look-around.
        /// </summary>
        private Vector3 OrbitPivot()
        {
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director != null && director.TryGetViewPivot(out var pivot))
            {
                return pivot;
            }

            return _camera.transform.position + _camera.transform.forward * 2f;
        }

        private bool TryGetViewTransforms(out Transform pivot, out Transform entity)
        {
            var manager = GalaxyExplorerManager.Instance;
            pivot = manager != null ? manager.ViewPivotTransform : null;
            entity = manager != null ? manager.ViewEntityTransform : null;
            if (pivot == null || entity == null)
            {
                return false;
            }

            if (!_viewDefaultsStored)
            {
                _pivotDefault = new Pose(pivot.localPosition, pivot.localRotation);
                _entityDefault = new Pose(entity.localPosition, entity.localRotation);
                _viewDefaultsStored = true;
            }
            return true;
        }
    }
}
