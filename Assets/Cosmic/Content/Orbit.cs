using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmic
{
    public class Orbit : MonoBehaviour
    {
        public const float TargetRealismPlanetScale = 0.0001f;

        const float EpochJulianDays = 2451545f;
        const float DaysPerSecond = 100f;
        const float RealKmToMetres = 4.231401e-11f;
        const float AnomalyEpsilon = 0.000001f;
        const int MaxIterations = 20;
        const float TwoPi = Mathf.PI * 2f;

        struct Elements
        {
            public float semiMajorMetres, semiMajorRealKm, realScale, eccentricity, eccentricityReal;
            public float inclinationRadians, inclinationRealRadians, argumentOfPerigeeRadians, ascendingNodeRadians;
            public float meanAnomalyRadians, periodDays, periodRealDays, speedMultiplier, speedMultiplierReal;
            public int steps;
        }

        struct MoonElements
        {
            public float radiusRatio, orbitSeconds, planeDegrees, phaseDegrees;
        }

        class Orbiter
        {
            public Elements elements;
            public Transform anchor;
            public double days;
        }

        class Rider
        {
            public MoonElements elements;
            public Transform planetAnchor, anchor;
            public float degrees;
        }

        static Elements Planet(float semiMajorMetres, float semiMajorRealKm, float eccentricityReal, float inclinationReal,
                               float periodDays, float meanAnomaly, int steps, float argumentOfPerigee = 0f, float ascendingNode = 0f) => new Elements
        {
            semiMajorMetres = semiMajorMetres, semiMajorRealKm = semiMajorRealKm, realScale = RealKmToMetres,
            eccentricityReal = eccentricityReal, inclinationRealRadians = inclinationReal, meanAnomalyRadians = meanAnomaly,
            argumentOfPerigeeRadians = argumentOfPerigee, ascendingNodeRadians = ascendingNode,
            periodDays = periodDays, periodRealDays = periodDays, speedMultiplier = 0.1f, speedMultiplierReal = 1f, steps = steps,
        };

        static Elements Star(float semiMajorMetres, float eccentricity, float argumentOfPerigee, float inclination,
                             float periodDays, float periodRealDays, int steps) => new Elements
        {
            semiMajorMetres = semiMajorMetres, semiMajorRealKm = semiMajorMetres, realScale = 1f,
            eccentricity = eccentricity, eccentricityReal = eccentricity, argumentOfPerigeeRadians = argumentOfPerigee,
            inclinationRadians = inclination, inclinationRealRadians = inclination,
            periodDays = periodDays, periodRealDays = periodRealDays, speedMultiplier = 0.08f, speedMultiplierReal = 0.08f, steps = steps,
        };

        static MoonElements Moon(float radiusRatio, float orbitSeconds, float planeDegrees, float phaseDegrees) =>
            new MoonElements { radiusRatio = radiusRatio, orbitSeconds = orbitSeconds, planeDegrees = planeDegrees, phaseDegrees = phaseDegrees };

        static readonly Dictionary<string, Elements> Table = new Dictionary<string, Elements>
        {
            { "mercury", Planet(0.2f, 57909070f, 0.2056303f, 0.1222606f, 87.9691f, 0f, 50) },
            { "venus", Planet(0.35f, 108208200f, 0.006755698f, 0.05924677f, 224.6983f, 0f, 100) },
            { "earth", Planet(0.5f, 149653500f, 0.0170424f, 0.00000467f, 365.4601f, 0f, 100) },
            { "mars", Planet(0.65f, 227939000f, 0.0933146f, 0.03228644f, 686.9707f, 0f, 90) },
            { "jupiter", Planet(0.95f, 778673200f, 0.04892307f, 0.02277056f, 4335.467f, 0f, 100) },
            { "saturn", Planet(1.2f, 1.433365e+9f, 0.05559913f, 0.04336025f, 10831.37f, 0f, 140) },
            { "uranus", Planet(1.4f, 2.876712e+9f, 0.04439848f, 0.01347816f, 30799.62f, 0f, 160) },
            { "neptune", Planet(1.6f, 4.50346e+9f, 0.01121432f, 0.03085696f, 60327.95f, 4.673694f, 180) },
            { "pluto", Planet(1.8f, 5.908209e+9f, 0.2477692f, 0.2996067f, 90655.48f, 0f, 200, 2.009103f, 1.924108f) },
            { "s2", Star(0.9f, 0.7f, 245.4f, -48.1f, 270f, 5902.05f, 400) },
            { "s102", Star(0.9f, 0.7f, 185f, 151f, 192.24f, 4202.25f, 269) },
        };

        static readonly Dictionary<string, MoonElements> MoonTable = new Dictionary<string, MoonElements>
        {
            { "moon", Moon(1.8f, 39.187054f, 7f, 0f) },
            { "phobos", Moon(0.96f, 8f, 7f, 0f) },
            { "deimos", Moon(1.24f, 8.428745f, -7f, 180f) },
            { "io", Moon(1.2f, 10.062305f, 7f, 0f) },
            { "europa", Moon(1.36f, 14.230249f, -7f, 90f) },
            { "ganymede", Moon(1.6f, 20.12461f, 7f, 180f) },
            { "callisto", Moon(2.08f, 30.649227f, -7f, 270f) },
            { "mimas", Moon(1.12f, 8f, 7f, 0f) },
            { "enceladus", Moon(1.28f, 8.87412f, -7f, 90f) },
            { "titan", Moon(1.92f, 29.906103f, 7f, 180f) },
            { "iapetus", Moon(2.4f, 60f, -7f, 270f) },
        };

        [SerializeField]
        Material ringMaterial;

        [SerializeField]
        Camera ringCamera;

        public bool Running = true;

        readonly List<Orbiter> orbiters = new List<Orbiter>();
        readonly List<Rider> riders = new List<Rider>();
        Transform sun, frameRoot;
        OrbitRings rings;
        bool sunShares;
        float realism;

        public float Realism { get => realism; set => realism = Mathf.Clamp01(value); }
        public float Alpha { get; set; } = 1f;
        public int Count => orbiters.Count;

        Vector3 SunOrigin => sunShares ? sun.localPosition : Vector3.zero;
        Quaternion SunFrame => sunShares ? sun.localRotation : Quaternion.identity;

        public float BodyScaleAt(float realism, float authoredScale) => Mathf.Lerp(authoredScale, TargetRealismPlanetScale, realism);

        // Anchors are siblings of the sun under one rig root, so an orbit is anchor.localPosition; a sun parented elsewhere counts as that root's origin.
        public void Bind(Transform sun, IList<(Body body, Transform anchor)> bodies)
        {
            this.sun = sun;
            orbiters.Clear();
            riders.Clear();
            var days = StartDays();
            string unknown = null;
            for (var i = 0; bodies != null && i < bodies.Count; i++)
            {
                var (body, anchor) = bodies[i];
                if (body == null || anchor == null)
                    continue;
                if (Table.TryGetValue(body.id ?? string.Empty, out var elements))
                    orbiters.Add(new Orbiter { elements = elements, anchor = anchor, days = days });
                else
                    unknown = unknown == null ? body.id : unknown + ", " + body.id;
            }
            frameRoot = orbiters.Count > 0 && orbiters[0].anchor != null ? orbiters[0].anchor.parent : null;
            sunShares = sun != null && frameRoot != null && sun.parent == frameRoot;
            if (unknown != null)
                Debug.LogWarning($"Orbit has no elements for {unknown}; those bodies stay where they are.", this);
            BuildRings();
        }

        public void BindMoons(IList<(Body moon, Transform planetAnchor, Transform moonAnchor)> moons)
        {
            riders.Clear();
            string unknown = null;
            for (var i = 0; moons != null && i < moons.Count; i++)
            {
                var (moon, planetAnchor, anchor) = moons[i];
                if (moon == null || planetAnchor == null || anchor == null)
                    continue;
                if (MoonTable.TryGetValue(moon.id ?? string.Empty, out var elements))
                    riders.Add(new Rider { elements = elements, planetAnchor = planetAnchor, anchor = anchor, degrees = elements.phaseDegrees });
                else
                    unknown = unknown == null ? moon.id : unknown + ", " + moon.id;
            }
            if (unknown != null)
                Debug.LogWarning($"Orbit has no moon orbit for {unknown}; those moons stay where they are.", this);
        }

        public void Sample(int bodyIndex, float t01, float realism, out Vector3 local)
        {
            local = Vector3.zero;
            if (bodyIndex < 0 || bodyIndex >= orbiters.Count)
                return;
            var elements = orbiters[bodyIndex].elements;
            local = PositionAt(elements, MeanAnomalyFor(elements, t01), realism);
        }

        void LateUpdate()
        {
            if (Running && sun != null)
            {
                var dt = Time.deltaTime;
                var origin = SunOrigin;
                var frame = SunFrame;
                for (var i = 0; i < orbiters.Count; i++)
                {
                    var orbiter = orbiters[i];
                    if (orbiter.anchor == null)
                        continue;
                    var elements = orbiter.elements;
                    orbiter.days += dt * DaysPerSecond * Mathf.Lerp(elements.speedMultiplier, elements.speedMultiplierReal, realism);
                    orbiter.anchor.localPosition = origin + frame * PositionAt(elements, MeanAnomalyAt(elements, orbiter.days, realism), realism);
                }
                for (var i = 0; i < riders.Count; i++)
                    Ride(riders[i], dt);
            }
            DrawRings();
        }

        void OnDisable() => rings?.Detach();

        void OnDestroy()
        {
            rings?.Release();
            rings = null;
        }

        static void Ride(Rider rider, float dt)
        {
            if (rider.anchor == null || rider.planetAnchor == null)
                return;
            if (rider.elements.orbitSeconds > 0f)
                rider.degrees += 360f / rider.elements.orbitSeconds * dt;
            var diameter = rider.planetAnchor.localScale.x;
            var radius = rider.elements.radiusRatio * (diameter > 0.0001f ? diameter : 1f);
            var turn = Quaternion.Euler(rider.elements.planeDegrees, 0f, 0f) * Quaternion.Euler(0f, rider.degrees, 0f);
            rider.anchor.localPosition = rider.planetAnchor.localPosition + turn * (Vector3.right * radius);
        }

        void DrawRings()
        {
            if (rings == null)
                return;
            for (var i = 0; i < orbiters.Count; i++)
            {
                var anchor = orbiters[i].anchor;
                rings.SetPlanet(i, anchor == null ? Vector4.zero : (Vector4)anchor.position);
            }
            if (ringCamera == null)
                ringCamera = Camera.main;
            var toWorld = (frameRoot == null ? Matrix4x4.identity : frameRoot.localToWorldMatrix)
                * Matrix4x4.TRS(SunOrigin, SunFrame, Vector3.one);
            rings.Draw(ringCamera, toWorld, realism, Alpha, frameRoot == null ? 1f : frameRoot.lossyScale.x);
        }

        void BuildRings()
        {
            rings?.Release();
            rings = null;
            if (orbiters.Count == 0 || ringMaterial == null)
                return;
            var points = new List<OrbitRings.OrbitPoint>();
            var spans = new OrbitRings.Span[orbiters.Count];
            for (var i = 0; i < orbiters.Count; i++)
            {
                var elements = orbiters[i].elements;
                var steps = Mathf.Max(4, elements.steps);
                var start = points.Count;
                for (var s = 0; s < steps - 1; s++)
                {
                    var mean = MeanAnomalyFor(elements, s / (float)steps);
                    points.Add(new OrbitRings.OrbitPoint { schematic = PositionAt(elements, mean, 0f), real = PositionAt(elements, mean, 1f) });
                }
                spans[i] = new OrbitRings.Span { start = start, count = points.Count - start, index = i };
            }
            rings = new OrbitRings(ringMaterial);
            rings.Build(points, spans);
        }

        static float MeanAnomalyFor(in Elements elements, float t01) => Mathf.Repeat(elements.meanAnomalyRadians - t01 * TwoPi, TwoPi);

        static float MeanAnomalyAt(in Elements elements, double days, float realism)
        {
            var period = Mathf.Lerp(elements.periodDays, elements.periodRealDays, realism);
            return MeanAnomalyFor(elements, period > 0f ? (float)(days % period) / period : 0f);
        }

        static Vector3 PositionAt(in Elements elements, float meanAnomaly, float realism)
        {
            var eccentricity = Mathf.Lerp(elements.eccentricity, elements.eccentricityReal, realism);
            var semiMajor = Mathf.Lerp(elements.semiMajorMetres, elements.semiMajorRealKm * elements.realScale, realism);
            var inclination = Mathf.Lerp(elements.inclinationRadians, elements.inclinationRealRadians, realism);
            var trueAnomaly = TrueAnomaly(meanAnomaly, eccentricity);
            var radius = semiMajor * (1f - eccentricity * eccentricity) / (1f + eccentricity * Mathf.Cos(trueAnomaly));
            var node = elements.ascendingNodeRadians;
            var angle = trueAnomaly + elements.argumentOfPerigeeRadians;
            return new Vector3(
                radius * (Mathf.Cos(node) * Mathf.Cos(angle) - Mathf.Sin(node) * Mathf.Sin(angle) * Mathf.Cos(inclination)),
                radius * (Mathf.Sin(angle) * Mathf.Sin(inclination)),
                -radius * (Mathf.Sin(node) * Mathf.Cos(angle) + Mathf.Cos(node) * Mathf.Sin(angle) * Mathf.Cos(inclination)));
        }

        static float TrueAnomaly(float meanAnomaly, float eccentricity)
        {
            float previous, next = meanAnomaly + eccentricity / 2f;
            var iterations = 0;
            do
            {
                iterations++;
                previous = next;
                next = previous - (previous - eccentricity * Mathf.Sin(previous) - meanAnomaly) / (1f - eccentricity * Mathf.Cos(previous));
            }
            while (Mathf.Abs(previous - next) > AnomalyEpsilon && iterations < MaxIterations);

            if (iterations == MaxIterations)
                return 0f;
            var cosEccentric = Mathf.Cos(next);
            var trueAnomaly = Mathf.Acos((cosEccentric - eccentricity) / (1f - eccentricity * cosEccentric));
            if (meanAnomaly > Mathf.PI)
                trueAnomaly = TwoPi - trueAnomaly;
            return float.IsNaN(trueAnomaly) ? 0f : trueAnomaly;
        }

        static double StartDays()
        {
            var now = DateTime.Now;
            var month = now.Month < 3 ? now.Month + 12 : now.Month;
            var year = now.Month < 3 ? now.Year - 1 : now.Year;
            var julianDay = now.Day + (153 * month - 457) / 5 + 365 * year + year / 4 - year / 100 + year / 400 + 1721119;
            return julianDay + (now.Hour - 12) / 24.0 + now.Minute / 1440.0 + now.Second / 86400.0 - EpochJulianDays;
        }
    }
}
