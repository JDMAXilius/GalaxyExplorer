using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using MouseDevice = UnityEngine.InputSystem.Mouse;

namespace Cosmic
{
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_ScreenSpaceRayPoseDriver)]
    public class Mouse : XRRayInteractor
    {
        const float RawWheelNotch = 120f;

        [SerializeField] Transform pivot;
        [SerializeField] Hotkeys hotkeys;
        [SerializeField] float attachMetres = 1f;
        [SerializeField] float orbitDegreesPerScreen = 200f;
        [SerializeField] float panMetresPerScreen = 1.5f;
        [SerializeField] float zoomMetresPerNotch = 0.1f;
        [SerializeField] float spinDegreesPerScreen = 360f;
        [SerializeField] float scalePerNotch = 1.1f;
        [SerializeField] float pushMetresPerNotch = 0.08f;

        Camera cam;
        Grabbable spinning;
        Pose home;
        bool orbiting, homed, configured;

        public void ResetView()
        {
            if (!Anchored()) return;
            pivot.SetLocalPositionAndRotation(home.position, home.rotation);
        }

        protected override void Awake()
        {
            base.Awake();
            Configure();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (hotkeys != null) hotkeys.Recenter += ResetView;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hotkeys != null) hotkeys.Recenter -= ResetView;
        }

        void Update()
        {
            Configure();
            var mouse = MouseDevice.current;
            if (mouse == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            transform.SetPositionAndRotation(ray.origin, Quaternion.LookRotation(ray.direction, cam.transform.up));
            if (!hasSelection && attachTransform != null) attachTransform.localPosition = Vector3.forward * attachMetres;

            Left(mouse);
            Right(mouse);
            Wheel(mouse);

            var press = mouse.leftButton.isPressed && !orbiting;
            selectInput.QueueManualState(press, press ? 1f : 0f);
            uiPressInput.QueueManualState(press, press ? 1f : 0f);
        }

        void Configure()
        {
            if (configured) return;
            configured = true;
            // Manual readers because the press is a mouse button, not a device action; the block is XRI's own and tests canvas render mode, so world-space UI still takes the ray.
            selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            enableUIInteraction = true;
            blockInteractionsWithScreenSpaceUI = true;
            manipulateAttachTransform = false;
            if (attachTransform != null) attachTransform.localPosition = Vector3.forward * attachMetres;
        }

        void Left(MouseDevice mouse)
        {
            var button = mouse.leftButton;
            if (button.wasPressedThisFrame) orbiting = !hasHover && !hasSelection;
            else if (button.wasReleasedThisFrame) orbiting = false;
            if (orbiting && button.isPressed) Orbit(mouse.delta.ReadValue());
        }

        void Right(MouseDevice mouse)
        {
            var button = mouse.rightButton;
            if (button.wasPressedThisFrame) spinning = Target(true);
            else if (button.wasReleasedThisFrame) spinning = null;
            if (!button.isPressed) return;
            var delta = mouse.delta.ReadValue();
            if (spinning != null)
                spinning.Spin(new Vector2(-delta.x / Screen.width, delta.y / Screen.height) * spinDegreesPerScreen);
            else
                Pan(delta);
        }

        void Wheel(MouseDevice mouse)
        {
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            var notches = Mathf.Abs(scroll) > 10f ? scroll / RawWheelNotch : scroll;
            var held = Target(true);
            var keyboard = Keyboard.current;
            if (held == null) Zoom(notches);
            else if (keyboard != null && keyboard.shiftKey.isPressed) Push(notches);
            else held.ScaleBy(Mathf.Pow(scalePerNotch, notches));
        }

        Grabbable Target(bool includeHovered)
        {
            foreach (var selected in interactablesSelected)
                if (selected is Grabbable grabbable) return grabbable;
            if (!includeHovered) return null;
            foreach (var hovered in interactablesHovered)
                if (hovered is Grabbable target) return target;
            return null;
        }

        void Push(float notches)
        {
            var attach = attachTransform;
            if (attach == null) return;
            var origin = transform.position;
            var forward = transform.forward;
            var moved = attach.position + forward * (notches * pushMetresPerNotch);
            attach.position = Vector3.Dot(moved - origin, forward) > 0f ? moved : origin;
        }

        void Orbit(Vector2 delta)
        {
            if (!Anchored()) return;
            var view = cam.transform;
            var yaw = delta.x / Screen.width * orbitDegreesPerScreen;
            var pitch = -delta.y / Screen.height * orbitDegreesPerScreen;
            pivot.rotation = Quaternion.AngleAxis(yaw, view.up) * Quaternion.AngleAxis(pitch, view.right) * pivot.rotation;
        }

        void Pan(Vector2 delta)
        {
            if (!Anchored()) return;
            var view = cam.transform;
            pivot.position += (view.right * delta.x + view.up * delta.y) / Screen.height * panMetresPerScreen;
        }

        void Zoom(float notches)
        {
            if (!Anchored()) return;
            pivot.position -= cam.transform.forward * (notches * zoomMetresPerNotch);
        }

        bool Anchored()
        {
            if (pivot == null) return false;
            if (!homed)
            {
                home = new Pose(pivot.localPosition, pivot.localRotation);
                homed = true;
            }

            return true;
        }
    }
}
