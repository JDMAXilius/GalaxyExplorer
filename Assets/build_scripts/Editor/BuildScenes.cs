// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using UnityEditor;

namespace GalaxyExplorer.Build
{
    /// <summary>The scenes a player build takes, refused unless the app boots where it should (CS-334).</summary>
    public static class BuildScenes
    {
        public const string Boot = "Assets/scenes/main_scene.unity";

        public static string[] Enabled(out string problem)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Distinct().ToArray();
            problem = scenes.Length == 0 ? "no scene is enabled in Build Settings"
                : scenes[0] != Boot ? $"the first enabled scene is {scenes[0]}, not {Boot}, so the player would boot into it"
                : scenes.Any(p => p.StartsWith("Assets/Cosmic/")) ? "a scene from the parked rework (Assets/Cosmic) is enabled"
                : null;
            return scenes;
        }
    }
}
