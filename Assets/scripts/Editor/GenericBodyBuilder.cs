// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes a body prefab for a body that has no art of its own: one sphere, one material, driven by the
    /// published quantities in <see cref="BodyAppearance"/>.
    ///
    /// <para><b>Why there is no per-exoplanet art, and never will be.</b> Our ten bodies each have a bespoke
    /// <c>poi_&lt;id&gt;_prefab</c> carrying real NASA/USGS surface imagery. <b>No exoplanet has an observed
    /// surface map.</b> None. Any terrain painted on one would be invented, and D-010 is explicit that a
    /// modelled appearance must be declared as modelled and never presented as observation. So the honest
    /// representation of an exoplanet is a featureless ball whose <i>colour and shading response</i> follow
    /// from measurements — the planet's composition class, the equilibrium temperature, the host star's
    /// effective temperature — and whose panel says that is what it is. A featureless ball is also, bluntly,
    /// the truth about how much we know: for the great majority of these worlds the measured quantities are a
    /// radius, a period, and sometimes a mass.</para>
    ///
    /// <para><b>What is measurement and what is convention.</b> Two things here are computed from published
    /// numbers by a citable method and are not anyone's taste: the <b>colour of the star's light</b>
    /// (<see cref="StarColor"/>) and the <b>visible-band brightness</b> at the planet. Everything else — which
    /// hue a rocky world gets, how hot a rocky world has to be before it glows, how dark a hot giant goes — is
    /// a <b>declared design convention</b>, written out in <see cref="Palette"/> in one place so that it can be
    /// argued with, and named as a convention in every report this builder prints. It is not dressed up as
    /// physics.</para>
    ///
    /// <para><b>Which shader, and why that one.</b> <c>Planets/Standard</c>
    /// (<c>Assets/shaders/planet_shader.shader</c>) — the same shader all ten of our own bodies use. It
    /// already carries the stereo macros and <c>#pragma target 4.5</c> the Quest build needs, it takes a
    /// <c>_SunDirection</c> that <c>SunLightReceiver</c> already drives, and its inputs line up one for one
    /// with what the parameter set produces: <c>_AlbedoMultiplier</c> takes the derived surface colour,
    /// <c>_LightTint</c> takes the star's computed colour, <c>_AmbientColor</c> takes the night-side level and
    /// <c>_SpecParams</c> the glint. Reusing it means a generic body sits in the same light as Earth beside it
    /// instead of looking like it came from a different app, and it means <b>no new shader to write, to fix
    /// for multiview, or to register under Always Included Shaders</b>.</para>
    ///
    /// <para><b>The two texture slots are left empty on purpose.</b> <c>_MainTex</c> unassigned falls back to
    /// the shader's <c>"white"</c> default, so the albedo is exactly <c>_AlbedoMultiplier</c> and the specular
    /// mask (which the shader reads from the albedo's alpha) is fully on, controlled by
    /// <c>_SpecParams.z</c>. <c>_NormalAlpha</c> unassigned is white too, which decodes to a normal of
    /// (1,1,1) — wrong — so <c>_NormalScale</c> is set to (0,0,1), which multiplies the tangent and binormal
    /// contributions out and leaves the shader using the interpolated geometric normal exactly. Note that this
    /// does <i>not</i> make the mesh's tangents irrelevant: the vertex shader normalises the tangent
    /// unconditionally, so a mesh with no tangent stream would produce a NaN that survives the multiply by
    /// zero. Hence the tangent check in <see cref="ResolveSphere"/>.</para>
    ///
    /// <para><b>Rings are not generated.</b> <see cref="BodyAppearance.RingOuterRadiusPlanetRadii"/> exists
    /// because Saturn's and Uranus's ring geometry is published and a future body in our own system might need
    /// it, but this builder will not draw a ring system from it, because for anything outside our own system
    /// there is no published ring geometry to draw — no exoplanet ring system has been measured. A ringed body
    /// therefore still comes from a hand-authored prefab, and <c>SolarRowBuilder.FindRings</c> keeps working
    /// on that unchanged. The builder says so rather than silently ignoring the field.</para>
    ///
    /// <para><b>The shape written</b> is deliberately the shape <see cref="SolarRowBuilder"/> already knows how
    /// to take apart, so that builder needs no branch for a generic body at all:
    /// <code>
    /// poi_&lt;id&gt;_prefab
    ///   &lt;id&gt;_tilt              localRotation = the obliquity, when it has been measured
    ///     axis_rotator              [ConstantRotateAxis] one turn per 60 s (120 s for a star), GDD 6.1
    ///       &lt;id&gt;_sphere_mesh   [MeshFilter, MeshRenderer, SunLightReceiver] the mesh the diameter is measured on
    /// </code>
    /// <c>SolarRowBuilder</c> finds <c>&lt;id&gt;_sphere_mesh</c>, walks up to <c>&lt;id&gt;_tilt</c>, lifts
    /// that subtree, normalises it to a 1 m diameter and puts the interaction surface on the body root — so a
    /// generic body is grabbed, pulled, scaled and placed by exactly the same components as Earth, and needs
    /// no desktop work either, since <c>DesktopMouseInput</c> drives those same components.</para>
    ///
    /// Menu: <b>Cosmic Simulation -> Build Generic Bodies</b> rebuilds every generic body of the
    /// <see cref="SystemProfile"/> selected in the Project window. <see cref="SolarRowBuilder"/> calls
    /// <see cref="EnsureBodyPrefab"/> for itself, so that menu item is for checking a body on its own.
    /// </summary>
    public static class GenericBodyBuilder
    {
        public const string PrefabFolder = "Assets/prefabs/generated_bodies";
        public const string MaterialFolder = "Assets/materials/generated";

        private const string ShaderPath = "Assets/shaders/planet_shader.shader";
        private const string ShaderName = "Planets/Standard";

        /// <summary>
        /// Where the sphere comes from, in preference order. The first is an upstream asset named for exactly
        /// this purpose and referenced by nothing else in the project; the others are the meshes two of our own
        /// bodies already ship, so a build never depends on a single file surviving.
        /// </summary>
        private static readonly string[] SphereCandidates =
        {
            "Assets/models/planet_unit_sphere_model.fbx",
            "Assets/models/venus_model.fbx",
            "Assets/models/mercury_model.fbx",
        };

        /// <summary>
        /// How far from a cube a candidate mesh's bounds may be and still be believed to be a sphere. A sphere
        /// is the one shape this may ever be, and a wrong mesh here would be normalised to a 1 m "diameter"
        /// that is not a diameter of anything.
        /// </summary>
        private const float SphereBoundsTolerance = 1.08f;

        /// <summary>GDD 6.1: "one visible turn per 60 s for planets, 120 s for the Sun".</summary>
        private const float PlanetTurnSeconds = 60f;
        private const float StarTurnSeconds = 120f;

        // ---------- the declared appearance convention
        //
        // Everything in this block is a design decision, not a measurement. It is gathered here so that a
        // reviewer can disagree with it in one place, and so the report can name it as a convention.

        private static class Palette
        {
            /// <summary>Regolith: the grey-tan of an airless rock at moderate temperature.</summary>
            public static readonly Color Rocky = new Color(0.541f, 0.514f, 0.471f, 1f);

            /// <summary>
            /// What a rocky world is warmed toward as its equilibrium temperature rises. Anchored on the fact
            /// that the hottest of these — 55 Cnc e at an equilibrium temperature around 2000 K — is a
            /// published lava world, not on any measured colour, because none has been measured.
            /// </summary>
            public static readonly Color Lava = new Color(1f, 0.478f, 0.235f, 1f);

            public const float LavaFromK = 1200f;
            public const float LavaFullK = 2200f;

            /// <summary>Pale methane cyan, the colour family of the two ice giants we can see.</summary>
            public static readonly Color IceGiant = new Color(0.561f, 0.749f, 0.847f, 1f);

            /// <summary>Warm banded tan, the colour family of the two gas giants we can see.</summary>
            public static readonly Color GasGiant = new Color(0.784f, 0.690f, 0.541f, 1f);

            /// <summary>
            /// What a gas giant is taken toward as it gets hot. The <i>direction</i> is sourced — the hot
            /// Jupiters with measured albedos are very dark, and the one with a measured colour, HD 189733 b,
            /// is a deep blue — but the exact hue is convention.
            /// </summary>
            public static readonly Color HotGiant = new Color(0.153f, 0.192f, 0.314f, 1f);

            public const float HotGiantFromK = 800f;
            public const float HotGiantFullK = 1600f;

            /// <summary>
            /// A body whose class could not be assigned, because its mass or its radius has not been measured.
            /// Deliberately a flat neutral grey: it should look like a placeholder, because it is one.
            /// </summary>
            public static readonly Color Unknown = new Color(0.604f, 0.604f, 0.604f, 1f);

            /// <summary>
            /// Published geometric albedos of the solar-system bodies each class is named for, used only to
            /// normalise a <i>measured</i> albedo into a lightness multiplier. Mercury 0.14 (rocky), Uranus and
            /// Neptune about 0.45 (ice giant), Jupiter and Saturn about 0.5 (gas giant).
            /// </summary>
            public static float ReferenceAlbedo(BodyClass bodyClass)
            {
                switch (bodyClass)
                {
                    case BodyClass.Rocky: return 0.14f;
                    case BodyClass.IceGiant: return 0.45f;
                    case BodyClass.GasGiant: return 0.50f;
                    default: return 0.30f;
                }
            }

            /// <summary>How far a measured albedo is allowed to push the lightness either way.</summary>
            public const float AlbedoFloor = 0.35f;
            public const float AlbedoCeiling = 1.6f;

            /// <summary>A glint, by class. Clouds do not glint; rock does a little; ice does more.</summary>
            public static float SpecularMask(BodyClass bodyClass, bool bright)
            {
                switch (bodyClass)
                {
                    case BodyClass.Rocky: return bright ? 0.35f : 0.15f;
                    case BodyClass.IceGiant:
                    case BodyClass.GasGiant: return 0f;
                    default: return 0.1f;
                }
            }
        }

        // ---------- the shading constants, lifted from a shipping body
        //
        // These are mercury_material's own values on the same shader, so a generic body sits in the same light
        // as the ten hand-authored ones rather than in a second, differently tuned lighting model. Only the
        // colours below are derived; these numbers are inherited on purpose.

        private static readonly Vector4 LightAmount = new Vector4(0.79f, 0f, 0f, 0f);
        private static readonly Vector4 LightGamma = new Vector4(1.2f, 0.8f, 1f, 1f);
        private static readonly Vector4 FresnelTerm = new Vector4(1.6f, 0.8f, 0f, 0f);
        private const float FresnelLitLevel = 0.48f;   // mercury_material's _FresnelColor against white
        private const float FresnelDarkLevel = 0.165f; // and its _FresnelDarkSideColor
        private const float SpecularPower = 40f;

        /// <summary>
        /// Night-side level, as a fraction of the body's own colour. The low end is a floor rather than a
        /// measurement: over passthrough a body whose dark side goes to black punches a hole in the room.
        /// The high end is mercury_material's 0.2.
        /// </summary>
        private const float AmbientFloor = 0.06f;
        private const float AmbientCeiling = 0.20f;

        // ---------- entry points

        [MenuItem("Cosmic Simulation/Build Generic Bodies")]
        public static void BuildSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("GenericBodyBuilder: leave play mode first.");
                return;
            }

            var profile = Selection.activeObject as SystemProfile;
            if (profile == null)
            {
                Debug.LogError("GenericBodyBuilder: select a SystemProfile in the Project window first " +
                               "(Assets/data/systems). Run Cosmic Simulation > Build System Profiles if there " +
                               "are none.");
                return;
            }

            var text = new StringBuilder();
            var written = 0;
            var failed = 0;

            foreach (var body in profile.Bodies ?? System.Array.Empty<SystemBody>())
            {
                if (body == null || string.IsNullOrEmpty(body.Id) || body.SourcePrefab != null)
                {
                    continue;
                }

                var prefab = EnsureBodyPrefab(body, out var note);
                text.AppendLine(prefab != null ? "  " + note : $"  {body.Id}: FAILED - {note}");
                if (prefab != null)
                {
                    written++;
                }
                else
                {
                    failed++;
                }
            }

            if (written == 0 && failed == 0)
            {
                Debug.Log($"GenericBodyBuilder: every body of '{profile.Id}' has a prefab of its own, so there " +
                          "is nothing generic to build.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var report = $"GenericBodyBuilder: {written} generic bodies written for '{profile.Id}' into " +
                         $"{PrefabFolder}" + (failed > 0 ? $", {failed} FAILED" : string.Empty) + $".\n{text}" +
                         "  Every colour above is a declared convention keyed to published quantities, not an " +
                         "observation (D-010). Each of these bodies' panel copy has to say so.";

            if (failed > 0)
            {
                Debug.LogError(report);
                return;
            }

            Debug.Log(report);
        }

        /// <summary>
        /// The prefab for a body with no art of its own, built or rebuilt in place so its GUID survives.
        /// Returns null and fills <paramref name="note"/> with the reason when it cannot be built.
        ///
        /// <para>Rebuilt on every call rather than reused when present: the appearance is a pure function of
        /// the profile, so a stale prefab is only ever a way for a profile edit to have no effect.</para>
        /// </summary>
        public static GameObject EnsureBodyPrefab(SystemBody body, out string note)
        {
            note = string.Empty;

            if (body == null || string.IsNullOrEmpty(body.Id))
            {
                note = "no id";
                return null;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath) ?? Shader.Find(ShaderName);
            if (shader == null)
            {
                note = $"'{ShaderPath}' is missing";
                return null;
            }

            var sphere = ResolveSphere(out var sphereNote);
            if (sphere == null)
            {
                note = sphereNote;
                return null;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var material = WriteMaterial(body, shader, out var recipe);

            var root = new GameObject($"poi_{body.Id}_prefab");
            var tilt = new GameObject($"{body.Id}_tilt");
            tilt.transform.SetParent(root.transform, false);

            // Obliquity is only measured inside our own system. Where it is not, this stays at zero rather
            // than picking a plausible-looking angle, which would be an invention nobody could later spot.
            tilt.transform.localRotation =
                Quaternion.Euler(0f, 0f, -body.Appearance.AxialTiltDegrees);

            var rotator = new GameObject("axis_rotator");
            rotator.transform.SetParent(tilt.transform, false);

            var turnSeconds = body.IsStar ? StarTurnSeconds : PlanetTurnSeconds;
            var spin = rotator.AddComponent<GalaxyExplorer.ConstantRotateAxis>();
            spin.axis = Vector3.up;

            // Positive is prograde about the body's own +Y, after the obliquity above. A body whose rotation
            // direction has not been measured still turns — a frozen ball reads as a broken one — and turns
            // prograde, which the report names so the panel copy can too.
            spin.speed = (body.Appearance.Spin == SpinDirection.Retrograde ? -1f : 1f) * 360f / turnSeconds;

            var meshNode = new GameObject($"{body.Id}_sphere_mesh");
            meshNode.transform.SetParent(rotator.transform, false);
            meshNode.AddComponent<MeshFilter>().sharedMesh = sphere;

            var renderer = meshNode.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            if (!body.IsStar)
            {
                // A star lights the others; pointing one at itself would be meaningless. UsePropertyBlock is
                // on because this material is an asset of ours that nothing instances — SolarRowBuilder strips
                // the Fader that used to do it — so the historical sharedMaterial write would leave a sun
                // direction in a .mat and therefore in a diff.
                var receiver = meshNode.AddComponent<GalaxyExplorer.SunLightReceiver>();
                receiver.UsePropertyBlock = true;
            }

            var path = $"{PrefabFolder}/poi_{body.Id}_prefab.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                note = $"could not save {path}";
                return null;
            }

            note = $"{body.Id}: {recipe} -> {path}";
            if (body.Appearance.RingOuterRadiusPlanetRadii > 0f)
            {
                note += $"\n    NO RINGS DRAWN: the profile gives a ring edge at " +
                        $"{body.Appearance.RingOuterRadiusPlanetRadii:F2} planet radii, but this builder does " +
                        "not generate ring geometry — outside our own system there is no published ring " +
                        "geometry to generate it from. Give the body a hand-authored prefab instead.";
            }

            return saved;
        }

        // ---------- the material

        private static Material WriteMaterial(SystemBody body, Shader shader, out string recipe)
        {
            var path = $"{MaterialFolder}/{body.Id}_surface_material.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var created = material == null;

            if (created)
            {
                material = new Material(shader) { name = $"{body.Id}_surface_material" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // Re-running is the point: an existing material is reset to the recipe, not left half tuned.
                material.shader = shader;
            }

            var appearance = body.Appearance;
            var starTemperature = appearance.IlluminantTemperatureK > 0f
                ? appearance.IlluminantTemperatureK
                : StarColor.SunEffectiveTemperatureK;
            var starLight = StarColor.Srgb(starTemperature);

            // Both texture slots stay empty; see the class comment. _NormalScale is what makes that safe.
            material.SetTexture("_MainTex", null);
            material.SetTexture("_NormalAlpha", null);
            material.SetVector("_NormalScale", new Vector4(0f, 0f, 1f, 0f));
            material.SetVector("_SunDirection", Vector4.zero);
            material.SetVector("_FresnelTerm", FresnelTerm);
            material.SetVector("_LightGammaCorrection", LightGamma);
            material.SetFloat("_TransitionAlpha", 1f);
            material.SetFloat("_SRCBLEND", 1f);
            material.SetFloat("_DSTBLEND", 0f);

            if (body.IsStar)
            {
                // A star is drawn as self-luminous: all of its colour arrives through _AmbientColor, which the
                // shader adds outside the sun term, so the ball reads evenly bright whichever way it faces and
                // does not need a second light source to make sense of it.
                material.SetColor("_AlbedoMultiplier", Color.white);
                material.SetColor("_AmbientColor", starLight);
                material.SetColor("_LightTint", Color.white);
                material.SetVector("_LightAmount", Vector4.zero);
                material.SetColor("_FresnelColor", starLight);
                material.SetColor("_FresnelDarkSideColor", starLight);
                material.SetVector("_SpecParams", new Vector4(SpecularPower, 0f, 0f, 0f));

                recipe = $"star, Teff {starTemperature:F0} K -> {StarColor.Hex(starLight)} " +
                         $"(visible band {StarColor.VisibleBandFraction(starTemperature) * 100f:F1}% of output, " +
                         $"the Sun's is {StarColor.VisibleBandFraction(StarColor.SunEffectiveTemperatureK) * 100f:F1}%), " +
                         $"one turn per {StarTurnSeconds:F0} s";
            }
            else
            {
                var surface = Surface(appearance);
                var ambientLevel = AmbientLevel(appearance);

                material.SetColor("_AlbedoMultiplier", surface);
                material.SetColor("_AmbientColor", surface * ambientLevel);
                material.SetColor("_LightTint", starLight);
                material.SetVector("_LightAmount", LightAmount);
                material.SetColor("_FresnelColor", surface * FresnelLitLevel);
                material.SetColor("_FresnelDarkSideColor", surface * FresnelDarkLevel);

                var bright = appearance.AlbedoMeasured &&
                             appearance.GeometricAlbedo > Palette.ReferenceAlbedo(appearance.Class);
                material.SetVector("_SpecParams",
                    new Vector4(SpecularPower, 0f, Palette.SpecularMask(appearance.Class, bright), 0f));

                var visible = StarColor.VisibleBrightnessEarth(starTemperature, appearance.InsolationEarth);
                recipe = $"{appearance.Class}, Teq " +
                         (appearance.EquilibriumTemperatureK > 0f
                             ? $"{appearance.EquilibriumTemperatureK:F0} K"
                             : "not available") +
                         $" -> {StarColor.Hex(surface)}" +
                         (appearance.AlbedoMeasured
                             ? $" (measured albedo {appearance.GeometricAlbedo:F2})"
                             : " (albedo not measured; class reference used)") +
                         $", lit {StarColor.Hex(starLight)} from a {starTemperature:F0} K star" +
                         (appearance.InsolationEarth > 0f
                             ? $", {appearance.InsolationEarth:F2}x Earth's insolation = {visible:F3}x Earth's " +
                               "visible light"
                             : ", insolation not available") +
                         $", ambient {ambientLevel:F2}" +
                         (appearance.Spin == SpinDirection.NotMeasured
                             ? ", ROTATION NOT MEASURED (shown turning prograde)"
                             : $", {appearance.Spin.ToString().ToLowerInvariant()}") +
                         (Mathf.Abs(appearance.AxialTiltDegrees) > 0.01f
                             ? $", obliquity {appearance.AxialTiltDegrees:F1} deg"
                             : ", obliquity not measured (upright)");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The declared surface colour: a class hue, warmed or darkened by the equilibrium temperature, scaled
        /// by a measured albedo where there is one. <b>A convention, not an observation</b> — the class and the
        /// temperature behind it are published, the hue is ours.
        /// </summary>
        public static Color Surface(BodyAppearance appearance)
        {
            var temperature = appearance.EquilibriumTemperatureK;
            Color colour;

            switch (appearance.Class)
            {
                case BodyClass.Rocky:
                    colour = Color.Lerp(Palette.Rocky, Palette.Lava,
                        Ramp(temperature, Palette.LavaFromK, Palette.LavaFullK));
                    break;

                case BodyClass.GasGiant:
                    colour = Color.Lerp(Palette.GasGiant, Palette.HotGiant,
                        Ramp(temperature, Palette.HotGiantFromK, Palette.HotGiantFullK));
                    break;

                case BodyClass.IceGiant:
                    // No warming ramp: a body this hot is not an ice giant, so a temperature that would warm
                    // one is a sign the class is wrong rather than something to render.
                    colour = Palette.IceGiant;
                    break;

                default:
                    colour = Palette.Unknown;
                    break;
            }

            if (appearance.AlbedoMeasured)
            {
                var reference = Palette.ReferenceAlbedo(appearance.Class);
                var multiplier = reference > 0f
                    ? Mathf.Clamp(appearance.GeometricAlbedo / reference, Palette.AlbedoFloor,
                                  Palette.AlbedoCeiling)
                    : 1f;
                colour = new Color(Mathf.Clamp01(colour.r * multiplier), Mathf.Clamp01(colour.g * multiplier),
                                   Mathf.Clamp01(colour.b * multiplier), 1f);
            }

            return colour;
        }

        /// <summary>
        /// The night-side level, between a passthrough floor and the level our own bodies use.
        ///
        /// <para>Keyed to the visible-band brightness at the planet rather than to the raw insolation, because
        /// the two differ by a factor of ten for a cool star. This is a <i>relative darkness cue</i>, not a
        /// claim about exposure: the app draws every body at a comparable exposure, exactly as every published
        /// image of a planet does, and the true brightness figure belongs in the panel copy where it can be
        /// stated rather than implied.</para>
        /// </summary>
        public static float AmbientLevel(BodyAppearance appearance)
        {
            var visible = StarColor.VisibleBrightnessEarth(
                appearance.IlluminantTemperatureK > 0f
                    ? appearance.IlluminantTemperatureK
                    : StarColor.SunEffectiveTemperatureK,
                appearance.InsolationEarth);

            if (visible <= 0f)
            {
                return AmbientCeiling; // nothing published: no reason to draw it darker than our own bodies
            }

            // Cube root, so the range from TRAPPIST-1 h (0.015) to 55 Cnc e (2413) reads as a difference at
            // all. A linear map would put every red-dwarf world on the floor together.
            var k = Mathf.Clamp01(Mathf.Pow(Mathf.Min(visible, 1f), 1f / 3f));
            return Mathf.Lerp(AmbientFloor, AmbientCeiling, k);
        }

        /// <summary>Zero below <paramref name="from"/>, one above <paramref name="to"/>, smooth between.</summary>
        private static float Ramp(float value, float from, float to)
        {
            if (value <= 0f || to <= from)
            {
                return 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        /// <summary>
        /// Creates the folder if it is not there, and only refreshes when it had to. A Refresh per body is
        /// harmless but slow, and this runs inside <see cref="SolarRowBuilder"/>'s own pass.
        /// </summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        // ---------- the sphere

        /// <summary>
        /// Resolved once per editor session. A destroyed asset reference compares equal to null, so a reimport
        /// of the model behind it simply sends this back through <see cref="ResolveSphere"/>.
        /// </summary>
        private static Mesh cachedSphere;

        /// <summary>
        /// The mesh every generic body is drawn on: the first candidate that is actually a sphere and carries
        /// the vertex streams <c>Planets/Standard</c> reads.
        ///
        /// <para>The checks are not ceremony. A mesh that is not a sphere would be normalised by
        /// <c>SolarRowBuilder</c> to a 1 m "diameter" that is not a diameter of anything, and every layout
        /// measurement downstream would be wrong by whatever its longest axis happened to be. A mesh with no
        /// tangent stream would make the shader's <c>normalize(v.tangent)</c> a NaN that propagates into the
        /// fragment colour whatever <c>_NormalScale</c> is, so where the importer can be told to calculate
        /// tangents, it is told, and the asset is reimported.</para>
        /// </summary>
        private static Mesh ResolveSphere(out string note)
        {
            if (cachedSphere != null)
            {
                note = $"sphere '{cachedSphere.name}'";
                return cachedSphere;
            }

            var tried = new List<string>();

            foreach (var path in SphereCandidates)
            {
                var mesh = BestSphereIn(path, out var why);
                if (mesh != null)
                {
                    cachedSphere = mesh;
                    note = $"sphere from {path} ('{mesh.name}')";
                    return mesh;
                }

                tried.Add($"{path}: {why}");
            }

            note = "no usable sphere mesh. Tried " + string.Join("; ", tried);
            return null;
        }

        private static Mesh BestSphereIn(string path, out string why)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null)
            {
                why = "not in the project";
                return null;
            }

            var mesh = LargestSphere(path);
            if (mesh == null)
            {
                why = "no mesh inside it has near-cubic bounds, so none of them is a sphere";
                return null;
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                why = $"'{mesh.name}' has no normals or no UVs";
                return null;
            }

            if (!mesh.HasVertexAttribute(VertexAttribute.Tangent))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    why = $"'{mesh.name}' has no tangents and the asset is not a model import, so none can " +
                          "be calculated";
                    return null;
                }

                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.SaveAndReimport();
                Debug.Log($"GenericBodyBuilder: {path} imported no tangents, which would make " +
                          $"{ShaderName}'s vertex shader produce a NaN. Its importer is now set to calculate " +
                          "them and the asset has been reimported.");

                mesh = LargestSphere(path);
                if (mesh == null || !mesh.HasVertexAttribute(VertexAttribute.Tangent))
                {
                    why = "tangents could not be calculated even after a reimport";
                    return null;
                }
            }

            why = string.Empty;
            return mesh;
        }

        /// <summary>
        /// The mesh with the most vertices among those inside <paramref name="path"/> whose bounds are
        /// near-cubic. Most vertices rather than first, because a planet FBX carries a glow shell beside the
        /// body itself and both are spheres.
        /// </summary>
        private static Mesh LargestSphere(string path)
        {
            Mesh best = null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var mesh = asset as Mesh;
                if (mesh == null)
                {
                    continue;
                }

                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                var narrowest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (narrowest <= 0.0001f || widest / narrowest > SphereBoundsTolerance)
                {
                    continue;
                }

                if (best == null || mesh.vertexCount > best.vertexCount)
                {
                    best = mesh;
                }
            }

            return best;
        }
    }
}
