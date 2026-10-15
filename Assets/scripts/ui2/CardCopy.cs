// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace CosmicSimulation.UI2
{
    /// <summary>What the card says, worked out from the copy and the body data; no UI here, so it can be tested (CS-348).</summary>
    public static class CardCopy
    {
        public struct Fact
        {
            public string Label;
            public string Value;
            public string Unit;
        }

        private static readonly string[] SecondFactLabels = { "orbital period", "one orbit", "orbit of the galaxy" };

        /// <summary>A body's two headline facts: its diameter, then how long one orbit takes.</summary>
        public static (Fact? first, Fact? second) BodyFacts(BodyInfo info)
        {
            Fact? size = null;
            Fact? orbit = null;
            var spare = new List<Fact>();
            foreach (var stat in info.Stats)
            {
                var label = (stat.Label ?? string.Empty).ToLowerInvariant();
                var fact = new Fact
                {
                    Label = Sentence(stat.Label),
                    Value = stat.Exponent == 0 ? stat.Value : stat.Value + "<sup>" + stat.Exponent + "</sup>",
                    Unit = stat.Unit,
                };
                if (size == null && label == "diameter")
                {
                    size = fact;
                }
                else if (orbit == null && Array.IndexOf(SecondFactLabels, label) >= 0)
                {
                    fact.Label = label == "orbit of the galaxy" ? fact.Label : "Orbit";
                    orbit = fact;
                }
                else
                {
                    spare.Add(fact);
                }
            }

            var a = size ?? (spare.Count > 0 ? spare[0] : (Fact?)null);
            var b = orbit ?? (spare.Count > (size == null ? 1 : 0) ? spare[size == null ? 1 : 0] : (Fact?)null);
            return (a, b);
        }

        public static Fact? PairFact(BodyInfo info)
        {
            foreach (var stat in info.Stats)
            {
                if (stat.Label != null && stat.Label.ToLowerInvariant() == "diameter")
                {
                    return new Fact { Label = info.DisplayName + " diameter", Value = stat.Value, Unit = stat.Unit };
                }
            }

            return null;
        }

        /// <summary>A place's facts come from its second line, written "Kind - distance" in the copy files.</summary>
        public static (Fact? first, Fact? second) PlaceFacts(ExperienceModule module)
        {
            var line = module.SecondLine ?? string.Empty;
            var cut = line.IndexOf(" - ", StringComparison.Ordinal);
            if (cut <= 0 || cut + 3 >= line.Length || !char.IsDigit(line[cut + 3]))
            {
                return (null, null);
            }

            var kind = line.Substring(0, cut).Trim();
            var distance = line.Substring(cut + 3).Trim();
            var space = distance.IndexOf(' ');
            var kindSpace = kind.IndexOf(' ');
            return (
                new Fact
                {
                    Label = "Distance",
                    Value = space > 0 ? distance.Substring(0, space) : distance,
                    Unit = space > 0 ? distance.Substring(space + 1) : string.Empty,
                },
                new Fact
                {
                    Label = "Type",
                    Value = kindSpace > 0 ? kind.Substring(0, kindSpace) : kind,
                    Unit = kindSpace > 0 ? kind.Substring(kindSpace + 1) : string.Empty,
                });
        }

        // The copy files write stat labels in capitals for the legacy panel; the card uses sentence case.
        public static string Sentence(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            var lower = label.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        public static string Opening(ScenePanelCopy copy) =>
            copy != null && copy.Paragraphs != null && copy.Paragraphs.Length > 0 ? Sentences(copy.Paragraphs[0]) : string.Empty;

        /// <summary>The card is a summary: the first sentence, and the second when the two stay short.</summary>
        public static string Sentences(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var first = text.IndexOf(". ", StringComparison.Ordinal);
            if (first < 0)
            {
                return text;
            }

            var second = text.IndexOf(". ", first + 2, StringComparison.Ordinal);
            var end = second > 0 && second < 150 ? second : first;
            return text.Substring(0, end + 1);
        }

        /// <summary>"Andromeda" rather than "Andromeda (M31)": the catalogue number stays in the copy.</summary>
        public static string ShortName(ExperienceModule module)
        {
            var name = !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : module.Panel.Title;
            var bracket = name.IndexOf(" (", StringComparison.Ordinal);
            name = bracket > 0 ? name.Substring(0, bracket) : name;
            return name.EndsWith(" Galaxy", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7) : name;
        }

        public static bool Diameter(BodyInfo info, out float kilometres)
        {
            kilometres = 0f;
            foreach (var stat in info.Stats)
            {
                if (stat.Label != null && stat.Label.ToLowerInvariant().Contains("diameter"))
                {
                    return float.TryParse(stat.Value.Replace(",", string.Empty), NumberStyles.Float,
                                          CultureInfo.InvariantCulture, out kilometres) && kilometres > 0f;
                }
            }

            return false;
        }
    }
}
