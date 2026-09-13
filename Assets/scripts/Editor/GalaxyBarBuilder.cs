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
    /// Builds the bar that sits across the top of the Galaxies sphere, listing every galaxy we know.
    ///
    /// <para>A screen-space overlay, like <c>DesktopDock</c> and for the same reason: it has to be legible and
    /// always in the same place on a monitor. The headset gets a world-space version when the Quest pass
    /// reaches it - the component that drives this one does not care which it is driving.</para>
    ///
    /// <para>The built galaxies come from the Galaxies module's own <c>Places</c> list, so
    /// <c>Build Galaxy Choices</c> stays the one place that decides what we have. The rest are named here and
    /// carry no module, which is what greys them: the seven in <c>docs/research/galaxies.md</c> that need a
    /// generator we have not written.</para>
    /// </summary>
    public static class GalaxyBarBuilder
    {
        private const string PrefabFolder = "Assets/prefabs/ui";
        private const string PrefabName = "galaxy_bar_prefab";
        private const string HostPath = "Assets/data/experiences/galaxies.asset";

        private const float BarHeight = 64f;
        private const float EntryWidth = 150f;
        private const float EntryHeight = 40f;
        private const float Gap = 8f;

        private static readonly Color Plate = new Color(0.055f, 0.078f, 0.094f, 0.85f);
        private static readonly Color Ink = new Color(1f, 1f, 1f, 0.92f);

        /// <summary>
        /// The ones with no generator yet, in the order <c>docs/research/galaxies.md</c> ranks them. Named
        /// rather than hidden: a menu that lists four galaxies looks finished, and this set is not.
        /// </summary>
        private static readonly (string Id, string Name)[] Coming =
        {
            ("ngc1300", "NGC 1300"),
            ("ngc4565", "NGC 4565"),
            ("sombrero", "Sombrero (M104)"),
            ("m87", "M87"),
            ("centaurus_a", "Centaurus A"),
            ("lmc", "Large Magellanic Cloud"),
            ("antennae", "Antennae"),
        };

        [MenuItem("Cosmic Simulation/Build Galaxy Bar")]
        public static void Build()
        {
            var host = AssetDatabase.LoadAssetAtPath<ExperienceModule>(HostPath);
            if (host == null)
            {
                Debug.LogError($"GalaxyBarBuilder: {HostPath} is missing, so the bar has no place to belong to.");
                return;
            }

            var built = new List<ExperienceModule>();
            if (host.Places != null)
            {
                foreach (var place in host.Places)
                {
                    if (place != null) built.Add(place);
                }
            }

            var root = new GameObject(PrefabName, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var group = root.AddComponent<CanvasGroup>();

            var bar = Rect("bar", root.transform, 0f, BarHeight);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(0f, -BarHeight);
            bar.offsetMax = Vector2.zero;

            var plate = Image("plate", bar, Plate);
            plate.rectTransform.anchorMin = Vector2.zero;
            plate.rectTransform.anchorMax = Vector2.one;
            plate.rectTransform.offsetMin = Vector2.zero;
            plate.rectTransform.offsetMax = Vector2.zero;
            plate.raycastTarget = false;

            var title = Text("title", bar, "GALAXIES", 15f, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(24f, 0f);
            title.rectTransform.sizeDelta = new Vector2(140f, EntryHeight);
            title.color = new Color(1f, 1f, 1f, 0.45f);
            title.characterSpacing = 8f;

            var buttons = new List<Button>();
            var labels = new List<TMP_Text>();
            var fills = new List<Image>();
            var modules = new List<ExperienceModule>();

            var total = built.Count + Coming.Length;
            var span = total * EntryWidth + (total - 1) * Gap;
            var start = -span * 0.5f + EntryWidth * 0.5f;

            for (var i = 0; i < total; i++)
            {
                var module = i < built.Count ? built[i] : null;
                var name = module != null ? module.DisplayName : Coming[i - built.Count].Name;

                var fill = Image($"entry_{i}", bar, Plate);
                var rect = fill.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(EntryWidth, EntryHeight);
                rect.anchoredPosition = new Vector2(start + i * (EntryWidth + Gap), 0f);

                var label = Text("label", rect, name, 14f, TextAlignmentOptions.Center);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 14f;

                var button = fill.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;

                buttons.Add(button);
                labels.Add(label);
                fills.Add(fill);
                modules.Add(module);
            }

            var component = root.AddComponent<GalaxyBar>();
            var so = new SerializedObject(component);
            so.FindProperty("host").objectReferenceValue = host;
            so.FindProperty("group").objectReferenceValue = group;
            Fill(so.FindProperty("entryButtons"), buttons);
            Fill(so.FindProperty("entryLabels"), labels);
            Fill(so.FindProperty("entryFills"), fills);
            Fill(so.FindProperty("entries"), modules);
            so.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(PrefabFolder);
            var path = $"{PrefabFolder}/{PrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"GalaxyBarBuilder: {path} written - {built.Count} galaxy(ies) we can open, " +
                      $"{Coming.Length} listed as coming.");
        }

        private static void Fill<T>(SerializedProperty list, List<T> values) where T : Object
        {
            list.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static RectTransform Rect(string name, Transform parent, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image Image(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = colour;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size,
                                     TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = Ink;
            label.raycastTarget = false;
            return label;
        }
    }
}
