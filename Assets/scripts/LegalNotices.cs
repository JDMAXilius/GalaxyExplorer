// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer
{
    /// <summary>
    /// The legal notices that have to travel inside the player, and the only place that knows where they live.
    /// </summary>
    /// <remarks>
    /// The MIT License covering the inherited Galaxy Explorer code requires its copyright notice and permission
    /// notice to be included in every copy of the software, so the text cannot live only in the repository root:
    /// <c>Quest3ProjectSetup</c> copies <c>License.txt</c> into <c>Assets/Resources/legal/</c> at the start of
    /// every build, and this class reads it back at runtime. Microsoft's notice and ours are deliberately two
    /// separate files: do not reword, trim or merge them.
    /// </remarks>
    public static class LegalNotices
    {
        /// <summary>Verbatim copy of the repository's <c>License.txt</c>, kept in sync by the build script.</summary>
        public const string GalaxyExplorerLicenseResource = "legal/galaxy_explorer_license";

        /// <summary>Our own copyright and derivation statement, kept apart from Microsoft's notice.</summary>
        public const string ProjectNoticeResource = "legal/cosmic_simulation_xr_notice";

        /// <summary>
        /// The short attribution the About copy requires word-for-word. It stands in for the full text only when
        /// the full text is also reachable, never on its own.
        /// </summary>
        public const string GalaxyExplorerAttribution =
            "Galaxy Explorer (c) Microsoft Corporation. Used under the MIT License.";

        private static string _galaxyExplorerLicense;
        private static string _projectNotice;

        /// <summary>The full, unaltered MIT License text for the inherited Galaxy Explorer code.</summary>
        public static string GalaxyExplorerLicense => Load(ref _galaxyExplorerLicense, GalaxyExplorerLicenseResource);

        /// <summary>Cosmic Simulation XR's own copyright notice.</summary>
        public static string ProjectNotice => Load(ref _projectNotice, ProjectNoticeResource);

        /// <summary>
        /// "Cosmic Simulation XR 0.9.0". Both halves come from the player settings the build script writes, so this
        /// cannot disagree with the APK's own version the way a hand-typed label would.
        /// </summary>
        public static string VersionLine => Application.productName + " " + Application.version;

        private static string Load(ref string cache, string resourcePath)
        {
            if (!string.IsNullOrEmpty(cache))
            {
                return cache;
            }

            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                // A build that cannot show the notice is a licence breach, so fail loudly rather than draw a blank
                // panel, and still put the attribution on screen.
                Debug.LogError($"[LegalNotices] Resources/{resourcePath}.txt is missing from this build. Run " +
                               "Cosmic Simulation > Quest 3 > Configure Project to restore it from License.txt.");
                cache = GalaxyExplorerAttribution;
                return cache;
            }

            cache = asset.text;
            return cache;
        }
    }
}
