// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// What a body <i>does</i> in its system, which is what the app's behaviour keys off: a star lights the
    /// others and can be touched (<see cref="SunTouchResponse"/>), a planet cannot.
    /// <see cref="DwarfPlanet"/> is the IAU's published classification and changes no behaviour; it is here so
    /// a run can report a system honestly rather than calling Pluto a planet.
    /// </summary>
    public enum BodyRole
    {
        Star,
        Planet,
        DwarfPlanet,
    }

    /// <summary>
    /// A planet's bulk composition class, assigned from its <b>published mass and radius</b> (NASA Exoplanet
    /// Archive <c>pl_bmasse</c> and <c>pl_rade</c>), never guessed from its name.
    ///
    /// <para><see cref="Unknown"/> is the honest answer for the many planets with a measured radius and no
    /// measured mass — most of the Kepler and TESS catalogue — and for directly imaged giants whose mass comes
    /// from an evolutionary model rather than a dynamical measurement. It is the default, so a field left
    /// unfilled reads as "not known" rather than as a claim.</para>
    /// </summary>
    public enum BodyClass
    {
        Unknown = 0,
        Rocky,
        IceGiant,
        GasGiant,
    }

    /// <summary>
    /// Which way a body turns on its axis. <see cref="NotMeasured"/> is the answer for effectively every
    /// planet outside our own system: close-in worlds are <i>expected</i> to be tidally locked or in a
    /// spin-orbit resonance from tidal theory, but no terrestrial exoplanet's rotation has actually been
    /// measured (see <c>docs/research/exoplanet_systems.md</c> section 4.4). A body marked NotMeasured still
    /// turns in the app — a frozen ball reads as broken — and its panel has to say the rotation is not
    /// measured, which is what D-010 requires of anything modelled.
    /// </summary>
    public enum SpinDirection
    {
        Prograde,
        Retrograde,
        NotMeasured,
    }

    /// <summary>
    /// Where a body's <i>appearance</i> comes from. This is the distinction D-010 turns on, so it is a field
    /// and not a comment.
    ///
    /// <para><see cref="Observed"/>: the body has a bespoke prefab carrying a real surface map — the ten
    /// bodies of our own system, whose textures are NASA/USGS imagery. <see cref="Modelled"/>: the body is
    /// drawn from the parameter set in <see cref="BodyAppearance"/>, a colour and a shading response derived
    /// from published physical quantities. No exoplanet has an observed surface map, so every exoplanet is
    /// Modelled, and its panel copy must say so.</para>
    /// </summary>
    public enum AppearanceSource
    {
        Observed,
        Modelled,
    }

    /// <summary>
    /// Everything needed to draw a body that has no art of its own, and nothing else.
    ///
    /// <para><b>Every field is a published quantity or is explicitly absent.</b> What the renderer actually
    /// needs — a base colour, a sunlight tint, an ambient level — is <i>derived</i> from these by
    /// <c>GenericBodyBuilder</c>, so the derivation is one reviewable function rather than a colour somebody
    /// picked. The hue convention that turns a class and a temperature into a colour is a declared design
    /// convention, not a measurement, and the builder's report says so for every body it writes.</para>
    ///
    /// <para><b>Where each field comes from.</b> Names in brackets are NASA Exoplanet Archive
    /// <c>pscomppars</c> columns.
    /// <list type="bullet">
    /// <item><see cref="Class"/> — assigned from published mass and radius (<c>pl_bmasse</c>,
    /// <c>pl_rade</c>).</item>
    /// <item><see cref="IlluminantTemperatureK"/> — the effective temperature of the star that lights this
    /// body (<c>st_teff</c>); on a star entry, its own. Drives the light's colour through
    /// <see cref="StarColor"/>.</item>
    /// <item><see cref="EquilibriumTemperatureK"/> — <c>pl_eqt</c> where the archive has one, otherwise the
    /// zero-albedo, full-redistribution floor 278.5 · L^0.25 / sqrt(a). A floor for comparison, not a surface
    /// temperature.</item>
    /// <item><see cref="InsolationEarth"/> — <c>pl_insol</c>, or L / a² in Earth units.</item>
    /// <item><see cref="GeometricAlbedo"/> — measured for a handful of bodies only (HD 189733 b is the
    /// famous one). Negative means not measured, and the class default is used instead.</item>
    /// <item><see cref="AxialTiltDegrees"/> — obliquity, measured only inside our own system.</item>
    /// <item><see cref="Spin"/> and <see cref="RotationPeriodHours"/> — <c>pl_rotp</c>, which is empty for
    /// essentially every planet on any exoplanet shortlist.</item>
    /// <item><see cref="RingOuterRadiusPlanetRadii"/> — published ring geometry. Zero for everything outside
    /// our own system, because no exoplanet ring system has been measured; the builder will not fabricate ring
    /// geometry from a non-zero value it cannot source, so rings still come from a hand-authored prefab.</item>
    /// </list></para>
    /// </summary>
    [Serializable]
    public struct BodyAppearance
    {
        [Tooltip("Bulk composition class from published mass and radius. Unknown when either is unmeasured.")]
        public BodyClass Class;

        [Tooltip("Effective temperature in kelvin of the star lighting this body (archive st_teff); on a star " +
                 "entry, the star's own. Zero falls back to the Sun's 5772 K.")]
        public float IlluminantTemperatureK;

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

        /// <summary>
        /// Observed only when there is a bespoke prefab behind it. Derived rather than stored, because the two
        /// could otherwise disagree and the wrong one would end up in the copy.
        /// </summary>
        public AppearanceSource Provenance =>
            SourcePrefab != null ? AppearanceSource.Observed : AppearanceSource.Modelled;

        public bool IsStar => Role == BodyRole.Star;
    }

    /// <summary>
    /// One planetary system, in the order its bodies are to be arranged: the thing that turns
    /// <c>SolarRowBuilder</c> from a builder for <i>our</i> system into a builder for <b>any</b> system
    /// (D-010).
    ///
    /// <para><b>Why a ScriptableObject rather than a code table.</b> The rest of this app's data layer is
    /// ScriptableObjects — <see cref="BodyInfo"/>, <see cref="LayoutPreset"/>, <see cref="ExperienceModule"/> —
    /// and for one reason that applies here too: a prefab, a scene or another asset can hold a reference to
    /// one, and a static C# table cannot. A profile has to be referenceable because the module it builds, the
    /// layouts it is arranged in and the copy it carries are all assets already.
    ///
    /// CLAUDE.md's preference for reproducible scripts is honoured the way the rest of the data layer honours
    /// it: <b>nobody hand-tunes a profile asset.</b> <c>SystemProfileBuilder</c> writes it from a table that
    /// lives in one file, so what a reviewer reads in a diff is ten lines of numbers rather than four hundred
    /// lines of YAML, and re-running the menu item resets a profile someone poked in the inspector. That is
    /// the same arrangement as <c>LayoutPresetBuilder</c> and its two <c>LayoutPreset</c> assets.</para>
    ///
    /// <para><b>One ordered list, not stars-then-planets.</b> The host star is simply the first entry with
    /// <see cref="BodyRole.Star"/>. That keeps a builder's loop single — which is why our own system's output
    /// is unchanged by the generalisation, the Sun still being entry zero — and it is the only shape that
    /// handles a <b>circumbinary</b> system honestly: Kepler-16 has two entries with the Star role and no
    /// single central body, and nothing downstream needs a special case for it beyond reading
    /// <see cref="Stars"/> instead of "the Sun".</para>
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

        /// <summary>
        /// The host star, or the first of them. Null for a system with no star in it at all — which is not a
        /// hypothetical: the first exoplanets ever found orbit the pulsar PSR B1257+12, and a pulsar is not
        /// something this app draws as a lit ball.
        /// </summary>
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
