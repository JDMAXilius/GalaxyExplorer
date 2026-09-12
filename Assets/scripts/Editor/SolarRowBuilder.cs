// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicSimulation;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the content of the <b>Solar System Planets</b> experience (GDD 4.1): ten bodies at arm's reach that
    /// the player grabs, grows and compares.
    ///
    /// <para><b>A prefab, not a scene.</b> The ticket asked for a <c>solar_row_scene</c>; CS-036 made that
    /// unnecessary. <see cref="ExperienceDirector"/> now opens a module from its
    /// <see cref="ExperienceModule.ContentPrefab"/> when it names no scene, spawning it under a content root that
    /// hangs off the <c>ViewLoader</c> and destroying it on the way out. A scene here would buy nothing but load
    /// time and one more thing to keep in sync, so this writes one prefab and hangs it on the module.</para>
    ///
    /// <para><b>Nothing is authored here.</b> Every body's geometry and materials come out of the existing
    /// <c>poi_&lt;id&gt;_prefab</c> under <c>Assets/prefabs/poi_prefabs</c> — the same prefabs the orbit model
    /// uses. What is lifted out of each one is the <c>*_tilt</c> subtree: the axial tilt, the constant-rotation
    /// node under it, and the planet's sphere, clouds, rings, glow and (for the Sun) flares. Everything the orbit
    /// model wraps around that — the POI card, the orbit trail, the LOD shell, the offset scale controller, the
    /// highlighter, the moons — is left behind, and the interaction components that were on the mesh are stripped
    /// so the new pattern can put one set on the body root.</para>
    ///
    /// <para><b>Normalisation.</b> A <see cref="LayoutSlot.Scale"/> is a diameter in metres (the convention
    /// CS-040 set), which is only true if a body is 1 m across at <c>localScale</c> 1. The bodies as authored are
    /// nothing of the sort — they are sized for an orbit model, and each FBX has its own idea of a unit. So the
    /// copied visual is parented under a <c>visual</c> node whose uniform scale is <c>1 / d</c>, where <c>d</c> is
    /// the measured diameter of that body's own sphere mesh. The measurement is taken per axis in the mesh's own
    /// space and multiplied by the accumulated scale — never as a world AABB, because the axial tilt would
    /// inflate a rotated sphere's box by up to 40 per cent and the normalisation with it. After that the body
    /// root's <c>localScale</c> <i>is</i> its diameter, the grab sphere's radius 0.5 <i>is</i> its surface, and
    /// <see cref="ScaleLimits"/>' metre limits mean what they say.</para>
    ///
    /// <para><b>The body pattern.</b> Everything the player touches sits on the body root, the way
    /// <see cref="NebulaPrefabBuilder"/> arranges a nebula: a sphere collider, a <c>GEInteractable</c> for it, a
    /// <c>ManipulationHandler</c> whose host is that same transform, <see cref="ScaleLimits"/> on Body limits
    /// (5 cm to 3 m), a <c>SolverHandler</c> and <c>ForceSolver</c> for the pull, and a
    /// <see cref="FreePlacementSolver"/> so a released body stays put. Keeping the collider, the handler and the
    /// limits on one object matters: <c>ManipulationHandler</c> looks for its scale constraint with
    /// <c>GetComponent</c>, and <c>ScaleLimits</c> measures the transform it is on, so splitting them the way the
    /// old prefabs did would silently mis-scale every clamp.</para>
    ///
    /// <para><b>Why the plain <c>ForceSolver</c> and not <c>PlanetForceSolver</c>.</b> The planet subclass forces
    /// <c>GoalScale = Vector3.one</c> every frame a body is in its Root state, which would blow every body up to
    /// 1 m and destroy both arrangements, and it dereferences a <c>PlanetHighlighter</c> without a null check on
    /// every state change. Both are fine in the orbit model, where the scale controller and the highlighter are
    /// present; neither survives normalisation. The behaviour worth keeping from it — growing to 25 cm on the way
    /// to the hand — is in <see cref="LayoutRig"/> instead.</para>
    ///
    /// Re-running rewrites the prefab in place, so its GUID and the module's reference to it survive.
    /// Menu: <b>Cosmic Simulation -> Build Solar Row Content</b>.
    /// </summary>
    public static class SolarRowBuilder
    {
        private const string ModulePath = "Assets/data/experiences/solar_system_planets.asset";
        private const string BodyFolder = "Assets/data/bodies";
        private const string LayoutFolder = "Assets/data/layouts";
        private const string SourceFolder = "Assets/prefabs/poi_prefabs";
        private const string PanelPrefabPath = "Assets/prefabs/ui/info_panel_prefab.prefab";
        private const string TractorBeamPath = "Assets/prefabs/tractor_beam_prefab.prefab";
        private const string OutputFolder = "Assets/prefabs/experiences";
        private const string OutputName = "solar_system_planets_content_prefab";

        /// <summary>The arrangement the prefab is authored in, and the one the experience opens with.</summary>
        private const string DefaultLayoutId = "solar_row";

        /// <summary>GDD 4.1: small bodies keep a 6 cm invisible grab sphere so they can still be pinched.</summary>
        private const float GrabDiameter = 0.06f;

        /// <summary>GDD 4.1: a body pulled out grows to 25 cm.</summary>
        private const float PullDiameter = 0.25f;

        /// <summary>GDD 4.1 / 8.7: a layout change takes 0.8 s, and so does a body coming home.</summary>
        private const float RestoreSeconds = 0.8f;

        /// <summary>
        /// CS-060: how much room a ring's edge is left from whatever stands next to it, in metres. The GDD fixes
        /// the pitch (25 cm) and the planet diameter (15 cm) but says nothing about ring span, and Saturn's rings
        /// are 2.26 planet diameters wide, so in Solar Row the two ring systems intersect. <see cref="LayoutRig"/>
        /// shrinks them to what the arrangement leaves room for; this is the only number that decision needs.
        /// </summary>
        private const float RingClearance = 0.01f;

        /// <summary>The ten bodies, in the order GDD 4.1 lists them.</summary>
        private static readonly string[] Order =
        {
            "sun", "mercury", "venus", "earth", "mars", "jupiter", "saturn", "uranus", "neptune", "pluto",
        };

        /// <summary>
        /// Components that belong to the orbit model, not to a body the player arranges. Ordered so that a
        /// component is always removed before the one it requires: <c>PostManipulationResetter</c> and
        /// <c>ScaleLimits</c> both require a <c>ManipulationHandler</c>, and <c>GEInteractable</c> wants the
        /// collider it registered.
        /// </summary>
        private static readonly System.Type[] StripInOrder =
        {
            typeof(UiPreviewTarget),
            typeof(GEInteractable),
            typeof(PostManipulationResetter),
            typeof(ScaleLimits),
            typeof(ManipulationHandler),
            typeof(Collider),
            typeof(PlanetHighlighter),
            typeof(PlanetOffsetScaleController),
            typeof(Moon),
            typeof(GalaxyExplorer.Fader),
        };

        /// <summary>What one body ended up being, so the run can report itself.</summary>
        private class Built
        {
            public string Id;
            public BodyInfo Info;
            public GameObject Root;
            public Transform Home;
            public ForceSolver Force;
            public FreePlacementSolver Placement;
            public SphereCollider Grab;
            public InfoPanel Panel;
            public Transform SurfaceNode;
            public Transform Rings;
            public Vector3 RingBase;

            // Everything the report prints is a copy taken while the hierarchy was alive: the report runs after
            // the temporary objects have been destroyed, and a destroyed Transform cannot even be asked its name.
            public string Source;
            public string TiltNode;
            public string SurfaceName;
            public string Mesh;
            public string Material;
            public string RingName;
            public bool HasRings;          // Rings itself is a destroyed Transform by the time the report runs

            public float SourceDiameter;   // as authored, in the source prefab's own units
            public float Normalised;       // measured after normalisation; should read 1.000
            public float VisualSpan;       // widest solid extent (rings, clouds) as a multiple of the diameter
            public float RingSpan;         // the ring mesh alone, as a multiple of the diameter; 0 without rings
            public float Diameter;         // final, from the layout slot
            public Vector3 Position;       // final, local to the content root
            public int Stripped;
            public int Renderers;
            public int Vertices;
        }

        [MenuItem("Cosmic Simulation/Build Solar Row Content")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("SolarRowBuilder: leave play mode first.");
                return;
            }

            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(ModulePath);
            if (module == null)
            {
                Debug.LogError($"SolarRowBuilder: no experience module at {ModulePath}.");
                return;
            }

            var layout = LoadLayout(DefaultLayoutId) ??
                         (module.Layouts != null && module.Layouts.Length > 0 ? module.Layouts[0] : null);
            if (layout == null)
            {
                Debug.LogError("SolarRowBuilder: no layout to author the prefab in. Run " +
                               "Cosmic Simulation > Build Layout Presets first.");
                return;
            }

            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (panelPrefab == null)
            {
                Debug.LogWarning($"SolarRowBuilder: no {PanelPrefabPath}; the bodies get no panels. Run " +
                                 "Cosmic Simulation > Build UI Prefabs and re-run this.");
            }

            var beam = AssetDatabase.LoadAssetAtPath<GameObject>(TractorBeamPath);
            if (beam == null)
            {
                Debug.LogWarning($"SolarRowBuilder: no {TractorBeamPath}; force-pull will show no tractor beam.");
            }

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();

            var root = new GameObject(OutputName);
            var tracker = BuildTracker(root.transform);
            var homes = Group("homes", root.transform);
            var group = Group("bodies", root.transform);
            var panels = Group("panels", root.transform);

            var built = new List<Built>();
            var missing = new List<string>();

            foreach (var id in Order)
            {
                var entry = BuildBody(id, layout, group, homes, panels, panelPrefab, beam, tracker);
                if (entry == null)
                {
                    missing.Add(id);
                    continue;
                }

                built.Add(entry);
            }

            if (built.Count == 0)
            {
                Object.DestroyImmediate(root);
                Debug.LogError("SolarRowBuilder: not one body could be built; nothing written.");
                return;
            }

            WireSunlight(root.transform, built);
            WireSaturnRings(built);

            var rig = root.AddComponent<LayoutRig>();
            WireRig(rig, module, layout, built);

            var path = $"{OutputFolder}/{OutputName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError($"SolarRowBuilder: could not save {path}; the module is unchanged.");
                return;
            }

            Assign(module, saved);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report(built, missing, layout, module, path);
        }

        // ---------- one body

        private static Built BuildBody(string id, LayoutPreset layout, Transform group, Transform homes,
                                       Transform panels, GameObject panelPrefab, GameObject beam,
                                       ControllerTransformTracker tracker)
        {
            var sourcePath = $"{SourceFolder}/poi_{id}_prefab.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"SolarRowBuilder: no source prefab at {sourcePath}; '{id}' skipped.");
                return null;
            }

            var info = LoadBodyInfo(id);
            if (info == null)
            {
                Debug.LogWarning($"SolarRowBuilder: no BodyInfo with Id '{id}' in {BodyFolder}; the body is " +
                                 "built but its panel will be blank.");
            }

            var slot = layout.Find(id);
            if (!slot.HasValue)
            {
                Debug.LogError($"SolarRowBuilder: layout '{layout.Id}' has no slot for '{id}'; skipped.");
                return null;
            }

            // A plain copy with every nested prefab materialised: the subtree is about to be taken apart, and a
            // prefab instance will not let its children be reparented out of it.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var surface = FindSurface(instance.transform, id);
            if (surface == null)
            {
                Object.DestroyImmediate(instance);
                Debug.LogError($"SolarRowBuilder: no '{id}_sphere_mesh' (or '{id}_mesh') inside {sourcePath}; " +
                               "nothing to measure, so '" + id + "' is skipped.");
                return null;
            }

            var tilt = FindTilt(surface, instance.transform);

            var bodyRoot = new GameObject("body_" + id);
            bodyRoot.transform.SetParent(group, false);

            var visual = new GameObject("visual");
            visual.transform.SetParent(bodyRoot.transform, false);

            tilt.SetParent(visual.transform, false);
            Object.DestroyImmediate(instance);

            var stripped = Strip(tilt);

            // ---- normalise to a 1 m diameter, measured on the body's own sphere and nothing else
            if (!Measure(bodyRoot.transform, surface, out var sourceDiameter, out _))
            {
                Object.DestroyImmediate(bodyRoot);
                Debug.LogError($"SolarRowBuilder: '{id}' has no measurable mesh under '{surface.name}'; skipped.");
                return null;
            }

            visual.transform.localScale = Vector3.one / sourceDiameter;

            // Some source prefabs do not have the planet at their own pivot — Jupiter is nested two prefabs deep
            // and lands 18 cm out, which is larger than the body itself at Solar Row's 15 cm. Left alone the body
            // would spin about a point outside itself and sit off its layout slot, so shift the visual back by
            // however far the sphere's centre missed.
            var centreOffset = CentreOffset(bodyRoot.transform, surface);
            if (centreOffset.magnitude > 0.001f)
            {
                visual.transform.localPosition -= centreOffset;
            }

            Measure(bodyRoot.transform, surface, out var normalised, out var offsetMillimetres);
            Measure(bodyRoot.transform, visual.transform, out var visualSpan, out _);

            // Measured here, before the body root is scaled to its slot, so every span below is already a
            // multiple of the body's own diameter and holds at whatever size a layout later gives it.
            var rings = FindRings(visual.transform, surface, normalised, out var ringSpan);
            var ringRatio = rings != null ? ringSpan / Mathf.Max(0.0001f, normalised) : 0f;

            if (offsetMillimetres > 1f)
            {
                Debug.LogWarning($"SolarRowBuilder: '{id}' still sits {offsetMillimetres:F1} mm off its own root " +
                                 "after re-centring, so it will not spin about its centre.");
            }

            var surfaceRenderer = LargestRenderer(surface);

            // ---- the parts the player touches, all on the body root
            var grab = bodyRoot.AddComponent<SphereCollider>();
            grab.radius = 0.5f; // the normalised body's own surface
            bodyRoot.AddComponent<GEInteractable>();

            var manipulation = bodyRoot.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            manipulationSo.FindProperty("manipulationType").enumValueIndex =
                (int)ManipulationHandler.HandMovementType.OneAndTwoHanded;
            // GDD 4.1: two hands scale it and rotate it freely.
            manipulationSo.FindProperty("twoHandedManipulationType").enumValueIndex =
                (int)ManipulationHandler.TwoHandedManipulation.MoveRotateScale;
            manipulationSo.FindProperty("allowFarManipulation").boolValue = true;
            manipulationSo.ApplyModifiedPropertiesWithoutUndo();

            var limits = bodyRoot.AddComponent<ScaleLimits>();
            var limitsSo = new SerializedObject(limits);
            limitsSo.FindProperty("kind").enumValueIndex = (int)ScaleLimits.Kind.Body; // 5 cm to 3 m
            // Measured from the planet alone: a glow card or a ring system is much wider than the body, and the
            // GDD's limits are about how big the body looks.
            limitsSo.FindProperty("measureFrom").objectReferenceValue = surfaceRenderer;
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- home, and the pull
            var home = new GameObject("home_" + id).transform;
            home.SetParent(homes, false);
            home.localPosition = slot.Value.LocalPosition;
            home.localRotation = Quaternion.Euler(slot.Value.LocalEuler);

            var handler = bodyRoot.AddComponent<SolverHandler>();
            var force = bodyRoot.AddComponent<ForceSolver>();
            force.RootTransform = home;
            force.ManipulationHandler = manipulation;
            force.AttractionCollider = grab;
            force.ControllerTracker = tracker;
            force.TractionBeamPrefab = beam;
            force.AttractionDwellDuration = 2f;
            force.AttractionDwellForgiveness = 0.5f;
            force.OffsetToObjectBoundsFromController = true;
            force.AvergeSides = true;
            force.EnableForce = true;

            var forceSo = new SerializedObject(force);
            forceSo.FindProperty("SolverHandler").objectReferenceValue = handler;
            forceSo.FindProperty("moveLerpTime").floatValue = 0.3f;
            forceSo.FindProperty("rotateLerpTime").floatValue = 0.3f;
            forceSo.FindProperty("scaleLerpTime").floatValue = 0.3f;
            forceSo.FindProperty("maintainScale").boolValue = false;
            forceSo.FindProperty("smoothing").boolValue = true;
            forceSo.ApplyModifiedPropertiesWithoutUndo();

            var placement = bodyRoot.AddComponent<FreePlacementSolver>();
            var placementSo = new SerializedObject(placement);
            placementSo.FindProperty("restoreSeconds").floatValue = RestoreSeconds;
            placementSo.ApplyModifiedPropertiesWithoutUndo();

            if (id == "sun")
            {
                AddSunTouch(bodyRoot, visual.transform, surfaceRenderer);
            }

            // ---- place it, in the arrangement the prefab is authored in
            bodyRoot.transform.localPosition = slot.Value.LocalPosition;
            bodyRoot.transform.localRotation = Quaternion.Euler(slot.Value.LocalEuler);
            bodyRoot.transform.localScale = Vector3.one * Mathf.Max(0.0001f, slot.Value.Scale);
            grab.radius = Mathf.Max(0.5f, GrabDiameter * 0.5f / Mathf.Max(0.0001f, slot.Value.Scale));

            var panel = BuildPanel(id, info, panelPrefab, panels, bodyRoot.transform, surfaceRenderer, force);

            var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
            var vertices = 0;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    vertices += filter.sharedMesh.vertexCount;
                }
            }

            var surfaceFilter = surfaceRenderer != null ? surfaceRenderer.GetComponent<MeshFilter>() : null;

            return new Built
            {
                Id = id,
                Info = info,
                Root = bodyRoot,
                Home = home,
                Force = force,
                Placement = placement,
                Grab = grab,
                Panel = panel,
                SurfaceNode = surface,
                Rings = rings,
                RingBase = rings != null ? rings.localScale : Vector3.one,
                RingName = rings != null ? rings.name : "none",
                HasRings = rings != null,
                RingSpan = ringRatio,
                Source = sourcePath,
                TiltNode = tilt.name,
                SurfaceName = surface.name,
                Mesh = surfaceFilter != null && surfaceFilter.sharedMesh != null
                    ? AssetPath(surfaceFilter.sharedMesh) + " / " + surfaceFilter.sharedMesh.name
                    : "none",
                Material = surfaceRenderer != null && surfaceRenderer.sharedMaterial != null
                    ? AssetPath(surfaceRenderer.sharedMaterial)
                    : "none",
                SourceDiameter = sourceDiameter,
                Normalised = normalised,
                VisualSpan = visualSpan,
                Diameter = slot.Value.Scale,
                Position = slot.Value.LocalPosition,
                Stripped = stripped,
                Renderers = renderers.Length,
                Vertices = vertices,
            };
        }

        private static InfoPanel BuildPanel(string id, BodyInfo info, GameObject panelPrefab, Transform panels,
                                            Transform target, Renderer bounds, ForceSolver force)
        {
            if (panelPrefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, panels);
            instance.name = "panel_" + id;
            instance.transform.localPosition = Vector3.zero;

            var panel = instance.GetComponent<InfoPanel>();
            if (panel == null)
            {
                Debug.LogWarning($"SolarRowBuilder: {PanelPrefabPath} has no InfoPanel; '{id}' gets no panel.");
                Object.DestroyImmediate(instance);
                return null;
            }

            var so = new SerializedObject(panel);
            so.FindProperty("variant").enumValueIndex = (int)InfoPanel.Variant.Body;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("targetBounds").objectReferenceValue = bounds;
            // A body panel is open exactly while the body is out of its arrangement, which the solver knows.
            so.FindProperty("body").objectReferenceValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The copy itself is written at runtime: InfoPanel reads a BodyInfo rather than storing one, so
            // LayoutRig binds it on Awake. Nothing here can serialise the text.
            if (info == null)
            {
                Debug.LogWarning($"SolarRowBuilder: '{id}' has no BodyInfo, so its panel stays empty.");
            }

            return panel;
        }

        private static void AddSunTouch(GameObject bodyRoot, Transform centre, Renderer surface)
        {
            // CS-046: SunTouchResponse has to sit on the same GameObject as the Sun's ForceSolver, because that
            // is where ExecuteHierarchy stops.
            var touch = bodyRoot.AddComponent<SunTouchResponse>();
            var so = new SerializedObject(touch);
            so.FindProperty("centre").objectReferenceValue = centre;

            var surfaces = so.FindProperty("surfaces");
            surfaces.arraySize = surface != null ? 1 : 0;
            if (surface != null)
            {
                surfaces.GetArrayElementAtIndex(0).objectReferenceValue = surface;
            }

            // Half the normalised body, multiplied by whatever the Sun is currently scaled to.
            so.FindProperty("radiusMetres").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- finding the pieces inside a source prefab

        /// <summary>The body's own sphere: the mesh the diameter is measured on.</summary>
        private static Transform FindSurface(Transform instance, string id)
        {
            var named = FindDescendant(instance, id + "_sphere_mesh")
                        ?? FindDescendant(instance, id + "_mesh")
                        ?? FindDescendant(instance, id + "_planet_mesh");
            if (named != null)
            {
                return named;
            }

            foreach (var candidate in instance.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name.EndsWith("_sphere_mesh", System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The node carrying the axial tilt, which is the whole visual. Found by walking up from the sphere
        /// rather than by name: Mercury's tilt node is called <c>mars_tilt</c> in the original prefab.
        /// </summary>
        private static Transform FindTilt(Transform surface, Transform stopAt)
        {
            for (var t = surface; t != null && t != stopAt; t = t.parent)
            {
                if (t.name.EndsWith("_tilt", System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            // No node named for the tilt: take everything hanging under the offset scale controller instead.
            for (var t = surface; t != null && t.parent != null && t.parent != stopAt; t = t.parent)
            {
                if (t.parent.name.EndsWith("_scale_controller", System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return surface.parent != null && surface.parent != stopAt ? surface.parent : surface;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            foreach (var candidate in parent.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int Strip(Transform subtree)
        {
            var removed = 0;
            foreach (var type in StripInOrder)
            {
                foreach (var component in subtree.GetComponentsInChildren(type, true))
                {
                    if (component != null)
                    {
                        Object.DestroyImmediate(component);
                        removed++;
                    }
                }
            }

            return removed;
        }

        // ---------- measuring

        /// <summary>
        /// Names that are light rather than surface. A glow card is a screen-facing billboard several times the
        /// width of the planet behind it and the Sun's flares reach further still; counting either as geometry
        /// would put every body's diameter out by a factor and report overlaps that are not there.
        ///
        /// <para><c>"atmosphere"</c> is here for the same reason, and is what <see cref="FindRings"/>' comment
        /// already asks for: <c>AtmosphereShellBuilder</c> puts an additive rim shell 2.5% outside a body, and
        /// letting that set <c>visualSpan</c> would hand the ring-clearance solver a clearance driven by a
        /// transparent glow rather than by solid geometry. No object in the project was named for it before
        /// that builder existed, so adding the token changes no measurement taken so far.</para>
        /// </summary>
        private static readonly string[] NotGeometry =
            { "glow", "flare", "halo", "atmosphere", "afforda", "highlight", "trail" };

        private static bool IsGeometry(Transform t)
        {
            var name = t.name.ToLowerInvariant();
            foreach (var token in NotGeometry)
            {
                if (name.Contains(token))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The diameter of the solid geometry under <paramref name="subject"/>, in metres of
        /// <paramref name="bodyRoot"/>'s space, and how far off that root the widest mesh sits.
        ///
        /// Each mesh is measured in its own space and multiplied by its accumulated scale, so a rotation on the
        /// way down — every one of these bodies has an axial tilt — cannot inflate the answer the way a world
        /// AABB would. That is exact for a sphere, which is all this is ever pointed at.
        /// </summary>
        private static bool Measure(Transform bodyRoot, Transform subject, out float diameter, out float offsetMillimetres)
        {
            diameter = 0f;
            offsetMillimetres = 0f;
            var found = false;

            foreach (var filter in subject.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsGeometry(filter.transform))
                {
                    continue;
                }

                var scale = filter.transform.lossyScale;
                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x * Mathf.Abs(scale.x),
                    Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));

                // The planet is the widest piece of geometry, not the one that reaches furthest. Jupiter's cloud
                // prefab carries decorative surface meshes - inner_spot, double_stream_01 - which are barely a
                // centimetre across but sit 33 cm out in model space. Sizing by reach picked one of those and
                // measured Jupiter at 67 cm instead of its true 10 cm, which would have normalised its visible
                // sphere down to about a sixth of every other body in the row.
                if (widest > diameter)
                {
                    diameter = widest;
                    var centre = bodyRoot.InverseTransformPoint(filter.transform.TransformPoint(mesh.bounds.center));
                    offsetMillimetres = centre.magnitude * 1000f;
                }

                found = true;
            }

            return found && diameter > 0.0001f;
        }

        /// <summary>
        /// Where the body's own sphere sits relative to the root it is supposed to turn about, in root space.
        /// Same selection rule as <see cref="Measure"/> — the widest piece of real geometry, glow and flares
        /// excluded — so the two always agree about which mesh is the planet.
        /// </summary>
        private static Vector3 CentreOffset(Transform bodyRoot, Transform subject)
        {
            var offset = Vector3.zero;
            var widestSeen = 0f;

            foreach (var filter in subject.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled ||
                    !filter.gameObject.activeInHierarchy || !IsGeometry(filter.transform))
                {
                    continue;
                }

                var scale = filter.transform.lossyScale;
                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x * Mathf.Abs(scale.x),
                    Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));

                if (widest > widestSeen)
                {
                    widestSeen = widest;
                    offset = bodyRoot.InverseTransformPoint(filter.transform.TransformPoint(mesh.bounds.center));
                }
            }

            return offset;
        }

        /// <summary>
        /// The body's ring mesh, when it has one, and how wide it is in <paramref name="visual"/>'s space.
        ///
        /// Two tests, both needed. The name — on the object, its mesh or its material — because a ring is the
        /// one piece of a body that may legitimately be shrunk, and a cloud shell or an atmosphere sitting a few
        /// per cent outside the planet must never be picked up by mistake. And the width, because a "ring" that
        /// is no wider than the planet is nothing the arrangement has to make room for. A body with neither is
        /// simply a body without rings: the clamp does nothing for it and the run says so.
        /// </summary>
        private static Transform FindRings(Transform visual, Transform surface, float sphereDiameter, out float span)
        {
            Transform best = null;
            span = 0f;

            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (filter.transform == surface || !IsGeometry(filter.transform))
                {
                    continue;
                }

                var material = renderer.sharedMaterial;
                if (!NamesRings(filter.gameObject.name) && !NamesRings(mesh.name) &&
                    !(material != null && NamesRings(material.name)))
                {
                    continue;
                }

                var scale = filter.transform.lossyScale;
                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x * Mathf.Abs(scale.x),
                    Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));

                if (widest <= sphereDiameter * 1.05f || widest <= span)
                {
                    continue;
                }

                span = widest;
                best = filter.transform;
            }

            if (best == null)
            {
                span = 0f;
            }

            return best;
        }

        private static bool NamesRings(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf("ring", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Renderer LargestRenderer(Transform subject)
        {
            Renderer best = null;
            var bestSize = -1f;

            foreach (var renderer in subject.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !IsGeometry(renderer.transform))
                {
                    continue;
                }

                var scale = renderer.transform.lossyScale;
                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x * Mathf.Abs(scale.x),
                    Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));

                if (widest > bestSize)
                {
                    bestSize = widest;
                    best = renderer;
                }
            }

            return best;
        }

        // ---------- shared parts and cross-body wiring

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static ControllerTransformTracker BuildTracker(Transform parent)
        {
            // One tracker for all ten bodies. Without it every ForceSolver builds its own at runtime and warns
            // about it. It writes its own world pose every frame, so living inside content that the director
            // scales on the way in costs it nothing.
            var go = new GameObject("controller_tracker");
            go.transform.SetParent(parent, false);

            var tracker = go.AddComponent<ControllerTransformTracker>();
            var so = new SerializedObject(tracker);
            so.FindProperty("handOffsetRotation").vector3Value = new Vector3(90f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return tracker;
        }

        /// <summary>
        /// Points every <c>SunLightReceiver</c> at our own Sun. Left unset it calls <c>GameObject.Find("Sun")</c>
        /// from <c>LateUpdate</c>, once per receiver per frame, forever — a whole-scene search on a Quest 3.
        /// </summary>
        private static void WireSunlight(Transform root, List<Built> built)
        {
            var sun = built.FirstOrDefault(b => b.Id == "sun");
            var target = sun != null ? (sun.SurfaceNode != null ? sun.SurfaceNode : sun.Root.transform) : null;
            if (target == null)
            {
                Debug.LogWarning("SolarRowBuilder: no Sun was built, so the other bodies have no light direction.");
                return;
            }

            var wired = 0;
            foreach (var receiver in root.GetComponentsInChildren<GalaxyExplorer.SunLightReceiver>(true))
            {
                receiver.Sun = target;
                EditorUtility.SetDirty(receiver);
                wired++;
            }

            Debug.Log($"SolarRowBuilder: {wired} SunLightReceiver components pointed at '{target.name}'.");
        }

        /// <summary>
        /// <c>SaturnVisual</c> reads a content root's scale every <c>LateUpdate</c> and dereferences it without a
        /// check; in the orbit model that was the view root. Here the body is its own root, so the ring radii
        /// follow the body's size rather than a scene's.
        /// </summary>
        private static void WireSaturnRings(List<Built> built)
        {
            var saturn = built.FirstOrDefault(b => b.Id == "saturn");
            if (saturn == null)
            {
                return;
            }

            foreach (var visual in saturn.Root.GetComponentsInChildren<GalaxyExplorer.SaturnVisual>(true))
            {
                visual.contentRoot = saturn.Root;
                EditorUtility.SetDirty(visual);
                Debug.Log("SolarRowBuilder: SaturnVisual now reads Saturn's own scale for its ring radii. Its " +
                          "_InnerRingRadius/_OuterRingRadius will want retuning on the headset (CS-048).");
            }
        }

        private static void WireRig(LayoutRig rig, ExperienceModule module, LayoutPreset layout, List<Built> built)
        {
            var so = new SerializedObject(rig);
            so.FindProperty("module").objectReferenceValue = module;
            so.FindProperty("startLayout").objectReferenceValue = layout;
            so.FindProperty("minGrabDiameter").floatValue = GrabDiameter;
            so.FindProperty("pullDiameter").floatValue = PullDiameter;
            so.FindProperty("pullGrowSeconds").floatValue = 0.35f;
            so.FindProperty("ringClearance").floatValue = RingClearance;

            var list = so.FindProperty("bodies");
            list.arraySize = built.Count;
            for (var i = 0; i < built.Count; i++)
            {
                var entry = built[i];
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Id").stringValue = entry.Id;
                element.FindPropertyRelative("Info").objectReferenceValue = entry.Info;
                element.FindPropertyRelative("Root").objectReferenceValue = entry.Root.transform;
                element.FindPropertyRelative("Home").objectReferenceValue = entry.Home;
                element.FindPropertyRelative("Force").objectReferenceValue = entry.Force;
                element.FindPropertyRelative("Placement").objectReferenceValue = entry.Placement;
                element.FindPropertyRelative("Grab").objectReferenceValue = entry.Grab;
                element.FindPropertyRelative("Panel").objectReferenceValue = entry.Panel;
                element.FindPropertyRelative("Rings").objectReferenceValue = entry.Rings;
                element.FindPropertyRelative("RingSpanRatio").floatValue = entry.RingSpan;
                element.FindPropertyRelative("RingBaseScale").vector3Value = entry.RingBase;
                element.FindPropertyRelative("SpanRatio").floatValue = entry.VisualSpan;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Resizing a SerializedProperty array of a nested serializable class is the only way in, and it is
            // the one step here that fails quietly. Read the rig's own lookup back to prove every entry landed.
            var wired = built.Count(entry => rig.Find(entry.Id) != null);
            if (wired != built.Count)
            {
                Debug.LogError($"SolarRowBuilder: LayoutRig kept only {wired} of {built.Count} bodies; the ones " +
                               "it lost will not follow an arrangement change.");
            }
        }

        private static void Assign(ExperienceModule module, GameObject prefab)
        {
            var so = new SerializedObject(module);
            so.FindProperty("ContentPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            if (!string.IsNullOrEmpty(module.SceneName))
            {
                Debug.LogWarning($"SolarRowBuilder: '{module.Id}' still names the scene '{module.SceneName}', and " +
                                 "ExperienceDirector prefers a scene over a prefab. Clear it (Cosmic Simulation > " +
                                 "Wire Experiences) or the new content will never open.");
            }
        }

        // ---------- loading

        private static LayoutPreset LoadLayout(string id)
        {
            var direct = AssetDatabase.LoadAssetAtPath<LayoutPreset>($"{LayoutFolder}/{id}.asset");
            if (direct != null)
            {
                return direct;
            }

            return AssetDatabase.FindAssets("t:LayoutPreset", new[] { LayoutFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<LayoutPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(p => p != null && p.Id == id);
        }

        private static BodyInfo LoadBodyInfo(string id)
        {
            var direct = AssetDatabase.LoadAssetAtPath<BodyInfo>($"{BodyFolder}/{id}.asset");
            if (direct != null && direct.Id == id)
            {
                return direct;
            }

            return AssetDatabase.FindAssets("t:BodyInfo", new[] { BodyFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<BodyInfo>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(b => b != null && b.Id == id);
        }

        private static string AssetPath(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "(embedded)" : path;
        }

        // ---------- checking the run

        /// <summary>
        /// Prints what was actually built, measured rather than intended, so a run can be checked against
        /// GDD 4.1 without opening the prefab: how many bodies, what each one was made from, how big it came out,
        /// where it sits, and how close any two of them come to touching.
        /// </summary>
        private static void Report(List<Built> built, List<string> missing, LayoutPreset layout,
                                   ExperienceModule module, string path)
        {
            var text = new StringBuilder();
            text.AppendLine($"SolarRowBuilder: {built.Count} of {Order.Length} bodies written to {path}, " +
                            $"authored in '{layout.DisplayName}' ({layout.Slots.Length} slots, " +
                            $"{layout.TransitionSeconds:F2} s transition) and hung on module '{module.Id}'.");

            if (missing.Count > 0)
            {
                text.AppendLine($"  MISSING: {string.Join(", ", missing)}");
            }

            var vertices = 0;
            var renderers = 0;

            foreach (var entry in built)
            {
                var grabDiameter = Mathf.Max(entry.Diameter, GrabDiameter);
                var normalisedOff = Mathf.Abs(entry.Normalised - 1f);
                var flag = normalisedOff > 0.005f ? "  NOT NORMALISED" : string.Empty;

                text.AppendLine(
                    $"  {entry.Id,-8} d={entry.Diameter * 100f,7:F2} cm  " +
                    $"pos=({entry.Position.x,6:F3}, {entry.Position.y,5:F3}, {entry.Position.z,6:F3})  " +
                    $"grab={grabDiameter * 100f,5:F1} cm  " +
                    $"visual={entry.VisualSpan * entry.Diameter * 100f,7:F2} cm ({entry.VisualSpan:F2}x)  " +
                    $"norm={entry.Normalised:F4}{flag}");
                text.AppendLine(
                    $"           from {entry.Source} :: {entry.TiltNode} / {entry.SurfaceName}, " +
                    $"authored {entry.SourceDiameter:F4} -> x{1f / entry.SourceDiameter:F3}, " +
                    $"{entry.Stripped} legacy components stripped, {entry.Renderers} renderers, " +
                    $"{entry.Vertices} verts");
                text.AppendLine($"           mesh {entry.Mesh}");
                text.AppendLine($"           material {entry.Material}");

                if (entry.HasRings)
                {
                    text.AppendLine($"           rings {entry.RingName}, true span {entry.RingSpan:F3}x the " +
                                    "body's diameter at any size");
                }

                vertices += entry.Vertices;
                renderers += entry.Renderers;
            }

            // Neighbour spacing, and the closest any two bodies come over every pair - on an arc a big body can
            // reach past the one beside it, so neighbours alone would not answer "no body overlaps".
            if (built.Count < 2)
            {
                Debug.Log(text.ToString());
                return;
            }

            var minStep = float.MaxValue;
            var maxStep = 0f;
            for (var i = 0; i < built.Count - 1; i++)
            {
                var step = Vector3.Distance(built[i].Position, built[i + 1].Position);
                minStep = Mathf.Min(minStep, step);
                maxStep = Mathf.Max(maxStep, step);
            }

            var closestBody = float.MaxValue;
            var closestBodyPair = "none";

            for (var i = 0; i < built.Count; i++)
            {
                for (var j = i + 1; j < built.Count; j++)
                {
                    var distance = Vector3.Distance(built[i].Position, built[j].Position);

                    var bodyGap = distance - (built[i].Diameter + built[j].Diameter) * 0.5f;
                    if (bodyGap < closestBody)
                    {
                        closestBody = bodyGap;
                        closestBodyPair = $"{built[i].Id}/{built[j].Id}";
                    }
                }
            }

            text.AppendLine(
                $"  spacing: centre to centre {minStep * 100f:F1}-{maxStep * 100f:F1} cm; " +
                $"closest planet surfaces {closestBody * 100f:F2} cm ({closestBodyPair}).");

            ReportRings(text, built, module, layout);

            text.AppendLine(
                $"  weight: {renderers} renderers, {vertices} vertices across the ten bodies.");
            text.AppendLine(
                $"  grab: nothing smaller than {GrabDiameter * 100f:F0} cm to the hand; a pull grows a body to " +
                $"{PullDiameter * 100f:F0} cm; two hands are limited to 5 cm - 3 m (ScaleLimits.Kind.Body).");

            Debug.Log(text.ToString());
        }

        /// <summary>
        /// CS-060, per arrangement: what each ring system would be without the clamp, what
        /// <see cref="LayoutRig"/> will leave it, and the closest any two bodies then come to touching. It runs
        /// the rig's own <see cref="LayoutRig.RingFactor"/> rather than restating the rule, so a run cannot report
        /// numbers the headset will not show, and it covers every layout the module offers rather than only the
        /// one the prefab is authored in — a clamp that is right in Solar Row and wrong in Relative Size is
        /// exactly the failure worth catching here.
        /// </summary>
        private static void ReportRings(StringBuilder text, List<Built> built, ExperienceModule module,
                                        LayoutPreset authored)
        {
            var layouts = module.Layouts != null && module.Layouts.Length > 0
                ? module.Layouts
                : new[] { authored };

            foreach (var preset in layouts)
            {
                if (preset == null)
                {
                    continue;
                }

                var extents = new List<LayoutRig.Extent>();
                var owners = new List<Built>();

                foreach (var entry in built)
                {
                    var slot = preset.Find(entry.Id);
                    if (!slot.HasValue)
                    {
                        continue;
                    }

                    extents.Add(new LayoutRig.Extent
                    {
                        Position = slot.Value.LocalPosition,
                        Diameter = Mathf.Max(0.0001f, slot.Value.Scale),
                        SpanRatio = entry.VisualSpan,
                        RingRatio = entry.HasRings ? entry.RingSpan : 0f,
                    });
                    owners.Add(entry);
                }

                if (extents.Count < 2)
                {
                    continue;
                }

                var halves = new float[extents.Count];
                var rings = new StringBuilder();

                for (var i = 0; i < extents.Count; i++)
                {
                    var extent = extents[i];
                    if (extent.RingRatio <= 0f)
                    {
                        halves[i] = extent.HalfExtent;
                        continue;
                    }

                    var factor = LayoutRig.RingFactor(i, extents, RingClearance);
                    var trueSpan = extent.Diameter * extent.RingRatio;
                    var clamped = trueSpan * factor;

                    // The planet is still there when its rings have been pulled in past it.
                    halves[i] = Mathf.Max(extent.Diameter, clamped) * 0.5f;

                    rings.Append(rings.Length > 0 ? ", " : string.Empty);
                    rings.Append($"{owners[i].Id} {trueSpan * 100f:F2} -> {clamped * 100f:F2} cm (x{factor:F3})");
                }

                var closest = float.MaxValue;
                var pair = "none";

                for (var i = 0; i < extents.Count; i++)
                {
                    for (var j = i + 1; j < extents.Count; j++)
                    {
                        var gap = Vector3.Distance(extents[i].Position, extents[j].Position) - halves[i] - halves[j];
                        if (gap < closest)
                        {
                            closest = gap;
                            pair = $"{owners[i].Id}/{owners[j].Id}";
                        }
                    }
                }

                text.AppendLine(
                    $"  rings in '{preset.Id}': {(rings.Length > 0 ? rings.ToString() : "no ringed body placed")}; " +
                    $"closest surfaces including rings and clouds {closest * 100f:F2} cm ({pair})" +
                    (closest < 0f ? "  OVERLAP" : string.Empty) + ".");
            }
        }
    }
}
