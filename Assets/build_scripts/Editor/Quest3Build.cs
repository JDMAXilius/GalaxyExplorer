// Licensed under the MIT License. See LICENSE in the project root for license information.

// Meta Quest 3 APK build.
// Usage: Cosmic Simulation > Quest 3 > Build APK, or
//        Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Quest3Build.BuildApk
// Install: adb install -r Builds/Quest3/CosmicSimulationXR.apk (or drag the APK into Meta Quest Developer Hub).

using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GalaxyExplorer.Build
{
    public static class Quest3Build
    {
        private const string OutputPath = "Builds/Quest3/CosmicSimulationXR.apk";

        [MenuItem("Cosmic Simulation/Quest 3/Build APK")]
        public static void BuildApk()
        {
            Quest3ProjectSetup.ConfigureProject();

            // An APK that does not carry the MIT notice for the inherited Galaxy Explorer code is not
            // distributable, so refuse to produce one rather than leave the problem to be noticed at submission.
            if (!Quest3ProjectSetup.EnsureLegalNoticesShip())
            {
                Debug.LogError("[GEBuild] Aborted: the legal notices would not ship with this APK. See the [Legal] errors above.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Distinct().ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[GEBuild] Quest 3 result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"scenes={scenes.Length} size={summary.totalSize / (1024 * 1024)} MB time={summary.totalTime} output={summary.outputPath}");

            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
