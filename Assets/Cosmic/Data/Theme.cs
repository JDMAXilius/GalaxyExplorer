using TMPro;
using UnityEngine;

namespace Cosmic
{
    [CreateAssetMenu(menuName = "Cosmic/Theme")]
    public class Theme : ScriptableObject
    {
        public Color ink = Color.white;
        public Color inkSecondary = new Color32(0x9E, 0xB8, 0xC4, 0xFF);
        public Color accentCyan = new Color32(0x6C, 0xCF, 0xDD, 0xFF);
        public Color tagDark = new Color32(0x0E, 0x14, 0x18, 0xCC);

        // Type millimetres are TMP em sizes on the one-unit-is-one-millimetre canvas; the cap heights the docs quote are 0.7 of them.
        public float titleMm = 24f;
        public float subtitleMm = 8f;
        public float statMm = 12f;
        public float labelMm = 6.5f;
        public float bodyMm = 9.5f;
        public float tagMm = 5f;
        public float moonSmallMm = 5f;
        public float moonLargeMm = 18f;

        public float tagRadiusMm = 4f;
        public float plateRadiusMm = 6f;

        public float panelWidthMm = 161f;
        public Vector2 tagSizeMm = new Vector2(60f, 24f);
        public Vector2 dockTileMm = new Vector2(110f, 62f);

        public TMP_FontAsset font;
    }
}
