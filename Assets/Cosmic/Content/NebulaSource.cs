using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Cosmic
{
    public static class NebulaSource
    {
        const float LuminanceFloor = 0.10f;

        public static StarVert[] Nebula(float[] luminance, int width, int height, Color[] plate,
            PointSources.NebulaShape shape, float thicknessRatio, int seed, int count = 28000)
        {
            if (luminance == null || plate == null || width <= 0 || height <= 0 ||
                luminance.Length < width * height || plate.Length < width * height)
            {
                return new StarVert[0];
            }

            var peak = 0f;
            for (var i = 0; i < width * height; i++)
            {
                peak = Mathf.Max(peak, luminance[i]);
            }

            var floor = peak * LuminanceFloor;
            var range = Mathf.Max(peak - floor, 1e-4f);
            var random = new Random(seed);
            var points = new List<StarVert>(count);
            var attempts = 0;
            var maxAttempts = count * 60;

            while (points.Count < count && attempts++ < maxAttempts)
            {
                var px = random.Next(width);
                var py = random.Next(height);
                var index = py * width + px;

                var weight = luminance[index] - floor;
                if (weight <= 0f) continue;

                var brightness = weight / range;
                if (random.NextDouble() > brightness) continue;

                var u = ((px + (float)random.NextDouble()) / width) * 2f - 1f;
                var v = ((py + (float)random.NextDouble()) / height) * 2f - 1f;
                if (!Depth(shape, u, v, thicknessRatio, brightness, random, out var w)) continue;

                points.Add(Describe(new Vector3(u, v, w), plate[index], random));
            }

            return points.ToArray();
        }

        static bool Depth(PointSources.NebulaShape shape, float u, float v, float thickness, float brightness,
            Random random, out float w)
        {
            switch (shape)
            {
                case PointSources.NebulaShape.Shell:
                    return ShellDepth(u, v, thickness, random, out w);

                case PointSources.NebulaShape.Bipolar:
                {
                    var lobe = Mathf.Max(thickness * 1.6f, 1e-4f);
                    if (!ShellDepth(u / lobe, (Mathf.Abs(v) - 0.5f) / lobe, thickness, random, out w))
                    {
                        return false;
                    }

                    w *= thickness * 1.6f;
                    return true;
                }

                default:
                {
                    var centre = (Noise(u * 1.7f, v * 1.7f) - 0.5f) * thickness * 1.4f;
                    var spread = thickness * Mathf.Lerp(0.35f, 1f, brightness);
                    w = centre + Gaussian(random) * spread * 0.5f;
                    return Mathf.Abs(w) <= 1f;
                }
            }
        }

        static bool ShellDepth(float u, float v, float thickness, Random random, out float w)
        {
            w = 0f;
            var r = Mathf.Sqrt(u * u + v * v);
            if (r >= 1f)
            {
                w = Gaussian(random) * thickness * 0.35f;
                return Mathf.Abs(w) <= 1f;
            }

            var cos = Mathf.Sqrt(1f - r * r);
            if (random.NextDouble() > cos) return false;

            var side = random.NextDouble() < 0.5 ? -1f : 1f;
            w = side * cos + Gaussian(random) * thickness * 0.5f;
            return Mathf.Abs(w) <= 1f;
        }

        static StarVert Describe(Vector3 local, Color colour, Random random)
        {
            var t = (float)random.NextDouble();
            var cell = random.Next(4);

            var point = new StarVert
            {
                ellipseDistance = new Vector2(local.x, local.z).magnitude,
                curveOffset = Mathf.Atan2(local.z, local.x),
                yOffset = local.y,
                ellipseOffset = Mathf.InverseLerp(-1f, 1f, local.z),
                color = new Vector3(colour.r, colour.g, colour.b),
                size = Mathf.Lerp(0.55f, 1.6f, t * t),
                uv = new Vector2(cell % 2 == 0 ? 0f : 0.5f, cell < 2 ? 0f : -0.5f),
            };

            // The old bake drew a per-point shimmer phase here; the draw stays so every later point matches it.
            random.NextDouble();
            return point;
        }

        static float Gaussian(Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            var value = Mathf.Sqrt(-2f * Mathf.Log((float)u1)) * Mathf.Cos(2f * Mathf.PI * (float)u2);
            return Mathf.Clamp(value, -2.5f, 2.5f);
        }

        static float Noise(float x, float y)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var xf = x - xi;
            var yf = y - yi;
            var sx = xf * xf * (3f - 2f * xf);
            var sy = yf * yf * (3f - 2f * yf);

            var a = Lattice(xi, yi);
            var b = Lattice(xi + 1, yi);
            var c = Lattice(xi, yi + 1);
            var d = Lattice(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        static float Lattice(int x, int y)
        {
            unchecked
            {
                var h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
