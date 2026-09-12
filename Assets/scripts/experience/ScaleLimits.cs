// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// How large and how small two hands may make this object, in metres of real width.
    ///
    /// The limits in the GDD are physical, not multipliers: a body may be grown from 5 cm to 3 m, a model from
    /// 40 cm to 4 m, a nebula from 30 cm to 2 m. Those are the sizes that keep an object reachable at one end
    /// and let Saturn's rings pass around the player at the other, and they mean nothing as scale factors,
    /// because a body's authored scale has no fixed relationship to how wide it looks.
    ///
    /// So the object is measured once, on the first clamp, and the metre limits are converted into scale on the
    /// spot. Measuring lazily rather than in Awake matters: bodies are authored at their orbit size and are
    /// grown by the force solver before the player can ever get two hands on them.
    /// </summary>
    [RequireComponent(typeof(ManipulationHandler))]
    public class ScaleLimits : MonoBehaviour, IManipulationScaleConstraint
    {
        public enum Kind
        {
            /// <summary>A planet, moon or the Sun, pulled out and held.</summary>
            Body,

            /// <summary>A whole model the player moves as one piece, like the orbit view.</summary>
            Model,

            /// <summary>A nebula or other destination overlay.</summary>
            Nebula,

            /// <summary>Use the metres set below.</summary>
            Custom
        }

        [SerializeField]
        [Tooltip("Picks the metre limits from the GDD. Custom uses the two values below.")]
        private Kind kind = Kind.Body;

        [SerializeField]
        [Tooltip("Smallest width in metres, when Kind is Custom.")]
        private float customMinMetres = 0.05f;

        [SerializeField]
        [Tooltip("Largest width in metres, when Kind is Custom.")]
        private float customMaxMetres = 3f;

        [SerializeField]
        [Tooltip("Measured from the renderers when empty. Set it for an object whose bounds are misleading.")]
        private Renderer measureFrom;

        private float _metresPerScaleUnit;

        /// <summary>Current width in metres, as the player sees it. Measures the object if it has not been yet.</summary>
        public float CurrentMetres
        {
            get
            {
                if (_metresPerScaleUnit <= 0f)
                {
                    Measure();
                }

                return transform.localScale.x * _metresPerScaleUnit;
            }
        }

        /// <summary>Metres of width per unit of local scale. Zero until the object can be measured.</summary>
        public float MetresPerScaleUnit
        {
            get
            {
                if (_metresPerScaleUnit <= 0f)
                {
                    Measure();
                }

                return _metresPerScaleUnit;
            }
        }

        public float MinMetres
        {
            get
            {
                switch (kind)
                {
                    case Kind.Body: return 0.05f;
                    case Kind.Model: return 0.4f;
                    case Kind.Nebula: return 0.3f;
                    default: return customMinMetres;
                }
            }
        }

        public float MaxMetres
        {
            get
            {
                switch (kind)
                {
                    case Kind.Body: return 3f;
                    case Kind.Model: return 4f;
                    case Kind.Nebula: return 2f;
                    default: return customMaxMetres;
                }
            }
        }

        public Vector3 ClampScale(Vector3 desiredLocalScale)
        {
            if (_metresPerScaleUnit <= 0f)
            {
                Measure();
                if (_metresPerScaleUnit <= 0f)
                {
                    return desiredLocalScale; // nothing to measure; better unbounded than frozen at zero
                }
            }

            var desiredMetres = desiredLocalScale.x * _metresPerScaleUnit;
            var clamped = Mathf.Clamp(desiredMetres, MinMetres, MaxMetres);
            if (Mathf.Approximately(clamped, desiredMetres))
            {
                return desiredLocalScale;
            }

            // Scale uniformly: a body pinned at its limit on one axis must not be squashed on the others.
            var factor = clamped / desiredMetres;
            return desiredLocalScale * factor;
        }

        /// <summary>Re-measures the object, for content that swaps its mesh at runtime.</summary>
        public void Remeasure() => Measure();

        private void Measure()
        {
            var scale = transform.localScale.x;
            if (Mathf.Approximately(scale, 0f))
            {
                return;
            }

            var bounds = MeasuredBounds();
            if (!bounds.HasValue)
            {
                return;
            }

            var widest = bounds.Value.size;
            _metresPerScaleUnit = Mathf.Max(widest.x, widest.y, widest.z) / scale;
        }

        private Bounds? MeasuredBounds()
        {
            if (measureFrom != null)
            {
                return measureFrom.bounds;
            }

            Bounds? total = null;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                // A halo, a label or a highlighter ring is much wider than the body it belongs to and would
                // make everything measure large; only real geometry counts.
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)
                {
                    continue;
                }

                total = total.HasValue ? Encapsulate(total.Value, r.bounds) : r.bounds;
            }

            return total;
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }
    }
}
