// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Import settings for the sourced astronomy plates in <c>Assets/Textures/nebulae/</c> and
    /// <c>Assets/Textures/galaxies/</c>.
    ///
    /// These are photographs, not UI: they are seen at arm's length filling a good part of the view, they are
    /// sampled at every angle as the player walks around an overlay, and there are enough of them that they
    /// have to be compressed. So unlike the UI sprites they keep mip maps, take ASTC on Android, and give up
    /// CPU-side read access, which halves their memory.
    ///
    /// Doing this here rather than by hand means a re-downloaded or re-cropped plate cannot quietly come back
    /// as an uncompressed, readable 2048 that costs 16 MB on the device.
    /// </summary>
    public class AstronomyTextureImporter : AssetPostprocessor
    {
        private static readonly string[] Folders =
        {
            "Assets/Textures/nebulae/",
            "Assets/Textures/galaxies/",
        };

        /// <summary>
        /// Folders inside the ones above that this must keep its hands off. The galaxy portraits live under
        /// Textures/galaxies because that is where galaxy imagery belongs, but they are sprites with alpha,
        /// not deep-field plates: the rules below would set them to Default and, worse, strip their alpha
        /// channel outright, which is the thing that carries their light. That failure is silent - a portrait
        /// simply stops loading as a Sprite and the pin that wanted it gets nothing.
        /// </summary>
        private static readonly string[] NotOurs =
        {
            "Assets/Textures/galaxies/portraits/",
        };

        // Bump when the rules below change, or Unity will not reimport plates it has already processed and a
        // stale setting ships silently.
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            var match = Array.Exists(Folders,
                f => assetPath.StartsWith(f, StringComparison.OrdinalIgnoreCase));
            var excluded = Array.Exists(NotOurs,
                f => assetPath.StartsWith(f, StringComparison.OrdinalIgnoreCase));
            if (!match || excluded)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.sRGBTexture = true;

            // The plates are already downscaled to their intended size, so this is a ceiling rather than a
            // resize; a smaller file keeps its own resolution.
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(android);
        }

        [MenuItem("Cosmic Simulation/Reimport Astronomy Textures")]
        private static void ReimportAll()
        {
            var count = 0;
            foreach (var folder in Folders)
            {
                var trimmed = folder.TrimEnd('/');
                if (!AssetDatabase.IsValidFolder(trimmed))
                {
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { trimmed }))
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                    count++;
                }
            }

            Debug.Log($"Astronomy textures reimported: {count}");
        }
    }
}
