// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// <b>Andromeda</b> (GDD 4.4): a second particle galaxy, 1.2 m across, that the player grabs, tilts and
    /// scales, with the scene panel to its right. Profile #1 — the numbers, the three layers and the colours;
    /// <see cref="GalaxyBuilder"/> is the machinery that turns them into assets.
    ///
    /// <para><b>Why this exists.</b> <c>Assets/data/experiences/andromeda.asset</c> shipped with an empty
    /// <c>SceneName</c> <i>and</i> a null <c>ContentPrefab</c>. Under the rule CS-036 set,
    /// <c>ExperienceDirector.Switch</c> refuses such a module before anything moves, so one of the seven dock
    /// tiles was dead by construction. Writing a content prefab and pointing the module at it is the whole
    /// fix — no new code path, no new scene.</para>
    ///
    /// <para><b>Why the file kept its name.</b> The builder is now data-driven (D-010: about a dozen real
    /// galaxies become visitable places), so the generic half moved to <see cref="GalaxyBuilder"/> and this file
    /// holds only what is Andromeda. The menu item <b>Cosmic Simulation &gt; Build Andromeda Content</b> is
    /// unchanged, because docs and tickets name it.</para>
    ///
    /// <para><b>Andromeda's shape is stated against the Milky Way's</b>, because "visibly distinguishable at a
    /// glance" (GDD 4.4's acceptance criterion) is a comparison, not an absolute. Every value below carries the
    /// corresponding value on <c>milky_way_prefab</c>'s three <c>SpiralGalaxy</c> components.</para>
    /// </summary>
    public static class AndromedaBuilder
    {
        /// <summary>
        /// Everything that makes this galaxy this galaxy. A fresh instance per call — see
        /// <see cref="GalaxyProfiles.All"/>.
        /// </summary>
        public static GalaxyProfile Profile() => new GalaxyProfile
        {
            Id = "andromeda",
            DisplayName = "Andromeda (M31)",
            Kind = GalaxyKind.SpiralArms,

            // GDD 4.4: "1.2 m across". The galaxy node is scaled so that this comes out exact.
            WidthMetres = 1.2f,

            // GDD 4.4: "flatter (tilt 75 deg)".
            DiscTiltDegrees = 75f,

            // Not in the GDD, and the one look knob CS-103 is most likely to move. The tilt alone leaves the
            // disc 15 deg from face-on to a player looking horizontally, which reads as a flat circle; yawing it
            // as well foreshortens it into the inclined oval Andromeda is recognised by. The pair presents the
            // disc about 56 deg off face-on (the build log prints the figure).
            //
            // These are literals rather than GalaxyProfile.TiltForPresentedInclination(...) of a published
            // inclination, and deliberately: M31's measured inclination is near 77 deg, which would present a
            // much more edge-on galaxy than GDD 4.4's "flatter, white core, bluer outer arms" describes, and
            // changing it is a look decision with a re-bake attached rather than a refactor. When the research
            // note lands, that is the conversation to have; until then the committed assets are what these two
            // numbers produce.
            DiscYawDegrees = 55f,

            // GDD 4.4: "Move, tilt, scale with hands (0.5-3 m)". None of ScaleLimits' presets (Body 0.05-3,
            // Model 0.4-4, Nebula 0.3-2) is this range, so it is stated rather than approximated.
            MinScaleMetres = 0.5f,
            MaxScaleMetres = 3f,

            // The player is outside this object (unlike the Cosmic Web), so a sphere the size of the thing is
            // exactly what the hand and the mouse ray should find, and it stops short of the scene panel, which
            // the director anchors 0.55 m to the right of the content root.
            GrabRadiusFraction = 1f,

            // Ellipse semi-axes, shared by all three layers so they describe one galaxy. Milky Way: 0.8 / 1.0.
            // Slightly more eccentric than ours, for a longer arm sweep.
            XRadii = 0.72f,
            ZRadii = 1f,

            // Andromeda is a two-fold symmetric grand design, so the second arm starts opposite the first —
            // which is what ArmCount 2 means (180 deg apart). Milky Way: 2 arms 95 deg apart, lopsided.
            ArmCount = 2,

            // Total azimuth the arm sweeps, in degrees. Milky Way: 370 (about one turn). Andromeda's arms are
            // more tightly wound than ours and read almost as rings, so this is higher.
            //
            // Left as the literal the committed star data was baked from, not
            // GalaxyProfile.WindingFromPitchAngle(...), for one reason: re-deriving it would move it by a float
            // rounding and rewrite all 17,200 baked stars for no visible change, and a diff that big has to
            // mean something. For the record, 470 deg over the disc layer's 0.1-1.0 radial range is a pitch
            // angle of about 15.7 deg by that same conversion, which the build log prints. The literature puts
            // M31 nearer 8-13 deg; adopting a sourced figure is a deliberate look change and a re-bake, and
            // every new profile should use WindingFromPitchAngle from the start.
            WindingDegrees = 470f,

            // Turns of the whole galaxy per unit of time, through the shader's _Age. Milky Way: 0.08. Slower,
            // because this one is a single object the player is looking at rather than a map they are working
            // over, and because nothing here may look like it is bouncing or drifting under a grab.
            VelocityMultiplier = 0.05f,

            Layers = GalaxyLayers(),
            Palette = Colours(),
        };

        /// <summary>
        /// The three layers, in draw order. 17,200 points in total against the Milky Way's 18,720 and the
        /// 400,000 point sprites a whole frame is allowed (Technical Overview 7.4) — see the log line the build
        /// prints for the arithmetic, and <see cref="GalaxyBuilder"/> for what a dozen of these would cost.
        /// </summary>
        private static GalaxyLayerSpec[] GalaxyLayers() => new[]
        {
            new GalaxyLayerSpec
            {
                Role = GalaxyLayerRole.Clouds,
                Id = "clouds",
                ShaderPath = GalaxyBuilder.CloudShaderPath,
                Seed = 31415,
                Ellipses = 250,          // Milky Way clouds: 320
                StarsPerEllipse = 10,    // Milky Way clouds: 10
                MinEllipseScale = 0.02f, // Milky Way clouds: 0.02
                MaxEllipseScale = 0.62f, // Milky Way clouds: 1.0 — held in, so this reads as a bulge
                Fuzz = new Vector2(0.92f, 1.08f),
                ThicknessAtCentre = 1f,
                ThicknessAtRim = 0.35f,
                YRange = 0.075f,
                WorldSpaceScale = 0.02f, // Milky Way clouds: 0.02
                Tint = Color.white,
                TintMultiplier = 0.3f,   // Milky Way clouds: 0.3
                IsShadow = false,
                DrawIndex = 0,
            },
            new GalaxyLayerSpec
            {
                Role = GalaxyLayerRole.Dust,
                Id = "dust",
                ShaderPath = GalaxyBuilder.DustShaderPath,
                Seed = 27182,
                Ellipses = 200,
                StarsPerEllipse = 8,
                MinEllipseScale = 0.46f, // an annulus, not a disc: Andromeda's dust is a ring
                MaxEllipseScale = 1f,
                Fuzz = new Vector2(0.94f, 1.06f),
                ThicknessAtCentre = 0.4f,
                ThicknessAtRim = 0.4f,
                YRange = 0.018f,         // dust sits in a thin plane
                WorldSpaceScale = 0.02f, // Milky Way negative: 0.03
                // The warm red the dock tile already uses for Andromeda (ThumbnailBuilder's #8C4D42), pushed to
                // full value here because this shader multiplies the tint in twice — once as colour, once as
                // the alpha that decides how much of what is behind the dust it takes away.
                Tint = new Color(0.09f, 0.033f, 0.020f, 0.45f),
                TintMultiplier = 1f,
                IsShadow = true,
                DrawIndex = 1,
                // The band already starts at MinEllipseScale; this puts the weight of it at about two thirds of
                // the galaxy's radius and lets it fade at both edges, so the lane has a shape rather than a hard
                // inner rim. M31's ring is the reason these are not the generator's defaults.
                DustRingPeak = 0.35f,
                DustRingWidth = 0.3f,
            },
            new GalaxyLayerSpec
            {
                Role = GalaxyLayerRole.Stars,
                Id = "stars",
                ShaderPath = GalaxyBuilder.StarShaderPath,
                Seed = 16180,
                Ellipses = 300,          // Milky Way stars: 320
                StarsPerEllipse = 15,    // Milky Way stars: 13
                MinEllipseScale = 0.1f,  // Milky Way stars: 0.02 — Andromeda's inner disc is comparatively empty
                MaxEllipseScale = 1f,
                Fuzz = new Vector2(0.965f, 1.035f),
                ThicknessAtCentre = 1f,
                ThicknessAtRim = 0.1f,
                // GDD 4.4 "flatter". Half-thickness 0.055 against a radius of 1 at the core and a tenth of that
                // at the rim. The Milky Way bakes 0.1 and its scene instance then stretches y by 2.5.
                YRange = 0.055f,
                WorldSpaceScale = 0.008f, // Milky Way stars: 0.005, at a smaller object scale
                Tint = Color.white,
                TintMultiplier = 1f,
                IsShadow = false,
                DrawIndex = 2,
            },
        };

        /// <summary>
        /// Andromeda's palette is the dock tile's: ThumbnailBuilder already keys this place to #FFEDD1 warm
        /// white, #8C4D42 reddish brown and a near-black edge. Using the same hues keeps the tile honest.
        /// </summary>
        private static GalaxyPalette Colours() => new GalaxyPalette
        {
            // GDD 4.4: white core, bluer outer arms.
            Disc = new[]
            {
                (0.00f, new Color(1.00f, 0.93f, 0.82f)), // #FFEDD1
                (0.22f, new Color(1.00f, 0.90f, 0.76f)),
                (0.45f, new Color(0.90f, 0.86f, 0.84f)),
                (0.70f, new Color(0.70f, 0.78f, 0.97f)),
                (1.00f, new Color(0.55f, 0.70f, 1.00f)),
            },

            // The bulge: white in the middle, warm at its edge.
            Clouds = new[]
            {
                (0.00f, new Color(1.00f, 0.96f, 0.88f)),
                (0.45f, new Color(0.98f, 0.88f, 0.74f)),
                (1.00f, new Color(0.78f, 0.58f, 0.46f)),
            },

            // GDD 4.4: warm reddish dust ring. #8C4D42's hue, at full value.
            Dust = new[]
            {
                (0.00f, new Color(0.85f, 0.44f, 0.36f)),
                (0.55f, new Color(1.00f, 0.56f, 0.46f)),
                (1.00f, new Color(0.86f, 0.48f, 0.44f)),
            },

            // Ionised hydrogen in the ring: the warm knots that break an arm up.
            HiiKnot = new Color(1.00f, 0.52f, 0.34f),

            // Young blue stars, which is what makes the outer arms read as blue at all.
            BlueGiant = new Color(0.62f, 0.78f, 1.00f),
        };

        /// <summary>
        /// Kept at its original path: <c>docs/</c> and several closed tickets name this menu item. It is now one
        /// line over <see cref="GalaxyBuilder"/>.
        /// </summary>
        [MenuItem("Cosmic Simulation/Build Andromeda Content")]
        public static void Build()
        {
            GalaxyBuilder.Build(Profile());
        }
    }
}
