using System.Collections.Generic;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The galaxies beyond Andromeda, as profiles for <see cref="GalaxyBuilder"/>.
    /// <para>
    /// Three to begin with, and they are the three chosen in <c>docs/research/galaxies.md</c> that need
    /// <b>no generator feature that does not exist</b>: all are spiral-arm galaxies seen at a workable angle.
    /// NGC 1300 wants a bar term, M87 and Centaurus A want an elliptical generator, and the Antennae want
    /// tidal tails - each of those is a ticket, not a profile, and <see cref="GalaxyProfile.Problems"/>
    /// refuses a profile whose generator is missing rather than drawing arms on something that has none.
    /// </para>
    /// <para>
    /// <b>Read the confidence note at the top of <c>docs/research/galaxies.md</c> before trusting a number
    /// here.</b> Arm counts and orientations are textbook and safe. Pitch angles are the weakest column -
    /// published values disagree by up to ten degrees and depend on how they were measured - so they are
    /// good enough to make a galaxy look like itself and <b>not</b> good enough to print in a panel. No
    /// figure in this file has a citation yet; that is CS-127.
    /// </para>
    /// <para>
    /// Winding is always derived through <see cref="GalaxyProfile.WindingFromPitchAngle"/> rather than
    /// written as a literal, which is the opposite of Andromeda's profile. Andromeda keeps its literal 470
    /// because re-deriving it would rewrite 17,200 already-committed stars for no visible change; a new
    /// galaxy has nothing baked yet, so it starts honest - the pitch angle is the input, and the winding is
    /// whatever that implies.
    /// </para>
    /// </summary>
    public static class GalaxyLibrary
    {
        /// <summary>
        /// The radial range the disc layer covers, and therefore the range a pitch angle has to be fitted
        /// across. Kept next to the layer specs that use it so the two cannot drift apart: the conversion is
        /// only meaningful if these are the same numbers the stars layer is actually built with.
        /// </summary>
        private const float DiscInner = 0.1f;

        private const float DiscOuter = 1f;

        /// <summary>Every profile this file contributes, in the order they should be built.</summary>
        public static GalaxyProfile[] All() => new[]
        {
            Whirlpool(),
            Pinwheel(),
            Triangulum(),
        };

        // ------------------------------------------------------------------ M51

        /// <summary>
        /// M51, the Whirlpool. The archetype of a "grand design" spiral: two arms so well defined that it is
        /// the picture most people have in mind when they hear the word galaxy. Seen very nearly face-on,
        /// which is the whole reason it looks like that from here.
        /// <para>
        /// Its companion NGC 5195 sits at the end of the northern arm and is physically interacting with it.
        /// The companion is <b>not built here</b>: it is a small early-type galaxy with no arms, so it needs
        /// the elliptical generator kind. Until that exists this is M51 without the thing tugging its arm,
        /// which is a real omission rather than a simplification - see <c>docs/research/galaxies.md</c>.
        /// </para>
        /// </summary>
        public static GalaxyProfile Whirlpool()
        {
            const float pitchDegrees = 18f;   // literature clusters around 15-20 for M51
            const float inclination = 20f;    // close to face-on
            const float yaw = 20f;

            return new GalaxyProfile
            {
                Id = "whirlpool",
                DisplayName = "Whirlpool Galaxy (M51)",
                SecondLine = "M51",
                Kind = GalaxyKind.SpiralArms,

                // Slightly smaller than Andromeda's 1.2 m. M51 is a genuinely smaller galaxy, and the set
                // reads better if the sizes are not all identical.
                WidthMetres = 1.1f,

                DiscTiltDegrees = GalaxyProfile.TiltForPresentedInclination(inclination, yaw),
                DiscYawDegrees = yaw,

                MinScaleMetres = 0.5f,
                MaxScaleMetres = 3f,
                GrabRadiusFraction = 1f,

                // NOT a cosmetic squash - this is what makes arms exist at all. The generator sweeps a
                // family of nested ellipses, each rotated a little further, and it is their eccentricity that
                // curls them into arms. At 0.95 the ellipses are nearly circular and the galaxy comes out a
                // featureless disc; Andromeda's 0.72 is what gives it visible arms. M51 is the strongest
                // grand design in the set, so it goes further still.
                XRadii = 0.62f,
                ZRadii = 1f,

                ArmCount = 2,
                WindingDegrees = GalaxyProfile.WindingFromPitchAngle(pitchDegrees, DiscInner, DiscOuter),

                VelocityMultiplier = 0.05f,

                Layers = SpiralLayers(
                    cloudsMax: 0.45f,       // a modest bulge; the arms are the object
                    dustMin: 0.18f,
                    hiiWeight: 1.15f),      // M51's arms are beaded with pink HII regions

                Palette = new GalaxyPalette
                {
                    // Yellow-white core, blue-white arms. The pink comes from the HII accent below.
                    Disc = new[]
                    {
                        (0.00f, new Color(1.00f, 0.95f, 0.86f)),
                        (0.25f, new Color(0.94f, 0.92f, 0.88f)),
                        (0.55f, new Color(0.72f, 0.82f, 0.98f)),
                        (1.00f, new Color(0.58f, 0.74f, 1.00f)),
                    },
                    Clouds = new[]
                    {
                        (0.00f, new Color(1.00f, 0.97f, 0.90f)),
                        (0.50f, new Color(0.97f, 0.90f, 0.79f)),
                        (1.00f, new Color(0.80f, 0.68f, 0.60f)),
                    },
                    // Dust lanes ride the inner edge of each arm, and read cooler than Andromeda's warm ring.
                    Dust = new[]
                    {
                        (0.00f, new Color(0.42f, 0.34f, 0.34f)),
                        (0.55f, new Color(0.52f, 0.40f, 0.38f)),
                        (1.00f, new Color(0.44f, 0.36f, 0.36f)),
                    },
                    HiiKnot = new Color(1.00f, 0.42f, 0.52f),   // the pink that makes M51 read as M51
                    BlueGiant = new Color(0.60f, 0.78f, 1.00f),
                },
            };
        }

        // ------------------------------------------------------------------ M101

        /// <summary>
        /// M101, the Pinwheel. Big - around 170,000 light years, comfortably larger than the Milky Way - and
        /// face-on, with many arms of unequal strength rather than a clean two-arm design.
        /// <para>
        /// <b>Known simplification:</b> M101 is genuinely lopsided, one side brighter and more extended than
        /// the other. This generator sweeps symmetric ellipse families, so what comes out is more regular
        /// than the real galaxy. Asymmetry would need per-arm strength, which the generator does not have.
        /// </para>
        /// </summary>
        public static GalaxyProfile Pinwheel()
        {
            // 26 deg over the disc's 0.1-1.0 range is about 272 deg of winding, which against 3 arms
            // (120 deg apart) is a ratio of 2.3 - just inside what reads. See the arm-legibility note on
            // SpiralLayers.
            const float pitchDegrees = 26f;
            const float inclination = 18f;
            const float yaw = 15f;

            return new GalaxyProfile
            {
                Id = "pinwheel",
                DisplayName = "Pinwheel Galaxy (M101)",
                SecondLine = "M101",
                Kind = GalaxyKind.SpiralArms,

                // The largest of the three on purpose. M101 really is bigger than Andromeda, and if every
                // galaxy arrives at the same size on the table then the set says nothing about scale.
                WidthMetres = 1.35f,

                DiscTiltDegrees = GalaxyProfile.TiltForPresentedInclination(inclination, yaw),
                DiscYawDegrees = yaw,

                MinScaleMetres = 0.5f,
                MaxScaleMetres = 3f,
                GrabRadiusFraction = 1f,

                // See the note on M51: this is the arm-forming parameter, not apparent squash.
                XRadii = 0.65f,
                ZRadii = 1f,

                // Three, not the five M101 actually shows, and this is a generator limit rather than a
                // choice. Five arms at any realistic pitch wind past each other and the disc renders as a
                // featureless wash - measured, not assumed: the five-arm bake was flat. Three is the most
                // this generator can separate while keeping a believable pitch.
                ArmCount = 3,
                WindingDegrees = GalaxyProfile.WindingFromPitchAngle(pitchDegrees, DiscInner, DiscOuter),

                VelocityMultiplier = 0.045f,

                Layers = SpiralLayers(
                    cloudsMax: 0.3f,        // a small bulge: this is a late-type disc
                    dustMin: 0.15f,
                    hiiWeight: 1.5f),       // M101 is studded with star-forming knots, more than M51

                Palette = new GalaxyPalette
                {
                    Disc = new[]
                    {
                        (0.00f, new Color(1.00f, 0.94f, 0.84f)),
                        (0.18f, new Color(0.90f, 0.90f, 0.92f)),
                        (0.50f, new Color(0.70f, 0.82f, 1.00f)),
                        (1.00f, new Color(0.56f, 0.74f, 1.00f)),
                    },
                    Clouds = new[]
                    {
                        (0.00f, new Color(1.00f, 0.96f, 0.88f)),
                        (0.55f, new Color(0.92f, 0.88f, 0.82f)),
                        (1.00f, new Color(0.74f, 0.72f, 0.72f)),
                    },
                    Dust = new[]
                    {
                        (0.00f, new Color(0.38f, 0.33f, 0.34f)),
                        (0.55f, new Color(0.46f, 0.38f, 0.38f)),
                        (1.00f, new Color(0.40f, 0.34f, 0.35f)),
                    },
                    HiiKnot = new Color(1.00f, 0.40f, 0.50f),
                    BlueGiant = new Color(0.62f, 0.80f, 1.00f),
                },
            };
        }

        // ------------------------------------------------------------------ M33

        /// <summary>
        /// M33, Triangulum. The third-largest member of the Local Group, after Andromeda and ourselves, and
        /// the nearest of these three by a wide margin.
        /// <para>
        /// Its arms are <b>flocculent</b> - many short, patchy, discontinuous fragments rather than two long
        /// sweeping ones - which is a genuinely different character from M51 and the reason it is in the set.
        /// It has essentially no bulge and no bar. The generator cannot make arms break up, so this is
        /// approximated with more arms at a looser pitch; the result reads busier than M51 rather than
        /// truly patchy, and that is a known limit.
        /// </para>
        /// </summary>
        public static GalaxyProfile Triangulum()
        {
            // 34 deg is about 205 deg of winding, which against 3 arms is a ratio of 1.7 - comfortably
            // inside legibility, and the loosest pitch in the set, which suits a late-type disc.
            const float pitchDegrees = 34f;
            const float inclination = 54f;    // a clear tilt, unlike the two above
            const float yaw = 35f;

            return new GalaxyProfile
            {
                Id = "triangulum",
                DisplayName = "Triangulum Galaxy (M33)",
                SecondLine = "M33",
                Kind = GalaxyKind.SpiralArms,

                WidthMetres = 0.95f,

                DiscTiltDegrees = GalaxyProfile.TiltForPresentedInclination(inclination, yaw),
                DiscYawDegrees = yaw,

                MinScaleMetres = 0.5f,
                MaxScaleMetres = 3f,
                GrabRadiusFraction = 1f,

                // The least eccentric of the three, deliberately: M33's arms are flocculent - patchy and
                // ill-defined - so weaker arm structure is closer to the truth here than a crisp spiral.
                XRadii = 0.7f,
                ZRadii = 1f,

                // Three. M33 is flocculent - many short patchy fragments - and this generator cannot break
                // an arm up, so four or more simply washed out when baked. Three at a loose pitch is the
                // closest legible thing, and it reads more organised than the real galaxy.
                ArmCount = 3,
                WindingDegrees = GalaxyProfile.WindingFromPitchAngle(pitchDegrees, DiscInner, DiscOuter),

                VelocityMultiplier = 0.05f,

                Layers = SpiralLayers(
                    cloudsMax: 0.18f,       // almost no bulge at all - the lowest in the set
                    dustMin: 0.12f,
                    hiiWeight: 1.6f),       // NGC 604, one of the largest known HII regions, lives here

                Palette = new GalaxyPalette
                {
                    // Bluer overall than the other two: a young, actively star-forming disc with little
                    // old-yellow population to warm the centre.
                    Disc = new[]
                    {
                        (0.00f, new Color(0.96f, 0.94f, 0.90f)),
                        (0.20f, new Color(0.84f, 0.88f, 0.96f)),
                        (0.55f, new Color(0.66f, 0.80f, 1.00f)),
                        (1.00f, new Color(0.54f, 0.73f, 1.00f)),
                    },
                    Clouds = new[]
                    {
                        (0.00f, new Color(0.98f, 0.95f, 0.90f)),
                        (0.55f, new Color(0.86f, 0.86f, 0.88f)),
                        (1.00f, new Color(0.70f, 0.74f, 0.80f)),
                    },
                    Dust = new[]
                    {
                        (0.00f, new Color(0.34f, 0.32f, 0.34f)),
                        (0.55f, new Color(0.42f, 0.37f, 0.38f)),
                        (1.00f, new Color(0.36f, 0.33f, 0.35f)),
                    },
                    HiiKnot = new Color(1.00f, 0.38f, 0.48f),
                    BlueGiant = new Color(0.64f, 0.82f, 1.00f),
                },
            };
        }

        // ------------------------------------------------------------------ shared layer shape

        /// <summary>
        /// The three-layer stack every spiral here uses, with the handful of knobs that actually differ
        /// between them exposed as arguments. Kept as one function rather than copied three times so that a
        /// change to the stack cannot land on one galaxy and miss the others.
        /// <para>
        /// Point counts match Andromeda's 17,200, which the build log reports as 4.3% of the Quest 3 point
        /// ceiling. Only one destination is ever open at a time, so the set costs what one galaxy costs.
        /// </para>
        /// </summary>
        /// <summary>
        /// <b>Arm legibility, measured rather than guessed.</b> Four bakes previewed through
        /// <see cref="GalaxyPreview"/> give a usable rule: arms only read when the total winding is under
        /// roughly <b>2.3x the arm spacing</b> (spacing being 360/arms). M51 at 406 deg over 2 arms is 2.26
        /// and reads; Andromeda at 470 over 2 arms is 2.6 and reads as rings. Five arms at 326 deg is 4.5
        /// and rendered as a featureless disc, as did four arms at 2.5. Past that ratio the arms wind across
        /// each other often enough that the disc averages out.
        /// <para>
        /// The practical consequence: <b>this generator does two- and three-arm galaxies.</b> A genuinely
        /// many-armed or flocculent galaxy has to be approximated with three, and the profile should say so
        /// rather than pretend. That is a generator limit worth fixing before the set grows much further -
        /// per-arm strength would let arms overlap without averaging out.
        /// </para>
        /// </summary>
        private static GalaxyLayerSpec[] SpiralLayers(float cloudsMax, float dustMin, float hiiWeight)
        {
            return new[]
            {
                new GalaxyLayerSpec
                {
                    Role = GalaxyLayerRole.Clouds,
                    Id = "clouds",
                    ShaderPath = GalaxyBuilder.CloudShaderPath,
                    Seed = 31415,
                    Ellipses = 250,
                    StarsPerEllipse = 10,
                    MinEllipseScale = 0.02f,
                    MaxEllipseScale = cloudsMax,
                    Fuzz = new Vector2(0.9f, 1.1f),
                    ThicknessAtCentre = 1f,
                    ThicknessAtRim = 0.25f,
                    YRange = 0.07f,
                    WorldSpaceScale = 0.02f,
                    Tint = Color.white,
                    TintMultiplier = 0.3f,
                    IsShadow = false,
                    DrawIndex = 0,
                },
                new GalaxyLayerSpec
                {
                    Role = GalaxyLayerRole.Dust,
                    Id = "dust",
                    ShaderPath = GalaxyBuilder.DustShaderPath,
                    Seed = 27182,
                    Ellipses = 200,
                    StarsPerEllipse = 8,
                    // Unlike Andromeda, whose dust is a ring starting well out at 0.46, these three have dust
                    // through most of the disc, following the arms rather than forming an annulus.
                    MinEllipseScale = dustMin,
                    MaxEllipseScale = 1f,
                    Fuzz = new Vector2(0.94f, 1.06f),
                    ThicknessAtCentre = 0.4f,
                    ThicknessAtRim = 0.4f,
                    YRange = 0.018f,
                    WorldSpaceScale = 0.02f,
                    Tint = new Color(0.07f, 0.06f, 0.06f, 0.4f),
                    TintMultiplier = 1f,
                    IsShadow = true,
                    DrawIndex = 1,
                    // Centred and broad: the weight of the dust sits mid-disc and fades both ways, rather
                    // than peaking in a ring the way M31's does.
                    DustRingPeak = 0.5f,
                    DustRingWidth = 0.55f,
                },
                new GalaxyLayerSpec
                {
                    Role = GalaxyLayerRole.Stars,
                    Id = "stars",
                    ShaderPath = GalaxyBuilder.StarShaderPath,
                    Seed = 16180,
                    Ellipses = 300,
                    StarsPerEllipse = 15,
                    MinEllipseScale = DiscInner,
                    MaxEllipseScale = DiscOuter,
                    Fuzz = new Vector2(0.965f, 1.035f),
                    ThicknessAtCentre = 1f,
                    ThicknessAtRim = 0.1f,
                    YRange = 0.05f,
                    WorldSpaceScale = 0.008f,
                    Tint = Color.white,
                    TintMultiplier = hiiWeight,
                    IsShadow = false,
                    DrawIndex = 2,
                },
            };
        }

        // ------------------------------------------------------------------ module assets

        /// <summary>
        /// The panel copy for each galaxy. Our own words, per the standing rule, and written to be true
        /// rather than impressive: where something is an approximation of the real object, the copy does not
        /// claim otherwise.
        /// <para>
        /// Distances are stated because the diorama model throws scale away - every galaxy arrives about the
        /// same size on the player's table, so the only place the difference between 2.7 million and 21
        /// million light years can live is the text.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, string[]> Copy = new Dictionary<string, string[]>
        {
            ["whirlpool"] = new[]
            {
                "Two arms, wound tight and picked out in blue-white star clusters, with pink knots of glowing hydrogen strung along them like beads. This is the galaxy most people picture when they hear the word, and we see it almost perfectly face-on - which is the only reason it looks this way from here.",
                "It is about 23 million light years away, in Canes Venatici. A smaller galaxy, NGC 5195, is passing through its outskirts and pulling on the northern arm; that companion is not shown here yet.",
            },
            ["pinwheel"] = new[]
            {
                "A face-on disc around 170,000 light years across - comfortably larger than our own galaxy - with many arms of uneven strength rather than a tidy pair, scattered with bright knots where new stars are forming.",
                "It sits about 21 million light years away in Ursa Major. The real galaxy is noticeably lopsided, brighter and more extended on one side; what you are holding is more even than that.",
            },
            ["triangulum"] = new[]
            {
                "Our third neighbour. After the Milky Way and Andromeda, this is the largest galaxy in the Local Group, and at about 2.7 million light years it is close enough that individual clouds of glowing gas can be picked out inside it.",
                "Its arms are flocculent - short, patchy fragments rather than long sweeping ones - and it has almost no central bulge. The version here is smoother and more organised than the real thing, which the generator cannot yet break up.",
            },
        };

        /// <summary>
        /// Creates the <see cref="ExperienceModule"/> each profile needs, if it is not already there.
        /// <para>
        /// <see cref="GalaxyBuilder"/> refuses to build a galaxy with no module and says so, because the
        /// module is what the content prefab gets attached to and what a tag opens. This makes the three
        /// modules rather than asking anyone to click them into existence, so the whole set is reproducible
        /// from scripts - which is the same reason the profiles are a code table and not assets.
        /// </para>
        /// <para>
        /// They are <see cref="ExperienceKind.Destination"/>: places reached from inside a
        /// parent view by pinching a tag, exactly as the seven nebulae are, not new dock tiles. The dock is
        /// seven tiles and GDD 3.1 says so.
        /// </para>
        /// </summary>
        [MenuItem("Cosmic Simulation/Build Galaxy Modules")]
        public static void BuildModules()
        {
            var made = 0;
            var kept = 0;
            var filled = 0;

            foreach (var profile in All())
            {
                var path = profile.ModulePath;
                var existing = AssetDatabase.LoadAssetAtPath<ExperienceModule>(path);
                if (existing != null)
                {
                    // Kept, not rewritten: the panel copy on an existing module may have been edited since,
                    // and clobbering that is the one thing a "build" of already-built content must not do.
                    // The exception is a second line that is still blank - these modules were written before
                    // the field existed, and a blank there is an absence rather than a decision.
                    if (string.IsNullOrEmpty(existing.SecondLine) && !string.IsNullOrEmpty(profile.SecondLine))
                    {
                        var patch = new SerializedObject(existing);
                        patch.FindProperty("SecondLine").stringValue = profile.SecondLine;
                        patch.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(existing);
                        filled++;
                    }

                    kept++;
                    continue;
                }

                if (!Copy.TryGetValue(profile.Id, out var paragraphs))
                {
                    Debug.LogError($"GalaxyLibrary: no panel copy for '{profile.Id}'. A place with no words " +
                                   "is not shippable, so the module is not created.");
                    continue;
                }

                var module = ScriptableObject.CreateInstance<ExperienceModule>();
                var so = new SerializedObject(module);
                so.FindProperty("Id").stringValue = profile.Id;
                so.FindProperty("Kind").enumValueIndex = (int)ExperienceKind.Destination;
                so.FindProperty("DisplayName").stringValue = profile.DisplayName;
                so.FindProperty("SecondLine").stringValue = profile.SecondLine ?? string.Empty;

                // Dimmed, like the Milky Way and Andromeda: these are objects held in a lit room, not places
                // the player stands inside. Full black is for Sagittarius A* and the Cosmic Web.
                so.FindProperty("Environment").enumValueIndex = (int)EnvironmentMode.Dimmed;

                var panel = so.FindProperty("Panel");
                panel.FindPropertyRelative("Title").stringValue = profile.DisplayName;
                var array = panel.FindPropertyRelative("Paragraphs");
                array.arraySize = paragraphs.Length;
                for (var i = 0; i < paragraphs.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).stringValue = paragraphs[i];
                }

                panel.FindPropertyRelative("Instruction").stringValue =
                    "Grab it with two hands to turn it or change its size.";

                so.ApplyModifiedPropertiesWithoutUndo();

                var folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogError($"GalaxyLibrary: {folder} does not exist, so '{profile.Id}' has nowhere " +
                                   "to go. Create the folder and run this again.");
                    continue;
                }

                AssetDatabase.CreateAsset(module, path);
                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"GalaxyLibrary: {made} module(s) created, {kept} already present " +
                      $"({filled} of those given the second line their map tag needs). " +
                      "Now run Cosmic Simulation > Build All Galaxy Content.");
        }
    }
}
