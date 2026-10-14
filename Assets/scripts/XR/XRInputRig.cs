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
    /// On the XR Origin: palm poses and input mode for the app, and <see cref="GEInputEvents.GlobalPointerDown"/>
    /// for select presses that hit nothing (open cards close on it).
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

        private float _nextSubsystemLook;

        private void UpdateHands()
        {
            // A stopped subsystem is let go at once, so its hands untrack this frame; a new one is looked for at most
            // once a second, because on the desktop there is never one and asking every frame allocates.
            if (_handSubsystem != null && !_handSubsystem.running)
            {
                _handSubsystem = null;
            }

            if (_handSubsystem == null && Time.unscaledTime >= _nextSubsystemLook)
            {
                _nextSubsystemLook = Time.unscaledTime + 1f;
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

            var dropped = false;
            if (leftWasTracked && !LeftHandTracked) dropped |= ReleaseHand(InteractorHandedness.Left);
            if (rightWasTracked && !RightHandTracked) dropped |= ReleaseHand(InteractorHandedness.Right);
            WatchForLostHands(dropped);
        }

        // ---------- "We cannot see your hands" (U2-P8, screen S36)
        // Said when a lost hand cancels a grab, or when nothing has been tracked for a few seconds; lowered hands
        // are normal, so the latter speaks once per loss and at most once a minute.

        private const string TrackingLost = "We cannot see your hands. Bring them into view to continue."; // docs/copy/ui.md tracking_lost
        private const float LostSeconds = 3f;
        private const float LostCooldownSeconds = 60f;
        private float _lostFor;
        private bool _lostSaid;
        private float _lostSaidAt = float.NegativeInfinity;

        private void WatchForLostHands(bool dropped)
        {
            if (!XRSettings.isDeviceActive)
            {
                return;
            }

            if (dropped)
            {
                SayLost();
            }

            var nothingSeen = !LeftHandTracked && !RightHandTracked && !LeftControllerTracked && !RightControllerTracked;
            if (!nothingSeen)
            {
                _lostFor = 0f;
                _lostSaid = false;
                return;
            }

            _lostFor += Time.unscaledDeltaTime;
            if (!_lostSaid && _lostFor >= LostSeconds && Time.unscaledTime - _lostSaidAt >= LostCooldownSeconds)
            {
                SayLost();
            }
        }

        private void SayLost()
        {
            _lostSaid = true;
            _lostSaidAt = Time.unscaledTime;
            CosmicSimulation.SwitchNotice.Say(TrackingLost);
        }

        /// <summary>
        /// Tracking loss does not end a grab by itself: the pinch inputs keep their last value and the modality
        /// manager only hears tracking acquired, so a pinch carried out of view would hold on (CS-283).
        /// </summary>
        /// <returns>True when the hand was holding or pinching something that had to be let go.</returns>
        private bool ReleaseHand(InteractorHandedness side)
        {
            var dropped = false;
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
                    dropped |= _airPinches[i] != null;
                    ReleaseAirPinch(i);
                }
            }

            if (manager == null)
            {
                return dropped;
            }

            manager.GetRegisteredInteractors(_registered);
            foreach (var interactor in _registered)
            {
                if (interactor.handedness == side && interactor is IXRSelectInteractor selector && selector.hasSelection)
                {
                    manager.CancelInteractorSelection(selector);
                    dropped = true;
                }
            }

            return dropped;
        }

        /// <summary>
        /// World palm normal from the wrist and index/little knuckle positions, which unlike the palm joint's axes have
        /// no convention to get inside out. Knuckles, as the metacarpal bases are too near the wrist to be steady.
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

        /// <summary>Mean curl of the four fingers, 0 flat to 1 fist, from XR Hands' runtime (no Gestures sample needed).</summary>
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
        /// A pinch that hit nothing joins a held grab as its second pointer or, as the second of two, takes the open
        /// place. False means it is a plain global click.
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
