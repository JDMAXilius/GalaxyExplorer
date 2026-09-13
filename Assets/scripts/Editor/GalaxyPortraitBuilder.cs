// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Renders each named galaxy as a transparent portrait sprite, for the pins that stand in the Galaxies
    /// sphere where that galaxy really is.
    ///
    /// <para><b>Why a portrait and not the galaxy itself.</b> A built galaxy is tens of thousands of points
    /// with its own command buffers. Four of them hanging in the sphere at once, on top of the 420 deep-field
    /// sprites already there, is a frame budget spent on things the player is looking at from across the room.
    /// A portrait is one quad and one texture, and it is a plot of that galaxy's <b>own baked star data</b> -
    /// the same numbers the real thing draws, through <see cref="GalaxyPreview.Accumulate"/> - so it is a
    /// picture of that galaxy rather than a picture of some galaxy.</para>
    ///
    /// <para><b>Alpha carries the light.</b> The preview PNGs are opaque because they are read on a monitor;
    /// a portrait hangs in space, so its alpha is the tone-mapped luminance and the empty sky between the
    /// arms is empty. Premultiplied would be better for an additive shader and is not what Unity's default
    /// sprite import expects, so this stays straight alpha and the material does the rest.</para>
    /// </summary>
    public static class GalaxyPortraitBuilder
    {
        private const string Folder = "Assets/Textures/galaxies/portraits";

        [MenuItem("Cosmic Simulation/Build Galaxy Portraits")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var written = 0;
            var missing = new System.Collections.Generic.List<string>();

            var paths = new System.Collections.Generic.List<string>();
            foreach (var profile in GalaxyProfiles.All())
            {
                var path = Build(profile);
                if (path != null) { written++; paths.Add(path); }
                else missing.Add(profile.Id);
            }

            // Import settings in a second pass, after a refresh. On the first write the asset does not exist
            // in the database yet, so AssetImporter.GetAtPath answers null and every setting is dropped in
            // silence - which is how four portraits ended up as plain textures that LoadAssetAtPath<Sprite>
            // could not see at all.
            AssetDatabase.Refresh();
            foreach (var path in paths) AsSprite(path);
            AssetDatabase.Refresh();
            Debug.Log($"GalaxyPortraitBuilder: {written} portrait(s) written to {Folder}"
                      + (missing.Count > 0 ? $"; nothing baked yet for: {string.Join(", ", missing)}" : string.Empty));
        }

        /// <summary>
        /// Spreads each star over a few pixels. The plot puts one pixel per star because that is what the
        /// preview wants - a faithful scatter to check arms and colour against. A portrait is seen from
        /// across a room as a thing that glows, and an unsoftened plot reads as dust on the lens: the stars
        /// never touch, so there is no disc, only speckle. A separable box blur run three times is a close
        /// enough gaussian and costs nothing on a 900 px square built once in the editor.
        /// </summary>
        private static void Soften(Vector3[] acc, int edge)
        {
            const int Radius = 3;
            const int Passes = 3;
            var temp = new Vector3[acc.Length];

            for (var pass = 0; pass < Passes; pass++)
            {
                Sweep(acc, temp, edge, Radius, true);
                Sweep(temp, acc, edge, Radius, false);
            }

            // Blurring conserves light but spreads it, so a core that was solid goes grey. Putting the peak
            // back keeps the bulge reading as a bulge rather than as a wide smudge.
            var gain = Radius * 0.6f;
            for (var i = 0; i < acc.Length; i++) acc[i] *= gain;
        }

        private static void Sweep(Vector3[] from, Vector3[] to, int edge, int radius, bool horizontal)
        {
            var window = radius * 2 + 1;
            for (var major = 0; major < edge; major++)
            {
                for (var minor = 0; minor < edge; minor++)
                {
                    var sum = Vector3.zero;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var at = Mathf.Clamp(minor + k, 0, edge - 1);
                        sum += horizontal ? from[major * edge + at] : from[at * edge + major];
                    }

                    var index = horizontal ? major * edge + minor : minor * edge + major;
                    to[index] = sum / window;
                }
            }
        }

        private static void AsSprite(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogWarning($"GalaxyPortraitBuilder: {path} has no texture importer, so it stays a plain texture.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        private static string Build(GalaxyProfile profile)
        {
            var acc = GalaxyPreview.Accumulate(profile);
            if (acc == null) return null;

            var edge = GalaxyPreview.Edge;
            Soften(acc, edge);
            var pixels = new Color32[edge * edge];
            for (var i = 0; i < acc.Length; i++)
            {
                // The same Reinhard roll-off and gamma the preview uses, so a portrait and its preview are
                // the same image with one of them carrying its light in alpha instead of on black.
                var v = acc[i];
                var r = Mathf.Clamp01(Mathf.Pow(v.x / (1f + v.x), 0.75f));
                var g = Mathf.Clamp01(Mathf.Pow(v.y / (1f + v.y), 0.75f));
                var b = Mathf.Clamp01(Mathf.Pow(v.z / (1f + v.z), 0.75f));

                // Luminance, not the largest channel: a red-dominant outer arm and a blue one of the same
                // brightness should be equally solid, and max() would make the blue one thinner.
                var light = Mathf.Clamp01(0.2126f * r + 0.7152f * g + 0.0722f * b);
                pixels[i] = new Color32(
                    (byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)(light * 255f));
            }

            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            var path = $"{Folder}/{profile.Id}_portrait.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            return path;
        }
    }
}
