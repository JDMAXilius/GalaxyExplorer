// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Desktop mouse and keyboard: the mouse is a far pointer through the same routing the hands use; keys follow GDD 5.3.
    /// </summary>
    // No Backspace: drill-down navigation is retired (roadmap 4.3); everything navigates through the dock.
    public class DesktopMouseInput : MonoBehaviour
    {
        private const float RaycastDistance = 100f;
        private const float DragThresholdPixels = 4f;

        private const float MenuLookupSeconds = 0.5f;

        [SerializeField] private float orbitDegreesPerScreen = 200f;
        [SerializeField] private float panMetersPerScreen = 1.5f;
        [SerializeField] private float zoomMetersPerNotch = 0.25f;
        [SerializeField] private float planetSpinDegreesPerScreen = 360f;
        [SerializeField] private float planetScalePerNotch = 1.1f;
        [Tooltip("Wheel scale limits, relative to the size a planet grows to when pulled.")]
        [SerializeField] private float minPlanetScale = 0.1f;
        [SerializeField] private float maxPlanetScale = 3f;

        // 1-9 and 0 are the Sun through Pluto, M is the Moon (GDD 5.3).
        private static readonly Key[] BodyKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.M,
        };

        // Index-matched with BodyKeys; values are BodyInfo.Id / LayoutRig.Body.Id.
        private static readonly string[] BodyIds =
        {
            "sun", "mercury", "venus", "earth", "mars",
            "jupiter", "saturn", "uranus", "neptune", "pluto", "moon",
        };

        private static readonly Key[] ExperienceKeys =
        {
            Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8,
        };

        private Camera _camera;
        private Transform _grabPoint;
        private DesktopMenuManager _desktopMenu;
        private float _menuLookupCooldown;
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

        private bool _viewDefaultsStored;
        private Pose _pivotDefault, _entityDefault;

        public static DesktopMouseInput Instance { get; private set; }

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
                    Pan(delta);
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
                Zoom(notches);
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

        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            FindDesktopMenu();

            // GDD 5.3 order. Not gated on GlobalMenuManager availability: that flag belongs to the retired intro flow.

            if (keyboard.rKey.wasPressedThisFrame && InputEnabled)
            {
                RestoreLayout();
            }

            if (keyboard.homeKey.wasPressedThisFrame)
            {
                Recenter();
            }

            if (keyboard.cKey.wasPressedThisFrame && InputEnabled)
            {
                CosmicSimulation.DockController.Instance?.ToggleBeing();
            }

            // P previews passthrough on desktop by toggling the dimming and backdrop. L toggles body and moon labels.
            if (keyboard.lKey.wasPressedThisFrame)
            {
                CosmicSimulation.MoonLabel.ShowLabels = !CosmicSimulation.MoonLabel.ShowLabels;
            }

            if (keyboard.pKey.wasPressedThisFrame && ExperienceModeManager.PassthroughAvailable)
            {
                CosmicSimulation.EnvironmentController.Instance?.TogglePassthrough();
            }

            // Tab belongs to DesktopDock; only without one does it fold the legacy row, so the two never fight.
            if (keyboard.tabKey.wasPressedThisFrame && CosmicSimulation.DesktopDock.Instance == null &&
                _desktopMenu != null)
            {
                _desktopMenu.OnToggleDesktopButtonVisibility();
            }

            // The controls overlay still lives on the legacy desktop HUD.
            if ((keyboard.hKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame) && _desktopMenu != null)
            {
                _desktopMenu.OnHelpButtonPressed();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseTopmost();
            }

            for (var i = 0; i < ExperienceKeys.Length; i++)
            {
                if (keyboard[ExperienceKeys[i]].wasPressedThisFrame)
                {
                    SwitchExperience(i);
                }
            }

            if (InputEnabled)
            {
                for (var slot = 0; slot < BodyKeys.Length; slot++)
                {
                    if (keyboard[BodyKeys[slot]].wasPressedThisFrame)
                    {
                        ToggleBody(slot);
                    }
                }
            }
        }

        // The legacy HUD's scene loads and unloads under us, so a cached reference goes stale; retry on a timer
        // rather than searching every frame for something that may not exist.
        private void FindDesktopMenu()
        {
            if (_desktopMenu != null)
            {
                return;
            }

            _menuLookupCooldown -= Time.unscaledDeltaTime;
            if (_menuLookupCooldown > 0f)
            {
                return;
            }

            _menuLookupCooldown = MenuLookupSeconds;
            _desktopMenu = FindAnyObjectByType<DesktopMenuManager>();
        }

        /// <summary>R and the dock's Restore: the current experience's objects return to their arrangement (GDD 5.2).</summary>
        private static void RestoreLayout()
        {
            // First and unconditional: non-body content has no solver or rig, and the rig branch returns (CS-107).
            CosmicSimulation.FreePlacementAnchor.RestoreAll();

            // The rig animates anchors and walks pulled bodies home together; don't snap bodies behind its back.
            var restored = false;
            foreach (var rig in CosmicSimulation.Registry<CosmicSimulation.LayoutRig>.Active)
            {
                rig.Restore();
                restored = true;
            }

            if (restored)
            {
                return;
            }

            // Pre-CS-060 experiences have no rig; each body's placement solver keeps the 0.8 s ease.
            foreach (var solver in CosmicSimulation.Registry<ForceSolver>.Active)
            {
                RestoreBody(solver);
            }
        }

        /// <summary>Home: the dock re-parks and the desktop camera goes back.</summary>
        private void Recenter()
        {
            // Through the dock so Home and its Recenter button cannot drift apart.
            var dock = CosmicSimulation.DesktopDock.Instance;
            if (dock != null)
            {
                dock.Recenter();
                return;
            }

            CosmicSimulation.DockController.Instance?.Recenter();
            ResetView();
        }

        /// <summary>Esc: closes the controls overlay alone if open, otherwise everything opened over the experience.</summary>
        private void CloseTopmost()
        {
            if (_desktopMenu != null && _desktopMenu.IsHelpVisible)
            {
                _desktopMenu.SetHelpVisible(false);
                return;
            }

            var closed = false;
            foreach (var popup in FindObjectsByType<CosmicSimulation.DockPopup>(FindObjectsSortMode.None))
            {
                if (popup.IsOpen)
                {
                    popup.Close();
                    closed = true;
                }
            }

            if (closed)
            {
                return;
            }

            // Before the panel sweep, which would hide the destination's panel and leave the nebula unnamed (GDD 4.3).
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director != null && director.HasOpenDestination)
            {
                director.ClearDestinations();
                return;
            }

            // Body panels close only with R; Hide() is a no-op on them and reaches destination and scene panels.
            foreach (var panel in CosmicSimulation.Registry<CosmicSimulation.InfoPanel>.Active)
            {
                panel.Hide();
            }

            // SwitchNotice handles Escape itself.

            var manager = GalaxyExplorerManager.Instance;
            if (manager != null && manager.CardPoiManager != null)
            {
                manager.CardPoiManager.CloseAnyOpenCard();
            }
        }

        // Counts only dock tiles, so destinations never take a function key.
        private static void SwitchExperience(int index)
        {
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director == null || director.IsSwitching)
            {
                return;
            }

            var seen = 0;
            foreach (var module in director.Modules)
            {
                if (module == null || module.Kind != CosmicSimulation.ExperienceKind.DockTile)
                {
                    continue;
                }

                if (seen++ == index)
                {
                    director.Switch(module);
                    return;
                }
            }
        }

        // Lookup order: the rig, the moon orbits, then the legacy preview-bar slots (pre-CS-060 view scenes).
        private static void ToggleBody(int slot)
        {
            var id = BodyIds[slot];

            foreach (var rig in CosmicSimulation.Registry<CosmicSimulation.LayoutRig>.Active)
            {
                var body = rig.Find(id);
                if (body != null && body.Force != null)
                {
                    ToggleBody(body.Force);
                    return;
                }
            }

            // A moon exists only once its planet is out; until then M correctly does nothing.
            foreach (var orbit in FindObjectsByType<CosmicSimulation.MoonOrbit>(FindObjectsSortMode.None))
            {
                if (orbit.Info != null && orbit.Info.Id == id && orbit.Solver != null)
                {
                    ToggleBody(orbit.Solver);
                    return;
                }
            }

            foreach (var target in FindObjectsByType<UiPreviewTarget>(FindObjectsSortMode.None))
            {
                if (target.slotId == slot && target.forceSolver != null)
                {
                    ToggleBody(target.forceSolver);
                    return;
                }
            }
        }

        private static void ToggleBody(ForceSolver solver)
        {
            if (solver.ForceState == ForceSolver.State.Root)
            {
                solver.OnPointerDown();
                return;
            }

            // Only this one goes back: many objects are out at once (GDD 5.2).
            RestoreBody(solver);
        }

        private static void RestoreBody(ForceSolver solver)
        {
            // The same path a tap on the body takes (CS-174), so a key and a click can never disagree.
            solver.ReturnHome();
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

        // ---------- the desktop view: the camera rig moves (CS-172)
        // Only the rig moves, never content, so experiences are untouched and headset sessions are left alone.

        private Transform _rig;
        private Pose _rigHome;
        private bool _rigHomeStored;
        private float _orbitPitch;

        [SerializeField]
        [Tooltip("How far the view may pitch up or down from its home, in degrees, so a drag cannot roll the " +
                 "camera over the top.")]
        private float maxPitchDegrees = 85f;

        /// <summary>The camera's rig root, which the desktop view moves. Null in a headset.</summary>
        private Transform Rig()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive || _camera == null)
            {
                return null;
            }

            if (_rig == null)
            {
                _rig = _camera.transform.root;
            }

            if (!_rigHomeStored)
            {
                _rigHome = new Pose(_rig.position, _rig.rotation);
                _rigHomeStored = true;
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

        private void Orbit(Vector2 delta)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            var cameraTransform = _camera.transform;
            var pivot = OrbitPivot();

            // FreeLook's convention, which the nebula scenes use.
            var yaw = delta.x / Screen.width * orbitDegreesPerScreen;
            var pitch = -delta.y / Screen.height * orbitDegreesPerScreen;
            var clamped = Mathf.Clamp(_orbitPitch + pitch, -maxPitchDegrees, maxPitchDegrees) - _orbitPitch;
            _orbitPitch += clamped;

            // Yaw about world up, then pitch about camera right: that pair never introduces roll.
            rig.RotateAround(pivot, Vector3.up, yaw);
            rig.RotateAround(pivot, cameraTransform.right, clamped);
        }

        private void Pan(Vector2 delta)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            var cameraTransform = _camera.transform;
            var move = (cameraTransform.right * delta.x + cameraTransform.up * delta.y) / Screen.height * panMetersPerScreen;
            rig.position -= move;
        }

        private void Zoom(float notches)
        {
            var rig = Rig();
            if (rig == null)
            {
                return;
            }

            rig.position += _camera.transform.forward * (notches * zoomMetersPerNotch);
        }

        private void Spin(Transform planet, Vector2 delta)
        {
            var cameraTransform = _camera.transform;
            var yaw = -delta.x / Screen.width * planetSpinDegreesPerScreen;
            var pitch = delta.y / Screen.height * planetSpinDegreesPerScreen;
            planet.rotation = Quaternion.AngleAxis(yaw, cameraTransform.up) * Quaternion.AngleAxis(pitch, cameraTransform.right) * planet.rotation;
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

            if (!TryGetViewTransforms(out var pivot, out var entity))
            {
                return;
            }

            entity.SetLocalPositionAndRotation(_entityDefault.position, _entityDefault.rotation);
            pivot.SetLocalPositionAndRotation(_pivotDefault.position, _pivotDefault.rotation);
        }
    }
}
