// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Mouse and keyboard input for desktop mode (no headset). The mouse acts as a far pointer through the same
    /// focus/pointer routing the Quest hands use, so hovering, clicking and dragging reach the same handlers:
    ///
    /// - Hover: markers and planets highlight and play their focus sound.
    /// - Left click: open cards, change views, press buttons; a planet is pulled in front of the camera.
    /// - Left drag on a pulled planet: move it. Right drag on it: spin it. Wheel over it: scale it.
    /// - Left drag on empty space: orbit the view. Right drag: pan. Wheel: zoom. Home: reset the view.
    /// - Backspace: back. R: reset planets. Esc: close the open card.
    /// </summary>
    public class DesktopMouseInput : MonoBehaviour
    {
        private const float RaycastDistance = 100f;
        private const float DragThresholdPixels = 4f;

        [SerializeField] private float orbitDegreesPerScreen = 200f;
        [SerializeField] private float panMetersPerScreen = 1.5f;
        [SerializeField] private float zoomMetersPerNotch = 0.1f;
        [SerializeField] private float planetSpinDegreesPerScreen = 360f;
        [SerializeField] private float planetScalePerNotch = 1.1f;
        [Tooltip("Wheel scale limits, relative to the size a planet grows to when pulled.")]
        [SerializeField] private float minPlanetScale = 0.1f;
        [SerializeField] private float maxPlanetScale = 3f;

        private Camera _camera;
        private Transform _grabPoint;
        private GEPointer _pointer;

        private GEInteractable _hovered;
        private GEInteractable _pressed;
        private ForceSolver _spinning;
        private float _grabDistance;
        private Vector2 _leftPressPosition, _rightPressPosition;
        private bool _orbiting, _panning;

        // Content pose at startup, restored by Home.
        private bool _viewDefaultsStored;
        private Pose _pivotDefault, _entityDefault;

        public static DesktopMouseInput Instance { get; private set; }

        /// <summary>Input is paused while scenes transition.</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            Instance = this;
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

            HandleKeyboard();

            var screenPosition = mouse.position.ReadValue();
            var ray = _camera.ScreenPointToRay(screenPosition);

            if (!InputEnabled)
            {
                CancelInteraction();
                return;
            }

            UpdateGrabPoint(ray);

            // The desktop menu is uGUI: leave the mouse to it when the cursor is over it.
            var overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var hit = !overUI && Physics.Raycast(ray, out var hitInfo, RaycastDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                ? hitInfo
                : (RaycastHit?)null;
            var target = hit.HasValue ? hit.Value.collider.GetComponent<GEInteractable>() : null;

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
            // The grab point rides the mouse ray at the grab distance, facing like the camera, so the
            // manipulation handler moves a dragged planet along with the cursor.
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
            if (button.wasPressedThisFrame)
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
                    // Clicking empty space counts as a global click (closes cards), then may start an orbit.
                    GEInputEvents.RaiseGlobalPointerDown(new GEPointerEventData(_pointer));
                }
            }

            if (button.isPressed && _pressed == null && !_orbiting && !IsOverUI() &&
                (screenPosition - _leftPressPosition).magnitude > DragThresholdPixels && !button.wasPressedThisFrame)
            {
                _orbiting = true;
            }

            if (_orbiting && button.isPressed)
            {
                Orbit(mouse.delta.ReadValue());
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
                // Spin a planet that has been pulled out; right-dragging anything else pans the view.
                var solver = target != null ? target.GetComponentInParent<ForceSolver>() : null;
                _spinning = solver != null && solver.ForceState != ForceSolver.State.Root ? solver : null;
                _panning = _spinning == null && !IsOverUI();
            }

            if (button.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                if (_spinning != null)
                {
                    Spin(_spinning.transform, delta);
                }
                else if (_panning)
                {
                    Pan(delta);
                }
            }

            if (button.wasReleasedThisFrame)
            {
                _spinning = null;
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
            else
            {
                Zoom(notches);
            }
        }

        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            var manager = GalaxyExplorerManager.Instance;
            if (keyboard == null || manager == null)
            {
                return;
            }

            var menu = FindAnyObjectByType<GlobalMenuManager>();
            if (keyboard.backspaceKey.wasPressedThisFrame && menu != null && menu.MenuIsAvailable && menu.BackButtonNeedsShowing)
            {
                menu.OnBackButtonPressed();
            }

            if (keyboard.rKey.wasPressedThisFrame && menu != null && menu.MenuIsAvailable && menu.ResetButtonNeedsShowing)
            {
                menu.OnResetButtonPressed();
            }

            if (keyboard.escapeKey.wasPressedThisFrame && manager.CardPoiManager != null)
            {
                manager.CardPoiManager.CloseAnyOpenCard();
            }

            if (keyboard.homeKey.wasPressedThisFrame)
            {
                ResetView();
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
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        // View controls move the same content transforms TouchScript's camera controller uses on touchscreens.
        private bool TryGetViewTransforms(out Transform pivot, out Transform entity)
        {
            var controller = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.CameraControllerHandler : null;
            pivot = controller != null ? controller.Pivot : null;
            entity = controller != null ? controller.EntityToMove : null;
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

        private void Orbit(Vector2 delta)
        {
            if (!TryGetViewTransforms(out var pivot, out _))
            {
                return;
            }

            var cameraTransform = _camera.transform;
            var yaw = delta.x / Screen.width * orbitDegreesPerScreen;
            var pitch = -delta.y / Screen.height * orbitDegreesPerScreen;
            pivot.rotation = Quaternion.AngleAxis(yaw, cameraTransform.up) * Quaternion.AngleAxis(pitch, cameraTransform.right) * pivot.rotation;
        }

        private void Pan(Vector2 delta)
        {
            if (!TryGetViewTransforms(out _, out var entity))
            {
                return;
            }

            var cameraTransform = _camera.transform;
            var move = (cameraTransform.right * delta.x + cameraTransform.up * delta.y) / Screen.height * panMetersPerScreen;
            entity.position += move;
        }

        private void Zoom(float notches)
        {
            if (!TryGetViewTransforms(out _, out var entity))
            {
                return;
            }

            // Move the content toward (wheel up) or away from the camera.
            entity.position -= _camera.transform.forward * (notches * zoomMetersPerNotch);
        }

        private void Spin(Transform planet, Vector2 delta)
        {
            var cameraTransform = _camera.transform;
            var yaw = -delta.x / Screen.width * planetSpinDegreesPerScreen;
            var pitch = delta.y / Screen.height * planetSpinDegreesPerScreen;
            planet.rotation = Quaternion.AngleAxis(yaw, cameraTransform.up) * Quaternion.AngleAxis(pitch, cameraTransform.right) * planet.rotation;
        }

        private void ResetView()
        {
            if (!TryGetViewTransforms(out var pivot, out var entity))
            {
                return;
            }

            entity.SetLocalPositionAndRotation(_entityDefault.position, _entityDefault.rotation);
            pivot.SetLocalPositionAndRotation(_pivotDefault.position, _pivotDefault.rotation);
        }
    }
}
