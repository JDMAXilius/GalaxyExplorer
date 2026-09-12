// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Text;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Puts the atmosphere rim shell onto a body's source prefab: the wiring half of CS-039, which
    /// <see cref="AtmosphereMaterialBuilder"/> deliberately does not do. That builder writes
    /// <c>Assets/materials/&lt;id&gt;_atmosphere_material.mat</c> and stops; until this runs, nothing in the
    /// project references it.
    ///
    /// <para><b>Why the source prefab and not the generated content.</b> <see cref="SolarRowBuilder"/>
    /// instantiates <c>poi_&lt;id&gt;_prefab</c> from <c>Assets/prefabs/poi_prefabs</c> and unpacks it into
    /// <c>solar_system_planets_content_prefab</c>, and <c>solar_system_prefab</c> (the orbit model) references
    /// the same source prefab as a nested instance. Authoring the shell in the source therefore reaches both
    /// places, and survives the next <b>Build Solar Row Content</b>; authoring it in the generated prefab would
    /// be thrown away by that rebuild. The generated prefab still has to be rebuilt once after this runs, which
    /// the report says.</para>
    ///
    /// <para><b>What one shell is.</b> A single child of the node that already parents the body's own sphere —
    /// a sibling of <c>&lt;id&gt;_sphere_mesh</c>, not a child of it, which matters (see below) — carrying:
    /// <list type="bullet">
    /// <item>a <c>MeshFilter</c> pointing at <b>the sphere's own mesh</b>, so no new asset enters the project
    /// and the shell is exactly the body's shape;</item>
    /// <item>a <c>MeshRenderer</c> with <c>&lt;id&gt;_atmosphere_material</c>, shadows and probes off: the
    /// shader is additive with <c>ZWrite Off</c>, so a shadow caster or a probe fetch buys nothing and costs
    /// fill on a Quest 3;</item>
    /// <item>a <see cref="SunLightReceiver"/> so <c>_SunDirection</c> tracks the Sun and the rim has a
    /// terminator, with <see cref="SunLightReceiver.UsePropertyBlock"/> on — see the leak note below;</item>
    /// <item><b>no collider</b>, and nothing else.</item>
    /// </list>
    /// Scale is <see cref="ShellScale"/> = 1.025 of the sphere's own, per
    /// <see cref="AtmosphereMaterialBuilder"/>: the rim lives in the gap, and below about 1.01 it disappears
    /// into the surface. At Solar Row's 15 cm bodies that gap is 1.9 mm.</para>
    ///
    /// <para><b>Why a sibling of the sphere rather than a child of it.</b> Three things measure a body off the
    /// sphere's own subtree, and a 1.025 shell inside that subtree would quietly enlarge all of them:
    /// <c>SolarRowBuilder.Measure</c> (which normalises every body to a 1 m diameter, so the shell would shrink
    /// Earth by 2.4%), <c>SolarRowBuilder.LargestRenderer</c> (which becomes <c>ScaleLimits.measureFrom</c> and
    /// <c>InfoPanel.targetBounds</c>), and XRI's collider sweep on the <c>GEInteractable</c> that sits on the
    /// sphere. As a sibling the shell is outside all three. The one number it does still reach is
    /// <c>SolarRowBuilder</c>'s <c>visualSpan</c>, which becomes <c>LayoutRig.Extent.SpanRatio</c> and feeds the
    /// ring-clearance solver — so <c>"atmosphere"</c> is in both builders' <c>NotGeometry</c> lists, alongside
    /// <c>"glow"</c> and <c>"halo"</c>, exactly as <c>FindRings</c>' own comment asks for.</para>
    ///
    /// <para><b>The sharedMaterial leak.</b> <see cref="SunLightReceiver"/> writes <c>_SunDirection</c> to
    /// <c>currentRenderer.sharedMaterial</c> every <c>LateUpdate</c>. In the orbit model a <c>Fader</c> ancestor
    /// happens to instance the material first, but <c>SolarRowBuilder.Strip</c> removes every <c>Fader</c> from
    /// the body visual, so in Solar Row the write lands on the <c>.mat</c> asset on disk and a play session
    /// leaves a sun direction in a diff. This builder therefore sets the opt-in
    /// <see cref="SunLightReceiver.UsePropertyBlock"/>, which routes the same write through a
    /// <c>MaterialPropertyBlock</c> — the pattern <c>SunTouchResponse</c> already uses for
    /// <c>_TouchBrightness</c>. Per-renderer is also the correct scope: two bodies sharing one atmosphere
    /// material would otherwise fight over one uniform.</para>
    ///
    /// <para><b>What a shell costs, before you add the second recipe.</b> Measured on Earth, 12 Sep 2026:
    /// reusing the sphere's mesh means a shell is a full duplicate of it — <b>6 468 verts and 12 288
    /// triangles</b>, plus one draw call, per body. For Earth alone that is nothing. For all ten bodies it would
    /// be about <b>123 000 triangles</b> of additive, depth-writing-free fill drawn over the planets, and
    /// overdraw is the cost that actually binds on a Quest 3 (Technical Overview §7.4 budgets draw calls at
    /// ≤ 150 and forbids realtime shadows; it sets no triangle ceiling, so triangles are not the number to
    /// argue from — fill is). Whoever adds the second recipe should give the shells <b>one shared low-poly
    /// sphere</b> rather than ten full-density duplicates: the rim is a smooth Fresnel gradient computed per
    /// pixel and its silhouette is the only place tessellation shows, so it needs far fewer triangles than a
    /// texture-mapped surface does. That is a deliberate trade this builder has not taken yet, because CS-039
    /// asked it to reuse the mesh the body already has and there is exactly one body.</para>
    ///
    /// <para><b>Idempotent.</b> The shell is found by name (<c>&lt;id&gt;_atmosphere_shell</c>) anywhere under
    /// the prefab, updated in place, moved to the right parent if it drifted, and any duplicate is destroyed. A
    /// second run produces the same asset, so scene and prefab references to it survive.</para>
    ///
    /// <para>Order: <b>Build Atmosphere Materials</b>, then this, then <b>Build Solar Row Content</b> and
    /// <b>Build Moons</b>.</para>
    /// </summary>
    public static class AtmosphereShellBuilder
    {
        private const string PrefabFolder = "Assets/prefabs/poi_prefabs";
        private const string MaterialFolder = "Assets/materials";
        private const string MaterialSuffix = "_atmosphere_material";
        private const string ShellSuffix = "_atmosphere_shell";
        private const string ExpectedShader = "CosmicSimulation/PlanetAtmosphereRim";

        /// <summary>
        /// 1.025 of the body's own sphere, from <see cref="AtmosphereMaterialBuilder"/>'s recipe: the rim is the
        /// gap, so a larger shell reads as a thicker atmosphere and below about 1.01 there is nothing to see.
        /// </summary>
        private const float ShellScale = 1.025f;

        /// <summary>
        /// Solar Row's body diameter (GDD 4.1), used only to state the shell's gap in millimetres in the report
        /// so the run is checkable against a physical number rather than a ratio.
        /// </summary>
        private const float SolarRowDiameter = 0.15f;

        /// <summary>
        /// Every body that could have one, in GDD 4.1's order. A body without a matching material is skipped
        /// and named in the report: the recipe list, not this list, decides who gets an atmosphere.
        /// </summary>
        private static readonly string[] Candidates =
        {
            "sun", "mercury", "venus", "earth", "mars", "jupiter", "saturn", "uranus", "neptune", "pluto",
        };

        /// <summary>Layers a non-interactive visual must never land on, because pointers look for them.</summary>
        private static readonly string[] InteractionLayers = { "POI", "ForceGrab" };

        private class Wired
        {
            public string Id;
            public string PrefabPath;
            public string MaterialName;
            public string ParentPath;
            public string SurfaceName;
            public string MeshName;
            public int Vertices;
            public long Triangles;
            public float SurfaceDiameter;
            public float ShellDiameter;
            public float Ratio;
            public float Concentricity;
            public float WidestSurface;
            public string WidestSurfaceName;
            public float WidestLight;
            public string WidestLightName;
            public int ShellColliders;
            public int ShellInteractables;
            public int SurfaceColliders;
            public int SurfaceInteractables;
            public bool Created;
            public int LayerIndex;
            public string LayerName;
        }

        [MenuItem("Cosmic Simulation/Build Atmosphere Shells")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("AtmosphereShellBuilder: leave play mode first — this edits prefab assets.");
                return;
            }

            var wired = new List<Wired>();
            var skipped = new List<string>();
            var failed = new List<string>();
            var haveMaterial = 0;

            foreach (var id in Candidates)
            {
                var materialPath = $"{MaterialFolder}/{id}{MaterialSuffix}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    // Not an error: only the bodies AtmosphereMaterialBuilder has a recipe for get a shell.
                    skipped.Add($"{id} (no {id}{MaterialSuffix}.mat)");
                    continue;
                }

                haveMaterial++;

                if (!CheckMaterial(id, material, materialPath, failed))
                {
                    continue;
                }

                var entry = Wire(id, material, failed);
                if (entry != null)
                {
                    wired.Add(entry);
                }
            }

            if (haveMaterial == 0)
            {
                Debug.LogError("AtmosphereShellBuilder: no '<id>" + MaterialSuffix + ".mat' exists in " +
                               $"{MaterialFolder}, so there is nothing to wire. Run " +
                               "Cosmic Simulation > Build Atmosphere Materials first.");
                return;
            }

            if (wired.Count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Report(wired, skipped, failed);
        }

        /// <summary>
        /// The material has to be usable before a prefab is opened for it: a broken shader would put a magenta
        /// ball 2.5% outside every wired body, which is worse than no atmosphere at all.
        /// </summary>
        private static bool CheckMaterial(string id, Material material, string path, List<string> failed)
        {
            if (material.shader == null)
            {
                failed.Add($"{id}: {path} has no shader");
                Debug.LogError($"AtmosphereShellBuilder: '{path}' has no shader; '{id}' not wired.");
                return false;
            }

            if (ShaderUtil.ShaderHasError(material.shader))
            {
                failed.Add($"{id}: shader '{material.shader.name}' has errors");
                Debug.LogError($"AtmosphereShellBuilder: shader '{material.shader.name}' on '{path}' has " +
                               $"compile errors; '{id}' not wired rather than shipping a magenta shell.");
                return false;
            }

            if (material.shader.name != ExpectedShader)
            {
                // Not fatal — someone may be trying another rim shader — but it is worth saying out loud.
                Debug.LogWarning($"AtmosphereShellBuilder: '{path}' uses '{material.shader.name}', not " +
                                 $"'{ExpectedShader}'. Wiring it anyway.");
            }

            return true;
        }

        // ---------- one body

        private static Wired Wire(string id, Material material, List<string> failed)
        {
            var prefabPath = $"{PrefabFolder}/poi_{id}_prefab.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                failed.Add($"{id}: no prefab at {prefabPath}");
                Debug.LogError($"AtmosphereShellBuilder: '{id}' has a material but no prefab at {prefabPath}; " +
                               "nothing changed.");
                return null;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                failed.Add($"{id}: {prefabPath} would not open");
                Debug.LogError($"AtmosphereShellBuilder: {prefabPath} would not open for editing.");
                return null;
            }

            Wired entry = null;
            var save = false;

            try
            {
                entry = Populate(id, root, material, prefabPath, failed);
                save = entry != null;

                if (save)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out save);
                }
            }
            finally
            {
                // Unloading is not optional: a prefab left loaded keeps a hidden preview scene alive for the
                // rest of the editor session, whatever went wrong above.
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (entry == null)
            {
                return null;
            }

            if (!save)
            {
                failed.Add($"{id}: could not save {prefabPath}");
                Debug.LogError($"AtmosphereShellBuilder: could not save {prefabPath}; '{id}' is unchanged.");
                return null;
            }

            return entry;
        }

        private static Wired Populate(string id, GameObject root, Material material, string prefabPath,
                                      List<string> failed)
        {
            var surface = FindSurface(root.transform, id);
            if (surface == null)
            {
                failed.Add($"{id}: no '{id}_sphere_mesh' in the prefab");
                Debug.LogError($"AtmosphereShellBuilder: no '{id}_sphere_mesh' (or '{id}_mesh', " +
                               $"'{id}_planet_mesh') inside {prefabPath}, so there is no sphere to sit outside; " +
                               "nothing changed.");
                return null;
            }

            var filter = surface.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                failed.Add($"{id}: '{surface.name}' has no mesh");
                Debug.LogError($"AtmosphereShellBuilder: '{surface.name}' in {prefabPath} has no MeshFilter " +
                               "mesh, so there is no mesh to reuse; nothing changed.");
                return null;
            }

            var parent = surface.parent;
            if (parent == null)
            {
                // Parenting the shell under the sphere itself would put it inside the GEInteractable's collider
                // sweep and inside every bounds query that measures the body. Refuse rather than do that.
                failed.Add($"{id}: '{surface.name}' is the prefab root");
                Debug.LogError($"AtmosphereShellBuilder: '{surface.name}' is the root of {prefabPath}, so the " +
                               "shell has nowhere to be a sibling of it; nothing changed.");
                return null;
            }

            var shellName = id + ShellSuffix;
            var existing = FindAllNamed(root.transform, shellName);
            var created = existing.Count == 0;

            // Duplicates can only come from a hand edit or an interrupted earlier run. One survives.
            for (var i = 1; i < existing.Count; i++)
            {
                Debug.LogWarning($"AtmosphereShellBuilder: {prefabPath} had {existing.Count} objects named " +
                                 $"'{shellName}'; the extras were removed.");
                Object.DestroyImmediate(existing[i].gameObject);
            }

            var shell = created ? new GameObject(shellName).transform : existing[0];

            if (shell.parent != parent)
            {
                shell.SetParent(parent, false);
            }

            // Last, so a depth-first Renderer query under the body still finds the sphere first — that is the
            // fallback InfoPanel and MoonLabel use when targetBounds is not set — and so a re-run does not
            // reshuffle the existing children.
            shell.SetAsLastSibling();

            var shellScale = surface.localScale * ShellScale;
            var centre = mesh.bounds.center;

            shell.localRotation = surface.localRotation;
            shell.localScale = shellScale;

            // Concentric with the sphere, not with the parent's origin. The two share a mesh, so if that mesh is
            // not centred on its own pivot, scaling by 1.025 about the pivot would walk the shell 2.5% of the
            // offset off the body. Exact for a centred mesh (the correction is zero) and right for one that is not.
            shell.localPosition = surface.localPosition + surface.localRotation *
                (Vector3.Scale(surface.localScale, centre) - Vector3.Scale(shellScale, centre));

            StripInteraction(shell, prefabPath);

            var shellFilter = GetOrAdd<MeshFilter>(shell.gameObject);
            shellFilter.sharedMesh = mesh;

            var renderer = GetOrAdd<MeshRenderer>(shell.gameObject);
            renderer.sharedMaterials = new[] { material };
            // Additive, ZWrite Off, no lighting: a shadow caster, a receiver or a probe fetch here buys nothing
            // and costs fill and a light-probe lookup per body on a Quest 3.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.staticShadowCaster = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            var receiver = GetOrAdd<SunLightReceiver>(shell.gameObject);
            receiver.Sun = null;              // found by name at runtime, the way every other receiver here is
            receiver.SetMaterials = true;     // matches the body's other receivers; only gates the Moon caching
            receiver.UsePropertyBlock = true; // _SunDirection through a MaterialPropertyBlock, not the .mat asset

            // A new child defaults to layer 0; take the group node's layer instead so the shell sits exactly
            // where a hand-added sibling would, and say so if that turns out to be a layer pointers look at.
            shell.gameObject.layer = parent.gameObject.layer;
            var layerName = LayerMask.LayerToName(shell.gameObject.layer);
            foreach (var interactionLayer in InteractionLayers)
            {
                if (layerName == interactionLayer)
                {
                    Debug.LogWarning($"AtmosphereShellBuilder: '{shellName}' landed on layer '{layerName}', " +
                                     "which pointers query. It has no collider so it cannot be hit, but the " +
                                     "layer is wrong for a purely visual child.");
                }
            }

            var surfaceDiameter = Widest(mesh, surface.lossyScale);
            var shellDiameter = Widest(mesh, shell.lossyScale);

            // Read back rather than asserted: where the two mesh centres actually landed in the parent's space.
            // Any non-zero here means the position correction above got the sign or the order wrong, and the
            // shell is sitting off the body by that much.
            var surfaceCentre = surface.localPosition +
                                surface.localRotation * Vector3.Scale(surface.localScale, centre);
            var shellCentre = shell.localPosition + shell.localRotation * Vector3.Scale(shell.localScale, centre);

            // The rim is only visible if no other *surface* reaches past it. A light card can be far wider and
            // still not hide it — see Outermost for why that is a fact about the blend state and not a guess.
            var widestSurface = Outermost(parent, shell, true, out var widestSurfaceName);
            var widestLight = Outermost(parent, shell, false, out var widestLightName);

            return new Wired
            {
                Id = id,
                PrefabPath = prefabPath,
                MaterialName = material.name,
                ParentPath = PathUnder(root.transform, parent),
                SurfaceName = surface.name,
                MeshName = mesh.name,
                Vertices = mesh.vertexCount,
                Triangles = TriangleCount(mesh),
                SurfaceDiameter = surfaceDiameter,
                ShellDiameter = shellDiameter,
                Ratio = surfaceDiameter > 0.0000001f ? shellDiameter / surfaceDiameter : 0f,
                Concentricity = Vector3.Distance(surfaceCentre, shellCentre),
                WidestSurface = widestSurface,
                WidestSurfaceName = widestSurfaceName,
                WidestLight = widestLight,
                WidestLightName = widestLightName,
                // Counted after the strip, so the report states what is on the object rather than what was meant.
                ShellColliders = shell.GetComponents<Collider>().Length,
                ShellInteractables = shell.GetComponents<GEInteractable>().Length,
                SurfaceColliders = surface.GetComponents<Collider>().Length,
                SurfaceInteractables = surface.GetComponents<GEInteractable>().Length,
                Created = created,
                LayerIndex = shell.gameObject.layer,
                LayerName = string.IsNullOrEmpty(layerName) ? "(unnamed)" : layerName,
            };
        }

        /// <summary>
        /// The shell must never be a pointer target. Nothing here adds a collider or an interactable, but an
        /// earlier hand edit might have, and XRI collects colliders off an interactable's own children — so a
        /// re-run takes them away again rather than trusting that nobody touched it.
        /// </summary>
        private static void StripInteraction(Transform shell, string prefabPath)
        {
            foreach (var interactable in shell.GetComponents<GEInteractable>())
            {
                Debug.LogWarning($"AtmosphereShellBuilder: '{shell.name}' in {prefabPath} carried a " +
                                 "GEInteractable; removed, a shell is not a pointer target.");
                Object.DestroyImmediate(interactable);
            }

            foreach (var handler in shell.GetComponents<ManipulationHandler>())
            {
                Object.DestroyImmediate(handler);
            }

            // After the interactable, which registers them.
            foreach (var collider in shell.GetComponents<Collider>())
            {
                Debug.LogWarning($"AtmosphereShellBuilder: '{shell.name}' in {prefabPath} carried a " +
                                 $"{collider.GetType().Name}; removed, the shell must not catch a poke or " +
                                 "enlarge what a hand can grab.");
                Object.DestroyImmediate(collider);
            }
        }

        // ---------- finding the pieces

        /// <summary>
        /// The body's own sphere. Same names and same order as <c>SolarRowBuilder.FindSurface</c>, so the two
        /// always agree about which mesh the planet is.
        /// </summary>
        private static Transform FindSurface(Transform root, string id)
        {
            var named = FindDescendant(root, id + "_sphere_mesh")
                        ?? FindDescendant(root, id + "_mesh")
                        ?? FindDescendant(root, id + "_planet_mesh");
            if (named != null)
            {
                return named;
            }

            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.EndsWith("_sphere_mesh", System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Every object of that name, so a duplicated shell is found rather than added to.</summary>
        private static List<Transform> FindAllNamed(Transform root, string name)
        {
            var found = new List<Transform>();
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    found.Add(candidate);
                }
            }

            return found;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        // ---------- measuring, so the run checks itself

        /// <summary>
        /// The widest extent of <paramref name="mesh"/> once <paramref name="scale"/> is applied, measured in
        /// the mesh's own space and multiplied out rather than taken from a world AABB: every one of these
        /// bodies has an axial tilt above it, and a world AABB of a tilted sphere reads large. Exact for a
        /// sphere, which is all this is pointed at.
        /// </summary>
        private static float Widest(Mesh mesh, Vector3 scale)
        {
            var size = mesh.bounds.size;
            return Mathf.Max(size.x * Mathf.Abs(scale.x),
                Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));
        }

        /// <summary>
        /// Names that are light rather than surface — the same list, and the same reason, as
        /// <c>SolarRowBuilder.NotGeometry</c>. Kept as its own copy because that one is private to that class.
        /// </summary>
        private static readonly string[] NotSurface =
            { "glow", "flare", "halo", "atmosphere", "afforda", "highlight", "trail" };

        private static bool IsSurface(Transform t)
        {
            var name = t.name.ToLowerInvariant();
            foreach (var token in NotSurface)
            {
                if (name.Contains(token))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The widest enabled mesh under the body's group other than the shell, in the group's own units —
        /// asked twice, once of the surfaces and once of the light cards, because only one of the two answers
        /// can bury a rim.
        ///
        /// <para><b>Why the distinction is the whole point.</b> The first run of this builder reported Earth's
        /// shell as buried by <c>earth_glow_mesh</c> at 1.117 of the sphere against the shell's 1.025. That was
        /// this diagnostic's error, not the shell's. <c>earth_glow_mesh</c> carries <see cref="FaceCamera"/>: it
        /// is a screen-facing billboard card, so 1.117 is a card <i>width</i> and not a radius wrapped round the
        /// body, and comparing it to a shell's radius is a category error — exactly the one
        /// <c>SolarRowBuilder.NotGeometry</c>'s comment already warns about ("a glow card is a screen-facing
        /// billboard several times the width of the planet behind it"). On top of that <c>halo_shader</c> draws
        /// <c>Blend SrcAlpha One</c> with <c>ZWrite Off</c> at queue <c>Geometry+70</c> (2070): with a
        /// destination factor of <c>One</c> it can only ever <i>add</i> to the framebuffer, and writing no depth
        /// it cannot depth-reject anything drawn later. Its own texture alpha gates everything it emits, so
        /// where <c>glow_normal_alpha_texture</c> is transparent the card contributes literally nothing. The rim
        /// draws at queue 3000, after it, also additive and also without depth — and two additive layers that
        /// write no depth commute, so the result is their sum whichever order they land in.</para>
        ///
        /// <para>A surface mesh is different: <c>earth_clouds_mesh</c> at 1.01 is a real concentric layer, and a
        /// rim inside <i>that</i> would genuinely be in the wrong place. So the burial test runs against
        /// surfaces only, and the widest light card is reported beside it as information.</para>
        /// </summary>
        private static float Outermost(Transform group, Transform shell, bool surfaces, out string name)
        {
            var widest = 0f;
            name = surfaces ? "(no other surface)" : "(no light cards)";

            foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled || filter.transform == shell ||
                    filter.transform.IsChildOf(shell) || !IsActiveUnder(filter.transform, group) ||
                    IsSurface(filter.transform) != surfaces)
                {
                    continue;
                }

                var extent = Widest(mesh, filter.transform.lossyScale);
                if (extent > widest)
                {
                    widest = extent;
                    name = filter.gameObject.name;
                }
            }

            return widest;
        }

        /// <summary>
        /// Active all the way up to <paramref name="stopAt"/>, walked with <c>activeSelf</c> rather than asked
        /// with <c>activeInHierarchy</c> — the same reason <c>MoonBuilder</c> gives: this runs inside the
        /// preview scene <c>LoadPrefabContents</c> opens, and <c>activeInHierarchy</c> answers a question about
        /// that scene as well as about the subtree. Earth's legacy <c>Earth_LOD1</c> stand-in is switched off in
        /// the prefab, and a disabled mesh must not be reported as burying the rim.
        /// </summary>
        private static bool IsActiveUnder(Transform t, Transform stopAt)
        {
            for (var node = t; node != null; node = node.parent)
            {
                if (!node.gameObject.activeSelf)
                {
                    return false;
                }

                if (node == stopAt)
                {
                    break;
                }
            }

            return true;
        }

        private static long TriangleCount(Mesh mesh)
        {
            long indices = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                indices += mesh.GetIndexCount(i);
            }

            return indices / 3;
        }

        private static string PathUnder(Transform root, Transform node)
        {
            var parts = new List<string>();
            for (var t = node; t != null && t != root; t = t.parent)
            {
                parts.Add(t.name);
            }

            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---------- the report

        /// <summary>
        /// How wide the body's widest light card is as a multiple of its sphere. Reported so the brightness the
        /// rim has to read against is a number in the log rather than something only visible on screen.
        /// </summary>
        private static string LightRatio(Wired entry)
        {
            return entry.SurfaceDiameter > 0.0000001f && entry.WidestLight > 0f
                ? $"{entry.WidestLight / entry.SurfaceDiameter:F3}x the sphere"
                : "none";
        }

        private static void Report(List<Wired> wired, List<string> skipped, List<string> failed)
        {
            var text = new StringBuilder();
            text.AppendLine($"AtmosphereShellBuilder: {wired.Count} shell(s) wired, {skipped.Count} " +
                            $"body/bodies without a material, {failed.Count} failure(s).");
            text.AppendLine($"  target scale {ShellScale:F3} of the body's own sphere " +
                            $"(a {(ShellScale - 1f) * 0.5f * SolarRowDiameter * 1000f:F2} mm gap at Solar Row's " +
                            $"{SolarRowDiameter * 100f:F0} cm bodies)");

            foreach (var entry in wired)
            {
                var ratioOff = Mathf.Abs(entry.Ratio - ShellScale) > 0.0005f ? "  RATIO OFF" : string.Empty;

                // A centre more than a thousandth of the body off is a positioning bug, not rounding.
                var offCentre = entry.SurfaceDiameter > 0.0000001f &&
                                entry.Concentricity / entry.SurfaceDiameter > 0.001f
                    ? "  NOT CONCENTRIC"
                    : string.Empty;

                var stolen = entry.ShellColliders > 0 || entry.ShellInteractables > 0 ? "  SHELL IS TOUCHABLE"
                    : string.Empty;
                var lost = entry.SurfaceColliders == 0 || entry.SurfaceInteractables == 0
                    ? "  SPHERE LOST ITS INPUT"
                    : string.Empty;

                // Surfaces only. A wider additive light card cannot hide the rim, so it must not fail the run.
                var buried = entry.ShellDiameter > entry.WidestSurface
                    ? $"shell clears it by {(entry.ShellDiameter - entry.WidestSurface) * 0.5f:F4}"
                    : "SHELL IS BURIED — a surface layer reaches past the rim";

                text.AppendLine(
                    $"  {entry.Id,-8} {(entry.Created ? "created" : "updated")}  " +
                    $"ratio={entry.Ratio:F4} (want {ShellScale:F3}){ratioOff}");
                text.AppendLine(
                    $"           {entry.ParentPath}/{entry.Id}{ShellSuffix}  layer {entry.LayerIndex} " +
                    $"'{entry.LayerName}'");
                text.AppendLine(
                    $"           sphere '{entry.SurfaceName}' {entry.SurfaceDiameter:F4} -> shell " +
                    $"{entry.ShellDiameter:F4} in prefab units, centres " +
                    $"{entry.Concentricity:F6} apart{offCentre}");
                text.AppendLine(
                    $"           mesh '{entry.MeshName}' reused: {entry.Vertices} verts, {entry.Triangles} " +
                    $"tris added per body");
                text.AppendLine(
                    $"           widest other surface: '{entry.WidestSurfaceName}' at " +
                    $"{entry.WidestSurface:F4} ({buried})");
                text.AppendLine(
                    $"           widest light card:   '{entry.WidestLightName}' at {entry.WidestLight:F4} " +
                    $"({LightRatio(entry)}) — additive, no depth write, cannot occlude the rim");
                text.AppendLine(
                    $"           shell: {entry.ShellColliders} collider(s), {entry.ShellInteractables} " +
                    $"interactable(s){stolen} | sphere keeps {entry.SurfaceColliders} collider(s), " +
                    $"{entry.SurfaceInteractables} interactable(s){lost}");
                text.AppendLine(
                    $"           material '{entry.MaterialName}', SunLightReceiver via MaterialPropertyBlock, " +
                    $"saved into {entry.PrefabPath}");
            }

            if (skipped.Count > 0)
            {
                text.AppendLine("  not wired (no material — expected for every body without a recipe in " +
                                "AtmosphereMaterialBuilder):");
                foreach (var line in skipped)
                {
                    text.AppendLine($"    {line}");
                }
            }

            if (failed.Count > 0)
            {
                text.AppendLine("  failures:");
                foreach (var line in failed)
                {
                    text.AppendLine($"    {line}");
                }
            }

            if (wired.Count > 0)
            {
                text.AppendLine("  next: Cosmic Simulation > Build Solar Row Content, then Build Moons — the " +
                                "generated content prefab holds an unpacked copy of these bodies and will not " +
                                "see the shell until it is rebuilt. The orbit model (solar_system_prefab) " +
                                "references the source prefabs and already has it.");
            }

            if (failed.Count > 0)
            {
                Debug.LogError(text.ToString());
            }
            else
            {
                Debug.Log(text.ToString());
            }
        }
    }
}
