// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the atmosphere rim materials, so the recipe is a script anyone can re-run and read rather than a
    /// .mat somebody once tuned by hand and nobody dares touch.
    ///
    /// <para><b>Why not halo_shader.</b> CS-047 said reuse it. It cannot do this job. <c>halo_shader</c> has no
    /// view-dependent term at all: its rim is a tangent-space normal map baked into
    /// <c>glow_normal_alpha_texture</c> and painted onto the dedicated <c>*_glow_mesh</c> submesh inside each
    /// planet's original FBX, lit by <c>_SunDirection</c>. Point it at the plain 1 m sphere our body prefabs are
    /// normalised to and you get a lit ball, not a limb glow, and no material setting adds a Fresnel term the
    /// shader never computes. It also predates the Quest port — no stereo macros, no <c>#pragma target</c> — so
    /// under Android multiview it would draw one eye's view into both. Hence
    /// <c>Assets/shaders/planet_atmosphere_rim_shader.shader</c>, which is about forty lines.</para>
    ///
    /// <para><b>How the material is meant to be used.</b> Put it on a sphere child of the body, uniformly scaled
    /// to roughly 1.025 of the body's own sphere. The rim lives in that gap, so a larger shell reads as a thicker
    /// atmosphere; below about 1.01 it disappears into the surface. Give that child a <c>SunLightReceiver</c>
    /// pointing at the Sun so <c>_SunDirection</c> tracks it — without one the rim lights evenly all the way
    /// round, which is wrong but not broken. Note that <c>SunLightReceiver</c> writes to <c>sharedMaterial</c>,
    /// so re-run this menu item before committing if a play session has left a direction in the asset.</para>
    /// </summary>
    public static class AtmosphereMaterialBuilder
    {
        private const string ShaderPath = "Assets/shaders/planet_atmosphere_rim_shader.shader";
        private const string ShaderName = "CosmicSimulation/PlanetAtmosphereRim";
        private const string MaterialFolder = "Assets/materials";

        /// <summary>Softness of the day/night edge. One value for every body: it is the sun, not the planet.</summary>
        private const float SunWrap = 0.35f;

        private readonly struct Recipe
        {
            public readonly string FileName;
            public readonly Color Color;
            public readonly float RimPower;
            public readonly float RimScale;
            public readonly float SunInfluence;

            public Recipe(string fileName, Color color, float rimPower, float rimScale, float sunInfluence)
            {
                FileName = fileName;
                Color = color;
                RimPower = rimPower;
                RimScale = rimScale;
                SunInfluence = sunInfluence;
            }
        }

        private static readonly Recipe[] Recipes =
        {
            // Earth. The blue is the one earth_glow_material already uses, so the new rim matches art that
            // shipped instead of introducing a second blue. Falloff is tight because the body is 25 cm in the
            // hand and a wide band there reads as fog; sun influence is high because the day/night edge on Earth
            // is the detail people recognise.
            new Recipe("earth_atmosphere_material", new Color(0.372f, 0.663f, 0.972f, 1f), 3.2f, 1.15f, 0.8f),
        };

        [MenuItem("Cosmic Simulation/Build Atmosphere Materials")]
        public static void Build()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                Debug.LogError($"AtmosphereMaterialBuilder: '{ShaderPath}' is missing; nothing built.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Debug.LogError($"AtmosphereMaterialBuilder: '{MaterialFolder}' is missing; nothing built.");
                return;
            }

            foreach (var recipe in Recipes)
            {
                var path = $"{MaterialFolder}/{recipe.FileName}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var created = material == null;

                if (created)
                {
                    material = new Material(shader) { name = recipe.FileName };
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    // Re-running is the point: an existing material is reset to the recipe, not left half tuned.
                    material.shader = shader;
                }

                Apply(material, recipe);
                EditorUtility.SetDirty(material);
                Debug.Log($"AtmosphereMaterialBuilder: {(created ? "created" : "updated")} {path}");
            }

            AssetDatabase.SaveAssets();
        }

        private static void Apply(Material material, Recipe recipe)
        {
            material.SetColor("_Color", recipe.Color);
            material.SetFloat("_RimPower", recipe.RimPower);
            material.SetFloat("_RimScale", recipe.RimScale);
            material.SetFloat("_SunInfluence", recipe.SunInfluence);
            material.SetFloat("_SunWrap", SunWrap);
            material.SetFloat("_TransitionAlpha", 1f);

            // Zero is the shader's "no sun set" case, and writing it back here is also how a direction that
            // leaked out of a play session gets out of the asset again before it reaches a diff.
            material.SetVector("_SunDirection", Vector4.zero);
        }
    }
}
