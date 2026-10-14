// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Point cloud for the Cosmic Web (GDD 4.7): <see cref="StarVertDescriptor"/>s for the same buffer and quad
    /// shader the galaxy uses. Deterministic from <see cref="Settings.Seed"/> alone.
    /// </summary>
    // The struct carries a cylindrical position so the buffer stride matches the galaxy's: ellipseDistance = radius,
    // curveOffset = angle in radians (the shader adds _Age, giving the drift without touching the transform), yOffset
    // = height, metres at local scale 1; ellipseOffset = 0..1 on the ramp; color = brightness only, hue is the material's.
    // Voids are Voronoi cells; points are projected exactly onto bisector planes, lines and points (walls, filaments,
    // nodes) rather than rejection-sampled, then kept only if their sites are still the nearest.
    public static class CosmicWebGenerator
    {
        /// <summary>Which piece of the tessellation a point belongs to. Drives its size, brightness and ramp.</summary>
        public enum Structure
        {
            /// <summary>A lone tracer inside a void.</summary>
            Void = 0,

            /// <summary>On a wall between two voids: a sheet.</summary>
            Wall = 1,

            /// <summary>On an edge where three walls meet: a filament.</summary>
            Filament = 2,

            /// <summary>On a vertex where four voids meet: a cluster.</summary>
            Node = 3,
        }

        /// <summary>Everything that shapes the web. All lengths are metres at the volume's local scale 1.</summary>
        [Serializable]
        public struct Settings
        {
            public int Seed;
            public int PointCount;

            /// <summary>Half-width of the volume in metres at local scale 1. The GDD asks for a 5 m volume.</summary>
            public float Radius;

            /// <summary>Voronoi sites along one side of the volume's bounding cube. More cells, finer web.</summary>
            public int CellsPerAxis;

            /// <summary>How far a site may wander from its cell centre, as a fraction of the cell. Keep at or below 0.5.</summary>
            public float SiteJitter;

            /// <summary>Share of candidates, normalised; not of the result, since vertex projections are rejected far more often.</summary>
            public float VoidShare, WallShare, FilamentShare, NodeShare;

            /// <summary>Thickness of each structure, as a fraction of a cell.</summary>
            public float WallThickness, FilamentRadius, NodeRadius;

            /// <summary>How near equidistant counts as on the tessellation, as a fraction of a cell. Larger is blurrier.</summary>
            public float SnapTolerance;

            /// <summary>Density falls off from here (0..1 of the radius) to nothing at the rim, so the volume has no visible edge.</summary>
            public float RimFadeStart;

            /// <summary>Sprite size multiplier for each structure, before the per-point random spread.</summary>
            public float VoidSize, WallSize, FilamentSize, NodeSize;

            /// <summary>Brightness multiplier for each structure. The hue is the material's, not ours.</summary>
            public float VoidBrightness, WallBrightness, FilamentBrightness, NodeBrightness;

            /// <summary>The CS-065 tuning, with the point count from <see cref="CosmicWebRenderer.DefaultPointCount"/>.</summary>
            public static Settings Default => new Settings
            {
                Seed = 20260912,
                PointCount = CosmicWebRenderer.DefaultPointCount,
                Radius = 2.5f,
                CellsPerAxis = 7,
                SiteJitter = 0.45f,
                VoidShare = 0.05f,
                WallShare = 0.25f,
                FilamentShare = 0.50f,
                NodeShare = 0.20f,
                WallThickness = 0.035f,
                FilamentRadius = 0.030f,
                NodeRadius = 0.070f,
                SnapTolerance = 0.05f,
                RimFadeStart = 0.80f,
                VoidSize = 0.45f,
                WallSize = 0.70f,
                FilamentSize = 1.00f,
                NodeSize = 1.90f,
                VoidBrightness = 0.45f,
                WallBrightness = 0.75f,
                FilamentBrightness = 1.00f,
                NodeBrightness = 1.50f,
            };
        }

        /// <summary>Generates the whole cloud in one go. Use <see cref="Builder"/> to spread it over frames.</summary>
        public static StarVertDescriptor[] Generate(Settings settings)
        {
            var builder = new Builder(settings);
            var points = new StarVertDescriptor[builder.Capacity];
            while (!builder.Done)
            {
                builder.Step(points, 0f);
            }

            if (builder.Count == points.Length)
            {
                return points;
            }

            var trimmed = new StarVertDescriptor[builder.Count];
            Array.Copy(points, trimmed, builder.Count);
            return trimmed;
        }

        /// <summary>
        /// Resumable generator: <see cref="Step"/> fills the caller's array within a millisecond budget, so the web
        /// condenses into view instead of stalling the headset.
        /// </summary>
        public sealed class Builder
        {
            // Every accepted point costs two 27-site scans and some are rejected, so a hard ceiling on tries is
            // the only thing standing between a badly tuned Settings and a hang. Running short is reported.
            private const int CandidatesPerPoint = 24;

            // Checking a Stopwatch per candidate would cost more than the candidate. 512 is about a tenth of a
            // millisecond's work, fine enough that a 3 ms budget is not overshot in any way a frame notices.
            private const int TimeCheckInterval = 512;

            private readonly Settings _settings;
            private readonly float _cellSize;
            private readonly float _origin;       // one corner of the grid; the volume is centred on zero
            private readonly float _tolerance;    // metres
            private readonly float _maxProjection;
            private readonly float _voidCut, _wallCut, _filamentCut; // cumulative, normalised share thresholds
            private readonly uint _seed;

            // A projection may carry a candidate up to two cells outside the volume, and the search around it
            // then reaches one further, so the site table has to extend a few cells past the grid on each side.
            private const int SiteMargin = 4;

            private readonly int _span;
            private readonly Vector3[] _sites;

            private Rng _rng;
            private int _written;
            private int _candidates;

            public Builder(Settings settings)
            {
                settings.PointCount = Mathf.Max(0, settings.PointCount);
                settings.Radius = Mathf.Max(0.01f, settings.Radius);
                settings.CellsPerAxis = Mathf.Clamp(settings.CellsPerAxis, 2, 64);

                // Beyond half a cell a site can escape the 3x3x3 neighbourhood its cell is scanned with, and
                // the "four nearest" would quietly stop being the four nearest.
                settings.SiteJitter = Mathf.Clamp(settings.SiteJitter, 0f, 0.5f);

                _settings = settings;
                _cellSize = 2f * settings.Radius / settings.CellsPerAxis;
                _origin = -settings.Radius;
                _tolerance = Mathf.Max(1e-5f, settings.SnapTolerance) * _cellSize;

                // A bisector pair that is nearly parallel intersects a long way away. Catching that by distance
                // is one subtraction; catching it by the validation scan costs a full 27-site pass.
                _maxProjection = 2f * _cellSize;

                var total = Mathf.Max(1e-5f,
                    Mathf.Max(0f, settings.VoidShare) + Mathf.Max(0f, settings.WallShare) +
                    Mathf.Max(0f, settings.FilamentShare) + Mathf.Max(0f, settings.NodeShare));
                _voidCut = Mathf.Max(0f, settings.VoidShare) / total;
                _wallCut = _voidCut + Mathf.Max(0f, settings.WallShare) / total;
                _filamentCut = _wallCut + Mathf.Max(0f, settings.FilamentShare) / total;

                _seed = unchecked((uint)settings.Seed);
                _rng = new Rng(Hash(_seed ^ 0x5bf03635u));

                _span = settings.CellsPerAxis + 2 * SiteMargin;
                _sites = BuildSites();
            }

            /// <summary>How many points the caller's array must hold.</summary>
            public int Capacity => _settings.PointCount;

            /// <summary>How many points have been written so far.</summary>
            public int Count => _written;

            /// <summary>True once the cloud is complete, or once the candidate budget ran out.</summary>
            public bool Done => _written >= _settings.PointCount || _candidates >= Budget;

            /// <summary>True when the builder gave up before filling the array; the web is real but thinner than asked.</summary>
            public bool RanShort => _written < _settings.PointCount && _candidates >= Budget;

            private int Budget => _settings.PointCount * CandidatesPerPoint + 1024;

            /// <summary>Appends points from <see cref="Count"/>; returns how many. A budget of zero or less runs to completion.</summary>
            public int Step(StarVertDescriptor[] into, float millisecondBudget)
            {
                if (into == null)
                {
                    throw new ArgumentNullException(nameof(into));
                }

                var start = _written;
                var limit = Mathf.Min(_settings.PointCount, into.Length);
                var timed = millisecondBudget > 0f;
                var watch = timed ? Stopwatch.StartNew() : null;
                var sinceCheck = 0;

                while (_written < limit && _candidates < Budget)
                {
                    _candidates++;
                    if (TryMakePoint(out var point))
                    {
                        into[_written++] = point;
                    }

                    if (!timed || ++sinceCheck < TimeCheckInterval)
                    {
                        continue;
                    }

                    sinceCheck = 0;
                    if (watch.Elapsed.TotalMilliseconds >= millisecondBudget)
                    {
                        break;
                    }
                }

                return _written - start;
            }

            // ---------- one point

            private bool TryMakePoint(out StarVertDescriptor point)
            {
                point = default;

                var p = RandomInBall() * _settings.Radius;
                var roll = _rng.NextFloat();

                Structure structure;
                float thickness;
                if (roll < _voidCut)
                {
                    structure = Structure.Void;
                    thickness = 0f;
                }
                else
                {
                    var origin = p;
                    Nearest(p, out var s1, out var s2, out var s3, out var s4, out _, out _, out _, out _);

                    p = ProjectOntoPlane(p, Midpoint(s1, s2), s2 - s1);

                    if (roll < _wallCut)
                    {
                        structure = Structure.Wall;
                        thickness = _settings.WallThickness;
                    }
                    else if (roll < _filamentCut)
                    {
                        if (!ProjectOntoEdge(ref p, s1, s2, s3))
                        {
                            return false;
                        }

                        structure = Structure.Filament;
                        thickness = _settings.FilamentRadius;
                    }
                    else
                    {
                        if (!ProjectOntoEdge(ref p, s1, s2, s3) || !ProjectOntoVertex(ref p, s1, s2, s3, s4))
                        {
                            return false;
                        }

                        structure = Structure.Node;
                        thickness = _settings.NodeRadius;
                    }

                    if ((p - origin).sqrMagnitude > _maxProjection * _maxProjection)
                    {
                        return false;
                    }

                    // Projections land on infinite planes/lines; only points whose sites are still the nearest are on a
                    // real face. Measured before the thickness jitter, which would carry a node off its own vertex.
                    Nearest(p, out _, out _, out _, out _, out var d1, out var d2, out var d3, out var d4);
                    var reached = structure == Structure.Wall ? d2 - d1
                        : structure == Structure.Filament ? d3 - d1
                        : d4 - d1;
                    if (reached > _tolerance)
                    {
                        return false;
                    }

                    p += RandomInBall() * (thickness * _cellSize);
                }

                var distance = p.magnitude;
                if (distance > _settings.Radius)
                {
                    return false;
                }

                // No hard sphere edge: the outermost points thin out instead of stopping.
                var rim = Mathf.InverseLerp(_settings.Radius, _settings.RimFadeStart * _settings.Radius, distance);
                if (_rng.NextFloat() > Mathf.Clamp01(rim))
                {
                    return false;
                }

                point = Describe(p, structure);
                return true;
            }

            private StarVertDescriptor Describe(Vector3 p, Structure structure)
            {
                float ramp, size, brightness;
                switch (structure)
                {
                    case Structure.Wall:
                        ramp = 0.30f;
                        size = _settings.WallSize;
                        brightness = _settings.WallBrightness;
                        break;
                    case Structure.Filament:
                        ramp = 0.62f;
                        size = _settings.FilamentSize;
                        brightness = _settings.FilamentBrightness;
                        break;
                    case Structure.Node:
                        ramp = 1.00f;
                        size = _settings.NodeSize;
                        brightness = _settings.NodeBrightness;
                        break;
                    default:
                        ramp = 0.00f;
                        size = _settings.VoidSize;
                        brightness = _settings.VoidBrightness;
                        break;
                }

                // A little spread on the ramp coordinate, or the three classes read as three flat colour bands
                // instead of a gradient.
                ramp = Mathf.Clamp01(ramp + _rng.NextRange(-0.06f, 0.06f));

                var tile = _rng.NextUInt() & 3u;

                return new StarVertDescriptor
                {
                    ellipseDistance = Mathf.Sqrt(p.x * p.x + p.z * p.z),
                    curveOffset = Mathf.Atan2(p.z, p.x),
                    yOffset = p.y,
                    ellipseOffset = ramp,
                    color = Vector3.one * (brightness * _rng.NextRange(0.80f, 1.25f)),
                    uv = new Vector2((tile & 1u) == 0u ? 0f : 0.5f, (tile & 2u) == 0u ? 0f : 0.5f),
                    size = size * _rng.NextRange(0.75f, 1.35f),
                    random = _rng.NextFloat(),
                };
            }

            // ---------- the tessellation

            // Scanning only the 3x3x3 neighbourhood is exact while site jitter stays at or below half a cell.
            private void Nearest(Vector3 p,
                out Vector3 s1, out Vector3 s2, out Vector3 s3, out Vector3 s4,
                out float d1, out float d2, out float d3, out float d4)
            {
                var gx = Mathf.FloorToInt((p.x - _origin) / _cellSize);
                var gy = Mathf.FloorToInt((p.y - _origin) / _cellSize);
                var gz = Mathf.FloorToInt((p.z - _origin) / _cellSize);

                float q1 = float.MaxValue, q2 = float.MaxValue, q3 = float.MaxValue, q4 = float.MaxValue;
                s1 = s2 = s3 = s4 = Vector3.zero;

                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dz = -1; dz <= 1; dz++)
                        {
                            var site = Site(gx + dx, gy + dy, gz + dz);
                            var ox = site.x - p.x;
                            var oy = site.y - p.y;
                            var oz = site.z - p.z;
                            var q = ox * ox + oy * oy + oz * oz;

                            if (q < q1)
                            {
                                q4 = q3; s4 = s3;
                                q3 = q2; s3 = s2;
                                q2 = q1; s2 = s1;
                                q1 = q; s1 = site;
                            }
                            else if (q < q2)
                            {
                                q4 = q3; s4 = s3;
                                q3 = q2; s3 = s2;
                                q2 = q; s2 = site;
                            }
                            else if (q < q3)
                            {
                                q4 = q3; s4 = s3;
                                q3 = q; s3 = site;
                            }
                            else if (q < q4)
                            {
                                q4 = q; s4 = site;
                            }
                        }
                    }
                }

                d1 = Mathf.Sqrt(q1);
                d2 = Mathf.Sqrt(q2);
                d3 = Mathf.Sqrt(q3);
                d4 = Mathf.Sqrt(q4);
            }

            // Clamped, not wrapped: only a point already outside the volume reaches past the table, and it is rejected.
            private Vector3 Site(int ix, int iy, int iz) =>
                _sites[(Clamp(ix) * _span + Clamp(iy)) * _span + Clamp(iz)];

            private int Clamp(int cell)
            {
                var i = cell + SiteMargin;
                return i < 0 ? 0 : i >= _span ? _span - 1 : i;
            }

            // Hashed once up front: the nearest-four search runs millions of times and would otherwise hash 27 sites each.
            private Vector3[] BuildSites()
            {
                var sites = new Vector3[_span * _span * _span];
                var jitter = _settings.SiteJitter;

                for (var x = 0; x < _span; x++)
                {
                    var ix = x - SiteMargin;
                    for (var y = 0; y < _span; y++)
                    {
                        var iy = y - SiteMargin;
                        for (var z = 0; z < _span; z++)
                        {
                            var iz = z - SiteMargin;

                            var h = Hash(unchecked((uint)ix * 73856093u) ^ unchecked((uint)iy * 19349663u) ^
                                         unchecked((uint)iz * 83492791u) ^ _seed);

                            var jx = Unit(h);
                            h = Hash(h);
                            var jy = Unit(h);
                            h = Hash(h);
                            var jz = Unit(h);

                            sites[(x * _span + y) * _span + z] = new Vector3(
                                _origin + (ix + 0.5f + jitter * (jx * 2f - 1f)) * _cellSize,
                                _origin + (iy + 0.5f + jitter * (jy * 2f - 1f)) * _cellSize,
                                _origin + (iz + 0.5f + jitter * (jz * 2f - 1f)) * _cellSize);
                        }
                    }
                }

                return sites;
            }

            private static Vector3 Midpoint(Vector3 a, Vector3 b) => (a + b) * 0.5f;

            private static Vector3 ProjectOntoPlane(Vector3 p, Vector3 through, Vector3 normal)
            {
                var n = normal.normalized;
                return p - n * Vector3.Dot(p - through, n);
            }

            // Slides a point on the (s1,s2) wall along it onto the (s1,s3) wall: their shared Voronoi edge.
            // Fails when the walls are near parallel.
            private static bool ProjectOntoEdge(ref Vector3 p, Vector3 s1, Vector3 s2, Vector3 s3)
            {
                var nA = (s2 - s1).normalized;
                var nB = (s3 - s1).normalized;

                // The part of B's normal that lies in wall A: moving along it keeps us on A.
                var tangent = nB - nA * Vector3.Dot(nB, nA);
                var along = Vector3.Dot(tangent, nB);
                if (Mathf.Abs(along) < 1e-4f)
                {
                    return false;
                }

                p -= tangent * (Vector3.Dot(p - Midpoint(s1, s3), nB) / along);
                return true;
            }

            // Slides a point on the (s1,s2,s3) edge along it onto the (s1,s4) wall: the Voronoi vertex.
            private static bool ProjectOntoVertex(ref Vector3 p, Vector3 s1, Vector3 s2, Vector3 s3, Vector3 s4)
            {
                var direction = Vector3.Cross(s2 - s1, s3 - s1);
                if (direction.sqrMagnitude < 1e-10f)
                {
                    return false;
                }

                direction = direction.normalized;
                var nC = (s4 - s1).normalized;
                var along = Vector3.Dot(direction, nC);
                if (Mathf.Abs(along) < 1e-4f)
                {
                    return false;
                }

                p -= direction * (Vector3.Dot(p - Midpoint(s1, s4), nC) / along);
                return true;
            }

            // ---------- randomness

            /// <summary>A point in the unit ball, by rejection: three multiplies beats a cube root and two trig calls.</summary>
            private Vector3 RandomInBall()
            {
                for (var attempt = 0; attempt < 16; attempt++)
                {
                    var v = new Vector3(
                        _rng.NextRange(-1f, 1f),
                        _rng.NextRange(-1f, 1f),
                        _rng.NextRange(-1f, 1f));
                    if (v.sqrMagnitude <= 1f)
                    {
                        return v;
                    }
                }

                return Vector3.zero;
            }
        }

        // ---------- deterministic hashing, shared by the sites and the stream

        /// <summary>A 32-bit avalanche (the "lowbias32" finaliser). Cheap, and good enough that neighbouring cells look unrelated.</summary>
        private static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>A hash turned into 0..1, using the top 24 bits: the low bits of an xorshift are the weak ones.</summary>
        private static float Unit(uint h) => (h >> 8) * (1f / 16777216f);

        // xorshift32, not UnityEngine.Random: shared global state would make the web depend on what ran first.
        private struct Rng
        {
            private uint _state;

            public Rng(uint seed)
            {
                _state = seed == 0u ? 0x9e3779b9u : seed;
            }

            public uint NextUInt()
            {
                unchecked
                {
                    var x = _state;
                    x ^= x << 13;
                    x ^= x >> 17;
                    x ^= x << 5;
                    _state = x;
                    return x;
                }
            }

            public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

            public float NextRange(float min, float max) => min + (max - min) * NextFloat();
        }
    }
}
