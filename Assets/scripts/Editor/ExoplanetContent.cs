using System.Collections.Generic;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the <see cref="BodyInfo"/> assets and the <see cref="ExperienceModule"/> that a real exoplanet
    /// system needs before <c>SystemProfileBuilder</c> can build it.
    /// <para>
    /// Our own ten bodies get their copy from <c>docs/copy/bodies.md</c> through <c>CopyImporter</c>. An
    /// exoplanet system is different in one way that matters: almost every interesting number is either
    /// unmeasured or measured only as an upper limit, and the whole point of D-010's truthfulness clause is
    /// that those have to read as absences rather than as figures. Writing the stats here, next to the
    /// profile that declares the same system, keeps the two from drifting - a stat that says "not yet
    /// measured" and an appearance that quietly assumes a value would be exactly the kind of disagreement
    /// nobody notices.
    /// </para>
    /// <para>
    /// Every figure below comes from <c>docs/research/exoplanet_systems.md</c> section 7.9, which took them
    /// from the NASA Exoplanet Archive (<c>pscomppars</c>) and Luque et al. 2023, Nature
    /// (arxiv.org/abs/2311.17775). Where the archive and the paper disagree - the star's temperature and
    /// radius - the archive composite is used throughout, because mixing two sources is how a system ends up
    /// internally inconsistent.
    /// </para>
    /// </summary>
    public static class ExoplanetContent
    {
        private const string BodyFolder = "Assets/data/bodies";
        private const string ModuleFolder = "Assets/data/experiences";

        /// <summary>
        /// One body's copy and stats. <c>Mass</c> is deliberately a string plus an exponent rather than a
        /// number: three of these six planets have only an upper limit on their mass, and the honest display
        /// for those is words, not a figure.
        /// </summary>
        private readonly struct Body
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Subtitle;
            public readonly string Paragraph;
            public readonly string DiameterKm;
            public readonly string Mass;          // mantissa, or the words that replace it
            public readonly int MassExponent;     // 0 when Mass is words
            public readonly string OrbitalPeriod;
            public readonly string OrbitalDistance;

            public Body(string id, string name, string subtitle, string paragraph, string diameterKm,
                        string mass, int massExponent, string orbitalPeriod, string orbitalDistance)
            {
                Id = id;
                Name = name;
                Subtitle = subtitle;
                Paragraph = paragraph;
                DiameterKm = diameterKm;
                Mass = mass;
                MassExponent = massExponent;
                OrbitalPeriod = orbitalPeriod;
                OrbitalDistance = orbitalDistance;
            }
        }

        private const string NotMeasured = "Not yet measured";

        /// <summary>
        /// HD 110067's seven bodies. Radii are measured for all six planets because all six transit; masses
        /// exist for b, d and f only, and c, e and g carry published upper limits which are <b>not</b> shown
        /// as values.
        /// </summary>
        private static readonly Body[] Hd110067 =
        {
            new Body("hd110067_star", "HD 110067", "THE HOST STAR",
                "A K-type dwarf a little cooler and smaller than the Sun, in Coma Berenices, about 105 light " +
                "years away. It is bright enough to keep studying, and old - somewhere around seven to eight " +
                "billion years. From any of its six planets it would look like a warm white disc several " +
                "times wider than the Sun looks from Earth.",
                "1,097,448", "1.59", 30, NotMeasured, "0"),

            new Body("hd110067_b", "HD 110067 b", "FIRST IN THE CHAIN",
                "The innermost of six worlds locked into a rhythm: for every two orbits this planet makes, " +
                "the next one out makes three, and that pattern continues all the way to the sixth. Its " +
                "measured density is far too low for rock, so it almost certainly carries a deep hydrogen " +
                "atmosphere with no surface to stand on.",
                "28,032", "3.40", 25, "9.11", "0.0793"),

            new Body("hd110067_c", "HD 110067 c", "SECOND IN THE CHAIN",
                "Its size is measured, because it passes in front of its star and blocks a little light. Its " +
                "mass is not - only an upper limit is known, so how heavy it is remains genuinely open.",
                "30,428", NotMeasured, 0, "13.67", "0.1039"),

            new Body("hd110067_d", "HD 110067 d", "THIRD IN THE CHAIN",
                "The largest of the six. Its density works out at about a third of Earth's, which is the " +
                "signature of a small rocky core wrapped in an enormous envelope of hydrogen and helium.",
                "36,340", "5.09", 25, "20.52", "0.1362"),

            new Body("hd110067_e", "HD 110067 e", "FOURTH IN THE CHAIN",
                "The smallest of the six, and the one the discovery team singled out as possibly different - " +
                "it may be the only member of the family without a huge hydrogen envelope. Its mass has not " +
                "been measured, so that remains a maybe.",
                "24,719", NotMeasured, 0, "30.79", "0.1785"),

            new Body("hd110067_f", "HD 110067 f", "FIFTH IN THE CHAIN",
                "Less dense than Neptune. Whatever this world is made of, most of its volume is atmosphere, " +
                "and the solid part of it must be small.",
                "33,142", "3.01", 25, "41.06", "0.2163"),

            new Body("hd110067_g", "HD 110067 g", "SIXTH IN THE CHAIN",
                "The outermost known planet, taking about 55 days to go round. Even out here it receives six " +
                "times the sunlight Earth does - this system is packed far tighter than ours, and none of it " +
                "is in the zone where liquid water could sit on a surface.",
                "33,218", NotMeasured, 0, "54.77", "0.2621"),
        };

        private static readonly string[] Hd110067Panel =
        {
            "Six worlds around a star slightly cooler than the Sun, 105 light years away, moving in a rhythm " +
            "that has held for billions of years. Each planet's year is a simple fraction of its neighbour's - " +
            "three orbits to two, over and over, then four to three for the outer pair. Systems are born like " +
            "this and almost always lose it; this one never did.",

            "All six are larger than Earth and far lighter than their size suggests, which means deep " +
            "hydrogen atmospheres rather than ground. Nobody has photographed them and nobody has seen their " +
            "surfaces, because they may not have any. What you are looking at is built from measured sizes " +
            "and orbits - the appearance is our best reading of the physics, not a picture.",

            "The whole system would fit comfortably inside the orbit of Mercury.",
        };

        [MenuItem("Cosmic Simulation/Build Exoplanet Content")]
        public static void Build()
        {
            var made = EnsureBodies(Hd110067);
            var module = EnsureModule("hd110067", "HD 110067", "SIX WORLDS IN STEP", Hd110067Panel);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"ExoplanetContent: {made} BodyInfo asset(s) written or updated, module " +
                      $"{(module ? "ready" : "FAILED")}.\n" +
                      "Next: Cosmic Simulation > Build System Profiles, then Build System Layouts, " +
                      "then Build System Content.");
        }

        private static int EnsureBodies(IEnumerable<Body> bodies)
        {
            var count = 0;

            foreach (var body in bodies)
            {
                var path = $"{BodyFolder}/{body.Id}.asset";
                var info = AssetDatabase.LoadAssetAtPath<BodyInfo>(path);
                var existed = info != null;

                if (!existed)
                {
                    info = ScriptableObject.CreateInstance<BodyInfo>();
                    AssetDatabase.CreateAsset(info, path);
                }

                var so = new SerializedObject(info);
                so.FindProperty("Id").stringValue = body.Id;
                so.FindProperty("DisplayName").stringValue = body.Name;
                so.FindProperty("Subtitle").stringValue = body.Subtitle;
                so.FindProperty("Paragraph").stringValue = body.Paragraph;

                var stats = so.FindProperty("Stats");
                var rows = new List<(string label, string value, string unit, int exponent)>
                {
                    ("Diameter", body.DiameterKm, "km", 0),
                    ("Mass", body.Mass, body.Mass == NotMeasured ? string.Empty : "kg", body.MassExponent),
                };

                // A star has no orbital period or distance of its own here, and a zero would read as a
                // measurement. Leave the rows out entirely rather than print "0".
                if (body.OrbitalPeriod != NotMeasured)
                {
                    rows.Add(("Orbital Period", body.OrbitalPeriod, "days", 0));
                    rows.Add(("Orbital Distance", body.OrbitalDistance, "AU", 0));
                }

                // Every one of these worlds is expected to be tidally locked and not one has a measured
                // rotation period, so this says so rather than repeating the year or inventing a figure.
                rows.Add(("Day Length", NotMeasured, string.Empty, 0));

                stats.arraySize = rows.Count;
                for (var i = 0; i < rows.Count; i++)
                {
                    var element = stats.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Label").stringValue = rows[i].label;
                    element.FindPropertyRelative("Value").stringValue = rows[i].value;
                    element.FindPropertyRelative("Unit").stringValue = rows[i].unit;
                    element.FindPropertyRelative("Exponent").intValue = rows[i].exponent;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(info);
                count++;
            }

            return count;
        }

        private static bool EnsureModule(string id, string displayName, string secondLine, string[] paragraphs)
        {
            var path = $"{ModuleFolder}/{id}.asset";
            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(path);

            if (module == null)
            {
                module = ScriptableObject.CreateInstance<ExperienceModule>();
                AssetDatabase.CreateAsset(module, path);
            }

            var so = new SerializedObject(module);
            so.FindProperty("Id").stringValue = id;

            // A Destination, like the nebulae and the new galaxies - reached by pinching a tag inside a
            // parent view, not a new dock tile. The dock is seven tiles and GDD 3.1 says so.
            so.FindProperty("Kind").enumValueIndex = (int)ExperienceKind.Destination;
            so.FindProperty("DisplayName").stringValue = displayName;
            so.FindProperty("SecondLine").stringValue = secondLine;

            // Dimmed, matching the solar system views: this is a thing held in a lit room.
            so.FindProperty("Environment").enumValueIndex = (int)EnvironmentMode.Dimmed;

            var panel = so.FindProperty("Panel");
            panel.FindPropertyRelative("Title").stringValue = displayName;
            var array = panel.FindPropertyRelative("Paragraphs");
            array.arraySize = paragraphs.Length;
            for (var i = 0; i < paragraphs.Length; i++)
            {
                array.GetArrayElementAtIndex(i).stringValue = paragraphs[i];
            }

            panel.FindPropertyRelative("Instruction").stringValue =
                "Pinch a world to lift it out, two hands to grow it, let go to leave it.";

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);
            return true;
        }
    }
}
