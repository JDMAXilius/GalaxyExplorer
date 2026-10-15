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

    /// <summary>Scale limits, picked up from the same GameObject as a <see cref="ManipulationHandler"/>.</summary>
    public interface IManipulationScaleConstraint
    {
        Vector3 ClampScale(Vector3 desiredLocalScale);
    }

    /// <summary>
    /// One- and two-handed move/rotate/scale of <see cref="HostTransform"/>. Serialized names and enum values match
    /// MRTK v2's ManipulationHandler so existing prefab settings carry over.
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

        // Hand velocity for the release fling test (CS-290), from the interactor, not the attach point: a far
        // grab's attach point swings on a long lever.
        private const float VelocitySmoothingSeconds = 0.1f;
        private Vector3 _lastPointerPosition, _pointerVelocity;
        private IManipulationScaleConstraint _scaleConstraint;
        private bool _lookedForScaleConstraint;

        private Vector3 _oneHandPositionOffset;
        private Quaternion _oneHandRotationOffset;

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

            // Settable so a grab handle can drive another root (the dock's drag bar); re-captures because the
            // offsets were measured against the old host.
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
        /// How many hands may drive this handler. Desktop reads it: right-drag and the wheel mirror the two-handed
        /// turn and scale, so a one-handed handle has nothing to mirror.
        /// </summary>
        public HandMovementType ManipulationType
        {
            get => manipulationType;
            set => manipulationType = value;
        }

        /// <summary>
        /// Settable for handlers added at run time, which no prefab could tick the box on (a view scene's place).
        /// </summary>
        public bool PlayGrabSounds
        {
            get => playGrabSounds;
            set => playGrabSounds = value;
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

        // Every place set, newest last. A stack rather than one slot (CS-378): a destination nebula opened over the
        // Milky Way is the place while it is open, and the map has to take the air pinches back once it closes,
        // though nothing re-registers the map then.
        private static readonly List<ManipulationHandler> Places = new List<ManipulationHandler>();

        /// <summary>
        /// The place the player stands inside or in front of (a nebula, the Cosmic Web, a view scene), which no ray
        /// from inside can hit; driven by two air pinches (<see cref="BeginAirManipulation"/>). Set by whoever
        /// spawns the place; reads back the newest one still alive, so a closed overlay hands back to the place
        /// under it. Setting null forgets them all.
        /// </summary>
        public static ManipulationHandler Place
        {
            get
            {
                for (var i = Places.Count - 1; i >= 0; i--)
                {
                    var place = Places[i];
                    if (place == null)
                    {
                        Places.RemoveAt(i);
                        continue;
                    }

                    // Skipped, not dropped, while switched off: it may come back. A scene being unloaded is no
                    // longer loaded, so the place just left cannot be pinched in the frames before it is destroyed.
                    if (place.gameObject.activeInHierarchy && place.gameObject.scene.isLoaded)
                    {
                        return place;
                    }
                }

                return null;
            }
            set
            {
                if (value == null)
                {
                    Places.Clear();
                    return;
                }

                Places.Remove(value);
                Places.Add(value);
            }
        }

        /// <summary>
        /// Replaces <see cref="ClampScale"/> during air-only manipulation: arm's-length limits would crush a room.
        /// </summary>
        public Func<Vector3, Vector3> AirScaleClamp { get; set; }

        /// <summary>Speed in m/s of the hand that let go last; zero for the mouse, a two-handed release or a lost pointer.</summary>
        public float ReleaseSpeed { get; private set; }

        /// <summary>
        /// Joins an air pinch to a one-handed grab as its second pointer. It never holds alone: it leaves with
        /// the grabbing hand.
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
        /// Two air pinches with nothing held drive the host. A disabled handler (the nebulae) is enabled for the
        /// gesture and disabled again after.
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

        /// <summary>Applies this object's scale limits to an externally computed scale, so desktop wheel scaling obeys them too.</summary>
        public Vector3 ClampScale(Vector3 desiredLocalScale)
        {
            var constraint = ScaleConstraint();
            if (constraint != null)
            {
                return constraint.ClampScale(desiredLocalScale);
            }

            return FallbackScaleClamp != null ? FallbackScaleClamp(desiredLocalScale) : desiredLocalScale;
        }

        /// <summary>Used when there is no <see cref="IManipulationScaleConstraint"/>; a force solver sets it for unlimited bodies.</summary>
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

            // A cancelled grab is not a throw: a hand dropped fast out of view would otherwise fling.
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
            // A pointer can vanish without OnPointerUp (tracking lost, pointer destroyed). Treat that exactly like
            // a release, or stale offsets and a missing OnManipulationEnded accumulate across grabs.
            var before = _pointers.Count;
            _pointers.RemoveAll(p => p == null || !p.IsActive);
            var lost = before - _pointers.Count;

            if (lost > 0)
            {
                DropStrandedAirPointers();
                if (_pointers.Count == 0)
                {
                    // Pointer is null: the lost one is gone, so listeners keyed on Pointer must tolerate null.
                    ReleaseSpeed = 0f;
                    End(null);
                }
                else
                {
                    // Re-capture, or the object snaps as the two-handed midpoint collapses onto one pointer.
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

                var airOnly = _airPointers.Count >= 2;
                if (scale && _twoHandStartVector.sqrMagnitude > 1e-6f)
                {
                    targetScale = _hostStartScale * (handVector.magnitude / _twoHandStartVector.magnitude);
                    targetScale = airOnly && AirScaleClamp != null
                        ? AirScaleClamp(targetScale)
                        : ClampScale(targetScale);
                }

                // Air-only grabs scale about the hands' midpoint (CS-305): a place's pivot is metres away, and
                // scaling about it slides the part between the hands out from under them.
                var startOffset = _hostStartPosition - _twoHandStartMidpoint;
                if (airOnly && Mathf.Abs(_hostStartScale.x) > 1e-6f)
                {
                    startOffset *= targetScale.x / _hostStartScale.x;
                }

                targetPosition = (move ? midpoint : _twoHandStartMidpoint) + deltaRotation * startOffset;
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

        // Cached, including "none": runs every frame of a two-hand grab and interface GetComponent is not free.
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
