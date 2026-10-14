// Windows x64 builds: the plain one and the Steam-shaped one (CS-253).
// Menu:  Cosmic Simulation > Windows > Build              -> Builds/Win64/CosmicSimulationXR.exe
//        Cosmic Simulation > Windows > Build Steam Build  -> Builds/Steam/win64/CosmicSimulationXR.exe
// Batch: Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Unity6WindowsBuild.BuildWindows64
//        Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Unity6WindowsBuild.BuildSteam
// Switches (batch command line): -il2cpp (IL2CPP instead of Mono), -allow-dev-key (bench build with a key in it).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GalaxyExplorer.Build
{
    public static class Unity6WindowsBuild
    {
        private const string OutputPath = "Builds/Win64/CosmicSimulationXR.exe";
        private const string SteamOutputPath = "Builds/Steam/win64/CosmicSimulationXR.exe";

        [MenuItem("Cosmic Simulation/Windows/Build")]
        public static void BuildWindows64() => Build(OutputPath);

        [MenuItem("Cosmic Simulation/Windows/Build Steam Build")]
        public static void BuildSteam() => Build(SteamOutputPath);

        private static void Build(string outputPath)
        {
            var args = Environment.GetCommandLineArgs();

            // A key typed into the asset is serialized into the player, and a Mono player is trivial to read.
            if (BeingKeyGuard.ShipsKey() && !args.Contains("-allow-dev-key"))
            {
                Fail($"{BeingKeyGuard.KeysAsset} holds a key, which would ship inside the build. Clear the field " +
                     "(the editor still reads OPENAI_API_KEY from the environment or .env), or pass -allow-dev-key for a bench build.");
                return;
            }

            // With this on, the player starts OpenXR on every launch and -desktop would still wake SteamVR (CS-250).
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr != null && xr.InitManagerOnStart)
            {
                Fail("Standalone has 'Initialize XR on Startup' on. Run Cosmic Simulation > Quest 3 > Configure Project, " +
                     "or untick it under XR Plug-in Management > Windows; GalaxyExplorer.XR.Launch starts XR on -vr.");
                return;
            }

            // Mono by default, because only the Mono Windows player module is installed on this machine. With
            // -il2cpp and no IL2CPP module, Unity's own build fails, which is the loud failure wanted.
            var backend = args.Contains("-il2cpp") ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);

            // The desktop build shows the same version on its About slate as the Quest build, and this script does
            // not run Quest3ProjectSetup, so read the constant that owns it directly (decision D-001a).
            PlayerSettings.bundleVersion = Quest3ProjectSetup.AppVersion;
            if (!Quest3ProjectSetup.EnsureLegalNoticesShip())
            {
                Fail("The legal notices would not ship; see the [Legal] error above.");
                return;
            }

            var scenes = BuildScenes.Enabled(out var sceneProblem);
            if (sceneProblem != null)
            {
                Fail(sceneProblem + ".");
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[GEBuild] result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"scenes={scenes.Length} size={summary.totalSize} bytes time={summary.totalTime} output={summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                Fail("The Windows build did not succeed.");
                return;
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(outputPath), "build_info.txt"),
                $"version {Quest3ProjectSetup.AppVersion}\ncommit {Commit()}\nbackend {backend}\nbuilt {DateTime.UtcNow:u}\n");
        }

        private static string Commit()
        {
            try
            {
                using (var git = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }))
                {
                    var commit = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit();
                    return git.ExitCode == 0 && commit.Length > 0 ? commit : "unknown";
                }
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        // From the menu an error in the Console is enough; a batch run has to return a failing exit code.
        private static void Fail(string message)
        {
            Debug.LogError("[GEBuild] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
