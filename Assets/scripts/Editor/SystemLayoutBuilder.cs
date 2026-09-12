// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Generates the two arrangements — <b>Row</b> and <b>Relative Size</b> — for a
    /// <see cref="SystemProfile"/> that the GDD does not describe, and hangs them on that system's module.
    /// <see cref="SolarRowBuilder"/> needs a <see cref="LayoutPreset"/> whose slots name the profile's bodies
    /// before it can author anything, so without this a new system cannot be built at all.
    ///
    /// <para><b>It refuses our own solar system, on purpose.</b> Those two arrangements are owned by
    /// <c>LayoutPresetBuilder</c>, which writes them from GDD 4.1 <i>literally</i> — ten bodies, 15 cm across,
    /// 25 cm apart, and a Relative Size table in which Pluto is deliberately 5 mm. This builder restates the
    /// same geometry rules for a system the GDD says nothing about, and if it were pointed at
    /// <c>solar_row</c> it would overwrite the design contract with its own arithmetic. Two builders sharing
    /// the constants below is a duplication worth removing later — the right end state is that
    /// <c>LayoutPresetBuilder</c> calls into this with our system's profile — but that means editing a file
    /// this change does not own, and quietly regenerating <c>solar_row.asset</c> is exactly the way to fail the
    /// one acceptance test that matters. The constants are public so that follow-up is mechanical.</para>
    ///
    /// <para><b>What the report is for.</b> Most of the interesting exoplanet systems are nothing like ours in
    /// shape, and several of them break assumptions the Solar Row layout was written under. The report says
    /// which ones, per system, in the terms the arrangement actually uses — sweep, pitch, closest surfaces, and
    /// the ratio of the largest body to the smallest against the 5 cm - 3 m body clamp — so the answer is a
    /// measurement rather than an opinion.</para>
    ///
    /// Menu: <b>Cosmic Simulation -> Build System Layouts</b>, over the profile selected in the Project window.
    /// </summary>
    public static class SystemLayoutBuilder
    {
        public const string Folder = "Assets/data/layouts";

        // ---------- GDD 4.1's numbers, restated
        //
        // Identical to LayoutPresetBuilder's, and public so the two can be folded together without either
        // one's numbers having to be retyped a third time.

        /// <summary>GDD 4.1: "all bodies animate back into the chosen layout over 0.8 s".</summary>
        public const float TransitionSeconds = 0.8f;

        /// <summary>GDD 4.1: every body 15 cm across in the Row arrangement.</summary>
        public const float RowDiameter = 0.15f;

        /// <summary>GDD 4.1: 25 cm apart, centre to centre.</summary>
        public const float RowSpacing = 0.25f;

        /// <summary>GDD 4.1: 1.2 m above the floor.</summary>
        public const float RowHeight = 1.2f;

        /// <summary>GDD 4.1: an arc of radius 1.1 m centred on the player's start position.</summary>
        public const float ArcRadius = 1.1f;

        /// <summary>GDD 4.1: small bodies keep a 6 cm invisible grab sphere, so that is the room they take up.</summary>
        public const float GrabSphere = 0.06f;

        /// <summary>GDD 4.1: the largest body in Relative Size is 3 m across (our Sun).</summary>
        public const float LargestRelativeDiameter = 3f;

        /// <summary>Clear air between two bodies, as a fraction of the distance at which they would touch.</summary>
        public const float GapFraction = 0.10f;

        /// <summary>Least clear air between any two bodies, whatever their size.</summary>
        public const float MinGap = 0.10f;

        /// <summary>Keep the largest body's surface this far off a player standing at the arc's centre.</summary>
        public const float PlayerClearance = 0.5f;

        /// <summary>A body wider than chest height is lifted until it clears the floor by this much.</summary>
        public const float FloorClearance = 0.05f;

        /// <summary>
        /// The body scale clamp two hands are held to (<c>ScaleLimits.Kind.Body</c>): 5 cm to 3 m, a window of
        /// sixty to one. A system whose largest and smallest bodies are further apart than this cannot have a
        /// true-proportion arrangement in which every body is also comfortably grabbable, which is a fact about
        /// the system and not a bug — ours is 586 : 1 and fails it, which is why the GDD authors Pluto at 5 mm.
        /// </summary>
        public const float BodyClampRatio = 60f;

        /// <summary>Wider than this and the row wraps around behind the player rather than standing in front.</summary>
        private const float MaxComfortableSweepDegrees = 180f;

        [MenuItem("Cosmic Simulation/Build System Layouts")]
        public static void BuildSelected()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("SystemLayoutBuilder: leave play mode first.");
                return;
            }

            var profile = Selection.activeObject as SystemProfile;
            if (profile == null)
            {
                Debug.LogError("SystemLayoutBuilder: select a SystemProfile in the Project window first " +
                               "(Assets/data/systems).");
                return;
            }

            Build(profile);
        }

        /// <summary>
        /// Writes <c>&lt;id&gt;_row</c> and <c>&lt;id&gt;_relative</c> for this profile and attaches them to
        /// its module, replacing the assets in place so their GUIDs survive.
        /// </summary>
        public static void Build(SystemProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Id))
            {
                Debug.LogError("SystemLayoutBuilder: the profile has no id; nothing written.");
                return;
            }

            if (profile.Id == SystemProfileBuilder.SolarSystemId)
            {
                Debug.LogError(
                    "SystemLayoutBuilder: our own solar system's two arrangements are GDD 4.1 verbatim and are " +
                    "owned by Cosmic Simulation > Build Layout Presets. Running this over them would replace " +
                    "the design contract with this builder's arithmetic — in particular the GDD's deliberate " +
                    "5 mm Pluto. Nothing written.");
                return;
            }

            var bodies = (profile.Bodies ?? System.Array.Empty<SystemBody>())
                .Where(b => b != null && !string.IsNullOrEmpty(b.Id))
                .ToArray();

            if (bodies.Length < 2)
            {
                Debug.LogError($"SystemLayoutBuilder: '{profile.Id}' has {bodies.Length} usable bodies; an " +
                               "arrangement needs at least two. Nothing written.");
                return;
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var row = Write($"{profile.Id}_row", "Row", "One line, all the same size", Row(bodies));
            var relative = Write($"{profile.Id}_relative", "Relative Size", "True sizes next to each other",
                                 Relative(bodies, out var missingDiameters));

            AssetDatabase.SaveAssets();
            Attach(profile, row, relative);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report(profile, bodies, row, relative, missingDiameters);
        }

        // ---------- the two arrangements

        /// <summary>
        /// Every body the same size on the arc, evenly spaced and symmetrical about straight ahead. Spacing is
        /// centre to centre in a straight line, as the GDD words it, so the step is the angle that subtends a
        /// 25 cm chord rather than 25 cm measured along the curve.
        ///
        /// <para>This rule is the one thing that transfers to any system unchanged, because it throws the
        /// sizes away. The only thing a system's shape can do to it is make it long: a system with many
        /// planets sweeps further round the player, which the report measures.</para>
        /// </summary>
        private static LayoutSlot[] Row(SystemBody[] bodies)
        {
            var step = ArcStep(RowSpacing, ArcRadius);
            var slots = new LayoutSlot[bodies.Length];

            for (var i = 0; i < bodies.Length; i++)
            {
                var angle = (i - (bodies.Length - 1) * 0.5f) * step;
                slots[i] = Slot(bodies[i].Id, angle, ArcRadius, RowHeight, RowDiameter);
            }

            return slots;
        }

        /// <summary>
        /// The same order and the same arc, every body at its true proportion, scaled so the largest is the
        /// GDD's 3 m. Each gap is sized from the pair that shares it — the distance at which they would touch,
        /// a tenth of that again, and a fixed 10 cm — with a small body counting as its 6 cm grab sphere rather
        /// than its pinhead of a disc, so two of them never end up with overlapping targets.
        ///
        /// <para>Published diameters are the only input, so <b>every ratio in this arrangement is true</b>.
        /// That is worth more here than it is for our own system: a dozen of the researched systems have a
        /// star-to-smallest-planet ratio inside the 5 cm - 3 m clamp, which ours, at 586 to 1, does not.</para>
        /// </summary>
        private static LayoutSlot[] Relative(SystemBody[] bodies, out List<string> missingDiameters)
        {
            missingDiameters = new List<string>();

            var largestKm = bodies.Max(b => b.DiameterKm);
            var metresPerKm = largestKm > 0f ? LargestRelativeDiameter / largestKm : 0f;

            var diameters = new float[bodies.Length];
            var spans = new float[bodies.Length];

            for (var i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].DiameterKm > 0f && metresPerKm > 0f)
                {
                    diameters[i] = bodies[i].DiameterKm * metresPerKm;
                }
                else
                {
                    // A body with no measured radius gets the grab sphere and nothing else. Inferring a size
                    // from its mass, or from what a planet of its period "should" be, would be a fabrication
                    // sitting in a layout where it reads as a measurement.
                    diameters[i] = GrabSphere;
                    missingDiameters.Add(bodies[i].Id);
                }

                spans[i] = Mathf.Max(diameters[i], GrabSphere);
            }

            var radius = Mathf.Max(ArcRadius, diameters.Max() * 0.5f + PlayerClearance);

            var chords = new float[bodies.Length - 1];
            var totalAngle = 0f;
            for (var i = 0; i < bodies.Length - 1; i++)
            {
                var touching = (spans[i] + spans[i + 1]) * 0.5f;
                chords[i] = touching * (1f + GapFraction) + MinGap;
                totalAngle += ArcStep(chords[i], radius);
            }

            var slots = new LayoutSlot[bodies.Length];
            var angle = -totalAngle * 0.5f;
            for (var i = 0; i < bodies.Length; i++)
            {
                if (i > 0)
                {
                    angle += ArcStep(chords[i - 1], radius);
                }

                var height = Mathf.Max(RowHeight, diameters[i] * 0.5f + FloorClearance);
                slots[i] = Slot(bodies[i].Id, angle, radius, height, diameters[i]);
            }

            return slots;
        }

        // ---------- geometry

        /// <summary>The angle that subtends a straight-line chord on a circle of this radius.</summary>
        private static float ArcStep(float chord, float radius)
        {
            var ratio = Mathf.Clamp(chord / (2f * radius), -1f, 1f);
            return 2f * Mathf.Asin(ratio);
        }

        /// <summary>
        /// A body on the arc at <paramref name="angle"/> from straight ahead, turned to face the arc's centre,
        /// which is where the player is standing.
        /// </summary>
        private static LayoutSlot Slot(string bodyId, float angle, float radius, float height, float diameter)
        {
            return new LayoutSlot
            {
                BodyId = bodyId,
                LocalPosition = new Vector3(radius * Mathf.Sin(angle), height, radius * Mathf.Cos(angle)),
                LocalEuler = new Vector3(0f, angle * Mathf.Rad2Deg + 180f, 0f),
                Scale = diameter,
            };
        }

        // ---------- writing

        private static LayoutPreset Write(string id, string displayName, string secondLine, LayoutSlot[] slots)
        {
            var path = $"{Folder}/{id}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<LayoutPreset>(path);
            var existed = preset != null;

            if (!existed)
            {
                preset = ScriptableObject.CreateInstance<LayoutPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            preset.Id = id;
            preset.DisplayName = displayName;
            preset.SecondLine = secondLine;
            preset.TransitionSeconds = TransitionSeconds;
            preset.Slots = slots;
            EditorUtility.SetDirty(preset);

            Debug.Log($"SystemLayoutBuilder: {(existed ? "updated" : "created")} {path} " +
                      $"('{displayName}', {slots.Length} slots).");
            return preset;
        }

        private static void Attach(SystemProfile profile, params LayoutPreset[] presets)
        {
            if (profile.Module == null)
            {
                Debug.LogError($"SystemLayoutBuilder: '{profile.Id}' names no experience module, so the " +
                               "arrangements were written but nothing offers them.");
                return;
            }

            var so = new SerializedObject(profile.Module);
            var list = so.FindProperty("Layouts");
            list.arraySize = presets.Length;
            for (var i = 0; i < presets.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile.Module);

            if (profile.AuthoredLayoutId != presets[0].Id)
            {
                Debug.LogWarning(
                    $"SystemLayoutBuilder: '{profile.Id}' says it is authored in '{profile.AuthoredLayoutId}' " +
                    $"but the first arrangement written is '{presets[0].Id}'. SolarRowBuilder will fall back to " +
                    "the module's first layout, which is that one, so this works — but the profile's " +
                    "AuthoredLayoutId should be corrected in SystemProfileBuilder's table.");
            }
        }

        // ---------- checking the run, and the shapes this model does not fit

        private static void Report(SystemProfile profile, SystemBody[] bodies, LayoutPreset row,
                                   LayoutPreset relative, List<string> missingDiameters)
        {
            var text = new StringBuilder();
            text.AppendLine($"SystemLayoutBuilder: '{profile.Id}' ({profile.DisplayName}), {bodies.Length} " +
                            $"bodies, two arrangements written.");

            Measure(text, "Row", row.Slots);
            Measure(text, "Relative Size", relative.Slots);

            // The ratio that decides whether a true-proportion arrangement is possible at all.
            var measured = bodies.Where(b => b.DiameterKm > 0f).ToArray();
            if (measured.Length >= 2)
            {
                var largest = measured.Max(b => b.DiameterKm);
                var smallest = measured.Min(b => b.DiameterKm);
                var ratio = largest / smallest;
                var smallestMetres = LargestRelativeDiameter * smallest / largest;

                text.AppendLine(
                    $"  proportions: largest to smallest is {ratio:F1} : 1, so with the largest at " +
                    $"{LargestRelativeDiameter:F1} m the smallest is {smallestMetres * 100f:F2} cm. " +
                    (ratio <= BodyClampRatio
                        ? $"That is inside the {BodyClampRatio:F0} : 1 body clamp (5 cm - 3 m), so every body " +
                          "in Relative Size is both true and comfortably grabbable."
                        : $"That is outside the {BodyClampRatio:F0} : 1 body clamp, so the smallest bodies sit " +
                          $"below the 5 cm floor and are held by their {GrabSphere * 100f:F0} cm grab sphere " +
                          "instead — the same compromise GDD 4.1 makes for Pluto."));
            }

            if (missingDiameters.Count > 0)
            {
                text.AppendLine(
                    $"  NO MEASURED DIAMETER: {string.Join(", ", missingDiameters)}. Each is placed at the " +
                    $"{GrabSphere * 100f:F0} cm grab minimum rather than at a size inferred from something " +
                    "else, and its panel has to carry a stat that reads 'not measured' rather than a number.");
            }

            ReportShapeMismatches(text, profile, bodies);
            Debug.Log(text.ToString());
        }

        private static void Measure(StringBuilder text, string name, LayoutSlot[] slots)
        {
            var first = Mathf.Atan2(slots[0].LocalPosition.x, slots[0].LocalPosition.z);
            var last = Mathf.Atan2(slots[slots.Length - 1].LocalPosition.x, slots[slots.Length - 1].LocalPosition.z);
            var sweep = Mathf.Abs(last - first) * Mathf.Rad2Deg;

            var closest = float.MaxValue;
            var pair = "none";
            for (var i = 0; i < slots.Length; i++)
            {
                for (var j = i + 1; j < slots.Length; j++)
                {
                    var span = (Mathf.Max(slots[i].Scale, GrabSphere) + Mathf.Max(slots[j].Scale, GrabSphere)) * 0.5f;
                    var gap = Vector3.Distance(slots[i].LocalPosition, slots[j].LocalPosition) - span;
                    if (gap < closest)
                    {
                        closest = gap;
                        pair = $"{slots[i].BodyId}/{slots[j].BodyId}";
                    }
                }
            }

            text.AppendLine(
                $"  {name}: sweep {sweep:F1} deg; diameters {slots.Min(s => s.Scale) * 100f:F2}-" +
                $"{slots.Max(s => s.Scale) * 100f:F1} cm; closest surfaces {closest * 100f:F2} cm ({pair})" +
                (closest < 0f ? "  OVERLAP" : string.Empty) +
                (sweep > MaxComfortableSweepDegrees
                    ? $"  SWEEP WRAPS PAST {MaxComfortableSweepDegrees:F0} deg: bodies at the ends sit behind " +
                      "the player. Either raise the arc radius for this system or split the row."
                    : string.Empty) + ".");
        }

        /// <summary>
        /// The places where this app's model of a planetary system does not fit a real one. Every line here is
        /// something the researcher needs to know before a system is committed to, so it is printed per system
        /// rather than kept in a document nobody opens next to the editor.
        /// </summary>
        private static void ReportShapeMismatches(StringBuilder text, SystemProfile profile, SystemBody[] bodies)
        {
            var stars = profile.StarCount;

            if (stars == 0)
            {
                text.AppendLine(
                    "  NO CENTRAL STAR. Nothing lights the bodies and nothing answers SunTouchResponse. A " +
                    "pulsar system (PSR B1257+12, where the first exoplanets were found) and a free-floating " +
                    "planet both land here, and neither is drawable as a lit ball by this model. Not handled: " +
                    "they need their own presentation.");
            }
            else if (stars > 1)
            {
                text.AppendLine(
                    $"  CIRCUMBINARY / MULTIPLE STAR ({stars} stars). Handled only as far as the arrangement " +
                    "goes: the profile carries as many Star bodies as the system has, the row places all of " +
                    "them, and the first one is what lights the others. What is NOT handled is that the " +
                    "planets orbit the pair's barycentre rather than either star, so any orbit drawing for " +
                    "this system would be wrong, and the second star does not light anything at all.");
            }

            var unclassified = bodies
                .Where(b => !b.IsStar && b.SourcePrefab == null && b.Appearance.Class == BodyClass.Unknown)
                .Select(b => b.Id)
                .ToArray();
            if (unclassified.Length > 0)
            {
                text.AppendLine(
                    $"  CLASS NOT ASSIGNED: {string.Join(", ", unclassified)}. A composition class needs a " +
                    "published mass as well as a radius, and for much of the catalogue only the radius is " +
                    "measured — a transiting planet gives up its size long before it gives up its mass. These " +
                    "bodies are drawn as neutral grey placeholders on purpose, so they do not claim to be " +
                    "rock or gas, and their mass stat has to read 'not measured' rather than a value " +
                    "converted from a radius by a mass-radius relation. Such a conversion is a model output, " +
                    "not a measurement, and it must not appear in a panel as a number.");
            }

            var noRotation = bodies
                .Where(b => !b.IsStar && b.Appearance.Spin == SpinDirection.NotMeasured)
                .Select(b => b.Id)
                .ToArray();
            if (noRotation.Length > 0)
            {
                text.AppendLine(
                    $"  ROTATION NOT MEASURED: {string.Join(", ", noRotation)}. These bodies turn in the app " +
                    "so they do not read as broken, prograde, at the GDD's one turn per 60 s. That display " +
                    "rotation is not a claim: for a close-in planet the expectation from tidal theory is that " +
                    "it is locked, so its day equals its year and the Day Length stat has no separate value. " +
                    "The stat must read 'same as its year (expected, not measured)' or 'not measured' — never " +
                    "a number.");
            }

            var span = bodies.Where(b => !b.IsStar).Select(b => b.OrbitSemiMajorAu).DefaultIfEmpty(0f).Max();
            if (span > 0f)
            {
                // 39.48 AU is Sun to Pluto: the span the GDD's own arrangements were shaped around.
                const float SolarSpanAu = 39.48f;
                var relative = span / SolarSpanAu;

                if (relative < 0.05f)
                {
                    text.AppendLine(
                        $"  COMPACT SYSTEM: the outermost body is at {span:F3} AU, {relative:F4} of " +
                        "Sun-to-Pluto. Handled, and the reason is worth stating because it is easy to assume " +
                        "otherwise: the Solar Row pitch (25 cm), the body diameter (15 cm) and the ring " +
                        "clearance (1 cm) are lengths in the player's room, not fractions of the system, so a " +
                        "system a thousand times smaller than ours arranges to exactly the same numbers. " +
                        "Neither arrangement is drawn to scale — Row throws sizes and distances away entirely " +
                        "and Relative Size keeps only the size ratios — so nothing here shrinks with the " +
                        "system. What being compact does mean is that a future to-scale orbit view would be " +
                        "far MORE honest for this system than for ours, not less: the ratio of orbit radius " +
                        "to body diameter is what makes a true orbit diagram undrawable, and for a compact " +
                        "system that ratio is about a hundred times better than ours.");
                }
                else if (relative > 1.2f)
                {
                    text.AppendLine(
                        $"  WIDE SYSTEM: the outermost body is at {span:F1} AU, {relative:F2}x Sun-to-Pluto. " +
                        "Handled for both arrangements, for the same reason — neither is drawn to scale. Note " +
                        "for the copy that a wide system's planets are directly imaged giants with orbital " +
                        "periods of centuries, so an orbital-period stat in years is a large number and the " +
                        "planets are points rather than discs in each other's skies.");
                }
            }

            var ringed = bodies.Where(b => b.Appearance.RingOuterRadiusPlanetRadii > 0f && b.SourcePrefab == null)
                .Select(b => b.Id)
                .ToArray();
            if (ringed.Length > 0)
            {
                text.AppendLine(
                    $"  RINGS ASKED FOR WITHOUT ART: {string.Join(", ", ringed)}. GenericBodyBuilder does not " +
                    "draw ring geometry, so these bodies have none and the arrangement leaves them no extra " +
                    "room. The ring-clearance solver in LayoutRig measures a real ring mesh, so it does " +
                    "nothing here — correctly, since no exoplanet ring system has been measured.");
            }

            text.AppendLine(
                "  travel: the distance between systems cannot be drawn at the same scale as a system. At a " +
                "scale where a compact system is a metre across, the trip to it is tens of thousands of " +
                "kilometres. Any journey between places is a transition, not a flight, and the copy should " +
                "say so rather than imply a distance.");
        }
    }
}
