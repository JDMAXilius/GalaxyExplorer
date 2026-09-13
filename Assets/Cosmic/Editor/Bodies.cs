using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cosmic.Editor
{
    internal static class Bodies
    {
        const string Models = "Assets/models/";
        const string Shaders = "Assets/Cosmic/Shaders/";
        const string BeamShaderPath = "Assets/shaders/force_pull_shaders/tractor_beam_shader.shader";
        const string UnitSphere = Models + "planet_unit_sphere_model.fbx";
        const string GlowTexture = "glow_normal_alpha_texture.tga";
        const string CloudNoise = "clouds_noise_alpha_texture.tga";

        const float ModelMetres = 100f;
        const float GrabRadius = 0.5f;
        const float MinGrabDiameterMetres = 0.06f;
        const float MinMoonDiameterMetres = 0.03f;
        const float RowDiameterMetres = 0.15f;
        const float CloudShell = 1.01f;
        const float AtmosphereShell = 1.025f;
        const float SunGlowScale = 0.019471662f;
        const float SunFlareGroupScale = 0.024039999f;
        const float SunFlareArmScale = 0.59427786f;
        const float SunFlareQuadScale = 0.00086005894f;

        class Skin
        {
            public string id, model, mesh, texture, normal, keyword;
            public Color albedo = Color.white, ambient, fresnel, dark, night = Color.clear;
            public Color tint = new Color(1f, 0.9801618f, 0.8602941f);
            public Vector4 term = new Vector4(1.6f, 0.8f, 0f, 0f);
            public Vector4 gamma = new Vector4(1.2f, 0.8f, 1f, 1f);
            public Vector4 normalScale = new Vector4(1f, 1f, 1f, 0f);
            public Vector4 spec = new Vector4(60f, 0f, 0f, 0f);
            public float light = 1f, tiltDegrees, modelMetres = ModelMetres;
            public int queue = -1, srcBlend = 1, dstBlend = 0, zWrite = 1;
        }

        struct MoonSpec
        {
            public string id, parent, texture, normal;
            public float radiusRatio, diameterRatio;
        }

        static readonly Skin[] Skins =
        {
            S("mercury", C(0.19999999f, 0.19291963f, 0.19290778f), C(0.479f, 0.43767446f, 0.4339176f),
              C(0.16500002f, 0.15743752f, 0.15675002f), 0.79f, V(1f, 1f, 1f, 0f), V(40f, 0f, 0.2f, 0f), 25.007f),
            S("venus", C(0.28936172f, 0.3f, 0.3f), C(1f, 0.82026f, 0.57f), C(0.433f, 0.39633623f, 0.34423497f),
              0.75f, V(1f, 1f, 2f, 0f), V(60f, 0f, 0f, 0f), 25.007f),
            S("earth", C(0.1544118f, 0.1544118f, 0.1544118f), C(0.82427824f, 1.0635303f, 1.274f),
              C(0.27691603f, 0.3572927f, 0.428f), 1f, V(1f, 1f, 1f, 0f), V(20f, 0f, 1f, 0f), 23.501f),
            S("mars", C(0.41f, 0.41f, 0.41f), C(0.733f, 0.68482655f, 0.61791897f),
              C(0.455f, 0.3939394f, 0.30924243f), 0.8f, V(2f, 2f, 1f, 0f), V(60f, 0f, 0f, 0f), 25.007f),
            S("jupiter", C(0.096453905f, 0.1f, 0.1f), C(0.90588236f, 0.7019608f, 0.5294118f),
              C(0.116999984f, 0.10432498f, 0.09359999f), 0.79f, V(0.5f, 0.5f, 2f, 0f), V(60f, 0f, 0f, 0f), 3.13f),
            S("saturn", C(0.19999999f, 0.19291963f, 0.19290778f), C(0.466f, 0.42451015f, 0.36674199f),
              C(0.46599996f, 0.44652116f, 0.41939998f), 0.58f, V(0f, 0f, 0.1f, 0f), V(60f, 0f, 0f, 0f), 26.705f),
            S("uranus", C(0.116f, 0.116f, 0.116f), C(0.621f, 0.621f, 0.621f), C(0.204f, 0.204f, 0.204f),
              1f, V(1f, 1f, 1f, 0f), V(60f, 0f, 0.1f, 0f), 98.0f),
            S("neptune", C(0.17097369f, 0.178f, 0.178f), C(0.584f, 0.82028794f, 1f),
              C(0.11978432f, 0.16801962f, 0.205f), 0.78f, V(1f, 1f, 1f, 0f), V(60f, 0f, 0.5f, 0f), 28.325f),
            S("pluto", C(0.2901961f, 0.3019608f, 0.3019608f), C(0.577f, 0.577f, 0.577f),
              C(0.43137258f, 0.41372553f, 0.38823533f), 0.75f, V(1f, 1f, 1f, 0f), V(60f, 0f, 0.2f, 0f), 120f),
            S("sun", Color.clear, C(3.632f, 2.2425573f, 1.1750586f), Color.clear, 1f, V(1f, 1f, 1f, 0f),
              V(60f, 0f, 0f, 0f), 7.251f),
        };

        static readonly MoonSpec[] MoonTable =
        {
            M("moon", "earth", 1.8f, 0.18464692f, "moon_diffuse_speculare_texture.tga", "moon_normal_texture.tga"),
            M("phobos", "mars", 0.96f, 0.12f, "mars_moons_texture.jpg", null),
            M("deimos", "mars", 1.24f, 0.12f, "mars_moons_texture.jpg", null),
            M("io", "jupiter", 1.2f, 0.19362946f, "jupiter_moons_texture.jpg", "generic_normal_texture.tga"),
            M("europa", "jupiter", 1.36f, 0.16593774f, "jupiter_moons_texture.jpg", "generic_normal_texture.tga"),
            M("ganymede", "jupiter", 1.6f, 0.28f, "jupiter_moons_texture.jpg", "generic_normal_texture.tga"),
            M("callisto", "jupiter", 2.08f, 0.25624147f, "jupiter_moons_texture.jpg", "generic_normal_texture.tga"),
            M("mimas", "saturn", 1.12f, 0.12f, "saturn_moons_texture.jpg", "generic_normal_texture.tga"),
            M("enceladus", "saturn", 1.28f, 0.12f, "saturn_moons_texture.jpg", "generic_normal_texture.tga"),
            M("titan", "saturn", 1.92f, 0.27372816f, "saturn_moons_texture.jpg", "generic_normal_texture.tga"),
            M("iapetus", "saturn", 2.4f, 0.12f, "saturn_moons_texture.jpg", "generic_normal_texture.tga"),
        };

        static readonly Quaternion[] FlareArms =
        {
            new Quaternion(0.00300155f, -0.0730509f, 0.0672623f, 0.995053f),
            new Quaternion(0.72305f, 0.147086f, -0.455285f, 0.498278f),
            new Quaternion(-0.788836f, 0.345228f, -0.460755f, -0.215083f),
            new Quaternion(-0.0408219f, 0.993862f, -0.0561696f, -0.0861242f),
            new Quaternion(0.027756f, -0.057645f, 0.649123f, 0.757988f),
            new Quaternion(0.387858f, 0.610417f, 0.0157438f, 0.690442f),
            new Quaternion(-0.708344f, 0.294853f, -0.120612f, -0.629891f),
            new Quaternion(0.275587f, 0.787823f, 0.534279f, -0.133916f),
            new Quaternion(0.01297f, 0.505752f, 0.786001f, 0.355316f),
            new Quaternion(-0.536089f, -0.153056f, 0.454226f, 0.694882f),
            new Quaternion(0.266365f, -0.542533f, 0.558426f, -0.568215f),
            new Quaternion(-0.27829f, 0.0341451f, 0.959882f, -0.00385837f),
        };

        static readonly float[] FlareArmLengths = { 0.344f, 0.353f, 0.336f };

        internal static void Assemble(GameObject root, Body body)
        {
            var id = body.id;
            var moon = Moon(id);
            var diameter = moon.id == null ? RowDiameterMetres : Diameter(moon);
            root.AddComponent<SphereCollider>().radius =
                Mathf.Max(GrabRadius, MinGrabDiameterMetres * 0.5f / Mathf.Max(0.0001f, diameter));
            var grab = root.AddComponent<Grabbable>();
            Content.Set(grab, "limits", Grabbable.Limits.Body);
            Content.Set(grab, "widthAtUnitScaleMetres", 1f);
            Content.Set(grab, "keepUpright", false);
            Content.Set(grab, "autoReturn", false);
            var rigidbody = root.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }

            Content.Set(root.AddComponent<Pull>(), "beamMaterial", Beam());
            var skin = Skinned(id, moon);
            Surface(root.transform, "mesh", skin);
            if (id == "sun") SunRig(root, skin);
            else Extras(root.transform, id, skin);
        }

        internal static List<Body> Listed(Place place)
        {
            var bodies = new List<Body>();
            if (place.bodies != null) bodies.AddRange(place.bodies);
            if (bodies.Count == 0 && place.layouts != null)
            {
                foreach (var layout in place.layouts)
                    foreach (var slot in layout.slots)
                        if (slot.body != null && !bodies.Contains(slot.body)) bodies.Add(slot.body);
            }

            bodies.RemoveAll(body => body == null || body.IsMoon);
            return bodies;
        }

        internal static int Moons(Body body, Rig rig, Transform planetAnchor)
        {
            var made = 0;
            var planet = planetAnchor.Find($"body_{body.id}");
            if (body.moons == null || planet == null) return made;
            var planetDiameter = Mathf.Max(0.0001f, planetAnchor.localScale.x);
            foreach (var rider in body.moons)
            {
                if (rider == null) continue;
                var spec = Moon(rider.id);
                if (spec.id == null) continue;
                var anchor = Content.Child(planet, $"home_{rider.id}");
                anchor.localScale = Vector3.one * (Diameter(spec) / planetDiameter);
                anchor.localPosition = Vector3.right * spec.radiusRatio;
                made += Content.Nest(rider, anchor, $"body_{rider.id}", 1f) ? 1 : 0;
                rig.Anchor(rider);
            }

            return made;
        }

        internal static Material Ring()
        {
            var material = Content.Mat("orbit", Shaders + "Orbit.shader");
            if (material == null) return null;
            Content.T(material, "_MainTex", Content.Tex(Content.Textures + "orbit_alpha_texture.png"));
            Content.C(material, "_Color", new Color(0.42020997f, 0.8149237f, 0.87f, 0.19f));
            Content.C(material, "_PlanetHighlightColor", new Color(0.4397091f, 0.6882862f, 1.0225793f, 0.19f));
            Content.F(material, "_Width", 0.0035f);
            Content.F(material, "_FadeOffDistanceAroundPlanet", 0.04f);
            Content.F(material, "_TrailTailAngle", -1.07f);
            Content.F(material, "_TransitionAlpha", 1f);
            return material;
        }

        internal static Material Beam()
        {
            var material = Content.Mat("beam", BeamShaderPath);
            if (material == null) return null;
            Content.F(material, "_LineLength", 1f);
            Content.F(material, "_LineWidth", 0.05f);
            Content.F(material, "_Coverage", 1f);
            Content.F(material, "_Active", 0f);
            return material;
        }

        internal static void Hole(Material disc, Material glow)
        {
            Content.T(disc, "_MainTex",
                      Content.Tex(Content.Textures + "sagittarius_a_black_hole_ray_marching_accretion_disc_1_highres.png"));
            Content.T(disc, "_MainTex2",
                      Content.Tex(Content.Textures + "sagittarius_a_black_hole_ray_marching_accretion_disc_2_highres.png"));
            Content.T(disc, "_MainTex3",
                      Content.Tex(Content.Textures + "sagittarius_a_black_hole_ray_marching_accretion_disc_3_skewed_highres.tga"));
            Content.C(disc, "_Tint1", new Color(0.8088235f, 0.59357476f, 0.32115054f));
            Content.C(disc, "_Tint2", new Color(0.63235295f, 0.58232903f, 0.49286333f));
            Content.F(disc, "_BlackHoleMass", 0.333f);
            Content.F(disc, "_MaxStepCount", 4f);
            Content.F(disc, "_FrontStepExtension", -0.51f);
            Content.F(disc, "_StepSizeExtension", 0.12f);
            Content.F(disc, "_EventHorizonDistance", 0.36f);
            Content.F(disc, "_EventHorizonDistanceDisc", 0.34f);
            Content.F(disc, "_DiscInnerDistance", 0.18f);
            Content.F(disc, "_DiscOuterDistance", 0.8f);
            Content.F(disc, "_RadialTextureScale", 3.142f);
            Content.F(disc, "_SpinSpeed1", 0.01f);
            Content.F(disc, "_SpinSpeed2", 0.02f);
            Content.F(disc, "_SpinSpeed3", 0f);
            Content.F(disc, "_Scale", 0.78f);
            Content.F(disc, "_SkyboxFade", 1f);
            Content.F(disc, "_EventHorizonPower", 4.01f);
            Content.F(disc, "_EventHorizonTint", 1f);
            Content.F(disc, "_ParallaxAmount", 0.035f);
            Content.T(glow, "_MainTex",
                      Content.Tex(Content.Textures + "sagittarius_a_black_hole_glow_card_texture.tga"));
        }

        static void Surface(Transform parent, string name, Skin skin)
        {
            var material = Content.Mat($"planet_{skin.id}", Shaders + (skin.id == "sun" ? "Sun.shader" : "Planet.shader"),
                                       skin.keyword);
            if (material == null) return;
            Content.T(material, "_MainTex", skin.texture == null ? null : Content.Tex(Content.Textures + skin.texture));
            Content.C(material, "_AlbedoMultiplier", skin.albedo);
            Content.C(material, "_FresnelColor", skin.fresnel);
            Content.V(material, "_FresnelTerm", skin.term);
            Content.F(material, "_TransitionAlpha", 1f);
            Content.F(material, "_SRCBLEND", skin.srcBlend);
            Content.F(material, "_DSTBLEND", skin.dstBlend);
            if (skin.id == "sun")
            {
                Content.C(material, "_TintColor", Color.white);
                Content.V(material, "_AddCycleParams", V(0.82f, 0.1f, 0.34f, 0.63f));
                Content.V(material, "_CycleParams", V(0.82f, 0.1f, 1.9f, 0.5f));
                Content.F(material, "_TouchBodyGain", 0.7f);
                Content.F(material, "_TouchRimGain", 1.5f);
            }
            else
            {
                Content.T(material, "_NormalAlpha",
                          skin.normal == null ? null : Content.Tex(Content.Textures + skin.normal));
                Content.C(material, "_AmbientColor", skin.ambient);
                Content.C(material, "_FresnelDarkSideColor", skin.dark);
                Content.C(material, "_NightLightColor", skin.night);
                Content.C(material, "_LightTint", skin.tint);
                Content.V(material, "_LightGammaCorrection", skin.gamma);
                Content.V(material, "_NormalScale", skin.normalScale);
                Content.V(material, "_SpecParams", skin.spec);
                Content.V(material, "_LightAmount", new Vector4(skin.light, 0f, 0f, 0f));
                Content.F(material, "_ZWRITE", skin.zWrite);
                Content.F(material, "_CULL", 2f);
            }

            if (skin.id == "saturn")
            {
                Content.T(material, "_RingsAlphaTex", Content.Tex(Content.Textures + "saturn_rings_alpha_texture.tga"));
                Content.F(material, "_InnerRingRadius", 0.0175f);
                Content.F(material, "_OuterRingRadius", 0.025f);
            }

            material.renderQueue = skin.queue;
            var node = Content.Draw(parent, name, Content.Mesh(skin.model, skin.mesh), material,
                                    Vector3.one * skin.modelMetres);
            node.transform.localRotation = Quaternion.Euler(skin.tiltDegrees, 0f, 0f);
        }

        static void Extras(Transform parent, string id, Skin skin)
        {
            switch (id)
            {
                case "earth":
                    Clouds(parent, skin, "EarthClouds", CloudNoise, "earth_clous_normal_alpha_texture.tga",
                           C(1.437f, 1.437f, 1.437f), V(0.93f, 1.01f, 0f, 0f), V(1.3f, -9.92f, 0.29f, 0f), 3f,
                           Color.white, -1);
                    Atmosphere(parent, skin);
                    Halo(parent, skin, "EarthGlow", C(0.372276f, 0.66340417f, 0.972f));
                    break;
                case "venus":
                    Clouds(parent, skin, "VenusClouds", CloudNoise, "venus_cloud_normal_alpha_texture.tga",
                           C(0.09599999f, 0.07850496f, 0.044544f), V(2.73f, 1.49f, 0f, 0f),
                           V(1f, -3.17f, 0.5f, 0f), 3f, C(1f, 0.9630482f, 0.87599987f), -1);
                    Halo(parent, skin, "VenusGlow", C(0.61960787f, 0.4392157f, 0.23921569f));
                    break;
                case "mars":
                    Halo(parent, skin, "MarsGlow", C(0.32941177f, 0.32941177f, 0.32941177f));
                    break;
                case "jupiter":
                    Clouds(parent, skin, "JupiterClouds", "jupiter_clouds_diffuse_alpha_texture.tga",
                           "jupiter_clouds_diffuse_alpha_texture.tga", C(0.243f, 0.243f, 0.243f),
                           V(1.61f, 1.15f, 0f, 0f), V(0.65f, -8.51f, 0.33f, 0f), -2.17f,
                           C(0.477f, 0.477f, 0.477f), 2000);
                    Halo(parent, skin, "EarthGlow", C(0.44705883f, 0.3647059f, 0.27450982f),
                         Models + "earth_model.fbx");
                    break;
                case "saturn":
                    Rings(parent, skin, "SaturnRings", "saturn_diffuse_alpha_texture.tga", 0.005f,
                          C(0.14705884f, 0.14705884f, 0.14705884f), null);
                    Halo(parent, skin, "SaturnGlow", C(0.6627451f, 0.62352943f, 0.49803922f));
                    break;
                case "uranus":
                    Rings(parent, skin, "UranusRings", "uranus_rings_diffuse_alpha_texture.tga", 0.06f,
                          Color.clear, "_BASIC");
                    Halo(parent, skin, "UranusGlow", C(0.2f, 0.21568628f, 0.21960784f));
                    break;
            }
        }

        static void Clouds(Transform parent, Skin skin, string mesh, string noise, string normal, Color color,
                           Vector4 term, Vector4 light, float noiseAmount, Color ambient, int queue)
        {
            var material = Content.Mat($"clouds_{skin.id}", Shaders + "Clouds.shader",
                                       skin.id == "jupiter" ? "_DIFFUSE" : null);
            if (material == null) return;
            Content.T(material, "_Noise", Content.Tex(Content.Textures + noise));
            Content.T(material, "_NormalAlpha", Content.Tex(Content.Textures + normal));
            Content.C(material, "_Color", color);
            Content.C(material, "_AmbientColor", ambient);
            Content.V(material, "_FresnelTerm", term);
            Content.V(material, "_LightAmount", light);
            Content.F(material, "_NoiseAmount", noiseAmount);
            Content.F(material, "_TransitionAlpha", 1f);
            material.renderQueue = queue;
            Shell(parent, "clouds", skin, mesh, material, CloudShell, skin.model);
        }

        static void Atmosphere(Transform parent, Skin skin)
        {
            var material = Content.Mat($"atmosphere_{skin.id}", Shaders + "Atmosphere.shader");
            if (material == null) return;
            Content.C(material, "_Color", C(0.372f, 0.663f, 0.972f));
            Content.F(material, "_RimPower", 3.2f);
            Content.F(material, "_RimScale", 1.15f);
            Content.F(material, "_SunInfluence", 0.8f);
            Content.F(material, "_SunWrap", 0.35f);
            Content.F(material, "_TransitionAlpha", 1f);
            Shell(parent, "atmosphere", skin, skin.mesh, material, AtmosphereShell, skin.model);
        }

        static void Halo(Transform parent, Skin skin, string mesh, Color color, string model = null)
        {
            var material = Content.Mat($"halo_{skin.id}", Shaders + "Halo.shader");
            if (material == null) return;
            Content.T(material, "_MainTex", Content.Tex(Content.Textures + GlowTexture));
            Content.C(material, "_Color", color);
            Content.C(material, "_AmbientColor", Color.clear);
            Content.F(material, "_TransitionAlpha", 1f);
            Shell(parent, "halo", skin, mesh, material, 1f, model ?? skin.model);
        }

        static void Rings(Transform parent, Skin skin, string mesh, string texture, float planetRadius,
                          Color ambient, string keyword)
        {
            var material = Content.Mat($"rings_{skin.id}", Shaders + "Rings.shader", keyword);
            if (material == null) return;
            Content.T(material, "_MainTex", Content.Tex(Content.Textures + texture));
            Content.C(material, "_Ambient", ambient);
            Content.F(material, "_PlanetRadius", planetRadius);
            Content.F(material, "_TransitionAlpha", 1f);
            var node = Content.Child(parent, "rings");
            node.localRotation = Quaternion.Euler(skin.tiltDegrees, 0f, 0f);
            Content.Draw(node, "ring_mesh", Content.Mesh(skin.model, mesh), material,
                         Vector3.one * skin.modelMetres);
        }

        static void Shell(Transform parent, string name, Skin skin, string mesh, Material material, float scale,
                          string model)
        {
            var node = Content.Draw(parent, name, Content.Mesh(model, mesh), material,
                                    Vector3.one * (skin.modelMetres * scale));
            node.transform.localRotation = Quaternion.Euler(skin.tiltDegrees, 0f, 0f);
        }

        // Sun.flareQuads and Sun.flares are the same quads: Sun scales each one from zero, so the arm carries the offset.
        static void SunRig(GameObject root, Skin skin)
        {
            var sun = root.AddComponent<Sun>();
            var globe = root.transform.Find("mesh");
            var surface = globe != null ? globe.GetComponent<Renderer>() : null;
            var corona = Corona(root.transform, skin);
            var glow = Glow(root.transform, "glow", "sun_glow", C(1.521f, 0.6753241f, 0f),
                            V(3.24f, 9.090909f, 0.5f, 0.5f), C(1f, 0.528f, 0f), V(2.02f, 1.8867924f, 0.5f, 0.5f),
                            0.252f, skin.modelMetres * SunGlowScale);
            var lens = Lens(root.transform, skin);
            var group = Content.Child(root.transform, "flares");
            group.localScale = Vector3.one * (skin.modelMetres * SunFlareGroupScale);
            var roots = new List<Object>();
            var quads = new List<Object>();
            var flareMaterial = Content.Mat("sun_flare", Shaders + "SunFlare.shader");
            Content.T(flareMaterial, "_MainTex", Content.Tex(Content.Textures + "sun_flare_texture.tga"));
            Content.C(flareMaterial, "_Color", C(0.77599996f, 0.77599996f, 0.77599996f));
            Content.V(flareMaterial, "_FresnelAlphaParams", V(0f, 1f, 1f, 1f));
            Content.F(flareMaterial, "_TransitionAlpha", 1f);
            for (var i = 0; i < FlareArms.Length; i++)
            {
                var shape = i / 4 + 1;
                var arm = Content.Child(group, $"flare_arm_{i:00}");
                arm.localRotation = FlareArms[i];
                arm.localScale = Vector3.one * SunFlareArmScale;
                var quad = Content.Draw(arm, $"flare_{i:00}", Content.Mesh($"{Models}sun_flare_{shape}_model.fbx", null),
                                        flareMaterial, Vector3.one * SunFlareQuadScale);
                quad.transform.localPosition = new Vector3(FlareArmLengths[shape - 1], 0f, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                roots.Add(quad.transform);
                quads.Add(quad);
            }

            Content.Set(sun, "body", root.transform);
            Content.SetArray(sun, "surfaces", new List<Object> { surface, corona });
            Content.Set(sun, "glow", glow);
            Content.Set(sun, "lensFlare", lens);
            Content.Set(sun, "flareGroup", corona != null ? corona.transform : null);
            Content.SetArray(sun, "flareQuads", roots);
            Content.SetArray(sun, "flares", quads);
            Content.Set(sun, "radiusMetres", 0.5f);
        }

        static Renderer Corona(Transform parent, Skin skin)
        {
            var material = Content.Mat("sun_corona", Shaders + "SunCorona.shader");
            if (material == null) return null;
            Content.T(material, "_MainTex", Content.Tex(Content.Textures + "sun_alpha_texture.png"));
            Content.C(material, "_Color", C(0.9843138f, 0.70980394f, 0.4039216f));
            Content.V(material, "_FresnelAlphaParams", V(0f, 4.01f, 1.1f, 1f));
            Content.F(material, "_TransitionAlpha", 1f);
            return Content.Draw(parent, "corona", Content.Mesh(skin.model, "SunFlares"), material,
                                Vector3.one * skin.modelMetres);
        }

        static Renderer Glow(Transform parent, string name, string materialName, Color a, Vector4 aParams, Color b,
                             Vector4 bParams, float clipRadius, float scale)
        {
            var material = Content.Mat(materialName, Shaders + "SunGlow.shader");
            if (material == null) return null;
            Content.C(material, "_ColorA", a);
            Content.V(material, "_ColorAParams", aParams);
            Content.C(material, "_ColorB", b);
            Content.V(material, "_ColorBParams", bParams);
            Content.F(material, "_ClipRadius", clipRadius);
            Content.F(material, "_TransitionAlpha", 1f);
            Content.F(material, "_SRCBLEND", 1f);
            Content.F(material, "_DSTBLEND", 1f);
            return Content.Draw(parent, name, Content.Builtin("Quad.fbx"), material, Vector3.one * scale);
        }

        static Renderer Lens(Transform parent, Skin skin)
        {
            var material = Content.Mat("sun_lens_flare", Shaders + "LensFlare.shader");
            if (material == null) return null;
            Content.T(material, "_MainTex", Content.Tex(Content.Textures + "sun_lens_flare_texture.tga"));
            Content.C(material, "_Tint", C(1.298f, 0.9786931f, 0.6776324f));
            Content.V(material, "_FadeParams", V(9f, 14f, 0f, 0f));
            Content.F(material, "_TransitionAlpha", 1f);
            return Content.Draw(parent, "lens_flare", Content.Builtin("Quad.fbx"), material,
                                Vector3.one * (skin.modelMetres * SunGlowScale));
        }

        static Skin Skinned(string id, MoonSpec moon)
        {
            foreach (var skin in Skins)
                if (skin.id == id)
                    return skin;
            if (moon.id != null)
                return new Skin
                {
                    id = id, model = $"{Models}{Model(moon)}", mesh = null, texture = moon.texture,
                    normal = moon.normal, ambient = C(0.2f, 0.1921569f, 0.1921569f),
                    fresnel = C(0.357f, 0.357f, 0.357f), dark = C(0.088f, 0.088f, 0.088f), light = 0.8f,
                };
            var star = id.EndsWith("_star");
            return new Skin
            {
                id = id, model = UnitSphere, mesh = null, modelMetres = 1f,
                albedo = star ? Color.white : C(0.561f, 0.749f, 0.847f),
                ambient = star ? C(1f, 0.919317f, 0.852616f) : new Color(0.1122f, 0.1498f, 0.1694f, 0.2f),
                fresnel = star ? C(1f, 0.919317f, 0.852616f) : new Color(0.26928f, 0.35952f, 0.40656f, 0.48f),
                dark = star ? C(1f, 0.919317f, 0.852616f) : new Color(0.092565f, 0.123585f, 0.139755f, 0.165f),
                tint = star ? Color.white : C(1f, 0.919317f, 0.852616f), light = star ? 0f : 0.79f,
                normalScale = V(0f, 0f, 1f, 0f), spec = V(40f, 0f, 0f, 0f),
            };
        }

        static string Model(MoonSpec moon) =>
            moon.id == "moon" ? "moon_model.fbx" : $"{moon.id}_{moon.parent}_moon_model.fbx";

        static float Diameter(MoonSpec moon) =>
            Mathf.Max(MinMoonDiameterMetres, RowDiameterMetres * moon.diameterRatio);

        static MoonSpec Moon(string id)
        {
            foreach (var moon in MoonTable)
                if (moon.id == id)
                    return moon;
            return default;
        }

        static MoonSpec M(string id, string parent, float radiusRatio, float diameterRatio, string texture,
                          string normal) => new MoonSpec
        {
            id = id, parent = parent, radiusRatio = radiusRatio, diameterRatio = diameterRatio, texture = texture,
            normal = normal,
        };

        static Skin S(string id, Color ambient, Color fresnel, Color dark, float light, Vector4 normalScale,
                      Vector4 spec, float tiltDegrees)
        {
            var skin = new Skin
            {
                id = id, model = $"{Models}{id}_model.fbx", mesh = char.ToUpper(id[0]) + id.Substring(1),
                texture = $"{id}_diffuse_specular_texture.tga", normal = $"{id}_normal_texture.tga",
                ambient = ambient, fresnel = fresnel, dark = dark, light = light, normalScale = normalScale,
                spec = spec, tiltDegrees = tiltDegrees,
            };
            switch (id)
            {
                case "earth":
                    skin.keyword = "_EARTH";
                    skin.texture = "earth_diffude_specular_texture.tga";
                    skin.normal = "earth_normal_emissive_texture.tga";
                    skin.albedo = C(1.191f, 1.191f, 1.191f);
                    skin.night = C(1.04f, 0.9698764f, 0.77235293f);
                    skin.gamma = V(0.8f, 0.7f, 1f, 1f);
                    skin.tint = C(1f, 0.93933624f, 0.77205884f);
                    skin.term = V(1.7f, 1f, 0f, 0f);
                    skin.srcBlend = 5;
                    skin.dstBlend = 10;
                    skin.zWrite = 0;
                    skin.queue = 3000;
                    break;
                case "mars":
                    skin.albedo = C(0.69f, 0.6628513f, 0.61893f);
                    break;
                case "jupiter":
                    skin.queue = 2000;
                    break;
                case "saturn":
                    skin.keyword = "_SATURN";
                    skin.texture = "saturn_diffuse_alpha_texture.tga";
                    skin.normal = null;
                    skin.tint = Color.white;
                    skin.srcBlend = 5;
                    skin.dstBlend = 10;
                    break;
                case "neptune":
                    skin.texture = "neptune_diffuse_specular_textures.tga";
                    skin.night = Color.white;
                    break;
                case "pluto":
                    skin.texture = "pluto_diffuse_specular_material.tga";
                    break;
                case "sun":
                    skin.texture = "sun_diffuse_texture.tga";
                    skin.normal = null;
                    skin.term = V(0.54f, 0.81f, 0f, 0f);
                    break;
            }

            return skin;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static Vector4 V(float x, float y, float z, float w) => new Vector4(x, y, z, w);
    }
}
