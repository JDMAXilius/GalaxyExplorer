using UnityEngine;

/// <summary>
/// Force solver for a moon that can be pulled out of its orbit. While at rest the moon rides its orbit anchor around
/// the parent planet (position, rotation and the planet's current scale) and shows or hides with the planet's other
/// moons; once pulled it behaves like any planet: dwell, pull, grab, move, rotate, scale, voice-over, ambience and reset.
/// </summary>
public class MoonForceSolver : PlanetForceSolver
{
    [SerializeField]
    [Tooltip("The planet this moon orbits. Its controller tracker is shared when none is assigned here.")]
    private ForceSolver parentPlanet = null;

    [SerializeField]
    [Tooltip("The orbit animation (on the planet) that shows and hides this moon while it is in its orbit.")]
    private Moon orbit = null;

    // At rest in the orbit, or being dwelled on from there.
    private bool InOrbit => ForceState == State.Root || ForceState == State.None ||
                            (ForceState == State.Dwell && PreviousForceState == State.Root);

    protected override void Awake()
    {
        if (ControllerTracker == null && parentPlanet != null)
        {
            ControllerTracker = parentPlanet.ControllerTracker;
        }

        base.Awake();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Application.onBeforeRender += FollowOrbit;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= FollowOrbit;
    }

    public override void SolverUpdate()
    {
        base.SolverUpdate();
        FollowOrbit();

        if (orbit != null)
        {
            orbit.KeepVisible = !InOrbit;
        }
    }

    // Also run just before rendering: the planet can move after this solver has updated in the same frame.
    private void FollowOrbit()
    {
        if (!InOrbit || RootTransform == null)
        {
            return;
        }

        transform.SetPositionAndRotation(RootTransform.position, RootTransform.rotation);
        var parent = transform.parent;
        var parentScale = parent != null ? parent.lossyScale.x : 1f;
        if (parentScale > 0f)
        {
            transform.localScale = Vector3.one * (RootTransform.lossyScale.x / parentScale);
        }
    }
}
