using System;
using UnityEngine;

namespace Cosmic
{
    public static class WebSource
    {
        public static StarVert[] Web(in PointSources.WebSettings settings) => new Builder(settings).Generate();

        sealed class Builder
        {
            const int CandidatesPerPoint = 24;
            const int SiteMargin = 4;

            enum Piece { Void, Wall, Filament, Node }

            readonly PointSources.WebSettings settings;
            readonly float cellSize, origin, tolerance, maxProjection;
            readonly float voidCut, wallCut, filamentCut;
            readonly uint seed;
            readonly int span;
            readonly Vector3[] sites;
            readonly Vector3[] near = new Vector3[4];
            readonly float[] nearDistance = new float[4];
            uint state;

            public Builder(PointSources.WebSettings s)
            {
                s.pointCount = Mathf.Max(0, s.pointCount);
                s.radiusMetres = Mathf.Max(0.01f, s.radiusMetres);
                s.cellsPerAxis = Mathf.Clamp(s.cellsPerAxis, 2, 64);
                s.siteJitter = Mathf.Clamp(s.siteJitter, 0f, 0.5f);
                settings = s;
                cellSize = 2f * s.radiusMetres / s.cellsPerAxis;
                origin = -s.radiusMetres;
                tolerance = Mathf.Max(1e-5f, s.snapTolerance) * cellSize;
                maxProjection = 2f * cellSize;

                var wall = Mathf.Max(0f, s.wallShare);
                var filament = Mathf.Max(0f, s.filamentShare);
                var node = Mathf.Max(0f, s.nodeShare);
                var empty = Mathf.Max(0f, 1f - wall - filament - node);
                var total = Mathf.Max(1e-5f, empty + wall + filament + node);
                voidCut = empty / total;
                wallCut = voidCut + wall / total;
                filamentCut = wallCut + filament / total;

                seed = unchecked((uint)s.seed);
                state = Hash(seed ^ 0x5bf03635u);
                if (state == 0u) state = 0x9e3779b9u;
                span = s.cellsPerAxis + 2 * SiteMargin;
                sites = BuildSites();
            }

            public StarVert[] Generate()
            {
                var points = new StarVert[settings.pointCount];
                var budget = settings.pointCount * CandidatesPerPoint + 1024;
                var written = 0;
                for (var candidate = 0; written < points.Length && candidate < budget; candidate++)
                {
                    if (TryMakePoint(out var point)) points[written++] = point;
                }

                if (written == points.Length) return points;
                var trimmed = new StarVert[written];
                Array.Copy(points, trimmed, written);
                return trimmed;
            }

            bool TryMakePoint(out StarVert point)
            {
                point = default;
                var p = RandomInBall() * settings.radiusMetres;
                var roll = NextFloat();
                Piece piece;

                if (roll < voidCut)
                {
                    piece = Piece.Void;
                }
                else
                {
                    var start = p;
                    Nearest(p);
                    Vector3 s1 = near[0], s2 = near[1], s3 = near[2], s4 = near[3];
                    p = ProjectOntoPlane(p, Midpoint(s1, s2), s2 - s1);
                    float thickness;
                    if (roll < wallCut)
                    {
                        piece = Piece.Wall;
                        thickness = settings.wallThickness;
                    }
                    else if (roll < filamentCut)
                    {
                        if (!ProjectOntoEdge(ref p, s1, s2, s3)) return false;
                        piece = Piece.Filament;
                        thickness = settings.filamentRadius;
                    }
                    else
                    {
                        if (!ProjectOntoEdge(ref p, s1, s2, s3)) return false;
                        if (!ProjectOntoVertex(ref p, s1, s2, s3, s4)) return false;
                        piece = Piece.Node;
                        thickness = settings.nodeRadius;
                    }
                    if ((p - start).sqrMagnitude > maxProjection * maxProjection) return false;
                    Nearest(p);
                    var reached = piece == Piece.Wall ? nearDistance[1] - nearDistance[0]
                        : piece == Piece.Filament ? nearDistance[2] - nearDistance[0]
                        : nearDistance[3] - nearDistance[0];
                    if (reached > tolerance) return false;
                    p += RandomInBall() * (thickness * cellSize);
                }

                var distance = p.magnitude;
                if (distance > settings.radiusMetres) return false;

                var rim = Mathf.InverseLerp(settings.radiusMetres, settings.rimFadeStart * settings.radiusMetres, distance);
                if (NextFloat() > Mathf.Clamp01(rim)) return false;

                point = Describe(p, piece);
                return true;
            }

            StarVert Describe(Vector3 p, Piece piece)
            {
                var index = (int)piece;
                var ramp = index == 1 ? 0.30f : index == 2 ? 0.62f : index == 3 ? 1.00f : 0.00f;
                ramp = Mathf.Clamp01(ramp + NextRange(-0.06f, 0.06f));
                var tile = NextUInt() & 3u;

                var point = new StarVert
                {
                    ellipseDistance = Mathf.Sqrt(p.x * p.x + p.z * p.z),
                    curveOffset = Mathf.Atan2(p.z, p.x),
                    yOffset = p.y,
                    ellipseOffset = ramp,
                    color = Vector3.one * (settings.brightness[index] * NextRange(0.80f, 1.25f)),
                    uv = new Vector2((tile & 1u) == 0u ? 0f : 0.5f, (tile & 2u) == 0u ? 0f : 0.5f),
                    size = settings.sizes[index] * NextRange(0.75f, 1.35f),
                };

                // The old bake drew a per-point shimmer phase here; the draw stays so every later point matches it.
                NextFloat();
                return point;
            }

            void Nearest(Vector3 p)
            {
                var gx = Mathf.FloorToInt((p.x - origin) / cellSize);
                var gy = Mathf.FloorToInt((p.y - origin) / cellSize);
                var gz = Mathf.FloorToInt((p.z - origin) / cellSize);
                for (var i = 0; i < 4; i++)
                {
                    nearDistance[i] = float.MaxValue;
                    near[i] = Vector3.zero;
                }

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    var site = Site(gx + dx, gy + dy, gz + dz);
                    var ox = site.x - p.x;
                    var oy = site.y - p.y;
                    var oz = site.z - p.z;
                    var q = ox * ox + oy * oy + oz * oz;
                    for (var k = 0; k < 4; k++)
                    {
                        if (q >= nearDistance[k]) continue;
                        for (var m = 3; m > k; m--)
                        {
                            nearDistance[m] = nearDistance[m - 1];
                            near[m] = near[m - 1];
                        }
                        nearDistance[k] = q;
                        near[k] = site;
                        break;
                    }
                }

                for (var i = 0; i < 4; i++) nearDistance[i] = Mathf.Sqrt(nearDistance[i]);
            }

            Vector3 Site(int ix, int iy, int iz) => sites[(Clamp(ix) * span + Clamp(iy)) * span + Clamp(iz)];

            int Clamp(int cell)
            {
                var i = cell + SiteMargin;
                return i < 0 ? 0 : i >= span ? span - 1 : i;
            }

            Vector3[] BuildSites()
            {
                var table = new Vector3[span * span * span];
                var jitter = settings.siteJitter;
                for (var i = 0; i < table.Length; i++)
                {
                    var ix = i / (span * span) - SiteMargin;
                    var iy = i / span % span - SiteMargin;
                    var iz = i % span - SiteMargin;
                    var h = Hash(unchecked((uint)ix * 73856093u) ^ unchecked((uint)iy * 19349663u) ^
                                 unchecked((uint)iz * 83492791u) ^ seed);
                    var jx = Unit(h);
                    h = Hash(h);
                    var jy = Unit(h);
                    h = Hash(h);
                    var jz = Unit(h);
                    table[i] = new Vector3(
                        origin + (ix + 0.5f + jitter * (jx * 2f - 1f)) * cellSize,
                        origin + (iy + 0.5f + jitter * (jy * 2f - 1f)) * cellSize,
                        origin + (iz + 0.5f + jitter * (jz * 2f - 1f)) * cellSize);
                }

                return table;
            }

            static Vector3 Midpoint(Vector3 a, Vector3 b) => (a + b) * 0.5f;

            static Vector3 ProjectOntoPlane(Vector3 p, Vector3 through, Vector3 normal)
            {
                var n = normal.normalized;
                return p - n * Vector3.Dot(p - through, n);
            }

            static bool ProjectOntoEdge(ref Vector3 p, Vector3 s1, Vector3 s2, Vector3 s3)
            {
                var nA = (s2 - s1).normalized;
                var nB = (s3 - s1).normalized;
                var tangent = nB - nA * Vector3.Dot(nB, nA);
                var along = Vector3.Dot(tangent, nB);
                if (Mathf.Abs(along) < 1e-4f) return false;
                p -= tangent * (Vector3.Dot(p - Midpoint(s1, s3), nB) / along);
                return true;
            }

            static bool ProjectOntoVertex(ref Vector3 p, Vector3 s1, Vector3 s2, Vector3 s3, Vector3 s4)
            {
                var direction = Vector3.Cross(s2 - s1, s3 - s1);
                if (direction.sqrMagnitude < 1e-10f) return false;
                direction = direction.normalized;
                var nC = (s4 - s1).normalized;
                var along = Vector3.Dot(direction, nC);
                if (Mathf.Abs(along) < 1e-4f) return false;
                p -= direction * (Vector3.Dot(p - Midpoint(s1, s4), nC) / along);
                return true;
            }

            Vector3 RandomInBall()
            {
                for (var attempt = 0; attempt < 16; attempt++)
                {
                    var v = new Vector3(NextRange(-1f, 1f), NextRange(-1f, 1f), NextRange(-1f, 1f));
                    if (v.sqrMagnitude <= 1f) return v;
                }

                return Vector3.zero;
            }

            uint NextUInt()
            {
                unchecked
                {
                    var x = state;
                    x ^= x << 13;
                    x ^= x >> 17;
                    x ^= x << 5;
                    state = x;
                    return x;
                }
            }

            float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

            float NextRange(float min, float max) => min + (max - min) * NextFloat();

            static uint Hash(uint x)
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

            static float Unit(uint h) => (h >> 8) * (1f / 16777216f);
        }
    }
}
