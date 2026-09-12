// Unity 6 upgrade: headless Windows x64 build.
// Usage: Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Unity6WindowsBuild.BuildWindows64

using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GalaxyExplorer.Build
{
    public static class Unity6WindowsBuild
    {
        private const string OutputPath = "Builds/Win64/CosmicSimulationXR.exe";

        public static void BuildWindows64()
        {
            // Mono, because only the Mono Windows player module is installed on this machine.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // The desktop build shows the same version on its About slate as the Quest build, and this script does
            // not run Quest3ProjectSetup, so read the constant that owns it directly (decision D-001a).
            PlayerSettings.bundleVersion = Quest3ProjectSetup.AppVersion;
            Quest3ProjectSetup.EnsureLegalNoticesShip();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Distinct().ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[GEBuild] result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"scenes={scenes.Length} size={summary.totalSize} bytes time={summary.totalTime} output={summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
