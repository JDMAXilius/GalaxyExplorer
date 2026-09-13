// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the destination tags of GDD 4.3 into <c>galaxy_pois_prefab</c>: a <c>label_button_prefab</c> per
    /// destination, its module assigned, its name taken from that module, and a <see cref="DestinationTags"/> on
    /// the node above them that routes every pick.
    ///
    /// <b>Twelve tags, in two groups.</b> Nine are places inside the Milky Way (GDD 4.3). Three —
    /// <c>whirlpool</c>, <c>pinwheel</c>, <c>triangulum</c> — are other galaxies, and they sit outside the disc:
    /// see <see cref="External"/> for where they go and why they go here at all.
    ///
    /// <b>Into the existing POI prefab rather than a new one.</b> Three reasons, in order of weight. The prefab
    /// is already referenced from <c>galaxy_view_scene</c>, so nothing has to be added to a scene — and a scene
    /// edit is the one thing in this job that cannot be verified from the repo. The old POI markers' own
    /// positions live in exactly this space, so the tags inherit the placements the original app spent time on
    /// instead of new ones invented in a text editor. And the tags then fade, load and unload with the map they
    /// belong to, which a second prefab dropped into the scene would have to be taught to do.
    ///
    /// <b>Units.</b> Everything in <see cref="Placements"/> is in the local space of the prefab's <c>POIs</c>
    /// root. That space has a deliberately odd local scale — (0.4, 2.5, 0.4) — which the scene cancels exactly:
    /// <c>GrabArea</c> above it is (2.5, 0.4, 2.5), with no rotation or offset between the two, so the product
    /// is the identity and a child of <c>POIs</c> is in undistorted metres of the galaxy's own frame. That is
    /// why the tags go under <c>POIs</c> unscaled and why they are *not* uniform when the prefab is opened on
    /// its own in prefab mode. Do not "fix" the squashed preview by rescaling this node; it would shear the
    /// tags in the only place they are ever seen.
    ///
    /// <b>The placements are not astrometric.</b> Six of them are the original app's, which put the Sun about
    /// 0.66 m from the centre of a 1.6 m disc and everything else on the same plane — an artistic scatter, not
    /// galactic coordinates. Helix and Orion are placed to match: same plane, radii inside the same band, in
    /// the two largest gaps in bearing so no two labels overlap. The galactic centre is the one honest one,
    /// because the centre of the galaxy really is the origin of this space.
    ///
    /// Re-running replaces the whole <c>destination_tags</c> node, so it is idempotent: running it twice leaves
    /// exactly twelve tags, and running it after <c>Build UI Prefabs</c> picks up whatever the label prefab has
    /// become.
    /// </summary>
    public static class DestinationTagBuilder
    {
        private const string PoiPrefabPath = "Assets/prefabs/poi_prefabs/galaxy_pois_prefab.prefab";
        private const string LabelPrefabPath = "Assets/prefabs/ui/label_button_prefab.prefab";
        private const string TagRootName = "destination_tags";

        private static readonly string[] ModuleFolders =
        {
            "Assets/data/destinations",
            "Assets/data/experiences",
        };

        /// <summary>
        /// How many map units one canvas millimetre of the card is worth.
        ///
        /// <b>This is the piece the first build of these tags did not have, and its absence is why every tag
        /// came out too small to read.</b> The label prefab's root is scaled 0.001 because "one canvas unit is
        /// one millimetre" — and that convention is true of UI the player reads at arm's length, which is the
        /// UI it was written for. A destination tag is not that. It is a child of the map, in a frame where
        /// the whole galaxy is 1.6 units across, so the GDD's 60 x 24 mm card is 3.75 per cent of the disc and
        /// its 5 mm name subtends about a fifth of a degree at the 1.2 m the map floats at. That is three or
        /// four pixels of cap height. Nothing shrank it: the fitted plates came out 62 to 92 mm and the
        /// auto-size floors never fired. It was authored at that size.
        ///
        /// <b>The number.</b> Text in a headset wants roughly a degree of cap height to be comfortable, and a
        /// degree at 1.2 m is about 21 mm — so the 5 mm name needs a little over four times. Four also keeps
        /// the tier ladder below affordable: a card becomes 96 mm tall, the step 104 mm, and the four tiers
        /// span 0.31 m against the 0.26 m the map carries today. For reference the two inherited markers the
        /// player liked set their name with a 3D TMP at font size 0.85, an em of about 85 mm in this same
        /// frame — seventeen times ours. They could afford that because there were two of them and each hung
        /// off to one side on a long angled leader; twelve cards stacked straight up cannot. So this is a
        /// compromise, and it is the one number to turn when the map is on screen.
        ///
        /// Applied to the tag's root transform, so the card's own layout stays in the millimetres GDD 8.4
        /// specifies and every canvas-unit measurement in this file — plate width, leader length, collider box
        /// — follows for free. The one thing that does not is <see cref="TierStepMetres"/>, which is in map
        /// metres and therefore reads this directly.
        /// </summary>
        private const float CardScaleOnMap = 4f;

        /// <summary>Half the card's height, in canvas units (millimetres). The leader starts at its bottom edge.</summary>
        private const float PillHalfHeightMm = 12f;

        /// <summary>
        /// The card's height, in canvas units.
        ///
        /// <b>24, not the 16 of GDD 8.4.</b> The spec's 60 x 16 mm plate was drawn for a tag carrying a name and
        /// nothing else. Every tag now carries a second line under the name - the treatment the two surviving
        /// original markers used, and the one the player asked to see everywhere - and two lines of type at
        /// 5 mm and 3.6 mm with any margin at all do not fit in 16. The width is fitted to whichever of the two
        /// lines is longer, and the 60 mm floor below is still the spec's.
        /// </summary>
        private const float PillHeightMm = PillHalfHeightMm * 2f;

        /// <summary>
        /// Width of the hairline, in canvas units (millimetres).
        ///
        /// <b>1.2 and not the 0.6 this shipped with.</b> A Quest 3 resolves roughly 20 pixels per degree at the
        /// centre of the lens. The tags sit on a disc that floats about 1.2 m away, so a 0.6 mm line subtends
        /// 0.029 degrees — a little over half a pixel. A sub-pixel line does not read as thin, it reads as
        /// dashed, and it crawls as the head moves. 1.2 mm is 1.1 pixels at that distance and still a fourteenth
        /// of the pill's height, which is what GDD 8.4 means by "thin".
        /// </summary>
        private const float LeaderWidthMm = 1.2f;

        /// <summary>
        /// How deep the tag's collider is, in canvas units (millimetres). The label prefab draws a flat plate
        /// and ships a 2 mm box, which is enough for a ray but mean for a near pinch; this is the same
        /// widening CS-108 gave the dock's drag bar, and for the same reason.
        /// </summary>
        private const float ColliderDepthMm = 8f;

        // ---------- how wide a pill has to be to hold its name
        //
        // The label prefab ships a 60 mm pill with a 54 mm text box, which fits "Crab Nebula" and does not fit
        // "Galactic Center - Sagittarius A*" — thirty-two characters at 5 mm needs about 83 mm, so that tag has
        // been wrapping onto a second line and overflowing an 8 mm box since it was built. Rather than shorten
        // names the GDD spells out, the plate is fitted to the name it carries: measured off TMP, padded, and
        // never narrower than the 60 mm the spec asks for.

        private const float MinPillWidthMm = 60f;

        /// <summary>
        /// Past this the plate stops growing and the type shrinks instead. Nothing in the set reaches it - the
        /// longest built is Orion at about 96 mm, set by its subtitle rather than its name, and the two that
        /// used to be longest ("Solar System", "Galactic Center") now sit on the 60 mm floor. It is here so
        /// that a long name added later degrades to small-but-readable rather than to a slab wider than the
        /// galaxy.
        /// </summary>
        private const float MaxPillWidthMm = 130f;

        private const float PillPadMm = 5f;

        /// <summary>Height of the name's text box inside the card, in canvas units — the label prefab's own.</summary>
        private const float TextBoxHeightMm = 8f;

        /// <summary>Height of the second line's text box, in canvas units — the label prefab's own.</summary>
        private const float SecondBoxHeightMm = 5f;

        /// <summary>
        /// Where the name sits when there is a second line under it, in canvas units above the card's centre.
        /// <see cref="SecondLineYMm"/> is where the second line sits. Both are <c>internal</c> and both are read
        /// by <see cref="UiPrefabBuilder"/> when it authors the prefab, because this builder rewrites the name's
        /// Y on every tag and the prefab sets both: a literal in each file would let them drift, and the failure
        /// would be twelve slightly crooked cards that nothing checks.
        /// </summary>
        internal const float NameYMm = 4.5f;

        /// <summary>Where the second line sits, in canvas units below the card's centre. See <see cref="NameYMm"/>.</summary>
        internal const float SecondLineYMm = -5f;

        /// <summary>
        /// Floor for auto-sized <i>subtitle</i> type, in canvas units.
        ///
        /// <b>Below <see cref="MinFontSizeMm"/> on purpose, and that is not a contradiction.</b> The subtitle is
        /// authored at exactly 3.6 mm, so a floor of 3.6 would give autosizing a range of zero - the shrink
        /// would do nothing and an over-long second line would simply run off the plate, which is the one
        /// outcome the width fitting exists to prevent. A subtitle that has to come down to 3.0 mm is too small
        /// to read comfortably at 1.2 m and <see cref="Build"/> says so by name when it happens; small and
        /// flagged beats clipped and silent.
        /// </summary>
        private const float MinSecondFontSizeMm = 3.0f;

        /// <summary>The selected outline is this much larger than the pill on every side, as authored.</summary>
        private const float OutlineMarginMm = 4f;

        /// <summary>Floor for auto-sized type, in canvas units. Below this a 1.2 m tag stops being readable.</summary>
        private const float MinFontSizeMm = 3.6f;

        /// <summary>One tag: which module, where its point on the disc is, and how far above it the label floats.</summary>
        private readonly struct Placement
        {
            public readonly string Id;
            public readonly Vector3 Anchor;
            public readonly float Lift;

            public Placement(string id, float x, float y, float z, float lift)
            {
                Id = id;
                Anchor = new Vector3(x, y, z);
                Lift = lift;
            }
        }

        // Anchors in POIs-local units, which the scene makes metres (see the class note).
        //
        // Seven of the nine are lifted verbatim off markers that already exist: crab, solar_system, homunculus,
        // pillars, ngc1501 and trumpler14 off the PrefabInstance root transforms in this prefab, and
        // sagittarius_a off the loose `galactic_center` marker that sits directly in galaxy_view_scene — which
        // is at the origin, and the origin really is the centre of the galaxy, so that one is not arbitrary.
        // Helix and Orion are the two the original app never had. Helix fills the gap in bearing between the
        // Sun (80 deg) and Homunculus (165 deg) at a comparable radius; Orion sits between NGC 1501 (-56 deg)
        // and Crab (-120 deg) on a shorter radius, which is at least the right way round — Orion is the
        // nearest of the nine.
        //
        // Lift is straight up, in the same units, and these are starting heights rather than final ones.
        // 0.12 m clears the disc; the two on short radii start at 0.17 so their labels do not sit on top of the
        // arms, and the galactic centre at 0.22 to clear the bright core it names. SpaceOut then raises
        // whichever of them would otherwise overlap a neighbour - see that method for why it has to run after
        // the cards are built rather than being solved here.
        private static readonly Placement[] Placements =
        {
            new Placement("helix",        -0.371f, 0f,     0.594f, 0.12f),
            new Placement("crab",         -0.409f, 0f,    -0.716f, 0.12f),
            new Placement("solar_system",  0.115f, 0f,     0.648f, 0.12f),
            new Placement("sagittarius_a", 0f,     0f,     0f,     0.22f),
            new Placement("homunculus",   -0.772f, 0f,     0.205f, 0.12f),
            new Placement("orion",        -0.047f, 0f,    -0.538f, 0.17f),
            new Placement("pillars",      -0.669f, 0f,    -0.337f, 0.12f),
            new Placement("ngc1501",       0.364f, 0.06f, -0.547f, 0.12f),
            new Placement("trumpler14",    0.328f, 0f,    -0.176f, 0.17f),
        };

        /// <summary>Where the Sun sits on this map. The same anchor <c>solar_system</c> uses, by construction.</summary>
        private static readonly Vector3 SunAnchor = new Vector3(0.115f, 0f, 0.648f);

        /// <summary>
        /// An outside galaxy, given as a real bearing from the Sun rather than as a made-up point.
        /// </summary>
        private readonly struct Bearing
        {
            /// <summary>Module id.</summary>
            public readonly string Id;

            /// <summary>Galactic longitude l, degrees. 0 is the direction of the galactic centre.</summary>
            public readonly float Longitude;

            /// <summary>Galactic latitude b, degrees. Positive is out of the disc on the north side.</summary>
            public readonly float Latitude;

            /// <summary>How far along that bearing to draw it, in map metres. Layout only — see the note.</summary>
            public readonly float Range;

            public Bearing(string id, float longitude, float latitude, float range)
            {
                Id = id;
                Longitude = longitude;
                Latitude = latitude;
                Range = range;
            }
        }

        /// <summary>
        /// The three galaxies built by <c>GalaxyLibrary</c>, hung outside the disc of the Milky Way map.
        ///
        /// <b>Why here.</b> They are <see cref="ExperienceKind.Destination"/> modules with content prefabs and
        /// nothing at all opened them: no dock tile, no tag, no route of any kind, so the work done on them was
        /// invisible in the running app. The Galaxies view is where they will eventually belong, and when it has
        /// its own <see cref="DestinationTags"/> these three should move there. Until then this is the one place
        /// that can reach them without editing a scene or a file this builder does not own, and it is not a
        /// stopgap that reads badly: standing on the Milky Way map and being shown the way out to three other
        /// galaxies is a better piece of storytelling than finding them in a drawer.
        ///
        /// <b>The bearings are real; the distances are not.</b> Longitude and latitude are the published
        /// galactic coordinates of each galaxy, so the direction each tag lies in — relative to the line from
        /// the Sun to the galactic centre, which this map does get right — is the true one. M51 and M101 are
        /// near the north galactic pole and come out high above the disc, which is exactly where they are;
        /// M33 is well below it and beyond the rim, which is also true. The <b>range</b> along each bearing is
        /// pure layout: the real distances are 2.7, 21 and 23 million light years and the map is 1.6 m across,
        /// so no honest scale exists. The ranges here are chosen only to keep the three labels apart and inside
        /// arm's reach, and the panel copy is where the actual distances are stated.
        ///
        /// Two further caveats, stated rather than hidden. The map's spiral is an artist's disc, so which face
        /// of it is the galactic north pole was never decided; the constellation of three may be mirrored.
        /// And the tag is lifted straight up in the room rather than perpendicular to the disc — see
        /// <see cref="DestinationTags"/> — so the height reads as "above the map", not as a measured distance
        /// out of the galactic plane.
        /// </summary>
        private static readonly Bearing[] External =
        {
            // l, b from the standard catalogues; all three are textbook figures.
            //   M51  l = 104.85, b = +68.56   -> foot ( 0.002, 0, 0.700), 0.32 m up
            //   M101 l = 102.04, b = +59.77   -> foot (-0.109, 0, 0.739), 0.41 m up
            //   M33  l = 133.61, b = -31.33   -> foot ( 0.018, 0, 0.780), 0.10 m down
            // All three feet land on the disc inside its 0.8 m rim, so the leaders read as drop lines to it.
            // M33's range is the one the clamp in FromBearing bites on: taken at face value it put the foot at
            // 1.04, a quarter of a metre off the edge of the galaxy, and the tag with it.
            // Empty on purpose, and the bearings above are kept as a comment rather than deleted because the
            // arithmetic that produced them is the expensive part. The three outside galaxies are no longer
            // pins on our own map: a galaxy is a place you go to, the way Andromeda is, not a label on the
            // Milky Way. They are selected from the Galaxies experience instead - see docs/GDD.md - and this
            // table now holds only what is genuinely inside this galaxy.
        };

        [MenuItem("Cosmic Simulation/Build Destination Tags")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("DestinationTagBuilder: leave play mode first.");
                return;
            }

            var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPrefabPath);
            if (labelPrefab == null)
            {
                Debug.LogError($"DestinationTagBuilder: {LabelPrefabPath} is missing. " +
                               "Run Cosmic Simulation > Build UI Prefabs first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PoiPrefabPath) == null)
            {
                Debug.LogError($"DestinationTagBuilder: {PoiPrefabPath} is missing.");
                return;
            }

            Undersized.Clear();

            var modules = LoadModules();
            var wanted = AllPlacements();

            var contents = PrefabUtility.LoadPrefabContents(PoiPrefabPath);
            try
            {
                var built = new List<string>();
                var missing = new List<string>();

                var tagRoot = ResetTagRoot(contents.transform);
                var set = tagRoot.AddComponent<DestinationTags>();
                var buttons = new List<LabelButton>();
                var placed = new List<Placement>();

                foreach (var placement in wanted)
                {
                    if (!modules.TryGetValue(placement.Id, out var module))
                    {
                        // Skipped rather than built blank: a tag with no module logs on every pinch, and a
                        // silent one is worse than an absent one.
                        missing.Add(placement.Id);
                        continue;
                    }

                    var button = BuildTag(labelPrefab, tagRoot.transform, placement, module);
                    if (button == null)
                    {
                        missing.Add(placement.Id);
                        continue;
                    }

                    buttons.Add(button);
                    placed.Add(placement);
                    built.Add($"{placement.Id} -> \"{module.DisplayName}\" at " +
                              $"({placement.Anchor.x:0.###}, {placement.Anchor.y:0.###}, {placement.Anchor.z:0.###})" +
                              $" {(placement.Lift < 0f ? "- " : "+ ")}{Mathf.Abs(placement.Lift):0.##} up, " +
                              (string.IsNullOrEmpty(module.SceneName)
                                  ? (module.ContentPrefab != null ? "overlay" : "NOT BUILT YET")
                                  : $"scene {module.SceneName}"));
                }

                // After the cards exist, because it needs their fitted widths, and before Bind, because Bind
                // writes the lifts into the component that reproduces them at run time.
                var raised = SpaceOut(buttons, placed);

                Bind(set, buttons, placed);

                PrefabUtility.SaveAsPrefabAsset(contents, PoiPrefabPath);

                Debug.Log($"DestinationTagBuilder: {built.Count} of {wanted.Count} tags written into " +
                          $"{PoiPrefabPath} under '{TagRootName}'.\n" +
                          $"  built: {string.Join("\n         ", built)}\n" +
                          $"  no module asset: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}\n" +
                          $"  moved onto another tier: {(raised.Count == 0 ? "none" : string.Join(", ", raised))}\n" +
                          $"  subtitle shrunk below {MinFontSizeMm} mm to fit: " +
                          $"{(Undersized.Count == 0 ? "none" : string.Join(", ", Undersized))}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------- pieces

        /// <summary>The nine inside the galaxy, then the three outside it, in that order.</summary>
        private static List<Placement> AllPlacements()
        {
            var all = new List<Placement>(Placements.Length + External.Length);
            all.AddRange(Placements);

            foreach (var bearing in External)
            {
                all.Add(FromBearing(bearing));
            }

            return all;
        }

        /// <summary>
        /// Turns a galactic bearing into a point on this map.
        ///
        /// The map gives us two of the three axes for free: the galactic plane is the local XZ plane, and the
        /// line from the Sun to the origin is l = 0, because the origin of this space really is the centre of
        /// the galaxy. The third, which way round l runs, is the assumption called out on <see cref="External"/>.
        ///
        /// The point returned is the <b>foot</b> — where the bearing crosses the plane of the disc — and the
        /// height goes into the lift, so the tag ends up with a long leader dropping to the disc rather than a
        /// short one dangling in empty space. A galaxy below the plane gets a negative lift, and the leader is
        /// drawn upward instead; <see cref="SetLeader"/> handles the sign.
        ///
        /// <para><b>The foot is kept on the disc.</b> Triangulum's range put its foot 1.04 out from the centre
        /// of a disc whose rim is at 0.8, so its card hung 0.29 below the plane, well clear of the galaxy,
        /// with a long leader running up to a point in empty space — the one tag on the map that was visibly
        /// not attached to anything. Nothing is lost by pulling it in: the range along each bearing is
        /// declared on <see cref="External"/> as pure layout, while the <i>direction</i> from the Sun is the
        /// real one, and shortening the distance travelled along that direction keeps the direction exactly.
        /// The height is then taken from the shortened distance so the latitude stays true as well. M51 and
        /// M101 already land inside the rim and are not touched.</para>
        /// </summary>
        private static Placement FromBearing(Bearing bearing)
        {
            var towardCentre = -SunAnchor;
            towardCentre.y = 0f;
            towardCentre.Normalize();

            // l = 90 degrees. Cross(up, towardCentre) is a quarter turn about the map's own vertical, which is
            // the galactic pole axis here, so the pair is an orthonormal basis for the plane.
            var quarterTurn = Vector3.Cross(Vector3.up, towardCentre);

            var l = bearing.Longitude * Mathf.Deg2Rad;
            var b = bearing.Latitude * Mathf.Deg2Rad;

            var alongPlane = Mathf.Cos(l) * towardCentre + Mathf.Sin(l) * quarterTurn;

            var travelled = KeepFootOnDisc(alongPlane, Mathf.Cos(b) * bearing.Range);
            var foot = SunAnchor + alongPlane * travelled;

            // Tan rather than Sin(b) * Range, so that a clamped bearing keeps its latitude instead of its
            // height. For an unclamped one the two are the same number: travelled is Cos(b) * Range.
            var height = travelled * Mathf.Tan(b);

            return new Placement(bearing.Id, foot.x, 0f, foot.z, height);
        }

        /// <summary>
        /// The furthest a foot may sit from the centre, in map metres. The disc's rim is at 0.8 — the radius
        /// the nine hand-placed anchors reach — and this is a hair inside it, so a leader lands on the disc
        /// rather than on the edge of it.
        /// </summary>
        private const float FootMaxRadiusMetres = 0.78f;

        /// <summary>
        /// How far along a bearing from the Sun a foot may travel before it leaves the disc.
        ///
        /// The ray is <c>SunAnchor + direction * d</c> with <c>direction</c> a unit vector in the plane, so
        /// the exit is the positive root of <c>d^2 + 2(S.a)d + (|S|^2 - R^2) = 0</c>. The Sun sits well inside
        /// the rim, so the discriminant is positive for every bearing and the guard below is belt and braces.
        /// </summary>
        private static float KeepFootOnDisc(Vector3 direction, float wanted)
        {
            var along = Vector3.Dot(SunAnchor, direction);
            var discriminant = along * along - SunAnchor.sqrMagnitude
                               + FootMaxRadiusMetres * FootMaxRadiusMetres;
            if (discriminant <= 0f)
            {
                return wanted;
            }

            return Mathf.Min(wanted, -along + Mathf.Sqrt(discriminant));
        }

        /// <summary>
        /// Every <see cref="ExperienceModule"/> in the two data folders, by id. Destinations and dock tiles
        /// together, because the map carries both kinds of tag.
        /// </summary>
        private static Dictionary<string, ExperienceModule> LoadModules()
        {
            var byId = new Dictionary<string, ExperienceModule>();

            foreach (var guid in AssetDatabase.FindAssets("t:ExperienceModule", ModuleFolders))
            {
                var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(guid));
                if (module == null || string.IsNullOrEmpty(module.Id))
                {
                    continue;
                }

                byId[module.Id] = module;
            }

            return byId;
        }

        /// <summary>Drops whatever this builder wrote last time and makes a fresh, empty node. Idempotence.</summary>
        private static GameObject ResetTagRoot(Transform poisRoot)
        {
            // A loop, not one Find: a hand edit could have left two nodes of the same name, and leaving one
            // behind would double the tags in exactly the way this method exists to prevent.
            for (var existing = poisRoot.Find(TagRootName); existing != null; existing = poisRoot.Find(TagRootName))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var tagRoot = new GameObject(TagRootName);
            tagRoot.transform.SetParent(poisRoot, false);

            // Unscaled on purpose. The POIs node's (0.4, 2.5, 0.4) is cancelled by GrabArea above it in the
            // scene, so this frame is undistorted metres there; undoing it here would break that, not fix it.
            tagRoot.transform.localPosition = Vector3.zero;
            tagRoot.transform.localRotation = Quaternion.identity;
            tagRoot.transform.localScale = Vector3.one;
            return tagRoot;
        }

        private static LabelButton BuildTag(GameObject labelPrefab, Transform parent, Placement placement,
                                            ExperienceModule module)
        {
            var instance = PrefabUtility.InstantiatePrefab(labelPrefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = "tag_" + placement.Id;

            var t = instance.transform;

            // The authored pose. At runtime DestinationTags re-hangs every tag straight up in the room from its
            // anchor, which is what lets the leader land on its point however the map is tilted; this is what
            // the prefab looks like in the editor and what is used if that data ever goes missing.
            t.localPosition = placement.Anchor + Vector3.up * placement.Lift;
            t.localRotation = Quaternion.identity;

            // Multiplied off the prefab's own scale rather than written as a literal, so the millimetre
            // convention stays owned by UiPrefabBuilder and this file only applies the map conversion.
            t.localScale = labelPrefab.transform.localScale * CardScaleOnMap;
            Mark(t);

            var button = instance.GetComponent<LabelButton>();
            if (button == null)
            {
                Debug.LogError($"DestinationTagBuilder: {LabelPrefabPath} has no LabelButton on its root.");
                Object.DestroyImmediate(instance);
                return null;
            }

            var so = new SerializedObject(button);
            so.FindProperty("destination").objectReferenceValue = module;

            // The children are read back off the component's own references rather than found by path, so a
            // rename inside UiPrefabBuilder cannot silently leave this builder writing into nothing.
            var text = so.FindProperty("label").objectReferenceValue as TMP_Text;
            var second = so.FindProperty("secondLine").objectReferenceValue as TMP_Text;
            var pill = so.FindProperty("pill").objectReferenceValue as Graphic;
            var leader = so.FindProperty("leader").objectReferenceValue as Graphic;
            var grow = so.FindProperty("growTarget").objectReferenceValue as Transform;
            var outline = so.FindProperty("selectedOutline").objectReferenceValue as GameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            FitPill(instance.transform as RectTransform, text, second, pill, grow, outline,
                    module.DisplayName, module.SecondLine);
            SetLeader(leader, placement.Lift);

            // Turned to the player about the world Y only, so the plate stays upright and level however the
            // galaxy is tilted. Billboard writes rotation away from the camera, which is the direction a
            // world-space canvas has to face to be read (the same convention InfoPanel and MoonLabel use).
            var billboard = instance.AddComponent<Billboard>();
            var billboardSo = new SerializedObject(billboard);
            billboardSo.FindProperty("pivotAxis").enumValueIndex = (int)PivotAxis.Y;
            billboardSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            return button;
        }

        /// <summary>
        /// Puts the name on the plate and makes the plate wide enough to hold it.
        ///
        /// The width is measured off TMP rather than counted in characters, because the difference between
        /// "NGC 1501" and "Galactic Center - Sagittarius A*" at 5 mm is the difference between a plate that fits
        /// and one that silently wraps onto a line the 8 mm text box cannot show. Wrapping is turned off as
        /// well, so that if the measurement is ever off by a millimetre the name runs a hair over the plate
        /// instead of vanishing.
        /// </summary>
        private static void FitPill(RectTransform root, TMP_Text label, TMP_Text second, Graphic pill,
                                    Transform grow, GameObject outline, string displayName, string secondText)
        {
            if (label == null)
            {
                return;
            }

            // A module with no second line gets the name centred on its own rather than pushed up to leave room
            // for an empty box. Only the three outside galaxies were in that state when this was written, and
            // they are given one below, but the case has to work: this prefab is the general destination tag.
            var hasSecond = second != null && !string.IsNullOrWhiteSpace(secondText);

            label.text = displayName;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = false;
            label.ForceMeshUpdate();

            var titleWidth = Measure(label, displayName);

            var secondWidth = 0f;
            if (second != null)
            {
                second.gameObject.SetActive(hasSecond);
                if (hasSecond)
                {
                    // Upper-cased here rather than in the copy deck, so the deck stays readable prose and the
                    // caps are a property of this treatment. TMP's FontStyles.UpperCase would do it at draw
                    // time, but then preferredWidth measures the lower-case string and the plate comes out
                    // narrow enough to clip - which is the bug this whole method exists to prevent.
                    second.text = secondText.ToUpperInvariant();
                    second.textWrappingMode = TextWrappingModes.NoWrap;
                    second.enableAutoSizing = false;
                    second.ForceMeshUpdate();
                    secondWidth = Measure(second, second.text);
                }

                Mark(second);
                Mark(second.rectTransform);
                Mark(second.gameObject);
            }

            // The plate has to hold whichever line is longer. "NGC 1501" under a subtitle of "PLANETARY NEBULA"
            // is the case that makes this matter: the name is half the width of the line beneath it.
            var wanted = Mathf.Max(titleWidth, secondWidth) + 2f * PillPadMm;
            var width = Mathf.Clamp(wanted, MinPillWidthMm, MaxPillWidthMm);

            if (wanted > MaxPillWidthMm)
            {
                // Shrink whichever line was the one that overflowed, and only that one: shrinking the name
                // because the subtitle is long would make the name smaller than it needs to be.
                if (titleWidth >= secondWidth)
                {
                    Shrink(label, MinFontSizeMm);
                }
                else if (second != null)
                {
                    Shrink(second, MinSecondFontSizeMm);
                    Undersized.Add(displayName);
                }
            }

            Mark(label);

            SetSize(root, width, PillHeightMm);
            SetSize(grow as RectTransform, width, PillHeightMm);
            SetSize(pill != null ? pill.rectTransform : null, width, PillHeightMm);
            SetSize(outline != null ? outline.transform as RectTransform : null,
                    width + OutlineMarginMm, PillHeightMm + OutlineMarginMm);

            var inner = width - 2f * PillPadMm;
            SetSize(label.rectTransform, inner, TextBoxHeightMm);
            if (second != null)
            {
                SetSize(second.rectTransform, inner, SecondBoxHeightMm);
            }

            // One line sits centred; two sit either side of centre at the offsets the prefab authored. Written
            // here as well as there because a one-line tag has to look deliberate, not like a two-line tag
            // with the bottom half missing.
            SetY(label.rectTransform, hasSecond ? NameYMm : 0f);
            if (hasSecond)
            {
                SetY(second.rectTransform, SecondLineYMm);
            }

            WidenCollider(grow, width);
        }

        /// <summary>
        /// How wide a line of type is, in canvas units, with a fallback for the case TMP will not answer.
        /// </summary>
        private static float Measure(TMP_Text text, string content)
        {
            var measured = text.preferredWidth;
            if (!float.IsNaN(measured) && !float.IsInfinity(measured) && measured > 1f)
            {
                return measured;
            }

            // TMP can decline to measure before its font atlas is warm. A character count at roughly half an em
            // is a poor substitute but a safe one: it errs wide, and a slightly roomy plate is a great deal
            // better than a clipped name.
            return (content == null ? 0 : content.Length) * text.fontSize * 0.52f;
        }

        /// <summary>
        /// Lets a line shrink to fit rather than run off the plate. The floor is a parameter because the name
        /// and the subtitle have different ones - see <see cref="MinSecondFontSizeMm"/>.
        /// </summary>
        private static void Shrink(TMP_Text text, float floorMm)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = floorMm;
        }

        /// <summary>
        /// Names of tags whose subtitle had to shrink under <see cref="MinFontSizeMm"/> to fit. Collected across
        /// one run and printed by <see cref="Build"/>, so a copy change that makes a line unreadable is visible
        /// the moment it is made rather than on a headset weeks later.
        /// </summary>
        private static readonly List<string> Undersized = new List<string>();

        private static void SetY(RectTransform rect, float y)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            Mark(rect);
        }

        /// <summary>
        /// Draws the hairline between the pill and the point it names.
        ///
        /// Straight down in canvas units, because the tag hangs straight up over its point and only ever turns
        /// about Y: a vertical line in the canvas is a vertical line in the room, whichever way the player is
        /// standing. One canvas unit is a millimetre and the label prefab's root is scaled 0.001, so a lift
        /// given in metres becomes a height in canvas units by multiplying by a thousand.
        ///
        /// <b>Minus the card's half-height, which the first version of this forgot.</b> The rect is centred on
        /// the card, not on its bottom edge, so a line of the full lift length starting <see cref="PillHalfHeightMm"/>
        /// below centre ends that far <i>past</i> the point it is supposed to touch. The span that has to be
        /// covered is from the bottom edge of the card to the point, which is the lift less that half-height.
        /// It reads the constant rather than a number, so the 8 mm -> 12 mm change that came with the second
        /// line needed no edit here.
        ///
        /// A negative lift hangs the tag below its point — one of the outside galaxies is under the galactic
        /// plane — and the same line is then drawn upward from the top edge.
        /// </summary>
        private static void SetLeader(Graphic leader, float liftMetres)
        {
            if (leader == null)
            {
                return;
            }

            var rect = leader.rectTransform;
            var span = Mathf.Abs(liftMetres) * 1000f - PillHalfHeightMm;

            if (span <= 1f)
            {
                // The point is inside the plate. There is no line to draw, and a 1 mm stub sticking out of the
                // pill would read as a rendering fault rather than as a leader.
                leader.enabled = false;
                Mark(leader);
                Mark(rect);
                return;
            }

            leader.enabled = true;
            var direction = liftMetres < 0f ? 1f : -1f;
            rect.sizeDelta = new Vector2(LeaderWidthMm, span);
            rect.anchoredPosition = new Vector2(0f, direction * (PillHalfHeightMm + span * 0.5f));

            Mark(leader);
            Mark(rect);
        }

        private static void WidenCollider(Transform growTarget, float widthMm)
        {
            if (growTarget == null || !growTarget.TryGetComponent<BoxCollider>(out var box))
            {
                return;
            }

            box.size = new Vector3(widthMm, PillHeightMm, ColliderDepthMm);
            Mark(box);
        }

        private static void SetSize(RectTransform rect, float widthMm, float heightMm)
        {
            if (rect == null)
            {
                return;
            }

            // sizeDelta is the size itself only while the anchors are together, which every rect in the label
            // prefab keeps them (the prefab builder never moves them off centre for this one).
            rect.sizeDelta = new Vector2(widthMm, heightMm);
            Mark(rect);
        }

        private static void Mark(Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        /// <summary>
        /// Spreads the cards above the disc over a few heights, so that no two neighbours draw on top of each
        /// other, and rewrites each one's placement and leader to match.
        ///
        /// <para><b>The first version of this tested whether two cards were close together on the disc, and it
        /// was the wrong test.</b> The cards billboard: they turn to face the player about world Y, so two of
        /// them at the same height overlap whenever the player is anywhere near the line joining them - and
        /// that is true however far apart they are, because what matters is where they land in the view, not
        /// where they sit on the map. Six of the nine Milky Way tags started at exactly 0.12 m, which made
        /// every such overlap a total one. Testing planar distance found nothing and reported success.</para>
        ///
        /// <para><b>So: heights assigned in order of radius.</b> The outermost card sits lowest and each step
        /// inwards is <see cref="TierStepMetres"/> higher, which is a card height plus a couple of millimetres
        /// - the least that stops two cards touching whatever angle they are seen from. Radius is the ordering
        /// rather than the array's order because it means something: the leaders fan out from a low outer rim
        /// to a high centre, inner cards clear the bright core and the arms the way the old hand-typed 0.17
        /// and 0.22 were trying to, and the arrangement is stable - adding a destination slots it in by where
        /// it is rather than reshuffling everything.</para>
        ///
        /// <para><b>The ladder wraps, and that is the concession <see cref="CardScaleOnMap"/> forced.</b> One
        /// tier per card costs a card height each, so eleven of them spanned 0.26 m while a card was 24 mm
        /// tall. At the size the cards actually have to be to read, a card is 96 mm and eleven tiers would be
        /// a 1.1 m tower over a disc 1.6 m across — which is a worse picture than the overlap it prevents. So
        /// the tier index wraps at <see cref="TierCycle"/>: the spread stays at 0.31 m, and two cards share
        /// a height only when they are four apart in the radius ordering, which puts most of the disc between
        /// them. It is a smaller guarantee than "cannot ever touch" and it is deliberately smaller — twelve
        /// legible cards on a map this size cannot all be non-overlapping from every angle, and the inherited
        /// markers the player is asking for did not solve it by stacking either. They hung off to one side on
        /// a long angled leader, which is the layout to move to if the wrap is not enough; see the note on
        /// <c>LabelButton.PointLeaderAt</c>, which is written for that and does not currently work for a
        /// Graphic leader.</para>
        ///
        /// <para>A card hung <i>below</i> the plane is left exactly as placed. There is one - Triangulum,
        /// which really is under the galactic plane - and being the only thing down there it has nothing to
        /// collide with.</para>
        /// </summary>
        private static List<string> SpaceOut(List<LabelButton> buttons, List<Placement> placements)
        {
            var raised = new List<string>();

            var above = new List<int>();
            for (var i = 0; i < placements.Count; i++)
            {
                if (placements[i].Lift >= 0f)
                {
                    above.Add(i);
                }
            }

            // Outermost first. The comparison is on the square, which orders identically and avoids eleven
            // square roots for nothing.
            above.Sort((a, b) => RadiusSquared(placements[b].Anchor).CompareTo(RadiusSquared(placements[a].Anchor)));

            for (var order = 0; order < above.Count; order++)
            {
                var index = above[order];
                var placement = placements[index];
                var lift = BaseLiftMetres + (order % TierCycle) * TierStepMetres;

                if (Mathf.Approximately(lift, placement.Lift))
                {
                    continue;
                }

                raised.Add($"{placement.Id} {placement.Lift:0.###} -> {lift:0.###}");

                // The placement is the record Bind writes and DestinationTags reproduces at run time, so it has
                // to change too - not just the transform. A card moved here whose anchor still said 0.12 would
                // snap back to the old height on the first frame.
                placements[index] = new Placement(
                    placement.Id, placement.Anchor.x, placement.Anchor.y, placement.Anchor.z, lift);

                var t = buttons[index].transform;
                t.localPosition = placement.Anchor + Vector3.up * lift;
                Mark(t);

                var so = new SerializedObject(buttons[index]);
                SetLeader(so.FindProperty("leader").objectReferenceValue as Graphic, lift);
                PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[index]);
            }

            return raised;
        }

        /// <summary>How high the outermost card floats above the disc, in metres. Enough to clear it.</summary>
        private const float BaseLiftMetres = 0.12f;

        /// <summary>
        /// The gap between one tier and the next, in map metres: a card height plus a 2 mm gap, converted
        /// through <see cref="CardScaleOnMap"/>. Below a card height two cards can still catch each other's
        /// corners when they line up in the view, so a card height is the least that works — and it has to be
        /// the card's height <i>on the map</i>, which is why this reads the scale. Leaving it in raw canvas
        /// millimetres would have stepped the ladder by a quarter of a card and put four cards back on top of
        /// each other.
        /// </summary>
        private const float TierStepMetres = (PillHeightMm + 2f) * CardScaleOnMap * 0.001f;

        /// <summary>
        /// How many heights the ladder uses before it starts again at the bottom. See the note on
        /// <see cref="SpaceOut"/> for why it wraps at all; four keeps the spread at the 0.29 m the map already
        /// carries while the cards are four times the size.
        /// </summary>
        private const int TierCycle = 4;

        /// <summary>How far an anchor is from the centre of the disc, squared. Height is not part of it.</summary>
        private static float RadiusSquared(Vector3 anchor)
        {
            return anchor.x * anchor.x + anchor.z * anchor.z;
        }

        private static void Bind(DestinationTags set, List<LabelButton> buttons, List<Placement> placements)
        {
            var so = new SerializedObject(set);

            var array = so.FindProperty("tags");
            array.arraySize = buttons.Count;
            for (var i = 0; i < buttons.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }

            // Written in the same order and the same length as the tags above, which is the only thing
            // DestinationTags checks before trusting them.
            var anchors = so.FindProperty("anchors");
            anchors.arraySize = placements.Count;
            for (var i = 0; i < placements.Count; i++)
            {
                var element = anchors.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Point").vector3Value = placements[i].Anchor;
                element.FindPropertyRelative("Lift").floatValue = placements[i].Lift;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
