// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
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
    /// - Right drag on two-handed content (a nebula, the Cosmic Web): turn it. Wheel over it: scale it, inside
    ///   its own <c>ScaleLimits</c>. This is the desktop half of the GDD's two-hand rotate and scale.
    /// - Left drag on the world dock's drag bar: move the whole dock, upright and dock-sized (right drag and the
    ///   wheel stay on the view there — see <c>ManipulatedHandler</c>). The world dock parks below the
    ///   desktop frustum on purpose, so the dock a monitor player actually uses is <c>DesktopDock</c>, which is
    ///   pinned to the screen and has nothing to drag.
    /// - Left drag on empty space: orbit the view. Right drag: pan. Wheel: zoom.
    /// - R: restore the arrangement. Home: recenter. P: passthrough preview. Esc: close a panel or the overlay.
    /// - 1-9, 0: pull the Sun through Pluto (again to send that one back). M: the Moon.
    /// - Tab: show or hide the dock. H or F1: controls overlay. F2-F8: switch experience.
    ///
    /// The key map is GDD 5.3 and is meant to be read against it row by row. There is deliberately no Backspace:
    /// drill-down navigation is retired (roadmap 4.3), <c>TransitionManager</c>'s Back survives only as an
    /// internal transition helper, and everything the player navigates with now goes through the dock.
    /// </summary>
    public class DesktopMouseInput : MonoBehaviour
    {
        private const float RaycastDistance = 100f;
        private const float DragThresholdPixels = 4f;

        // Seconds between attempts to find a menu that is not there yet, or at all.
        private const float MenuLookupSeconds = 0.5f;

        [SerializeField] private float orbitDegreesPerScreen = 200f;
        [SerializeField] private float panMetersPerScreen = 1.5f;
        [SerializeField] private float zoomMetersPerNotch = 0.1f;
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

        // The same eleven bodies as BodyKeys, index for index - a key's position in one array is its body in the
        // other. These are BodyInfo.Id / LayoutRig.Body.Id keys; the legacy preview bar knew them as slot
        // numbers instead, which is why the index is what the two tables share.
        private static readonly string[] BodyIds =
        {
            "sun", "mercury", "venus", "earth", "mars",
            "jupiter", "saturn", "uranus", "neptune", "pluto", "moon",
        };

        // The seven dock tiles, in the order they appear on the dock.
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

            // Recover from a button release we never saw.
            //
            // CancelInteraction only ran when input was disabled, so any drag state that started but never
            // got its matching release stayed set for the rest of the session: press on an object and
            // release outside the Game view, or over a uGUI panel that swallowed the up-event, and _pressed
            // / _orbiting / _panning were still true afterwards. From then on the guards at the top of each
            // handler ("if _pressed == null && !_orbiting ...") refused every new interaction, which is
            // exactly the reported "works at first, then rotation and zoom get stuck once you have used
            // everything back and forth".
            //
            // The button is the authority, not our bookkeeping: if it is not down, the drag is over.
            ReconcileButtons(mouse);

            UpdateGrabPoint(ray);

            // The desktop menu and the desktop dock are uGUI: leave the mouse to them when the cursor is over
            // one. Deliberately a screen-space-only test - the world-space UI prefabs carry GraphicRaycasters
            // too, and standing down over a destination tag or an info panel would kill the very hover and
            // click this method exists to deliver.
            var overUI = CosmicSimulation.UiEventSystemInstaller.IsPointerOverScreenSpaceUi(screenPosition);
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

                // Content the hands turn with two hands rather than by pulling it out — a nebula overlay, the
                // Cosmic Web — has no ForceSolver, so without this it would have no desktop rotation at all.
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
                // The two-handed equivalent. The limits are the object's own (ScaleLimits), not the planet
                // ones above, so a nebula and the Cosmic Web stop where the GDD says they stop.
                var host = handler.HostTransform;
                host.localScale = handler.ClampScale(host.localScale * Mathf.Pow(planetScalePerNotch, notches));
            }
            else
            {
                Zoom(notches);
            }
        }

        /// <summary>
        /// The <see cref="ManipulationHandler"/> above an interactable that is not pulled by a force solver and
        /// that two hands could turn or scale — the pair of gestures the right-drag and the wheel stand in for.
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

            // A one-handed handle has no two-handed gesture to mirror, and mirroring one anyway would be worse
            // than nothing: the dock's drag bar moves the dock, so a right-drag would leave the whole dock
            // rolled over at an angle nothing brings back, and a wheel notch would resize its millimetre canvas
            // without limit. Both gestures fall back to panning and zooming the view, as they did before the bar
            // could be dragged at all. The left drag is unaffected — that arrives through GEInteractable.
            return handler.ManipulationType == ManipulationHandler.HandMovementType.OneHandedOnly ? null : handler;
        }

        /// <summary>The transform such a handler would move, or null.</summary>
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

            // GDD 5.3, in the order that table lists it. Nothing here asks GlobalMenuManager whether it is
            // "available": that flag is raised by the legacy ViewLoader intro flow, and a key map that only
            // works once a retired flow has finished is a key map that does not work.

            if (keyboard.rKey.wasPressedThisFrame && InputEnabled)
            {
                RestoreLayout();
            }

            if (keyboard.homeKey.wasPressedThisFrame)
            {
                Recenter();
            }

            // P previews on the desktop what the passthrough button does in the headset: there is no room to
            // show through a monitor, so this toggles the dimming and the black backdrop instead.
            // L hides and shows the name labels over every body and moon. The owner asked for the labels
            // to be something that can be turned off, and a key is the desktop half of that; the utility
            // window is where the headset half belongs.
            if (keyboard.lKey.wasPressedThisFrame)
            {
                CosmicSimulation.MoonLabel.ShowLabels = !CosmicSimulation.MoonLabel.ShowLabels;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                CosmicSimulation.EnvironmentController.Instance?.TogglePassthrough();
            }

            // Tab belongs to the dock, and DesktopDock reads it for itself. Only when there is no dock does Tab
            // fall back to folding the legacy button row away, so the two can never fight over one key.
            if (keyboard.tabKey.wasPressedThisFrame && CosmicSimulation.DesktopDock.Instance == null &&
                _desktopMenu != null)
            {
                _desktopMenu.OnToggleDesktopButtonVisibility();
            }

            // The controls overlay still lives on the legacy desktop HUD; DesktopMenuManager reopens its own
            // root for it, so this no longer depends on the rest of that HUD being on screen.
            if ((keyboard.hKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame) && _desktopMenu != null)
            {
                _desktopMenu.OnHelpButtonPressed();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseTopmost();
            }

            // F2 to F8 jump straight to an experience, in dock order.
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

        // The legacy HUD lives in a scene that is loaded and unloaded under us, so a reference taken once goes
        // stale - but a FindAnyObjectByType every frame for something that may simply not be in the scene is a
        // cost paid forever. Look again a couple of times a second while it is missing.
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

        /// <summary>
        /// R, and the dock's Restore. GDD 5.2: the current experience's objects go back to their arrangement,
        /// and nothing else moves.
        /// </summary>
        private static void RestoreLayout()
        {
            // First, and outside both branches below, because neither can reach it: content the player arranges
            // that is not a body has no ForceSolver and belongs to no rig — the nebula overlays, the Cosmic Web,
            // Andromeda — and the rig branch returns (CS-107).
            CosmicSimulation.FreePlacementAnchor.RestoreAll();

            // A rig knows the arrangement, animates the anchors, and walks anything the player pulled out home
            // over the same duration, so ask it rather than snapping bodies to their roots behind its back.
            var rigs = FindObjectsByType<CosmicSimulation.LayoutRig>(FindObjectsSortMode.None);
            if (rigs.Length > 0)
            {
                foreach (var rig in rigs)
                {
                    rig.Restore();
                }

                return;
            }

            // Experiences built before CS-060 have no rig. Restoring each body through its own placement solver
            // keeps the 0.8 s ease the GDD asks for; a body without one has nowhere to animate from and snaps.
            foreach (var solver in FindObjectsByType<ForceSolver>(FindObjectsSortMode.None))
            {
                RestoreBody(solver);
            }
        }

        /// <summary>Home. Both halves of the word: the dock re-parks, and the desktop camera goes back.</summary>
        private void Recenter()
        {
            // Through the dock when there is one, so Home and the dock's own Recenter button cannot drift apart.
            var dock = CosmicSimulation.DesktopDock.Instance;
            if (dock != null)
            {
                dock.Recenter();
                return;
            }

            CosmicSimulation.DockController.Instance?.Recenter();
            ResetView();
        }

        /// <summary>
        /// Esc: close the one thing that is in the way. The controls overlay covers the view, so it goes first
        /// and alone; otherwise anything that opened over the experience closes together.
        /// </summary>
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

            // A destination overlay covers the map it was opened from, so it is the next thing in the way
            // (GDD 4.3: "the overlay closes with Esc"). Above the panel sweep below, which would otherwise hide
            // the destination's own panel and leave the nebula sitting there with nothing naming it.
            var director = CosmicSimulation.ExperienceDirector.Instance;
            if (director != null && director.HasOpenDestination)
            {
                director.ClearDestinations();
                return;
            }

            // A body's panel is not closable on its own - it is open exactly while its body is out of the
            // arrangement, so R is what shuts it. Hide() only reaches the panels that are driven by hand, which
            // is the destination and scene panels, and is a no-op on the rest.
            foreach (var panel in FindObjectsByType<CosmicSimulation.InfoPanel>(FindObjectsSortMode.None))
            {
                panel.Hide();
            }

            // SwitchNotice dismisses itself on Escape, in its own Update. Deliberately not repeated here.

            var manager = GalaxyExplorerManager.Instance;
            if (manager != null && manager.CardPoiManager != null)
            {
                manager.CardPoiManager.CloseAnyOpenCard();
            }
        }

        // Opens the nth dock experience. Counts only the tiles, so destinations never take a function key.
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

        // Pulls one body out in front of the camera, or sends that one back. Three ways to find it, newest
        // first: the arrangement's rig, the moon orbits, and finally the legacy preview-bar slots, which are
        // all that the pre-CS-060 view scenes have.
        private static void ToggleBody(int slot)
        {
            var id = BodyIds[slot];

            foreach (var rig in FindObjectsByType<CosmicSimulation.LayoutRig>(FindObjectsSortMode.None))
            {
                var body = rig.Find(id);
                if (body != null && body.Force != null)
                {
                    ToggleBody(body.Force);
                    return;
                }
            }

            // A moon is not part of an arrangement: it rides its orbit and only exists once its planet is out,
            // so before that there is nothing for M to pull and the key correctly does nothing.
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

            // Only this one goes back. The old path here pressed the menu's Reset, which sent *every* planet
            // home - the opposite of GDD 5.2, where many objects are out at once and nothing the player did
            // not ask about moves.
            RestoreBody(solver);
        }

        private static void RestoreBody(ForceSolver solver)
        {
            var placement = solver.GetComponent<CosmicSimulation.FreePlacementSolver>();
            if (placement != null && placement.isActiveAndEnabled)
            {
                placement.RestoreLayout();
                return;
            }

            solver.ResetToRoot();
            solver.EnableForce = true;
        }

        /// <summary>
        /// Ends any drag whose mouse button is no longer held. Cheap, runs every frame, and is the only
        /// thing standing between a missed release and a session that stops responding.
        /// </summary>
        private void ReconcileButtons(Mouse mouse)
        {
            // wasReleasedThisFrame is the whole guard, and leaving it out cost every world-space click in the
            // app. On the frame a button comes up the Input System reports isPressed already false *and*
            // wasReleasedThisFrame true; this method runs before HandleLeftButton, so an unguarded
            // "!isPressed" matched every ordinary release, cleared _pressed and raised the up as a cancel.
            // HandleLeftButton then found nothing to release and never raised OnPointerClicked - so a mouse
            // click delivered a down and a cancel and nothing else, and every GEButton, every destination tag
            // and every POI card went silently dead to the mouse. The case this exists for is a release we
            // never saw at all, which is exactly the case where wasReleasedThisFrame is false.
            var leftLost = !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame;
            var rightLost = !mouse.rightButton.isPressed && !mouse.rightButton.wasReleasedThisFrame;

            if (_pressed != null && leftLost)
            {
                var pressed = _pressed;
                _pressed = null;

                // Released, but we cannot know it was over the same object, so this is a cancel rather than
                // a click: raising it as a click here would fire actions the player did not aim at.
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

        public void ResetView()
        {
            // Every desktop route to the word "Recenter" funnels through here — the Home key, the dock's button
            // (DesktopDock.Recenter) and the legacy HUD's Reset View — so it is the one place that can honour
            // contract F-13's "Recenter returns everything" for content no rig and no solver owns (CS-107).
            // Bodies are deliberately untouched: today Recenter does not move them, and this ticket does not
            // change that. In the headset the world dock's Recenter button goes to DockController.Recenter,
            // which needs the same line.
            CosmicSimulation.FreePlacementAnchor.RestoreAll();

            if (!TryGetViewTransforms(out var pivot, out var entity))
            {
                return;
            }

            entity.SetLocalPositionAndRotation(_entityDefault.position, _entityDefault.rotation);
            pivot.SetLocalPositionAndRotation(_pivotDefault.position, _pivotDefault.rotation);
        }
    }
}
