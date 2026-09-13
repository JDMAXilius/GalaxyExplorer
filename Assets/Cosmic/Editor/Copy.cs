using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Cosmic.Editor.Markdown;

namespace Cosmic.Editor
{
    public static class Copy
    {
        const string CopyFolder = "docs/copy";
        const string OutFolder = "Assets/Cosmic/Data/Generated";

        [MenuItem("Cosmic/Import Copy")]
        public static void Import()
        {
            var bodies = new Dictionary<string, Body>();
            var places = new Dictionary<string, Place>();
            int moons = 0, destinations = 0, hints = 0, wired = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                EnsureFolders();
                ImportBodies(bodies);
                moons = ImportMoons(bodies);
                ImportPlaces(places);
                destinations = ImportDestinations(places);
                hints = ImportHints(places);
                wired = Wire(places, bodies);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"Cosmic copy import: {bodies.Count - moons} bodies, {moons} moons, " +
                      $"{places.Count - destinations - hints} places, {destinations} destinations, {hints} hints, " +
                      $"{wired} references wired -> {OutFolder}");
        }

        static void EnsureFolders()
        {
            // Not AssetDatabase.CreateFolder: inside StartAssetEditing it makes "bodies 1", "bodies 2" instead.
            foreach (var sub in new[] { "places", "bodies", "layouts" })
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), OutFolder, sub));
            }

            AssetDatabase.Refresh();
        }

        static string[] Read(string file) => ReadLines(CopyFolder, file);

        static T LoadOrCreate<T>(string folder, string id) where T : ScriptableObject
        {
            var path = $"{OutFolder}/{folder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static void ImportBodies(Dictionary<string, Body> bodies)
        {
            foreach (var (id, lines) in Sections(Read("bodies.md")))
            {
                var body = LoadOrCreate<Body>("bodies", id);
                body.id = id;
                body.title = Field(lines, "Title") ?? id;
                body.subtitle = Field(lines, "Subtitle") ?? string.Empty;
                var prose = Paragraphs(lines);
                body.paragraph = prose.Length > 0 ? prose[0] : string.Empty;
                body.stats = TableRows(lines)
                    .Where(r => r.Length >= 3 && r[0].Length > 0)
                    .Select(r => new Stat
                    {
                        label = r[0],
                        value = r[1],
                        unit = r.Length > 2 ? r[2] : string.Empty,
                        exponent = r.Length > 3 && int.TryParse(r[3], out var e) ? e : 0,
                    })
                    .ToArray();

                var diameter = body.stats.FirstOrDefault(s => s.label == "Diameter");
                if (!string.IsNullOrEmpty(diameter.value))
                {
                    body.diameterKm = Number(diameter.value);
                }

                EditorUtility.SetDirty(body);
                bodies[id] = body;
            }
        }

        static int ImportMoons(Dictionary<string, Body> bodies)
        {
            var lines = Read("moons.md");
            var sentences = Sentences(lines);
            var byParent = new Dictionary<string, List<Body>>();
            var count = 0;

            foreach (var row in TableRows(lines).Where(r => r.Length >= 5))
            {
                var id = row[0];
                if (id.Length == 0 || !sentences.ContainsKey(id))
                {
                    continue;
                }

                var moon = LoadOrCreate<Body>("bodies", id);
                moon.id = id;
                moon.title = row[1];
                moon.paragraph = sentences[id];
                var parentName = row[2];
                moon.subtitle = parentName.ToUpperInvariant();
                moon.diameterKm = Number(row[3]);
                moon.stats = new[]
                {
                    new Stat { label = "ORBITS", value = parentName, unit = string.Empty },
                    new Stat { label = "DIAMETER", value = row[3].Replace(" km", string.Empty), unit = "km" },
                    new Stat { label = "ONE ORBIT", value = row[4].Replace(" days", string.Empty), unit = "days" },
                };

                var parentId = parentName.ToLowerInvariant();
                if (bodies.TryGetValue(parentId, out var parent))
                {
                    moon.orbits = parent;
                    if (!byParent.TryGetValue(parentId, out var list))
                    {
                        byParent[parentId] = list = new List<Body>();
                    }

                    list.Add(moon);
                }

                EditorUtility.SetDirty(moon);
                bodies[id] = moon;
                count++;
            }

            foreach (var pair in byParent)
            {
                bodies[pair.Key].moons = pair.Value.ToArray();
                EditorUtility.SetDirty(bodies[pair.Key]);
            }

            return count;
        }

        static void ImportPlaces(Dictionary<string, Place> places)
        {
            foreach (var (id, lines) in Sections(Read("experiences.md")))
            {
                var place = LoadOrCreate<Place>("places", id);
                place.id = id;
                place.panel = new PanelCopy
                {
                    title = Field(lines, "Title") ?? id,
                    paragraphs = Paragraphs(lines),
                    instruction = Field(lines, "Instruction") ?? string.Empty,
                };
                place.title = Field(lines, "Dock") ?? place.panel.title;
                place.subtitle = Field(lines, "Second line") ?? string.Empty;

                // The room is not copy, so it is only taken when the deck states one; otherwise it is authored.
                if (Enum.TryParse<RoomMode>(Field(lines, "Room"), true, out var room))
                {
                    place.room = room;
                }

                EditorUtility.SetDirty(place);
                places[id] = place;
            }
        }

        static int ImportDestinations(Dictionary<string, Place> places)
        {
            var lines = Read("nebulae.md");
            var labels = TableRows(lines).Where(r => r.Length >= 2).ToDictionary(r => r[0], r => r[1]);
            var count = 0;

            foreach (var (id, section) in Sections(lines))
            {
                var place = LoadOrCreate<Place>("places", id);
                place.id = id;
                place.title = labels.TryGetValue(id, out var label) ? label : id;
                place.subtitle = Field(section, "Second line") ?? string.Empty;
                place.room = RoomMode.Halo;
                place.panel = new PanelCopy
                {
                    title = Field(section, "Title") ?? place.title,
                    paragraphs = Paragraphs(section),
                    instruction = string.Empty,
                };
                EditorUtility.SetDirty(place);
                places[id] = place;
                count++;
            }

            return count;
        }

        static int ImportHints(Dictionary<string, Place> places)
        {
            var count = 0;
            foreach (var (id, lines) in Sections(Read("hints.md")))
            {
                var place = LoadOrCreate<Place>("places", id);
                place.id = id;
                place.title = Field(lines, "Title") ?? id;
                place.panel = new PanelCopy
                {
                    title = place.title,
                    paragraphs = Array.Empty<string>(),
                    instruction = Field(lines, "Line") ?? string.Empty,
                };
                EditorUtility.SetDirty(place);
                places[id] = place;
                count++;
            }

            return count;
        }

        static int Wire(Dictionary<string, Place> places, Dictionary<string, Body> bodies)
        {
            var wired = 0;
            foreach (var (id, lines) in Sections(Read("experiences.md")))
            {
                if (!places.TryGetValue(id, out var place))
                {
                    continue;
                }

                var bodyIds = Ids(Field(lines, "Bodies"));
                if (bodyIds.Length > 0)
                {
                    place.bodies = bodyIds.Where(bodies.ContainsKey).Select(b => bodies[b]).ToArray();
                    wired += place.bodies.Length;
                }

                var placeIds = Ids(Field(lines, "Destinations"));
                if (placeIds.Length > 0)
                {
                    place.destinations = placeIds.Where(places.ContainsKey).Select(p => places[p]).ToArray();
                    wired += place.destinations.Length;
                }

                EditorUtility.SetDirty(place);
            }

            return wired;
        }
    }
}
