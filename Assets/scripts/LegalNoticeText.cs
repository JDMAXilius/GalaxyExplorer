// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

namespace GalaxyExplorer
{
    /// <summary>
    /// Writes one of the app's legal notices into a TextMeshPro element.
    /// </summary>
    /// <remarks>
    /// Self-contained on purpose: the About slate is a hand-authored prefab, and every text it shows is typed into
    /// the asset. A notice or a version number typed in the same way goes stale the moment the build script's
    /// constants change, so those two are filled from code instead. Adding one to the slate is then a prefab-only
    /// job - drop this on the text object and pick a notice.
    /// </remarks>
    [DisallowMultipleComponent]
    public class LegalNoticeText : MonoBehaviour
    {
        public enum Notice
        {
            /// <summary>"Cosmic Simulation XR 0.9.0", from the version the build script owns.</summary>
            Version,

            /// <summary>One line of MIT attribution; only use where the full text is also reachable.</summary>
            GalaxyExplorerAttribution,

            /// <summary>The full, unaltered MIT License text. Needs a large element - roughly 1.1 kB.</summary>
            GalaxyExplorerLicense,

            /// <summary>Our own copyright notice, which is not Microsoft's and must not be merged with it.</summary>
            ProjectNotice,
        }

        [SerializeField, Tooltip("Text element to fill. Left empty, the TextMeshPro on this object is used.")]
        private TMP_Text target;

        [SerializeField, Tooltip("Which notice to write out.")]
        private Notice notice = Notice.GalaxyExplorerAttribution;

        [SerializeField, TextArea, Tooltip("Optional text placed in front of the notice, e.g. \"Version \". Local to this element, not a shared style.")]
        private string prefix = string.Empty;

        private void OnEnable()
        {
            Apply();
        }

        /// <summary>
        /// Reads the notice and writes it out. Safe to call at any time, including the frame this component is
        /// added, because nothing here depends on Awake having run.
        /// </summary>
        public void Apply()
        {
            if (target == null)
            {
                target = GetComponent<TMP_Text>();
            }

            if (target == null)
            {
                Debug.LogError($"[LegalNoticeText] '{name}' has no TMP_Text to write {notice} into.", this);
                return;
            }

            target.text = prefix + TextFor(notice);
        }

        private static string TextFor(Notice notice)
        {
            switch (notice)
            {
                case Notice.Version:
                    return LegalNotices.VersionLine;
                case Notice.GalaxyExplorerLicense:
                    return LegalNotices.GalaxyExplorerLicense;
                case Notice.ProjectNotice:
                    return LegalNotices.ProjectNotice;
                default:
                    return LegalNotices.GalaxyExplorerAttribution;
            }
        }
    }
}
