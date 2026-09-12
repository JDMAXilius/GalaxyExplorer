// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Holds the bodies of an arrangeable experience and moves them between its <see cref="LayoutPreset"/>s.
    /// Solar Row against Relative Size is the case it was written for (GDD 4.1); nothing in it is specific to
    /// the solar system, so the orbit model's Schematic/Realistic pair can use it too.
    ///
    /// <para><b>How a layout is applied.</b> Every body owns a <i>home anchor</i>, an empty transform that is its
    /// <see cref="ForceSolver.RootTransform"/>. A body sitting in the arrangement is in
    /// <see cref="ForceSolver.State.Root"/>, and the force solver snaps it onto that anchor every frame, so
    /// moving the anchor moves the body — no second animation to keep in step, and one authority for where a
    /// body belongs. Applying a layout therefore animates the <i>anchors</i> from where they are to the new
    /// slots over the preset's <see cref="LayoutPreset.TransitionSeconds"/>, with the cubic ease-out this app
    /// uses everywhere and never a bounce (GDD 8.7).</para>
    ///
    /// <para>A body the player has pulled out is not on its anchor, so the solver is not carrying it. Those are
    /// handed to <see cref="FreePlacementSolver.RestoreLayout(float)"/>, which walks them home over the same
    /// duration and reads the anchor live, so they chase it while it is still moving and land on it. Panels and
    /// moons unwind on the way, because <c>RestoreLayout</c> finishes by putting the solver back into Root.</para>
    ///
    /// <para><b>Scale is a diameter.</b> A <see cref="LayoutSlot.Scale"/> is the body's diameter in metres, which
    /// only means anything because every body prefab is normalised to a 1 m diameter (CS-041, and the convention
    /// CS-040 set). So the rig writes it straight into <c>localScale</c> and can read <c>lossyScale</c> back as a
    /// measurement in metres.</para>
    ///
    /// <para><b>Two things it keeps true every frame.</b> A body is never smaller to the hand than
    /// <see cref="minGrabDiameter"/> — GDD 4.1 asks for a 6 cm invisible grab sphere so a 5 mm Pluto can still be
    /// pinched — which is done by widening the grab collider rather than by adding a second one, so a body has
    /// one shape and one interactable however large it is. And a body being pulled out grows to
    /// <see cref="pullDiameter"/> on the way to the hand (GDD 4.1: "grows to 25 cm").</para>
    /// </summary>
    public class LayoutRig : MonoBehaviour
    {
        /// <summary>One body of the arrangement and the handful of parts a layout has to touch.</summary>
        [Serializable]
        public class Body
        {
            [Tooltip("Matches LayoutSlot.BodyId and BodyInfo.Id.")]
            public string Id;

            [Tooltip("Copy for this body's panel.")]
            public BodyInfo Info;

            [Tooltip("The body. Its localScale is its diameter in metres: the visual under it is normalised to 1 m.")]
            public Transform Root;

            [Tooltip("Where the arrangement puts it. This is the body's ForceSolver.RootTransform.")]
            public Transform Home;

            public ForceSolver Force;

            public FreePlacementSolver Placement;

            [Tooltip("The body's grab shape. Its radius is widened so a small body is still pinchable.")]
            public SphereCollider Grab;

            [Tooltip("The panel that opens beside it. Bound to Info on Awake.")]
            public InfoPanel Panel;

            [NonSerialized] public Vector3 FromPosition;
            [NonSerialized] public Vector3 ToPosition;
            [NonSerialized] public Quaternion FromRotation;
            [NonSerialized] public Quaternion ToRotation;
            [NonSerialized] public float FromDiameter;
            [NonSerialized] public float ToDiameter;

            /// <summary>True while this body's own scale is the rig's to animate, rather than the placement solver's.</summary>
            [NonSerialized] public bool ScaleDrivenHere;

            /// <summary>True once an arrangement has named this body, so the rig has a From and a To for it.</summary>
            [NonSerialized] public bool Moving;

            /// <summary>True while the body is still growing to its pulled-out size.</summary>
            [NonSerialized] public bool Growing;

            /// <summary>True when there is enough here to place.</summary>
            public bool IsUsable => Root != null && Home != null && !string.IsNullOrEmpty(Id);
        }

        [SerializeField]
        [Tooltip("The experience these bodies belong to. A layout asked for on any other module is ignored.")]
        private ExperienceModule module;

        [SerializeField]
        [Tooltip("Applied on Start, with no animation. Empty falls back to the module's first layout.")]
        private LayoutPreset startLayout;

        [SerializeField]
        private Body[] bodies = Array.Empty<Body>();

        [SerializeField]
        [Tooltip("Smallest a body may be to the hand, in metres. GDD 4.1 asks for a 6 cm grab sphere.")]
        private float minGrabDiameter = 0.06f;

        [SerializeField]
        [Tooltip("Diameter a body grows to as it is pulled out, in metres. Zero leaves it at its layout size.")]
        private float pullDiameter = 0.25f;

        [SerializeField]
        [Tooltip("Seconds the pull-out growth takes.")]
        private float pullGrowSeconds = 0.35f;

        private float _elapsed;
        private float _duration;
        private bool _running;

        /// <summary>The arrangement the rig last applied.</summary>
        public LayoutPreset Current { get; private set; }

        /// <summary>True while bodies are still travelling to the arrangement.</summary>
        public bool IsTransitioning => _running;

        public int BodyCount => bodies != null ? bodies.Length : 0;

        /// <summary>Raised once an arrangement has been asked for, at the start of the move.</summary>
        public event Action<LayoutPreset> LayoutApplied;

        private void Awake()
        {
            if (bodies == null)
            {
                return;
            }

            foreach (var body in bodies)
            {
                if (body != null && body.Panel != null && body.Info != null)
                {
                    body.Panel.Bind(body.Info);
                }
            }
        }

        private void OnEnable() => DockController.LayoutRequested += OnLayoutRequested;

        private void OnDisable() => DockController.LayoutRequested -= OnLayoutRequested;

        private void Start() => Apply(startLayout != null ? startLayout : FirstLayout(), 0f);

        // ---------- asking for an arrangement

        /// <summary>The body with this id, or null. Desktop shortcuts and tests reach the bodies through this.</summary>
        public Body Find(string bodyId)
        {
            if (bodies == null || string.IsNullOrEmpty(bodyId))
            {
                return null;
            }

            foreach (var body in bodies)
            {
                if (body != null && body.Id == bodyId)
                {
                    return body;
                }
            }

            return null;
        }

        /// <summary>Puts everything back into the arrangement it is already in — Recenter, and the desktop's R.</summary>
        public void Restore() => Apply(Current, -1f);

        /// <summary>
        /// Moves every body this arrangement names into it. <paramref name="seconds"/> below zero uses the
        /// preset's own <see cref="LayoutPreset.TransitionSeconds"/>; zero snaps.
        /// </summary>
        public void Apply(LayoutPreset layout, float seconds = -1f)
        {
            if (layout == null || bodies == null)
            {
                return;
            }

            var duration = seconds >= 0f ? seconds : Mathf.Max(0f, layout.TransitionSeconds);

            foreach (var body in bodies)
            {
                if (body == null || !body.IsUsable)
                {
                    continue;
                }

                var found = layout.Find(body.Id);
                if (!found.HasValue)
                {
                    // An arrangement that does not name a body leaves it exactly where it is, rather than
                    // dragging it to the origin — or, worse, back to the slot some earlier arrangement gave it.
                    body.Moving = false;
                    continue;
                }

                body.Moving = true;

                var slot = found.Value;
                var diameter = Mathf.Max(0.0001f, slot.Scale);

                body.FromPosition = body.Home.localPosition;
                body.ToPosition = slot.LocalPosition;
                body.FromRotation = body.Home.localRotation;
                body.ToRotation = Quaternion.Euler(slot.LocalEuler);
                body.FromDiameter = body.Root.localScale.x;
                body.ToDiameter = diameter;
                body.Growing = false;

                // Home is where a released body returns to, and how big it is when it gets there.
                if (body.Placement != null)
                {
                    body.Placement.SetHomeScale(Vector3.one * diameter);
                }

                var atHome = body.Force == null || body.Force.ForceState == ForceSolver.State.Root;
                body.ScaleDrivenHere = atHome;

                if (duration <= 0f)
                {
                    body.Home.localPosition = body.ToPosition;
                    body.Home.localRotation = body.ToRotation;
                    body.Root.localScale = Vector3.one * diameter;

                    if (!atHome && body.Placement != null)
                    {
                        body.Placement.RestoreImmediate();
                    }
                    else if (!atHome && body.Force != null)
                    {
                        body.Force.ResetToRoot();
                    }

                    continue;
                }

                if (!atHome)
                {
                    // Out in the room: walk it home over the same time the anchors take, reading the anchor live
                    // so it lands on the moving target rather than where the target used to be.
                    if (body.Placement != null)
                    {
                        body.Placement.RestoreLayout(duration);
                    }
                    else if (body.Force != null)
                    {
                        body.Force.ResetToRoot();
                        body.ScaleDrivenHere = true;
                    }
                }
            }

            Current = layout;
            _elapsed = 0f;
            _duration = duration;
            _running = duration > 0f;

            LayoutApplied?.Invoke(layout);
        }

        private LayoutPreset FirstLayout()
        {
            return module != null && module.Layouts != null && module.Layouts.Length > 0 ? module.Layouts[0] : null;
        }

        private void OnLayoutRequested(ExperienceModule requested, LayoutPreset layout)
        {
            if (module == null || requested == module)
            {
                Apply(layout);
            }
        }

        // ---------- every frame

        private void LateUpdate()
        {
            if (_running)
            {
                _elapsed += Time.deltaTime;
                var t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
                Step(1f - Mathf.Pow(1f - t, 3f)); // cubic ease-out; nothing in this app bounces (GDD 8.7)
                _running = t < 1f;
            }

            KeepBodiesReachable();
        }

        /// <summary>Moves the anchors, and the scale of every body the rig is carrying, to <paramref name="eased"/>.</summary>
        private void Step(float eased)
        {
            if (bodies == null)
            {
                return;
            }

            foreach (var body in bodies)
            {
                if (body == null || !body.Moving || !body.IsUsable)
                {
                    continue;
                }

                body.Home.localPosition = Vector3.Lerp(body.FromPosition, body.ToPosition, eased);
                body.Home.localRotation = Quaternion.Slerp(body.FromRotation, body.ToRotation, eased);

                // A body the player grabbed mid-transition is theirs, not the rig's, until they let go.
                var stillOurs = body.Force == null || body.Force.ForceState == ForceSolver.State.Root;
                if (body.ScaleDrivenHere && stillOurs)
                {
                    body.Root.localScale = Vector3.one * Mathf.Lerp(body.FromDiameter, body.ToDiameter, eased);
                }
            }
        }

        /// <summary>
        /// Keeps every body pinchable and grows the one being pulled.
        ///
        /// The grab sphere is the body's own collider widened, not a second shape: the body prefab is normalised
        /// to a 1 m diameter, so a radius of 0.5 is exactly its surface and anything above that is the margin a
        /// small body needs. Because it is recomputed from <c>lossyScale</c> it stays right as the player scales
        /// the body, and it collapses back to the surface the moment the body is wider than the minimum.
        /// </summary>
        private void KeepBodiesReachable()
        {
            if (bodies == null)
            {
                return;
            }

            var minRadius = Mathf.Max(0f, minGrabDiameter) * 0.5f;

            foreach (var body in bodies)
            {
                if (body == null || body.Root == null)
                {
                    continue;
                }

                if (body.Grab != null)
                {
                    var metres = Mathf.Max(0.0001f, body.Root.lossyScale.x);
                    var wanted = Mathf.Max(0.5f, minRadius / metres);
                    if (Mathf.Abs(body.Grab.radius - wanted) > wanted * 0.01f)
                    {
                        body.Grab.radius = wanted;
                    }
                }

                Grow(body);
            }
        }

        /// <summary>
        /// GDD 4.1: a body pulled out "grows to 25 cm". It only ever grows — shrinking Jupiter on the way to the
        /// hand would undo the whole point of the Relative Size arrangement — and it stops the moment the player
        /// takes hold of it, so a two-handed resize is never fought.
        /// </summary>
        private void Grow(Body body)
        {
            if (pullDiameter <= 0f || body.Force == null)
            {
                return;
            }

            switch (body.Force.ForceState)
            {
                case ForceSolver.State.Attraction:
                    body.Growing = body.Root.localScale.x < pullDiameter;
                    break;
                case ForceSolver.State.Free:
                    break; // keep growing if it is still short of the target
                default:
                    body.Growing = false;
                    return;
            }

            if (!body.Growing)
            {
                return;
            }

            var current = body.Root.localScale.x;
            var step = pullGrowSeconds <= 0f ? 1f : Mathf.Clamp01(Time.deltaTime / pullGrowSeconds);
            var next = Mathf.Lerp(current, pullDiameter, step);

            if (next >= pullDiameter - pullDiameter * 0.01f)
            {
                next = pullDiameter;
                body.Growing = false;
            }

            body.Root.localScale = Vector3.one * next;
        }
    }
}
