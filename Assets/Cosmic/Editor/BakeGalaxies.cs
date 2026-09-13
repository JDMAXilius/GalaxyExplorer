using System;
using UnityEngine;

namespace Cosmic.Editor
{
    internal static class BakeGalaxies
    {
        const float DiscInnerScale = 0.1f;
        const float DiscOuterScale = 1f;
        const float WhirlpoolTiltDegrees = 20f;
        const float MilkyWaySceneScale = 0.4f;
        const float MilkyWayQuestFactor = 2f;
        const float MilkyWayEnvelopeUnits = 1.05f;

        internal static readonly (string id, Action<Galaxy> fill)[] Set =
        {
            ("milky_way", MilkyWay), ("andromeda", Andromeda), ("whirlpool", Whirlpool),
            ("pinwheel", Pinwheel), ("triangulum", Triangulum),
        };

        static void MilkyWay(Galaxy g)
        {
            Shape(g, MilkyWaySceneScale * MilkyWayQuestFactor * MilkyWayEnvelopeUnits * 2f, 0f, 0f, 0.8f, 2,
                  370f, 0.08f);
            var layers = SpiralLayers(1f, 0.2f, 1f);
            layers[0].ellipses = 320;
            layers[0].fuzz = new Vector2(0.95f, 1.05f);
            layers[0].yRange = 0.1f;
            layers[1].ellipses = 200;
            layers[1].starsPerEllipse = 10;
            layers[1].fuzz = new Vector2(1f, 1.15f);
            layers[1].yRange = 0.04f;
            layers[1].worldSpaceScale = 0.03f;
            layers[1].tint = new Color(0.0549f, 0.0510f, 0.00973f, 0.647f);
            layers[2].ellipses = 320;
            layers[2].starsPerEllipse = 13;
            layers[2].minEllipseScale = 0.02f;
            layers[2].fuzz = new Vector2(0.98f, 1.02f);
            layers[2].yRange = 0.1f;
            layers[2].worldSpaceScale = 0.005f;
            g.layers = layers;
            g.palette = Palette(
                new[] { Stop(0f, 1.00f, 0.97f, 0.92f), Stop(0.35f, 0.97f, 0.97f, 0.97f),
                        Stop(0.70f, 0.86f, 0.90f, 1.00f), Stop(1f, 0.78f, 0.86f, 1.00f) },
                new[] { Stop(0f, 1.00f, 0.98f, 0.94f), Stop(0.50f, 0.94f, 0.93f, 0.90f),
                        Stop(1f, 0.82f, 0.82f, 0.84f) },
                new[] { Stop(0f, 0.35f, 0.32f, 0.30f), Stop(0.55f, 0.42f, 0.36f, 0.32f),
                        Stop(1f, 0.36f, 0.33f, 0.31f) },
                new Color(1.00f, 0.50f, 0.38f), new Color(0.65f, 0.80f, 1.00f));
        }

        static void Andromeda(Galaxy g)
        {
            Shape(g, 1.2f, 75f, 55f, 0.72f, 2, 470f, 0.05f);
            var layers = SpiralLayers(0.62f, 0.46f, 1f);
            layers[0].fuzz = new Vector2(0.92f, 1.08f);
            layers[0].thicknessAtRim = 0.35f;
            layers[0].yRange = 0.075f;
            layers[1].tint = new Color(0.09f, 0.033f, 0.020f, 0.45f);
            layers[1].dustRingPeak = 0.35f;
            layers[1].dustRingWidth = 0.3f;
            layers[2].yRange = 0.055f;
            g.layers = layers;
            g.palette = Palette(
                new[] { Stop(0f, 1.00f, 0.93f, 0.82f), Stop(0.22f, 1.00f, 0.90f, 0.76f),
                        Stop(0.45f, 0.90f, 0.86f, 0.84f), Stop(0.70f, 0.70f, 0.78f, 0.97f),
                        Stop(1f, 0.55f, 0.70f, 1.00f) },
                new[] { Stop(0f, 1.00f, 0.96f, 0.88f), Stop(0.45f, 0.98f, 0.88f, 0.74f),
                        Stop(1f, 0.78f, 0.58f, 0.46f) },
                new[] { Stop(0f, 0.85f, 0.44f, 0.36f), Stop(0.55f, 1.00f, 0.56f, 0.46f),
                        Stop(1f, 0.86f, 0.48f, 0.44f) },
                new Color(1.00f, 0.52f, 0.34f), new Color(0.62f, 0.78f, 1.00f));
        }

        static void Whirlpool(Galaxy g)
        {
            // Tilt(20, 20) degenerates to 90 degrees, so M51 poses its inclination as the tilt; the other three derive theirs.
            Shape(g, 1.1f, WhirlpoolTiltDegrees, 20f, 0.62f, 2, Winding(18f), 0.05f);
            g.layers = SpiralLayers(0.45f, 0.18f, 1.15f);
            g.palette = Palette(
                new[] { Stop(0f, 1.00f, 0.95f, 0.86f), Stop(0.25f, 0.94f, 0.92f, 0.88f),
                        Stop(0.55f, 0.72f, 0.82f, 0.98f), Stop(1f, 0.58f, 0.74f, 1.00f) },
                new[] { Stop(0f, 1.00f, 0.97f, 0.90f), Stop(0.50f, 0.97f, 0.90f, 0.79f),
                        Stop(1f, 0.80f, 0.68f, 0.60f) },
                new[] { Stop(0f, 0.42f, 0.34f, 0.34f), Stop(0.55f, 0.52f, 0.40f, 0.38f),
                        Stop(1f, 0.44f, 0.36f, 0.36f) },
                new Color(1.00f, 0.42f, 0.52f), new Color(0.60f, 0.78f, 1.00f));
        }

        static void Pinwheel(Galaxy g)
        {
            Shape(g, 1.35f, Tilt(18f, 15f), 15f, 0.65f, 3, Winding(26f), 0.045f);
            g.layers = SpiralLayers(0.3f, 0.15f, 1.5f);
            g.palette = Palette(
                new[] { Stop(0f, 1.00f, 0.94f, 0.84f), Stop(0.18f, 0.90f, 0.90f, 0.92f),
                        Stop(0.50f, 0.70f, 0.82f, 1.00f), Stop(1f, 0.56f, 0.74f, 1.00f) },
                new[] { Stop(0f, 1.00f, 0.96f, 0.88f), Stop(0.55f, 0.92f, 0.88f, 0.82f),
                        Stop(1f, 0.74f, 0.72f, 0.72f) },
                new[] { Stop(0f, 0.38f, 0.33f, 0.34f), Stop(0.55f, 0.46f, 0.38f, 0.38f),
                        Stop(1f, 0.40f, 0.34f, 0.35f) },
                new Color(1.00f, 0.40f, 0.50f), new Color(0.62f, 0.80f, 1.00f));
        }

        static void Triangulum(Galaxy g)
        {
            Shape(g, 0.95f, Tilt(54f, 35f), 35f, 0.7f, 3, Winding(34f), 0.05f);
            g.layers = SpiralLayers(0.18f, 0.12f, 1.6f);
            g.palette = Palette(
                new[] { Stop(0f, 0.96f, 0.94f, 0.90f), Stop(0.20f, 0.84f, 0.88f, 0.96f),
                        Stop(0.55f, 0.66f, 0.80f, 1.00f), Stop(1f, 0.54f, 0.73f, 1.00f) },
                new[] { Stop(0f, 0.98f, 0.95f, 0.90f), Stop(0.55f, 0.86f, 0.86f, 0.88f),
                        Stop(1f, 0.70f, 0.74f, 0.80f) },
                new[] { Stop(0f, 0.34f, 0.32f, 0.34f), Stop(0.55f, 0.42f, 0.37f, 0.38f),
                        Stop(1f, 0.36f, 0.33f, 0.35f) },
                new Color(1.00f, 0.38f, 0.48f), new Color(0.64f, 0.82f, 1.00f));
        }

        static void Shape(Galaxy g, float widthMetres, float tiltDegrees, float yawDegrees, float xRadii,
                          int armCount, float windingDegrees, float velocity)
        {
            g.widthMetres = widthMetres;
            g.discTiltDegrees = tiltDegrees;
            g.discYawDegrees = yawDegrees;
            g.minScaleMetres = 0.5f;
            g.maxScaleMetres = 3f;
            g.grabRadiusFraction = 1f;
            g.xRadii = xRadii;
            g.zRadii = 1f;
            g.armCount = armCount;
            g.windingDegrees = windingDegrees;
            g.velocityMultiplier = velocity;
        }

        static GalaxyLayerSpec[] SpiralLayers(float cloudsMax, float dustMin, float hiiWeight) => new[]
        {
            Layer(GalaxyLayer.Clouds, "clouds", 31415, 250, 10, 0.02f, cloudsMax, new Vector2(0.9f, 1.1f),
                  1f, 0.25f, 0.07f, 0.02f, Color.white, 0.3f, 0),
            Layer(GalaxyLayer.Dust, "dust", 27182, 200, 8, dustMin, 1f, new Vector2(0.94f, 1.06f),
                  0.4f, 0.4f, 0.018f, 0.02f, new Color(0.07f, 0.06f, 0.06f, 0.4f), 1f, 1),
            Layer(GalaxyLayer.Stars, "stars", 16180, 300, 15, DiscInnerScale, DiscOuterScale,
                  new Vector2(0.965f, 1.035f), 1f, 0.1f, 0.05f, 0.008f, Color.white, hiiWeight, 2),
        };

        static GalaxyLayerSpec Layer(GalaxyLayer role, string id, int seed, int ellipses, int starsPerEllipse,
                                     float minScale, float maxScale, Vector2 fuzz, float centre, float rim,
                                     float yRange, float worldSpaceScale, Color tint, float multiplier,
                                     int drawIndex) => new GalaxyLayerSpec
        {
            layer = role, id = id, seed = seed, ellipses = ellipses, starsPerEllipse = starsPerEllipse,
            minEllipseScale = minScale, maxEllipseScale = maxScale, fuzz = fuzz, thicknessAtCentre = centre,
            thicknessAtRim = rim, yRange = yRange, worldSpaceScale = worldSpaceScale, tint = tint,
            tintMultiplier = multiplier, drawIndex = drawIndex, dustRingPeak = 0.5f, dustRingWidth = 0.55f,
        };

        static GalaxyPalette Palette(ColorStop[] disc, ColorStop[] clouds, ColorStop[] dust, Color hii,
                                     Color blue) =>
            new GalaxyPalette { disc = disc, clouds = clouds, dust = dust, hiiKnot = hii, blueGiant = blue };

        static ColorStop Stop(float t, float r, float g, float b) =>
            new ColorStop { t = t, color = new Color(r, g, b) };

        static float Winding(float pitchDegrees) =>
            Mathf.Log(DiscOuterScale / DiscInnerScale) / Mathf.Tan(pitchDegrees * Mathf.Deg2Rad) * Mathf.Rad2Deg;

        static float Tilt(float inclinationDegrees, float yawDegrees) => Mathf.Asin(
            Mathf.Clamp(Mathf.Cos(inclinationDegrees * Mathf.Deg2Rad) / Mathf.Cos(yawDegrees * Mathf.Deg2Rad),
                        -1f, 1f)) * Mathf.Rad2Deg;
    }
}
