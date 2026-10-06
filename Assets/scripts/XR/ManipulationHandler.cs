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

        // Pointers that joined by pinching the air rather than by selecting this object (see AddAirPointer).
        private readonly List<GEPointer> _airPointers = new List<GEPointer>();
        private bool _enabledForAir;

        // Every handler with a hand on it, oldest first.
        private static readonly List<ManipulationHandler> Active = new List<ManipulationHandler>();

        // One-handed grab: how fast the hand itself is moving, smoothed, for the fling test on release (CS-290).
        // The interactor's own position, not the attach point: a far grab's attach point swings on a long lever.
        private const float VelocitySmoothingSeconds = 0.1f;
        private Vector3 _lastPointerPosition, _pointerVelocity;
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

        /// <summary>The object most recently taken hold of and still held, or null.</summary>
        public static ManipulationHandler Held
        {
            get
            {
                for (var i = Active.Count - 1; i >= 0; i--)
                {
                    if (Active[i] != null)
                    {
                        return Active[i];
                    }

                    Active.RemoveAt(i);
                }

                return null;
            }
        }

        /// <summary>
        /// The handler that moves the place the player is standing in (a nebula, the Cosmic Web), which no ray
        /// from inside can hit. Set by whoever spawns the place; two pinches in the air drive it
        /// (<see cref="BeginAirManipulation"/>).
        /// </summary>
        public static ManipulationHandler Place { get; set; }

        /// <summary>
        /// Scale limit used instead of <see cref="ClampScale"/> while only air pinches drive this handler. A
        /// place's metre limits were written for an object held at arm's length and would crush a room.
        /// </summary>
        public Func<Vector3, Vector3> AirScaleClamp { get; set; }

        /// <summary>
        /// Speed in m/s of the hand that let go last; zero for the mouse, a two-handed release or a lost pointer.
        /// </summary>
        public float ReleaseSpeed { get; private set; }

        /// <summary>
        /// A pinch in the air by the other hand joins a one-handed grab as its second pointer, so spreading the
        /// hands scales and turns what is held. It leaves through <see cref="OnPointerUp"/> like any pointer,
        /// and never holds alone: when the hand that grabbed lets go, it goes too.
        /// </summary>
        public bool AddAirPointer(GEPointer pointer)
        {
            if (!enabled || pointer == null || _pointers.Count != 1 || _pointers.Contains(pointer) ||
                manipulationType == HandMovementType.OneHandedOnly)
            {
                return false;
            }

            _pointers.Add(pointer);
            _airPointers.Add(pointer);
            CaptureGrabState();
            return true;
        }

        /// <summary>
        /// Two pinches in the air with nothing held move and scale this handler's host. Ends when either lets
        /// go. Works on a handler that ships disabled (the nebulae): it is enabled for the gesture and put back.
        /// </summary>
        public bool BeginAirManipulation(GEPointer first, GEPointer second)
        {
            if (first == null || second == null || first == second || _pointers.Count > 0 ||
                manipulationType == HandMovementType.OneHandedOnly || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!enabled)
            {
                enabled = true;
                _enabledForAir = true;
            }

            _pointers.Add(first);
            _pointers.Add(second);
            _airPointers.Add(first);
            _airPointers.Add(second);
            Begin(first);
            CaptureGrabState();
            return true;
        }

        private void Begin(GEPointer pointer)
        {
            Active.Remove(this);
            Active.Add(this);
            if (playGrabSounds)
            {
                AudioService.Instance?.PlayClip(AudioId.ManipulationStart);
            }

            OnManipulationStarted.Invoke(new ManipulationEventData { ManipulationSource = gameObject, Pointer = pointer });
        }

        private void End(GEPointer pointer)
        {
            Active.Remove(this);
            _airPointers.Clear();
            if (playGrabSounds)
            {
                AudioService.Instance?.PlayClip(AudioId.ManipulationEnd);
            }

            OnManipulationEnded.Invoke(new ManipulationEventData { ManipulationSource = gameObject, Pointer = pointer });

            if (_enabledForAir)
            {
                enabled = false;
            }
        }

        // An air pointer only ever assists. Once no pointer that selected the object is left and fewer than two
        // air pinches remain, nothing is holding it.
        private void DropStrandedAirPointers()
        {
            if (_airPointers.Count == 0)
            {
                return;
            }

            _airPointers.RemoveAll(p => !_pointers.Contains(p));
            if (_pointers.Count == _airPointers.Count && _pointers.Count < 2)
            {
                _pointers.Clear();
                _airPointers.Clear();
            }
        }

        /// <summary>
        /// Applies this object's scale constraint, if it has one, to a scale someone else worked out. Desktop
        /// mode scales with the wheel rather than with two hands, and must land inside the same limits.
        /// </summary>
        public Vector3 ClampScale(Vector3 desiredLocalScale)
        {
            var constraint = ScaleConstraint();
            if (constraint != null)
            {
                return constraint.ClampScale(desiredLocalScale);
            }

            return FallbackScaleClamp != null ? FallbackScaleClamp(desiredLocalScale) : desiredLocalScale;
        }

        /// <summary>
        /// Used only when there is no <see cref="IManipulationScaleConstraint"/> on this GameObject. A force
        /// solver sets it for bodies whose prefab carries no ScaleLimits, so they are not scaled without bound.
        /// </summary>
        public Func<Vector3, Vector3> FallbackScaleClamp { get; set; }

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
                Begin(pointer);
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

            // Zero after a two-handed hold: CaptureGrabState cleared the velocity when the second hand joined.
            // Zero when the grab was taken away too: a hand dropped fast out of view is not a throw.
            ReleaseSpeed = pointer.IsMouse || eventData.Canceled ? 0f : _pointerVelocity.magnitude;
            DropStrandedAirPointers();

            if (_pointers.Count == 0)
            {
                End(pointer);
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
            _airPointers.Clear();
            _enabledForAir = false;
            Active.Remove(this);
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
                DropStrandedAirPointers();
                if (_pointers.Count == 0)
                {
                    // Pointer is null because the one that went away is exactly what we no longer have.
                    // Listeners that match on ManipulationSource still work; those that match on Pointer
                    // have to tolerate null, which is the honest report of "we do not know which".
                    ReleaseSpeed = 0f;
                    End(null);
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
                    targetScale = _airPointers.Count >= 2 && AirScaleClamp != null
                        ? AirScaleClamp(targetScale)
                        : ClampScale(targetScale);
                }
            }
            else if (manipulationType != HandMovementType.TwoHandedOnly)
            {
                var pointerTransform = _pointers[0].AttachTransform;
                targetRotation = pointerTransform.rotation * _oneHandRotationOffset;
                targetPosition = pointerTransform.TransformPoint(_oneHandPositionOffset);

                if (Time.deltaTime > 0f)
                {
                    var handPosition = _pointers[0].Transform.position;
                    var velocity = (handPosition - _lastPointerPosition) / Time.deltaTime;
                    _pointerVelocity = Vector3.Lerp(_pointerVelocity, velocity,
                        1f - Mathf.Exp(-Time.deltaTime / VelocitySmoothingSeconds));
                    _lastPointerPosition = handPosition;
                }
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
            _pointerVelocity = Vector3.zero;
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
                _lastPointerPosition = _pointers[0].Transform.position;
                _oneHandPositionOffset = pointerTransform.InverseTransformPoint(host.position);
                _oneHandRotationOffset = Quaternion.Inverse(pointerTransform.rotation) * host.rotation;
            }
        }
    }
}
