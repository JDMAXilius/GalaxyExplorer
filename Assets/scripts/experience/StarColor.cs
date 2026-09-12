// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The colour of a star's light, computed from its published effective temperature and nothing else.
    ///
    /// <para><b>Why this is code and not a table of colours.</b> D-010 allows modelled appearance and forbids
    /// presenting it as observation. A star's continuum colour is the one part of a planetary system's
    /// appearance that is not modelled at all: it follows from a single measured quantity, <c>st_teff</c>, by
    /// a standard and citable calculation. Computing it means a new system needs no colour decisions from
    /// anyone — the researcher supplies a temperature and the app derives the rest — and it means the numbers
    /// in <c>docs/research/exoplanet_systems.md</c> section 4.2 and the numbers on the headset are produced
    /// the same way instead of being transcribed.</para>
    ///
    /// <para><b>The calculation.</b> A Planck spectrum at the star's effective temperature, integrated over
    /// 360-830 nm against the analytic multi-lobe piecewise-Gaussian fits to the CIE 1931 colour matching
    /// functions of Wyman, Sloan and Shirley 2013 (<c>https://jcgt.org/published/0002/02/01/</c>), converted
    /// to linear sRGB with the standard D65 matrix, clipped to gamut, and normalised so the brightest channel
    /// saturates. Normalising is what makes this a <i>colour</i> rather than a brightness: a star's brightness
    /// at a planet is a separate published quantity (insolation), and conflating the two is how red dwarfs end
    /// up drawn as dim little coals.</para>
    ///
    /// <para><b>Check values</b>, so a change here is caught rather than admired. From the research note's
    /// independent run of the same method: the Sun (5772 K) is <c>#FFF1EA</c>, TRAPPIST-1 (2566 K) is
    /// <c>#FFA94F</c>, Beta Pictoris (8039 K) is <c>#E2E7FF</c>, and the Sun's 400-700 nm fraction is 0.367
    /// against TRAPPIST-1's 0.039. <c>SystemProfileBuilder</c>'s self-check menu item prints these. As written
    /// this reproduces <b>all eleven</b> hex values in that table exactly, and its visible fractions to within
    /// 0.002 — the residual is the integration grid, not the method.</para>
    ///
    /// <para><b>The one thing this is not.</b> These are the colours of the light as it arrives, against a D65
    /// white reference — right for a star seen against black sky, and right for the tint of the light falling
    /// on a body. They are <i>not</i> what the ground would look like to somebody standing there, because a
    /// human eye adapts to its only light source and would see a white rock as white. The familiar "everything
    /// is red" painting of an M-dwarf world is a colorimetric statement dressed up as a perceptual one. This
    /// app uses the unadapted convention, because everything it draws is seen from outside.</para>
    /// </summary>
    public static class StarColor
    {
        /// <summary>IAU 2015 nominal solar effective temperature, the reference every ratio here is against.</summary>
        public const float SunEffectiveTemperatureK = 5772f;

        /// <summary>The band a human eye works in, and the band the 'visible fraction' is measured over.</summary>
        public const float VisibleLowNm = 400f;
        public const float VisibleHighNm = 700f;

        // Physical constants, SI. Only their combination matters, but writing them out keeps the formula
        // readable against any textbook.
        private const double PlanckH = 6.62607015e-34;    // J s
        private const double LightC = 2.99792458e8;       // m / s
        private const double BoltzmannK = 1.380649e-23;   // J / K
        private const double StefanBoltzmann = 5.670374419e-8; // W m^-2 K^-4

        // CIE integration range and step. 1 nm over the full matching-function support: this runs at build
        // time, a few hundred times at most, so there is no reason to economise and lose a digit.
        private const int CieLowNm = 360;
        private const int CieHighNm = 830;

        /// <summary>
        /// Below this the fits and the Planck integral are still arithmetic but the answer stops meaning
        /// anything — no star this cool exists, and a zero or negative temperature would divide by zero.
        /// </summary>
        private const float MinimumTemperatureK = 500f;

        /// <summary>
        /// The star's continuum colour, gamma-encoded sRGB, brightest channel at 1.
        ///
        /// <para>Gamma-encoded is deliberate and it is the project's colour space, not a choice made here:
        /// this project runs in Gamma colour space (<c>ProjectSettings m_ActiveColorSpace: 0</c>), so a
        /// material colour is handed to the shader as authored. Write this straight into a material; do not
        /// linearise it first.</para>
        /// </summary>
        public static Color Srgb(float temperatureK)
        {
            var linear = LinearRgb(temperatureK);
            return new Color(Encode(linear.r), Encode(linear.g), Encode(linear.b), 1f);
        }

        /// <summary>
        /// The same colour in linear sRGB, brightest channel at 1. For arithmetic — blending two stars of a
        /// binary, or scaling by a brightness — which has to happen in linear light to mean anything.
        /// </summary>
        public static Color LinearRgb(float temperatureK)
        {
            var t = Mathf.Max(MinimumTemperatureK, temperatureK);

            double x = 0d, y = 0d, z = 0d;
            for (var nm = CieLowNm; nm <= CieHighNm; nm++)
            {
                var spectral = SpectralRadiance(nm, t);
                x += spectral * CieX(nm);
                y += spectral * CieY(nm);
                z += spectral * CieZ(nm);
            }

            // The step is constant and the result is normalised, so dropping the 1 nm width changes nothing.
            var r = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            var g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            var b = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

            // Out-of-gamut channels come back negative for the coolest and hottest stars. Clipping to zero is
            // the standard gamut clip and is why TRAPPIST-1 reads as a saturated amber rather than as a
            // meaningless colour; the alternative, desaturating toward white, would make every cool star look
            // more sunlike than it is.
            r = Math.Max(0d, r);
            g = Math.Max(0d, g);
            b = Math.Max(0d, b);

            var peak = Math.Max(r, Math.Max(g, b));
            if (peak <= 0d)
            {
                return Color.white;
            }

            return new Color((float)(r / peak), (float)(g / peak), (float)(b / peak), 1f);
        }

        /// <summary>
        /// The fraction of the star's total output that falls between 400 and 700 nm.
        ///
        /// <para>This is the single most commonly dropped fact about cool stars, and the reason it is here
        /// rather than in a document: TRAPPIST-1 puts 3.9 per cent of its output in the visible band against
        /// the Sun's 36.7 per cent, so a planet receiving four times Earth's total insolation can still be in
        /// permanent deep twilight. Any brightness the app derives from insolation has to be multiplied by
        /// this or it will draw red-dwarf worlds far too bright.</para>
        ///
        /// <para>Computed as the band integral over the analytic total, sigma T^4 / pi, so the denominator
        /// carries no numerical error at all.</para>
        /// </summary>
        public static float VisibleBandFraction(float temperatureK)
        {
            var t = Mathf.Max(MinimumTemperatureK, temperatureK);

            // 1 nm steps, midpoint rule, in metres of wavelength: the integrand is smooth over 300 nm and
            // this agrees with a finer grid to more digits than anyone will read.
            var band = 0d;
            for (var nm = (int)VisibleLowNm; nm < (int)VisibleHighNm; nm++)
            {
                band += SpectralRadiance(nm + 0.5, t) * 1e-9;
            }

            var total = StefanBoltzmann * Math.Pow(t, 4d) / Math.PI;
            return total <= 0d ? 0f : (float)(band / total);
        }

        /// <summary>
        /// How bright this star's light is in the visible band, relative to the Sun's at Earth, given the
        /// insolation the archive publishes for the planet. Earth is 1.
        ///
        /// <para><c>insolationEarth</c> is a <i>bolometric</i> ratio — all wavelengths — which is why it
        /// cannot be used as a brightness on its own.</para>
        /// </summary>
        public static float VisibleBrightnessEarth(float temperatureK, float insolationEarth)
        {
            if (insolationEarth <= 0f)
            {
                return 0f;
            }

            var sun = VisibleBandFraction(SunEffectiveTemperatureK);
            if (sun <= 0f)
            {
                return 0f;
            }

            return insolationEarth * VisibleBandFraction(temperatureK) / sun;
        }

        /// <summary>"#RRGGBB", for a build report that can be checked against a published table by eye.</summary>
        public static string Hex(Color srgb)
        {
            var r = Mathf.RoundToInt(Mathf.Clamp01(srgb.r) * 255f);
            var g = Mathf.RoundToInt(Mathf.Clamp01(srgb.g) * 255f);
            var b = Mathf.RoundToInt(Mathf.Clamp01(srgb.b) * 255f);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        // ---------- Planck

        /// <summary>
        /// Planck's law in wavelength form, per metre of wavelength, for a wavelength given in nanometres:
        /// 2hc^2 / lambda^5 / (exp(hc / lambda k T) - 1).
        /// </summary>
        private static double SpectralRadiance(double nm, double temperatureK)
        {
            var lambda = nm * 1e-9;
            var exponent = PlanckH * LightC / (lambda * BoltzmannK * temperatureK);

            // exp overflows to infinity for short wavelengths at low temperatures, which would give NaN
            // rather than the zero the physics asks for.
            if (exponent > 700d)
            {
                return 0d;
            }

            var denominator = Math.Exp(exponent) - 1d;
            if (denominator <= 0d)
            {
                return 0d;
            }

            return 2d * PlanckH * LightC * LightC / (Math.Pow(lambda, 5d) * denominator);
        }

        // ---------- CIE 1931, analytic
        //
        // Wyman, Sloan and Shirley 2013, the multi-lobe piecewise-Gaussian fit. Each lobe is a Gaussian with
        // a different width either side of its peak, which is what lets three or fewer lobes track curves
        // that a symmetric Gaussian misses by a long way in the blue.

        private static double CieX(double nm)
        {
            return 1.056 * Lobe(nm, 599.8, 37.9, 31.0)
                   + 0.362 * Lobe(nm, 442.0, 16.0, 26.7)
                   - 0.065 * Lobe(nm, 501.1, 20.4, 26.2);
        }

        private static double CieY(double nm)
        {
            return 0.821 * Lobe(nm, 568.8, 46.9, 40.5)
                   + 0.286 * Lobe(nm, 530.9, 16.3, 31.1);
        }

        private static double CieZ(double nm)
        {
            return 1.217 * Lobe(nm, 437.0, 11.8, 36.0)
                   + 0.681 * Lobe(nm, 459.0, 26.0, 13.8);
        }

        private static double Lobe(double nm, double peak, double widthBelow, double widthAbove)
        {
            var t = (nm - peak) / (nm < peak ? widthBelow : widthAbove);
            return Math.Exp(-0.5 * t * t);
        }

        // ---------- sRGB transfer

        /// <summary>Linear light to an sRGB-encoded channel: the IEC 61966-2-1 transfer function.</summary>
        private static float Encode(float linear)
        {
            var c = Mathf.Clamp01(linear);
            return c <= 0.0031308f
                ? 12.92f * c
                : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }
    }
}
