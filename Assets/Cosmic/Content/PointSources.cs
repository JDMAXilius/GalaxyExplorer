using System;
using UnityEngine;
using Random = System.Random;

namespace Cosmic
{
    public static class PointSources
    {
        public enum NebulaShape { Shell, Bipolar, Cloud }

        static readonly Vector2 TilePoint = new Vector2(0.5f, 0f);
        static readonly Vector2 TilePuffy = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TileRayed = new Vector2(0f, 0.5f);
        static readonly Vector2 TileCross = new Vector2(0f, 0f);

        public static StarVert[] Spiral(Galaxy galaxy, in GalaxyLayerSpec layer)
        {
            var arms = Mathf.Max(1, galaxy.armCount);
            var rng = new Random(layer.seed);
            var points = new StarVert[Mathf.Max(0, layer.ellipses * layer.starsPerEllipse * arms)];
            var n = 0;
            for (var arm = 0; arm < arms; arm++)
            {
                var armOffsetDegrees = galaxy.ArmSpacingDegrees * arm;
                for (var i = 0; i < layer.ellipses; i++)
                {
                    var p = i / (float)layer.ellipses;
                    var offsetRadians = (armOffsetDegrees + galaxy.windingDegrees * p) * Mathf.Deg2Rad;
                    var baseDistance = Mathf.Lerp(layer.minEllipseScale, layer.maxEllipseScale, p);
                    for (var j = 0; j < layer.starsPerEllipse; j++)
                    {
                        points[n++] = MakeStar(galaxy, layer, rng, p, offsetRadians, baseDistance);
                    }
                }
            }

            return points;
        }

        public static float Envelope(StarVert[] points, Galaxy galaxy)
        {
            var reach = 0f;
            if (points != null)
            {
                for (var i = 0; i < points.Length; i++)
                {
                    reach = Mathf.Max(reach, points[i].ellipseDistance);
                }
            }

            return reach * Mathf.Max(galaxy.xRadii, galaxy.zRadii);
        }

        public static StarVert[] Nebula(float[] luminance, int width, int height, Color[] plate,
            NebulaShape shape, float thicknessRatio, int seed, int count = 28000) =>
            NebulaSource.Nebula(luminance, width, height, plate, shape, thicknessRatio, seed, count);

        public static StarVert[] Web(in WebSettings settings) => WebSource.Web(settings);

        public static int Seed(string id)
        {
            unchecked
            {
                var hash = 2166136261u;
                var length = id == null ? 0 : id.Length;
                for (var i = 0; i < length; i++)
                {
                    hash = (hash ^ (uint)id[i]) * 16777619u;
                }

                return (int)hash;
            }
        }

        static StarVert MakeStar(Galaxy galaxy, in GalaxyLayerSpec layer, Random rng,
            float p, float offsetRadians, float baseDistance)
        {
            var distance = baseDistance * Mathf.Lerp(layer.fuzz.x, layer.fuzz.y, Next(rng));
            var thickness = Mathf.Lerp(layer.thicknessAtCentre, layer.thicknessAtRim, Mathf.SmoothStep(0f, 1f, p))
                            * layer.yRange;
            Shade(galaxy.palette, layer, rng, p, out var colour, out var size, out var tile);

            var star = new StarVert
            {
                curveOffset = Next(rng) * Mathf.PI * 2f,
                ellipseOffset = offsetRadians,
                ellipseDistance = distance,
                yOffset = (Next(rng) * 2f - 1f) * thickness,
                color = new Vector3(colour.r, colour.g, colour.b),
                uv = tile,
                size = size,
            };

            // The old bake drew a per-point `random` here; the draw stays so every later point matches it.
            Next(rng);
            return star;
        }

        static void Shade(GalaxyPalette palette, in GalaxyLayerSpec layer, Random rng, float p,
            out Color colour, out float size, out Vector2 tile)
        {
            switch (layer.layer)
            {
                case GalaxyLayer.Clouds:
                    colour = Ramp(palette.clouds, p) * Mathf.Lerp(1f, 0.45f, p);
                    size = Mathf.Lerp(1.6f, 3.4f, p) * Mathf.Lerp(0.85f, 1.15f, Next(rng));
                    tile = TilePuffy;
                    return;

                case GalaxyLayer.Dust:
                {
                    var fromPeak = (p - layer.dustRingPeak) / Mathf.Max(0.0001f, layer.dustRingWidth);
                    var weight = Mathf.Lerp(0.35f, 1f, Mathf.Exp(-fromPeak * fromPeak));
                    colour = Ramp(palette.dust, p) * weight;
                    size = Mathf.Lerp(1.4f, 3f, Next(rng)) * weight;
                    tile = TilePuffy;
                    return;
                }

                default:
                {
                    var falloff = Mathf.Lerp(1f, 0.55f, p);
                    var roll = Next(rng);

                    if (roll < 0.86f)
                    {
                        colour = Ramp(palette.disc, p);
                        size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff;
                        tile = TilePoint;
                        return;
                    }

                    if (roll < 0.93f)
                    {
                        colour = Ramp(palette.disc, p);
                        size = Mathf.Lerp(0.9f, 1.5f, Next(rng)) * falloff;
                        tile = TilePuffy;
                        return;
                    }

                    if (roll < 0.965f)
                    {
                        if (p > 0.4f)
                        {
                            colour = palette.hiiKnot;
                            size = Mathf.Lerp(1.4f, 2.3f, Next(rng)) * falloff;
                            tile = TileRayed;
                            return;
                        }

                        colour = Ramp(palette.disc, p);
                        size = Mathf.Lerp(0.62f, 1f, Next(rng)) * falloff;
                        tile = TilePoint;
                        return;
                    }

                    if (p > 0.3f)
                    {
                        colour = palette.blueGiant;
                        size = Mathf.Lerp(1.1f, 1.9f, Next(rng)) * falloff;
                        tile = TileCross;
                        return;
                    }

                    colour = Ramp(palette.disc, p);
                    size = Mathf.Lerp(0.9f, 1.5f, Next(rng)) * falloff;
                    tile = TilePuffy;
                    return;
                }
            }
        }

        internal static float Next(Random rng) => (float)rng.NextDouble();

        static Color Ramp(ColorStop[] ramp, float d)
        {
            if (ramp == null || ramp.Length == 0)
            {
                return Color.white;
            }

            d = Mathf.Clamp01(d);
            for (var i = 1; i < ramp.Length; i++)
            {
                if (d <= ramp[i].t)
                {
                    var span = Mathf.Max(0.00001f, ramp[i].t - ramp[i - 1].t);
                    return Color.Lerp(ramp[i - 1].color, ramp[i].color, (d - ramp[i - 1].t) / span);
                }
            }

            return ramp[ramp.Length - 1].color;
        }

        [Serializable]
        public struct WebSettings
        {
            public int seed, pointCount, cellsPerAxis;
            public float radiusMetres, siteJitter, wallShare, filamentShare, nodeShare;
            public float wallThickness, filamentRadius, nodeRadius, snapTolerance, rimFadeStart;
            public Vector4 sizes, brightness;

            public static WebSettings Default => new WebSettings
            {
                seed = 20260912,
                pointCount = 40000,
                cellsPerAxis = 7,
                radiusMetres = 2.5f,
                siteJitter = 0.45f,
                wallShare = 0.25f,
                filamentShare = 0.50f,
                nodeShare = 0.20f,
                wallThickness = 0.035f,
                filamentRadius = 0.030f,
                nodeRadius = 0.070f,
                snapTolerance = 0.05f,
                rimFadeStart = 0.80f,
                sizes = new Vector4(0.45f, 0.70f, 1.00f, 1.90f),
                brightness = new Vector4(0.45f, 0.75f, 1.00f, 1.50f),
            };
        }
    }
}
