using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cosmic.Editor
{
    public static class Layouts
    {
        const string Out = "Assets/Cosmic/Data/Generated";
        const string OldSystems = "Assets/data/systems";

        const float TransitionSeconds = 0.8f;
        const float OrbitTransitionSeconds = 1f;
        const float RowDiameterMetres = 0.15f;
        const float SchematicSunMetres = 0.3f;
        const float RowPitchMetres = 0.25f;
        const float RowHeightMetres = 1.2f;
        const float ArcRadiusMetres = 1.1f;
        const float GrabSphereMetres = 0.06f;
        const float LargestRelativeMetres = 3f;
        const float GapFraction = 0.1f;
        const float MinGapMetres = 0.1f;
        const float PlayerClearanceMetres = 0.5f;
        const float FloorClearanceMetres = 0.05f;
        const float TightClearanceMetres = 0.01f;

        // ringSpans spaced the shipped arcs and stays so a rebuild moves nothing; spanRatio is the measured width the clamp reads.
        struct Spec
        {
            public string id;
            public float diameterMetres, ringSpans, spanRatio;
        }

        struct Placement
        {
            public string id;
            public Vector3 position, euler;
            public float diameterMetres, spanMetres, spanRatio;
        }

        // GDD 4.1 tabulates Relative Size rather than scaling it - Pluto is a deliberate 5 mm, not the 0.5 mm
        // its true ratio gives - so these diameters are typed and must not be re-derived from Body.diameterKm.
        static readonly Spec[] Solar =
        {
            S("sun", 3f, 1f, 1f), S("mercury", 0.0105f, 1f, 1f), S("venus", 0.026f, 1f, 1.01f),
            S("earth", 0.0275f, 1f, 1.01f), S("mars", 0.015f, 1f, 1.0000002f),
            S("jupiter", 0.3f, 1f, 1.0072546f), S("saturn", 0.25f, 2.3f, 2.257225f),
            S("uranus", 0.11f, 2f, 1.9908094f), S("neptune", 0.106f, 1f, 1f), S("pluto", 0.005f, 1f, 1f),
        };

        [MenuItem("Cosmic/Build/Layouts")]
        public static void BuildLayouts()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Cosmic layouts: leave play mode first. Nothing written.");
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), Out, "layouts"));
                AssetDatabase.Refresh();

                Attach("solar_system_planets",
                       Write("solar_row", "Solar Row", "One line, all the same size", LayoutKind.Row, Row(Solar)),
                       Write("relative_size", "Relative Size", "True sizes next to each other",
                             LayoutKind.Relative, Relative(Solar)),
                       Write("solar_schematic", "Schematic", "Even orbit spacing, planets enlarged",
                             LayoutKind.Schematic, Orbits(Solar)),
                       Write("solar_realistic", "Realistic", "True relative orbit radii",
                             LayoutKind.Realistic, Orbits(Solar)));

                var hd110067 = FromProfile("hd110067");
                if (hd110067 != null)
                {
                    Attach("hd110067",
                           Write("hd110067_row", "Row", "One line, all the same size", LayoutKind.Row,
                                 Row(hd110067)),
                           Write("hd110067_relative", "Relative Size", "True sizes next to each other",
                                 LayoutKind.Relative, Relative(hd110067)));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        static Spec S(string id, float diameterMetres, float ringSpans, float spanRatio) =>
            new Spec { id = id, diameterMetres = diameterMetres, ringSpans = ringSpans, spanRatio = spanRatio };

        static Placement[] Row(Spec[] specs)
        {
            var step = ArcStep(RowPitchMetres, ArcRadiusMetres);
            var placements = new Placement[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var angle = (i - (specs.Length - 1) * 0.5f) * step;
                placements[i] = At(specs[i], angle, ArcRadiusMetres, RowHeightMetres, RowDiameterMetres);
            }

            return placements;
        }

        static Placement[] Orbits(Spec[] specs)
        {
            var placements = new Placement[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                placements[i] = new Placement
                {
                    id = specs[i].id,
                    diameterMetres = specs[i].id == "sun" ? SchematicSunMetres : RowDiameterMetres,
                    spanMetres = RowDiameterMetres * specs[i].ringSpans,
                    spanRatio = specs[i].spanRatio,
                };
            }

            return placements;
        }

        static Placement[] Relative(Spec[] specs)
        {
            var count = specs.Length;
            var spans = new float[count];
            var largestMetres = 0f;
            for (var i = 0; i < count; i++)
            {
                spans[i] = Mathf.Max(specs[i].diameterMetres * specs[i].ringSpans, GrabSphereMetres);
                largestMetres = Mathf.Max(largestMetres, specs[i].diameterMetres);
            }

            var radiusMetres = Mathf.Max(ArcRadiusMetres, largestMetres * 0.5f + PlayerClearanceMetres);
            var chords = new float[count - 1];
            var sweep = 0f;
            for (var i = 0; i < count - 1; i++)
            {
                var touching = (spans[i] + spans[i + 1]) * 0.5f;
                chords[i] = touching * (1f + GapFraction) + MinGapMetres;
                sweep += ArcStep(chords[i], radiusMetres);
            }

            var placements = new Placement[count];
            var angle = -sweep * 0.5f;
            for (var i = 0; i < count; i++)
            {
                angle += i > 0 ? ArcStep(chords[i - 1], radiusMetres) : 0f;
                var heightMetres = Mathf.Max(RowHeightMetres, specs[i].diameterMetres * 0.5f + FloorClearanceMetres);
                placements[i] = At(specs[i], angle, radiusMetres, heightMetres, specs[i].diameterMetres);
            }

            return placements;
        }

        static float ArcStep(float chordMetres, float radiusMetres) =>
            2f * Mathf.Asin(Mathf.Clamp(chordMetres / (2f * radiusMetres), -1f, 1f));

        static Placement At(Spec spec, float angleRadians, float radiusMetres, float heightMetres,
                            float diameterMetres) => new Placement
        {
            id = spec.id,
            position = new Vector3(radiusMetres * Mathf.Sin(angleRadians), heightMetres,
                                   radiusMetres * Mathf.Cos(angleRadians)),
            euler = new Vector3(0f, angleRadians * Mathf.Rad2Deg + 180f, 0f),
            diameterMetres = diameterMetres,
            spanMetres = Mathf.Max(diameterMetres * spec.ringSpans, GrabSphereMetres),
            spanRatio = spec.spanRatio,
        };

        static Layout Write(string id, string title, string subtitle, LayoutKind kind, Placement[] placements)
        {
            var path = $"{Out}/layouts/{id}.asset";
            var layout = AssetDatabase.LoadAssetAtPath<Layout>(path);
            var existed = layout != null;
            if (!existed)
            {
                layout = ScriptableObject.CreateInstance<Layout>();
                AssetDatabase.CreateAsset(layout, path);
            }

            var slots = new List<Slot>(placements.Length);
            var placed = new List<Placement>(placements.Length);
            foreach (var placement in placements)
            {
                var body = AssetDatabase.LoadAssetAtPath<Body>($"{Out}/bodies/{placement.id}.asset");
                if (body == null)
                {
                    Debug.LogError($"Cosmic layouts: {id} wants {Out}/bodies/{placement.id}.asset and there is " +
                                   "none, so that slot is dropped rather than written as a null reference.");
                    continue;
                }

                slots.Add(new Slot
                {
                    body = body,
                    localPosition = placement.position,
                    localEuler = placement.euler,
                    scale = placement.diameterMetres,
                    spanRatio = placement.spanRatio,
                });
                placed.Add(placement);
            }

            layout.id = id;
            layout.title = title;
            layout.subtitle = subtitle;
            layout.kind = kind;
            var orbiting = kind == LayoutKind.Schematic || kind == LayoutKind.Realistic;
            layout.transitionSeconds = orbiting ? OrbitTransitionSeconds : TransitionSeconds;
            layout.slots = slots.ToArray();
            EditorUtility.SetDirty(layout);

            if (orbiting)
            {
                Debug.Log($"Cosmic layouts: {(existed ? "updated" : "created")} {id} - {placed.Count} bodies " +
                          $"at {RowDiameterMetres * 100f:F0} cm, spaced by the orbit model.");
            }
            else
            {
                Report(id, existed, placed);
            }

            return layout;
        }

        static void Report(string id, bool existed, List<Placement> placed)
        {
            var closestMetres = float.MaxValue;
            var tight = string.Empty;
            for (var i = 0; i < placed.Count; i++)
            {
                for (var j = i + 1; j < placed.Count; j++)
                {
                    var gap = Vector3.Distance(placed[i].position, placed[j].position)
                              - (placed[i].spanMetres + placed[j].spanMetres) * 0.5f;
                    closestMetres = Mathf.Min(closestMetres, gap);
                    if (gap < TightClearanceMetres)
                    {
                        tight += $" {placed[i].id}/{placed[j].id} {gap * 1000f:F1} mm" + (gap < 0f ? " OVERLAP;" : ";");
                    }
                }
            }

            var endToEnd = placed.Count > 1
                ? Vector3.Distance(placed[0].position, placed[placed.Count - 1].position)
                : 0f;
            // Widest extent, so a ring span counts: LayoutRig clamps rings per pair at run time (CS-060), which
            // is why a negative figure here is a note rather than a failure.
            Debug.Log($"Cosmic layouts: {(existed ? "updated" : "created")} {id} - {placed.Count} bodies, " +
                      $"{endToEnd:F2} m end to end, closest widest extents " +
                      (placed.Count > 1 ? $"{closestMetres * 100f:F1} cm." : "n/a.") +
                      (tight.Length > 0 ? $" Under {TightClearanceMetres * 100f:F0} cm:{tight}" : string.Empty));
        }

        static void Attach(string placeId, params Layout[] layouts)
        {
            var path = $"{Out}/places/{placeId}.asset";
            var place = AssetDatabase.LoadAssetAtPath<Place>(path);
            if (place == null)
            {
                Debug.LogError($"Cosmic layouts: no place at {path}, so its layouts were written but nothing " +
                               "offers them.");
                return;
            }

            place.layouts = layouts;
            EditorUtility.SetDirty(place);
            Debug.Log($"Cosmic layouts: {placeId} offers {layouts.Length} layouts, opening with '{layouts[0].title}'.");
        }

        static Spec[] FromProfile(string systemId)
        {
            var path = $"{OldSystems}/{systemId}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var bodies = profile != null ? new SerializedObject(profile).FindProperty("Bodies") : null;
            if (bodies == null || bodies.arraySize < 2)
            {
                Debug.LogError($"Cosmic layouts: {path} is missing or lists fewer than two bodies, so " +
                               $"{systemId}'s two layouts were not written.");
                return null;
            }

            var ids = new List<string>(bodies.arraySize);
            var kilometres = new List<float>(bodies.arraySize);
            var largestKm = 0f;
            for (var i = 0; i < bodies.arraySize; i++)
            {
                var element = bodies.GetArrayElementAtIndex(i);
                var bodyId = element.FindPropertyRelative("Id")?.stringValue ?? string.Empty;
                if (string.IsNullOrEmpty(bodyId))
                {
                    continue;
                }

                var generated = AssetDatabase.LoadAssetAtPath<Body>($"{Out}/bodies/{bodyId}.asset");
                var km = generated != null && generated.diameterKm > 0f
                    ? generated.diameterKm
                    : element.FindPropertyRelative("DiameterKm")?.floatValue ?? 0f;
                ids.Add(bodyId);
                kilometres.Add(km);
                largestKm = Mathf.Max(largestKm, km);
            }

            var metresPerKm = largestKm > 0f ? LargestRelativeMetres / largestKm : 0f;
            var specs = new Spec[ids.Count];
            for (var i = 0; i < specs.Length; i++)
            {
                // No measured radius means the grab sphere and nothing else: a diameter inferred from mass would
                // read as a measurement in an arrangement that claims true proportions.
                var metres = kilometres[i] > 0f ? kilometres[i] * metresPerKm : GrabSphereMetres;
                specs[i] = S(ids[i], metres, 1f, 1f);
            }

            return specs.Length >= 2 ? specs : null;
        }
    }
}
