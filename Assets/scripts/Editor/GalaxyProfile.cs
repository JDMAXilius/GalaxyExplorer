// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Which generator draws a galaxy's points.
    ///
    /// <para>Only <see cref="GalaxyKind.SpiralArms"/> is implemented. The other two are declared rather than omitted
    /// because <see cref="GalaxyBuilder"/> has to be able to <i>refuse</i> a profile it cannot draw: a giant
    /// elliptical run through the spiral generator would come out as a disc with arms, which is a wrong picture
    /// of a real object and exactly the kind of thing D-010's truthfulness rule exists to stop. Adding one later
    /// is a new case in <c>GalaxyBuilder.Generate</c> and a new branch in
    /// <c>GalaxyBuilder.Shade</c> — nothing else in the pipeline is spiral-specific.</para>
    /// </summary>
    public enum GalaxyKind
    {
        /// <summary>Nested rotated ellipses, the <c>SpiralGalaxy</c> recipe. The only one that works today.</summary>
        SpiralArms,

        /// <summary>
        /// <b>Not implemented.</b> Would want a de Vaucouleurs / Sérsic radial profile and a triaxial envelope
        /// with no azimuthal structure at all — the ellipse-rotation trick has nothing to contribute.
        /// </summary>
        Elliptical,

        /// <summary>
        /// <b>Not implemented.</b> Would want clumped star-forming knots placed from a noise field rather than
        /// any analytic curve, which is a different generator again.
        /// </summary>
        Irregular,
    }

    /// <summary>Which layer of a galaxy this is. Decides colour, sprite size and atlas tile.</summary>
    public enum GalaxyLayerRole
    {
        /// <summary>The diffuse glow. Concentrated inwards, so it stacks up into the bright core.</summary>
        Clouds,

        /// <summary>The dust lane or ring, drawn with the darkening "negative" shader.</summary>
        Dust,

        /// <summary>The disc stars.</summary>
        Stars,
    }

    /// <summary>
    /// One <c>SpiralGalaxy</c> layer: what to bake and what to set on the component.
    ///
    /// <para><b>Point count is the frame budget.</b> <c>Ellipses * StarsPerEllipse * ArmCount</c> point sprites
    /// come out of this, each six vertices and two triangles, and they are all overlapping additive fill. See
    /// the budget note on <see cref="GalaxyBuilder"/>.</para>
    /// </summary>
    public struct GalaxyLayerSpec
    {
        public GalaxyLayerRole Role;

        /// <summary>Goes into the asset file names, so lower case and no spaces: "clouds", "dust", "stars".</summary>
        public string Id;

        public string ShaderPath;

        /// <summary>Fixed, so a re-run with no parameter change writes byte-identical assets.</summary>
        public int Seed;

        public int Ellipses;
        public int StarsPerEllipse;

        /// <summary>Radial extent, as a fraction of the galaxy radius.</summary>
        public float MinEllipseScale;

        public float MaxEllipseScale;

        /// <summary>Per-star jitter on the radius, so the ellipses do not read as rings.</summary>
        public Vector2 Fuzz;

        /// <summary>Half-thickness at the centre and at the rim, before <see cref="YRange"/>.</summary>
        public float ThicknessAtCentre;

        public float ThicknessAtRim;

        /// <summary>Vertical scale, in the same units as the radius (which runs to 1).</summary>
        public float YRange;

        /// <summary>Sprite size, before the object's world scale. Feeds <c>_WSScale</c>.</summary>
        public float WorldSpaceScale;

        public Color Tint;
        public float TintMultiplier;

        /// <summary>Only meaningful on the downscaled path, which this content does not use.</summary>
        public bool IsShadow;

        /// <summary>
        /// Draw order. <c>SpiralGalaxy.Start</c> waits this many frames before creating its <c>DrawStars</c>,
        /// and <c>DrawStars</c> attaches its command buffers in creation order, so this is literally which
        /// layer is painted first.
        /// </summary>
        public int DrawIndex;

        /// <summary>
        /// <b><see cref="GalaxyLayerRole.Dust"/> only.</b> Where along the radius the dust is densest, as a
        /// fraction of the layer's own radial progression (0 at <see cref="MinEllipseScale"/>, 1 at
        /// <see cref="MaxEllipseScale"/>), and the half-width of that Gaussian. A ring galaxy peaks partway out
        /// and fades at both edges; a galaxy whose dust runs all the way in wants the peak at 0 and a wide
        /// width. Ignored by the other two roles.
        /// </summary>
        public float DustRingPeak;

        public float DustRingWidth;
    }

    /// <summary>
    /// A galaxy's colour story: three centre-to-rim ramps, one per layer, plus the two accent colours that give
    /// the disc its texture. This is one of the five things a new galaxy needs from research (D-010), and the
    /// only one of them that is a judgement rather than a measurement — real galaxy imagery is almost always
    /// false-colour or stretched, so what is recorded here is "what an honest rendering of the published images
    /// looks like", and the panel copy has to say so where it matters.
    /// </summary>
    public sealed class GalaxyPalette
    {
        /// <summary>The disc stars, centre to rim.</summary>
        public (float t, Color c)[] Disc = Array.Empty<(float, Color)>();

        /// <summary>The bulge glow, centre to rim.</summary>
        public (float t, Color c)[] Clouds = Array.Empty<(float, Color)>();

        /// <summary>The dust, inner edge to outer.</summary>
        public (float t, Color c)[] Dust = Array.Empty<(float, Color)>();

        /// <summary>Ionised hydrogen: the warm knots that break an arm up.</summary>
        public Color HiiKnot = new Color(1.00f, 0.52f, 0.34f);

        /// <summary>Young blue stars, which is what makes outer arms read as blue at all.</summary>
        public Color BlueGiant = new Color(0.62f, 0.78f, 1.00f);

        public (float t, Color c)[] For(GalaxyLayerRole role)
        {
            switch (role)
            {
                case GalaxyLayerRole.Clouds: return Clouds;
                case GalaxyLayerRole.Dust: return Dust;
                default: return Disc;
            }
        }
    }

    /// <summary>
    /// Everything that differs between one generated galaxy and the next: its identity, the pose and size it is
    /// presented at, the shape of its arms, its three layers and its colours. <see cref="GalaxyBuilder"/> turns
    /// one of these into star-data assets, materials, a content prefab and a wired
    /// <c>ExperienceModule</c>, and knows nothing about which galaxy it is building.
    ///
    /// <para><b>Why a code table and not a ScriptableObject per galaxy.</b> The generated star data is a
    /// multi-megabyte YAML array that no human reviews; the only thing that makes it reviewable is that it is
    /// reproducible — re-running the builder on an unchanged repo must produce an empty diff, which is how we
    /// know a diff means something. If the parameters lived in an inspector-editable asset, that asset would be
    /// a second input to the bake that could be nudged without a code diff, and the committed star data would
    /// stop being derivable from the scripts alone. CLAUDE.md's "edit prefabs through reproducible scripts"
    /// rule is the same argument. It also means a new galaxy arrives as one reviewable block of numbers in a
    /// pull request rather than as a binary-ish asset. The cost is that a non-programmer cannot author a
    /// galaxy, which is the right trade here: the numbers come from a research document, not from
    /// eyeballing an inspector, and the research document is itself reviewed.</para>
    ///
    /// <para><b>Fields are the real data, internals are derived.</b> Published galaxy morphology comes as arm
    /// count, pitch angle, bulge-to-disc ratio, inclination and a colour story. Three of those are not what the
    /// generator wants, so the conversions live here as named static functions rather than in a researcher's
    /// head: <see cref="ArmSpacingDegrees"/>, <see cref="WindingFromPitchAngle"/> and
    /// <see cref="TiltForPresentedInclination"/>.</para>
    /// </summary>
    public sealed class GalaxyProfile
    {
        // ---------- identity

        /// <summary>
        /// Stable key. Goes into every generated file name and into the default module path, so: lower case,
        /// ASCII, underscores, no spaces. "andromeda", "whirlpool", "sombrero".
        /// </summary>
        public string Id;

        /// <summary>For the build log and for error messages. Not written to the module — copy is owned by
        /// <c>CopyImporter</c> and <c>docs/copy/</c>, and overwriting it here would silently undo an edit.</summary>
        public string DisplayName;

        public GalaxyKind Kind = GalaxyKind.SpiralArms;

        // ---------- how it is presented

        /// <summary>Width of the whole galaxy in the room. The galaxy node is scaled so this comes out exact.</summary>
        public float WidthMetres = 1.2f;

        /// <summary>Rotation of the disc plane about X, degrees.</summary>
        public float DiscTiltDegrees;

        /// <summary>
        /// Rotation about Y, degrees, applied after the tilt. Tilt alone leaves a disc close to face-on reading
        /// as a flat circle; the yaw is what foreshortens it into an oval. See
        /// <see cref="PresentedInclinationDegrees"/> for what the pair actually presents.
        ///
        /// <para><b>Not the catalogue's position angle</b>, although it is the nearest thing here. Position
        /// angle is a roll in the plane of the sky — a rotation about the <i>view</i> axis, which leaves the
        /// projected axis ratio alone. This is a rotation about the room's up axis, so it changes the
        /// foreshortening as well, which is why <see cref="TiltForPresentedInclination"/> has to take it as an
        /// argument. A true position angle would be the Z term of the Euler triple, and the pose does not use
        /// one deliberately: the player rotates the galaxy with two hands and nothing snaps back, so an
        /// authored roll would survive only until the first grab.</para>
        /// </summary>
        public float DiscYawDegrees;

        /// <summary>Two-handed scale range, in metres of galaxy width.</summary>
        public float MinScaleMetres = 0.5f;

        public float MaxScaleMetres = 3f;

        /// <summary>
        /// The grab sphere, as a fraction of the galaxy's own radius. 1 is right for anything the player stands
        /// outside of; it also has to stop short of the scene panel, which the director anchors 0.55 m to the
        /// right of the content root.
        /// </summary>
        public float GrabRadiusFraction = 1f;

        // ---------- the shape of the arms

        /// <summary>
        /// Ellipse semi-axes, shared by every layer so they describe one galaxy. The eccentricity <i>is</i> the
        /// mechanism that makes arms — nested ellipses rotated through the winding angle — so these must not go
        /// to 1:1 or every ellipse becomes the same circle and the spiral disappears. Also the only two
        /// generation-time numbers the shader still reads at run time (as <c>_EllipseSize.xy</c>), so they have
        /// to match what was baked or the galaxy changes shape.
        ///
        /// <para><b>Not the catalogue's minor/major axis ratio.</b> A galaxy's published <c>b/a</c> is the
        /// <i>projected</i> ratio of a round disc seen at an angle, and here that comes from
        /// <see cref="DiscTiltDegrees"/> and <see cref="DiscYawDegrees"/>. These two are the disc's
        /// <i>intrinsic</i> eccentricity in its own plane, which no catalogue publishes and which exists in
        /// this generator only because it is what curls the ellipses into arms. Setting them from a <c>b/a</c>
        /// would double-count the inclination and squash the galaxy twice.</para>
        /// </summary>
        public float XRadii = 0.8f;

        public float ZRadii = 1f;

        /// <summary>
        /// How many arms. A published datum. Arms are placed evenly around the disc, so this alone fixes
        /// <see cref="ArmSpacingDegrees"/>.
        /// </summary>
        public int ArmCount = 2;

        /// <summary>
        /// Total azimuth the arm sweeps between its inner and outer radius, in degrees — the generator's own
        /// parameter (<c>SpiralGalaxy.SpiralRotation</c>). Set it with
        /// <see cref="WindingFromPitchAngle"/> from the published pitch angle rather than by eye; see the note
        /// on that method for the exception.
        /// </summary>
        public float WindingDegrees = 370f;

        /// <summary>
        /// Turns of the whole galaxy per unit of time, through the shader's <c>_Age</c>. The Milky Way map uses
        /// 0.08; a single object the player is holding wants less, because nothing may look like it is drifting
        /// under a grab.
        /// </summary>
        public float VelocityMultiplier = 0.05f;

        // ---------- content

        /// <summary>In draw order.</summary>
        public GalaxyLayerSpec[] Layers = Array.Empty<GalaxyLayerSpec>();

        public GalaxyPalette Palette;

        // ---------- where the generated assets go

        /// <summary>
        /// The <c>ExperienceModule</c> whose <c>ContentPrefab</c> gets pointed at the generated prefab. Leave
        /// empty for the conventional <c>Assets/data/experiences/&lt;Id&gt;.asset</c>.
        /// </summary>
        public string ModulePathOverride;

        /// <summary>Leave empty for the conventional <c>&lt;Id&gt;_content_prefab</c>.</summary>
        public string PrefabNameOverride;

        // ---------- derived

        public string ModulePath => string.IsNullOrEmpty(ModulePathOverride)
            ? $"Assets/data/experiences/{Id}.asset"
            : ModulePathOverride;

        public string PrefabName => string.IsNullOrEmpty(PrefabNameOverride)
            ? $"{Id}_content_prefab"
            : PrefabNameOverride;

        /// <summary>The child that carries the tilt, the yaw and the fit-to-width scale.</summary>
        public string GalaxyNodeName => $"{Id}_galaxy";

        public string StarDataPath(string folder, string layerId) =>
            $"{folder}/star_data_{Id}_{layerId}_scriptable_object.asset";

        public string MaterialPath(string folder, string layerId) =>
            $"{folder}/{Id}_{layerId}_material.mat";

        /// <summary>
        /// Azimuth between one arm and the next, in degrees; arm <c>n</c> starts at <c>n</c> times this. Arms
        /// are evenly spaced, which is what "two-armed
        /// grand design" or "four-armed" means, so arm count is the only input. (A lopsided pair — the Milky
        /// Way's own map uses 95 deg for two arms — is not expressible, and deliberately so: lopsidedness is a
        /// look choice and this table holds published data.)
        /// </summary>
        public float ArmSpacingDegrees => ArmCount > 0 ? 360f / ArmCount : 0f;

        /// <summary>
        /// The pitch angle <see cref="WindingDegrees"/> corresponds to, measured over the disc layer's radial
        /// range — the inverse of <see cref="WindingFromPitchAngle"/>. Reported in the build log so a profile
        /// whose winding was set by hand can be compared against the literature. 0 if there is no usable disc
        /// layer.
        /// </summary>
        public float PitchAngleDegrees
        {
            get
            {
                if (!TryGetLayer(GalaxyLayerRole.Stars, out var disc))
                {
                    return 0f;
                }

                return PitchAngleFromWinding(WindingDegrees, disc.MinEllipseScale, disc.MaxEllipseScale);
            }
        }

        /// <summary>
        /// How far off face-on the disc presents, in degrees, to a player looking along the content root's Z
        /// axis. 0 is a flat circle, 90 is edge-on.
        ///
        /// <para>The disc normal starts at +Y. <c>Quaternion.Euler(tilt, yaw, 0)</c> applies X then Y, so the
        /// normal goes to <c>(sin(tilt) sin(yaw), cos(tilt), sin(tilt) cos(yaw))</c> and the component along Z
        /// — which is what decides the foreshortening — is <c>sin(tilt) cos(yaw)</c>.</para>
        /// </summary>
        public float PresentedInclinationDegrees =>
            PresentedInclination(DiscTiltDegrees, DiscYawDegrees);

        public bool TryGetLayer(GalaxyLayerRole role, out GalaxyLayerSpec spec)
        {
            if (Layers != null)
            {
                for (var i = 0; i < Layers.Length; i++)
                {
                    if (Layers[i].Role == role)
                    {
                        spec = Layers[i];
                        return true;
                    }
                }
            }

            spec = default;
            return false;
        }

        /// <summary>Total point sprites this profile bakes, across every layer.</summary>
        public int TotalPoints
        {
            get
            {
                var total = 0;
                if (Layers != null)
                {
                    foreach (var layer in Layers)
                    {
                        total += layer.Ellipses * layer.StarsPerEllipse * Mathf.Max(1, ArmCount);
                    }
                }

                return total;
            }
        }

        // ---------- the conversions from published data to generator parameters

        /// <summary>
        /// Published <b>pitch angle</b> to this generator's total winding, in degrees.
        ///
        /// <para>Pitch angle is the standard morphological measure: the angle between an arm and the circle
        /// through the same point, so a small angle is a tightly wound galaxy. It is defined for a logarithmic
        /// spiral <c>r = r0 exp(phi tan psi)</c>, on which it is constant; inverting that over a radial range
        /// gives the azimuth the arm has to sweep to get from the inner radius to the outer one:</para>
        ///
        /// <code>delta_phi = ln(outer / inner) / tan(psi)</code>
        ///
        /// <para>which is what this returns, in degrees, and what
        /// <c>SpiralGalaxy.SpiralRotation</c> means. <c>inner</c> and <c>outer</c> are the disc layer's
        /// <c>MinEllipseScale</c> and <c>MaxEllipseScale</c> — the arm's radial extent, not the galaxy's.</para>
        ///
        /// <para><b>The approximation, stated.</b> This generator spaces its ellipses linearly in radius and
        /// linearly in azimuth, so the curve it actually draws is an Archimedean spiral, whose pitch angle
        /// varies along the arm: steep near the centre, shallow at the rim. Fitting the logarithmic spiral
        /// through the two endpoints is the honest single-number reading of it — the arm crosses the published
        /// pitch angle somewhere in the middle of its length and is off by a few degrees at each end. Getting
        /// this exactly right would mean spacing the ellipses geometrically, which changes the radial density
        /// profile (and therefore where the light is) for a difference nobody can see at 1.2 m across.</para>
        /// </summary>
        /// <param name="pitchAngleDegrees">Published pitch angle, exclusive of 0 and 90.</param>
        /// <param name="innerRadiusFraction">The disc layer's <c>MinEllipseScale</c>. Must be above 0.</param>
        /// <param name="outerRadiusFraction">The disc layer's <c>MaxEllipseScale</c>. Must exceed the inner.</param>
        public static float WindingFromPitchAngle(
            float pitchAngleDegrees, float innerRadiusFraction, float outerRadiusFraction)
        {
            if (pitchAngleDegrees <= 0f || pitchAngleDegrees >= 90f)
            {
                Debug.LogError($"GalaxyProfile.WindingFromPitchAngle: pitch angle {pitchAngleDegrees} is not " +
                               "between 0 and 90 degrees, so there is no spiral to describe. Returned 0, which " +
                               "will make the builder refuse the profile.");
                return 0f;
            }

            if (innerRadiusFraction <= 0f || outerRadiusFraction <= innerRadiusFraction)
            {
                Debug.LogError($"GalaxyProfile.WindingFromPitchAngle: radial range {innerRadiusFraction} to " +
                               $"{outerRadiusFraction} is empty or starts at the centre, where a logarithmic " +
                               "spiral has swept infinite azimuth. Pass the disc layer's MinEllipseScale and " +
                               "MaxEllipseScale. Returned 0.");
                return 0f;
            }

            var sweepRadians = Mathf.Log(outerRadiusFraction / innerRadiusFraction)
                               / Mathf.Tan(pitchAngleDegrees * Mathf.Deg2Rad);

            return sweepRadians * Mathf.Rad2Deg;
        }

        /// <summary>The inverse of <see cref="WindingFromPitchAngle"/>, for reporting.</summary>
        public static float PitchAngleFromWinding(
            float windingDegrees, float innerRadiusFraction, float outerRadiusFraction)
        {
            if (windingDegrees <= 0f || innerRadiusFraction <= 0f || outerRadiusFraction <= innerRadiusFraction)
            {
                return 0f;
            }

            var sweepRadians = windingDegrees * Mathf.Deg2Rad;
            return Mathf.Atan2(Mathf.Log(outerRadiusFraction / innerRadiusFraction), sweepRadians) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Published <b>inclination</b> to the tilt that presents it, given a chosen yaw.
        ///
        /// <para>Inclination is one number and the pose has two degrees of freedom, so the yaw is the free
        /// choice: it is which way the disc's major axis runs across the player's view, a composition decision
        /// with no published answer. Pick the yaw for the look, then take the tilt from here. Inverts
        /// <see cref="PresentedInclination"/>: <c>sin(tilt) cos(yaw) = cos(inclination)</c>.</para>
        ///
        /// <para>Not every pair is reachable — a galaxy closer to face-on than the yaw itself needs a smaller
        /// yaw. That case is clamped to 90 degrees of tilt and logged, rather than returning something that
        /// silently presents the wrong inclination.</para>
        /// </summary>
        public static float TiltForPresentedInclination(float inclinationDegrees, float yawDegrees)
        {
            var alongView = Mathf.Cos(inclinationDegrees * Mathf.Deg2Rad);
            var yawCosine = Mathf.Cos(yawDegrees * Mathf.Deg2Rad);

            if (Mathf.Abs(yawCosine) < 0.0001f)
            {
                Debug.LogError($"GalaxyProfile.TiltForPresentedInclination: a yaw of {yawDegrees} degrees turns " +
                               "the disc's major axis along the view axis, where no tilt can present a given " +
                               "inclination. Returned 90 (edge-on).");
                return 90f;
            }

            var sine = alongView / yawCosine;
            if (Mathf.Abs(sine) > 1f)
            {
                Debug.LogError($"GalaxyProfile.TiltForPresentedInclination: {inclinationDegrees} degrees of " +
                               $"inclination cannot be presented at a yaw of {yawDegrees} degrees - the galaxy " +
                               "is closer to face-on than the yaw allows. Lower the yaw. Returned 90 (edge-on).");
                return 90f;
            }

            return Mathf.Asin(sine) * Mathf.Rad2Deg;
        }

        /// <summary>See <see cref="PresentedInclinationDegrees"/>.</summary>
        public static float PresentedInclination(float tiltDegrees, float yawDegrees)
        {
            var alongView = Mathf.Sin(tiltDegrees * Mathf.Deg2Rad) * Mathf.Cos(yawDegrees * Mathf.Deg2Rad);
            return Mathf.Acos(Mathf.Clamp01(Mathf.Abs(alongView))) * Mathf.Rad2Deg;
        }

        // ---------- validation

        /// <summary>
        /// Everything wrong with this profile, or an empty array. Run before anything is written: a galaxy that
        /// is half on disk is worse than one that was refused, because the refusal names the field.
        /// </summary>
        public string[] Problems()
        {
            var problems = new System.Collections.Generic.List<string>();

            if (string.IsNullOrEmpty(Id))
            {
                problems.Add("Id is empty; it is part of every generated file name.");
            }
            else if (Id.IndexOfAny(new[] { ' ', '/', '\\', '.' }) >= 0 || Id != Id.ToLowerInvariant())
            {
                problems.Add($"Id '{Id}' must be lower case with no spaces, dots or slashes: it becomes a file name.");
            }

            if (Kind != GalaxyKind.SpiralArms)
            {
                problems.Add($"Kind is {Kind}, which has no generator. Only {nameof(GalaxyKind.SpiralArms)} is " +
                             "implemented; see the GalaxyKind doc comment for what the other two would need. " +
                             "Running this profile through the spiral generator would draw arms on something " +
                             "that has none, so it is refused instead.");
            }

            if (Layers == null || Layers.Length == 0)
            {
                problems.Add("no layers, so there would be nothing to draw.");
            }
            else
            {
                var ids = new System.Collections.Generic.HashSet<string>();
                foreach (var layer in Layers)
                {
                    if (string.IsNullOrEmpty(layer.Id))
                    {
                        problems.Add($"a {layer.Role} layer has no Id; it is part of its asset file names.");
                    }
                    else if (!ids.Add(layer.Id))
                    {
                        problems.Add($"two layers share the Id '{layer.Id}', so they would overwrite each " +
                                     "other's star data and material.");
                    }

                    if (layer.Ellipses < 1 || layer.StarsPerEllipse < 1)
                    {
                        problems.Add($"layer '{layer.Id}' bakes {layer.Ellipses} x {layer.StarsPerEllipse} " +
                                     "points, which is nothing.");
                    }

                    if (layer.MaxEllipseScale <= 0f || layer.MaxEllipseScale < layer.MinEllipseScale)
                    {
                        problems.Add($"layer '{layer.Id}' has a radial range of {layer.MinEllipseScale} to " +
                                     $"{layer.MaxEllipseScale}, which is empty.");
                    }

                    if (string.IsNullOrEmpty(layer.ShaderPath))
                    {
                        problems.Add($"layer '{layer.Id}' has no ShaderPath.");
                    }
                }
            }

            if (ArmCount < 1)
            {
                problems.Add($"ArmCount is {ArmCount}.");
            }

            if (XRadii <= 0f || ZRadii <= 0f)
            {
                problems.Add($"ellipse semi-axes {XRadii} / {ZRadii} must both be above zero.");
            }
            else if (Mathf.Approximately(XRadii, ZRadii))
            {
                problems.Add($"XRadii and ZRadii are both {XRadii}, so every ellipse is the same circle and " +
                             "the spiral disappears entirely. They have to differ.");
            }

            if (WindingDegrees <= 0f)
            {
                problems.Add("WindingDegrees is zero or negative, so the arms do not wind. If it came from " +
                             $"{nameof(WindingFromPitchAngle)}, that call logged why it returned 0.");
            }

            if (WidthMetres <= 0f)
            {
                problems.Add($"WidthMetres is {WidthMetres}.");
            }

            if (MinScaleMetres <= 0f || MaxScaleMetres <= MinScaleMetres)
            {
                problems.Add($"the scale range {MinScaleMetres} to {MaxScaleMetres} m is empty.");
            }

            if (Palette == null)
            {
                problems.Add("no Palette.");
            }
            else if (Layers != null)
            {
                foreach (var layer in Layers)
                {
                    var ramp = Palette.For(layer.Role);
                    if (ramp == null || ramp.Length < 2)
                    {
                        problems.Add($"the palette's {layer.Role} ramp has fewer than two stops, so layer " +
                                     $"'{layer.Id}' would have no colour gradient.");
                    }
                }
            }

            return problems.ToArray();
        }
    }

    /// <summary>
    /// Every galaxy the builder knows how to make.
    ///
    /// <para><b>Andromeda is deliberately the only entry.</b> D-010 opens the app to about a dozen real
    /// galaxies, but their arm counts, pitch angles, bulge ratios, inclinations and colour stories are being
    /// researched and sourced separately. Adding a profile with guessed numbers would put invented astronomy in
    /// front of a player, which is the one thing D-010 explicitly did not licence.</para>
    /// </summary>
    public static class GalaxyProfiles
    {
        /// <summary>
        /// A fresh instance per call, so nothing can accumulate state between two builds in one editor session.
        /// </summary>
        public static GalaxyProfile[] All() => new[]
        {
            AndromedaBuilder.Profile(),
        };

        /// <summary>The profile with this <see cref="GalaxyProfile.Id"/>, or null.</summary>
        public static GalaxyProfile Find(string id)
        {
            foreach (var profile in All())
            {
                if (profile.Id == id)
                {
                    return profile;
                }
            }

            return null;
        }

        /// <summary>"andromeda, whirlpool, sombrero" — for error messages that have to list the options.</summary>
        public static string Ids()
        {
            var all = All();
            var ids = new string[all.Length];
            for (var i = 0; i < all.Length; i++)
            {
                ids[i] = all[i].Id;
            }

            return string.Join(", ", ids);
        }
    }
}
