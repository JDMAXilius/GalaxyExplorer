// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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
    /// <para><b>Rings are clamped to the arrangement, not to a table.</b> Saturn's ring mesh is 2.26 times the
    /// planet's own diameter and Uranus's is 1.99, so in an arrangement that stands the bodies 25 cm apart the two
    /// ring discs pass straight through each other (CS-060). The rings are therefore scaled down to whatever the
    /// arrangement leaves room for: for each pair the gap between their centres, less
    /// <see cref="ringClearance"/>, has to hold both bodies' widest extents, and a ring system that does not fit
    /// is shrunk until it does. Two ring systems in the same gap shrink by the same factor — neither has a better
    /// claim on the space — while against a solid neighbour, which cannot give, the rings absorb the whole
    /// shortfall. Nothing is tabulated: the numbers come out of the layout being applied, so a change to the GDD's
    /// pitch changes the clamp with it, and in an arrangement whose bodies stand well apart (Relative Size) no
    /// pair is short of room and every ring keeps its true proportions.</para>
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

            [Tooltip("The body's ring mesh, when it has one. Left empty for a body without rings.")]
            public Transform Rings;

            [Tooltip("The rings' true span as a multiple of the body's diameter, measured when the prefab was " +
                     "built. A ratio rather than a length, so it holds at whatever size a layout gives the body.")]
            public float RingSpanRatio;

            [Tooltip("The rings' authored local scale. A clamp multiplies this rather than replacing it, so a " +
                     "ring mesh that was not authored at scale one survives.")]
            public Vector3 RingBaseScale = Vector3.one;

            [Tooltip("Widest solid geometry as a multiple of the body's diameter — the rings on a ringed body, " +
                     "the clouds otherwise. What its neighbours have to keep clear of.")]
            public float SpanRatio = 1f;

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

            [NonSerialized] public float FromRingFactor;
            [NonSerialized] public float ToRingFactor;

            /// <summary>
            /// What the arrangement last left the rings, as a fraction of their true span; 1 is untouched. Read
            /// it rather than the transform when something else has to agree with the clamp — the ring shadow
            /// band on the planet's own material is driven separately (CS-048) and will want this factor.
            /// </summary>
            [NonSerialized] public float RingFactor = 1f;

            /// <summary>True when there is enough here to place.</summary>
            public bool IsUsable => Root != null && Home != null && !string.IsNullOrEmpty(Id);

            /// <summary>True when this body has rings a layout can clamp.</summary>
            public bool HasRings => Rings != null && RingSpanRatio > 0.0001f;
        }

        /// <summary>
        /// One body reduced to what the ring clamp needs to know about it: where an arrangement puts it, how big,
        /// and how far its geometry reaches. Public so the editor builder can run the same arithmetic against a
        /// <see cref="LayoutPreset"/> and report the spans a run will actually produce.
        /// </summary>
        public struct Extent
        {
            /// <summary>Where the arrangement puts the body, in the rig's own space.</summary>
            public Vector3 Position;

            /// <summary>The body's diameter in metres — a <see cref="LayoutSlot.Scale"/>.</summary>
            public float Diameter;

            /// <summary>Widest solid geometry as a multiple of the diameter.</summary>
            public float SpanRatio;

            /// <summary>Ring span as a multiple of the diameter, or zero for a body without rings.</summary>
            public float RingRatio;

            /// <summary>Half the width this body needs kept clear, in metres.</summary>
            public float HalfExtent =>
                Diameter * Mathf.Max(1f, RingRatio > 0f ? RingRatio : SpanRatio) * 0.5f;
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

        [SerializeField]
        [Tooltip("Gap left between a ring's edge and whatever stands next to it, in metres.")]
        private float ringClearance = 0.01f;

        private float _elapsed;
        private float _duration;
        private bool _running;

        // Rebuilt on each layout change rather than allocated there: ten bodies, but a layout can be asked for
        // from a dock button held down, and this runs the pair loop every time.
        private readonly List<Extent> _extents = new List<Extent>();
        private readonly List<int> _extentOwners = new List<int>();

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

            // After the loop, because a ring is clamped against where its neighbours are *going*, not where they
            // happen to be standing halfway through the last change.
            PlanRings();

            Current = layout;
            _elapsed = 0f;
            _duration = duration;
            _running = duration > 0f;

            if (!_running)
            {
                foreach (var body in bodies)
                {
                    if (body != null && body.IsUsable)
                    {
                        StepRings(body, 1f);
                    }
                }
            }

            LayoutApplied?.Invoke(layout);
        }

        /// <summary>
        /// The box the arrangement fills right now: <c>center</c> in world space, <c>size</c> measured along the
        /// rig's own axes. False when there is no arrangement to measure.
        ///
        /// <para>It is built from the home anchors and the bodies' own diameters rather than from renderers,
        /// because those are the authored intent: they are right before the first <see cref="Start"/> has run,
        /// they still describe the arrangement when the player has carried a body off, and they are unmoved by
        /// haloes, labels and panels. It is measured on the rig's axes and only then put back into the world so
        /// that the centre is the middle of the arrangement itself, and not the middle of a world-aligned box
        /// drawn round a row standing at an angle.</para>
        ///
        /// <para><see cref="ExperienceDirector"/> uses it to frame a place on the content root. A rig whose
        /// bodies are not wired yet reports nothing rather than a box around the origin, so a half-built prefab
        /// is left where it is instead of being shoved somewhere by a measurement of nothing.</para>
        /// </summary>
        public bool TryGetArrangementBounds(out Bounds bounds)
        {
            bounds = default;

            if (bodies == null)
            {
                return false;
            }

            var rigScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            var found = false;

            foreach (var body in bodies)
            {
                if (body == null || !body.IsUsable)
                {
                    continue;
                }

                // The body's own width, taken from the transform rather than from a slot: a layout may not name
                // this body at all, and lossyScale is a diameter in metres by the convention CS-040 set. Divided
                // back out of the rig's own scale, because the box is being built in the rig's units.
                var width = Mathf.Max(0.0001f, body.Root.lossyScale.x) / rigScale * Mathf.Max(1f, body.SpanRatio);
                var box = new Bounds(transform.InverseTransformPoint(body.Home.position), Vector3.one * width);

                if (found)
                {
                    bounds.Encapsulate(box);
                }
                else
                {
                    bounds = box;
                    found = true;
                }
            }

            if (found)
            {
                bounds.center = transform.TransformPoint(bounds.center);
            }

            return found;
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
                if (body == null || !body.IsUsable)
                {
                    continue;
                }

                if (body.Moving)
                {
                    body.Home.localPosition = Vector3.Lerp(body.FromPosition, body.ToPosition, eased);
                    body.Home.localRotation = Quaternion.Slerp(body.FromRotation, body.ToRotation, eased);

                    // A body the player grabbed mid-transition is theirs, not the rig's, until they let go.
                    var stillOurs = body.Force == null || body.Force.ForceState == ForceSolver.State.Root;
                    if (body.ScaleDrivenHere && stillOurs)
                    {
                        body.Root.localScale = Vector3.one * Mathf.Lerp(body.FromDiameter, body.ToDiameter, eased);
                    }
                }

                // Rings follow the arrangement whether or not this body is moving into it, and whether or not the
                // player is holding it: a body out in the room is one the arrangement will take back, and a
                // neighbour's move can free up room for a body that never moved itself. Once the change is over
                // nothing here writes the ring scale again, so a held body simply keeps what the layout gave it.
                StepRings(body, eased);
            }
        }

        /// <summary>Moves one body's rings towards the span this arrangement allows them.</summary>
        private static void StepRings(Body body, float eased)
        {
            if (body.Rings == null)
            {
                return;
            }

            body.RingFactor = Mathf.Lerp(body.FromRingFactor, body.ToRingFactor, eased);
            body.Rings.localScale = RingBaseScaleOf(body) * body.RingFactor;
        }

        // ---------- rings

        /// <summary>
        /// Works out what each ring system is allowed in the arrangement that has just been asked for, and sets
        /// up the animation to it from wherever the rings are now.
        /// </summary>
        private void PlanRings()
        {
            _extents.Clear();
            _extentOwners.Clear();

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body == null || !body.IsUsable)
                {
                    continue;
                }

                TargetOf(body, out var position, out var diameter);
                _extents.Add(new Extent
                {
                    Position = position,
                    Diameter = diameter,
                    SpanRatio = body.SpanRatio,
                    RingRatio = body.HasRings ? body.RingSpanRatio : 0f,
                });
                _extentOwners.Add(i);
            }

            for (var i = 0; i < _extents.Count; i++)
            {
                var body = bodies[_extentOwners[i]];
                if (body.Rings == null)
                {
                    continue;
                }

                // Read the starting factor off the transform rather than off the last one we wrote: it is the
                // one thing here that cannot be out of date, and it makes an interrupted change animate from
                // where the rings actually are instead of jumping.
                var authored = RingBaseScaleOf(body);
                body.FromRingFactor = authored.x > 0.0001f ? body.Rings.localScale.x / authored.x : 1f;
                body.ToRingFactor = body.HasRings ? RingFactor(i, _extents, ringClearance) : 1f;
            }
        }

        /// <summary>
        /// Where this arrangement puts a body and how big it makes it. A body the arrangement does not name is
        /// staying where it is, so it is measured where it stands.
        /// </summary>
        private static void TargetOf(Body body, out Vector3 position, out float diameter)
        {
            if (body.Moving)
            {
                position = body.ToPosition;
                diameter = Mathf.Max(0.0001f, body.ToDiameter);
                return;
            }

            position = body.Home.localPosition;
            diameter = Mathf.Max(0.0001f, body.Root.localScale.x);
        }

        /// <summary>
        /// The rings' authored scale. A prefab built before CS-060 serialises none, so it is read off the
        /// transform the first time it is wanted — which is before anything has written a clamp to it.
        /// </summary>
        private static Vector3 RingBaseScaleOf(Body body)
        {
            if (body.RingBaseScale.sqrMagnitude < 0.000001f)
            {
                body.RingBaseScale = body.Rings != null ? body.Rings.localScale : Vector3.one;
            }

            return body.RingBaseScale;
        }

        /// <summary>
        /// How much of their true span the rings of <paramref name="subject"/> may keep in this arrangement, from
        /// 0 to 1. Nothing is tabulated: every number comes out of the arrangement, so the clamp follows a change
        /// to the GDD's spacing with no edit here.
        ///
        /// <para>The rule is one constraint per pair — the distance between two bodies, less
        /// <paramref name="clearance"/>, has to hold both of their widest extents. Where the neighbour also has
        /// rings the pair shares the shortfall equally, since neither has a better claim on the gap; where it is
        /// solid geometry that cannot be shrunk, the rings absorb all of it. A pair that already fits imposes
        /// nothing, which is what keeps an arrangement with room to spare — Relative Size — at true ring
        /// proportions rather than trimming rings that were never in anyone's way.</para>
        ///
        /// <para>The floor is the body itself: rings smaller than the planet they belong to would be hidden
        /// inside it, and an arrangement tight enough to ask for that is one whose planets already overlap, which
        /// is a fault in the layout rather than something to hide.</para>
        /// </summary>
        public static float RingFactor(int subject, IList<Extent> arrangement, float clearance)
        {
            if (arrangement == null || subject < 0 || subject >= arrangement.Count)
            {
                return 1f;
            }

            var self = arrangement[subject];
            var ringHalf = self.Diameter * self.RingRatio * 0.5f;
            if (ringHalf <= 0.0001f)
            {
                return 1f;
            }

            clearance = Mathf.Max(0f, clearance);
            var allowed = 1f;

            for (var i = 0; i < arrangement.Count; i++)
            {
                if (i == subject)
                {
                    continue;
                }

                var other = arrangement[i];
                var otherHalf = other.HalfExtent;
                var available = Vector3.Distance(self.Position, other.Position) - clearance;

                var limit = other.RingRatio > 0f
                    ? available / (ringHalf + otherHalf)   // two ring systems shrink by the same factor
                    : (available - otherHalf) / ringHalf;  // a solid neighbour cannot give: take the whole hit

                if (limit < allowed)
                {
                    allowed = limit;
                }
            }

            var floor = self.RingRatio > 1f ? 1f / self.RingRatio : 1f;
            return Mathf.Clamp(allowed, floor, 1f);
        }

        // ---------- reach

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
