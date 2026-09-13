// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using CosmicSimulation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Puts the named galaxies into the Galaxies sphere, each where it really is, each one a way in.
    ///
    /// <para>The sphere is 420 cutouts from two deep fields: real galaxies, but anonymous ones. Andromeda is
    /// not among them and cannot be - nobody photographed it in the Ultra Deep Field. So each galaxy we have
    /// built gets a pin: its own portrait (<see cref="GalaxyPortraitBuilder"/>, plotted from its own baked
    /// stars) hanging at its own galactic bearing, with a label under it that opens that galaxy as a place.
    ///
    /// <para><b>Not the Milky Way.</b> It was pinned here at l = 0, b = 0 and that was wrong twice over: the
    /// only picture of it we own is its dock thumbnail, which is a piece of interface rather than a galaxy,
    /// and a sphere of other galaxies is not where a player goes to find the one they are standing in. It is
    /// reached from the bar across the top of this place instead.</para>
    ///
    /// <para><b>The convention, written down because nothing else in the project fixes it.</b> Galactic
    /// longitude runs from Unity's +Z, which is where the player faces when the sphere opens, and increases
    /// toward +X; latitude lifts toward +Y. So the galactic centre is straight ahead, the galactic poles are
    /// overhead and underfoot, and the disc of our galaxy is the horizon. Anyone re-deriving these numbers
    /// should re-derive them against that sentence.</para>
    /// </summary>
    public static class GalaxyPinBuilder
    {
        private const string PrefabPath = "Assets/prefabs/experiences/galaxies_content_prefab.prefab";
        private const string LabelPrefabPath = "Assets/prefabs/ui/label_button_prefab.prefab";
        private const string ModuleFolder = "Assets/data/experiences";
        private const string PortraitFolder = "Assets/Textures/galaxies/portraits";
        private const string PinsNode = "galaxy_pins";

        /// <summary>Where the portraits hang. The shell itself is at 3 m; a pin sits just inside it.</summary>
        private const float ShellRadius = 2.85f;

        /// <summary>Portrait width in metres at that radius - about 10 degrees across, a hand at arm's length.</summary>
        private const float PortraitMetres = 0.5f;

        /// <summary>How far under the portrait the label hangs, in metres.</summary>
        private const float LabelDrop = 0.32f;

        /// <summary>The map conversion for a millimetre-authored label, as DestinationTagBuilder does it.</summary>
        private const float LabelScale = 0.0016f;

        private readonly struct Pin
        {
            public readonly string Id;
            public readonly float Longitude;
            public readonly float Latitude;
            public readonly string Portrait;

            public Pin(string id, float longitude, float latitude, string portrait)
            {
                Id = id;
                Longitude = longitude;
                Latitude = latitude;
                Portrait = portrait;
            }
        }

        /// <summary>
        /// Galactic l and b, textbook figures. M51, M101 and M33 are the same three that used to be pinned on
        /// the Milky Way's own map, carried over unchanged - the arithmetic was the expensive part, not the
        /// table. Andromeda's pair is as standard as they come. The Milky Way's is the definition of the
        /// coordinate system rather than a measurement.
        /// </summary>
        private static readonly Pin[] Pins =
        {
            new Pin("andromeda",  121.17f, -21.57f, PortraitFolder + "/andromeda_portrait.png"),
            new Pin("whirlpool",  104.85f,  68.56f, PortraitFolder + "/whirlpool_portrait.png"),
            new Pin("pinwheel",   102.04f,  59.77f, PortraitFolder + "/pinwheel_portrait.png"),
            new Pin("triangulum", 133.61f, -31.33f, PortraitFolder + "/triangulum_portrait.png"),
        };

        [MenuItem("Cosmic Simulation/Build Galaxy Pins")]
        public static void Build()
        {
            var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPrefabPath);
            if (labelPrefab == null)
            {
                Debug.LogError($"GalaxyPinBuilder: {LabelPrefabPath} is missing, so there is nothing to label a pin with.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError($"GalaxyPinBuilder: {PrefabPath} could not be opened.");
                return;
            }

            try
            {
                var old = contents.transform.Find(PinsNode);
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var root = new GameObject(PinsNode);
                root.transform.SetParent(contents.transform, false);
                var set = root.AddComponent<GalaxyPins>();

                var built = new List<LabelButton>();
                var missing = new List<string>();

                foreach (var pin in Pins)
                {
                    var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>($"{ModuleFolder}/{pin.Id}.asset");
                    if (module == null) { missing.Add(pin.Id + " (no module)"); continue; }

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pin.Portrait);
                    if (sprite == null) missing.Add(pin.Id + " (no portrait)");

                    var label = BuildPin(root.transform, labelPrefab, module, pin, sprite);
                    if (label != null) built.Add(label);
                }

                var so = new SerializedObject(set);
                var list = so.FindProperty("pins");
                list.arraySize = built.Count;
                for (var i = 0; i < built.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = built[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"GalaxyPinBuilder: {built.Count} pin(s) written into {PrefabPath} under '{PinsNode}'"
                          + (missing.Count > 0 ? $"; incomplete: {string.Join(", ", missing)}" : string.Empty));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static LabelButton BuildPin(Transform parent, GameObject labelPrefab, ExperienceModule module,
                                            Pin pin, Sprite portrait)
        {
            var node = new GameObject("pin_" + pin.Id);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = Direction(pin.Longitude, pin.Latitude) * ShellRadius;

            if (portrait != null)
            {
                var art = new GameObject("portrait");
                art.transform.SetParent(node.transform, false);
                var renderer = art.AddComponent<SpriteRenderer>();
                renderer.sprite = portrait;

                // The sprite's own pixels-per-unit decides its size, so the scale is worked back from the
                // metres this pin should cover rather than guessed.
                var width = portrait.bounds.size.x;
                art.transform.localScale = Vector3.one * (width > 0.0001f ? PortraitMetres / width : 1f);

                // Face the player, always: a flat portrait seen edge-on is a line, and the player walks
                // around inside this sphere.
                art.AddComponent<GalaxyExplorer.XR.Billboard>();
            }

            var instance = PrefabUtility.InstantiatePrefab(labelPrefab, node.transform) as GameObject;
            if (instance == null) return null;

            instance.name = "label_" + pin.Id;
            instance.transform.localPosition = Vector3.down * LabelDrop;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = labelPrefab.transform.localScale * LabelScale;

            var button = instance.GetComponent<LabelButton>();
            if (button == null)
            {
                Debug.LogError($"GalaxyPinBuilder: {LabelPrefabPath} has no LabelButton on its root.");
                Object.DestroyImmediate(instance);
                return null;
            }

            var so = new SerializedObject(button);
            so.FindProperty("destination").objectReferenceValue = module;
            var text = so.FindProperty("label").objectReferenceValue as TMP_Text;
            var second = so.FindProperty("secondLine").objectReferenceValue as TMP_Text;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (text != null) text.text = module.DisplayName;
            if (second != null) second.text = module.SecondLine;

            return button;
        }

        /// <summary>
        /// A galactic bearing as a direction in the sphere: l from +Z toward +X, b lifting toward +Y. See the
        /// class note - this is the only place the convention is fixed.
        /// </summary>
        private static Vector3 Direction(float longitudeDegrees, float latitudeDegrees)
        {
            var l = longitudeDegrees * Mathf.Deg2Rad;
            var b = latitudeDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(b) * Mathf.Sin(l), Mathf.Sin(b), Mathf.Cos(b) * Mathf.Cos(l));
        }
    }
}
