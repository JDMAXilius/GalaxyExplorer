// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// One figure on a body panel: a small label over a big number, with its unit beside it. A non-zero
    /// <see cref="Exponent"/> renders as a superscript, so mass reads as "5.97 x 10^24 kg".
    /// </summary>
    [Serializable]
    public struct Stat
    {
        [Tooltip("Small caps above the number, e.g. DISTANCE FROM EARTH.")]
        public string Label;

        [Tooltip("The number as it should read, separators included, e.g. 384,400.")]
        public string Value;

        [Tooltip("Shown small after the number, e.g. km.")]
        public string Unit;

        [Tooltip("Power of ten, rendered as a superscript. 0 means none.")]
        public int Exponent;

        /// <summary>The stat as one line of rich text, superscript included.</summary>
        public string ToRichText()
        {
            var number = Exponent == 0 ? Value : $"{Value}<sup>{Exponent}</sup>";
            return string.IsNullOrEmpty(Unit) ? number : $"{number} {Unit}";
        }
    }

    /// <summary>
    /// A body the player can pull out and hold: the Sun, a planet, a dwarf planet or a moon. Moons use the same
    /// type with fewer stats, and are listed by the planet they orbit. Authored in <c>docs/copy/bodies.md</c> and
    /// <c>docs/copy/moons.md</c>, imported by <c>CopyImporter</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Cosmic Simulation/Body Info", fileName = "body_info")]
    public class BodyInfo : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable key, matching the heading in docs/copy/. Never shown to the player.")]
        public string Id;

        public string DisplayName;

        [Tooltip("Small caps under the title on a body panel, e.g. OUR HOME PLANET.")]
        public string Subtitle;

        [Header("Content")]
        [TextArea(2, 6)]
        public string Paragraph;

        [Tooltip("Four for a planet; three for a moon (parent, diameter, orbit).")]
        public Stat[] Stats = Array.Empty<Stat>();

        [Header("Family")]
        [Tooltip("Moons that appear orbiting this body once it has been pulled out.")]
        public BodyInfo[] Moons = Array.Empty<BodyInfo>();

        [Tooltip("Set on a moon: the body it orbits.")]
        public BodyInfo Orbits;

        [Header("Audio")]
        public AudioClip Narration;
        public AudioClip Ambience;

        public bool IsMoon => Orbits != null;
    }
}
