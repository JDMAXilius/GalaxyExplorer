// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the sky each nebula actually sits in, as a true equirectangular panorama.
    ///
    /// <para><b>Why these are generated and not painted or prompted.</b> The domes this replaces were
    /// 1344 x 576 - which is 21:9, not the 2:1 an equirectangular map has to be - so they were stretched onto
    /// the sphere and pinched at the poles, and they had a seam where the left edge met the right. An image
    /// model cannot fix that, because nothing about the way it composes a picture knows that the top row of
    /// pixels is a single point and that column zero and column N are the same meridian. A starfield, on the
    /// other hand, is one of the few things that is easier to compute correctly than to draw: stars are points
    /// on a sphere, and placing them on a sphere is four lines of arithmetic.</para>
    ///
    /// <para><b>What makes each one different.</b> Not taste - galactic latitude. A nebula a few degrees off
    /// the plane of the Milky Way sits in a crowded sky with the band of the galaxy across it; one thirty
    /// degrees off the plane sits in near-empty sky where the next brightest things are other galaxies. The
    /// Helix and Trumpler 14 should not have the same backdrop, and until now they had backdrops that differed
    /// only in colour tint.</para>
    ///
    /// <para>Stars are placed uniformly <i>on the sphere</i> rather than uniformly in the image, which is the
    /// one thing an equirectangular starfield gets wrong if it is drawn by hand: rows near the poles cover a
    /// tiny solid angle, so an even scatter in UV piles stars up at the top and bottom of the sky.</para>
    /// </summary>
    public static class NebulaSkyBuilder
    {
        private const string Folder = "Assets/Textures/nebulae/domes";

        // 2:1 exactly, because that is what equirectangular means. 2048 wide rather than 4096 because
        // AstronomyTextureImporter caps this folder at 2048 and a silent downscale would eat half the stars.
        private const int Width = 2048;
        private const int Height = 1024;

        /// <summary>
        /// One sky per nebula, described by where in the galaxy you would be standing to see it.
        /// </summary>
        private readonly struct Sky
        {
            public readonly int Stars;
            public readonly float BandStrength;
            public readonly float BandTiltDegrees;
            public readonly Color Cast;
            public readonly int Seed;
            public readonly string Note;

            public Sky(int stars, float bandStrength, float bandTilt, Color cast, int seed, string note)
            {
                Stars = stars;
                BandStrength = bandStrength;
                BandTiltDegrees = bandTilt;
                Cast = cast;
                Seed = seed;
                Note = note;
            }
        }

        private static readonly Dictionary<string, Sky> Skies = new Dictionary<string, Sky>
        {
            // Aquarius, about thirty degrees below the plane. This is the emptiest sky of the seven: no band,
            // few stars, and the faint things in it are distant galaxies rather than more of our own.
            ["helix"] = new Sky(2600, 0.00f, 0f, new Color(0.022f, 0.026f, 0.036f), 11,
                "Aquarius, b = -57: far off the plane, the emptiest sky of the seven, no band at all"),

            // Taurus, six degrees below the plane and looking away from the galactic centre. A real band, but
            // the thin outer-galaxy one rather than the crowded inner-galaxy one.
            ["crab"] = new Sky(11000, 0.34f, 62f, new Color(0.035f, 0.04f, 0.055f), 23,
                "Taurus, b = -6: anticentre, moderate field, thin band"),

            // Camelopardalis, eleven degrees up. Quiet northern sky.
            ["ngc1501"] = new Sky(8600, 0.22f, 40f, new Color(0.03f, 0.038f, 0.048f), 37,
                "Camelopardalis, b = +6.5: near the plane but in the anticentre, so a thin band"),

            // Carina, right in the plane. One of the richest skies there is: the Carina arm seen lengthways,
            // crowded with hot stars and cut by the dust lanes that make the Coalsack-like gaps.
            ["homunculus"] = new Sky(27000, 0.92f, 12f, new Color(0.055f, 0.045f, 0.038f), 51,
                "Carina, b = -1: in the plane, very rich, strong warm band with dust lanes"),

            // Orion, nineteen degrees below the plane. Moderate star field, but the whole region is full of
            // faint blue reflection nebulosity from the Orion complex, which is worth a cast.
            ["orion"] = new Sky(9800, 0.26f, 75f, new Color(0.050f, 0.034f, 0.044f), 67,
                "Orion, b = -19: moderate field, dusky red Barnard's Loop wash and blue reflection patches"),

            // Serpens, on the plane and looking towards the inner galaxy. Very crowded, and the Great Rift
            // runs through it - the dark lanes are as much of the look as the stars.
            ["pillars"] = new Sky(31000, 1.00f, 28f, new Color(0.062f, 0.044f, 0.038f), 83,
                "Serpens, b = +1, l = 17: 17 degrees from the galactic centre. The richest sky of the seven - "
                + "Scutum and Sagittarius star clouds, and the Great Rift cutting through them"),

            // Carina again, a few arcminutes from the Homunculus, so the same sky with its own seed.
            ["trumpler14"] = new Sky(26000, 0.90f, 14f, new Color(0.052f, 0.046f, 0.042f), 97,
                "Carina, b = -1: in the plane, very rich, strong warm band"),
        };

        [MenuItem("Cosmic Simulation/Build Nebula Skies")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var report = new System.Text.StringBuilder();

            foreach (var pair in Skies)
            {
                var path = $"{Folder}/sky_{pair.Key}.png";
                var texture = Paint(pair.Value);

                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
                Object.DestroyImmediate(texture);

                // Import first, settings second: settings applied in the same pass as the write are dropped
                // silently. AstronomyTextureImporter governs this folder and does most of it, so this only
                // fixes the one thing that matters here and that it gets wrong for a panorama.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                report.AppendLine($"  sky_{pair.Key}: {pair.Value.Stars:N0} stars, band {pair.Value.BandStrength:0.00} - {pair.Value.Note}");
            }

            AssetDatabase.Refresh();
            Debug.Log($"NebulaSkyBuilder: {Skies.Count} sky/skies at {Width}x{Height} (2:1 equirectangular).\n{report}");
        }

        private static Texture2D Paint(Sky sky)
        {
            var pixels = new Color[Width * Height];
            var random = new System.Random(sky.Seed);

            // ---- the background: the cast, plus the band of the galaxy if this direction has one.
            var tilt = sky.BandTiltDegrees * Mathf.Deg2Rad;
            var bandNormal = new Vector3(0f, Mathf.Cos(tilt), Mathf.Sin(tilt)).normalized;
            var lift = (float)random.NextDouble() * 10f;

            for (var y = 0; y < Height; y++)
            {
                // Polar angle from the top row, which is what an equirectangular map stores.
                var theta = Mathf.PI * (y + 0.5f) / Height;
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);

                for (var x = 0; x < Width; x++)
                {
                    var phi = 2f * Mathf.PI * (x + 0.5f) / Width;
                    var direction = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi));

                    var colour = sky.Cast;

                    if (sky.BandStrength > 0.001f)
                    {
                        // Distance from the great circle, in radians. The band is a glow either side of it.
                        var offPlane = Mathf.Abs(Vector3.Dot(direction, bandNormal));
                        var across = Mathf.Exp(-offPlane * offPlane * 42f);

                        // Structure along the band, so it is not an airbrushed stripe.
                        var along = Fbm(direction * 2.6f + Vector3.one * lift, 4);

                        // And the dust in front of it. The Great Rift is not a gap in the stars, it is a cloud
                        // between us and them, so it multiplies rather than subtracts.
                        var dust = Mathf.Clamp01(Fbm(direction * 4.1f - Vector3.one * lift, 4) * 1.6f - 0.18f);

                        var glow = across * Mathf.Lerp(0.45f, 1.15f, along) * dust * sky.BandStrength;
                        colour += new Color(glow * 0.30f, glow * 0.26f, glow * 0.22f);
                    }

                    pixels[y * Width + x] = colour;
                }
            }

            // ---- the stars.
            for (var i = 0; i < sky.Stars; i++)
            {
                // Uniform on the sphere: cos(theta) uniform in -1..1, NOT theta uniform. Scattering evenly in
                // UV instead would pile every field up against the poles, where a row of pixels covers almost
                // no sky at all.
                var cosTheta = 1f - 2f * (float)random.NextDouble();
                var theta = Mathf.Acos(Mathf.Clamp(cosTheta, -1f, 1f));
                var phi = 2f * Mathf.PI * (float)random.NextDouble();

                var y = Mathf.Clamp(Mathf.RoundToInt(theta / Mathf.PI * Height - 0.5f), 0, Height - 1);
                var x = Mathf.RoundToInt(phi / (2f * Mathf.PI) * Width - 0.5f);
                x = ((x % Width) + Width) % Width;      // wraps, so there is no seam at the meridian

                // Far more faint stars than bright ones, which is what the real magnitude distribution looks
                // like and what stops a generated field reading as a scatter of identical dots.
                var roll = (float)random.NextDouble();
                var brightness = Mathf.Pow(roll, 3.2f) * 1.9f + 0.05f;

                // Colour by rough spectral class. Weighted towards the cool end because most stars are, with
                // enough hot blue-white ones to matter in a crowded field.
                var temperature = (float)random.NextDouble();
                Color tint;
                if (temperature > 0.90f) tint = new Color(0.72f, 0.80f, 1.00f);        // O and B
                else if (temperature > 0.72f) tint = new Color(0.88f, 0.92f, 1.00f);   // A
                else if (temperature > 0.45f) tint = new Color(1.00f, 0.98f, 0.94f);   // F and G
                else if (temperature > 0.18f) tint = new Color(1.00f, 0.90f, 0.74f);   // K
                else tint = new Color(1.00f, 0.78f, 0.62f);                            // M

                Splat(pixels, x, y, tint * brightness, brightness);
            }

            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Adds one star. Bright ones get a small core plus a neighbouring falloff so they do not alias into a
        /// single hard pixel; faint ones are a single pixel, because that is all they are.
        /// </summary>
        private static void Splat(Color[] pixels, int x, int y, Color colour, float brightness)
        {
            Add(pixels, x, y, colour);

            if (brightness < 0.45f)
            {
                return;
            }

            var spill = colour * (brightness < 1.0f ? 0.22f : 0.38f);
            Add(pixels, x - 1, y, spill);
            Add(pixels, x + 1, y, spill);
            Add(pixels, x, y - 1, spill);
            Add(pixels, x, y + 1, spill);

            if (brightness < 1.3f)
            {
                return;
            }

            var corner = colour * 0.16f;
            Add(pixels, x - 1, y - 1, corner);
            Add(pixels, x + 1, y - 1, corner);
            Add(pixels, x - 1, y + 1, corner);
            Add(pixels, x + 1, y + 1, corner);
        }

        private static void Add(Color[] pixels, int x, int y, Color colour)
        {
            if (y < 0 || y >= Height) return;
            x = ((x % Width) + Width) % Width;          // the sky wraps in longitude

            var index = y * Width + x;
            var existing = pixels[index];
            pixels[index] = new Color(
                Mathf.Min(existing.r + colour.r, 1f),
                Mathf.Min(existing.g + colour.g, 1f),
                Mathf.Min(existing.b + colour.b, 1f),
                1f);
        }

        // ---------- noise, on the direction vector so it is seamless by construction

        private static float Fbm(Vector3 at, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;

            for (var i = 0; i < octaves; i++)
            {
                sum += amplitude * Noise(at);
                total += amplitude;
                at *= 2.04f;
                amplitude *= 0.5f;
            }

            return Mathf.Clamp01(sum / Mathf.Max(total, 1e-4f));
        }

        private static float Noise(Vector3 at)
        {
            // Three 2D slices combined. Sampled on the direction vector rather than on the image, which is
            // what makes the result wrap at the meridian and behave at the poles without any special case.
            var a = Mathf.PerlinNoise(at.x + 13.1f, at.y + 5.7f);
            var b = Mathf.PerlinNoise(at.y + 29.3f, at.z + 17.9f);
            var c = Mathf.PerlinNoise(at.z + 41.7f, at.x + 3.3f);
            return (a + b + c) / 3f;
        }
    }
}
