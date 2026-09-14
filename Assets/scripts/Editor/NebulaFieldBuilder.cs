// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Bakes each nebula into a 3D density texture, for the raymarched field that draws it as gas rather than
    /// as points.
    ///
    /// <para><b>Why a field at all, when the points are already there.</b> Points thin out: the same count
    /// through a bigger volume is a sparser volume, which is why every attempt to make the nebula larger made
    /// it emptier. A field has no count. The same texture marched over 9 m or 90 m is the same cost and the
    /// same look, and it is the only representation here that <b>occludes</b> - gas in front genuinely dimming
    /// gas behind - because the march carries transmittance. Points can add light; they cannot take it away.
    /// </para>
    ///
    /// <para><b>What goes in a voxel.</b> RGB is the plate's own colour at that place, as everywhere else in
    /// this feature; A is density. Density comes from the same two things the point bake uses - the plate's
    /// brightness for the two dimensions a photograph has, and the object's known shape for the one it does
    /// not - times an FBM noise so the cloud has structure between the pixels of a 512-wide plate. Without
    /// that noise a marched field is visibly smooth and reads as fog rather than as gas.</para>
    ///
    /// <para>64 cubed is deliberate: 1 MB at RGBA32 and it holds every feature a 512 plate can justify once
    /// the shape model has spread it through depth. 128 cubed is 8 MB per nebula and seven of those is a
    /// number the device will notice.</para>
    /// </summary>
    public static class NebulaFieldBuilder
    {
        private const string Folder = "Assets/data/nebula_fields";
        private const int Size = 64;
        private const string NodeName = "nebula_field";
        private const string StarNodeName = "nebula_star";

        /// <summary>
        /// The two-colour palette each nebula is lit with, plus the three structure knobs that decide how much
        /// of its volume is empty.
        ///
        /// <para>Every emission nebula has this structure for the same physical reason: the ionising stars are
        /// inside it, so the gas near them is hot and shows its high-excitation lines - blue and blue-green -
        /// while the gas further out is cooler and recombining, which is red. Painting that ramp explicitly is
        /// what makes a marched field read as a nebula rather than as coloured fog, and it is the single
        /// biggest difference between this and the reference art.</para>
        ///
        /// <para>The plate's own colour is still underneath all of this; <c>colourMix</c> in the component
        /// decides how much the ramp wins.</para>
        /// </summary>
        private readonly struct Palette
        {
            public readonly Color Core;
            public readonly Color Shell;
            public readonly float Floor;
            public readonly float Contrast;
            public readonly float Warp;

            /// <summary>
            /// The central star. Zero intensity means the object has no single dominant source worth drawing -
            /// a star-forming region has a cluster rather than one star, and a fake one in the middle of it
            /// would be an invention rather than a simplification.
            /// </summary>
            public readonly Color StarColour;
            public readonly float StarIntensity;
            public readonly float StarSize;

            /// <summary>
            /// How many stars light this object. One for a planetary nebula or a remnant, which has a single
            /// exposed core; several for an H II region, which is lit by a cluster; none where the source is
            /// outside the object altogether, as it is for the Pillars.
            /// </summary>
            public readonly int StarCount;

            public Palette(Color core, Color shell, float floor, float contrast, float warp,
                Color starColour, float starIntensity, float starSize, int starCount = 1)
            {
                StarCount = starCount;
                Core = core;
                Shell = shell;
                Floor = floor;
                Contrast = contrast;
                Warp = warp;
                StarColour = starColour;
                StarIntensity = starIntensity;
                StarSize = starSize;
            }
        }

        private static readonly Dictionary<string, Palette> Palettes = new Dictionary<string, Palette>
        {
            // Crab. The interior is the one case on this list that is NOT line emission: it is synchrotron
            // continuum from the pulsar wind, broadband and near-white with a lavender cast, which is why it
            // has no particular colour of its own. The cage around it is Ha with [N II] sitting on top of it,
            // which is the red-orange. Getting the interior wrong - making it a saturated blue like an [O III]
            // nebula - is the difference between the Crab and a generic planetary.
            //
            // The pulsar is magnitude 16.5 and visually insignificant. It is there because it is the reason
            // the object glows, not because you would notice it; a beacon in the middle would be wrong.
            ["crab"] = new Palette(new Color(0.78f, 0.82f, 1.05f), new Color(1.65f, 0.52f, 0.24f),
                0.26f, 2.8f, 0.85f, new Color(0.82f, 0.88f, 1.00f), 2.5f, 0.7f),

            // Helix. Textbook ionisation stratification, and one of only two here whose famous images are
            // close to true colour: [O III] teal in the cavity, Ha and [N II] red further out.
            ["helix"] = new Palette(new Color(0.30f, 1.20f, 1.10f), new Color(1.55f, 0.32f, 0.28f),
                0.24f, 2.6f, 0.70f, new Color(0.88f, 0.94f, 1.00f), 4.5f, 0.95f),

            // NGC 1501 is [O III] almost throughout, with barely any stratification - a turquoise blistered
            // shell with only a faint warm rim. The famous Hubble image shows its central star as an orange
            // pearl, which is a filter-mapping artefact: CH Cam is a Wolf-Rayet-type core and genuinely
            // blue-white, so that is what it gets here.
            ["ngc1501"] = new Palette(new Color(0.34f, 1.10f, 1.20f), new Color(1.00f, 0.78f, 0.66f),
                0.22f, 2.4f, 0.70f, new Color(0.85f, 0.92f, 1.00f), 5f, 0.9f),

            // The Homunculus is a reflection nebula, not an emission one: nearly all of its light is Eta
            // Carinae's own, scattered off the dust it threw out in the 1840s and reddened by it. Hence warm
            // tan lobes rather than ionised colours, with genuine red [N II] only in the outer ejecta. The
            // star is around five million solar luminosities and utterly dominates its own nebula.
            ["homunculus"] = new Palette(new Color(1.25f, 1.02f, 0.86f), new Color(1.55f, 0.42f, 0.30f),
                0.11f, 1.9f, 0.65f, new Color(1.00f, 0.90f, 0.96f), 7.5f, 1.15f),

            // Orion is a blister on the near face of a molecular cloud rather than a shell, and its core is
            // whitish teal-green - [O III] and H-beta and continuum together, bright enough that the eye
            // actually sees colour in it. The wings are pink rather than red, because Ha is mixed with blue
            // H-beta. Lit by the Trapezium: four hot stars, not one, so four is what gets built.
            ["orion"] = new Palette(new Color(0.88f, 1.08f, 0.96f), new Color(1.42f, 0.64f, 0.70f),
                0.29f, 2.9f, 0.90f, new Color(0.78f, 0.86f, 1.00f), 4.5f, 0.75f, 4),

            // The Pillars are the one object where truth and expectation actively conflict. The image everyone
            // knows is the Hubble palette - [S II] to red, Ha to green, [O III] to blue - which makes gold
            // pillars against a blue-teal ground. In true colour they are opaque brown dust columns against a
            // flat red Ha field. Shipping the palette everyone recognises, because a physically honest version
            // of this one reads as broken to almost every viewer.
            //
            // No central star, and that is not an omission: the Pillars are lit by NGC 6611 from off-frame
            // above, and they exist BECAUSE that light is directional. A star in the middle of them would
            // contradict the only thing about them that makes physical sense.
            ["pillars"] = new Palette(new Color(1.38f, 1.06f, 0.52f), new Color(0.34f, 0.86f, 1.10f),
                0.06f, 1.7f, 0.95f, Color.white, 0f, 0f, 0),

            // Trumpler 14 is a cluster first and a nebula second: half a million years old, one of the densest
            // concentrations of hot massive stars in the galaxy. Blue-white starlight over hard-ionised teal
            // gas inside, red-pink Ha further out. Eight stars rather than one, because no single member
            // dominates the way Eta Carinae does.
            ["trumpler14"] = new Palette(new Color(0.58f, 1.02f, 1.32f), new Color(1.42f, 0.56f, 0.52f),
                0.22f, 2.5f, 0.90f, new Color(0.74f, 0.84f, 1.00f), 5f, 0.62f, 8),
        };

        private static Palette PaletteFor(string id) =>
            Palettes.TryGetValue(id, out var palette)
                ? palette
                : new Palette(new Color(0.6f, 0.8f, 1.5f), new Color(1.4f, 0.5f, 0.3f), 0.22f, 2.4f, 0.75f,
                    Color.white, 0f, 0f);

        [MenuItem("Cosmic Simulation/Build Nebula Fields")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var built = 0;
            var report = new System.Text.StringBuilder();

            foreach (var spec in NebulaVolumeBuilder.AllSpecs)
            {
                var plate = NebulaVolumeBuilder.ReadPlate(spec.PlatePath, out var error);
                if (plate == null)
                {
                    report.AppendLine($"  {spec.Id}: {error}");
                    continue;
                }

                try
                {
                    var texture = Bake(spec, plate);
                    var path = $"{Folder}/nebula_field_{spec.Id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(texture, existing);
                        Object.DestroyImmediate(texture);
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(texture, path);
                    }

                    built++;

                    var baked = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    var attached = Attach(spec, baked);
                    report.AppendLine($"  {spec.Id}: {Size}^3 field, {spec.Model} model, {attached}");
                }
                finally
                {
                    Object.DestroyImmediate(plate);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"NebulaFieldBuilder: {built} field(s) baked at {Size} cubed, {Size * Size * Size * 4 / 1024 / 1024f:0.0} MB each.\n{report}");
        }

        /// <summary>
        /// Puts the baked field on the nebula's prefab, and turns the point gas off where it does.
        ///
        /// <para><b>Why the point layers go.</b> They were three attempts at the same job - continuous gas -
        /// and a sum of sprites cannot do it: however soft and dim the sprite, a few thousand of them read as
        /// a sponge, which is what every look at this has said. The field does that job properly, because it
        /// is continuous and it occludes. What the points are genuinely good at is the thing a field at 64
        /// cubed cannot do at all - individual, resolvable pinpoints - so the stars layer stays and the gas
        /// and dust layers are switched off rather than deleted, which keeps them one click away.</para>
        /// </summary>
        private static string Attach(NebulaVolumeBuilder.Spec spec, Texture3D baked)
        {
            if (baked == null)
            {
                return "not attached (the baked asset would not load)";
            }

            if (!NebulaVolumeBuilder.Destinations().TryGetValue(spec.Id, out var module) || module == null)
            {
                return "not attached (no destination module)";
            }

            if (module.ContentPrefab == null)
            {
                return "not attached (module has no ContentPrefab)";
            }

            var path = AssetDatabase.GetAssetPath(module.ContentPrefab);
            if (string.IsNullOrEmpty(path))
            {
                return "not attached (ContentPrefab is not an asset)";
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // Replace by name, so running this twice does not stack two fields on top of each other.
                var existing = root.transform.Find(NodeName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var node = new GameObject(NodeName);
                node.transform.SetParent(root.transform, false);

                // Inactive while it is being assembled, and only switched on once it has its volume.
                //
                // NebulaField is ExecuteAlways and its OnEnable disables the component when it has nothing to
                // march - a reasonable guard that becomes a trap here, because AddComponent raises OnEnable
                // immediately, before Configure has handed it the texture. The component switched itself off
                // and that "off" was what SaveAsPrefabAsset then wrote into the prefab, so every field shipped
                // disabled and the nebula rendered as an empty frame with the point gas already turned off.
                node.SetActive(false);

                var palette = PaletteFor(spec.Id);
                var field = node.AddComponent<NebulaField>();
                field.Configure(baked, spec.RadiusMetres, palette.Core, palette.Shell,
                    palette.Floor, palette.Contrast, palette.Warp);

                // The field has to sit where the gas sits, and the gas centres itself on the player when the
                // place opens. Same component, same reason: otherwise the player stands beside the nebula.
                if (node.GetComponent<CentreOnViewer>() == null)
                {
                    node.AddComponent<CentreOnViewer>();
                }

                // The central star, where the object has one. Same inactive-while-assembling dance, same
                // reason: ExecuteAlways components raise OnEnable the instant AddComponent runs.
                var starNode = root.transform.Find(StarNodeName);
                if (starNode != null)
                {
                    Object.DestroyImmediate(starNode.gameObject);
                }

                var star = "no star, lit from outside";
                if (palette.StarIntensity > 0f && palette.StarCount > 0)
                {
                    // The whole cluster hangs off one node, so it centres on the player once rather than
                    // each member doing it separately and drifting apart.
                    var cluster = new GameObject(StarNodeName);
                    cluster.transform.SetParent(root.transform, false);
                    cluster.SetActive(false);

                    // Authored at the same offset CentreOnViewer will apply in play. CentreOnViewer is a
                    // coroutine with no ExecuteAlways, so it does nothing in the editor - without this the
                    // seven test scenes show the star sitting inside the camera while the built app shows it
                    // correctly ahead, and the scenes are the thing anyone actually looks at.
                    cluster.transform.localPosition = Vector3.forward * (spec.RadiusMetres * 0.55f);

                    // Ahead of the player, not on top of them.
                    //
                    // The gas centres on the viewer so they are standing inside it, and for a while the star
                    // did too - which put a flare drawn metres wide about a metre from the eye. Measured, that
                    // was Orion at a mean of 159/255 with 91% of the frame near-white: not a nebula, a lamp
                    // pressed against the face. The player is standing off-centre in the cloud, which is both
                    // the sane composition and the only one where a central star is a thing you look AT.
                    var centring = cluster.GetComponent<CentreOnViewer>();
                    if (centring == null)
                    {
                        centring = cluster.AddComponent<CentreOnViewer>();
                    }

                    var centringProperties = new SerializedObject(centring);
                    centringProperties.FindProperty("aheadMetres").floatValue = spec.RadiusMetres * 0.55f;
                    centringProperties.ApplyModifiedPropertiesWithoutUndo();

                    // Seeded by the object's own id, so a rebuild puts the same stars in the same places.
                    var random = new System.Random(spec.Id.GetHashCode());

                    for (var i = 0; i < palette.StarCount; i++)
                    {
                        var member = new GameObject($"{StarNodeName}_{i}");
                        member.transform.SetParent(cluster.transform, false);

                        if (palette.StarCount > 1)
                        {
                            // A cluster is small compared with the nebula it has cleared - the Trapezium is
                            // well under a light year across inside an object tens of light years wide - so
                            // these sit in a tight knot near the middle rather than scattered through the gas.
                            var spread = spec.RadiusMetres * 0.10f;
                            member.transform.localPosition = new Vector3(
                                (float)(random.NextDouble() * 2.0 - 1.0),
                                (float)(random.NextDouble() * 2.0 - 1.0) * 0.6f,
                                (float)(random.NextDouble() * 2.0 - 1.0)) * spread;
                        }

                        var component = member.AddComponent<NebulaStar>();

                        // Members of a real cluster are not identical. Varying brightness and size a little
                        // is what stops eight stars reading as one object drawn eight times.
                        var vary = 0.65f + (float)random.NextDouble() * 0.7f;
                        component.Configure(palette.StarColour,
                            palette.StarIntensity * (palette.StarCount > 1 ? vary : 1f),
                            palette.StarSize * (palette.StarCount > 1 ? vary : 1f));
                        component.enabled = true;
                    }

                    cluster.SetActive(true);
                    star = palette.StarCount == 1
                        ? $"one star at {palette.StarIntensity:0} intensity"
                        : $"{palette.StarCount} stars at ~{palette.StarIntensity:0} intensity";
                }

                // Assembled. Now it may wake up, and OnEnable will find its volume where it expects it.
                field.enabled = true;
                node.SetActive(true);

                var silenced = 0;
                foreach (var volume in root.GetComponentsInChildren<NebulaVolume>(true))
                {
                    var name = volume.gameObject.name.ToLowerInvariant();
                    if (name.Contains("star"))
                    {
                        continue;
                    }

                    if (volume.gameObject.activeSelf)
                    {
                        volume.gameObject.SetActive(false);
                        silenced++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"field at {spec.RadiusMetres:0.0} m, {star}, {silenced} point gas layer(s) off";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Texture3D Bake(NebulaVolumeBuilder.Spec spec, Texture2D plate)
        {
            var pixels = plate.GetPixels();
            var plateSize = plate.width;

            var texture = new Texture3D(Size, Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };

            var voxels = new Color32[Size * Size * Size];

            // Density is accumulated as floats first and normalised at the end.
            //
            // It has to be. Density here is a plate luminance times a shape weight times a noise, and all
            // three are 0..1, so the product lands somewhere around a tenth however bright the object is.
            // Written straight into a byte that is a field whose values are all near zero - and the shader's
            // floor, which is what buys the black space, then subtracts more than the whole signal and the
            // nebula disappears. Measured: the first build of this rendered at a mean of 0.3/255. Normalising
            // makes the floor and contrast knobs mean the same thing for all seven objects, which is the only
            // way one palette table can describe them.
            var densities = new float[Size * Size * Size];
            var brightest = 0f;

            for (var z = 0; z < Size; z++)
            {
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        // -1..1 through the volume.
                        var u = (x + 0.5f) / Size * 2f - 1f;
                        var v = (y + 0.5f) / Size * 2f - 1f;
                        var w = (z + 0.5f) / Size * 2f - 1f;

                        var shape = NebulaVolumeBuilder.ShapeDensity(spec, u, v, w);
                        var index = z * Size * Size + y * Size + x;

                        if (shape <= 0f)
                        {
                            voxels[index] = new Color32(0, 0, 0, 0);
                            continue;
                        }

                        // The plate supplies colour and the two dimensions it has; the shape model supplies
                        // the third. Sampled at the voxel's own place in the image plane, so a voxel behind a
                        // bright filament is that filament's colour rather than an average of the whole plate.
                        var px = Mathf.Clamp(Mathf.RoundToInt((u * 0.5f + 0.5f) * (plateSize - 1)), 0, plateSize - 1);
                        var py = Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * (plateSize - 1)), 0, plateSize - 1);
                        var colour = pixels[py * plateSize + px];

                        var luminance = 0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b;

                        // Structure between the plate's own pixels. Three octaves is enough at 64 cubed; more
                        // would be detail the texture cannot hold.
                        var detail = Fbm(new Vector3(u, v, w) * 3.1f, 3);
                        var density = luminance * shape * Mathf.Lerp(0.45f, 1.35f, detail);

                        densities[index] = density;
                        if (density > brightest) brightest = density;

                        voxels[index] = new Color32(
                            (byte)(Mathf.Clamp01(colour.r) * 255f),
                            (byte)(Mathf.Clamp01(colour.g) * 255f),
                            (byte)(Mathf.Clamp01(colour.b) * 255f),
                            0);
                    }
                }
            }

            // Normalise against the densest voxel, so the field always uses the whole 0..1 range it is given.
            var scale = brightest > 1e-5f ? 1f / brightest : 0f;
            for (var i = 0; i < densities.Length; i++)
            {
                voxels[i].a = (byte)(Mathf.Clamp01(densities[i] * scale) * 255f);
            }

            texture.SetPixels32(voxels);
            texture.Apply(true);
            texture.name = $"nebula_field_{spec.Id}";
            return texture;
        }

        /// <summary>Value noise summed over octaves. Deterministic, and not worth a package dependency.</summary>
        private static float Fbm(Vector3 at, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var frequency = 1f;

            for (var i = 0; i < octaves; i++)
            {
                sum += amplitude * Noise(at * frequency);
                frequency *= 2.03f;
                amplitude *= 0.5f;
            }

            return Mathf.Clamp01(sum);
        }

        private static float Noise(Vector3 at)
        {
            // Unity's Perlin is 2D only, so three slices are combined - cheap, and at 64 cubed the seams a
            // purist would object to are below one voxel.
            var a = Mathf.PerlinNoise(at.x, at.y);
            var b = Mathf.PerlinNoise(at.y, at.z);
            var c = Mathf.PerlinNoise(at.z, at.x);
            return (a + b + c) / 3f;
        }
    }
}
