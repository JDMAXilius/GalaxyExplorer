using GalaxyExplorer.XR;
using UnityEngine;

// Re-declares IGEFocusChangedHandler so the focus event below reaches this class first (C# interface
// re-implementation); OnFocusChanged still maps to the base's.
public class PlacementForceSolver : ForceSolver, IGEFocusChangedHandler
{
    private PlacementRing _placementRing;

    protected override void Awake()
    {
        base.Awake();
        _placementRing = GetComponentInChildren<PlacementRing>();
    }

    // With tracked hands the orb comes on a pinch, never on a hover. A hand ray rests on the orb most of the
    // time, so the one second dwell pulled it to the palm unasked. This is how the original treated a device
    // with nothing to dwell with (its gaze-and-tap branch skipped StartDwell): a select on the orb at Root pulls
    // it to the selecting hand, which ForceSolver.OnPointerDown already does. Hover is therefore not passed on;
    // a focus exit still is, so the base forgets a hand whose pinch had registered it.
    void IGEFocusChangedHandler.OnBeforeFocusChange(GEFocusEventData eventData)
    {
        var rig = XRInputRig.Instance;
        if (rig != null && rig.IsHandTrackingActive && eventData.NewFocusedObject != null)
        {
            return;
        }

        OnBeforeFocusChange(eventData);
    }

    protected override Vector3 GetOffsetPositionFromController()
    {
        var ringPosition = _placementRing.transform.position;
        return ringPosition + _placementRing.VectorToDiameterCircle(GoalPosition) - GoalPosition;
//        var diameter = _placementRing.Diameter + _placementRing.Thickness;
//        var controllerPosition = GoalPosition;
//        var placementRingTransform = _placementRing.transform;
//        var ringPosition = placementRingTransform.position;
//        var direction = (controllerPosition - ringPosition).normalized;
//        var up = placementRingTransform.up;
//        var point = ringPosition + diameter * .5f * Vector3.Cross(up, Vector3.Cross(direction, up));
//        return ringPosition - point;
    }
}
