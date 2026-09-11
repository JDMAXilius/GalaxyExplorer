using System;
using GalaxyExplorer.XR;
using UnityEngine;

/// <summary>
/// Tracks the user's hands (palm joint) or motion controllers and exposes a resolved pose: one side's pose while
/// a single side is tracked, or the midpoint of both sides. Consumers (hand menus, force-pull) listen to the
/// tracking events. Data comes from <see cref="XRInputRig"/>; without an XR rig (desktop) nothing is tracked.
/// </summary>
public class ControllerTransformTracker : MonoBehaviour
{
    public event Action<TrackedObjectType, Transform> NewControllerTrackingStarted;

    public event Action<TrackedObjectType> ControllerTrackingEnded;

    public event Action AllTrackingLost, TrackingUpdated, LeftTrackingUpdated, RightTrackingUpdated, LeftTrackingLost, RightTrackingLost, AnyTrackingStarted, LeftTrackingStarted, RightTrackingStarted;

    [Flags]
    public enum Controllers
    {
        LeftHand = 1,
        RightHand = 2,
        LeftController = 4,
        RightController = 8,
        Left = LeftHand | LeftController,
        Right = RightHand | RightController,
    }

    private Transform
        _leftHandTr,
        _rightHandTr,
        _leftControllerTr,
        _rightControllerTr;

    private Controllers _trackedControllers;

    [SerializeField]
    private Vector3 handOffsetRotation;

    [SerializeField]
    private Vector3 controllerOffsetRotation;

    private Quaternion _handOffsetRotationQuaternion, _controllerOffsetRotationQuaternion;

    private Vector3 _twoHandRotationVector = Vector3.zero;

    #region public accessors

    public bool IsTracking => _trackedControllers != 0;
    public bool BothSides => LeftSide && RightSide;
    public bool RightSide => (_trackedControllers & Controllers.Right) > 0;
    public bool LeftSide => (_trackedControllers & Controllers.Left) > 0;
    public Controllers TrackedControlles => _trackedControllers;

    public Transform LeftTransform =>
        _trackedControllers.HasFlag(Controllers.LeftController) ? _leftControllerTr : _leftHandTr;

    public Transform RightTransform =>
        _trackedControllers.HasFlag(Controllers.RightController) ? _rightControllerTr : _rightHandTr;

    public Vector3 HandOffsetRotation
    {
        get => _handOffsetRotationQuaternion.eulerAngles;
        set => _handOffsetRotationQuaternion = Quaternion.Euler(value);
    }

    public Vector3 ControllerOffsetRotation
    {
        get => _controllerOffsetRotationQuaternion.eulerAngles;
        set => _controllerOffsetRotationQuaternion = Quaternion.Euler(value);
    }

    public Vector3 LeftSidePosition =>
        _trackedControllers.HasFlag(Controllers.LeftController) ? _leftControllerTr.position : _leftHandTr.position;

    public Vector3 RightSidePosition =>
        _trackedControllers.HasFlag(Controllers.RightController) ? _rightControllerTr.position : _rightHandTr.position;

    public Quaternion LeftSideRotation =>
        _trackedControllers.HasFlag(Controllers.LeftController) ?
            _leftControllerTr.rotation * _controllerOffsetRotationQuaternion : _leftHandTr.rotation * _handOffsetRotationQuaternion;

    public Quaternion RightSideRotation =>
        _trackedControllers.HasFlag(Controllers.RightController) ?
            _rightControllerTr.rotation * _controllerOffsetRotationQuaternion : _rightHandTr.rotation * _handOffsetRotationQuaternion;

    public Vector3 ResolvedPosition => transform.position;
    public Quaternion ResolvedRotation => transform.rotation;

    public Transform ResolvedTransform => transform;

    #endregion public accessors

    private void Awake()
    {
        _handOffsetRotationQuaternion = Quaternion.Euler(handOffsetRotation);
        _controllerOffsetRotationQuaternion = Quaternion.Euler(controllerOffsetRotation);
    }

    private void Update()
    {
        PollTrackingState();
        CalculateTrackingTransform();
    }

    private void PollTrackingState()
    {
        var rig = XRInputRig.Instance;
        Controllers now = 0;
        if (rig != null)
        {
            _leftHandTr = rig.LeftPalm;
            _rightHandTr = rig.RightPalm;
            _leftControllerTr = rig.LeftControllerTransform;
            _rightControllerTr = rig.RightControllerTransform;

            if (rig.LeftHandTracked) now |= Controllers.LeftHand;
            if (rig.RightHandTracked) now |= Controllers.RightHand;
            if (rig.LeftControllerTracked) now |= Controllers.LeftController;
            if (rig.RightControllerTracked) now |= Controllers.RightController;
        }

        var before = _trackedControllers;
        if (before == now)
        {
            return;
        }

        _trackedControllers = now;
        RaiseDeviceEvents(before, now);
        CheckBeforeAfter(before, now);
    }

    private void RaiseDeviceEvents(Controllers before, Controllers after)
    {
        RaiseDeviceEvent(before, after, Controllers.LeftHand, TrackedObjectType.HandJointLeft, _leftHandTr);
        RaiseDeviceEvent(before, after, Controllers.RightHand, TrackedObjectType.HandJointRight, _rightHandTr);
        RaiseDeviceEvent(before, after, Controllers.LeftController, TrackedObjectType.MotionControllerLeft, _leftControllerTr);
        RaiseDeviceEvent(before, after, Controllers.RightController, TrackedObjectType.MotionControllerRight, _rightControllerTr);
    }

    private void RaiseDeviceEvent(Controllers before, Controllers after, Controllers device, TrackedObjectType type, Transform deviceTransform)
    {
        bool wasTracked = (before & device) != 0;
        bool isTracked = (after & device) != 0;
        if (!wasTracked && isTracked)
        {
            NewControllerTrackingStarted?.Invoke(type, deviceTransform);
        }
        else if (wasTracked && !isTracked)
        {
            ControllerTrackingEnded?.Invoke(type);
        }
    }

    private void CalculateTrackingTransform()
    {
        if (_trackedControllers == 0) return;

        if (!BothSides)
        {
            if ((_trackedControllers & Controllers.Left) > 0)
            {
                transform.SetPositionAndRotation(LeftSidePosition, LeftSideRotation);
            }
            else
            {
                transform.SetPositionAndRotation(RightSidePosition, RightSideRotation);
            }
            _twoHandRotationVector = Vector3.zero;
        }
        else
        {
            transform.position = (LeftSidePosition + RightSidePosition) * .5f;
            var newRotationVector = LeftSidePosition - RightSidePosition;
            if (_twoHandRotationVector != Vector3.zero)
            {
                transform.rotation = Quaternion.FromToRotation(_twoHandRotationVector, newRotationVector) *
                                     transform.rotation;
            }
            _twoHandRotationVector = newRotationVector;
        }
    }

    private void CheckBeforeAfter(Controllers before, Controllers after)
    {
        if (before == after) return;
        // check for top level
        if (before == 0 && after > 0)
        {
            AnyTrackingStarted?.Invoke();
            TrackingUpdated?.Invoke();
        }
        else if (before > 0 && after == 0)
        {
            AllTrackingLost?.Invoke();
        }
        if (before > 0 && after > 0)
        {
            TrackingUpdated?.Invoke();
        }

        // check for left side
        if ((before & Controllers.Left) == 0 && (after & Controllers.Left) > 0)
        {
            LeftTrackingStarted?.Invoke();
            LeftTrackingUpdated?.Invoke();
        }
        else if ((before & Controllers.Left) > 0 && (after & Controllers.Left) == 0)
        {
            LeftTrackingLost?.Invoke();
        }
        else if ((before & Controllers.Left) != (after & Controllers.Left))
        {
            LeftTrackingUpdated?.Invoke();
        }

        // check for right side
        if ((before & Controllers.Right) == 0 && (after & Controllers.Right) > 0)
        {
            RightTrackingStarted?.Invoke();
            RightTrackingUpdated?.Invoke();
        }
        else if ((before & Controllers.Right) > 0 && (after & Controllers.Right) == 0)
        {
            RightTrackingLost?.Invoke();
        }
        else if ((before & Controllers.Right) != (after & Controllers.Right))
        {
            RightTrackingUpdated?.Invoke();
        }
    }
}
