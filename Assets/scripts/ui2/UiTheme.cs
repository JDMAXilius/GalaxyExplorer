// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 look in one place. Values mirror <c>docs/design/v2/tokens.json</c>; sizes are millimetres,
    /// because every UI 2.0 canvas is built at one canvas unit per millimetre like the legacy ones.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Panel = new Color32(0x18, 0x1D, 0x24, 0xFF);
        public static readonly Color Deep = new Color32(0x0E, 0x14, 0x18, 0xFF);
        public static readonly Color Ink = Color.white;
        public static readonly Color InkSecondary = new Color32(0xAA, 0xB6, 0xC2, 0xFF);
        public static readonly Color Accent = new Color32(0x37, 0xE1, 0xFD, 0xFF);
        public static readonly Color Divider = new Color32(0x41, 0x50, 0x5D, 0xFF);

        /// <summary>The faint fill behind a hovered control.</summary>
        public static readonly Color HoverWash = new Color(1f, 1f, 1f, 0.07f);

        /// <summary>The dark teal fill behind a selected control.</summary>
        public static readonly Color SelectedWash = new Color(0.02f, 0.085f, 0.105f, 1f);

        public const float TargetMm = 24f;
        public const float GapMm = 12f;

        private const string FlagKey = "CosmicSimulation.Ui2";

        /// <summary>Whether the UI 2.0 shell replaces the legacy dock. On unless turned off (Cosmic Simulation > UI 2 > Turn Off).</summary>
        public static bool Enabled
        {
            get => _enabled ??= PlayerPrefs.GetInt(FlagKey, 1) == 1;
            set
            {
                _enabled = value;
                PlayerPrefs.SetInt(FlagKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private static bool? _enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _enabled = null;
    }
}
