// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Unity.XR.Management.AndroidManifest.Editor;
using UnityEngine.XR.OpenXR;

namespace GalaxyExplorer.Build
{
    /// <summary>
    /// The three manifest entries Meta's release-manifest page asks for that the Unity packages do not write.
    /// </summary>
    /// <remarks>
    /// XR Plug-in Management collects every <see cref="IAndroidManifestRequirementProvider"/> in the project while
    /// the Gradle project is generated (<c>AndroidManifestBuildEventReceiver</c>) - the same route the OpenXR
    /// Meta Quest feature uses for its own entries - so this needs no custom AndroidManifest.xml and survives
    /// the manifest merge. Requirements: https://developers.meta.com/horizon/resources/publish-mobile-manifest/
    /// </remarks>
    public class Quest3Manifest : IAndroidManifestRequirementProvider
    {
        /// <summary>Meta's documented device names. Decision D-012: Quest 3 and Quest 3S only.</summary>
        public const string SupportedDevices = "quest3|quest3s";

        public ManifestRequirement ProvideManifestRequirement()
        {
            return new ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<System.Type> { typeof(OpenXRLoader) },
                OverrideElements = new List<ManifestElement>
                {
                    // "In the activity node which launches your app, android:excludeFromRecents must be set to true."
                    new ManifestElement
                    {
                        ElementPath = new List<string> { "manifest", "application", "activity" },
                        Attributes = new Dictionary<string, string>
                        {
                            { "name", "com.unity3d.player.UnityPlayerGameActivity" },
                            { "excludeFromRecents", "true" },
                        },
                    },
                    // The Meta Quest feature writes Quest 3 under its old codename, "eureka"; Meta's page lists
                    // "quest3". Quest3ProjectSetup limits the feature's own device list to the same two headsets.
                    new ManifestElement
                    {
                        ElementPath = new List<string> { "manifest", "application", "meta-data" },
                        Attributes = new Dictionary<string, string>
                        {
                            { "name", "com.oculus.supportedDevices" },
                            { "value", SupportedDevices },
                        },
                    },
                },
                RemoveElements = new List<ManifestElement>
                {
                    // Unity adds BLUETOOTH whenever the Microphone class is used. The app does not use it, and the
                    // OpenXR package's own (pre XR Management 4.4) path strips it "since it will cause projects to
                    // fail Meta cert" (ModifyAndroidManifestMeta.cs); the current path no longer does.
                    new ManifestElement
                    {
                        ElementPath = new List<string> { "manifest", "uses-permission" },
                        Attributes = new Dictionary<string, string> { { "name", "android.permission.BLUETOOTH" } },
                    },
                },
            };
        }
    }
}
