// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace GalaxyExplorer.Build
{
    /// <summary>
    /// Idempotent project configuration for the Meta Quest 3 port (OpenXR on Windows for Quest Link,
    /// OpenXR + Meta Quest features on Android). Safe to re-run.
    /// </summary>
    public static class Quest3ProjectSetup
    {
        private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string AndroidPackageId = "com.jdmaxilius.galaxyexplorer";

        // Enabled for both Standalone (Quest Link) and Android (standalone Quest 3).
        private static readonly string[] CommonFeatures =
        {
            "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile",
            "UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile",
            "UnityEngine.XR.Hands.OpenXR.HandTracking",
            "UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim",
            "UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature", // passthrough
        };

        private static readonly string[] AndroidOnlyFeatures =
        {
            "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.DisplayUtilitiesFeature", // refresh rate control
        };

        [MenuItem("Galaxy Explorer/Quest 3/Configure Project")]
        public static void ConfigureProject()
        {
            EnsureOpenXRLoaders();
            var log = new StringBuilder("Quest 3 project setup\n");

            EnableFeatures(BuildTargetGroup.Standalone, CommonFeatures, log);
            EnableFeatures(BuildTargetGroup.Android, CommonFeatures.Concat(AndroidOnlyFeatures), log);

            // Quest (Vulkan): multiview. The vertex stage gets each eye's matrices from the view index, so the app's
            // shaders need no stereo macros. Windows over Link (D3D11): multi-pass, because single-pass instanced
            // there would need instancing macros in every custom shader; the PC has headroom for it.
            OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone).renderMode = OpenXRSettings.RenderMode.MultiPass;

            ConfigureAndroidPlayer(log);
            ConfigureQuality(log);

            // "MS HRTF Spatializer" is a Windows-only plugin; Unity's built-in 3D panning is used instead.
            var audioManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var audioManager = new SerializedObject(audioManagerAsset);
            audioManager.FindProperty("m_SpatializerPlugin").stringValue = string.Empty;
            audioManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioManagerAsset); // project settings are not saved unless marked dirty

            AssetDatabase.SaveAssets();
            AppendValidationIssues(BuildTargetGroup.Standalone, log);
            AppendValidationIssues(BuildTargetGroup.Android, log);
            Debug.Log(log.ToString());
        }

        private static void EnableFeatures(BuildTargetGroup group, IEnumerable<string> typeNames, StringBuilder log)
        {
            var features = OpenXRSettings.GetSettingsForBuildTargetGroup(group).GetFeatures();
            foreach (var typeName in typeNames)
            {
                var feature = features.FirstOrDefault(f => f.GetType().FullName == typeName);
                if (feature == null)
                {
                    log.AppendLine($"  [{group}] MISSING feature {typeName}");
                    continue;
                }

                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            log.AppendLine($"[{group}] enabled: " + string.Join(", ", features.Where(f => f.enabled).Select(f => f.GetType().Name)));
        }

        private static void ConfigureAndroidPlayer(StringBuilder log)
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(android, AndroidPackageId);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34; // Meta store requirement
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft; // the only orientation Quest supports
            log.AppendLine($"[Android] IL2CPP, ARM64, API {PlayerSettings.Android.minSdkVersion}-{PlayerSettings.Android.targetSdkVersion}, Vulkan, ASTC, id {AndroidPackageId}");
        }

        private static void ConfigureQuality(StringBuilder log)
        {
            // Meta recommends 4x MSAA on Quest; the project has a single quality level, shared with desktop.
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var quality = new SerializedObject(qualityAsset);
            var levels = quality.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++)
            {
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("antiAliasing").intValue = 4;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qualityAsset);
            log.AppendLine($"Quality: 4x MSAA on {levels.arraySize} level(s)");
        }

        [MenuItem("Galaxy Explorer/Quest 3/Apply Android Asset Overrides")]
        public static void ApplyAndroidAssetOverrides()
        {
            var log = new StringBuilder("Android asset overrides\n");
            const string android = "Android";

            AssetDatabase.StartAssetEditing();
            try
            {
                // Quest 3 has memory for 2048 ASTC textures; only the 4096 maps are capped.
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures", "Assets/models", "Assets/materials", "Assets/SolarSystem", "Assets/UI" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.maxTextureSize <= 2048)
                    {
                        continue;
                    }

                    var settings = importer.GetPlatformTextureSettings(android);
                    if (settings.overridden && settings.maxTextureSize <= 2048)
                    {
                        continue;
                    }

                    settings.overridden = true;
                    settings.maxTextureSize = 2048;
                    settings.format = importer.textureType == TextureImporterType.NormalMap
                        ? TextureImporterFormat.ASTC_4x4
                        : TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                    log.AppendLine($"  texture {path}: Android 2048 {settings.format}");
                }

                // Clips loaded fully decompressed cost tens of MB each on Android; stream the long ones instead.
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/audio" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is AudioImporter importer))
                    {
                        continue;
                    }

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    var defaults = importer.defaultSampleSettings;
                    if (clip == null || clip.length < 20f || defaults.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        continue;
                    }

                    var settings = importer.GetOverrideSampleSettings(android);
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.7f;
                    importer.SetOverrideSampleSettings(android, settings);
                    importer.SaveAndReimport();
                    log.AppendLine($"  audio {path} ({clip.length:F0}s): Android streaming");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log(log.ToString());
        }

        private static void AppendValidationIssues(BuildTargetGroup group, StringBuilder log)
        {
            // Some Meta Quest rules read the settings of the *selected* build target, so Android rules only
            // evaluate correctly while Android is the active platform.
            if (group == BuildTargetGroup.Android && EditorUserBuildSettings.selectedBuildTargetGroup != BuildTargetGroup.Android)
            {
                log.AppendLine("[Android] OpenXR validation skipped (switch the build target to Android to validate)");
                return;
            }

            var issues = new List<OpenXRFeature.ValidationRule>();
            try
            {
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, group);
            }
            catch (System.Exception e)
            {
                log.AppendLine($"[{group}] OpenXR validation threw: {e.Message}");
                return;
            }
            log.AppendLine($"[{group}] OpenXR validation issues: {issues.Count}");
            foreach (var issue in issues)
            {
                // Apply Unity's own automatic fixes for the Quest build (e.g. latency optimization).
                if (group == BuildTargetGroup.Android && issue.fixItAutomatic && issue.fixIt != null)
                {
                    issue.fixIt();
                    log.AppendLine($"  fixed: {issue.message}");
                    continue;
                }
                log.AppendLine($"  {(issue.error ? "ERROR" : "warn")}: {issue.message}");
            }
        }

        [MenuItem("Galaxy Explorer/Quest 3/List OpenXR Features")]
        public static void ListFeatures()
        {
            EnsureOpenXRLoaders();
            var sb = new StringBuilder();
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                sb.AppendLine($"[{group}]");
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null) continue;
                foreach (var feature in settings.GetFeatures())
                {
                    sb.AppendLine($"  {(feature.enabled ? "[x]" : "[ ]")} {feature.GetType().FullName} :: {feature.name}");
                }
            }
            Debug.Log(sb.ToString());
        }

        private static void EnsureOpenXRLoaders()
        {
            var perTarget = GetOrCreateGeneralSettings();
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                if (!perTarget.HasSettingsForBuildTarget(group))
                {
                    perTarget.CreateDefaultSettingsForBuildTarget(group);
                }

                var general = perTarget.SettingsForBuildTarget(group);
                if (general.Manager == null)
                {
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                }

                general.InitManagerOnStart = true;
                if (!general.Manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoader))
                {
                    XRPackageMetadataStore.AssignLoader(general.Manager, OpenXRLoader, group);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateGeneralSettings()
        {
            // XRGeneralSettingsPerBuildTarget.GetOrCreate() is internal; it is what the XR Plug-in Management
            // settings page calls to create Assets/XR/XRGeneralSettingsPerBuildTarget.asset.
            var method = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.NonPublic | BindingFlags.Static);
            return (XRGeneralSettingsPerBuildTarget)method.Invoke(null, null);
        }
    }
}
