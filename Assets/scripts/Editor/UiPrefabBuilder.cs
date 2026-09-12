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

            // Before the dock: the dock prefab holds a reference to it, so it has to exist as an asset first.
            var utility = BuildUtilityWindow();
            BuildDock(tile, utility);
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

        /// <summary>A sprite that may not have been drawn yet. The caller decides what to do about it.</summary>
        private static Sprite LoadOptional(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
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

        private static void BuildDock(GameObject tilePrefab, GameObject utilityWindowPrefab)
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

            var barGrab = bar.gameObject.AddComponent<ManipulationHandler>();
            var barGrabSo = new SerializedObject(barGrab);

            // The bar moves the *dock*, not itself (GDD 8.1, contract row F-06). Authored here rather than
            // patched at run time, because the prefab is the deployment story for this file. Left unset,
            // HostTransform falls back to the bar's own transform on first use and a working grab would peel
            // the 60 mm bar off the plate it hangs under (CS-108).
            barGrabSo.FindProperty("hostTransform").objectReferenceValue = root.transform;

            // One hand only. The default is OneAndTwoHanded with MoveRotateScale, and both halves of that are
            // wrong here: the dock is a fixed-size instrument, GDD 8.1 gives it no scale and no free rotation,
            // and Recenter writes position and rotation only — a dock a two-handed pinch had scaled would have
            // no way back to its authored size. One hand moves it; DockController re-tilts it toward the player
            // while it moves, so the wrist never rolls the plate over.
            barGrabSo.FindProperty("manipulationType").enumValueIndex =
                (int)ManipulationHandler.HandMovementType.OneHandedOnly;
            // Unreachable while the line above says OneHandedOnly — the handler drops the second pointer before
            // it ever consults this — but set anyway, so that changing the mode later cannot silently hand the
            // dock a scale gesture.
            barGrabSo.FindProperty("twoHandedManipulationType").enumValueIndex =
                (int)ManipulationHandler.TwoHandedManipulation.MoveRotate;
            // Grabbed directly, with no ForceSolver to sound the grab and release for it — the case the field's
            // own tooltip names.
            barGrabSo.FindProperty("playGrabSounds").boolValue = true;
            barGrabSo.ApplyModifiedPropertiesWithoutUndo();

            // Without this the handler above is unreachable: a select is routed to the nearest enabled
            // IGEPointerHandler up the hierarchy, ManipulationHandler deliberately is not one, and there is no
            // ForceSolver here to forward it (CS-106). Ending the pointer walk at the bar costs nothing — the
            // only handler anywhere above it in this prefab is none at all, and the bar carries no GEButton.
            bar.gameObject.AddComponent<ManipulationPointerRouter>();

            var recenter = SquareButton("recenter_button", underRow, 9f, 6f, Load("icon_recenter"), Plate, Ink);
            ((RectTransform)recenter.transform).anchoredPosition = new Vector2(396f, 0f);

            var help = SquareButton("help_button", underRow, 9f, 6f, Load("icon_help"), Plate, Ink);
            ((RectTransform)help.transform).anchoredPosition = new Vector2(408f, 0f);

            // The third small button, left of Recenter. GDD 8.1 lists only Recenter and Help under the dock but
            // also says mute lives in the utility window, and GDD 11 puts three more settings in there — so
            // something has to open it, and nothing did. No settings glyph was exported by CS-017 (five rounded
            // rects, the tile foot and six icons), so the mute icon stands in: it is the control players will be
            // looking for in there, and the window is where GDD 8.1 says it lives. Drop icon_settings.png into
            // Assets/ui/figma/ and re-run this menu item to replace it.
            var settingsGlyph = LoadOptional("icon_settings");
            if (settingsGlyph == null)
            {
                settingsGlyph = Load("icon_mute");
                Debug.Log("UiPrefabBuilder: no icon_settings sprite, so the dock's settings button wears the " +
                          "mute glyph. Add Assets/ui/figma/icon_settings.png and re-run to replace it.");
            }

            var utility = SquareButton("utility_button", underRow, 9f, 6f, settingsGlyph, Plate, Ink);
            ((RectTransform)utility.transform).anchoredPosition = new Vector2(384f, 0f);

            var dock = root.AddComponent<DockController>();
            var so = new SerializedObject(dock);
            so.FindProperty("tilePrefab").objectReferenceValue = tilePrefab != null ? tilePrefab.GetComponent<DockTile>() : null;
            so.FindProperty("tileRow").objectReferenceValue = row;
            so.FindProperty("passthroughButton").objectReferenceValue = passthrough;
            so.FindProperty("recenterButton").objectReferenceValue = recenter;
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("utilityButton").objectReferenceValue = utility;
            so.FindProperty("utilityWindowPrefab").objectReferenceValue =
                utilityWindowPrefab != null ? utilityWindowPrefab.GetComponent<UtilityWindow>() : null;
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

        // ---------- the utility window
        //
        // GDD 8.2 sizes this at 120 x 50 mm for a scale slider and a close box. GDD 11 then asks for mute,
        // narration-only mute and a text size in the same window, which does not fit in 50 mm of height, so it
        // is 120 x 102 — the width the design gives it, and as much height as four controls need on the 2 mm
        // grid with the 5 mm padding from docs/ui/spec.md §3. Every number below is a millimetre, because the
        // canvas Root() builds is scaled 0.001.

        private static GameObject BuildUtilityWindow()
        {
            const float width = 120f;
            const float height = 102f;
            const float row = 110f; // content width: the plate less 5 mm of padding on each side
            const float top = height * 0.5f;

            var root = Root("utility_window_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(width, height);

            Panel("plate", rt, width, height, Load("ui_rounded_r48"), Plate).raycastTarget = false;

            var title = Label("title", rt, 100f, 8f, "Settings", 5.6f, Ink, TextAlignmentOptions.Left, FontWeight.SemiBold);
            title.rectTransform.anchoredPosition = new Vector2(-5f, top - 9f);

            var close = SquareButton("close_button", rt, 9f, 6f, Load("icon_close"), Plate, Ink);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(width * 0.5f - 9.5f, top - 9.5f);

            // --- the scale rail

            var scaleLabel = Label("scale_label", rt, row, 6f, "SCALE", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            scaleLabel.characterSpacing = 8f;
            scaleLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 19f);

            var scaleValue = Label("scale_value", rt, row, 6f, "1.00x", 4.55f, Ink, TextAlignmentOptions.Right, FontWeight.Regular);
            scaleValue.rectTransform.anchoredPosition = new Vector2(0f, top - 19f);

            var track = Panel("scale_track", rt, row, 2f, Load("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.25f));
            track.rectTransform.anchoredPosition = new Vector2(0f, top - 26f);

            var fill = Panel("scale_fill", track.rectTransform, row * 0.5f, 2f, Load("ui_rounded_r8"), Cyan);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;

            // An 8 mm square at 4 mm radius is a circle.
            var knob = Panel("scale_knob", track.rectTransform, 8f, 8f, Load("ui_rounded_r32"), Ink);
            knob.raycastTarget = false;
            knob.rectTransform.anchoredPosition = Vector2.zero;

            // The hit area is 12 mm tall over a 2 mm rail: a fingertip or a hand ray cannot be asked to find
            // two millimetres, and the collider is what both of them actually hit.
            var trackBox = track.gameObject.AddComponent<BoxCollider>();
            trackBox.size = new Vector3(row, 12f, 2f);
            var trackInteractable = track.gameObject.AddComponent<GEInteractable>();

            // --- sound

            var soundLabel = Label("sound_label", rt, row, 6f, "SOUND", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            soundLabel.characterSpacing = 8f;
            soundLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 39f);

            var mute = PlateRow("mute_button", rt, row, 12f, 0f, top - 50f, "Sound on", out var muteFill, out var muteLabel);
            var narration = PlateRow("narration_button", rt, row, 12f, 0f, top - 64f, "Narration on",
                out var narrationFill, out var narrationLabel);

            // --- text size

            var textLabel = Label("text_size_label", rt, row, 6f, "TEXT SIZE", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            textLabel.characterSpacing = 8f;
            textLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 77f);

            var window = root.AddComponent<UtilityWindow>();
            var so = new SerializedObject(window);
            var sizeButtons = so.FindProperty("textSizeButtons");
            var sizeFills = so.FindProperty("textSizeFills");
            var sizeLabels = so.FindProperty("textSizeLabels");
            sizeButtons.arraySize = 3;
            sizeFills.arraySize = 3;
            sizeLabels.arraySize = 3;

            var captions = new[] { "1.0x", "1.25x", "1.5x" };
            for (var i = 0; i < 3; i++)
            {
                var button = PlateRow($"text_size_{i}", rt, 34f, 14f, (i - 1) * 37f, top - 89f, captions[i],
                    out var sizeFill, out var sizeLabel);

                sizeButtons.GetArrayElementAtIndex(i).objectReferenceValue = button;
                sizeFills.GetArrayElementAtIndex(i).objectReferenceValue = sizeFill;
                sizeLabels.GetArrayElementAtIndex(i).objectReferenceValue = sizeLabel;
            }

            so.FindProperty("scaleTrack").objectReferenceValue = trackInteractable;
            so.FindProperty("scaleFill").objectReferenceValue = fill.rectTransform;
            so.FindProperty("scaleKnob").objectReferenceValue = knob.rectTransform;
            so.FindProperty("scaleValue").objectReferenceValue = scaleValue;
            so.FindProperty("muteButton").objectReferenceValue = mute;
            so.FindProperty("muteFill").objectReferenceValue = muteFill;
            so.FindProperty("muteLabel").objectReferenceValue = muteLabel;
            so.FindProperty("narrationButton").objectReferenceValue = narration;
            so.FindProperty("narrationFill").objectReferenceValue = narrationFill;
            so.FindProperty("narrationLabel").objectReferenceValue = narrationLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "utility_window_prefab");
        }

        /// <summary>A full-width pressable plate with a label on it, as the pop-up's options are.</summary>
        private static GEButton PlateRow(string name, Transform parent, float w, float h, float x, float y,
                                         string text, out Image fill, out TMP_Text label)
        {
            fill = Panel(name, parent, w, h, Load("ui_rounded_r32"), Plate);
            fill.rectTransform.anchoredPosition = new Vector2(x, y);

            label = Label("label", fill.rectTransform, w - 6f, h - 2f, text, 5f, Ink, TextAlignmentOptions.Center, FontWeight.Medium);
            label.rectTransform.anchoredPosition = Vector2.zero;

            var box = fill.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, 2f);
            fill.gameObject.AddComponent<GEInteractable>();
            return fill.gameObject.AddComponent<GEButton>();
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
