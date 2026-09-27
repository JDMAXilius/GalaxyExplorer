// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Makes every moon in the <b>Solar System</b> (orbits) scene selectable the way the Earth's Moon already is
    /// (owner's direction, 27 Sep): pull a planet out, its moons appear round it, and each can be pulled out in
    /// turn, shows its own card and says its narration.
    ///
    /// <para>The Earth's <c>poi_earth_prefab</c> is the pattern and the template. Its Moon has three parts: the
    /// orbit (a <see cref="Moon"/> with the moon animator, under the planet's grab root, so the planet shows and
    /// hides it), a separate <c>moon_force_grab_root</c> at the prefab root carrying a <see cref="MoonForceSolver"/>
    /// that follows an anchor in that orbit while at rest, and a card. Mars, Jupiter and Saturn inherited their
    /// moons as decoration inside the planet's grab root - pointing at one grabbed the planet - so each is given
    /// the same three parts, its model moved out to its own grab root. Neptune and Pluto had no moons in this
    /// scene; Triton and Charon get an orbit built from nothing, on the global maps <see cref="MoonBuilder"/>
    /// already turned into materials.</para>
    ///
    /// Idempotent: everything is found by name or created, and a model already moved is not moved again.
    /// Menu: <b>Cosmic Simulation -> Build Solar System Moons</b>. Run <b>Build Moons</b> first for the materials.
    /// </summary>
    public static class SolarMoonBuilder
    {
        private const string PoiFolder = "Assets/prefabs/poi_prefabs/";
        private const string EarthPrefab = PoiFolder + "poi_earth_prefab.prefab";
        private const string MoonAnimator = "Assets/animation_controllers/moon_animator.controller";
        private const string MoonData = "Assets/data/moons/";

        private class Spec
        {
            public string Planet;
            public string Id;           // data asset id
            public string Rig;          // inherited rig name prefix, or null to build one
            public float Km;
            public float Days;
            public float OrbitPlanetRadii;
            public float OrbitRadius;       // in the planet grab root's space; wins over OrbitPlanetRadii
            public float TiltDegrees;       // the planet's obliquity, so the moons orbit its equator
        }

        private static readonly Spec[] Moons =
        {
            new Spec { Planet = "mars", Id = "phobos", Rig = "phobos" },
            new Spec { Planet = "mars", Id = "deimos", Rig = "deimos" },
            new Spec { Planet = "jupiter", Id = "io", Rig = "io" },
            new Spec { Planet = "jupiter", Id = "europa", Rig = "europa" },
            new Spec { Planet = "jupiter", Id = "ganymede", Rig = "ganymede" },
            new Spec { Planet = "jupiter", Id = "callisto", Rig = "calisto" },
            new Spec { Planet = "saturn", Id = "mimas", Rig = "mimas" },
            new Spec { Planet = "saturn", Id = "enceladus", Rig = "enceladus" },
            new Spec { Planet = "saturn", Id = "titan", Rig = "titan" },
            new Spec { Planet = "saturn", Id = "iapetus", Rig = "iapetus" },
            new Spec { Planet = "neptune", Id = "triton", Km = 2707f, Days = 5.9f, OrbitPlanetRadii = 2.2f },
            new Spec { Planet = "pluto", Id = "charon", Km = 1212f, Days = 6.4f, OrbitPlanetRadii = 2.6f },
            new Spec { Planet = "saturn", Id = "rhea", Km = 1527f, Days = 4.5f, OrbitRadius = 0.76f, TiltDegrees = 26.7f },
            new Spec { Planet = "saturn", Id = "dione", Km = 1123f, Days = 2.7f, OrbitRadius = 0.68f, TiltDegrees = 26.7f },
            new Spec { Planet = "saturn", Id = "tethys", Km = 1062f, Days = 1.9f, OrbitRadius = 0.6f, TiltDegrees = 26.7f },
            new Spec { Planet = "uranus", Id = "miranda", Km = 472f, Days = 1.4f, OrbitRadius = 0.07f, TiltDegrees = 98f },
            new Spec { Planet = "uranus", Id = "ariel", Km = 1158f, Days = 2.5f, OrbitRadius = 0.09f, TiltDegrees = 98f },
            new Spec { Planet = "uranus", Id = "umbriel", Km = 1169f, Days = 4.1f, OrbitRadius = 0.11f, TiltDegrees = 98f },
            new Spec { Planet = "uranus", Id = "titania", Km = 1577f, Days = 8.7f, OrbitRadius = 0.14f, TiltDegrees = 98f },
            new Spec { Planet = "uranus", Id = "oberon", Km = 1523f, Days = 13.5f, OrbitRadius = 0.17f, TiltDegrees = 98f },
            new Spec { Planet = "neptune", Id = "proteus", Km = 420f, Days = 1.1f, OrbitRadius = 0.045f, TiltDegrees = 28f },
            new Spec { Planet = "neptune", Id = "nereid", Km = 357f, Days = 360f, OrbitRadius = 0.16f, TiltDegrees = 28f },
            new Spec { Planet = "pluto", Id = "styx", Km = 16f, Days = 20.2f, OrbitRadius = 0.04f, TiltDegrees = 120f },
            new Spec { Planet = "pluto", Id = "nix", Km = 50f, Days = 24.9f, OrbitRadius = 0.046f, TiltDegrees = 120f },
            new Spec { Planet = "pluto", Id = "kerberos", Km = 19f, Days = 32.2f, OrbitRadius = 0.052f, TiltDegrees = 120f },
            new Spec { Planet = "pluto", Id = "hydra", Km = 51f, Days = 38.2f, OrbitRadius = 0.058f, TiltDegrees = 120f },
        };

        [MenuItem("Cosmic Simulation/Build Solar System Moons")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("SolarMoonBuilder: refused in play mode.");
                return;
            }

            var earth = PrefabUtility.LoadPrefabContents(EarthPrefab);
            try
            {
                var solverTemplate = earth.transform.Find("moon_force_grab_root").GetComponent<MoonForceSolver>();
                var handlerTemplate = solverTemplate.GetComponent<SolverHandler>();
                var meshTemplate = earth.transform.Find("moon_force_grab_root").GetComponentInChildren<ManipulationHandler>(true);
                var cardTemplate = earth.transform.Find("moon_info_card").gameObject;
                var reference = ReferenceMeshDiameter();
                var built = 0;

                foreach (var planet in Moons.Select(m => m.Planet).Distinct())
                {
                    var path = $"{PoiFolder}poi_{planet}_prefab.prefab";
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var spec in Moons.Where(m => m.Planet == planet))
                        {
                            if (BuildMoon(root.transform, spec, solverTemplate, handlerTemplate, meshTemplate, cardTemplate, reference))
                            {
                                built++;
                            }
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                Debug.Log($"SolarMoonBuilder: {built} of {Moons.Length} moons selectable in the Solar System scene.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(earth);
            }
        }

        private static bool BuildMoon(Transform prefab, Spec spec, MoonForceSolver solverTemplate, SolverHandler handlerTemplate,
                                      ManipulationHandler meshTemplate, GameObject cardTemplate, float reference)
        {
            var info = AssetDatabase.LoadAssetAtPath<BodyInfo>($"{MoonData}{spec.Id}.asset");
            var planetRoot = prefab.Find("force_grab_root");
            var planet = planetRoot != null ? planetRoot.GetComponent<PlanetForceSolver>() : null;
            if (info == null || planet == null)
            {
                Debug.LogError($"SolarMoonBuilder: '{spec.Id}' skipped - no data asset or no planet grab root.");
                return false;
            }

            var rig = spec.Rig ?? spec.Id;
            var tilt = Find(planetRoot, $"{rig}_axis_tilt") ?? NewOrbit(planetRoot, spec, reference);
            var offsetter = Find(tilt, $"{rig}_orbit_offsetter");
            if (spec.Rig == null)
            {
                // Orbits this builder made are re-laid on every run, so tuning a distance or a tilt needs no cleanup.
                tilt.localEulerAngles = new Vector3(spec.TiltDegrees, 0f, 0f);
                if (spec.OrbitRadius > 0f) offsetter.localPosition = new Vector3(spec.OrbitRadius, 0f, 0f);
            }
            var anchor = offsetter.Find($"{rig}_anchor");
            var grab = prefab.Find($"{spec.Id}_force_grab_root");

            if (grab == null)
            {
                var scale = offsetter.GetComponentInChildren<PlanetOffsetScaleController>(true);
                var surface = scale != null ? scale.GetComponentInChildren<MeshRenderer>(true) : null;
                if (surface == null)
                {
                    Debug.LogError($"SolarMoonBuilder: '{spec.Id}' has no model under {offsetter.name}.");
                    return false;
                }

                // The anchor sits on the model's middle, so the moon is pulled by its centre and not by a pivot
                // some models keep off to one side; the model keeps its exact pose by moving world-fixed.
                anchor = new GameObject($"{rig}_anchor").transform;
                anchor.SetParent(offsetter, false);
                anchor.position = surface.bounds.center;
                grab = new GameObject($"{spec.Id}_force_grab_root").transform;
                grab.SetParent(prefab, false);
                grab.SetPositionAndRotation(anchor.position, anchor.rotation);
                grab.localScale = Vector3.one * anchor.lossyScale.x / prefab.lossyScale.x;
                scale.transform.SetParent(grab, true);
            }

            var mesh = grab.GetComponentInChildren<MeshRenderer>(true);
            var shaped = MoonShapes.Get(spec.Id);
            if (shaped != null) mesh.GetComponent<MeshFilter>().sharedMesh = shaped;
            var collider = Ensure<SphereCollider>(mesh.gameObject);
            var bounds = mesh.GetComponent<MeshFilter>().sharedMesh.bounds;
            collider.center = bounds.center;
            collider.radius = bounds.extents.magnitude / Mathf.Sqrt(3f);
            var manipulation = Ensure<ManipulationHandler>(mesh.gameObject);
            EditorUtility.CopySerialized(meshTemplate, manipulation);
            manipulation.HostTransform = grab;
            Ensure<PostManipulationResetter>(mesh.gameObject);
            Ensure<GEInteractable>(mesh.gameObject);
            Ensure<NoAutomaticFade>(mesh.gameObject);

            var orbit = Ensure<Moon>(tilt.gameObject);
            Ensure<Animator>(tilt.gameObject).runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MoonAnimator);

            EditorUtility.CopySerialized(handlerTemplate, Ensure<SolverHandler>(grab.gameObject));
            var solver = Ensure<MoonForceSolver>(grab.gameObject);
            EditorUtility.CopySerialized(solverTemplate, solver);
            Ensure<Fader>(grab.gameObject);

            Set(orbit, "visualRoot", grab);
            Set(solver, "RootTransform", anchor, "ManipulationHandler", manipulation, "AttractionCollider", collider,
                "planetAudioClip", info.Narration, "planetAmbiantClip", null, "parentPlanet", planet, "orbit", orbit,
                "SolverHandler", grab.GetComponent<SolverHandler>());

            var card = prefab.Find($"{spec.Id}_info_card");
            if (card == null)
            {
                card = Object.Instantiate(cardTemplate, prefab, false).transform;
                card.name = $"{spec.Id}_info_card";
            }
            FillCard(card.GetComponent<PlanetInfoCard>(), info, solver, mesh);
            return true;
        }

        /// <summary>
        /// An orbit for a planet that had no moons in this scene: tilt, a rotator at the moon's period, and an
        /// offsetter a few planet radii out, holding the moon's scale controller and a sphere with its map.
        /// </summary>
        private static Transform NewOrbit(Transform planetRoot, Spec spec, float reference)
        {
            var planetMesh = planetRoot.GetComponentInChildren<PlanetOffsetScaleController>(true).GetComponentInChildren<MeshRenderer>(true);
            var planetRadius = planetMesh.bounds.extents.x / Mathf.Max(1e-5f, planetRoot.lossyScale.x);

            var group = planetRoot.Find($"{spec.Planet}_moons");
            if (group == null) group = new GameObject($"{spec.Planet}_moons").transform;
            group.SetParent(planetRoot, false);
            var tilt = new GameObject($"{spec.Id}_axis_tilt").transform;
            tilt.SetParent(group, false);
            tilt.localEulerAngles = new Vector3(spec.TiltDegrees, 0f, 0f);
            var rotator = new GameObject($"{spec.Id}_orbit_rotator").transform;
            rotator.SetParent(tilt, false);
            var rotate = rotator.gameObject.AddComponent<MoonOrbitRotator>();
            rotate.OrbitalPeriodMultiplicator = Mathf.Min(spec.Days, 40f);
            var offsetter = new GameObject($"{spec.Id}_orbit_offsetter").transform;
            offsetter.SetParent(rotator, false);
            offsetter.localPosition = new Vector3(spec.OrbitRadius > 0f ? spec.OrbitRadius : planetRadius * spec.OrbitPlanetRadii, 0f, 0f);

            var scale = new GameObject($"{spec.Id}_scale_controller");
            scale.transform.SetParent(offsetter, false);
            var controller = scale.AddComponent<PlanetOffsetScaleController>();
            controller.PlanetDiameterInKilometer = spec.Km;
            controller.UseGlobalPlanetScaleFactor = true;
            scale.transform.localScale = Vector3.one * spec.Km * .001f * PlanetOffsetScaleController.TargetOrbitScaleToCm *
                                         PlanetOffsetScaleController.GlobalPlanetScaleFactor;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = $"{spec.Id}_moon_model";
            sphere.transform.SetParent(scale.transform, false);
            sphere.transform.localScale = Vector3.one * reference;
            sphere.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>($"Assets/materials/{spec.Id}_moon_material.mat");
            sphere.AddComponent<SunLightReceiver>();
            return tilt;
        }

        /// <summary>
        /// How wide an inherited moon model is under its own scale controller, so a unit sphere under one is sized
        /// the same way and a new moon sits in true proportion to the old ones. Measured, from Ganymede.
        /// </summary>
        private static float ReferenceMeshDiameter()
        {
            var jupiter = PrefabUtility.LoadPrefabContents($"{PoiFolder}poi_jupiter_prefab.prefab");
            try
            {
                var tilt = Find(jupiter.transform, "ganymede_axis_tilt");
                var scale = (tilt != null ? tilt : jupiter.transform).GetComponentInChildren<PlanetOffsetScaleController>(true);
                var grab = jupiter.transform.Find("ganymede_force_grab_root");
                if (scale == null && grab != null) scale = grab.GetComponentInChildren<PlanetOffsetScaleController>(true);
                var mesh = scale.GetComponentInChildren<MeshRenderer>(true);
                return mesh.bounds.size.x / Mathf.Max(1e-6f, scale.transform.lossyScale.x);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(jupiter);
            }
        }

        private static void FillCard(PlanetInfoCard card, BodyInfo info, MoonForceSolver solver, Renderer surface)
        {
            var so = new SerializedObject(card);
            so.FindProperty("body").objectReferenceValue = solver;
            so.FindProperty("bodyRenderer").objectReferenceValue = surface;
            so.FindProperty("title").stringValue = info.DisplayName.ToUpperInvariant();
            so.FindProperty("subtitle").stringValue = info.Orbits != null ? $"MOON OF {info.Orbits.DisplayName.ToUpperInvariant()}" : "";
            so.FindProperty("description").stringValue = info.Paragraph;
            var stats = info.Stats.Where(s => s.Label != "ORBITS").ToArray();
            var facts = so.FindProperty("facts");
            facts.arraySize = stats.Length;
            for (var i = 0; i < stats.Length; i++)
            {
                var fact = facts.GetArrayElementAtIndex(i);
                fact.FindPropertyRelative("Label").stringValue = stats[i].Label;
                fact.FindPropertyRelative("Value").stringValue = $"{stats[i].Value} {stats[i].Unit}".Trim();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Not '??': a missing component is not a true null in the editor.
        private static T Ensure<T>(GameObject go) where T : Component
        {
            var found = go.GetComponent<T>();
            return found != null ? found : go.AddComponent<T>();
        }

        private static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static void Set(Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (var i = 0; i < pairs.Length; i += 2)
            {
                var property = so.FindProperty((string)pairs[i]);
                if (property == null)
                {
                    Debug.LogWarning($"SolarMoonBuilder: {target.GetType().Name} has no '{pairs[i]}'.");
                    continue;
                }
                property.objectReferenceValue = (Object)pairs[i + 1];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
