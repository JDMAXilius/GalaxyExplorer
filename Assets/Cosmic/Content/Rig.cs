using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmic
{
    public class Rig : MonoBehaviour
    {
        [SerializeField] Orbit orbit;
        [SerializeField] List<Body> bodies = new List<Body>();
        [SerializeField] float ringClearanceMetres = 0.01f;

        public event Action<Layout> Applied;

        readonly Dictionary<Body, Transform> anchors = new Dictionary<Body, Transform>();
        readonly List<Move> moves = new List<Move>();
        Coroutine running;
        bool orbitBound;

        public Layout Current { get; private set; }
        public IReadOnlyList<Body> Bodies => bodies;

        public void Bind(IList<Body> list)
        {
            bodies.Clear();
            anchors.Clear();
            orbitBound = false;
            if (list == null) return;
            for (var i = 0; i < list.Count; i++) Anchor(list[i]);
        }

        public Transform Anchor(Body body)
        {
            if (body == null) return null;
            if (anchors.TryGetValue(body, out var known) && known != null) return known;
            var name = $"home_{(string.IsNullOrEmpty(body.id) ? body.name : body.id)}";
            var anchor = transform.Find(name) ?? Deep(transform, name);
            if (anchor == null)
            {
                anchor = new GameObject(name).transform;
                anchor.SetParent(transform, false);
            }
            anchors[body] = anchor;
            if (!bodies.Contains(body)) bodies.Add(body);
            return anchor;
        }

        public void Apply(Layout layout, float seconds = -1f)
        {
            if (layout == null) return;
            Current = layout;
            var orbiting = layout.kind == LayoutKind.Schematic || layout.kind == LayoutKind.Realistic;
            var duration = Mathf.Max(0f, seconds < 0f ? layout.transitionSeconds : seconds);
            if (running != null) StopCoroutine(running);
            running = null;
            if (orbit != null)
            {
                if (orbiting) BindOrbit();
                orbit.Blend = orbiting && duration > 0f ? 0f : 1f;
                orbit.Running = orbiting;
            }
            var realism = layout.kind == LayoutKind.Realistic ? 1f : 0f;
            Plan(layout, orbiting, realism);
            if (duration <= 0f || !isActiveAndEnabled) Finish(realism, orbiting);
            else running = StartCoroutine(Run(realism, orbiting, duration));
        }

        void BindOrbit()
        {
            if (orbitBound) return;
            Transform sun = null;
            var pairs = new List<(Body body, Transform anchor)>(bodies.Count);
            var moons = new List<(Body moon, Transform planetAnchor, Transform moonAnchor)>();
            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                var anchor = Anchor(body);
                if (anchor == null) continue;
                if (anchor.parent != transform) moons.Add((body, anchor.parent != null ? anchor.parent.parent : null, anchor));
                else if (sun == null && body.kind == BodyKind.Star) sun = anchor;
                else pairs.Add((body, anchor));
            }
            orbit.Bind(sun != null ? sun : transform, pairs);
            orbit.BindMoons(moons);
            orbitBound = true;
        }

        static Transform Deep(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = Deep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        void Plan(Layout layout, bool orbiting, float realism)
        {
            var factors = Clamp(layout, orbiting);
            moves.Clear();
            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body == null || !layout.TryFind(body, out var slot)) continue;
                var anchor = Anchor(body);
                if (anchor == null) continue;
                var rings = anchor.Find($"body_{body.id}/rings");
                moves.Add(new Move
                {
                    anchor = anchor,
                    rings = rings,
                    fromPosition = anchor.localPosition,
                    toPosition = orbiting ? anchor.localPosition : slot.localPosition,
                    fromRotation = anchor.localRotation,
                    toRotation = orbiting ? anchor.localRotation : Quaternion.Euler(slot.localEuler),
                    fromScale = anchor.localScale,
                    toScale = Vector3.one * (orbiting && orbit != null ? orbit.BodyScaleAt(realism, slot.scale) : Mathf.Max(0.0001f, slot.scale)),
                    fromRing = rings != null ? rings.localScale : Vector3.one,
                    toRing = Vector3.one * factors[i],
                });
            }
        }

        // One constraint per pair: the gap less the clearance holds both half extents; only the `rings` child shrinks, never the globe.
        float[] Clamp(Layout layout, bool orbiting)
        {
            var count = bodies.Count;
            var factors = new float[count];
            var at = new Vector3[count];
            var half = new float[count];
            var spans = new float[count];
            var ringed = new bool[count];
            for (var i = 0; i < count; i++)
            {
                factors[i] = 1f;
                spans[i] = 1f;
                var anchor = Anchor(bodies[i]);
                if (anchor == null) continue;
                ringed[i] = anchor.Find($"body_{bodies[i].id}/rings") != null;
                at[i] = anchor.localPosition;
                var diameter = Mathf.Max(0.0001f, anchor.localScale.x);
                if (!orbiting && layout.TryFind(bodies[i], out var slot))
                {
                    at[i] = slot.localPosition;
                    diameter = Mathf.Max(0.0001f, slot.scale);
                    spans[i] = ringed[i] ? Mathf.Max(1f, slot.spanRatio) : 1f;
                }
                half[i] = diameter * spans[i] * 0.5f;
            }

            if (orbiting) return factors;
            var clearance = Mathf.Max(0f, ringClearanceMetres);
            for (var i = 0; i < count; i++)
            {
                if (!ringed[i] || half[i] <= 0f) continue;
                var allowed = 1f;
                for (var j = 0; j < count; j++)
                {
                    if (j == i || half[j] <= 0f) continue;
                    var available = Vector3.Distance(at[i], at[j]) - clearance;
                    var limit = ringed[j]
                        ? available / (half[i] + half[j])
                        : (available - half[j]) / half[i];
                    if (limit < allowed) allowed = limit;
                }
                factors[i] = Mathf.Clamp(allowed, 1f / spans[i], 1f);
            }

            return factors;
        }

        IEnumerator Run(float realism, bool orbiting, float seconds)
        {
            var from = orbit != null ? orbit.Realism : 0f;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Step(Tween.EaseOutCubic(elapsed / seconds), from, realism, orbiting);
                yield return null;
            }
            running = null;
            Finish(realism, orbiting);
        }

        void Step(float k, float fromRealism, float realism, bool orbiting)
        {
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.anchor == null) continue;
                if (!orbiting)
                {
                    move.anchor.localPosition = Vector3.LerpUnclamped(move.fromPosition, move.toPosition, k);
                    move.anchor.localRotation = Quaternion.SlerpUnclamped(move.fromRotation, move.toRotation, k);
                }
                move.anchor.localScale = Vector3.LerpUnclamped(move.fromScale, move.toScale, k);
                if (move.rings != null) move.rings.localScale = Vector3.LerpUnclamped(move.fromRing, move.toRing, k);
            }
            if (orbiting && orbit != null)
            {
                orbit.Realism = Mathf.LerpUnclamped(fromRealism, realism, k);
                orbit.Blend = Mathf.Clamp01(k);
            }
        }

        void Finish(float realism, bool orbiting)
        {
            Step(1f, realism, realism, orbiting);
            for (var i = 0; i < moves.Count; i++) Recapture(moves[i].anchor);
            Applied?.Invoke(Current);
        }

        static void Recapture(Transform anchor)
        {
            if (anchor == null) return;
            for (var i = 0; i < anchor.childCount; i++)
            {
                var grab = anchor.GetChild(i).GetComponent<Grabbable>();
                if (grab != null && !grab.Placed && !grab.isSelected) grab.CaptureHome();
            }
        }

        struct Move
        {
            public Transform anchor, rings;
            public Vector3 fromPosition, toPosition, fromScale, toScale, fromRing, toRing;
            public Quaternion fromRotation, toRotation;
        }
    }
}
