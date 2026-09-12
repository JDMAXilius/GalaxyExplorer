// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GalaxyExplorer.XR;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Hangs the moons on the bodies of the <b>Solar System Planets</b> experience (GDD 4.1, 6.2): hidden while
    /// their planet is in its arrangement, orbiting it with a small name once it has been pulled out, and
    /// pullable in their own right.
    ///
    /// <para><b>A post-pass, not a second builder.</b> <see cref="SolarRowBuilder"/> writes
    /// <c>solar_system_planets_content_prefab</c> from scratch every run, so a moon authored inside it would be
    /// thrown away the next time anyone rebuilt the row. This opens the finished prefab with
    /// <see cref="PrefabUtility.LoadPrefabContents"/>, adds what it owns and saves it back to the same path, so
    /// the prefab's GUID — and the experience module's reference to it — survive. The order is therefore
    /// <b>Build Solar Row Content</b> first, then this; re-running the row means re-running this.</para>
    ///
    /// <para><b>What one moon is.</b> The pattern <see cref="MoonOrbit"/> documents, built out here:
    /// <code>
    /// bodies / body_jupiter                       localScale = the planet's diameter in metres
    ///   moon_orbits / orbit_ganymede              turns once per orbitSeconds (MoonOrbit.Spin)
    ///     anchor_ganymede                         ForceSolver.RootTransform; localPosition.x = orbit radius
    /// moons / moon_ganymede                       localScale = the moon's diameter in metres
    ///   [SphereCollider + GEInteractable, ManipulationHandler(MoveRotateScale), ScaleLimits(Body),
    ///    SolverHandler, ForceSolver, FreePlacementSolver, Moon, MoonOrbit]
    ///   visual                                    localScale = 1 / measured source diameter
    /// moon_labels / moon_label_ganymede           [MoonLabel] a world canvas, one unit = one millimetre
    /// moon_panels / moon_panel_ganymede           [InfoPanel(variant Moon, body = the moon's solver)]
    /// </code>
    /// The anchor hangs under the <i>planet</i> so the orbit radius scales with it for free; the moon hangs in a
    /// group beside the bodies rather than under its planet, for two reasons. Its own scale would otherwise be
    /// multiplied by the planet's, and — the one that actually breaks — <c>ForceSolver.OnBeforeFocusChange</c>
    /// claims any focus event raised on a descendant of its own transform, so a moon parented to its planet
    /// could never be pulled: pointing at it would force-pull the planet instead.</para>
    ///
    /// <para><b>The three departures from the old prefab pattern</b> that CS-041 paid for, kept here.
    /// <c>ManipulationHandler</c> and <see cref="ScaleLimits"/> share one GameObject with the collider, because
    /// the handler finds its constraint with <c>GetComponent</c> and the constraint measures the transform it is
    /// on. The plain <c>ForceSolver</c>, not <c>MoonForceSolver</c>/<c>PlanetForceSolver</c>, which pin
    /// <c>GoalScale</c> to <c>Vector3.one</c> in Root and would inflate every moon to a metre. And the 6 cm grab
    /// minimum is the moon's own collider widened by <see cref="MoonOrbit"/>, never a second shape.</para>
    ///
    /// <para><b>Sizes and periods are derived, not tabulated.</b> A body's <c>localScale</c> is its diameter in
    /// metres, so everything below is a ratio of the planet measured at the 25 cm a pulled-out body grows to
    /// (GDD 4.1). Diameters are true diameters scaled so the largest moon is the GDD's 7 cm, clamped to its
    /// 3 cm floor; orbit radii are GDD 6.2's table divided by that same 25 cm; periods are compressed by a
    /// square root about ~16 days, which keeps the true ordering while stopping Mimas blurring and Iapetus
    /// looking frozen.</para>
    ///
    /// Re-running replaces the moons in place: every group this owns is removed by name and rebuilt, and nothing
    /// <see cref="SolarRowBuilder"/> owns is touched.
    /// Menu: <b>Cosmic Simulation -> Build Moons</b>.
    /// </summary>
    public static class MoonBuilder
    {
        private const string ContentPath = "Assets/prefabs/experiences/solar_system_planets_content_prefab.prefab";
        private const string MoonInfoFolder = "Assets/data/moons";
        private const string PanelPrefabPath = "Assets/prefabs/ui/info_panel_prefab.prefab";
        private const string TractorBeamPath = "Assets/prefabs/tractor_beam_prefab.prefab";

        // The groups this builder owns outright. Everything under them is deleted and rebuilt on every run;
        // nothing outside them is written, so 'bodies', 'homes' and 'panels' stay exactly as SolarRowBuilder
        // left them.
        private const string BodiesGroup = "bodies";
        private const string MoonsGroup = "moons";
        private const string LabelsGroup = "moon_labels";
        private const string PanelsGroup = "moon_panels";
        private const string OrbitsNode = "moon_orbits";

        /// <summary>GDD 4.1: a body pulled out grows to 25 cm. Every ratio below is measured against it.</summary>
        private const float ReferenceParentDiameter = 0.25f;

        /// <summary>GDD 6.2: "Moon sizes when orbiting: 3-7 cm (scaled to parent, floor 3 cm)."</summary>
        private const float LargestMoonDiameter = 0.07f;

        private const float SmallestMoonDiameter = 0.03f;

        /// <summary>GDD 6.2: "Held moon grows to 20 cm."</summary>
        private const float HeldDiameter = 0.2f;

        /// <summary>Same pull-out growth the bodies use (<see cref="LayoutRig"/>).</summary>
        private const float GrowSeconds = 0.35f;

        /// <summary>GDD 4.1 / 8.7: a body comes home over 0.8 s, and so does a moon.</summary>
        private const float RestoreSeconds = 0.8f;

        /// <summary>GDD 4.1: nothing is ever smaller than a 6 cm grab sphere to the hand.</summary>
        private const float GrabDiameter = 0.06f;

        // The period compression: a moon of ReferencePeriodDays takes ReferenceTurnSeconds to go round, and
        // everything else follows the square root of its true period. Clamped at both ends for comfort.
        private const float ReferencePeriodDays = 16f;
        private const float ReferenceTurnSeconds = 30f;
        private const float MinTurnSeconds = 8f;
        private const float MaxTurnSeconds = 60f;

        /// <summary>
        /// Degrees the orbit planes are tipped, alternating in sign down each planet's list. Without it two
        /// moons of the same planet ride the same circle and one hides the other every half turn.
        /// </summary>
        private const float OrbitInclination = 7f;

        /// <summary>
        /// One moon, as GDD 6.2 lists it. <see cref="Ships"/> marks the six the GDD names outright; the three
        /// it marks "(opt.)" are built and wired exactly the same way but left switched off, so turning one on
        /// is a checkbox rather than another run of this.
        ///
        /// Phobos and Deimos are deliberately absent: <c>docs/copy/moons.md</c> carries copy for them but
        /// GDD 6.2 does not list them, and the design contract is the GDD.
        /// </summary>
        private class Spec
        {
            public string Id;
            public string Parent;

            /// <summary>The moon's own prefab, or null when it has to be lifted out of a body prefab.</summary>
            public string PrefabPath;

            /// <summary>Where to find it when it has no prefab of its own: a prefab and a node inside it.</summary>
            public string HostPrefabPath;
            public string HostNode;

            public float DiameterKm;
            public float PeriodDays;

            /// <summary>GDD 6.2's in-app orbit radius, with the parent at 25 cm.</summary>
            public float OrbitRadiusMetres;

            public bool Ships;

            /// <summary>Desktop planet-bar slot, or -1. GDD 4.1 gives the Moon its own key.</summary>
            public int DesktopSlot = -1;
        }

        /// <summary>
        /// GDD 6.2, in the order it lists them, grouped by parent so a moon's phase around its planet can be
        /// spaced by its index in that group.
        /// </summary>
        private static readonly Spec[] Moons =
        {
            new Spec
            {
                Id = "moon", Parent = "earth",
                HostPrefabPath = "Assets/prefabs/poi_prefabs/poi_earth_prefab.prefab", HostNode = "moon_mesh",
                DiameterKm = 3474f, PeriodDays = 27.3f, OrbitRadiusMetres = 0.45f, Ships = true,
                DesktopSlot = 10,
            },
            new Spec
            {
                Id = "ganymede", Parent = "jupiter",
                PrefabPath = "Assets/prefabs/ganymede_jupiter_moon_prefab.prefab",
                DiameterKm = 5268f, PeriodDays = 7.2f, OrbitRadiusMetres = 0.40f, Ships = true,
            },
            new Spec
            {
                Id = "callisto", Parent = "jupiter",
                PrefabPath = "Assets/prefabs/callisto_jupiter_moon_prefab.prefab",
                DiameterKm = 4821f, PeriodDays = 16.7f, OrbitRadiusMetres = 0.52f, Ships = true,
            },
            new Spec
            {
                Id = "io", Parent = "jupiter",
                PrefabPath = "Assets/prefabs/io_jupiter_moon_prefab.prefab",
                DiameterKm = 3643f, PeriodDays = 1.8f, OrbitRadiusMetres = 0.30f, Ships = false,
            },
            new Spec
            {
                Id = "europa", Parent = "jupiter",
                PrefabPath = "Assets/prefabs/europa_jupiter_moon_prefab.prefab",
                DiameterKm = 3122f, PeriodDays = 3.6f, OrbitRadiusMetres = 0.34f, Ships = false,
            },
            new Spec
            {
                Id = "titan", Parent = "saturn",
                PrefabPath = "Assets/prefabs/titan_saturn_moon_prefab.prefab",
                DiameterKm = 5150f, PeriodDays = 15.9f, OrbitRadiusMetres = 0.48f, Ships = true,
            },
            new Spec
            {
                Id = "mimas", Parent = "saturn",
                PrefabPath = "Assets/prefabs/mimas_saturn_moon_prefab.prefab",
                DiameterKm = 396f, PeriodDays = 0.9f, OrbitRadiusMetres = 0.28f, Ships = true,
            },
            new Spec
            {
                Id = "iapetus", Parent = "saturn",
                PrefabPath = "Assets/prefabs/iapetus_saturn_moon_prefab.prefab",
                DiameterKm = 1469f, PeriodDays = 79f, OrbitRadiusMetres = 0.60f, Ships = true,
            },
            new Spec
            {
                Id = "enceladus", Parent = "saturn",
                PrefabPath = "Assets/prefabs/enceladus_saturn_moon_prefab.prefab",
                DiameterKm = 504f, PeriodDays = 1.4f, OrbitRadiusMetres = 0.32f, Ships = false,
            },
        };

        /// <summary>
        /// Components that belong to the orbit model rather than to a moon the player arranges. Ordered so a
        /// component always goes before the one that requires it: <c>PostManipulationResetter</c> and
        /// <see cref="ScaleLimits"/> both require a <c>ManipulationHandler</c>, and <c>GEInteractable</c> wants
        /// the collider it registered. (Same list as <see cref="SolarRowBuilder"/>'s, and for the same reason:
        /// only the Moon needs it, because it is lifted out of <c>poi_earth_prefab</c> where those components
        /// are still on the mesh.)
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

        /// <summary>What one moon ended up being, as values, so the report survives the prefab being unloaded.</summary>
        private class Built
        {
            public string Id;
            public string Parent;
            public bool Ships;

            public float Diameter;          // at a 25 cm parent, in metres
            public float DiameterRatio;     // of the parent's diameter
            public bool AtFloor;            // clamped up to the GDD's 3 cm
            public float OrbitRadius;       // at a 25 cm parent, in metres
            public float RadiusRatio;       // of the parent's diameter
            public float TurnSeconds;
            public float Phase;
            public float Inclination;

            public float AuthoredParentDiameter;
            public float AuthoredDiameter;
            public float AuthoredGrabRadius;

            public string Source;
            public string Mesh = "none";
            public string Material = "none";
            public float SourceDiameter;
            public float Normalised;
            public float OffsetMillimetres;
            public int Stripped;
            public int Renderers;
            public int Vertices;

            public bool HasInfo;
            public bool HasPanel;
            public bool HasLabel;
            public bool HasBeam;
        }

        [MenuItem("Cosmic Simulation/Build Moons")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("MoonBuilder: leave play mode first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ContentPath) == null)
            {
                Debug.LogError($"MoonBuilder: no content prefab at {ContentPath}. Run " +
                               "Cosmic Simulation > Build Solar Row Content first; this is a post-pass over it.");
                return;
            }

            var built = new List<Built>();
            var skipped = new List<string>();
            var saved = false;

            var root = PrefabUtility.LoadPrefabContents(ContentPath);
            if (root == null)
            {
                Debug.LogError($"MoonBuilder: {ContentPath} would not open for editing.");
                return;
            }

            try
            {
                if (Populate(root, built, skipped))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, ContentPath, out saved);
                }
            }
            finally
            {
                // Unloading is not optional: a prefab left loaded keeps a hidden preview scene alive for the
                // rest of the editor session, whatever went wrong above.
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (built.Count == 0 && skipped.Count == 0)
            {
                return; // Populate already said why
            }

            if (!saved)
            {
                Debug.LogError($"MoonBuilder: could not save {ContentPath}; the prefab is unchanged.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Report(built, skipped);
        }

        // ---------- the pass over the content prefab

        private static bool Populate(GameObject root, List<Built> built, List<string> skipped)
        {
            var bodies = root.transform.Find(BodiesGroup);
            if (bodies == null)
            {
                Debug.LogError($"MoonBuilder: {ContentPath} has no '{BodiesGroup}' group, so it was not written " +
                               "by SolarRowBuilder. Run Cosmic Simulation > Build Solar Row Content first.");
                return false;
            }

            // Everything this builder owns goes first, so a second run replaces rather than stacks.
            var moons = Regroup(root.transform, MoonsGroup);
            var labels = Regroup(root.transform, LabelsGroup);
            var panels = Regroup(root.transform, PanelsGroup);
            foreach (var body in bodies.Cast<Transform>().ToArray())
            {
                var stale = body.Find(OrbitsNode);
                if (stale != null)
                {
                    Object.DestroyImmediate(stale.gameObject);
                }
            }

            var tracker = root.GetComponentInChildren<ControllerTransformTracker>(true);
            if (tracker == null)
            {
                Debug.LogWarning("MoonBuilder: no ControllerTransformTracker in the content prefab, so every " +
                                 "moon's ForceSolver will build its own at runtime and warn about it.");
            }

            var beam = AssetDatabase.LoadAssetAtPath<GameObject>(TractorBeamPath);
            if (beam == null)
            {
                Debug.LogWarning($"MoonBuilder: no {TractorBeamPath}; pulling a moon will show no tractor beam.");
            }

            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (panelPrefab == null)
            {
                Debug.LogWarning($"MoonBuilder: no {PanelPrefabPath}; the moons get no panels and no label " +
                                 "styling. Run Cosmic Simulation > Build UI Prefabs and re-run this.");
            }

            var sun = SunTarget(bodies);
            var largest = Moons.Max(m => m.DiameterKm);

            foreach (var spec in Moons)
            {
                // Phase is spread over every moon this planet could have, optional ones included, so switching
                // one on never drops it on top of a moon that was already there.
                var family = Moons.Where(m => m.Parent == spec.Parent).ToArray();
                var index = System.Array.IndexOf(family, spec);

                var entry = BuildMoon(spec, index, family.Length, largest, bodies, moons, labels, panels,
                                      panelPrefab, beam, tracker, sun);
                if (entry == null)
                {
                    skipped.Add(spec.Id);
                    continue;
                }

                built.Add(entry);
            }

            return true;
        }

        /// <summary>Removes the named group if it is there and makes a fresh, empty one in its place.</summary>
        private static Transform Regroup(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// What the moons' <c>SunLightReceiver</c> components point at. Left unset a receiver calls
        /// <c>GameObject.Find("Sun")</c> from <c>LateUpdate</c>, once per receiver per frame, forever.
        /// </summary>
        private static Transform SunTarget(Transform bodies)
        {
            var sun = bodies.Find("body_sun");
            if (sun == null)
            {
                Debug.LogWarning("MoonBuilder: no body_sun in the content prefab, so the moons have no light " +
                                 "direction and will be lit from wherever a 'Sun' object happens to be.");
                return null;
            }

            return FindDescendant(sun, "sun_mesh") ?? sun;
        }

        // ---------- one moon

        private static Built BuildMoon(Spec spec, int index, int family, float largestMoonKm, Transform bodies,
                                       Transform moons, Transform labels, Transform panels,
                                       GameObject panelPrefab, GameObject beam,
                                       ControllerTransformTracker tracker, Transform sun)
        {
            var parentRoot = bodies.Find("body_" + spec.Parent);
            if (parentRoot == null)
            {
                Debug.LogError($"MoonBuilder: no 'body_{spec.Parent}' in the content prefab, so '{spec.Id}' has " +
                               "nothing to orbit; skipped.");
                return null;
            }

            var parentForce = parentRoot.GetComponent<ForceSolver>();
            if (parentForce == null)
            {
                Debug.LogError($"MoonBuilder: 'body_{spec.Parent}' has no ForceSolver, so nothing can tell when " +
                               $"it has been pulled out; '{spec.Id}' skipped.");
                return null;
            }

            var info = AssetDatabase.LoadAssetAtPath<BodyInfo>($"{MoonInfoFolder}/{spec.Id}.asset");
            if (info == null)
            {
                Debug.LogWarning($"MoonBuilder: no BodyInfo at {MoonInfoFolder}/{spec.Id}.asset; the moon is " +
                                 "built but its label and panel stay blank. Run Cosmic Simulation > Import Copy.");
            }

            var moonRoot = new GameObject("moon_" + spec.Id);
            moonRoot.transform.SetParent(moons, false);

            var visual = new GameObject("visual");
            visual.transform.SetParent(moonRoot.transform, false);

            var source = LiftVisual(spec, visual.transform);
            if (source == null)
            {
                Object.DestroyImmediate(moonRoot);
                return null;
            }

            var stripped = Strip(visual.transform);

            // ---- normalise to a 1 m diameter, so the moon root's localScale is its diameter in metres
            if (!Measure(moonRoot.transform, visual.transform, out var sourceDiameter, out _))
            {
                Object.DestroyImmediate(moonRoot);
                Debug.LogError($"MoonBuilder: nothing measurable under '{source}', so '{spec.Id}' is skipped.");
                return null;
            }

            visual.transform.localScale = Vector3.one / sourceDiameter;

            // A moon lifted out of a body prefab does not necessarily sit at its own pivot; left alone it would
            // ride its anchor from a point outside itself.
            var centre = CentreOffset(moonRoot.transform, visual.transform);
            if (centre.magnitude > 0.001f)
            {
                visual.transform.localPosition -= centre;
            }

            Measure(moonRoot.transform, visual.transform, out var normalised, out var offsetMillimetres);
            var surface = LargestRenderer(visual.transform);

            foreach (var receiver in visual.GetComponentsInChildren<GalaxyExplorer.SunLightReceiver>(true))
            {
                receiver.Sun = sun;
            }

            // ---- the numbers, all ratios of a 25 cm parent
            var wanted = Mathf.Clamp(LargestMoonDiameter * spec.DiameterKm / largestMoonKm,
                                     SmallestMoonDiameter, LargestMoonDiameter);
            var diameterRatio = wanted / ReferenceParentDiameter;
            var radiusRatio = spec.OrbitRadiusMetres / ReferenceParentDiameter;
            var turnSeconds = Mathf.Clamp(
                ReferenceTurnSeconds * Mathf.Sqrt(spec.PeriodDays / ReferencePeriodDays),
                MinTurnSeconds, MaxTurnSeconds);

            // ---- the orbit, under the planet so its radius scales with the planet for free
            var orbits = parentRoot.Find(OrbitsNode);
            if (orbits == null)
            {
                var group = new GameObject(OrbitsNode);
                group.transform.SetParent(parentRoot, false);
                orbits = group.transform;
            }

            var pivot = new GameObject("orbit_" + spec.Id).transform;
            pivot.SetParent(orbits, false);

            // Tilt first, then phase, so MoonOrbit's Rotate(up, Space.Self) turns within the tilted plane and
            // the phase is a true offset around it rather than whatever a combined Euler happens to produce.
            var phase = family > 1 ? 360f * index / family : 0f;
            var inclination = index % 2 == 0 ? OrbitInclination : -OrbitInclination;
            pivot.localRotation = Quaternion.Euler(inclination, 0f, 0f) * Quaternion.Euler(0f, phase, 0f);

            var anchor = new GameObject("anchor_" + spec.Id).transform;
            anchor.SetParent(pivot, false);
            anchor.localPosition = new Vector3(radiusRatio, 0f, 0f);

            // ---- everything the player touches, on the moon root
            var grab = moonRoot.AddComponent<SphereCollider>();
            grab.radius = 0.5f; // the normalised moon's own surface
            moonRoot.AddComponent<GEInteractable>();

            var manipulation = moonRoot.AddComponent<ManipulationHandler>();
            var manipulationSo = new SerializedObject(manipulation);
            manipulationSo.FindProperty("manipulationType").enumValueIndex =
                (int)ManipulationHandler.HandMovementType.OneAndTwoHanded;
            manipulationSo.FindProperty("twoHandedManipulationType").enumValueIndex =
                (int)ManipulationHandler.TwoHandedManipulation.MoveRotateScale;
            manipulationSo.FindProperty("allowFarManipulation").boolValue = true;
            manipulationSo.ApplyModifiedPropertiesWithoutUndo();

            var limits = moonRoot.AddComponent<ScaleLimits>();
            var limitsSo = new SerializedObject(limits);
            limitsSo.FindProperty("kind").enumValueIndex = (int)ScaleLimits.Kind.Body;
            limitsSo.FindProperty("measureFrom").objectReferenceValue = surface;
            limitsSo.ApplyModifiedPropertiesWithoutUndo();

            var handler = moonRoot.AddComponent<SolverHandler>();
            var force = moonRoot.AddComponent<ForceSolver>();
            force.RootTransform = anchor;
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

            var placement = moonRoot.AddComponent<FreePlacementSolver>();
            var placementSo = new SerializedObject(placement);
            placementSo.FindProperty("restoreSeconds").floatValue = RestoreSeconds;
            placementSo.ApplyModifiedPropertiesWithoutUndo();

            // The fader. visualRoot is left empty on purpose: it defaults to this object, which is where both
            // the renderers (under 'visual') and the grab collider live, so a hidden moon cannot be pinched.
            var fade = moonRoot.AddComponent<Moon>();

            if (spec.DesktopSlot >= 0)
            {
                // GDD 4.1's desktop line gives the Moon its own key. DesktopMouseInput.ToggleBody finds a body
                // by UiPreviewTarget.slotId, so this is the whole of that wiring; the digit keys for the ten
                // bodies are still owed by CS-049.
                var preview = moonRoot.AddComponent<UiPreviewTarget>();
                preview.slotId = spec.DesktopSlot;
                preview.displayName = info != null ? info.DisplayName : spec.Id;
                preview.forceSolver = force;
            }

            var label = BuildLabel(spec, info, labels, moonRoot.transform, surface, force, panelPrefab);
            var panel = BuildPanel(spec, panels, panelPrefab, moonRoot.transform, surface, force);

            // ---- place it where it would be right now, in the arrangement the prefab is authored in
            var parentDiameter = parentRoot.localScale.x;
            var authored = Mathf.Max(SmallestMoonDiameter, parentDiameter * diameterRatio);
            moonRoot.transform.localScale = Vector3.one * authored;
            moonRoot.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            grab.radius = Mathf.Max(0.5f, GrabDiameter * 0.5f / Mathf.Max(0.0001f, authored));

            var orbitSo = new SerializedObject(moonRoot.AddComponent<MoonOrbit>());
            orbitSo.FindProperty("info").objectReferenceValue = info;
            orbitSo.FindProperty("parentBody").objectReferenceValue = parentForce;
            orbitSo.FindProperty("parentRoot").objectReferenceValue = parentRoot;
            orbitSo.FindProperty("orbitPivot").objectReferenceValue = pivot;
            orbitSo.FindProperty("anchor").objectReferenceValue = anchor;
            orbitSo.FindProperty("fade").objectReferenceValue = fade;
            orbitSo.FindProperty("label").objectReferenceValue = label;
            orbitSo.FindProperty("panel").objectReferenceValue = panel;
            orbitSo.FindProperty("grab").objectReferenceValue = grab;
            orbitSo.FindProperty("placement").objectReferenceValue = placement;
            orbitSo.FindProperty("orbitSeconds").floatValue = turnSeconds;
            orbitSo.FindProperty("diameterRatio").floatValue = diameterRatio;
            orbitSo.FindProperty("minOrbitDiameter").floatValue = SmallestMoonDiameter;
            orbitSo.FindProperty("heldDiameter").floatValue = HeldDiameter;
            orbitSo.FindProperty("growSeconds").floatValue = GrowSeconds;
            orbitSo.FindProperty("restoreSeconds").floatValue = RestoreSeconds;
            orbitSo.FindProperty("minGrabDiameter").floatValue = GrabDiameter;
            orbitSo.ApplyModifiedPropertiesWithoutUndo();

            if (!spec.Ships)
            {
                // Available, not absent: the whole moon is wired and can be switched on in the inspector.
                moonRoot.SetActive(false);
                if (label != null)
                {
                    label.gameObject.SetActive(false);
                }

                if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                }
            }

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

            var surfaceFilter = surface != null ? surface.GetComponent<MeshFilter>() : null;

            return new Built
            {
                Id = spec.Id,
                Parent = spec.Parent,
                Ships = spec.Ships,
                Diameter = wanted,
                DiameterRatio = diameterRatio,
                AtFloor = wanted <= SmallestMoonDiameter + 0.0001f &&
                          LargestMoonDiameter * spec.DiameterKm / largestMoonKm < SmallestMoonDiameter,
                OrbitRadius = spec.OrbitRadiusMetres,
                RadiusRatio = radiusRatio,
                TurnSeconds = turnSeconds,
                Phase = phase,
                Inclination = inclination,
                AuthoredParentDiameter = parentDiameter,
                AuthoredDiameter = authored,
                AuthoredGrabRadius = grab.radius,
                Source = source,
                Mesh = surfaceFilter != null && surfaceFilter.sharedMesh != null
                    ? AssetPath(surfaceFilter.sharedMesh) + " / " + surfaceFilter.sharedMesh.name
                    : "none",
                Material = surface != null && surface.sharedMaterial != null
                    ? AssetPath(surface.sharedMaterial)
                    : "none",
                SourceDiameter = sourceDiameter,
                Normalised = normalised,
                OffsetMillimetres = offsetMillimetres,
                Stripped = stripped,
                Renderers = renderers.Length,
                Vertices = vertices,
                HasInfo = info != null,
                HasPanel = panel != null,
                HasLabel = label != null,
                HasBeam = beam != null,
            };
        }

        /// <summary>
        /// Puts the moon's geometry under <paramref name="visual"/> and returns where it came from, or null.
        ///
        /// A plain <c>Instantiate</c> rather than <c>PrefabUtility.InstantiatePrefab</c>: the copy is about to
        /// be taken apart and rescaled, and a prefab instance will not let that happen without being unpacked
        /// first. It is parented as it is created so that nothing is ever left loose in whatever scene happens
        /// to be open behind the prefab being edited.
        /// </summary>
        private static string LiftVisual(Spec spec, Transform visual)
        {
            if (!string.IsNullOrEmpty(spec.PrefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"MoonBuilder: no prefab at {spec.PrefabPath}; '{spec.Id}' skipped.");
                    return null;
                }

                var copy = Object.Instantiate(prefab, visual);
                copy.name = prefab.name;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
                return spec.PrefabPath;
            }

            var host = AssetDatabase.LoadAssetAtPath<GameObject>(spec.HostPrefabPath);
            if (host == null)
            {
                Debug.LogError($"MoonBuilder: no prefab at {spec.HostPrefabPath}; '{spec.Id}' skipped.");
                return null;
            }

            var instance = Object.Instantiate(host, visual);
            var node = FindDescendant(instance.transform, spec.HostNode);
            if (node == null)
            {
                Object.DestroyImmediate(instance);
                Debug.LogError($"MoonBuilder: no '{spec.HostNode}' inside {spec.HostPrefabPath}; " +
                               $"'{spec.Id}' skipped.");
                return null;
            }

            // Keep the node's own local pose and drop the chain of scale controllers it hung under; the
            // normalisation below is what decides the moon's size, not whatever the orbit model wanted.
            node.SetParent(visual, false);
            Object.DestroyImmediate(instance);
            return $"{spec.HostPrefabPath} :: {spec.HostNode}";
        }

        private static InfoPanel BuildPanel(Spec spec, Transform panels, GameObject panelPrefab,
                                            Transform target, Renderer bounds, ForceSolver force)
        {
            if (panelPrefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, panels);
            if (instance == null)
            {
                Debug.LogError($"MoonBuilder: could not nest {PanelPrefabPath} for '{spec.Id}'; no panel.");
                return null;
            }

            instance.name = "moon_panel_" + spec.Id;
            instance.transform.localPosition = Vector3.zero;

            var panel = instance.GetComponent<InfoPanel>();
            if (panel == null)
            {
                Debug.LogWarning($"MoonBuilder: {PanelPrefabPath} has no InfoPanel; '{spec.Id}' gets no panel.");
                Object.DestroyImmediate(instance);
                return null;
            }

            var so = new SerializedObject(panel);
            so.FindProperty("variant").enumValueIndex = (int)InfoPanel.Variant.Moon;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("targetBounds").objectReferenceValue = bounds;
            // A moon's panel is open exactly while the moon is out of its orbit, which its own solver knows.
            so.FindProperty("body").objectReferenceValue = force;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        /// <summary>
        /// The moon's name, on a world canvas of its own where one unit is one millimetre.
        ///
        /// Built here rather than instantiated from a prefab because there is no moon-label prefab to
        /// instantiate: <c>UiPrefabBuilder</c> writes the dock, the pop-up, the info panel and the label button,
        /// and nothing else needs this shape. The font and the outline material are taken off the info panel's
        /// own text so the two never drift apart, rather than restating the design tokens a second time.
        /// </summary>
        private static MoonLabel BuildLabel(Spec spec, BodyInfo info, Transform labels, Transform target,
                                            Renderer bounds, ForceSolver force, GameObject panelPrefab)
        {
            var go = new GameObject("moon_label_" + spec.Id, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(labels, false);

            // The canvas goes on before the rect is sized: adding one rewrites the RectTransform it lands on.
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 4f;
            go.AddComponent<CanvasGroup>();

            // Wide enough that the held style's 18 mm never wraps the longest name on the list.
            rt.sizeDelta = new Vector2(240f, 40f);
            rt.localScale = Vector3.one * 0.001f; // one canvas unit is a millimetre; MoonLabel keeps it there

            var textGo = new GameObject("name", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.sizeDelta = new Vector2(240f, 40f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = info != null ? info.DisplayName : spec.Id;
            text.fontSize = 5f; // MoonLabel drives this between its two styles; this is the orbiting one
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var reference = panelPrefab != null ? panelPrefab.GetComponentInChildren<TMP_Text>(true) : null;
            if (reference != null)
            {
                text.font = reference.font;
                // The shared material, not outlineWidth/outlineColor: those setters instance a material, and a
                // per-moon material instance would be written into this prefab.
                text.fontSharedMaterial = reference.fontSharedMaterial;
            }

            var label = go.AddComponent<MoonLabel>();
            var so = new SerializedObject(label);
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("info").objectReferenceValue = info;
            so.FindProperty("moon").objectReferenceValue = force;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("targetBounds").objectReferenceValue = bounds;
            // GDD 8.4: 5 mm under an orbiting moon, 18 mm bold once it is held.
            so.FindProperty("orbitingSize").floatValue = 5f;
            so.FindProperty("heldSize").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return label;
        }

        // ---------- measuring
        //
        // The same rules SolarRowBuilder measures bodies by, restated here because they are private to it and
        // this must not edit that file. Two of them matter for moons as much as for planets: measure each mesh
        // in its own space and multiply by the accumulated scale, never as a world AABB, because an axial tilt
        // inflates a rotated sphere's box by up to 40 per cent; and size by the widest geometry rather than the
        // one that reaches furthest, because a decorative mesh a centimetre across sitting far out in model
        // space would otherwise set the diameter.
        // "atmosphere" is here so the two lists stay identical: AtmosphereShellBuilder names its rim shell
        // <id>_atmosphere_shell, and no object in the project carried that word before it existed.

        private static readonly string[] NotGeometry =
            { "glow", "flare", "halo", "atmosphere", "afforda", "highlight", "trail" };

        /// <summary>
        /// Active all the way up to <paramref name="stopAt"/>, walked with <c>activeSelf</c> rather than asked
        /// with <c>activeInHierarchy</c>: this runs inside the preview scene <c>LoadPrefabContents</c> opens,
        /// and activeInHierarchy answers a question about a scene as well as about the subtree.
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

        private static bool Measure(Transform root, Transform subject, out float diameter,
                                    out float offsetMillimetres)
        {
            diameter = 0f;
            offsetMillimetres = 0f;
            var found = false;

            foreach (var filter in subject.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled ||
                    !IsActiveUnder(filter.transform, subject) || !IsGeometry(filter.transform))
                {
                    continue;
                }

                var scale = filter.transform.lossyScale;
                var size = mesh.bounds.size;
                var widest = Mathf.Max(size.x * Mathf.Abs(scale.x),
                    Mathf.Max(size.y * Mathf.Abs(scale.y), size.z * Mathf.Abs(scale.z)));

                if (widest > diameter)
                {
                    diameter = widest;
                    var centre = root.InverseTransformPoint(filter.transform.TransformPoint(mesh.bounds.center));
                    offsetMillimetres = centre.magnitude * 1000f;
                }

                found = true;
            }

            return found && diameter > 0.0001f;
        }

        private static Vector3 CentreOffset(Transform root, Transform subject)
        {
            var offset = Vector3.zero;
            var widestSeen = 0f;

            foreach (var filter in subject.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || !renderer.enabled ||
                    !IsActiveUnder(filter.transform, subject) || !IsGeometry(filter.transform))
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
                    offset = root.InverseTransformPoint(filter.transform.TransformPoint(mesh.bounds.center));
                }
            }

            return offset;
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

        private static string AssetPath(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "(embedded)" : path;
        }

        // ---------- checking the run

        /// <summary>
        /// Prints what was actually built, measured rather than intended, so a run can be checked against
        /// GDD 6.2 without opening the prefab. A moon that could not be built is named on the first line
        /// instead of passing silently.
        /// </summary>
        private static void Report(List<Built> built, List<string> skipped)
        {
            var shipping = built.Where(b => b.Ships).ToArray();
            var available = built.Where(b => !b.Ships).ToArray();

            var families = shipping.GroupBy(x => x.Parent)
                .Select(g => g.Key + " (" + string.Join(" + ", g.Select(m => m.Id)) + ")")
                .ToArray();
            var off = available.Length == 0 ? "none" : string.Join(", ", available.Select(b => b.Id));
            var missed = skipped.Count == 0 ? "none" : string.Join(", ", skipped);

            var text = new StringBuilder();
            text.AppendLine(
                $"MoonBuilder: {shipping.Length} moons on {families.Length} bodies in {ContentPath} - " +
                string.Join(", ", families) +
                $"; built but switched off: {off}; skipped: {missed}.");

            foreach (var entry in built)
            {
                var flags = new StringBuilder();
                if (!entry.Ships)
                {
                    flags.Append("  OFF");
                }

                if (!entry.HasInfo)
                {
                    flags.Append("  NO COPY");
                }

                if (!entry.HasPanel)
                {
                    flags.Append("  NO PANEL");
                }

                if (!entry.HasLabel)
                {
                    flags.Append("  NO LABEL");
                }

                if (Mathf.Abs(entry.Normalised - 1f) > 0.005f)
                {
                    flags.Append("  NOT NORMALISED");
                }

                if (entry.OffsetMillimetres > 1f)
                {
                    flags.Append($"  OFF CENTRE BY {entry.OffsetMillimetres:F1} mm");
                }

                var floor = entry.AtFloor ? "*" : " ";
                text.AppendLine(
                    $"  {entry.Id,-10} of {entry.Parent,-8} " +
                    $"d={entry.Diameter * 100f,5:F2} cm{floor} " +
                    $"(x{entry.DiameterRatio:F3} of the parent)  " +
                    $"r={entry.OrbitRadius * 100f,5:F1} cm (x{entry.RadiusRatio:F3})  " +
                    $"turn={entry.TurnSeconds,5:F1} s  " +
                    $"phase={entry.Phase,5:F0}, tilt={entry.Inclination,4:F0} deg{flags}");
                text.AppendLine(
                    $"             from {entry.Source}, authored {entry.SourceDiameter:F4} -> " +
                    $"x{1f / entry.SourceDiameter:F3}, norm={entry.Normalised:F4}, " +
                    $"{entry.Stripped} legacy components stripped, {entry.Renderers} renderers, " +
                    $"{entry.Vertices} verts");
                text.AppendLine($"             mesh {entry.Mesh}");
                text.AppendLine($"             material {entry.Material}");
                text.AppendLine(
                    $"             authored beside a {entry.AuthoredParentDiameter * 100f:F1} cm parent: " +
                    $"{entry.AuthoredDiameter * 100f:F2} cm wide, grab radius {entry.AuthoredGrabRadius:F2} " +
                    $"(= {entry.AuthoredGrabRadius * 2f * entry.AuthoredDiameter * 100f:F1} cm across)");
            }

            if (built.Any(b => !b.HasBeam))
            {
                text.AppendLine("  no tractor beam prefab was found, so dwelling on a moon shows nothing.");
            }

            // The one number that is easy to get wrong and impossible to see in the inspector: two moons of the
            // same planet whose orbit radii are closer than their two radii, so they would graze each other
            // when their differing periods eventually bring them round to the same side. The alternating
            // inclination usually saves such a pair, which is why this is a caution and not an error.
            foreach (var family in built.GroupBy(x => x.Parent))
            {
                var list = family.OrderBy(m => m.RadiusRatio).ToArray();
                for (var i = 0; i < list.Length - 1; i++)
                {
                    var inner = list[i];
                    var outer = list[i + 1];
                    var gap = (outer.RadiusRatio - inner.RadiusRatio) * ReferenceParentDiameter;
                    var touch = (inner.Diameter + outer.Diameter) * 0.5f;
                    if (gap >= touch)
                    {
                        continue;
                    }

                    var both = inner.Ships && outer.Ships
                        ? string.Empty
                        : " (only once both are switched on)";
                    text.AppendLine(
                        $"  ORBITS CLOSE: {inner.Id} and {outer.Id} run {gap * 100f:F1} cm apart but are " +
                        $"{touch * 100f:F1} cm across between them{both}; they are " +
                        $"{Mathf.Abs(Mathf.DeltaAngle(inner.Phase, outer.Phase)):F0} deg apart in phase and " +
                        $"tilted {inner.Inclination:F0}/{outer.Inclination:F0} deg, which is what keeps them " +
                        "clear. Widen one radius here if the headset says otherwise.");
                }
            }

            // Worth knowing rather than fixing: a moon's orbit is wider than the 25 cm the row is pitched at,
            // so a planet left in its slot will sweep its moons through its neighbours' slots. In practice a
            // planet whose moons are showing has been pulled to the hand, which is why this is a note.
            var widest = built.OrderByDescending(b => b.OrbitRadius).FirstOrDefault();
            if (widest != null)
            {
                text.AppendLine(
                    $"  reach: the widest orbit is {widest.Id}'s at {widest.OrbitRadius * 100f:F0} cm from a " +
                    "25 cm parent, which is wider than Solar Row's 25 cm pitch — a planet whose moons are out " +
                    "while it is still in the row will overlap its neighbours.");
            }

            text.AppendLine(
                $"  a moon grows to {HeldDiameter * 100f:F0} cm when pulled, comes home over {RestoreSeconds:F1} s, " +
                $"is never smaller than {GrabDiameter * 100f:F0} cm to the hand, and is limited to 5 cm - 3 m " +
                "by two hands (ScaleLimits.Kind.Body). * = clamped up to the GDD's 3 cm floor.");
            text.AppendLine(
                "  re-run this after any Cosmic Simulation > Build Solar Row Content: that rewrites the whole " +
                "content prefab and takes the moons with it.");

            Debug.Log(text.ToString());
        }
    }
}
