// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Keeps one moon riding its planet, and hands it over to the player when it is pulled.
    ///
    /// <para><b>The moon pattern.</b> A moon owns no orbit of its own: its parent carries an <i>orbit anchor</i> —
    /// an empty transform on a pivot that turns once per <see cref="orbitSeconds"/> — and the anchor is the moon's
    /// <see cref="ForceSolver.RootTransform"/>. The solver snaps the moon onto that anchor every
    /// <c>SolverUpdate</c>, and this component snaps it again in <see cref="Application.onBeforeRender"/> and in
    /// <c>LateUpdate</c>, because the planet can be moved, scaled or released <i>after</i> the solver has run in
    /// the same frame and a moon that lags its planet by one frame reads as detached. That is exactly what
    /// <c>MoonForceSolver</c> does in the orbit model, and this is that behaviour lifted out of it.</para>
    ///
    /// <para><b>Why not <c>MoonForceSolver</c> itself.</b> It derives from <c>PlanetForceSolver</c>, which pins
    /// <c>GoalScale</c> to <c>Vector3.one</c> in its Root state — every moon would inflate to a metre — and
    /// dereferences a <c>PlanetHighlighter</c> with no null check on every state change. Both are fine in the
    /// orbit model, where the scale controller and the highlighter are present; neither survives the arrangeable
    /// body pattern, where a body's <c>localScale</c> <i>is</i> its diameter in metres. So a moon here carries the
    /// plain <c>ForceSolver</c> the planets carry, and this component supplies the three things the subclass would
    /// have: the orbit ride, the show/hide, and the growth to a held size.</para>
    ///
    /// <para><b>Sizes are ratios, not lengths</b> (GDD 6.2: "3-7 cm, scaled to parent, floor 3 cm"). The moon's
    /// diameter is <see cref="diameterRatio"/> of whatever its planet currently measures, never below
    /// <see cref="minOrbitDiameter"/>, so growing Jupiter to a metre with two hands takes its moons with it. The
    /// orbit radius needs no such rule: the anchor is a child of the planet's own root, so it scales with it for
    /// free.</para>
    ///
    /// <para><b>Hidden until the planet is pulled out</b> (GDD 4.1). Visibility follows the <i>parent's</i> solver
    /// state, through the existing <see cref="Moon"/> fader, which cross-fades <c>_TransitionAlpha</c> and switches
    /// the moon's colliders off so a hidden moon cannot be pinched. A moon the player is holding stays up whatever
    /// its planet is doing (<see cref="Moon.KeepVisible"/>), and when the planet goes home the moon is walked back
    /// to its anchor over <see cref="restoreSeconds"/> — GDD 4.1: "moons return to their planets".</para>
    ///
    /// <para>Built by <c>MoonBuilder</c>; every field below is written there.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ForceSolver))]
    public class MoonOrbit : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Copy for this moon. Its panel and its label are both bound from here on Awake.")]
        private BodyInfo info;

        [Header("The planet it belongs to")]
        [SerializeField]
        [Tooltip("The parent body's solver. Its state decides whether this moon is showing at all.")]
        private ForceSolver parentBody;

        [SerializeField]
        [Tooltip("The parent's body root. Its localScale is the planet's diameter in metres.")]
        private Transform parentRoot;

        [SerializeField]
        [Tooltip("The turning node under the parent that carries the anchor. One turn per orbitSeconds.")]
        private Transform orbitPivot;

        [SerializeField]
        [Tooltip("Where the moon sits in its orbit. This is the moon's ForceSolver.RootTransform.")]
        private Transform anchor;

        [Header("Its own parts")]
        [SerializeField]
        [Tooltip("The fader that shows and hides it. Found on this object when empty.")]
        private Moon fade;

        [SerializeField]
        [Tooltip("The small name written under it.")]
        private MoonLabel label;

        [SerializeField]
        [Tooltip("Its short panel. Opens on its own when the moon is pulled out.")]
        private InfoPanel panel;

        [SerializeField]
        [Tooltip("Its grab shape, widened so a 3 cm moon is still pinchable.")]
        private SphereCollider grab;

        [SerializeField]
        [Tooltip("Brings it home. Found on this object when empty.")]
        private FreePlacementSolver placement;

        [Header("Numbers (GDD 6.2)")]
        [SerializeField]
        [Tooltip("Seconds for one turn around the planet.")]
        private float orbitSeconds = 30f;

        [SerializeField]
        [Tooltip("The moon's diameter as a fraction of its planet's, while it is in its orbit.")]
        private float diameterRatio = 0.2f;

        [SerializeField]
        [Tooltip("Smallest a moon may be in its orbit, in metres. The GDD's 3 cm floor.")]
        private float minOrbitDiameter = 0.03f;

        [SerializeField]
        [Tooltip("Diameter a moon grows to when it is pulled out, in metres. The GDD asks for 20 cm.")]
        private float heldDiameter = 0.2f;

        [SerializeField]
        [Tooltip("Seconds the pull-out growth takes.")]
        private float growSeconds = 0.35f;

        [SerializeField]
        [Tooltip("Seconds a moon takes to return to its orbit once its planet has gone home.")]
        private float restoreSeconds = 0.8f;

        [SerializeField]
        [Tooltip("Smallest a moon may be to the hand, in metres. GDD 4.1 asks for a 6 cm grab sphere.")]
        private float minGrabDiameter = 0.06f;

        private ForceSolver _moon;
        private bool _wasParentOut;
        private bool _growing;
        private bool _labelShown;

        public BodyInfo Info => info;

        /// <summary>Where this moon belongs when it is not in the player's hand.</summary>
        public Transform Anchor => anchor;

        /// <summary>The moon's own solver.</summary>
        public ForceSolver Solver => _moon;

        /// <summary>
        /// True while the moon is riding its orbit, or being dwelled on from it. Matches
        /// <c>MoonForceSolver</c>'s own test: dwelling on a moon still in its orbit is not holding it, but
        /// dwelling on one already pulled out is.
        /// </summary>
        public bool InOrbit
        {
            get
            {
                if (_moon == null)
                {
                    return true;
                }

                switch (_moon.ForceState)
                {
                    case ForceSolver.State.None:
                    case ForceSolver.State.Root:
                        return true;
                    case ForceSolver.State.Dwell:
                        return _moon.PreviousForceState == ForceSolver.State.Root;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// True while the planet is out of its arrangement, which is the whole condition for a moon existing at
        /// all (GDD 4.1). The same test <see cref="InfoPanel"/> uses to decide a body's panel is open, so a
        /// planet's panel and its moons always appear and disappear together.
        /// </summary>
        public bool ParentIsOut
        {
            get
            {
                if (parentBody == null)
                {
                    return true;
                }

                switch (parentBody.ForceState)
                {
                    case ForceSolver.State.Attraction:
                    case ForceSolver.State.Free:
                    case ForceSolver.State.Manipulation:
                        return true;
                    case ForceSolver.State.Dwell:
                        return parentBody.PreviousForceState != ForceSolver.State.Root;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// How wide the moon should be right now while it is in its orbit, in the units of whatever the body
        /// group is scaled to. A fraction of the planet, floored at the GDD's 3 cm.
        /// </summary>
        public float OrbitDiameter
        {
            get
            {
                var parentDiameter = parentRoot != null ? parentRoot.localScale.x : 0.25f;
                return Mathf.Max(minOrbitDiameter, parentDiameter * diameterRatio);
            }
        }

        private void Awake()
        {
            _moon = GetComponent<ForceSolver>();

            if (placement == null)
            {
                placement = GetComponent<FreePlacementSolver>();
            }

            if (fade == null)
            {
                fade = GetComponent<Moon>();
            }

            // Belt and braces: the builder serialises this too, but a moon whose anchor was lost would otherwise
            // snap to the world origin the first time the solver ran.
            if (anchor != null && _moon != null)
            {
                _moon.RootTransform = anchor;
            }

            if (info != null)
            {
                if (panel != null)
                {
                    panel.Bind(info);
                }

                if (label != null)
                {
                    label.Bind(info);
                }
            }

            if (label != null)
            {
                label.SetMoon(_moon);
                label.Hide();
            }

            _labelShown = false;

            if (fade != null)
            {
                fade.Blend = 0f;
                fade.KeepVisible = false;
            }

            _wasParentOut = false;
        }

        private void OnEnable() => Application.onBeforeRender += FollowOrbit;

        private void OnDisable() => Application.onBeforeRender -= FollowOrbit;

        private void Update()
        {
            Spin();

            var parentOut = ParentIsOut;
            ApplyVisibility(parentOut);

            // The planet has just gone home with the moon still out in the room. Nothing else will fetch it:
            // LayoutRig only knows about the bodies an arrangement names, and a moon has no slot of its own.
            if (_wasParentOut && !parentOut && !InOrbit)
            {
                SendHome();
            }

            _wasParentOut = parentOut;

            Grow();
            KeepReachable();
        }

        // The planet can move after the solvers have run, so the ride is re-applied as late as the frame allows.
        private void LateUpdate() => FollowOrbit();

        // ---------- the orbit

        private void Spin()
        {
            if (orbitPivot == null || orbitSeconds <= 0f)
            {
                return;
            }

            orbitPivot.Rotate(Vector3.up, 360f / orbitSeconds * Time.deltaTime, Space.Self);
        }

        private void FollowOrbit()
        {
            if (anchor == null || !InOrbit)
            {
                return;
            }

            transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            // OrbitDiameter is measured in the space the parent's body root lives in. The moons group and the
            // bodies group are both children of the content root at scale one, so the two spaces are the same
            // one; the correction is here so that stays true if either group is ever nested differently.
            var mine = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            var theirs = parentRoot != null && parentRoot.parent != null ? parentRoot.parent.lossyScale.x : mine;
            var correction = mine > 0.0001f ? theirs / mine : 1f;

            transform.localScale = Vector3.one * (OrbitDiameter * correction);
        }

        // ---------- showing, hiding and coming home

        private void ApplyVisibility(bool parentOut)
        {
            var held = !InOrbit;

            if (fade != null)
            {
                // Moon cross-fades this over 0.3 s and switches the colliders with it, so a hidden moon cannot be
                // pinched by mistake. KeepVisible overrides the blend while the player has hold of it.
                fade.Blend = parentOut ? 1f : 0f;
                fade.KeepVisible = held;
            }

            var wantLabel = parentOut || held;
            if (label != null && wantLabel != _labelShown)
            {
                if (wantLabel)
                {
                    label.Show();
                }
                else
                {
                    label.Hide();
                }
            }

            _labelShown = wantLabel;
        }

        private void SendHome()
        {
            if (placement != null)
            {
                placement.SetHomeScale(Vector3.one * OrbitDiameter);
                placement.RestoreLayout(restoreSeconds);
                return;
            }

            if (_moon != null)
            {
                _moon.ResetToRoot();
            }
        }

        // ---------- size and reach

        /// <summary>
        /// GDD 6.2: "Held moon grows to 20 cm." It only ever grows, and it stops the moment the player takes hold
        /// of it, so a two-handed resize is never fought. Same shape as <see cref="LayoutRig"/>'s pull-out growth,
        /// which a moon cannot use because it has no slot in any arrangement.
        /// </summary>
        private void Grow()
        {
            if (heldDiameter <= 0f || _moon == null)
            {
                return;
            }

            switch (_moon.ForceState)
            {
                case ForceSolver.State.Attraction:
                    _growing = transform.localScale.x < heldDiameter;
                    break;
                case ForceSolver.State.Free:
                    break; // keep growing if it is still short of the target
                default:
                    _growing = false;
                    return;
            }

            if (!_growing)
            {
                return;
            }

            var current = transform.localScale.x;
            var step = growSeconds <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime / growSeconds);
            var next = Mathf.Lerp(current, heldDiameter, step);

            if (next >= heldDiameter - heldDiameter * 0.01f)
            {
                next = heldDiameter;
                _growing = false;
            }

            transform.localScale = Vector3.one * next;
        }

        /// <summary>
        /// GDD 4.1's 6 cm grab sphere, applied to a moon: the collider is widened rather than a second shape
        /// added, so the moon has one interactable however small the arrangement leaves it. The visual is
        /// normalised to a 1 m diameter, so a radius of 0.5 is exactly its surface.
        /// </summary>
        private void KeepReachable()
        {
            if (grab == null)
            {
                return;
            }

            var metres = Mathf.Max(0.0001f, transform.lossyScale.x);
            var wanted = Mathf.Max(0.5f, minGrabDiameter * 0.5f / metres);
            if (Mathf.Abs(grab.radius - wanted) > wanted * 0.01f)
            {
                grab.radius = wanted;
            }
        }
    }
}
