using System;
using UnityEngine;

namespace Cosmic
{
    public enum GalaxyKind { SpiralArms, Elliptical, Irregular }

    public enum GalaxyLayer { Clouds, Dust, Stars }

    [Serializable]
    public struct ColorStop
    {
        [Range(0f, 1f)]
        public float t;

        public Color color;
    }

    [Serializable]
    public class GalaxyPalette
    {
        public ColorStop[] disc = Array.Empty<ColorStop>();
        public ColorStop[] clouds = Array.Empty<ColorStop>();
        public ColorStop[] dust = Array.Empty<ColorStop>();
        public Color hiiKnot = new Color(1.00f, 0.52f, 0.34f);
        public Color blueGiant = new Color(0.62f, 0.78f, 1.00f);

        public ColorStop[] For(GalaxyLayer layer)
        {
            switch (layer)
            {
                case GalaxyLayer.Clouds: return clouds;
                case GalaxyLayer.Dust: return dust;
                default: return disc;
            }
        }
    }

    [Serializable]
    public struct GalaxyLayerSpec
    {
        public GalaxyLayer layer;
        public string id;
        public Shader shader;
        public PointCloud points;
        public int seed;
        public int ellipses;
        public int starsPerEllipse;
        public float minEllipseScale;
        public float maxEllipseScale;
        public Vector2 fuzz;
        public float thicknessAtCentre;
        public float thicknessAtRim;
        public float yRange;
        public float worldSpaceScale;
        public Color tint;
        public float tintMultiplier;
        public int drawIndex;
        public float dustRingPeak;
        public float dustRingWidth;
        public float xRadii;
        public float zRadii;
        public float windingDegrees;
    }

    [CreateAssetMenu(menuName = "Cosmic/Galaxy", fileName = "galaxy")]
    public class Galaxy : ScriptableObject
    {
        public string id;
        public GalaxyKind kind = GalaxyKind.SpiralArms;
        public int seed = 1;
        public float widthMetres = 1.2f;
        public float discTiltDegrees;
        public float discYawDegrees;
        public float minScaleMetres = 0.5f;
        public float maxScaleMetres = 3f;
        public float grabRadiusFraction = 1f;

        // Intrinsic eccentricity in the disc's own plane, not a published axis ratio; at 1:1 the arms vanish.
        public float xRadii = 0.8f;

        public float zRadii = 1f;
        public int armCount = 2;
        public float windingDegrees = 370f;
        public float[] armOffsetsDegrees = Array.Empty<float>();
        public float velocityMultiplier = 0.05f;
        public GalaxyLayerSpec[] layers = Array.Empty<GalaxyLayerSpec>();
        public GalaxyPalette palette = new GalaxyPalette();

        public float ArmSpacingDegrees => armCount > 0 ? 360f / armCount : 0f;

        public float ArmOffsetDegrees(int arm) => armOffsetsDegrees != null && arm < armOffsetsDegrees.Length ? armOffsetsDegrees[arm] : ArmSpacingDegrees * arm;

        public Vector2 RadiiOf(in GalaxyLayerSpec layer) => new Vector2(layer.xRadii > 0f ? layer.xRadii : xRadii, layer.zRadii > 0f ? layer.zRadii : zRadii);

        public float WindingOf(in GalaxyLayerSpec layer) => layer.windingDegrees > 0f ? layer.windingDegrees : windingDegrees;

        public int TotalPoints
        {
            get
            {
                var total = 0;
                if (layers != null)
                {
                    foreach (var layer in layers)
                    {
                        total += layer.ellipses * layer.starsPerEllipse * Mathf.Max(1, armCount);
                    }
                }

                return total;
            }
        }

        public bool TryGetLayer(GalaxyLayer role, out GalaxyLayerSpec spec)
        {
            if (layers != null)
            {
                for (var i = 0; i < layers.Length; i++)
                {
                    if (layers[i].layer == role)
                    {
                        spec = layers[i];
                        return true;
                    }
                }
            }

            spec = default;
            return false;
        }
    }
}
