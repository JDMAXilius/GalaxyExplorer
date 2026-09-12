// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Reads the copy deck in <c>docs/copy/</c> and writes it into ScriptableObjects under
    /// <c>Assets/data/</c>. Writers edit markdown; nobody types player-facing text into Unity.
    /// Re-running is safe: existing assets are updated in place, so references survive.
    /// </summary>
    public static class CopyImporter
    {
        private const string CopyFolder = "docs/copy";
        private const string DataFolder = "Assets/data";

        [MenuItem("Cosmic Simulation/Import Copy")]
        public static void Import()
        {
            var log = new StringBuilder("Copy import\n");
            try
            {
                AssetDatabase.StartAssetEditing();
                EnsureFolders();
                var bodies = ImportBodies(log);
                ImportMoons(bodies, log);
                ImportExperiences(log);
                ImportDestinations(log);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(log.ToString());
        }

        // ---------- files and folders

        private static void EnsureFolders()
        {
            // Create through the file system, not AssetDatabase.CreateFolder: inside StartAssetEditing the
            // database is not refreshed, so IsValidFolder still reports a folder we just made as missing and
            // CreateFolder happily makes "data 1", "data 2", ... beside it.
            foreach (var sub in new[] { "experiences", "destinations", "bodies", "moons", "layouts" })
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), DataFolder, sub));
            }

            AssetDatabase.Refresh();
        }

        private static string[] ReadLines(string file)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), CopyFolder, file);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Copy file missing: {CopyFolder}/{file}");
            }

            return File.ReadAllLines(path);
        }

        private static T LoadOrCreate<T>(string folder, string id) where T : ScriptableObject
        {
            var path = $"{DataFolder}/{folder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        // ---------- markdown helpers

        /// <summary>Splits a file into "## id" sections, dropping everything before the first one.</summary>
        private static List<(string Id, List<string> Lines)> Sections(string[] lines)
        {
            var sections = new List<(string, List<string>)>();
            List<string> current = null;
            foreach (var raw in lines)
            {
                if (raw.StartsWith("## "))
                {
                    current = new List<string>();
                    sections.Add((raw.Substring(3).Trim(), current));
                }
                else
                {
                    current?.Add(raw);
                }
            }

            return sections;
        }

        private static string Field(IEnumerable<string> lines, string name)
        {
            var prefix = $"**{name}:**";
            var line = lines.FirstOrDefault(l => l.TrimStart().StartsWith(prefix));
            return line == null ? null : line.Trim().Substring(prefix.Length).Trim();
        }

        /// <summary>
        /// Prose paragraphs: wrapped lines joined, blank line ends a paragraph. Table rows, bold fields,
        /// horizontal rules and headings are not prose.
        /// </summary>
        private static string[] Paragraphs(IEnumerable<string> lines)
        {
            var result = new List<string>();
            var buffer = new StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                var isProse = line.Length > 0
                              && !line.StartsWith("|")
                              && !line.StartsWith("**")
                              && !line.StartsWith("---")
                              && !line.StartsWith("#");
                if (isProse)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Append(' ');
                    }
                    buffer.Append(line);
                }
                else if (buffer.Length > 0)
                {
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }
            }

            if (buffer.Length > 0)
            {
                result.Add(buffer.ToString());
            }

            return result.ToArray();
        }

        /// <summary>Body rows of a pipe table, header and separator dropped, cells trimmed.</summary>
        private static List<string[]> TableRows(IEnumerable<string> lines)
        {
            var rows = new List<string[]>();
            var seenHeader = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!line.StartsWith("|"))
                {
                    continue;
                }

                var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
                if (cells.All(c => c.Length == 0 || c.All(ch => ch == '-' || ch == ':')))
                {
                    continue; // separator
                }

                if (!seenHeader)
                {
                    seenHeader = true;
                    continue; // header
                }

                rows.Add(cells);
            }

            return rows;
        }

        // ---------- importers

        private static Dictionary<string, BodyInfo> ImportBodies(StringBuilder log)
        {
            var byId = new Dictionary<string, BodyInfo>();
            foreach (var (id, lines) in Sections(ReadLines("bodies.md")))
            {
                var body = LoadOrCreate<BodyInfo>("bodies", id);
                body.Id = id;
                body.DisplayName = Field(lines, "Title") ?? id;
                body.Subtitle = Field(lines, "Subtitle") ?? string.Empty;
                var prose = Paragraphs(lines);
                body.Paragraph = prose.Length > 0 ? prose[0] : string.Empty;
                body.Stats = TableRows(lines)
                    .Where(r => r.Length >= 3 && r[0].Length > 0)
                    .Select(r => new Stat
                    {
                        Label = r[0],
                        Value = r[1],
                        Unit = r.Length > 2 ? r[2] : string.Empty,
                        Exponent = r.Length > 3 && int.TryParse(r[3], out var e) ? e : 0,
                    })
                    .ToArray();
                EditorUtility.SetDirty(body);
                byId[id] = body;
            }

            log.AppendLine($"bodies: {byId.Count} ({string.Join(", ", byId.Keys)})");
            return byId;
        }

        private static void ImportMoons(Dictionary<string, BodyInfo> bodies, StringBuilder log)
        {
            var lines = ReadLines("moons.md");
            var rows = TableRows(lines);
            var sentences = new Dictionary<string, string>();

            // "**moon** - one sentence", wrapped across lines.
            string currentId = null;
            var buffer = new StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("**") && line.Contains("**"))
                {
                    if (currentId != null)
                    {
                        sentences[currentId] = buffer.ToString().Trim();
                        buffer.Clear();
                    }

                    var end = line.IndexOf("**", 2, StringComparison.Ordinal);
                    currentId = line.Substring(2, end - 2).Trim();
                    var rest = line.Substring(end + 2).TrimStart(' ', '-', '—').Trim();
                    buffer.Append(rest);
                }
                else if (currentId != null)
                {
                    if (line.Length == 0)
                    {
                        sentences[currentId] = buffer.ToString().Trim();
                        buffer.Clear();
                        currentId = null;
                    }
                    else if (!line.StartsWith("|") && !line.StartsWith("#") && !line.StartsWith("---"))
                    {
                        buffer.Append(' ').Append(line);
                    }
                }
            }

            if (currentId != null)
            {
                sentences[currentId] = buffer.ToString().Trim();
            }

            var moonsByParent = new Dictionary<string, List<BodyInfo>>();
            var count = 0;
            foreach (var row in rows.Where(r => r.Length >= 5))
            {
                var id = row[0];
                if (id.Length == 0 || !sentences.ContainsKey(id))
                {
                    continue;
                }

                var moon = LoadOrCreate<BodyInfo>("moons", id);
                moon.Id = id;
                moon.DisplayName = row[1];
                moon.Paragraph = sentences[id];
                var parentName = row[2];
                moon.Subtitle = parentName.ToUpperInvariant();
                moon.Stats = new[]
                {
                    new Stat { Label = "ORBITS", Value = parentName },
                    new Stat { Label = "DIAMETER", Value = row[3].Replace(" km", string.Empty), Unit = "km" },
                    new Stat { Label = "ONE ORBIT", Value = row[4].Replace(" days", string.Empty), Unit = "days" },
                };

                var parentId = parentName.ToLowerInvariant();
                if (bodies.TryGetValue(parentId, out var parent))
                {
                    moon.Orbits = parent;
                    if (!moonsByParent.TryGetValue(parentId, out var list))
                    {
                        moonsByParent[parentId] = list = new List<BodyInfo>();
                    }
                    list.Add(moon);
                }

                EditorUtility.SetDirty(moon);
                count++;
            }

            foreach (var (parentId, list) in moonsByParent.Select(kv => (kv.Key, kv.Value)))
            {
                bodies[parentId].Moons = list.ToArray();
                EditorUtility.SetDirty(bodies[parentId]);
            }

            log.AppendLine($"moons: {count}; attached to " +
                           string.Join(", ", moonsByParent.Select(kv => $"{kv.Key}({kv.Value.Count})")));
        }

        private static void ImportExperiences(StringBuilder log)
        {
            var ids = new List<string>();
            foreach (var (id, lines) in Sections(ReadLines("experiences.md")))
            {
                var module = LoadOrCreate<ExperienceModule>("experiences", id);
                module.Id = id;
                module.Kind = ExperienceKind.DockTile;
                module.Panel = new ScenePanelCopy
                {
                    Title = Field(lines, "Title") ?? id,
                    Paragraphs = Paragraphs(lines),
                    Instruction = Field(lines, "Instruction") ?? string.Empty,
                };
                // A dock tile is 110 mm wide and a panel title is not built to that. Falling back to the
                // panel title is what put "Galactic Center - Sagittarius A*" on a tile and overran the one
                // beside it, so the copy deck now carries a short Dock name and a Second line per
                // experience, and the fallback is only for an entry that has not been given one yet.
                module.DisplayName = Field(lines, "Dock") ?? module.Panel.Title;
                module.SecondLine = Field(lines, "Second line") ?? string.Empty;

                EditorUtility.SetDirty(module);
                ids.Add(id);
            }

            log.AppendLine($"experiences: {ids.Count} ({string.Join(", ", ids)})");
        }

        private static void ImportDestinations(StringBuilder log)
        {
            var lines = ReadLines("nebulae.md");
            var meta = TableRows(lines)
                .Where(r => r.Length >= 2)
                .ToDictionary(r => r[0], r => r[1]);

            var ids = new List<string>();
            foreach (var (id, section) in Sections(lines))
            {
                var module = LoadOrCreate<ExperienceModule>("destinations", id);
                module.Id = id;
                module.Kind = ExperienceKind.Destination;
                module.Environment = EnvironmentMode.BlackHalo;
                module.DisplayName = meta.TryGetValue(id, out var label) ? label : id;
                module.Panel = new ScenePanelCopy
                {
                    Title = Field(section, "Title") ?? module.DisplayName,
                    Paragraphs = Paragraphs(section),
                    Instruction = string.Empty,
                };
                EditorUtility.SetDirty(module);
                ids.Add(id);
            }

            log.AppendLine($"destinations: {ids.Count} ({string.Join(", ", ids)})");
        }
    }
}
