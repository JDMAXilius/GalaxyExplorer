// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// What a body does in its system: a star lights the others and can be touched (<see cref="SunTouchResponse"/>).
    /// <see cref="DwarfPlanet"/> is the IAU classification and changes no behaviour.
    /// </summary>
    public enum BodyRole
    {
        Star,
        Planet,
        DwarfPlanet,
    }

    /// <summary>
    /// Bulk composition from published mass and radius (NASA Exoplanet Archive <c>pl_bmasse</c>, <c>pl_rade</c>).
    /// <see cref="Unknown"/> is the default so an unfilled field is not a claim.
    /// </summary>
    public enum BodyClass
    {
        Unknown = 0,
        Rocky,
        IceGiant,
        GasGiant,
    }

    /// <summary>
    /// Spin sense. No exoplanet rotation is measured (docs/research/exoplanet_systems.md 4.4); a
    /// <see cref="NotMeasured"/> body still turns, and its panel must say so (D-010).
    /// </summary>
    public enum SpinDirection
    {
        Prograde,
        Retrograde,
        NotMeasured,
    }

    /// <summary>
    /// Where a body's appearance comes from (D-010): <see cref="Observed"/> is a prefab with real imagery;
    /// <see cref="Modelled"/> is derived from <see cref="BodyAppearance"/>, and the panel copy must say so.
    /// </summary>
    public enum AppearanceSource
    {
        Observed,
        Modelled,
    }

    /// <summary>
    /// Published quantities (NASA Exoplanet Archive <c>pscomppars</c> columns, see tooltips) for drawing a body
    /// with no art of its own; <c>GenericBodyBuilder</c> derives the render colours from them.
    /// </summary>
    [Serializable]
    public struct BodyAppearance
    {
        [Tooltip("Bulk composition class from published mass and radius. Unknown when either is unmeasured.")]
        public BodyClass Class;

        [Tooltip("Effective temperature in kelvin of the star lighting this body (archive st_teff); on a star " +
                 "entry, the star's own. Zero falls back to the Sun's 5772 K.")]
        public float IlluminantTemperatureK;

        // Zero-albedo, full-redistribution floor: 278.5 K * L^0.25 / sqrt(a in AU), L in solar luminosities.
        [Tooltip("Equilibrium temperature in kelvin (archive pl_eqt, or the zero-albedo floor). Zero means " +
                 "none available, and the class colour is used unwarmed.")]
        public float EquilibriumTemperatureK;

        [Tooltip("Starlight received, Earth = 1 (archive pl_insol, or L/a^2). Zero means none available.")]
        public float InsolationEarth;

        [Tooltip("Published geometric albedo. Negative means not measured, so the class default is used.")]
        public float GeometricAlbedo;

        [Tooltip("Obliquity in degrees. Measured only inside our own system; leave zero elsewhere and set " +
                 "Spin to NotMeasured.")]
        public float AxialTiltDegrees;

        [Tooltip("Which way the body turns, or NotMeasured.")]
        public SpinDirection Spin;

        [Tooltip("Published rotation period in hours (archive pl_rotp). Zero means not measured, and the " +
                 "body is given the GDD's one-turn-per-60-s display rotation instead.")]
        public float RotationPeriodHours;

        [Tooltip("Outer edge of a published ring system, in planet radii. Zero for no published rings, which " +
                 "is every body outside our own solar system.")]
        public float RingOuterRadiusPlanetRadii;

        /// <summary>A sane starting point: nothing measured, lit by a Sun-temperature star.</summary>
        public static BodyAppearance Default => new BodyAppearance
        {
            Class = BodyClass.Unknown,
            IlluminantTemperatureK = StarColor.SunEffectiveTemperatureK,
            EquilibriumTemperatureK = 0f,
            InsolationEarth = 0f,
            GeometricAlbedo = -1f,
            AxialTiltDegrees = 0f,
            Spin = SpinDirection.NotMeasured,
            RotationPeriodHours = 0f,
            RingOuterRadiusPlanetRadii = 0f,
        };

        public bool AlbedoMeasured => GeometricAlbedo >= 0f;
    }

    /// <summary>
    /// One body of a <see cref="SystemProfile"/>: which copy it carries, where its geometry comes from, and
    /// the two published numbers an arrangement needs (its diameter and how far out it orbits).
    /// </summary>
    [Serializable]
    public class SystemBody
    {
        [Tooltip("Stable key, matching BodyInfo.Id and LayoutSlot.BodyId. Never shown to the player.")]
        public string Id;

        public BodyRole Role = BodyRole.Planet;

        [Tooltip("Copy for this body's panel. Left empty, the builder looks one up by Id.")]
        public BodyInfo Info;

        [Tooltip("The body's own prefab, carrying real mesh and real surface imagery — our ten bodies' " +
                 "poi_<id>_prefab. Left empty, the body is built from Appearance instead, and is therefore " +
                 "declared Modelled.")]
        public GameObject SourcePrefab;

        [Tooltip("Published diameter in kilometres. What a true-proportion arrangement is computed from.")]
        public float DiameterKm;

        [Tooltip("Published orbital semi-major axis in AU. Zero for the system's own star.")]
        public float OrbitSemiMajorAu;

        [Tooltip("How to draw the body when it has no prefab of its own.")]
        public BodyAppearance Appearance = BodyAppearance.Default;

        /// <summary>Derived from <see cref="SourcePrefab"/> rather than stored, so the two cannot disagree.</summary>
        public AppearanceSource Provenance =>
            SourcePrefab != null ? AppearanceSource.Observed : AppearanceSource.Modelled;

        public bool IsStar => Role == BodyRole.Star;
    }

    /// <summary>
    /// One planetary system, bodies in arrangement order (D-010). Written by <c>SystemProfileBuilder</c>; do not
    /// hand-tune. One ordered list so circumbinary systems (two Star entries) need no special case.
    /// </summary>
    [CreateAssetMenu(menuName = "Cosmic Simulation/System Profile", fileName = "system_profile")]
    public class SystemProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable key, e.g. solar_system or trappist_1. Never shown to the player.")]
        public string Id;

        [Tooltip("The system's name as the player reads it, e.g. Our Solar System.")]
        public string DisplayName;

        [Header("Where the built content goes")]
        [Tooltip("The experience whose ContentPrefab this system's content is hung on.")]
        public ExperienceModule Module;

        [Tooltip("File name, without extension, of the content prefab the builder writes. Re-running writes " +
                 "the same path, so the prefab's GUID and every reference to it survive.")]
        public string ContentPrefabName;

        [Tooltip("Id of the LayoutPreset the content prefab is authored in — the arrangement the experience " +
                 "opens with. Empty falls back to the module's first layout.")]
        public string AuthoredLayoutId;

        [Header("Bodies, in the order they are arranged")]
        public SystemBody[] Bodies = Array.Empty<SystemBody>();

        /// <summary>The body with this id, or null. Linear over ten-odd entries; called at build time.</summary>
        public SystemBody Find(string bodyId)
        {
            if (Bodies == null || string.IsNullOrEmpty(bodyId))
            {
                return null;
            }

            foreach (var body in Bodies)
            {
                if (body != null && body.Id == bodyId)
                {
                    return body;
                }
            }

            return null;
        }

        /// <summary>The first star, or null for a starless system (e.g. pulsar PSR B1257+12).</summary>
        public SystemBody PrimaryStar
        {
            get
            {
                if (Bodies == null)
                {
                    return null;
                }

                foreach (var body in Bodies)
                {
                    if (body != null && body.IsStar)
                    {
                        return body;
                    }
                }

                return null;
            }
        }

        /// <summary>How many stars the system has. Two or more means no single central body to arrange around.</summary>
        public int StarCount
        {
            get
            {
                if (Bodies == null)
                {
                    return 0;
                }

                var count = 0;
                foreach (var body in Bodies)
                {
                    if (body != null && body.IsStar)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Stars, in profile order. Allocates; build-time only.</summary>
        public SystemBody[] Stars
        {
            get
            {
                var found = new SystemBody[StarCount];
                var next = 0;
                if (Bodies != null)
                {
                    foreach (var body in Bodies)
                    {
                        if (body != null && body.IsStar)
                        {
                            found[next++] = body;
                        }
                    }
                }

                return found;
            }
        }
    }
}
