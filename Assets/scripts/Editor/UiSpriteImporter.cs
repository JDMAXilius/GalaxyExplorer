// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Import settings for everything exported out of Figma into <c>Assets/ui/figma/</c>.
    ///
    /// The whole UI is white or alpha only and is tinted at runtime from the colour tokens, so these sprites
    /// carry shape, not colour. Sizes are authored at 8 px per millimetre, which is why the pixels-per-unit is
    /// 8000: one Unity unit is a metre, so a sprite pixel is exactly a millimetre and the numbers in
    /// <c>docs/ui/spec.md</c> can be used directly when laying out a panel.
    ///
    /// A file named <c>ui_rounded_r&lt;n&gt;</c> is nine-sliced with a border of n + 2 px, the extra two pixels
    /// keeping the corner arc inside the border so it is never stretched.
    /// </summary>
    public class UiSpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/ui/figma/";
        private const float PixelsPerMillimetre = 8f;
        private const float PixelsPerUnit = PixelsPerMillimetre * 1000f;

        private static readonly Regex RoundedRect = new Regex(@"^ui_rounded_r(\d+)$", RegexOptions.Compiled);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;

            // These are a few kilobytes each and sit right in front of the player's eyes; block compression
            // on a 24 px rounded corner is far more expensive to look at than it is to store.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.spriteBorder = BorderFor(Path.GetFileNameWithoutExtension(assetPath));
        }

        /// <summary>Nine-slice border in pixels, as left/bottom/right/top. Zero means a plain stretched sprite.</summary>
        private static Vector4 BorderFor(string fileName)
        {
            var match = RoundedRect.Match(fileName);
            if (!match.Success)
            {
                // Icons are drawn whole, and the tile foot is a vertical gradient stretched sideways.
                return Vector4.zero;
            }

            var radius = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var border = radius + 2;
            return new Vector4(border, border, border, border);
        }

        [MenuItem("Cosmic Simulation/Reimport UI Sprites")]
        private static void ReimportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') });
            foreach (var guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"UI sprites reimported: {guids.Length}");
        }
    }
}
