// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Lives on the XR Origin. Exposes the tracked controllers and hands to the app (palm poses for hand menus
    /// and force-pull targets, which input mode is active) and raises <see cref="GEInputEvents.GlobalPointerDown"/>
    /// for select presses that don't hit anything, which the card system uses to close open cards.
    /// </summary>
    public class XRInputRig : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Parent of the tracked devices (XR Origin's Camera Offset). XR Hands joint poses are relative to it.")]
        private Transform trackingSpace = null;

        [SerializeField]
        private Transform leftController = null;

        [SerializeField]
        private Transform rightController = null;

        [SerializeField]
        [Tooltip("Interactors whose select presses count as global clicks (the near-far interactors of both hands and controllers).")]
        private List<XRBaseInputInteractor> selectInteractors = new List<XRBaseInputInteractor>();

        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        private readonly List<IXRInteractor> _registered = new List<IXRInteractor>();

        // Select presses that hit nothing and are still held, and the handler they are driving, if any.
        private readonly List<XRBaseInputInteractor> _airPinches = new List<XRBaseInputInteractor>();
        private ManipulationHandler _airHandler;

        public static XRInputRig Instance { get; private set; }

        public Transform LeftControllerTransform => leftController;

        public Transform RightControllerTransform => rightController;

        /// <summary>Palm joint of the left hand, updated every frame while the hand is tracked.</summary>
        public Transform LeftPalm { get; private set; }

        /// <summary>Palm joint of the right hand, updated every frame while the hand is tracked.</summary>
        public Transform RightPalm { get; private set; }

        public bool LeftHandTracked { get; private set; }

        public bool RightHandTracked { get; private set; }

        public bool LeftControllerTracked => IsControllerTracked(leftController);

        public bool RightControllerTracked => IsControllerTracked(rightController);

        /// <summary>True when the user is interacting with tracked hands rather than controllers.</summary>
        public bool IsHandTrackingActive => LeftHandTracked || RightHandTracked ||
                                            XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.TrackedHand;

        private void Awake()
        {
            Instance = this;
            if (trackingSpace == null)
            {
                trackingSpace = transform;
            }

            LeftPalm = CreatePalm("Left Palm");
            RightPalm = CreatePalm("Right Palm");

            if (!XRSettings.isDeviceActive)
            {
                DisableTrackedDevices();
            }
        }

        // Desktop (no headset): no hands or controllers will ever be tracked, so switch their interactors off.
        private void DisableTrackedDevices()
        {
            var modalityManager = GetComponent<XRInputModalityManager>();
            if (modalityManager == null)
            {
                return;
            }

            foreach (var device in new[] { modalityManager.leftHand, modalityManager.rightHand, modalityManager.leftController, modalityManager.rightController })
            {
                if (device != null)
                {
                    device.SetActive(false);
                }
            }

            modalityManager.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private Transform CreatePalm(string palmName)
        {
            var palm = new GameObject(palmName).transform;
            palm.SetParent(trackingSpace, false);
            return palm;
        }

        private static bool IsControllerTracked(Transform controller)
        {
            // The modality manager only activates controller objects while controllers are the input mode.
            return controller != null && controller.gameObject.activeInHierarchy &&
                   XRInputModalityManager.currentInputMode.Value == XRInputModalityManager.InputMode.MotionController;
        }

        private void Update()
        {
            UpdateHands();
            RaiseGlobalClicks();
        }

        private void UpdateHands()
        {
            if (_handSubsystem == null || !_handSubsystem.running)
            {
                _handSubsystem = null;
                SubsystemManager.GetSubsystems(_subsystems);
                foreach (var subsystem in _subsystems)
                {
                    if (subsystem.running)
                    {
                        _handSubsystem = subsystem;
                        break;
                    }
                }
            }

            var leftWasTracked = LeftHandTracked;
            var rightWasTracked = RightHandTracked;
            LeftHandTracked = UpdatePalm(_handSubsystem?.leftHand, LeftPalm);
            RightHandTracked = UpdatePalm(_handSubsystem?.rightHand, RightPalm);

            if (leftWasTracked && !LeftHandTracked) ReleaseHand(InteractorHandedness.Left);
            if (rightWasTracked && !RightHandTracked) ReleaseHand(InteractorHandedness.Right);
        }

        /// <summary>
        /// Tracking loss does not release a grab by itself. XRHandDevice leaves pinchTouched/graspFirm at their
        /// last value when a hand drops out (only the tracking fields reset), MetaAimHand.indexPressed trusts
        /// whatever pinch strength the runtime last reported, and XRInputModalityManager only listens for tracking
        /// *acquired*, so the hand's interactors stay registered with Select still performed. A pinch carried out
        /// of the cameras' view would hold the body until the hand came back. Cancel it where it is (CS-283).
        /// </summary>
        private void ReleaseHand(InteractorHandedness side)
        {
            XRInteractionManager manager = null;
            foreach (var interactor in selectInteractors)
            {
                if (interactor != null && interactor.interactionManager != null)
                {
                    manager = interactor.interactionManager;
                    break;
                }
            }

            // Its air pinch too: the select input keeps its last value when the hand drops out.
            for (var i = _airPinches.Count - 1; i >= 0; i--)
            {
                if (_airPinches[i] == null || _airPinches[i].handedness == side)
                {
                    ReleaseAirPinch(i);
                }
            }

            if (manager == null)
            {
                return;
            }

            manager.GetRegisteredInteractors(_registered);
            foreach (var interactor in _registered)
            {
                if (interactor.handedness == side && interactor is IXRSelectInteractor selector && selector.hasSelection)
                {
                    manager.CancelInteractorSelection(selector);
                }
            }
        }

        /// <summary>
        /// The direction the palm of a tracked hand faces, in world space, measured from where the joints are
        /// rather than read off the palm joint's rotation: the normal of the triangle wrist, index knuckle,
        /// little knuckle. Which of the palm joint's axes points out of the palm is a convention (in OpenXR +Y
        /// is the back of the hand) and a wrong guess there turns a palm test inside out; three positions have
        /// no convention to get wrong. The knuckles rather than the metacarpal bases, which sit within a couple
        /// of centimetres of the wrist and make a triangle too small to be steady.
        /// </summary>
        public bool TryGetPalmNormal(bool leftHand, out Vector3 normal)
        {
            normal = default;
            if (_handSubsystem == null || !(leftHand ? LeftHandTracked : RightHandTracked))
            {
                return false;
            }

            var hand = leftHand ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            if (!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist) ||
                !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var index) ||
                !hand.GetJoint(XRHandJointID.LittleProximal).TryGetPose(out var little))
            {
                return false;
            }

            // The two hands are mirror images, so the winding that points out of one palm points out of the
            // back of the other.
            var toIndex = index.position - wrist.position;
            var toLittle = little.position - wrist.position;
            var cross = leftHand ? Vector3.Cross(toIndex, toLittle) : Vector3.Cross(toLittle, toIndex);
            if (cross.sqrMagnitude < 1e-10f)
            {
                return false;
            }

            // Joint poses are relative to the tracking space, like the palm poses above.
            normal = trackingSpace.TransformDirection(cross).normalized;
            return true;
        }

        private static bool UpdatePalm(XRHand? hand, Transform palm)
        {
            if (!hand.HasValue || !hand.Value.isTracked)
            {
                return false;
            }

            if (hand.Value.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose))
            {
                palm.localPosition = pose.position;
                palm.localRotation = pose.rotation;
            }

            return true;
        }

        /// <summary>
        /// How far the four fingers of a tracked hand are curled, 0 flat to 1 fist, averaged. XR Hands' own
        /// measure (<c>XRFingerShapeMath</c>, in the package runtime; the Gestures sample is not needed).
        /// </summary>
        public bool TryGetFingerCurl(bool leftHand, out float curl)
        {
            curl = 0f;
            if (_handSubsystem == null || !(leftHand ? LeftHandTracked : RightHandTracked))
            {
                return false;
            }

            var hand = leftHand ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            for (var finger = XRHandFingerID.Index; finger <= XRHandFingerID.Little; finger++)
            {
                if (!hand.CalculateFingerShape(finger, XRFingerShapeTypes.FullCurl).TryGetFullCurl(out var fullCurl))
                {
                    return false;
                }

                curl += fullCurl * 0.25f;
            }

            return true;
        }

        private void RaiseGlobalClicks()
        {
            // A pinch in the air has no interactable to report its release, so it is watched here.
            for (var i = _airPinches.Count - 1; i >= 0; i--)
            {
                var interactor = _airPinches[i];
                if (interactor == null || !interactor.isActiveAndEnabled || !interactor.selectInput.ReadIsPerformed())
                {
                    ReleaseAirPinch(i);
                }
            }

            foreach (var interactor in selectInteractors)
            {
                if (interactor == null || !interactor.isActiveAndEnabled || interactor.hasSelection)
                {
                    continue;
                }

                if (interactor.selectInput.ReadWasPerformedThisFrame() && !TakeAirPinch(interactor))
                {
                    GEInputEvents.RaiseGlobalPointerDown(new GEPointerEventData(GEPointer.For(interactor)));
                }
            }
        }

        /// <summary>
        /// A pinch that hit nothing. While the other hand holds something it joins that grab as the second
        /// pointer; with nothing held, the second of two takes hold of the open place. Anything else is the
        /// global click it always was (false).
        /// </summary>
        private bool TakeAirPinch(XRBaseInputInteractor interactor)
        {
            if (!_airPinches.Contains(interactor))
            {
                _airPinches.Add(interactor);
            }

            if (_airHandler != null && !_airHandler.IsManipulating)
            {
                _airHandler = null;
            }

            var pointer = GEPointer.For(interactor);
            var held = ManipulationHandler.Held;
            if (held != null)
            {
                if (!held.AddAirPointer(pointer))
                {
                    return false;
                }

                _airHandler = held;
                return true;
            }

            var place = ManipulationHandler.Place;
            if (place == null || _airPinches.Count != 2 ||
                !place.BeginAirManipulation(GEPointer.For(_airPinches[0]), pointer))
            {
                return false;
            }

            _airHandler = place;
            return true;
        }

        private void ReleaseAirPinch(int index)
        {
            var interactor = _airPinches[index];
            _airPinches.RemoveAt(index);
            if (interactor != null && _airHandler != null)
            {
                // Ignored by the handler unless it holds this pointer.
                _airHandler.OnPointerUp(new GEPointerEventData(GEPointer.For(interactor)));
            }
        }
    }
}
