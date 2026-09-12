using System.Collections.Generic;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Points every place and every moon at its narration clip, by name.
    /// <para>
    /// The clips follow one convention - <c>vo_destination_&lt;id&gt;_audio_clip.wav</c> - which the 22
    /// inherited Microsoft recordings already used, so the 18 generated in September 2026 were named to
    /// match rather than inventing a second scheme. That means this wiring is a lookup rather than a table,
    /// and a clip added later needs no code change: name it correctly and re-run.
    /// </para>
    /// <para>
    /// <b>Provenance matters here and is recorded in <c>Assets/_sources/CREDITS.md</c>.</b> The 22 original
    /// clips are the recorded performance inherited with the MIT project. The 18 new ones are synthesised
    /// (Higgsfield, seed_audio, preset voice "Holden"), chosen by measuring the fundamental frequency of
    /// the originals - a median of about 112 Hz across four clips - and picking the preset that landed
    /// closest, at about 103 Hz. They are deliberately a near match in register rather than a clone of the
    /// original performer's voice.
    /// </para>
    /// </summary>
    public static class NarrationWiring
    {
        private const string ClipFolder = "Assets/audio/vo_audio_clips/vo_destinations_audio_clips";

        /// <summary>
        /// The handful of inherited clips whose file name does not match the id the project later gave the
        /// place. Both are Microsoft recordings, so renaming the .wav would break its .meta GUID and every
        /// reference to it for no gain - an alias is the cheaper truth.
        /// </summary>
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["crab"] = "crab_nebula",
            ["trumpler14"] = "trumpler",
        };

        [MenuItem("Cosmic Simulation/Wire Narration")]
        public static void Wire()
        {
            var wired = new List<string>();
            var missing = new List<string>();
            var alreadySet = 0;

            // Places: every ExperienceModule, whether a dock tile or a destination.
            foreach (var guid in AssetDatabase.FindAssets("t:ExperienceModule"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(path);
                if (module == null || string.IsNullOrEmpty(module.Id))
                {
                    continue;
                }

                var result = Apply(module, module.Id, "Narration");
                Record(result, module.Id, wired, missing, ref alreadySet);
            }

            // Moons and bodies. A moon's clip is named for the moon, not for its parent.
            foreach (var guid in AssetDatabase.FindAssets("t:BodyInfo"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var info = AssetDatabase.LoadAssetAtPath<BodyInfo>(path);
                if (info == null || string.IsNullOrEmpty(info.Id))
                {
                    continue;
                }

                var result = Apply(info, info.Id, "Narration");
                Record(result, info.Id, wired, missing, ref alreadySet);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            wired.Sort();
            missing.Sort();

            Debug.Log($"NarrationWiring: {wired.Count} newly wired, {alreadySet} already correct, " +
                      $"{missing.Count} with no clip.\n" +
                      $"  wired: {string.Join(", ", wired)}\n" +
                      $"  no clip yet: {string.Join(", ", missing)}\n" +
                      "  A missing clip is not an error - several places and bodies have never had " +
                      "narration recorded or generated.");
        }

        private enum Result { Wired, AlreadySet, NoClip }

        private static Result Apply(Object asset, string id, string field)
        {
            var file = Aliases.TryGetValue(id, out var alias) ? alias : id;
            var clipPath = $"{ClipFolder}/vo_destination_{file}_audio_clip.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                return Result.NoClip;
            }

            var so = new SerializedObject(asset);
            var property = so.FindProperty(field);
            if (property == null)
            {
                return Result.NoClip;
            }

            if (property.objectReferenceValue == clip)
            {
                return Result.AlreadySet;
            }

            property.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return Result.Wired;
        }

        private static void Record(Result result, string id, List<string> wired, List<string> missing,
                                   ref int alreadySet)
        {
            switch (result)
            {
                case Result.Wired:
                    wired.Add(id);
                    break;
                case Result.AlreadySet:
                    alreadySet++;
                    break;
                default:
                    missing.Add(id);
                    break;
            }
        }
    }
}
