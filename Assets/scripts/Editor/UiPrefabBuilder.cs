// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using CosmicSimulation;
using GalaxyExplorer.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the world-space UI prefabs from the exported sprites, so the dock, its pop-up, the info panel and
    /// a destination tag are reproducible rather than assembled by hand and impossible to diff.
    ///
    /// Everything is authored on a canvas scaled so that <b>one canvas unit is one millimetre</b>. That is what
    /// makes the numbers in <c>docs/ui/spec.md</c> usable directly: a tile is 110 x 62, its foot is 26 tall, the
    /// gap between tiles is 4. The canvas' own scale of 0.001 turns those into metres at the last moment.
    ///
    /// Re-running replaces the prefabs in place, so references from scenes survive.
    /// </summary>
    public static class UiPrefabBuilder
    {
        private const string SpriteFolder = "Assets/ui/figma/";
        private const string OutputFolder = "Assets/prefabs/ui";

        // The design tokens, as colours.
        private static readonly Color Ink = Color.white;
        private static readonly Color InkSecondary = new Color32(0x9E, 0xB8, 0xC4, 0xFF);
        private static readonly Color Cyan = new Color32(0x6C, 0xCF, 0xDD, 0xFF);
        private static readonly Color Plate = new Color32(0x0E, 0x14, 0x18, 0xCC); // 80%
        private static readonly Color OnAccent = new Color32(0x0E, 0x14, 0x18, 0xFF);

        [MenuItem("Cosmic Simulation/Build UI Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();

            var tile = BuildDockTile();
            BuildDock(tile);
            BuildDockPopup();
            BuildInfoPanel();
            BuildLabelButton();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"UI prefabs written to {OutputFolder}");
        }

        // ---------- pieces

        private static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (sprite == null)
            {
                Debug.LogError($"UiPrefabBuilder: missing sprite {name}. Run Cosmic Simulation > Reimport UI Sprites.");
            }

            return sprite;
        }

        private static TMP_FontAsset Font()
        {
            // Whatever the project already uses for world-space text, so the prefabs match the existing look.
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset selawik");
            if (guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            }

            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static RectTransform Rect(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static Image Panel(string name, Transform parent, float w, float h, Sprite sprite, Color colour)
        {
            var rt = Rect(name, parent, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            // The sprites are imported at 8000 px per unit so a pixel is a millimetre; on a canvas whose unit
            // is also a millimetre the nine-slice corners then come out at their true size.
            image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, float w, float h, string text,
                                      float size, Color colour, TextAlignmentOptions align, FontWeight weight)
        {
            var rt = Rect(name, parent, w, h);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Font();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = align;
            tmp.fontWeight = weight;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;

            // Every panel floats over the room with no plate behind it, so the outline is what keeps it
            // readable against a bright wall (GDD 8.3).
            tmp.outlineWidth = 0.12f;
            tmp.outlineColor = new Color32(0x06, 0x0B, 0x10, 0xFF);
            return tmp;
        }

        private static GameObject Root(string name, bool worldCanvas)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (!worldCanvas)
            {
                return go;
            }

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            go.AddComponent<GraphicRaycaster>();

            var rt = (RectTransform)go.transform;
            rt.localScale = Vector3.one * 0.001f; // one canvas unit is a millimetre
            return go;
        }

        private static GameObject Save(GameObject go, string fileName)
        {
            var path = $"{OutputFolder}/{fileName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---------- the tile

        private static GameObject BuildDockTile()
        {
            var root = Root("dock_tile_prefab", false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(110f, 62f);

            var move = Rect("move", rt, 110f, 62f);

            // 2 mm corner, per the radius table in docs/ui/spec.md.
            var picture = Panel("thumbnail", move, 110f, 62f, Load("ui_rounded_r16"), Color.white);
            picture.raycastTarget = true; // the tile's own hit area

            var foot = Panel("foot", move, 110f, 26f, Load("ui_tile_foot"), Color.white);
            foot.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            foot.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            foot.rectTransform.pivot = new Vector2(0.5f, 0f);
            foot.rectTransform.anchoredPosition = Vector2.zero;
            foot.raycastTarget = false;

            var name = Label("name", move, 90f, 8f, "Place", 5.6f, Ink, TextAlignmentOptions.BottomLeft, FontWeight.SemiBold);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0f, 0f);
            name.rectTransform.pivot = new Vector2(0f, 0f);
            name.rectTransform.anchoredPosition = new Vector2(5f, 11f);

            var second = Label("second_line", move, 90f, 6f, "", 4.2f, InkSecondary, TextAlignmentOptions.BottomLeft, FontWeight.Regular);
            second.rectTransform.anchorMin = new Vector2(0f, 0f);
            second.rectTransform.anchorMax = new Vector2(0f, 0f);
            second.rectTransform.pivot = new Vector2(0f, 0f);
            second.rectTransform.anchoredPosition = new Vector2(5f, 4f);

            var underline = Panel("active_underline", move, 110f, 0.5f, null, Cyan);
            underline.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            underline.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            underline.rectTransform.pivot = new Vector2(0.5f, 0f);
            underline.rectTransform.anchoredPosition = Vector2.zero;
            underline.raycastTarget = false;
            underline.gameObject.SetActive(false);

            // A collider so a hand ray and a fingertip can find it; the canvas raycaster only serves the mouse.
            var box = move.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(110f, 62f, 2f);
            move.gameObject.AddComponent<GEInteractable>();
            var button = move.gameObject.AddComponent<GEButton>();

            var tile = root.AddComponent<DockTile>();
            var so = new SerializedObject(tile);
            so.FindProperty("thumbnail").objectReferenceValue = picture;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("secondLine").objectReferenceValue = second;
            so.FindProperty("activeUnderline").objectReferenceValue = underline.gameObject;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("moveTarget").objectReferenceValue = move;
            so.FindProperty("hoverLift").floatValue = 6f;   // millimetres, on this canvas
            so.FindProperty("pressDepth").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "dock_tile_prefab");
        }

        // ---------- the dock

        private static void BuildDock(GameObject tilePrefab)
        {
            var root = Root("dock_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(834f, 72f);

            Panel("plate", rt, 834f, 72f, Load("ui_rounded_r48"), Plate).raycastTarget = false;

            var row = Rect("tile_row", rt, 794f, 62f);
            row.anchoredPosition = new Vector2(-16f, 0f);

            var passthrough = SquareButton("passthrough_button", rt, 26f, 24f, Load("icon_passthrough"), Color.white, OnAccent);
            // Clear of the rightmost tile (its edge lands at 381) and inside the plate (half-width 417).
            ((RectTransform)passthrough.transform).anchoredPosition = new Vector2(400f, 0f);

            var underRow = Rect("under_dock", rt, 834f, 12f);
            underRow.anchorMin = new Vector2(0.5f, 0f);
            underRow.anchorMax = new Vector2(0.5f, 0f);
            underRow.pivot = new Vector2(0.5f, 1f);
            underRow.anchoredPosition = new Vector2(0f, -6f);

            var bar = Panel("drag_bar", underRow, 60f, 1.6f, Load("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.55f));
            bar.rectTransform.anchoredPosition = Vector2.zero;
            var barBox = bar.gameObject.AddComponent<BoxCollider>();
            barBox.size = new Vector3(60f, 8f, 2f);
            bar.gameObject.AddComponent<GEInteractable>();
            bar.gameObject.AddComponent<ManipulationHandler>();

            var recenter = SquareButton("recenter_button", underRow, 9f, 6f, Load("icon_recenter"), Plate, Ink);
            ((RectTransform)recenter.transform).anchoredPosition = new Vector2(396f, 0f);

            var help = SquareButton("help_button", underRow, 9f, 6f, Load("icon_help"), Plate, Ink);
            ((RectTransform)help.transform).anchoredPosition = new Vector2(408f, 0f);

            var dock = root.AddComponent<DockController>();
            var so = new SerializedObject(dock);
            so.FindProperty("tilePrefab").objectReferenceValue = tilePrefab != null ? tilePrefab.GetComponent<DockTile>() : null;
            so.FindProperty("tileRow").objectReferenceValue = row;
            so.FindProperty("passthroughButton").objectReferenceValue = passthrough;
            so.FindProperty("recenterButton").objectReferenceValue = recenter;
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("dragBar").objectReferenceValue = bar.transform;
            so.FindProperty("tilePitch").floatValue = 114f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "dock_prefab");
        }

        private static GEButton SquareButton(string name, Transform parent, float size, float glyph,
                                             Sprite icon, Color fill, Color glyphColour)
        {
            var back = Panel(name, parent, size, size, Load("ui_rounded_r16"), fill);
            var mark = Panel("glyph", back.rectTransform, glyph, glyph, icon, glyphColour);
            mark.raycastTarget = false;

            var box = back.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(size, size, 2f);
            back.gameObject.AddComponent<GEInteractable>();
            return back.gameObject.AddComponent<GEButton>();
        }

        // ---------- the layout pop-up

        private static void BuildDockPopup()
        {
            var root = Root("dock_popup_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(240f, 90f);

            Panel("plate", rt, 240f, 90f, Load("ui_rounded_r48"), Plate).raycastTarget = false;

            var popup = root.AddComponent<DockPopup>();
            var so = new SerializedObject(popup);
            var buttons = so.FindProperty("optionButtons");
            var labels = so.FindProperty("optionLabels");
            var fills = so.FindProperty("optionFills");
            buttons.arraySize = 2;
            labels.arraySize = 2;
            fills.arraySize = 2;

            for (var i = 0; i < 2; i++)
            {
                var fill = Panel($"option_{i}", rt, 108f, 44f, Load("ui_rounded_r32"), i == 0 ? Cyan : Plate);
                fill.rectTransform.anchoredPosition = new Vector2(i == 0 ? -58f : 58f, -6f);

                var text = Label("label", fill.rectTransform, 100f, 12f, i == 0 ? "Schematic" : "Realistic",
                    5f, i == 0 ? OnAccent : Ink, TextAlignmentOptions.Center, FontWeight.Medium);
                text.rectTransform.anchoredPosition = Vector2.zero;

                var box = fill.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(108f, 44f, 2f);
                fill.gameObject.AddComponent<GEInteractable>();
                var button = fill.gameObject.AddComponent<GEButton>();

                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
                labels.GetArrayElementAtIndex(i).objectReferenceValue = text;
                fills.GetArrayElementAtIndex(i).objectReferenceValue = fill;
            }

            var close = SquareButton("close_button", rt, 9f, 6f, Load("icon_close"), Plate, Ink);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(112f, 36f);
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "dock_popup_prefab");
        }

        // ---------- the info panel

        private static void BuildInfoPanel()
        {
            var root = Root("info_panel_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(161f, 176f);
            root.AddComponent<CanvasGroup>();

            // No plate: over passthrough a plate would punch a hole in the room.
            var title = Label("title", rt, 161f, 20f, "Earth", 16.8f, Ink, TextAlignmentOptions.TopLeft, FontWeight.Light);
            Top(title.rectTransform, 0f);

            var subtitle = Label("subtitle", rt, 161f, 8f, "OUR HOME PLANET", 5.6f, Cyan, TextAlignmentOptions.TopLeft, FontWeight.SemiBold);
            subtitle.characterSpacing = 8f;
            Top(subtitle.rectTransform, 22f);

            // A body paragraph runs to 55 words, which is about seven lines at this width; anything less and
            // the text runs into the stats below it.
            var paragraph = Label("paragraph", rt, 161f, 62f, "", 6.65f, Ink, TextAlignmentOptions.TopLeft, FontWeight.Regular);
            Top(paragraph.rectTransform, 34f);

            var divider = Panel("divider", rt, 161f, 0.25f, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            divider.raycastTarget = false;
            Top(divider.rectTransform, 102f);

            var grid = Rect("stats", rt, 161f, 44f);
            Top(grid, 110f);

            var so = new SerializedObject(root.AddComponent<InfoPanel>());
            var labels = so.FindProperty("statLabels");
            var values = so.FindProperty("statValues");
            labels.arraySize = 4;
            values.arraySize = 4;

            for (var i = 0; i < 4; i++)
            {
                var cell = Rect($"stat_{i}", grid, 78f, 20f);
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = new Vector2(0f, 1f);
                cell.pivot = new Vector2(0f, 1f);
                cell.anchoredPosition = new Vector2(i % 2 == 0 ? 0f : 83f, i < 2 ? 0f : -22f);

                var statLabel = Label("label", cell, 78f, 6f, "DIAMETER", 4.55f, InkSecondary, TextAlignmentOptions.TopLeft, FontWeight.SemiBold);
                statLabel.characterSpacing = 8f;
                Top(statLabel.rectTransform, 0f);

                var statValue = Label("value", cell, 78f, 12f, "12,742 km", 8.4f, Ink, TextAlignmentOptions.TopLeft, FontWeight.Regular);
                statValue.richText = true; // the mass exponent is a real <sup>
                Top(statValue.rectTransform, 7f);

                labels.GetArrayElementAtIndex(i).objectReferenceValue = statLabel;
                values.GetArrayElementAtIndex(i).objectReferenceValue = statValue;
            }

            var instruction = Label("instruction", rt, 161f, 20f, "", 6.65f, InkSecondary, TextAlignmentOptions.TopLeft, FontWeight.Regular);
            Top(instruction.rectTransform, 156f);

            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("subtitle").objectReferenceValue = subtitle;
            so.FindProperty("paragraph").objectReferenceValue = paragraph;
            so.FindProperty("instruction").objectReferenceValue = instruction;
            so.FindProperty("statGrid").objectReferenceValue = grid;
            so.FindProperty("metresPerUnit").floatValue = 0.001f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "info_panel_prefab");
        }

        private static void Top(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        // ---------- a destination tag

        private static void BuildLabelButton()
        {
            var root = Root("label_button_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(60f, 16f);

            var grow = Rect("grow", rt, 60f, 16f);
            var pill = Panel("pill", grow, 60f, 16f, Load("ui_rounded_r32"), Plate);

            var text = Label("name", grow, 54f, 8f, "Crab Nebula", 5f, Ink, TextAlignmentOptions.Center, FontWeight.Medium);
            text.rectTransform.anchoredPosition = Vector2.zero;

            var outline = Panel("selected_outline", grow, 64f, 20f, Load("ui_rounded_r32"), Cyan);
            outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            outline.transform.SetAsFirstSibling();

            var leader = Panel("leader", rt, 0.4f, 0.4f, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            leader.raycastTarget = false;

            var box = grow.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(60f, 16f, 2f);
            grow.gameObject.AddComponent<GEInteractable>();

            var label = root.AddComponent<LabelButton>();
            var so = new SerializedObject(label);
            so.FindProperty("pill").objectReferenceValue = pill;
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("growTarget").objectReferenceValue = grow;
            so.FindProperty("selectedOutline").objectReferenceValue = outline.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "label_button_prefab");
        }
    }
}
