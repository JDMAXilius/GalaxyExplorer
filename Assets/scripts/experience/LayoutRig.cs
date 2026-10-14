// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Moves an experience's bodies between its <see cref="LayoutPreset"/>s by animating each body's home anchor
    /// (its <see cref="ForceSolver.RootTransform"/>), so the force solver stays the one authority on placement.
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

            /// <summary>Ring clamp as a fraction of true span (1 = untouched); read this, not the transform, to agree with it.</summary>
            [NonSerialized] public float RingFactor = 1f;

            /// <summary>True when there is enough here to place.</summary>
            public bool IsUsable => Root != null && Home != null && !string.IsNullOrEmpty(Id);

            /// <summary>True when this body has rings a layout can clamp.</summary>
            public bool HasRings => Rings != null && RingSpanRatio > 0.0001f;
        }

        /// <summary>A body as the ring clamp sees it. Public so the editor builder can run the same arithmetic.</summary>
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

        // Reused across layout changes so a held dock button does not allocate per request.
        private readonly List<Extent> _extents = new List<Extent>();
        private readonly List<int> _extentOwners = new List<int>();

        /// <summary>The arrangement the rig last applied.</summary>
        public LayoutPreset Current { get; private set; }

        /// <summary>True while bodies are still travelling to the arrangement.</summary>
        public bool IsTransitioning => _running;

        public int BodyCount => bodies != null ? bodies.Length : 0;

        /// <summary>The bodies in their authored order, for a picker that lists them (UI 2.0).</summary>
        public System.Collections.Generic.IReadOnlyList<Body> Bodies => bodies ?? Array.Empty<Body>();

        /// <summary>Raised once an arrangement has been asked for, at the start of the move.</summary>
        public event Action<LayoutPreset> LayoutApplied;

        private void Awake()
        {
            Registry<LayoutRig>.Add(this);
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

        private void OnDestroy() => Registry<LayoutRig>.Remove(this);

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
                    // Unnamed bodies stay put rather than returning to a slot an earlier arrangement gave them.
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
                    // RestoreLayout reads the anchor live, so the body lands on the moving target.
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

            // After the loop: rings clamp against where neighbours are going, not where they stand mid-move.
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
        /// The box the arrangement fills now: centre in world space, size along the rig's axes. Built from home
        /// anchors, not renderers, so it is valid before Start and ignores carried-off bodies, haloes and panels.
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

                // From the transform, not a slot (a layout may not name this body); divided by the rig's scale
                // because the box is in rig units.
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

        private const float FitSpanCapMetres = 0.5f;

        /// <summary>The box <paramref name="layout"/> will fill once its bodies arrive, in rig space, read from its slots.</summary>
        public bool TryGetLayoutBounds(LayoutPreset layout, out Bounds local)
        {
            local = default;

            if (layout == null || layout.Slots == null || bodies == null)
            {
                return false;
            }

            var found = false;
            foreach (var slot in layout.Slots)
            {
                var body = Find(slot.BodyId);
                if (body == null || !body.IsUsable)
                {
                    continue;
                }

                // Capped so Relative Size frames the planets, not the Sun's full 3 m disc.
                var width = Mathf.Min(FitSpanCapMetres, Mathf.Max(0.0001f, slot.Scale) * Mathf.Max(1f, body.SpanRatio));
                var box = new Bounds(slot.LocalPosition, Vector3.one * width);
                if (found)
                {
                    local.Encapsulate(box);
                }
                else
                {
                    local = box;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>The current arrangement, or the one <see cref="Start"/> will apply; what framing should measure.</summary>
        public LayoutPreset PendingOrCurrent => Current != null ? Current : (startLayout != null ? startLayout : FirstLayout());

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

                    var stillOurs = body.Force == null || body.Force.ForceState == ForceSolver.State.Root;
                    if (body.ScaleDrivenHere && stillOurs)
                    {
                        body.Root.localScale = Vector3.one * Mathf.Lerp(body.FromDiameter, body.ToDiameter, eased);
                    }
                }

                // Rings step even for unmoved or held bodies: a neighbour's move can change the room they have.
                StepRings(body, eased);
            }
        }

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

                // From the transform, not the last value written, so an interrupted change does not jump.
                var authored = RingBaseScaleOf(body);
                body.FromRingFactor = authored.x > 0.0001f ? body.Rings.localScale.x / authored.x : 1f;
                body.ToRingFactor = body.HasRings ? RingFactor(i, _extents, ringClearance) : 1f;
            }
        }

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

        /// <summary>Lazily read off the transform for prefabs that serialise none, before any clamp is written (CS-060).</summary>
        private static Vector3 RingBaseScaleOf(Body body)
        {
            if (body.RingBaseScale.sqrMagnitude < 0.000001f)
            {
                body.RingBaseScale = body.Rings != null ? body.Rings.localScale : Vector3.one;
            }

            return body.RingBaseScale;
        }

        /// <summary>
        /// Fraction (0-1) of true span the rings of <paramref name="subject"/> may keep: each pair's gap less
        /// <paramref name="clearance"/> must hold both widest extents. Floored at the planet's own diameter.
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

        // Widens the body's own collider rather than adding a second one; radius 0.5 is the surface of the 1 m
        // normalised prefab, so anything above is grab margin (GDD 4.1, 6 cm).
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

        // GDD 4.1 "grows to 25 cm": only ever grows (never shrinks Jupiter), and stops once held so a two-handed
        // resize is never fought.
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
