// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Keeps one moon riding its planet's orbit anchor, shown only while the planet is out (GDD 4.1, 6.2).
    /// </summary>
    // Not MoonForceSolver: its PlanetForceSolver base pins scale to one, but here localScale is the diameter in
    // metres. Diameter is a ratio of the planet's, floored at 3 cm. Every field is written by MoonBuilder.
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
        private bool _ready;
        private bool _wasParentOut;
        private bool _growing;
        private bool _labelShown;

        /// <summary>The planet this moon belongs to; it comes off only while that planet is out.</summary>
        public ForceSolver ParentBody => parentBody;

        public BodyInfo Info
        {
            get
            {
                EnsureInit();
                return info;
            }
        }

        /// <summary>Where this moon belongs when it is not in the player's hand.</summary>
        public Transform Anchor
        {
            get
            {
                EnsureInit();
                return anchor;
            }
        }

        /// <summary>The moon's own solver.</summary>
        public ForceSolver Solver
        {
            get
            {
                EnsureInit();
                return _moon;
            }
        }

        /// <summary>True while riding its orbit, or dwelled on from it; dwelling on a pulled-out moon counts as held.</summary>
        public bool InOrbit
        {
            get
            {
                EnsureInit();

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
        /// True while the planet is out of its arrangement. The same test <see cref="InfoPanel"/> uses, so a
        /// planet's panel and its moons appear together.
        /// </summary>
        public bool ParentIsOut
        {
            get
            {
                EnsureInit();

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

        /// <summary>Diameter while in orbit: a fraction of the planet's, floored at 3 cm.</summary>
        public float OrbitDiameter
        {
            get
            {
                EnsureInit();

                var parentDiameter = parentRoot != null ? parentRoot.localScale.x : 0.25f;
                return Mathf.Max(minOrbitDiameter, parentDiameter * diameterRatio);
            }
        }

        private void Awake() => EnsureInit();

        // Moons are bound the frame they spawn, before Awake may have run, so every entry point initialises lazily.
        private void EnsureInit()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _moon = GetComponent<ForceSolver>();

            if (placement == null)
            {
                placement = GetComponent<FreePlacementSolver>();
            }

            if (fade == null)
            {
                fade = GetComponent<Moon>();
            }

            // Also serialised by the builder; a lost anchor would otherwise snap the moon to the world origin.
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

        private void OnEnable()
        {
            EnsureInit();
            Application.onBeforeRender += FollowOrbit;
        }

        private void OnDisable() => Application.onBeforeRender -= FollowOrbit;

        private void Update()
        {
            EnsureInit();
            Spin();

            var parentOut = ParentIsOut;
            ApplyVisibility(parentOut);

            // Planet went home with the moon still out; LayoutRig has no slot for a moon, so nothing else fetches it.
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

            // OrbitDiameter is in the parent root's space; corrects for the moons group being scaled differently.
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
                // Moon fades its colliders with the blend, so a hidden moon cannot be pinched.
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

        // Grows to the held size (GDD 6.2) and stops once the player takes hold, so a two-handed resize is never fought.
        private void Grow()
        {
            if (heldDiameter <= 0f || _moon == null)
            {
                return;
            }

            // FreePlacementSolver's restore lerps the same localScale during Free; growing then would fight it.
            if (placement != null && placement.IsRestoring)
            {
                _growing = false;
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

        // Widens the one collider to the 6 cm grab sphere (GDD 4.1); the visual is 1 m across, so radius 0.5 is its surface.
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
