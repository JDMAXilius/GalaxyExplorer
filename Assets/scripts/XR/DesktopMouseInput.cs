// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// The desktop mouse as a far pointer through the same routing the hands use. Empty-space drags and the wheel
    /// go to <see cref="DesktopCameraRig"/>; the keys are <see cref="DesktopShortcuts"/> (CS-349).
    /// </summary>
    [RequireComponent(typeof(DesktopCameraRig))]
    public class DesktopMouseInput : MonoBehaviour
    {
        private const float RaycastDistance = 100f;
        private const float DragThresholdPixels = 4f;

        [SerializeField] private float planetSpinDegreesPerScreen = 360f;
        [SerializeField] private float planetScalePerNotch = 1.1f;
        [Tooltip("Wheel scale limits, relative to the size a planet grows to when pulled.")]
        [SerializeField] private float minPlanetScale = 0.1f;
        [SerializeField] private float maxPlanetScale = 3f;

        private Camera _camera;
        private DesktopCameraRig _view;
        private Transform _grabPoint;
        private GEPointer _pointer;

        private GEInteractable _hovered;
        private GEInteractable _pressed;
        private ForceSolver _spinning;
        private Transform _spinningHost;
        private float _grabDistance;
        private Vector2 _leftPressPosition, _rightPressPosition;
        private bool _orbiting, _panning;

        /// <summary>Left button state last frame, so a press is detected even when wasPressedThisFrame is missed.</summary>
        private bool _leftWasDown;

        public static DesktopMouseInput Instance { get; private set; }

        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            Instance = this;
            _view = GetComponent<DesktopCameraRig>();
            _grabPoint = new GameObject("Desktop Mouse Grab Point").transform;
            _grabPoint.SetParent(transform, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
                _pointer = GEPointer.CreateMouse(_grabPoint, _camera.transform);
            }

            var screenPosition = mouse.position.ReadValue();
            var ray = _camera.ScreenPointToRay(screenPosition);

            if (!InputEnabled)
            {
                CancelInteraction();
                return;
            }

            // A release we never saw (outside the Game view, swallowed by uGUI) would leave drag state stuck and
            // block every later interaction; the button, not our bookkeeping, is the authority.
            ReconcileButtons(mouse);

            UpdateGrabPoint(ray);

            // Screen-space only: world-space prefabs carry GraphicRaycasters too, and standing down over them
            // would kill the hover and click this method exists to deliver.
            var overUI = CosmicSimulation.UiEventSystemInstaller.IsPointerOverScreenSpaceUi(screenPosition);
            var hit = !overUI && Physics.Raycast(ray, out var hitInfo, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                ? hitInfo
                : (RaycastHit?)null;
            // GetComponentInParent: several interactables put their collider on a child (a destination tag's pill),
            // as XRI allows for the hands.
            var target = hit.HasValue ? hit.Value.collider.GetComponentInParent<GEInteractable>() : null;

            if (_pressed == null && !_orbiting)
            {
                SetHovered(target);
            }

            HandleLeftButton(mouse, screenPosition, hit, target);
            HandleRightButton(mouse, screenPosition, target);
            HandleWheel(mouse, target);
        }

        private void UpdateGrabPoint(Ray ray)
        {
            // The manipulation handler follows this point, so a dragged planet tracks the cursor.
            _grabPoint.SetPositionAndRotation(ray.GetPoint(_grabDistance), _camera.transform.rotation);
        }

        private void SetHovered(GEInteractable target)
        {
            if (target == _hovered)
            {
                return;
            }

            if (_hovered != null && _pointer.Focused.Remove(_hovered))
            {
                _hovered.RaiseFocusExit(_pointer);
            }

            _hovered = target;
            if (_hovered != null && _pointer.Focused.Add(_hovered))
            {
                _hovered.RaiseFocusEnter(_pointer);
            }
        }

        private void HandleLeftButton(Mouse mouse, Vector2 screenPosition, RaycastHit? hit, GEInteractable target)
        {
            var button = mouse.leftButton;

            // A press is also "down now, up last frame": wasPressedThisFrame is a one-frame edge that a hitch or
            // queued synthetic input can skip, and a missed edge leaves _leftPressPosition stale and starts an orbit.
            var pressedNow = button.isPressed && !_leftWasDown;
            var pressEdge = button.wasPressedThisFrame || pressedNow;
            _leftWasDown = button.isPressed;

            if (pressEdge)
            {
                _leftPressPosition = screenPosition;
                if (target != null)
                {
                    _grabDistance = hit.Value.distance;
                    UpdateGrabPoint(_camera.ScreenPointToRay(screenPosition));
                    _pressed = target;
                    _pressed.RaisePointerDown(_pointer);
                }
                else if (hit == null && !IsOverUI())
                {
                    GEInputEvents.RaiseGlobalPointerDown(new GEPointerEventData(_pointer));
                }
            }

            if (button.isPressed && _pressed == null && !_orbiting && !IsOverUI() &&
                (screenPosition - _leftPressPosition).magnitude > DragThresholdPixels && !pressEdge)
            {
                _orbiting = true;
            }

            if (_orbiting && button.isPressed)
            {
                _view.Orbit(mouse.delta.ReadValue());
            }

            if (button.wasReleasedThisFrame)
            {
                _orbiting = false;
                if (_pressed != null)
                {
                    var pressed = _pressed;
                    _pressed = null;
                    pressed.RaisePointerUp(_pointer, true);
                }
            }
        }

        private void HandleRightButton(Mouse mouse, Vector2 screenPosition, GEInteractable target)
        {
            var button = mouse.rightButton;
            if (button.wasPressedThisFrame)
            {
                _rightPressPosition = screenPosition;
                var solver = target != null ? target.GetComponentInParent<ForceSolver>() : null;
                _spinning = solver != null && solver.ForceState != ForceSolver.State.Root ? solver : null;

                // Two-handed content (nebula, Cosmic Web) has no ForceSolver; this is its desktop rotation.
                _spinningHost = _spinning == null ? ManipulatedHost(target) : null;
                _panning = _spinning == null && _spinningHost == null && !IsOverUI();
            }

            if (button.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                if (_spinning != null)
                {
                    Spin(_spinning.transform, delta);
                }
                else if (_spinningHost != null)
                {
                    Spin(_spinningHost, delta);
                }
                else if (_panning)
                {
                    _view.Pan(delta);
                }
            }

            if (button.wasReleasedThisFrame)
            {
                _spinning = null;
                _spinningHost = null;
                _panning = false;
            }
        }

        private void HandleWheel(Mouse mouse, GEInteractable target)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f || IsOverUI())
            {
                return;
            }

            // Depending on Input System settings a wheel notch reads as 1 or as 120 (raw Windows value).
            var notches = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
            var solver = target != null ? target.GetComponentInParent<ForceSolver>() : null;
            if (solver != null && solver.ForceState != ForceSolver.State.Root)
            {
            // Bodies differ a lot in base scale (the Moon's is small), so clamp relative to the pulled size.
                var pulledScale = solver is PlanetForceSolver planet ? planet.EditScale : 1f;
                var scale = solver.transform.localScale.x * Mathf.Pow(planetScalePerNotch, notches);
                solver.transform.localScale = Vector3.one * Mathf.Clamp(scale, minPlanetScale * pulledScale, maxPlanetScale * pulledScale);
            }
            else if (solver == null && ManipulatedHandler(target) is ManipulationHandler handler)
            {
                // The object's own ScaleLimits, not the planet limits above.
                var host = handler.HostTransform;
                host.localScale = handler.ClampScale(host.localScale * Mathf.Pow(planetScalePerNotch, notches));
            }
            else
            {
                _view.Zoom(notches);
            }
        }

        /// <summary>
        /// The two-handed <see cref="ManipulationHandler"/> above an interactable no force solver pulls; right-drag and wheel stand in for it.
        /// </summary>
        private static ManipulationHandler ManipulatedHandler(GEInteractable target)
        {
            if (target == null || target.GetComponentInParent<ForceSolver>() != null)
            {
                return null;
            }

            var handler = target.GetComponentInParent<ManipulationHandler>();
            if (handler == null || !handler.isActiveAndEnabled)
            {
                return null;
            }

            // One-handed handles (the dock's drag bar) have no two-hand gesture to mirror; a right-drag would roll the
            // dock unrecoverably and the wheel would resize its canvas unbounded, so those fall back to the view.
            return handler.ManipulationType == ManipulationHandler.HandMovementType.OneHandedOnly ? null : handler;
        }

        private static Transform ManipulatedHost(GEInteractable target)
        {
            var handler = ManipulatedHandler(target);
            return handler != null ? handler.HostTransform : null;
        }

        /// <summary>Ends any drag whose mouse button is no longer held, so a missed release cannot wedge input.</summary>
        private void ReconcileButtons(Mouse mouse)
        {
            // The wasReleasedThisFrame guard is essential: this runs before HandleLeftButton, and on a normal release
            // isPressed is already false, so without it every click would become a cancel.
            var leftLost = !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame;
            var rightLost = !mouse.rightButton.isPressed && !mouse.rightButton.wasReleasedThisFrame;

            if (_pressed != null && leftLost)
            {
                var pressed = _pressed;
                _pressed = null;

                // A cancel, not a click: we cannot know the release was over the same object.
                pressed.RaisePointerUp(_pointer, false);
            }

            if (_orbiting && leftLost)
            {
                _orbiting = false;
            }

            if ((_panning || _spinning != null || _spinningHost != null) && rightLost)
            {
                _panning = false;
                _spinning = null;
                _spinningHost = null;
            }
        }

        private void CancelInteraction()
        {
            if (_pressed != null)
            {
                var pressed = _pressed;
                _pressed = null;
                pressed.RaisePointerUp(_pointer, false);
            }

            SetHovered(null);
            _orbiting = _panning = false;
            _spinning = null;
        }

        private static bool IsOverUI()
        {
            return CosmicSimulation.UiEventSystemInstaller.IsPointerOverScreenSpaceUi();
        }

        private void Spin(Transform planet, Vector2 delta)
        {
            var cameraTransform = _camera.transform;
            var yaw = -delta.x / Screen.width * planetSpinDegreesPerScreen;
            var pitch = delta.y / Screen.height * planetSpinDegreesPerScreen;
            planet.rotation = Quaternion.AngleAxis(yaw, cameraTransform.up) * Quaternion.AngleAxis(pitch, cameraTransform.right) * planet.rotation;
        }
    }
}
