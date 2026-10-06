// Licensed under the MIT License. See LICENSE in the project root for license information.

// Meta Quest 3 APK build.
// Usage: Cosmic Simulation > Quest 3 > Build APK (debug-signed, for the bench), or
//        Cosmic Simulation > Quest 3 > Build Store APK (release-signed from the environment, for upload), or
//        Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Quest3Build.BuildApk
//        Unity.exe -batchmode -quit -projectPath . -executeMethod GalaxyExplorer.Build.Quest3Build.BuildStoreApk
// Install: adb install -r Builds/Quest3/CosmicSimulationXR.apk (or drag the APK into Meta Quest Developer Hub).
// Signing setup for the store build: docs/release/RELEASE_SIGNING.md.

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GalaxyExplorer.Build
{
    public static class Quest3Build
    {
        private const string OutputPath = "Builds/Quest3/CosmicSimulationXR.apk";
        private const string StoreOutputPath = "Builds/Quest3/CosmicSimulationXR-store.apk";

        // The release keystore lives outside the repository and its passwords live only in the process
        // environment (CS-236). Nothing here writes them to ProjectSettings.
        private const string KeystorePathVar = "COSMIC_KEYSTORE_PATH";
        private const string KeystorePassVar = "COSMIC_KEYSTORE_PASS";
        private const string KeyAliasVar = "COSMIC_KEYALIAS";
        private const string KeyAliasPassVar = "COSMIC_KEYALIAS_PASS";

        [MenuItem("Cosmic Simulation/Quest 3/Build APK")]
        public static void BuildApk()
        {
            ExitIfFailed(Build(OutputPath));
        }

        [MenuItem("Cosmic Simulation/Quest 3/Build Store APK")]
        public static void BuildStoreApk()
        {
            var keystorePath = Environment.GetEnvironmentVariable(KeystorePathVar);
            var keystorePass = Environment.GetEnvironmentVariable(KeystorePassVar);
            var keyAlias = Environment.GetEnvironmentVariable(KeyAliasVar);
            var keyAliasPass = Environment.GetEnvironmentVariable(KeyAliasPassVar);

            var missing = new[]
                {
                    (KeystorePathVar, keystorePath), (KeystorePassVar, keystorePass),
                    (KeyAliasVar, keyAlias), (KeyAliasPassVar, keyAliasPass),
                }
                .Where(v => string.IsNullOrEmpty(v.Item2))
                .Select(v => v.Item1)
                .ToArray();

            if (missing.Length > 0)
            {
                Fail($"Store build refused: environment variable(s) not set: {string.Join(", ", missing)}. " +
                     "Set them in the shell that starts Unity (docs/release/RELEASE_SIGNING.md) and restart the editor; " +
                     "a running editor does not see variables set afterwards.");
                ExitIfFailed(false);
                return;
            }

            if (!File.Exists(keystorePath))
            {
                Fail($"Store build refused: {KeystorePathVar} points at '{keystorePath}', which does not exist.");
                ExitIfFailed(false);
                return;
            }

            // Keystore name and alias are serialized into ProjectSettings.asset; the two passwords are not
            // (Unity keeps them in memory for the editor session only). Put all of it back before anything can
            // end the process, so the bench build stays debug-signed and no path from this machine ends up in a
            // commit: a batch-mode exit inside the try would skip the finally, which is why Build only reports.
            var hadCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            var previousKeystore = PlayerSettings.Android.keystoreName;
            var previousAlias = PlayerSettings.Android.keyaliasName;
            var succeeded = false;
            try
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystorePath);
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = keyAlias;
                PlayerSettings.Android.keyaliasPass = keyAliasPass;

                succeeded = Build(StoreOutputPath);
            }
            finally
            {
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                PlayerSettings.Android.keystoreName = previousKeystore;
                PlayerSettings.Android.keyaliasName = previousAlias;
                PlayerSettings.Android.useCustomKeystore = hadCustomKeystore;
                AssetDatabase.SaveAssets();
            }

            ExitIfFailed(succeeded);
        }

        private static bool Build(string outputPath)
        {
            Quest3ProjectSetup.ConfigureProject();

            // An APK that does not carry the MIT notice for the inherited Galaxy Explorer code is not
            // distributable, so refuse to produce one rather than leave the problem to be noticed at submission.
            if (!Quest3ProjectSetup.EnsureLegalNoticesShip())
            {
                Fail("Aborted: the legal notices would not ship with this APK. See the [Legal] errors above.");
                return false;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Distinct().ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[GEBuild] Quest 3 result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"scenes={scenes.Length} size={summary.totalSize / (1024 * 1024)} MB time={summary.totalTime} output={summary.outputPath}");

            return summary.result == BuildResult.Succeeded;
        }

        private static void Fail(string message) => Debug.LogError("[GEBuild] " + message);

        // Unattended builds signal failure through the exit code. Called last, after every setting is restored.
        private static void ExitIfFailed(bool succeeded)
        {
            if (!succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
