using System;
using UnityEngine;

namespace Cosmic
{
    public enum BodyKind { Star, Planet, Dwarf }

    public enum BodyClass { Unknown = 0, Rocky, IceGiant, GasGiant }

    public enum Spin { Prograde, Retrograde, NotMeasured }

    [Serializable]
    public struct Stat
    {
        public string label;
        public string value;
        public string unit;
        public int exponent;

        public string ToRichText()
        {
            var number = exponent == 0 ? value : $"{value}<sup>{exponent}</sup>";
            return string.IsNullOrEmpty(unit) ? number : $"{number} {unit}";
        }
    }

    [Serializable]
    public struct Appearance
    {
        public const float SunEffectiveTemperatureK = 5772f;

        public BodyClass bodyClass;
        public float illuminantTemperatureK;
        public float equilibriumTemperatureK;
        public float insolationEarth;
        public float geometricAlbedo;
        public float axialTiltDegrees;
        public Spin spin;
        public float rotationPeriodHours;
        public float ringOuterRadiusPlanetRadii;

        public static Appearance Default => new Appearance
        {
            bodyClass = BodyClass.Unknown,
            illuminantTemperatureK = SunEffectiveTemperatureK,
            geometricAlbedo = -1f,
            spin = Spin.NotMeasured,
        };

        public bool AlbedoMeasured => geometricAlbedo >= 0f;
    }

    [CreateAssetMenu(menuName = "Cosmic/Body", fileName = "body")]
    public class Body : ScriptableObject
    {
        public string id;
        public string title;
        public string subtitle;

        [TextArea(2, 6)]
        public string paragraph;

        public Stat[] stats = Array.Empty<Stat>();

        // A star lights the rest of its system; nothing else here tells one apart from a planet.
        public BodyKind kind = BodyKind.Planet;

        public Body[] moons = Array.Empty<Body>();
        public Body orbits;
        public float diameterKm;
        public float orbitSemiMajorAu;
        public GameObject sourcePrefab;
        public Appearance appearance = Appearance.Default;
        public AudioClip narration;
        public AudioClip ambience;

        public bool IsMoon => orbits != null;
        public bool Observed => sourcePrefab != null;
    }
}
