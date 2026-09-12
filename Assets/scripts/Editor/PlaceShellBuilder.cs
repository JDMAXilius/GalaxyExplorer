// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Attaches a surrounding <see cref="PlaceShell"/> to a destination, so the place is somewhere the player is
    /// standing in rather than an object they are holding.
    ///
    /// <para>It writes two materials per destination -- a sky and a gas layer, both on
    /// <c>CosmicSimulation/PlaceShell</c>, both fed from the destination's own plate -- and puts one
    /// <c>place_shell</c> child on the destination's <c>ContentPrefab</c> wired to them. Nothing else changes:
    /// the nebula card overlay in that prefab is left exactly as <see cref="NebulaPrefabBuilder"/> made it. See
    /// <see cref="PlaceShell"/> for how the two coexist and what the shell costs.</para>
    ///
    /// <para><b>All seven are wired.</b> <see cref="Wired"/> is the whole list. The mechanism is general --
    /// every destination already has a plate, and the numbers below are all derived from the plate rather than
    /// hand-tuned per nebula -- so a destination costs one line in that array. Orion was wired first, alone,
    /// because it is the best-known object in the set and its plate is clean, wide and bright enough to carry a
    /// whole sky; the rest followed when the owner asked for every nebula to be a place you go inside.</para>
    ///
    /// <para><b>The astronomical objection, recorded rather than acted on.</b> An earlier pass here argued that
    /// a shell answers "what would it look like to be inside this", which is a real question only for the big
    /// diffuse clouds you could actually be inside -- Orion, the Carina complex around Trumpler 14, the Pillars
    /// -- and close to meaningless for the compact ones. That objection is sound and still stands as physics:
    /// the Crab is a nine-light-year shell of debris seen from outside, NGC 1501 is a planetary nebula a third
    /// of a light year across, the Homunculus is two lobes of ejecta around one star, and the Helix is a shell
    /// we happen to see face-on. You could not stand in the middle of any of them and see what this draws.
    ///
    /// It is wired anyway, deliberately, because the owner asked for it after seeing Orion and because the
    /// question the shell answers here is not "where could you stand" but "what is this made of" -- being
    /// surrounded by the gas and colour of an object is a way of showing it, not a claim about travel. What
    /// that costs is honesty, and the cost is paid in the copy: every shelled destination carries a line in its
    /// panel saying the surroundings are an impression built from the photograph rather than a view from
    /// inside. D-010 requires that line; without it this would be presenting a construction as observation.
    /// <see cref="Report"/> fails a destination whose copy does not carry it, so the two cannot drift.</para>
    ///
    /// <para><b>Truthfulness (docs/decisions.md D-010).</b> The plate is a real photograph and is not altered:
    /// the sky layer shows it whole and the gas layer shows one luminance band of it. What is <i>modelled</i> is
    /// the arrangement -- the plate is projected onto a dome, a second copy of it is hung closer to the player to
    /// make parallax, and the star field is procedural, not a catalogue. None of that is a measurement, and a
    /// destination that gets a shell needs a line in its panel copy saying the surroundings are an impression
    /// built from the photograph rather than a view from inside the nebula. That copy lives in
    /// <c>docs/copy/</c> and is not this builder's to write; it is listed as needing coordination.</para>
    ///
    /// <para><b>Order.</b> Run <b>Build Nebula Prefabs</b> first and this second. That builder rebuilds each
    /// destination prefab from scratch, which throws the <c>place_shell</c> child away; this one only ever adds
    /// it back. Running this twice is a no-op -- every <see cref="PlaceShell"/> under the prefab is removed
    /// before one is added, and the materials are written in place -- so re-running it after a nebula rebuild is
    /// the fix, and the report says so.</para>
    ///
    /// <para><b>Why the sphere is generated instead of reusing
    /// <c>boundry_space_background_stars_model.fbx</c>.</b> That model was the obvious candidate and it does not
    /// work. Its one mesh is called <c>StarCards</c>, and it is what the name says: a cloud of separate camera-
    /// facing quads scattered over a sphere, each one mapping the whole of a small star sprite, tinted by vertex
    /// colour -- which is why <c>Playspace/Stars</c> alpha-blends it with <c>Cull Off</c> at queue Background and
    /// discards fragments per star by world position. There is no continuous surface and no continuous UV
    /// parameterisation, so there is nowhere to put a plate: a photograph mapped onto it would appear as a few
    /// thousand independent postage stamps. It is also a star field we do not want, since the shell computes its
    /// own. <see cref="ReportStarModel"/> measures this rather than asserting it, and prints the numbers in the
    /// build report so the judgement can be checked; if it is ever replaced by a real shell mesh the report will
    /// say so. The generated sphere costs nothing by comparison and carries exact equirectangular UVs, which is
    /// what lets the shader place the plate without a per-pixel <c>atan2</c>.</para>
    /// </summary>
    public static class PlaceShellBuilder
    {
        private const string DestinationFolder = "Assets/data/destinations";
        private const string TextureFolder = "Assets/Textures";
        private const string MaterialFolder = "Assets/materials/place_shells";
        private const string ShaderName = "CosmicSimulation/PlaceShell";
        private const string StarModelPath = "Assets/models/boundry_space_background_stars_model.fbx";

        /// <summary>The destinations that get a shell. All seven -- see the class note for what that costs.</summary>
        private static readonly string[] Wired =
        {
            "orion", "pillars", "trumpler14", "helix", "crab", "homunculus", "ngc1501",
        };

        /// <summary>
        /// The phrase every shelled destination's panel copy has to contain, so that nobody meets one of these
        /// skies believing it is a photograph taken from inside the cloud. Matched case-insensitively on this
        /// fragment rather than on a whole sentence, so the copy can be reworded without breaking the check.
        /// </summary>
        private const string ImpressionPhrase = "impression built from";

        /// <summary>
        /// Same table as <see cref="NebulaPrefabBuilder"/>'s, duplicated rather than shared because that one is
        /// private and this builder must not reach into it. Seven lines; if a plate ever moves, both move.
        /// </summary>
        private static readonly Dictionary<string, string> Plates = new Dictionary<string, string>
        {
            { "helix",      "Assets/Textures/nebulae/helix_texture.jpg" },
            { "orion",      "Assets/Textures/nebulae/orion_texture.jpg" },
            { "crab",       "Assets/Textures/nebulae/crab_texture.jpg" },
            { "homunculus", "Assets/Textures/nebulae/homunculus_texture.jpg" },
            { "pillars",    "Assets/Textures/pillars_texture.tga" },
            { "ngc1501",    "Assets/Textures/ngc1501_texture.jpg" },
            { "trumpler14", "Assets/Textures/trumpler_texture.jpg" },
        };

        // ---------- the recipe, in one place so it can be argued with
        //
        // Azimuth spread is the only number chosen by eye: 0.42 puts the plate across about 150 degrees, which
        // is wider than anyone's field of view, so the nebula fills the sky ahead without wrapping round the
        // back where it would obviously be a repeated photograph. The gas layer is given a wider 0.62 because it
        // is the thing you are supposed to be inside. Elevation spread is not chosen at all -- it is derived
        // from the plate's own aspect ratio, so nothing is stretched.

        private const float SkySpreadU = 0.42f;
        private const float GasSpreadU = 0.62f;
        private const float MaxSpreadV = 0.9f;   // beyond this the patch reaches the pinched poles

        private const int SkyQueue = 1900;       // behind all scene geometry
        private const int GasQueue = 2900;       // over the sky, under the nebula cards at 3000

        private const float SkyRadius = 60f;
        private const float GasRadius = 7f;

        private class Built
        {
            public string Id;
            public string PrefabPath;
            public string PlateName;
            public int PlateWidth;
            public int PlateHeight;
            public float SkySpreadV;
            public float GasSpreadV;
            public int Removed;
            public int Triangles;
        }

        [MenuItem("Cosmic Simulation/Attach Place Shells")]
        public static void AttachAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("PlaceShellBuilder: leave play mode first — this edits prefab and material assets.");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"PlaceShellBuilder: shader '{ShaderName}' not found. " +
                               "Assets/shaders/place_shell_shader.shader must import without errors first.");
                return;
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                Debug.LogError($"PlaceShellBuilder: shader '{ShaderName}' has compile errors. Nothing wired, " +
                               "rather than wrapping the player in a magenta sphere.");
                return;
            }

            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();

            var modules = LoadDestinations();
            var built = new List<Built>();
            var failed = new List<string>();

            foreach (var id in Wired)
            {
                if (!modules.TryGetValue(id, out var module))
                {
                    failed.Add($"{id}: no destination module with that Id in {DestinationFolder}");
                    continue;
                }

                if (!SaysItIsAnImpression(module))
                {
                    // Refused rather than warned. A shell that claims to be a photograph taken from inside the
                    // cloud is precisely what D-010 forbids, and a warning in a console nobody reads is not a
                    // disclosure. The fix is one sentence in docs/copy/nebulae.md and a re-run of Import Copy.
                    failed.Add($"{id}: its panel copy does not say the surroundings are an " +
                               $"\"{ImpressionPhrase} ...\" the photograph, so no shell was attached. " +
                               "Add that line in docs/copy/nebulae.md, run Import Copy, and run this again.");
                    continue;
                }

                var entry = Attach(module, shader, failed);
                if (entry != null)
                {
                    built.Add(entry);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report(built, failed);
        }

        /// <summary>
        /// Whether this destination's panel already tells the player that its surroundings are constructed.
        ///
        /// Looks through the whole panel - title, every paragraph and the instruction - rather than at a fixed
        /// paragraph index, so the disclosure can live wherever it reads best in the copy.
        /// </summary>
        private static bool SaysItIsAnImpression(ExperienceModule module)
        {
            if (module == null)
            {
                return false;
            }

            var panel = module.Panel;
            if (Contains(panel.Title) || Contains(panel.Instruction))
            {
                return true;
            }

            if (panel.Paragraphs != null)
            {
                foreach (var paragraph in panel.Paragraphs)
                {
                    if (Contains(paragraph))
                    {
                        return true;
                    }
                }
            }

            return false;

            bool Contains(string text) =>
                !string.IsNullOrEmpty(text) &&
                text.IndexOf(ImpressionPhrase, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [MenuItem("Cosmic Simulation/Remove Place Shells")]
        public static void RemoveAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("PlaceShellBuilder: leave play mode first.");
                return;
            }

            var removed = new List<string>();

            foreach (var module in LoadDestinations().Values)
            {
                if (module.ContentPrefab == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(module.ContentPrefab);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    continue;
                }

                try
                {
                    var count = Strip(root);
                    if (count > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path, out _);
                        removed.Add($"{module.Id} ({count})");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(removed.Count == 0
                ? "PlaceShellBuilder: no destination had a place shell on it; nothing changed."
                : "PlaceShellBuilder: removed the place shell from " + string.Join(", ", removed) +
                  ". The materials under " + MaterialFolder + " are left alone; delete them by hand if you " +
                  "want them gone.");
        }

        // ---------- one destination

        private static Built Attach(ExperienceModule module, Shader shader, List<string> failed)
        {
            var plate = FindPlate(module.Id);
            if (plate == null)
            {
                failed.Add($"{module.Id}: no plate texture (expected {Expected(module.Id)})");
                return null;
            }

            if (module.ContentPrefab == null)
            {
                failed.Add($"{module.Id}: the module has no ContentPrefab. Run Build Nebula Prefabs first.");
                return null;
            }

            var prefabPath = AssetDatabase.GetAssetPath(module.ContentPrefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                failed.Add($"{module.Id}: its ContentPrefab is not an asset on disk");
                return null;
            }

            // Elevation spread from the plate's own shape. The shader's azimuth range is 2*pi*spreadU and its
            // elevation range is pi*spreadV, so an unstretched plate needs spreadV = 2 * spreadU * height/width.
            var aspect = plate.height / Mathf.Max(1f, plate.width);
            var skySpreadV = Mathf.Min(MaxSpreadV, 2f * SkySpreadU * aspect);
            var gasSpreadV = Mathf.Min(MaxSpreadV, 2f * GasSpreadU * aspect);

            var sky = SkyMaterial(module.Id, plate, shader, skySpreadV);
            var gas = GasMaterial(module.Id, plate, shader, gasSpreadV);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                failed.Add($"{module.Id}: {prefabPath} would not open for editing");
                return null;
            }

            int removedCount;
            try
            {
                removedCount = Strip(root);

                var shell = new GameObject("place_shell");
                shell.transform.SetParent(root.transform, false);
                Configure(shell.AddComponent<PlaceShell>(), sky, gas);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (!saved)
                {
                    failed.Add($"{module.Id}: {prefabPath} would not save");
                    return null;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return new Built
            {
                Id = module.Id,
                PrefabPath = prefabPath,
                PlateName = plate.name,
                PlateWidth = plate.width,
                PlateHeight = plate.height,
                SkySpreadV = skySpreadV,
                GasSpreadV = gasSpreadV,
                Removed = removedCount,
                Triangles = 64 * 32 * 2 + 48 * 24 * 2,
            };
        }

        /// <summary>Every existing shell under the prefab, so a re-run cannot leave two. Returns how many went.</summary>
        private static int Strip(GameObject root)
        {
            var existing = root.GetComponentsInChildren<PlaceShell>(true);
            foreach (var shell in existing)
            {
                if (shell != null && shell.gameObject != root)
                {
                    Object.DestroyImmediate(shell.gameObject);
                }
            }

            return existing.Length;
        }

        private static void Configure(PlaceShell shell, Material sky, Material gas)
        {
            var so = new SerializedObject(shell);

            so.FindProperty("skyMaterial").objectReferenceValue = sky;
            so.FindProperty("gasMaterial").objectReferenceValue = gas;

            so.FindProperty("skyRadius").floatValue = SkyRadius;
            so.FindProperty("gasRadius").floatValue = GasRadius;
            so.FindProperty("skySegments").vector2IntValue = new Vector2Int(64, 32);
            so.FindProperty("gasSegments").vector2IntValue = new Vector2Int(48, 24);

            so.FindProperty("gasSpinDegreesPerSecond").floatValue = 0.35f;
            so.FindProperty("gasTiltDegrees").floatValue = 24f;
            so.FindProperty("gasFollowDeadzone").floatValue = 2f;
            so.FindProperty("gasFollowSpeed").floatValue = 0.6f;

            so.FindProperty("fadeInSeconds").floatValue = 1.2f;
            so.FindProperty("fadeOutSeconds").floatValue = 0.35f;
            so.FindProperty("faceInitialGaze").boolValue = true;
            so.FindProperty("forceFullBlack").boolValue = true;
            so.FindProperty("hideInPassthrough").boolValue = true;

            // Layer 0. Never POI (11) or ForceGrab (12): pointers look for those, and a sphere around the
            // player's head on one of them would swallow every ray in the scene. There is no collider either,
            // so in practice nothing can hit it at all.
            so.FindProperty("rendererLayer").intValue = 0;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- materials

        /// <summary>
        /// The backdrop. Opaque -- its base colour's alpha is 1 -- so it replaces the room outright, and the
        /// whole photograph is used rather than a band, because this is the layer that has to look like a sky.
        /// </summary>
        private static Material SkyMaterial(string id, Texture2D plate, Shader shader, float spreadV)
        {
            var material = Load($"{MaterialFolder}/place_shell_{id}_sky.mat", $"place_shell_{id}_sky", shader);

            material.mainTexture = plate;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;

            material.SetColor("_Color", Color.white);

            // Not pure black. A void with a trace of the nebula's own colour in it stops the unplated half of the
            // sky reading as a hole, and gives the star field something to sit against.
            material.SetColor("_BaseColor", new Color(0.012f, 0.014f, 0.024f, 1f));

            material.SetFloat("_SpreadU", SkySpreadU);
            material.SetFloat("_SpreadV", spreadV);
            material.SetFloat("_PlateYaw", 0f);
            material.SetFloat("_PlatePitch", 0f);
            material.SetFloat("_EdgeFeather", 0.45f);
            material.SetFloat("_PlateGain", 1.15f);
            material.SetFloat("_PlateOpacity", 1f);

            material.SetFloat("_BandWeight", 0f);      // the whole plate
            material.SetFloat("_BandLow", 0.06f);
            material.SetFloat("_BandHigh", 2f);
            material.SetFloat("_BandFeather", 0.12f);

            material.SetColor("_StarColor", new Color(0.85f, 0.9f, 1f, 1f));
            material.SetFloat("_StarDensity", 64f);
            material.SetFloat("_StarSize", 0.09f);     // about 3 px on a Quest 3 eye buffer; smaller shimmers
            material.SetFloat("_StarFill", 0.12f);     // ~2 800 stars over the whole sky
            material.SetFloat("_StarBrightness", 1f);
            material.SetFloat("_StarWarmth", 0.35f);

            material.SetFloat("_Fade", 1f);
            material.SetFloat("_AlphaClip", 0f);       // nothing to discard: this layer is opaque everywhere
            material.renderQueue = SkyQueue;

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The nebulosity you are inside. A band of the same plate, zoomed in so it is not a visible second copy
        /// of the backdrop, transparent, and hung on the sphere that does not follow the player's head.
        /// </summary>
        private static Material GasMaterial(string id, Texture2D plate, Shader shader, float spreadV)
        {
            var material = Load($"{MaterialFolder}/place_shell_{id}_gas.mat", $"place_shell_{id}_gas", shader);

            material.mainTexture = plate;

            // The central 55% of the plate stretched over a wider patch: about 1.8x the sky layer's scale, which
            // is enough that the two do not read as the same picture twice.
            material.mainTextureScale = new Vector2(0.55f, 0.55f);
            material.mainTextureOffset = new Vector2(0.225f, 0.225f);

            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.75f));
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));   // alpha 0: this layer adds, never fills

            material.SetFloat("_SpreadU", GasSpreadU);
            material.SetFloat("_SpreadV", spreadV);
            material.SetFloat("_PlateYaw", 0f);
            material.SetFloat("_PlatePitch", 0f);
            material.SetFloat("_EdgeFeather", 0.6f);
            material.SetFloat("_PlateGain", 1.4f);
            material.SetFloat("_PlateOpacity", 0.85f);

            // The middle of the histogram: not the photograph's black sky, which must never be drawn, and not the
            // blown-out core, which the backdrop already shows. What is left is the wispy structure, and that is
            // the only part of the picture worth having in front of the player's face.
            material.SetFloat("_BandWeight", 1f);
            material.SetFloat("_BandLow", 0.10f);
            material.SetFloat("_BandHigh", 0.55f);
            material.SetFloat("_BandFeather", 0.16f);

            material.SetFloat("_StarBrightness", 0f);  // one star field, on the backdrop where it belongs
            material.SetFloat("_StarFill", 0f);
            material.SetFloat("_StarDensity", 64f);
            material.SetFloat("_StarSize", 0.09f);
            material.SetFloat("_StarWarmth", 0f);
            material.SetColor("_StarColor", Color.white);

            material.SetFloat("_Fade", 1f);

            // The one real fill saving. Most of this sphere is rejected by the band above, and on a tile-based
            // GPU discarding those fragments skips the framebuffer read-modify-write that blending at alpha zero
            // would still have paid for.
            material.SetFloat("_AlphaClip", 0.012f);
            material.renderQueue = GasQueue;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Load(string path, string name, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        // ---------- lookups

        private static Dictionary<string, ExperienceModule> LoadDestinations()
        {
            return AssetDatabase.FindAssets("t:ExperienceModule", new[] { DestinationFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && m.Kind == ExperienceKind.Destination && !string.IsNullOrEmpty(m.Id))
                .GroupBy(m => m.Id)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static string Expected(string id) =>
            Plates.TryGetValue(id, out var path) ? path : $"a texture named after '{id}' under {TextureFolder}";

        private static Texture2D FindPlate(string id)
        {
            if (Plates.TryGetValue(id, out var path))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (direct != null)
                {
                    return direct;
                }
            }

            foreach (var guid in AssetDatabase.FindAssets($"{id} t:Texture2D", new[] { TextureFolder }))
            {
                var found = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---------- the report

        private static void Report(List<Built> built, List<string> failed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PlaceShellBuilder: {built.Count} of {Wired.Length} wired destinations given a shell.");

            foreach (var b in built)
            {
                sb.AppendLine($"  {b.Id}");
                sb.AppendLine($"    plate    {b.PlateName} {b.PlateWidth}x{b.PlateHeight}");
                sb.AppendLine($"    sky      r={SkyRadius} m, head-following, opaque, queue {SkyQueue}, " +
                              $"plate across {SkySpreadU * 360f:F0}x{b.SkySpreadV * 180f:F0} deg + procedural stars");
                sb.AppendLine($"    gas      r={GasRadius} m, room-fixed, banded, queue {GasQueue}, " +
                              $"plate across {GasSpreadU * 360f:F0}x{b.GasSpreadV * 180f:F0} deg, 1.8x zoom");
                sb.AppendLine($"    cost     2 draw calls, {b.Triangles} triangles " +
                              $"({b.Triangles / 2f / 400000f * 100f:F1}% of the 400 000-point budget), " +
                              "about 2.0x full-screen overdraw");
                sb.AppendLine($"    prefab   {b.PrefabPath}" +
                              (b.Removed > 0 ? $" (replaced {b.Removed} existing shell(s))" : " (new)"));
            }

            if (failed.Count > 0)
            {
                sb.AppendLine("  not wired:");
                foreach (var f in failed)
                {
                    sb.AppendLine($"    {f}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("  The nebula card overlay in each prefab is untouched: the cards are the object you " +
                          "pick up, the shell is the place you are in. A shell asks the room for FullBlack at " +
                          "runtime, which is what stops the dim quad (queue 3900) and the black halo being " +
                          "painted over the sky; ExperienceDirector puts the module's own Environment back when " +
                          "the destination closes.");
            sb.AppendLine("  Re-run this after any Build Nebula Prefabs — that builder rebuilds the destination " +
                          "prefabs from scratch and throws the place_shell child away.");
            sb.AppendLine();
            sb.Append(ReportStarModel());

            if (failed.Count > 0)
            {
                Debug.LogWarning(sb.ToString());
            }
            else
            {
                Debug.Log(sb.ToString());
            }
        }

        /// <summary>
        /// Measures <c>boundry_space_background_stars_model.fbx</c> and says whether it could carry a plate,
        /// rather than leaving that as an assertion in a comment. A shell needs a continuous surface with a
        /// continuous UV parameterisation; a cloud of star cards has neither, and the give-away is that almost
        /// every vertex sits on a corner of the unit UV square, because each card maps the whole sprite.
        /// </summary>
        private static string ReportStarModel()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(StarModelPath).OfType<Mesh>().FirstOrDefault();
            if (mesh == null)
            {
                return $"  {StarModelPath}: no readable mesh found, so the generated sphere is used.\n";
            }

            var uv = mesh.uv;
            var corners = 0;
            foreach (var t in uv)
            {
                var atU = t.x < 0.01f || t.x > 0.99f;
                var atV = t.y < 0.01f || t.y > 0.99f;
                if (atU && atV)
                {
                    corners++;
                }
            }

            var fraction = uv.Length == 0 ? 0f : (float)corners / uv.Length;
            var verdict = fraction > 0.9f
                ? "a cloud of separate cards, each mapping a whole sprite — there is no continuous surface to " +
                  "put a plate on, so the shell generates its own sphere"
                : "it may have a usable continuous parameterisation after all — worth re-testing as the shell mesh";

            return $"  {StarModelPath} ('{mesh.name}'): {mesh.vertexCount} verts, " +
                   $"{mesh.triangles.Length / 3} tris, {uv.Length} UVs, {fraction * 100f:F0}% of them on a " +
                   $"corner of the unit square. Verdict: {verdict}.\n";
        }
    }
}
