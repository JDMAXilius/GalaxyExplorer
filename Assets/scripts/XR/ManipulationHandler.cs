// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyExplorer.XR
{
    public class ManipulationEventData
    {
        public GameObject ManipulationSource { get; set; }
        public GEPointer Pointer { get; set; }
    }

    [Serializable]
    public class ManipulationEvent : UnityEvent<ManipulationEventData>
    {
    }

    /// <summary>
    /// Limits how far two hands may scale an object. Put one on the same GameObject as a
    /// <see cref="ManipulationHandler"/> and it is picked up automatically; without one, scaling is unbounded.
    /// </summary>
    public interface IManipulationScaleConstraint
    {
        Vector3 ClampScale(Vector3 desiredLocalScale);
    }

    /// <summary>
    /// One- and two-handed move/rotate/scale of <see cref="HostTransform"/> driven by the pointers forwarded to
    /// <see cref="OnPointerDown"/>/<see cref="OnPointerUp"/>. Replaces MRTK v2's ManipulationHandler; its serialized
    /// field names and enum values match so existing prefab settings carry over.
    /// </summary>
    public class ManipulationHandler : MonoBehaviour
    {
        public enum HandMovementType
        {
            OneHandedOnly = 0,
            TwoHandedOnly,
            OneAndTwoHanded
        }

        public enum TwoHandedManipulation
        {
            Scale,
            Rotate,
            MoveScale,
            MoveRotate,
            RotateScale,
            MoveRotateScale
        }

        [SerializeField]
        [Tooltip("Transform that will be dragged. Defaults to the object of the component.")]
        private Transform hostTransform = null;

        [SerializeField]
        [Tooltip("Can manipulation be done only with one hand, only with two hands, or with both?")]
        private HandMovementType manipulationType = HandMovementType.OneAndTwoHanded;

        [SerializeField]
        [Tooltip("What manipulation will two hands perform?")]
        private TwoHandedManipulation twoHandedManipulationType = TwoHandedManipulation.MoveRotateScale;

        [SerializeField]
        [Tooltip("Specifies whether manipulation can be done using far interaction with pointers.")]
        private bool allowFarManipulation = true;

        [SerializeField]
        [Tooltip("Smooth the object's motion toward the hands.")]
        private bool smoothingActive = true;

        [SerializeField]
        [Tooltip("Seconds to close most of the distance to the hands when smoothing.")]
        private float smoothingTime = 0.05f;

        [SerializeField]
        [Tooltip("Play the grab and release sounds from here. Leave off on anything a ForceSolver pulls — the " +
                 "solver already plays them as it enters and leaves its Manipulation state, and a second " +
                 "emitter would double them. Turn it on for things grabbed directly: the nebula overlays, the " +
                 "cosmic web, the dock's drag bar.")]
        private bool playGrabSounds = false;

        public ManipulationEvent OnManipulationStarted = new ManipulationEvent();
        public ManipulationEvent OnManipulationEnded = new ManipulationEvent();
        public ManipulationEvent OnHoverEntered = new ManipulationEvent();
        public ManipulationEvent OnHoverExited = new ManipulationEvent();

        private readonly List<GEPointer> _pointers = new List<GEPointer>();
        private IManipulationScaleConstraint _scaleConstraint;
        private bool _lookedForScaleConstraint;

        // One-handed grab: host pose relative to the grabbing pointer.
        private Vector3 _oneHandPositionOffset;
        private Quaternion _oneHandRotationOffset;

        // Two-handed grab: starting hand configuration and host pose.
        private Vector3 _twoHandStartMidpoint, _twoHandStartVector, _hostStartPosition, _hostStartScale;
        private Quaternion _hostStartRotation;

        public Transform HostTransform
        {
            get
            {
                if (hostTransform == null)
                {
                    hostTransform = transform;
                }

                return hostTransform;
            }

            // Settable because a grab handle is not the thing being moved: the dock's drag bar drags the dock
            // root (GDD 8.1), and the committed dock prefab predates that, so DockController points this at the
            // dock on its way up rather than waiting for the UI prefab builder to be re-run. Re-captures the
            // grab offsets if a hand is already on it, because they were measured against the old host.
            set
            {
                if (hostTransform == value)
                {
                    return;
                }

                hostTransform = value;
                if (_pointers.Count > 0)
                {
                    CaptureGrabState();
                }
            }
        }

        /// <summary>
        /// How many hands may drive this handler. Settable for the same reason <see cref="HostTransform"/> is,
        /// and readable because the desktop mirror has to know: right-drag and the wheel stand in for the
        /// <i>two-handed</i> turn and scale, so a one-handed handle has nothing there to mirror.
        /// </summary>
        public HandMovementType ManipulationType
        {
            get => manipulationType;
            set => manipulationType = value;
        }

        public bool IsManipulating => _pointers.Count > 0;

        /// <summary>
        /// Applies this object's scale constraint, if it has one, to a scale someone else worked out. Desktop
        /// mode scales with the wheel rather than with two hands, and must land inside the same limits.
        /// </summary>
        public Vector3 ClampScale(Vector3 desiredLocalScale) =>
            ScaleConstraint()?.ClampScale(desiredLocalScale) ?? desiredLocalScale;

        public void OnPointerDown(GEPointerEventData eventData)
        {
            var pointer = eventData?.Pointer;
            if (!enabled || pointer == null || _pointers.Contains(pointer))
            {
                return;
            }

            if (!allowFarManipulation && !eventData.IsNear)
            {
                return;
            }

            if (manipulationType == HandMovementType.OneHandedOnly && _pointers.Count >= 1)
            {
                return;
            }

            _pointers.Add(pointer);
            if (_pointers.Count == 1)
            {
                if (playGrabSounds)
                {
                    AudioService.Instance?.PlayClip(AudioId.ManipulationStart);
                }

                OnManipulationStarted.Invoke(new ManipulationEventData { ManipulationSource = gameObject, Pointer = pointer });
            }

            CaptureGrabState();
            eventData.Use();
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
            var pointer = eventData?.Pointer;
            if (pointer == null || !_pointers.Remove(pointer))
            {
                return;
            }

            if (_pointers.Count == 0)
            {
                if (playGrabSounds)
                {
                    AudioService.Instance?.PlayClip(AudioId.ManipulationEnd);
                }

                OnManipulationEnded.Invoke(new ManipulationEventData { ManipulationSource = gameObject, Pointer = pointer });
            }
            else
            {
                CaptureGrabState();
            }

            eventData.Use();
        }

        private void OnDisable()
        {
            // Owners disable the handler when manipulation should stop; drop pointers without raising events.
            _pointers.Clear();
        }

        private void Update()
        {
            // A pointer can vanish without an OnPointerUp: a hand leaves tracking, a ray loses its target,
            // the mouse button is released over something that ate the event, or the pointer object is
            // destroyed. Pruning it silently was a real bug - the grab offsets stayed captured for a grab
            // that no longer existed, so the object jumped or stuck the moment the remaining pointer moved,
            // and OnManipulationEnded never fired, so every listener went on believing a manipulation was
            // still running. That is the "it works at first, then goes buggy once you use everything back
            // and forth" failure: the stale state accumulates across interactions rather than resetting.
            //
            // So a silent loss now ends exactly as a clean release does.
            var before = _pointers.Count;
            _pointers.RemoveAll(p => p == null || !p.IsActive);
            var lost = before - _pointers.Count;

            if (lost > 0)
            {
                if (_pointers.Count == 0)
                {
                    if (playGrabSounds)
                    {
                        AudioService.Instance?.PlayClip(AudioId.ManipulationEnd);
                    }

                    // Pointer is null because the one that went away is exactly what we no longer have.
                    // Listeners that match on ManipulationSource still work; those that match on Pointer
                    // have to tolerate null, which is the honest report of "we do not know which".
                    OnManipulationEnded.Invoke(
                        new ManipulationEventData { ManipulationSource = gameObject, Pointer = null });
                }
                else
                {
                    // Still held, but by fewer hands than the offsets were captured for. Re-capture, or the
                    // object snaps as the two-handed midpoint collapses onto one pointer.
                    CaptureGrabState();
                }
            }

            if (_pointers.Count == 0)
            {
                return;
            }

            var host = HostTransform;
            Vector3 targetPosition;
            Quaternion targetRotation;
            Vector3 targetScale = host.localScale;

            if (_pointers.Count >= 2 && manipulationType != HandMovementType.OneHandedOnly)
            {
                var a = _pointers[0].AttachTransform.position;
                var b = _pointers[1].AttachTransform.position;
                var midpoint = (a + b) * 0.5f;
                var handVector = b - a;

                bool move = twoHandedManipulationType == TwoHandedManipulation.MoveScale ||
                            twoHandedManipulationType == TwoHandedManipulation.MoveRotate ||
                            twoHandedManipulationType == TwoHandedManipulation.MoveRotateScale;
                bool rotate = twoHandedManipulationType == TwoHandedManipulation.Rotate ||
                              twoHandedManipulationType == TwoHandedManipulation.MoveRotate ||
                              twoHandedManipulationType == TwoHandedManipulation.RotateScale ||
                              twoHandedManipulationType == TwoHandedManipulation.MoveRotateScale;
                bool scale = twoHandedManipulationType == TwoHandedManipulation.Scale ||
                             twoHandedManipulationType == TwoHandedManipulation.MoveScale ||
                             twoHandedManipulationType == TwoHandedManipulation.RotateScale ||
                             twoHandedManipulationType == TwoHandedManipulation.MoveRotateScale;

                var deltaRotation = rotate ? Quaternion.FromToRotation(_twoHandStartVector, handVector) : Quaternion.identity;
                targetRotation = deltaRotation * _hostStartRotation;

                // Keep the host where it was relative to the hands' midpoint, rotated with the hands.
                var startOffset = _hostStartPosition - _twoHandStartMidpoint;
                targetPosition = (move ? midpoint : _twoHandStartMidpoint) + deltaRotation * startOffset;

                if (scale && _twoHandStartVector.sqrMagnitude > 1e-6f)
                {
                    targetScale = _hostStartScale * (handVector.magnitude / _twoHandStartVector.magnitude);
                    targetScale = ScaleConstraint()?.ClampScale(targetScale) ?? targetScale;
                }
            }
            else if (manipulationType != HandMovementType.TwoHandedOnly)
            {
                var pointerTransform = _pointers[0].AttachTransform;
                targetRotation = pointerTransform.rotation * _oneHandRotationOffset;
                targetPosition = pointerTransform.TransformPoint(_oneHandPositionOffset);
            }
            else
            {
                return;
            }

            if (smoothingActive && smoothingTime > 0f)
            {
                var t = 1f - Mathf.Exp(-Time.deltaTime / smoothingTime);
                host.position = Vector3.Lerp(host.position, targetPosition, t);
                host.rotation = Quaternion.Slerp(host.rotation, targetRotation, t);
                host.localScale = Vector3.Lerp(host.localScale, targetScale, t);
            }
            else
            {
                host.SetPositionAndRotation(targetPosition, targetRotation);
                host.localScale = targetScale;
            }
        }

        // Resolved once and cached, including the "there isn't one" answer: this runs every frame of a two-hand
        // grab, and GetComponent on an interface is not free.
        private IManipulationScaleConstraint ScaleConstraint()
        {
            if (!_lookedForScaleConstraint)
            {
                _scaleConstraint = GetComponent<IManipulationScaleConstraint>();
                _lookedForScaleConstraint = true;
            }

            return _scaleConstraint;
        }

        private void CaptureGrabState()
        {
            var host = HostTransform;
            if (_pointers.Count >= 2)
            {
                var a = _pointers[0].AttachTransform.position;
                var b = _pointers[1].AttachTransform.position;
                _twoHandStartMidpoint = (a + b) * 0.5f;
                _twoHandStartVector = b - a;
                _hostStartPosition = host.position;
                _hostStartRotation = host.rotation;
                _hostStartScale = host.localScale;
            }
            else if (_pointers.Count == 1)
            {
                var pointerTransform = _pointers[0].AttachTransform;
                _oneHandPositionOffset = pointerTransform.InverseTransformPoint(host.position);
                _oneHandRotationOffset = Quaternion.Inverse(pointerTransform.rotation) * host.rotation;
            }
        }
    }
}
