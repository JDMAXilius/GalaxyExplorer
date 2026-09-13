using System.IO;
using GalaxyExplorer;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Renders a flat PNG of a built galaxy straight from its baked star data, without play mode.
    /// <para>
    /// This exists because <c>DrawStars</c> carries no <c>ExecuteAlways</c>, so a galaxy draws nothing in
    /// the editor - the only way to see one is to enter play mode, and entering play mode through the relay
    /// does not currently work (CS-119). Rather than leave the content unviewable, this reproduces the one
    /// piece of shader maths that turns a <see cref="StarVertDescriptor"/> into a position, exactly as
    /// <c>Assets/shaders/cginc/StarPositionCompute.cginc</c> does, and plots the result.
    /// </para>
    /// <para>
    /// <b>What this is and is not.</b> It is a faithful plot of the real committed star data: the same
    /// positions, the same per-star colours, the same relative sizes. It is <b>not</b> what the app looks
    /// like. There is no additive blending curve, no sprite texture, no dust shadowing (the dust layer
    /// subtracts light in the real shader and is drawn here as dark dots), no tilt and no perspective. Use
    /// it to check that a galaxy has the arms, bulge and colour it should. Judge the look in a headset.
    /// </para>
    /// </summary>
    public static class GalaxyPreview
    {
        private const int Size = 900;
        private const string OutputFolder = "Logs/galaxy_previews";

        [MenuItem("Cosmic Simulation/Preview Galaxies (PNG)")]
        public static void PreviewAll()
        {
            Directory.CreateDirectory(OutputFolder);
            var written = 0;

            foreach (var profile in GalaxyProfiles.All())
            {
                if (Render(profile))
                {
                    written++;
                }
            }

            Debug.Log($"GalaxyPreview: {written} image(s) written to {OutputFolder}/. " +
                      "Face-on, no tilt, no additive curve - structure only, not the shipped look.");
        }

        /// <summary>The square edge of every image this produces, in pixels.</summary>
        internal const int Edge = Size;

        /// <summary>
        /// The light this galaxy's baked stars put on a square, in float so overlapping stars add rather than
        /// clip - the way an additive shader would. Null when the galaxy has nothing baked yet. Shared with
        /// <c>GalaxyPortraitBuilder</c> so the portraits in the sky and the previews on disk are plots of the
        /// same numbers by the same arithmetic; a second copy of the maths is exactly how the two would drift.
        /// </summary>
        internal static Vector3[] Accumulate(GalaxyProfile profile)
        {
            var acc = new Vector3[Size * Size];
            var any = false;

            foreach (var layer in profile.Layers)
            {
                var path = profile.StarDataPath(
                    "Assets/scriptable_objects/star_data_scriptabe_objects", layer.Id);
                var data = AssetDatabase.LoadAssetAtPath<StarsData>(path);
                if (data == null || data.stars == null || data.stars.Length == 0)
                {
                    continue;
                }

                any = true;
                Plot(acc, data.stars, profile, layer);
            }

            return any ? acc : null;
        }

        private static bool Render(GalaxyProfile profile)
        {
            var acc = Accumulate(profile);
            if (acc == null)
            {
                Debug.LogWarning($"GalaxyPreview: '{profile.Id}' has no baked star data; build it first.");
                return false;
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var pixels = new Color32[Size * Size];
            for (var i = 0; i < acc.Length; i++)
            {
                // Reinhard, so bright cores roll off instead of clipping, then a gentle gamma so the faint
                // outer arms are actually visible on a monitor.
                var v = acc[i];
                var r = Mathf.Pow(v.x / (1f + v.x), 0.75f);
                var g = Mathf.Pow(v.y / (1f + v.y), 0.75f);
                var b = Mathf.Pow(v.z / (1f + v.z), 0.75f);
                pixels[i] = new Color32(
                    (byte)(Mathf.Clamp01(r) * 255f),
                    (byte)(Mathf.Clamp01(g) * 255f),
                    (byte)(Mathf.Clamp01(b) * 255f), 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            var file = $"{OutputFolder}/{profile.Id}.png";
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log($"GalaxyPreview: {profile.Id} -> {file}  " +
                      $"({profile.ArmCount} arms, pitch {profile.PitchAngleDegrees:0.0} deg, " +
                      $"winding {profile.WindingDegrees:0} deg, {profile.TotalPoints:N0} points)");
            return true;
        }

        /// <summary>
        /// The position maths, copied deliberately rather than shared: it mirrors
        /// <c>ComputeStarPosition</c> in <c>StarPositionCompute.cginc</c> line for line, with <c>_Age</c> at
        /// zero. If that shader changes, this goes stale and the comment is the only thing that will say so.
        /// </summary>
        private static void Plot(Vector3[] acc, StarVertDescriptor[] stars, GalaxyProfile profile,
                                 GalaxyLayerSpec layer)
        {
            var xRadii = profile.XRadii;
            var zRadii = profile.ZRadii;

            // The galaxy's own envelope is about 1 unit; a little margin keeps the rim off the edge.
            const float half = 1.15f;
            var scale = Size / (2f * half);
            var centre = Size * 0.5f;

            foreach (var star in stars)
            {
                var curve = star.curveOffset;
                var x = Mathf.Cos(curve) * xRadii;
                var z = Mathf.Sin(curve) * zRadii;

                var cos = Mathf.Cos(star.ellipseOffset);
                var sin = Mathf.Sin(star.ellipseOffset);
                var zp = z * cos - x * sin;
                var xp = z * sin + x * cos;

                x = xp * star.ellipseDistance;
                z = zp * star.ellipseDistance;

                var px = Mathf.RoundToInt(centre + x * scale);
                var py = Mathf.RoundToInt(centre + z * scale);
                if (px < 1 || py < 1 || px >= Size - 1 || py >= Size - 1)
                {
                    continue;
                }

                // The dust layer removes light in the real shader. Plotting it as a dark dot is the closest
                // honest thing a plain additive preview can do, and it is why the lanes read at all here.
                var weight = layer.IsShadow ? -0.5f : 1f;
                var c = new Vector3(star.color.x, star.color.y, star.color.z) * (weight * 0.45f);

                // A one-pixel dot loses the faint outer disc entirely, so spread a little by star size.
                var radius = star.size > 0.02f ? 1 : 0;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var i = (py + dy) * Size + (px + dx);
                        var falloff = dx == 0 && dy == 0 ? 1f : 0.35f;
                        acc[i] += c * falloff;
                        if (acc[i].x < 0f) { acc[i].x = 0f; }
                        if (acc[i].y < 0f) { acc[i].y = 0f; }
                        if (acc[i].z < 0f) { acc[i].z = 0f; }
                    }
                }
            }
        }
    }
}
