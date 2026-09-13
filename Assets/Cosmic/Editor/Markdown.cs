using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Cosmic.Editor
{
    public static class Markdown
    {
        public static string[] ReadLines(string folder, string file)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), folder, file);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Copy file missing: {folder}/{file}");
            }

            return File.ReadAllLines(path);
        }

        public static List<(string Id, List<string> Lines)> Sections(string[] lines)
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

        public static string Field(IEnumerable<string> lines, string name)
        {
            var prefix = $"**{name}:**";
            var line = lines.FirstOrDefault(l => l.TrimStart().StartsWith(prefix));
            return line == null ? null : line.Trim().Substring(prefix.Length).Trim();
        }

        public static string[] Paragraphs(IEnumerable<string> lines)
        {
            var result = new List<string>();
            var buffer = new StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                var prose = line.Length > 0
                            && !line.StartsWith("|")
                            && !line.StartsWith("**")
                            && !line.StartsWith("---")
                            && !line.StartsWith("#");
                if (prose)
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

        public static List<string[]> TableRows(IEnumerable<string> lines)
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
                    continue;
                }

                if (!seenHeader)
                {
                    seenHeader = true;
                    continue;
                }

                rows.Add(cells);
            }

            return rows;
        }

        public static Dictionary<string, string> Sentences(string[] lines)
        {
            var sentences = new Dictionary<string, string>();
            string id = null;
            var buffer = new StringBuilder();

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("**") && line.IndexOf("**", 2, StringComparison.Ordinal) > 0)
                {
                    if (id != null)
                    {
                        sentences[id] = buffer.ToString().Trim();
                        buffer.Clear();
                    }

                    var end = line.IndexOf("**", 2, StringComparison.Ordinal);
                    id = line.Substring(2, end - 2).Trim();
                    buffer.Append(line.Substring(end + 2).TrimStart(' ', '-', '\u2014').Trim());
                }
                else if (id != null)
                {
                    if (line.Length == 0)
                    {
                        sentences[id] = buffer.ToString().Trim();
                        buffer.Clear();
                        id = null;
                    }
                    else if (!line.StartsWith("|") && !line.StartsWith("#") && !line.StartsWith("---"))
                    {
                        buffer.Append(' ').Append(line);
                    }
                }
            }

            if (id != null)
            {
                sentences[id] = buffer.ToString().Trim();
            }

            return sentences;
        }

        public static float Number(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            var digits = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return float.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0f;
        }

        public static string[] Ids(string list) => string.IsNullOrEmpty(list)
            ? Array.Empty<string>()
            : list.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
