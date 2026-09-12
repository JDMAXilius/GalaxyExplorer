// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the <see cref="SystemProfile"/> assets, and holds the one table that says which systems exist.
    ///
    /// <para><b>Why a table here and an asset there.</b> A profile has to be an asset, because prefabs,
    /// modules and layouts hold references to assets and cannot hold references to a static C# field. But
    /// nobody should hand-tune one: a system is ten-odd rows of published numbers, and ten rows in a C# table
    /// is a diff a reviewer can actually read, where the same thing in a <c>.asset</c> is four hundred lines of
    /// YAML in which a transposed digit is invisible. So the table is the source of truth and the asset is
    /// generated from it, exactly as <c>LayoutPresetBuilder</c> owns the two <see cref="LayoutPreset"/> assets
    /// and <c>CopyImporter</c> owns the <see cref="BodyInfo"/> assets. Re-running resets a profile somebody
    /// poked in the inspector, which is the point.</para>
    ///
    /// <para><b>The table holds our solar system and nothing else.</b> Real exoplanet systems are being
    /// researched (<c>docs/research/exoplanet_systems.md</c>) and will arrive as diameter, mass, orbital
    /// period, day length and orbital distance per planet with the unmeasured values marked as unmeasured.
    /// Adding a row before that data exists would mean inventing it, which D-010 forbids. The mechanism is
    /// what this change delivers; the second row is a separate, small commit.</para>
    ///
    /// Menu: <b>Cosmic Simulation -> Build System Profiles</b>, and
    /// <b>Cosmic Simulation -> Check Star Colour Model</b> to prove the colour maths against published values.
    /// </summary>
    public static class SystemProfileBuilder
    {
        public const string Folder = "Assets/data/systems";

        private const string BodyFolder = "Assets/data/bodies";

        /// <summary>The id of our own system: the profile every existing menu item and document means.</summary>
        public const string SolarSystemId = "solar_system";

        // ---------- the table

        /// <summary>
        /// One body of one system. <see cref="ArtPrefabPath"/> names the hand-authored prefab carrying real
        /// surface imagery; a row that leaves it null is built from <see cref="SystemBody.Appearance"/> by
        /// <see cref="GenericBodyBuilder"/> and is therefore declared modelled.
        /// </summary>
        private readonly struct Row
        {
            public readonly string Id;
            public readonly BodyRole Role;
            public readonly float DiameterKm;
            public readonly float OrbitSemiMajorAu;
            public readonly string ArtPrefabPath;
            public readonly BodyAppearance Appearance;

            public Row(string id, BodyRole role, float diameterKm, string artPrefabPath)
            {
                Id = id;
                Role = role;
                DiameterKm = diameterKm;
                OrbitSemiMajorAu = 0f;
                ArtPrefabPath = artPrefabPath;
                Appearance = BodyAppearance.Default;
            }

            public Row(string id, BodyRole role, float diameterKm, float orbitSemiMajorAu,
                       BodyAppearance appearance)
            {
                Id = id;
                Role = role;
                DiameterKm = diameterKm;
                OrbitSemiMajorAu = orbitSemiMajorAu;
                ArtPrefabPath = null;
                Appearance = appearance;
            }
        }

        private readonly struct Profile
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string ModulePath;
            public readonly string ContentPrefabName;
            public readonly string AuthoredLayoutId;
            public readonly Row[] Bodies;

            public Profile(string id, string displayName, string modulePath, string contentPrefabName,
                           string authoredLayoutId, Row[] bodies)
            {
                Id = id;
                DisplayName = displayName;
                ModulePath = modulePath;
                ContentPrefabName = contentPrefabName;
                AuthoredLayoutId = authoredLayoutId;
                Bodies = bodies;
            }
        }

        private const string PoiFolder = "Assets/prefabs/poi_prefabs";

        /// <summary>The prefab our own bodies have always been lifted out of.</summary>
        private static string Poi(string id) => $"{PoiFolder}/poi_{id}_prefab.prefab";

        /// <summary>
        /// Profile #1: our solar system, in the order GDD 4.1 lists its bodies, which is the order
        /// <see cref="SolarRowBuilder"/> used to carry as a hard-coded array. Diameters are GDD 6.1's, the
        /// app's own design contract.
        ///
        /// <para><b>Why every <c>Appearance</c> here is left at its default.</b> All ten of these bodies have
        /// their own prefab and their own real surface maps, so nothing reads their appearance parameters.
        /// Filling them in would put a page of transcribed numbers into the repo with no consumer to catch a
        /// transposed digit — and a transcription nobody reads is a transcription nobody checks. Note in
        /// particular that Saturn's and Uranus's rings are <i>not</i> declared here: the ring span every
        /// arrangement uses is measured off the real ring mesh by <c>SolarRowBuilder.FindRings</c>, and putting
        /// a second, hand-typed number beside it is how the two come to disagree.</para>
        ///
        /// <para><b>And why no orbital distances.</b> This system's two arrangements come from GDD 4.1 by way
        /// of <c>LayoutPresetBuilder</c>, which needs no semi-major axes.
        /// <see cref="SystemLayoutBuilder"/> — the generic path, for a system the GDD does not describe —
        /// is the only thing that reads them.</para>
        /// </summary>
        private static readonly Profile[] Profiles =
        {
            new Profile(
                SolarSystemId,
                "Our Solar System",
                "Assets/data/experiences/solar_system_planets.asset",
                "solar_system_planets_content_prefab",
                "solar_row",
                new[]
                {
                    new Row("sun",     BodyRole.Star,        1392700f, Poi("sun")),
                    new Row("mercury", BodyRole.Planet,         4879f, Poi("mercury")),
                    new Row("venus",   BodyRole.Planet,        12104f, Poi("venus")),
                    new Row("earth",   BodyRole.Planet,        12742f, Poi("earth")),
                    new Row("mars",    BodyRole.Planet,         6779f, Poi("mars")),
                    new Row("jupiter", BodyRole.Planet,       139820f, Poi("jupiter")),
                    new Row("saturn",  BodyRole.Planet,       116460f, Poi("saturn")),
                    new Row("uranus",  BodyRole.Planet,        50724f, Poi("uranus")),
                    new Row("neptune", BodyRole.Planet,        49244f, Poi("neptune")),
                    new Row("pluto",   BodyRole.DwarfPlanet,    2377f, Poi("pluto")),
                }),
        };

        // ---------- menu

        [MenuItem("Cosmic Simulation/Build System Profiles")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("SystemProfileBuilder: leave play mode first.");
                return;
            }

            var written = new List<SystemProfile>();
            foreach (var profile in Profiles)
            {
                var asset = Write(profile);
                if (asset != null)
                {
                    written.Add(asset);
                }
            }

            if (written.Count == 0)
            {
                return; // Write already said why
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Report(written);
        }

        /// <summary>
        /// The profile asset for <paramref name="id"/>, written from the table first if it is not there yet.
        /// <see cref="SolarRowBuilder"/> calls this so that <b>Build Solar Row Content</b> still works on a
        /// checkout that has never run <b>Build System Profiles</b> — the menu items the docs and the tickets
        /// name have to keep working on their own.
        ///
        /// <para><paramref name="rewriteFromTable"/> writes the asset even when it is already there.
        /// <b>Build Solar Row Content</b> passes true, and that is not belt-and-braces: our own system's body
        /// list <i>is</i> GDD 4.1, so that menu item has to produce the GDD's arrangement whatever anyone has
        /// since typed into the asset. A system the GDD does not describe has no such authority behind it, so
        /// the generic path leaves an existing profile alone.</para>
        /// </summary>
        public static SystemProfile EnsureProfile(string id, bool rewriteFromTable = false)
        {
            var existing = Load(id);
            if (existing != null && !rewriteFromTable)
            {
                return existing;
            }

            var index = System.Array.FindIndex(Profiles, p => p.Id == id);
            if (index < 0)
            {
                if (existing != null)
                {
                    // Asked to rewrite something the table does not describe. A hand-authored profile is a
                    // legitimate thing to have, so it is used as it is rather than refused.
                    return existing;
                }

                Debug.LogError($"SystemProfileBuilder: no system called '{id}', in the table or in {Folder}. " +
                               "Add a row to SystemProfileBuilder.Profiles, or author a SystemProfile asset.");
                return null;
            }

            var written = Write(Profiles[index]);
            if (written == null)
            {
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return written;
        }

        /// <summary>The profile asset with this id, by path first and then by a search, or null.</summary>
        public static SystemProfile Load(string id)
        {
            var direct = AssetDatabase.LoadAssetAtPath<SystemProfile>($"{Folder}/{id}.asset");
            if (direct != null && direct.Id == id)
            {
                return direct;
            }

            return AssetDatabase.FindAssets("t:SystemProfile", new[] { Folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SystemProfile>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(p => p != null && p.Id == id);
        }

        // ---------- writing

        private static SystemProfile Write(Profile source)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(source.ModulePath);
            if (module == null)
            {
                Debug.LogError($"SystemProfileBuilder: no experience module at {source.ModulePath}, so " +
                               $"'{source.Id}' has nothing to be hung on; nothing written.");
                return null;
            }

            var bodies = new List<SystemBody>(source.Bodies.Length);
            foreach (var row in source.Bodies)
            {
                GameObject art = null;
                if (!string.IsNullOrEmpty(row.ArtPrefabPath))
                {
                    art = AssetDatabase.LoadAssetAtPath<GameObject>(row.ArtPrefabPath);
                    if (art == null)
                    {
                        // Hard failure on purpose. A missing art prefab would otherwise leave SourcePrefab
                        // null, which means "draw this body generically" — so a moved file would quietly turn
                        // Earth into a grey ball instead of stopping the run.
                        Debug.LogError($"SystemProfileBuilder: '{source.Id}' names {row.ArtPrefabPath} for " +
                                       $"'{row.Id}' and it is not there. Nothing written, because a missing art " +
                                       "prefab would silently make the body generic.");
                        return null;
                    }
                }

                bodies.Add(new SystemBody
                {
                    Id = row.Id,
                    Role = row.Role,
                    Info = LoadBodyInfo(row.Id),
                    SourcePrefab = art,
                    DiameterKm = row.DiameterKm,
                    OrbitSemiMajorAu = row.OrbitSemiMajorAu,
                    Appearance = row.Appearance,
                });
            }

            var path = $"{Folder}/{source.Id}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SystemProfile>(path);
            var existed = profile != null;

            if (!existed)
            {
                profile = ScriptableObject.CreateInstance<SystemProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.Id = source.Id;
            profile.DisplayName = source.DisplayName;
            profile.Module = module;
            profile.ContentPrefabName = source.ContentPrefabName;
            profile.AuthoredLayoutId = source.AuthoredLayoutId;
            profile.Bodies = bodies.ToArray();
            EditorUtility.SetDirty(profile);

            Debug.Log($"SystemProfileBuilder: {(existed ? "updated" : "created")} {path} " +
                      $"('{source.DisplayName}', {bodies.Count} bodies, module '{module.Id}', authored in " +
                      $"'{source.AuthoredLayoutId}').");
            return profile;
        }

        private static BodyInfo LoadBodyInfo(string id)
        {
            var direct = AssetDatabase.LoadAssetAtPath<BodyInfo>($"{BodyFolder}/{id}.asset");
            if (direct != null && direct.Id == id)
            {
                return direct;
            }

            var found = AssetDatabase.FindAssets("t:BodyInfo", new[] { BodyFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<BodyInfo>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(b => b != null && b.Id == id);

            if (found == null)
            {
                Debug.LogWarning($"SystemProfileBuilder: no BodyInfo with Id '{id}' in {BodyFolder}. The body " +
                                 "will be built but its panel will be blank. Run Cosmic Simulation > Import Copy.");
            }

            return found;
        }

        // ---------- checking the run

        private static void Report(List<SystemProfile> written)
        {
            var text = new StringBuilder();
            text.AppendLine($"SystemProfileBuilder: {written.Count} system profile(s) in {Folder}.");

            foreach (var profile in written)
            {
                var observed = profile.Bodies.Count(b => b != null && b.Provenance == AppearanceSource.Observed);
                var modelled = profile.Bodies.Length - observed;

                text.AppendLine(
                    $"  {profile.Id,-16} '{profile.DisplayName}': {profile.Bodies.Length} bodies " +
                    $"({profile.StarCount} star(s), {observed} with real surface imagery, {modelled} modelled), " +
                    $"content '{profile.ContentPrefabName}', authored in '{profile.AuthoredLayoutId}'.");

                foreach (var body in profile.Bodies)
                {
                    if (body == null)
                    {
                        continue;
                    }

                    text.AppendLine(
                        $"    {body.Id,-10} {body.Role,-12} {body.DiameterKm,10:F0} km  " +
                        $"{body.Provenance}  " +
                        (body.SourcePrefab != null
                            ? AssetDatabase.GetAssetPath(body.SourcePrefab)
                            : $"generic ({body.Appearance.Class})") +
                        (body.Info == null ? "  NO COPY" : string.Empty));
                }

                if (modelled > 0)
                {
                    text.AppendLine(
                        $"    {modelled} of these bodies are drawn from parameters rather than from observed " +
                        "imagery. D-010 requires their panel copy to say so.");
                }

                if (profile.StarCount > 1)
                {
                    text.AppendLine(
                        $"    {profile.StarCount} stars: this system has no single central body, so any " +
                        "arrangement built around 'the star' is wrong for it. See SystemLayoutBuilder's report.");
                }

                if (profile.StarCount == 0)
                {
                    text.AppendLine(
                        "    no star at all. Nothing downstream will light these bodies, and a pulsar or a " +
                        "free-floating planet is not something this app draws as a lit ball.");
                }
            }

            Debug.Log(text.ToString());
        }

        /// <summary>
        /// Proves <see cref="StarColor"/> against values produced independently by the same published method,
        /// quoted in <c>docs/research/exoplanet_systems.md</c> section 4.2. Run this after touching that file:
        /// the colour maths is the one part of a modelled body that claims to be a computation rather than a
        /// convention, so it is the one part that has to be checkable.
        /// </summary>
        [MenuItem("Cosmic Simulation/Check Star Colour Model")]
        public static void CheckStarColours()
        {
            // Temperature, name, expected sRGB, expected 400-700 nm fraction.
            var cases = new[]
            {
                new object[] { 8039f, "Beta Pictoris", "#E2E7FF", 0.384f },
                new object[] { 5772f, "the Sun", "#FFF1EA", 0.367f },
                new object[] { 3459f, "TOI-700", "#FFC789", 0.138f },
                new object[] { 2900f, "Proxima Centauri", "#FFB567", 0.070f },
                new object[] { 2566f, "TRAPPIST-1", "#FFA94F", 0.039f },
            };

            var text = new StringBuilder();
            text.AppendLine("StarColor check, against docs/research/exoplanet_systems.md section 4.2:");
            var off = 0;

            foreach (var row in cases)
            {
                var temperature = (float)row[0];
                var name = (string)row[1];
                var expectedHex = (string)row[2];
                var expectedFraction = (float)row[3];

                var hex = StarColor.Hex(StarColor.Srgb(temperature));
                var fraction = StarColor.VisibleBandFraction(temperature);

                // A channel or two out in the last digit is rounding, not a broken model; a different hue is
                // not. The fraction is compared to within a hundredth for the same reason.
                var colourMatches = hex == expectedHex;
                var fractionMatches = Mathf.Abs(fraction - expectedFraction) <= 0.01f;
                if (!colourMatches || !fractionMatches)
                {
                    off++;
                }

                text.AppendLine(
                    $"  {name,-18} {temperature,6:F0} K  {hex} (expected {expectedHex})" +
                    (colourMatches ? string.Empty : "  COLOUR DIFFERS") +
                    $"  visible {fraction:F3} (expected {expectedFraction:F3})" +
                    (fractionMatches ? string.Empty : "  FRACTION DIFFERS"));
            }

            if (off > 0)
            {
                text.AppendLine(
                    $"  {off} of {cases.Length} rows differ. Either the integration changed or the published " +
                    "table did; do not adjust the expected values to match the code without saying why.");
                Debug.LogError(text.ToString());
                return;
            }

            Debug.Log(text.ToString());
        }
    }
}
