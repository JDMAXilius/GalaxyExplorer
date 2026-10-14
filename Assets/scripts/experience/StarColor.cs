// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A star's light colour from its effective temperature (<c>st_teff</c>): Planck spectrum over 360-830 nm
    /// against the Wyman, Sloan and Shirley 2013 CIE 1931 fits (jcgt.org/published/0002/02/01), D65 sRGB,
    /// gamut-clipped, brightest channel at 1. Unadapted (seen from outside), so brightness stays separate.
    /// Check values (docs/research/exoplanet_systems.md 4.2): Sun 5772 K #FFF1EA, TRAPPIST-1 2566 K #FFA94F.
    /// </summary>
    public static class StarColor
    {
        /// <summary>IAU 2015 nominal solar effective temperature, the reference every ratio here is against.</summary>
        public const float SunEffectiveTemperatureK = 5772f;

        /// <summary>The band a human eye works in, and the band the 'visible fraction' is measured over.</summary>
        public const float VisibleLowNm = 400f;
        public const float VisibleHighNm = 700f;

        // Physical constants, SI (CODATA 2018 exact values).
        private const double PlanckH = 6.62607015e-34;    // J s
        private const double LightC = 2.99792458e8;       // m / s
        private const double BoltzmannK = 1.380649e-23;   // J / K
        private const double StefanBoltzmann = 5.670374419e-8; // W m^-2 K^-4

        // CIE matching-function support, 1 nm steps: this runs at build time only, so precision beats speed.
        private const int CieLowNm = 360;
        private const int CieHighNm = 830;

        /// <summary>No star is this cool; the floor also keeps a zero or negative temperature from dividing by zero.</summary>
        private const float MinimumTemperatureK = 500f;

        /// <summary>
        /// Continuum colour, gamma-encoded sRGB, brightest channel at 1. The project is in Gamma colour space,
        /// so write this straight into a material; do not linearise it.
        /// </summary>
        public static Color Srgb(float temperatureK)
        {
            var linear = LinearRgb(temperatureK);
            return new Color(Encode(linear.r), Encode(linear.g), Encode(linear.b), 1f);
        }

        /// <summary>The same colour in linear sRGB, for blending or scaling, which must happen in linear light.</summary>
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

            // XYZ to linear sRGB, standard D65 matrix; the constant 1 nm step cancels in the normalisation.
            var r = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            var g = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            var b = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

            // Clip negative (out-of-gamut) channels to zero; desaturating toward white instead would make
            // cool stars look more sunlike than they are.
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
        /// Fraction of total output in 400-700 nm (Sun 0.367, TRAPPIST-1 0.039); multiply any insolation-derived
        /// brightness by this. Denominator is the analytic total, sigma T^4 / pi.
        /// </summary>
        public static float VisibleBandFraction(float temperatureK)
        {
            var t = Mathf.Max(MinimumTemperatureK, temperatureK);

            // 1 nm midpoint rule, width in metres to match the per-metre radiance.
            var band = 0d;
            for (var nm = (int)VisibleLowNm; nm < (int)VisibleHighNm; nm++)
            {
                band += SpectralRadiance(nm + 0.5, t) * 1e-9;
            }

            var total = StefanBoltzmann * Math.Pow(t, 4d) / Math.PI;
            return total <= 0d ? 0f : (float)(band / total);
        }

        /// <summary>
        /// Visible-band brightness relative to the Sun's at Earth (Earth = 1). <c>insolationEarth</c> is
        /// bolometric, which is why it cannot be used as a brightness on its own.
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

        /// <summary>Planck's law per metre of wavelength, wavelength in nm: 2hc^2 / lambda^5 / (exp(hc / lambda k T) - 1).</summary>
        private static double SpectralRadiance(double nm, double temperatureK)
        {
            var lambda = nm * 1e-9;
            var exponent = PlanckH * LightC / (lambda * BoltzmannK * temperatureK);

            // exp overflows for short wavelengths at low temperatures, giving NaN where physics says zero.
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
        // Wyman, Sloan and Shirley 2013 multi-lobe fit: each lobe is a Gaussian with different widths either side.

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
