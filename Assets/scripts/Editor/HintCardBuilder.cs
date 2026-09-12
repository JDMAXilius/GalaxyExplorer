// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the two hint cards of GDD 8.6 into the <see cref="HintCardSet"/> the runtime loads.
    ///
    /// The copy comes from <c>docs/copy/hints.md</c>, parsed the same way <see cref="CopyImporter"/> parses the
    /// rest of the deck, so the wording is owned by a writer and not by this file. The art is looked for in
    /// <c>Assets/ui/figma/</c> first - that is where the exported card art lands - and falls back to the intro's
    /// own onboarding hand sprites, which are already in the project, already white line art on transparency, and
    /// already what this app's first prompt looks like. Reuse before sourcing before generating.
    ///
    /// Re-running updates the asset in place, so anything pointing at it survives.
    /// </summary>
    public static class HintCardBuilder
    {
        private const string CopyFile = "docs/copy/hints.md";
        private const string FigmaFolder = "Assets/ui/figma/";
        private const string OnboardingFolder = "Assets/Textures/onboarding_sprites/";

        // A Resources folder, because HintCards builds its own canvas and has no prefab to hang references off,
        // and a sprite nothing in a scene references is not in the player build at all. See HintCardSet.
        private const string OutputFolder = "Assets/scriptable_objects/Resources";

        /// <summary>
        /// How each card in the deck behaves. Keyed by the heading in hints.md rather than read out of its prose:
        /// "the first two-hand scale" is a sentence for a person, and turning sentences into enum values is how a
        /// reworded line silently switches a card off.
        /// </summary>
        private static readonly (string Id, HintAction Action, bool TwoHanded, float TravelUnits)[] Behaviour =
        {
            ("hint_grab",  HintAction.Grab,   false, 14f),
            ("hint_scale", HintAction.Resize, true,  16f),
        };

        // GDD 8.6: a 3 s loop, and the card auto-advances after 6 s.
        private const float LoopSeconds = 3f;
        private const float HoldSeconds = 6f;

        [MenuItem("Cosmic Simulation/Build Hint Cards")]
        public static void Build()
        {
            var lines = ReadCopy();
            if (lines == null)
            {
                return;
            }

            var sections = Sections(lines);
            var cards = new List<HintCard>();
            var usedFallback = false;

            foreach (var (id, action, twoHanded, travel) in Behaviour)
            {
                var section = sections.FirstOrDefault(s => s.Id == id);
                if (section.Lines == null)
                {
                    Debug.LogError($"HintCardBuilder: {CopyFile} has no '## {id}' section.");
                    continue;
                }

                var frames = Exported(id);
                if (frames.Length == 0)
                {
                    frames = Fallback(id);
                    usedFallback = true;
                }

                cards.Add(new HintCard
                {
                    Id = id,
                    Title = Field(section.Lines, "Title") ?? id,
                    Line = Field(section.Lines, "Line") ?? string.Empty,
                    DismissOn = action,
                    TwoHanded = twoHanded,
                    Frames = frames,
                    LoopSeconds = LoopSeconds,
                    HoldSeconds = HoldSeconds,
                    TravelUnits = travel,
                });

                Debug.Log($"HintCardBuilder: {id} - {frames.Length} frame(s).");
            }

            if (cards.Count == 0)
            {
                Debug.LogError("HintCardBuilder: nothing to write.");
                return;
            }

            Save(cards.ToArray());

            if (usedFallback)
            {
                // A log, not a warning: the fallback is a shipping state, not a fault, and the editor relay
                // reports NOT-OK on any logged warning even when the command succeeded (CLAUDE.md).
                Debug.Log(
                    "HintCardBuilder: at least one card is using the intro's onboarding hand sprites. Drop the " +
                    $"exported card art into {FigmaFolder} named <card id>_01.png, _02.png ... and re-run to " +
                    "replace it.");
            }
        }

        // ---------- the copy deck

        private static string[] ReadCopy()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), CopyFile);
            if (!File.Exists(path))
            {
                Debug.LogError($"HintCardBuilder: copy file missing: {CopyFile}");
                return null;
            }

            return File.ReadAllLines(path);
        }

        /// <summary>Splits the file into "## id" sections, dropping everything before the first one.</summary>
        private static List<(string Id, List<string> Lines)> Sections(string[] lines)
        {
            var sections = new List<(string, List<string>)>();
            List<string> current = null;
            foreach (var raw in lines)
            {
                if (raw.StartsWith("## ", StringComparison.Ordinal))
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
            var line = lines.FirstOrDefault(l => l.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
            return line == null ? null : line.Trim().Substring(prefix.Length).Trim();
        }

        // ---------- the art

        /// <summary>Exported card art, in file-name order, so _01 comes before _02.</summary>
        private static Sprite[] Exported(string id)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), FigmaFolder);
            if (!Directory.Exists(folder))
            {
                return Array.Empty<Sprite>();
            }

            return Directory.GetFiles(folder, id + "*.png")
                .Select(file => FigmaFolder + Path.GetFileName(file))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path))
                .Where(sprite => sprite != null)
                .ToArray();
        }

        /// <summary>
        /// The intro's own hand sprites: an open hand and the same hand pinched. Cross-faded they are the gesture,
        /// which is precisely what <c>onboarding_force_hl2_prefab</c> does with them on its Timeline.
        /// </summary>
        private static Sprite[] Fallback(string id)
        {
            // Both cards are about a pinch, so both use the same pair; the two-hand card shows it twice, mirrored.
            var names = new[] { "onboarding_pull_sprite", "onboarding_hold_sprite" };

            var frames = names
                .Select(name => AssetDatabase.LoadAssetAtPath<Sprite>(OnboardingFolder + name + ".png"))
                .Where(sprite => sprite != null)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"HintCardBuilder: no art for {id}; {OnboardingFolder} has no hand sprites either.");
            }

            return frames;
        }

        // ---------- the asset

        private static void Save(HintCard[] cards)
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), OutputFolder));
            AssetDatabase.Refresh();

            var path = $"{OutputFolder}/{HintCardSet.ResourceName}.asset";
            var set = AssetDatabase.LoadAssetAtPath<HintCardSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<HintCardSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.Cards = cards;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"HintCardBuilder: {cards.Length} card(s) written to {path}.");
        }
    }
}
