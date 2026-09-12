// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Generates the two arrangements of the Solar System Planets experience - <b>Solar Row</b> and
    /// <b>Relative Size</b> - as <see cref="LayoutPreset"/> assets, and hangs them on the module so the dock
    /// pop-up has something to offer.
    ///
    /// Every number here comes from GDD 4.1 and nowhere else. Solar Row is the literal spec: ten bodies, each
    /// 15 cm across, 25 cm apart centre to centre, 1.2 m above the floor, on an arc of radius 1.1 m centred on
    /// the player's start position. Relative Size takes the true proportions the GDD lists (Sun 3.0 m down to
    /// Pluto 0.5 cm) and keeps the same order; the GDD leaves the spacing open beyond "no body overlaps", so it
    /// is derived rather than typed: each gap is sized from the two bodies that share it.
    ///
    /// Positions are computed from those numbers, not tabulated, so a change to one constant moves the whole
    /// arrangement consistently and a reviewer can check the rule instead of forty-odd floats.
    ///
    /// <para><b>Frame of reference.</b> Slots are local to the experience's content root, which sits at the
    /// player's start position on the floor, +Z forward. So <c>y</c> is height above the floor and the arc is
    /// centred on the player, who stands inside it and turns to look along the row.</para>
    ///
    /// <para><b>What Scale means.</b> <see cref="LayoutSlot.Scale"/> is the body's diameter in metres - that is,
    /// the multiplier for a body authored at unit diameter. Bodies must be normalised to a 1 m diameter for the
    /// layout to measure true (CS-041); the log below reports the diameters this builder wrote so the scene can
    /// be checked against them.</para>
    ///
    /// Re-running rewrites the two assets in place, so their GUIDs - and every reference to them - survive.
    /// Menu: <b>Cosmic Simulation -> Build Layout Presets</b>.
    /// </summary>
    public static class LayoutPresetBuilder
    {
        private const string Folder = "Assets/data/layouts";
        private const string BodyFolder = "Assets/data/bodies";
        private const string ModulePath = "Assets/data/experiences/solar_system_planets.asset";

        /// <summary>GDD 4.1: "all bodies animate back into the chosen layout over 0.8 s".</summary>
        private const float TransitionSeconds = 0.8f;

        // ---------- Solar Row, straight from GDD 4.1

        private const float RowDiameter = 0.15f;   // every body 15 cm across
        private const float RowSpacing = 0.25f;    // 25 cm apart, centre to centre
        private const float RowHeight = 1.2f;      // 1.2 m above the floor
        private const float ArcRadius = 1.1f;      // arc of radius 1.1 m, centred on the player

        // ---------- Relative Size, derived from the diameters

        /// <summary>GDD 4.1: small bodies keep a 6 cm invisible grab sphere, so that is the room they take up.</summary>
        private const float GrabSphere = 0.06f;

        /// <summary>Clear air between two bodies, as a fraction of how far apart their surfaces would touch.</summary>
        private const float GapFraction = 0.10f;

        /// <summary>Least clear air between any two bodies, whatever their size.</summary>
        private const float MinGap = 0.10f;

        /// <summary>The Sun is 3 m across; keep its surface this far off the player standing at the arc centre.</summary>
        private const float PlayerClearance = 0.5f;

        /// <summary>A body wider than chest height is lifted until it clears the floor by this much.</summary>
        private const float FloorClearance = 0.05f;

        /// <summary>One body of the row: its id, its true-proportion diameter, and how far its rings reach.</summary>
        private struct BodySpec
        {
            public readonly string Id;

            /// <summary>Diameter in metres in Relative Size (GDD 4.1).</summary>
            public readonly float Diameter;

            /// <summary>Widest visible extent as a multiple of the diameter - the ring system, where there is one.</summary>
            public readonly float RingSpan;

            public BodySpec(string id, float diameter, float ringSpan)
            {
                Id = id;
                Diameter = diameter;
                RingSpan = ringSpan;
            }
        }

        /// <summary>
        /// The ten bodies in the order the GDD gives them, with their Relative Size diameters. Ring spans are
        /// the outer edge of the visible ring system relative to the planet: Saturn's A ring reaches about 2.3
        /// planet diameters, Uranus' outer ring about 2. They only affect spacing, so a ringed planet is not
        /// given a neighbour inside its rings.
        /// </summary>
        private static readonly BodySpec[] Bodies =
        {
            new BodySpec("sun",     3.0f,    1f),
            new BodySpec("mercury", 0.0105f, 1f),
            new BodySpec("venus",   0.026f,  1f),
            new BodySpec("earth",   0.0275f, 1f),
            new BodySpec("mars",    0.015f,  1f),
            new BodySpec("jupiter", 0.30f,   1f),
            new BodySpec("saturn",  0.25f,   2.3f),
            new BodySpec("uranus",  0.11f,   2.0f),
            new BodySpec("neptune", 0.106f,  1f),
            new BodySpec("pluto",   0.005f,  1f),
        };

        [MenuItem("Cosmic Simulation/Build Layout Presets")]
        public static void BuildAll()
        {
            if (!BodyIdsExist())
            {
                return;
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var row = Write("solar_row", "Solar Row", "One line, all the same size", SolarRow());
            var relative = Write("relative_size", "Relative Size", "True sizes next to each other", RelativeSize());

            AssetDatabase.SaveAssets();
            Attach(row, relative);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------- the two arrangements

        /// <summary>
        /// Ten equal bodies on the arc, evenly spaced and symmetrical about the player's forward direction.
        /// Spacing is centre to centre in a straight line, as the GDD words it, so the arc step is the angle
        /// that subtends a 25 cm chord - not 25 cm measured along the curve.
        /// </summary>
        private static LayoutSlot[] SolarRow()
        {
            var count = Bodies.Length;
            var step = ArcStep(RowSpacing, ArcRadius);
            var slots = new LayoutSlot[count];
            var spans = new float[count];

            for (var i = 0; i < count; i++)
            {
                var angle = (i - (count - 1) * 0.5f) * step;
                slots[i] = Slot(Bodies[i].Id, angle, ArcRadius, RowHeight, RowDiameter);
                spans[i] = Mathf.Max(RowDiameter, GrabSphere);
            }

            Report("Solar Row", slots, spans, ArcRadius);
            // Saturn's rings are the tightest thing in either layout: at 15 cm across and a 2.3x ring span they
            // reach 17.25 cm from its centre, and Jupiter's near surface is at 17.5 cm. That is 2.5 mm of
            // clearance, so a Saturn model with a wider ring span really will intersect its neighbours.
            var saturnRingSpan = System.Array.Find(Bodies, b => b.Id == "saturn").RingSpan;
            var ringRadius = RowDiameter * 0.5f * saturnRingSpan;
            var neighbourSurface = RowSpacing - RowDiameter * 0.5f;
            Debug.Log($"LayoutPresetBuilder: Solar Row keeps the GDD's {RowSpacing * 100f:F0} cm pitch. Saturn's " +
                      $"rings reach {ringRadius * 100f:F2} cm and its neighbour's surface is at " +
                      $"{neighbourSurface * 100f:F2} cm - {(neighbourSurface - ringRadius) * 1000f:F1} mm of " +
                      "clearance, the tightest in either layout. A wider ring span will intersect.");
            return slots;
        }

        /// <summary>
        /// The same order and the same arc, but every body at its true proportion, and each gap sized from the
        /// pair that shares it: the distance at which their widest extents would touch, plus a tenth of that
        /// again, plus a fixed 10 cm. Small bodies count as their 6 cm grab sphere rather than their pinhead of
        /// a disc, so two of them never end up with overlapping targets.
        ///
        /// The arc has to open out - a 3 m Sun cannot sit 1.1 m from the player's face - so its radius is the
        /// Sun's radius plus half a metre of clear air. The Sun is also lifted until it clears the floor;
        /// everything else stays at chest height.
        /// </summary>
        private static LayoutSlot[] RelativeSize()
        {
            var count = Bodies.Length;

            var spans = new float[count];
            for (var i = 0; i < count; i++)
            {
                spans[i] = Mathf.Max(Bodies[i].Diameter * Bodies[i].RingSpan, GrabSphere);
            }

            var largestRadius = Bodies.Max(b => b.Diameter) * 0.5f;
            var radius = Mathf.Max(ArcRadius, largestRadius + PlayerClearance);

            var chords = new float[count - 1];
            var totalAngle = 0f;
            for (var i = 0; i < count - 1; i++)
            {
                var touching = (spans[i] + spans[i + 1]) * 0.5f;
                chords[i] = touching * (1f + GapFraction) + MinGap;
                totalAngle += ArcStep(chords[i], radius);
            }

            var slots = new LayoutSlot[count];
            var angle = -totalAngle * 0.5f;
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    angle += ArcStep(chords[i - 1], radius);
                }

                var height = Mathf.Max(RowHeight, Bodies[i].Diameter * 0.5f + FloorClearance);
                slots[i] = Slot(Bodies[i].Id, angle, radius, height, Bodies[i].Diameter);
            }

            Report("Relative Size", slots, spans, radius);
            return slots;
        }

        // ---------- geometry

        /// <summary>The angle that subtends a straight-line <paramref name="chord"/> on a circle of this radius.</summary>
        private static float ArcStep(float chord, float radius)
        {
            var ratio = Mathf.Clamp(chord / (2f * radius), -1f, 1f);
            return 2f * Mathf.Asin(ratio);
        }

        /// <summary>
        /// A body on the arc at <paramref name="angle"/> from straight ahead, turned to face the arc's centre -
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

            Debug.Log($"LayoutPresetBuilder: {(existed ? "updated" : "created")} {path} " +
                      $"('{displayName}', {slots.Length} slots, {TransitionSeconds:F2} s transition).");
            return preset;
        }

        private static void Attach(params LayoutPreset[] presets)
        {
            var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(ModulePath);
            if (module == null)
            {
                Debug.LogError($"LayoutPresetBuilder: no experience module at {ModulePath}; presets written but " +
                               "nothing points at them.");
                return;
            }

            var so = new SerializedObject(module);
            var list = so.FindProperty("Layouts");
            list.arraySize = presets.Length;
            for (var i = 0; i < presets.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = presets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(module);

            var names = string.Join(", ", presets.Select(p => p.DisplayName));
            Debug.Log($"LayoutPresetBuilder: {module.Id} now offers {presets.Length} layouts ({names}); " +
                      $"HasLayoutChoice={module.HasLayoutChoice}. The first, {presets[0].DisplayName}, is the " +
                      "one it opens with.");
        }

        // ---------- checking the run

        /// <summary>Every id in the table has to name a real BodyInfo, or the layout places nothing.</summary>
        private static bool BodyIdsExist()
        {
            var known = new HashSet<string>(
                AssetDatabase.FindAssets("t:BodyInfo", new[] { BodyFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(p => AssetDatabase.LoadAssetAtPath<BodyInfo>(p))
                    .Where(b => b != null && !string.IsNullOrEmpty(b.Id))
                    .Select(b => b.Id));

            var missing = Bodies.Select(b => b.Id).Where(id => !known.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                Debug.LogError($"LayoutPresetBuilder: no BodyInfo in {BodyFolder} for {string.Join(", ", missing)}. " +
                               "Nothing written.");
                return false;
            }

            Debug.Log($"LayoutPresetBuilder: {known.Count} BodyInfo assets found, all {Bodies.Length} layout " +
                      "bodies matched.");
            return true;
        }

        /// <summary>
        /// Measures the arrangement that was actually built and prints it, so a run can be checked against
        /// GDD 4.1 without opening the assets: how many bodies, how far the arc sweeps, how far apart the
        /// bodies sit, how big they are, and how close any two of them come to touching.
        /// </summary>
        private static void Report(string name, LayoutSlot[] slots, float[] spans, float radius)
        {
            var count = slots.Length;

            var first = Mathf.Atan2(slots[0].LocalPosition.x, slots[0].LocalPosition.z);
            var last = Mathf.Atan2(slots[count - 1].LocalPosition.x, slots[count - 1].LocalPosition.z);
            var sweep = Mathf.Abs(last - first);

            var minStep = float.MaxValue;
            var maxStep = 0f;
            for (var i = 0; i < count - 1; i++)
            {
                var d = Vector3.Distance(slots[i].LocalPosition, slots[i + 1].LocalPosition);
                minStep = Mathf.Min(minStep, d);
                maxStep = Mathf.Max(maxStep, d);
            }

            // Closest approach over every pair, not just neighbours: on an arc a big body can reach past the
            // one beside it, and "no body overlaps" is a claim about all of them.
            var closest = float.MaxValue;
            var closestPair = "none";
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    var gap = Vector3.Distance(slots[i].LocalPosition, slots[j].LocalPosition)
                              - (spans[i] + spans[j]) * 0.5f;
                    if (gap < closest)
                    {
                        closest = gap;
                        closestPair = $"{slots[i].BodyId}/{slots[j].BodyId}";
                    }
                }
            }

            var minDiameter = slots.Min(s => s.Scale);
            var maxDiameter = slots.Max(s => s.Scale);
            var lowest = slots.Min(s => s.LocalPosition.y);
            var highest = slots.Max(s => s.LocalPosition.y);
            var overlap = closest < 0f ? " OVERLAP" : string.Empty;

            Debug.Log($"LayoutPresetBuilder: {name} - {count} bodies on an arc of radius {radius:F2} m; " +
                      $"sweep {sweep * Mathf.Rad2Deg:F1} deg ({sweep * radius:F2} m along the arc, " +
                      $"{2f * radius * Mathf.Sin(sweep * 0.5f):F2} m end to end); " +
                      $"centre to centre {minStep * 100f:F1}-{maxStep * 100f:F1} cm; " +
                      $"diameters {minDiameter * 100f:F2}-{maxDiameter * 100f:F1} cm; " +
                      $"height {lowest:F2}-{highest:F2} m; " +
                      $"closest surfaces {closest * 100f:F1} cm ({closestPair}){overlap}.");
        }
    }
}
