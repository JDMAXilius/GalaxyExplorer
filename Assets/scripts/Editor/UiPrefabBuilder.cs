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

        // Meta's minimum direct-touch target and the spacing between two of them (CS-278), in canvas
        // millimetres. Every pressable the builder makes is at least TargetMm across the face.
        private const float TargetMm = 24f;
        private const float GapMm = 12f;
        private const float GlyphMm = 14f;

        // How far a press pushes a button's face in, in canvas millimetres - the same 4 mm DockTile uses, so the
        // dock's tiles and its small buttons give a fingertip the same answer.
        private const float PressDepthMm = 4f;

        [MenuItem("Cosmic Simulation/Build UI Prefabs")]
        public static void BuildAll()
        {
            // Building in play mode half-finishes and says almost nothing about it. TMP's outline setter
            // reaches through a CanvasRenderer that Awake has not wired on a freshly created object, the
            // NullReferenceException aborts the run partway down, and every prefab it had not reached yet is
            // left silently at its old contents. Refuse instead.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("UiPrefabBuilder: refusing to build in play mode - it aborts partway and leaves "
                               + "stale prefabs behind. Leave play mode and run this again.");
                return;
            }

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

        /// <summary>The project's UI font. Shared with DesktopDockBuilder: this used to be duplicated
        /// in both builders, which is how CS-122's wrong spelling survived in two places at once.</summary>
        internal static TMP_FontAsset Font()
        {
            // Selawik, deliberately: it is the OFL-licensed, metric-compatible stand-in for Segoe UI,
            // and Segoe UI is proprietary Microsoft and must not ship. The font files are named
            // "selawk", not "selawik" (selawk, selawkb, selawkl, selawksb, selawksl), and the old
            // spelling here matched nothing, fell through to the first TMP_FontAsset in the project
            // and silently gave every UI prefab segoeui SDF. See CS-122.
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset selawk");
            if (guids.Length == 0)
            {
                Debug.LogError("UiPrefabBuilder: no Selawik TMP_FontAsset found in the project. " +
                               "Expected Assets/Fonts/selawk SDF.asset. Refusing to fall back to " +
                               "another family - that is how segoeui SDF got into every prefab (CS-122).");
                return null;
            }

            // Regular, not whichever of the five faces the search happens to return first.
            TMP_FontAsset fallback = null;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    continue;
                }

                if (System.IO.Path.GetFileNameWithoutExtension(path) == "selawk SDF")
                {
                    return font;
                }

                fallback = fallback ?? font;
            }

            if (fallback != null)
            {
                Debug.LogWarning("UiPrefabBuilder: 'selawk SDF' (regular) not found; using " +
                                 fallback.name + " instead.");
            }

            return fallback;
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
            // No GEPressVisual here: DockTile moves "move" itself, for the hover lift and the press alike.
            var box = move.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(110f, 62f, 2f);
            var interactable = move.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(move.gameObject, box, interactable);
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

            // Under the plate: the grab handle in the middle and three controls either side of it, every one at
            // Meta's 24 mm fingertip minimum with 12 mm between neighbours (CS-278), symmetric about the bar.
            var underRow = Rect("under_dock", rt, 834f, TargetMm);
            underRow.anchorMin = new Vector2(0.5f, 0f);
            underRow.anchorMax = new Vector2(0.5f, 0f);
            underRow.pivot = new Vector2(0.5f, 1f);
            underRow.anchoredPosition = new Vector2(0f, -6f);

            // The handle is a plate-coloured pill the size of its own hit area with the old 60 mm line drawn on
            // it as the grip mark: the art grew to the target rather than a collider hanging 11 mm past a hairline.
            const float barWidth = 120f;
            var bar = Panel("drag_bar", underRow, barWidth, TargetMm, Load("ui_rounded_r16"), Plate);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            Panel("grip", bar.rectTransform, 60f, 1.6f, Load("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.55f)).raycastTarget = false;
            var barBox = bar.gameObject.AddComponent<BoxCollider>();
            barBox.size = new Vector3(barWidth, TargetMm, 2f);
            // No poke filter: this is a handle for a pinch, not a button, and a filter would let any finger that
            // brushes it drag the dock away.
            bar.gameObject.AddComponent<GEInteractable>();

            var barGrab = bar.gameObject.AddComponent<ManipulationHandler>();
            var barGrabSo = new SerializedObject(barGrab);

            // The bar moves the *dock*, not itself (GDD 8.1, contract row F-06). Authored here rather than
            // patched at run time, because the prefab is the deployment story for this file. Left unset,
            // HostTransform falls back to the bar's own transform on first use and a working grab would peel
            // the bar off the plate it hangs under (CS-108).
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

            // Three a side, 12 mm from the bar's end and from each other: 84, 120 and 156 mm out from centre.
            float Slot(int i) => barWidth * 0.5f + GapMm + TargetMm * 0.5f + i * (TargetMm + GapMm);

            // The settings button. GDD 8.1 lists only Recenter and Help under the dock but
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

            // Reset and About (CS-277) have no glyph exported yet, so they wear their names until icon_reset.png
            // and icon_about.png land in Assets/ui/figma/ - the arrangement the settings button had.
            var being = SquareButton("being_button", underRow, TargetMm, GlyphMm, Load("icon_being"), Plate, Ink);
            ((RectTransform)being.transform).anchoredPosition = new Vector2(-Slot(2), 0f);

            var utility = SquareButton("utility_button", underRow, TargetMm, GlyphMm, settingsGlyph, Plate, Ink);
            ((RectTransform)utility.transform).anchoredPosition = new Vector2(-Slot(1), 0f);

            var reset = SquareButton("reset_button", underRow, TargetMm, GlyphMm, LoadOptional("icon_reset"), Plate, Ink, "RESET");
            ((RectTransform)reset.transform).anchoredPosition = new Vector2(-Slot(0), 0f);

            var recenter = SquareButton("recenter_button", underRow, TargetMm, GlyphMm, Load("icon_recenter"), Plate, Ink);
            ((RectTransform)recenter.transform).anchoredPosition = new Vector2(Slot(0), 0f);

            var help = SquareButton("help_button", underRow, TargetMm, GlyphMm, Load("icon_help"), Plate, Ink);
            ((RectTransform)help.transform).anchoredPosition = new Vector2(Slot(1), 0f);

            var about = SquareButton("about_button", underRow, TargetMm, GlyphMm, LoadOptional("icon_about"), Plate, Ink, "ABOUT");
            ((RectTransform)about.transform).anchoredPosition = new Vector2(Slot(2), 0f);

            var dock = root.AddComponent<DockController>();
            var so = new SerializedObject(dock);
            so.FindProperty("tilePrefab").objectReferenceValue = tilePrefab != null ? tilePrefab.GetComponent<DockTile>() : null;
            so.FindProperty("tileRow").objectReferenceValue = row;
            so.FindProperty("passthroughButton").objectReferenceValue = passthrough;
            so.FindProperty("recenterButton").objectReferenceValue = recenter;
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("utilityButton").objectReferenceValue = utility;
            so.FindProperty("beingButton").objectReferenceValue = being;
            // Fields CS-277 adds to DockController; wired by name so this builder compiles and runs whether or
            // not that change has landed, and says so if it has not.
            Assign(so, "resetButton", reset);
            Assign(so, "aboutButton", about);
            so.FindProperty("beingPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Being/being.prefab")?.GetComponent<Cosmic.Companion.Being>();
            so.FindProperty("utilityWindowPrefab").objectReferenceValue =
                utilityWindowPrefab != null ? utilityWindowPrefab.GetComponent<UtilityWindow>() : null;
            so.FindProperty("dragBar").objectReferenceValue = bar.transform;
            so.FindProperty("tilePitch").floatValue = 114f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "dock_prefab");
        }

        /// <summary>
        /// A square icon button. The collider and button sit on the root; the face is a "move" child so the
        /// press can push it in. With no icon and a fallback given, the face carries the word instead.
        /// </summary>
        private static GEButton SquareButton(string name, Transform parent, float size, float glyph,
                                             Sprite icon, Color fill, Color glyphColour, string fallbackText = null)
        {
            var root = Rect(name, parent, size, size);
            var back = Panel("move", root, size, size, Load("ui_rounded_r16"), fill);
            if (icon != null || fallbackText == null)
            {
                Panel("glyph", back.rectTransform, glyph, glyph, icon, glyphColour).raycastTarget = false;
            }
            else
            {
                Label("glyph", back.rectTransform, size - 2f, size - 2f, fallbackText, glyph * 0.3f, glyphColour,
                      TextAlignmentOptions.Center, FontWeight.SemiBold);
            }

            return Pressable(root.gameObject, size, size, back.gameObject);
        }

        // ---------- the utility window
        //
        // GDD 8.2 sizes this at 120 x 50 mm for a scale slider and a close box. GDD 11 then asks for mute,
        // narration-only mute and a text size in the same window, which does not fit in 50 mm of height, so it
        // was 120 x 102. The owner then added the microphone and Quit (16 Sep, mockup CS-196), so it is
        // 120 x 130: mute and narration share a row to make room, and the microphone row and Quit sit below the
        // text size. CS-278 then grew every control to Meta's 24 mm fingertip minimum with 12 mm between
        // neighbouring targets, which makes it 120 x 222. Every number below is a millimetre, because the canvas
        // Root() builds is scaled 0.001.

        private static GameObject BuildUtilityWindow()
        {
            const float width = 120f;
            const float height = 222f;
            const float row = 110f; // content width: the plate less 5 mm of padding on each side
            const float top = height * 0.5f;

            var root = Root("utility_window_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(width, height);

            Panel("plate", rt, width, height, Load("ui_rounded_r48"), Plate).raycastTarget = false;

            var title = Label("title", rt, 80f, 8f, "Settings", 5.6f, Ink, TextAlignmentOptions.Left, FontWeight.SemiBold);
            title.rectTransform.anchoredPosition = new Vector2(-15f, top - 16f);

            var close = SquareButton("close_button", rt, TargetMm, GlyphMm, Load("icon_close"), Plate, Ink);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(width * 0.5f - 4f - TargetMm * 0.5f, top - 4f - TargetMm * 0.5f);

            // --- the scale rail

            var scaleLabel = Label("scale_label", rt, row, 6f, "SCALE", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            scaleLabel.characterSpacing = 8f;
            scaleLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 40f);

            var scaleValue = Label("scale_value", rt, row, 6f, "1.00x", 4.55f, Ink, TextAlignmentOptions.Right, FontWeight.Regular);
            scaleValue.rectTransform.anchoredPosition = new Vector2(0f, top - 40f);

            var track = Panel("scale_track", rt, row, 2f, Load("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.25f));
            track.rectTransform.anchoredPosition = new Vector2(0f, top - 52f);

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

            // The hit area is 24 mm tall over a 2 mm rail: a fingertip or a hand ray cannot be asked to find
            // two millimetres, and the collider is what both of them actually hit. Not a GEButton - the window
            // reads the drag itself - but a fingertip still needs the filter before XRI lets it take hold.
            var trackBox = track.gameObject.AddComponent<BoxCollider>();
            trackBox.size = new Vector3(row, TargetMm, 2f);
            var trackInteractable = track.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(track.gameObject, trackBox, trackInteractable);

            // --- sound

            var soundLabel = Label("sound_label", rt, row, 6f, "SOUND", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            soundLabel.characterSpacing = 8f;
            soundLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 70f);

            const float half = (row - GapMm) * 0.5f;
            var mute = PlateRow("mute_button", rt, half, TargetMm, -(half + GapMm) * 0.5f, top - 88f, "Sound on",
                out var muteFill, out var muteLabel);
            var narration = PlateRow("narration_button", rt, half, TargetMm, (half + GapMm) * 0.5f, top - 88f, "Narration on",
                out var narrationFill, out var narrationLabel);

            // --- text size

            var textLabel = Label("text_size_label", rt, row, 6f, "TEXT SIZE", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            textLabel.characterSpacing = 8f;
            textLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 106f);

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
                // Three across 110 mm with 12 mm between them: 28 wide on a 41 mm pitch.
                var button = PlateRow($"text_size_{i}", rt, 28f, TargetMm, (i - 1) * 41f, top - 124f, captions[i],
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

            // --- microphone

            var micLabel = Label("mic_label", rt, row, 6f, "MICROPHONE", 4.55f, InkSecondary, TextAlignmentOptions.Left, FontWeight.SemiBold);
            micLabel.characterSpacing = 8f;
            micLabel.rectTransform.anchoredPosition = new Vector2(0f, top - 142f);

            var previous = PlateRow("mic_previous", rt, TargetMm, TargetMm, -(row - TargetMm) * 0.5f, top - 160f, "<", out _, out _);
            var next = PlateRow("mic_next", rt, TargetMm, TargetMm, (row - TargetMm) * 0.5f, top - 160f, ">", out _, out _);

            // What is left between the two arrows with 3 mm of air to each: a readout, not a target.
            const float nameWidth = row - 2f * TargetMm - 6f;
            var nameField = Panel("mic_name", rt, nameWidth, TargetMm, Load("ui_rounded_r32"), new Color(1f, 1f, 1f, 0.04f));
            nameField.raycastTarget = false;
            nameField.rectTransform.anchoredPosition = new Vector2(0f, top - 160f);
            var deviceName = Label("label", nameField.rectTransform, nameWidth - 4f, 10f, "System default", 3.8f, Ink,
                TextAlignmentOptions.Center, FontWeight.Regular);
            deviceName.overflowMode = TextOverflowModes.Ellipsis;
            deviceName.rectTransform.anchoredPosition = Vector2.zero;

            var levelTrack = Panel("mic_track", rt, row, 2f, Load("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.12f));
            levelTrack.raycastTarget = false;
            levelTrack.rectTransform.anchoredPosition = new Vector2(0f, top - 177f);
            var level = Panel("mic_level", levelTrack.rectTransform, 0f, 2f, Load("ui_rounded_r8"), Cyan);
            level.raycastTarget = false;
            level.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            level.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            level.rectTransform.pivot = new Vector2(0f, 0.5f);
            level.rectTransform.anchoredPosition = Vector2.zero;

            var hint = Label("mic_hint", rt, row, 5f, "Speak to check it. The bar moves when the mic hears you.", 3.6f,
                InkSecondary, TextAlignmentOptions.Left, FontWeight.Regular);
            hint.rectTransform.anchoredPosition = new Vector2(0f, top - 183f);

            var microphone = root.AddComponent<MicrophoneRow>();
            var mic = new SerializedObject(microphone);
            mic.FindProperty("previousButton").objectReferenceValue = previous;
            mic.FindProperty("nextButton").objectReferenceValue = next;
            mic.FindProperty("deviceLabel").objectReferenceValue = deviceName;
            mic.FindProperty("levelFill").objectReferenceValue = level.rectTransform;
            mic.FindProperty("hintLabel").objectReferenceValue = hint;
            mic.FindProperty("levelWidthMm").floatValue = row;
            mic.ApplyModifiedPropertiesWithoutUndo();

            // --- quit

            var quit = PlateRow("quit_button", rt, row, TargetMm, 0f, top - 202f, "Quit Cosmic Simulation",
                out var quitFill, out var quitLabel);
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("quitFill").objectReferenceValue = quitFill;
            so.FindProperty("quitLabel").objectReferenceValue = quitLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "utility_window_prefab");
        }

        /// <summary>A full-width pressable plate with a label on it, as the pop-up's options are. The collider
        /// and button sit on the root; <paramref name="fill"/> is the "move" face a press pushes in.</summary>
        private static GEButton PlateRow(string name, Transform parent, float w, float h, float x, float y,
                                         string text, out Image fill, out TMP_Text label)
        {
            var root = Rect(name, parent, w, h);
            root.anchoredPosition = new Vector2(x, y);

            fill = Panel("move", root, w, h, Load("ui_rounded_r32"), Plate);
            label = Label("label", fill.rectTransform, w - 6f, h - 2f, text, 5f, Ink, TextAlignmentOptions.Center, FontWeight.Medium);
            label.rectTransform.anchoredPosition = Vector2.zero;

            return Pressable(root.gameObject, w, h, fill.gameObject);
        }

        /// <summary>
        /// What every pressable here shares: a collider the size of its face, the interactable that turns XRI
        /// hover and select into the app's events, the poke filter a fingertip needs before XRI will select it
        /// (CS-276), the press travel, and the button. Collider first: GEInteractable keeps only the colliders
        /// already on its own GameObject.
        /// </summary>
        private static GEButton Pressable(GameObject go, float w, float h, GameObject moving)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, 2f);
            var interactable = go.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(go, box, interactable);

            if (moving != null)
            {
                // GEPressVisual's depth is in the parent's local units, which on this canvas is millimetres; its
                // tooltip says metres because the legacy buttons it was written for sit at metre scale.
                var press = go.AddComponent<GEPressVisual>();
                var so = new SerializedObject(press);
                so.FindProperty("movingButtonVisuals").objectReferenceValue = moving;
                so.FindProperty("pressDepth").floatValue = PressDepthMm;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return go.AddComponent<GEButton>();
        }

        /// <summary>Sets a serialized reference by name, and says so when the field does not exist yet.</summary>
        private static void Assign(SerializedObject so, string property, Object value)
        {
            var found = so.FindProperty(property);
            if (found == null)
            {
                Debug.LogWarning($"UiPrefabBuilder: {so.targetObject.GetType().Name} has no '{property}' field; " +
                                 "left unwired. Re-run Build UI Prefabs once it exists.");
                return;
            }

            found.objectReferenceValue = value;
        }

        // ---------- the layout pop-up

        private static void BuildDockPopup()
        {
            // Four slots, two to a row. Two of them is the layout pair this panel has always shown and the
            // plate keeps its old 90 mm; four is the galaxy list, and DockPopup.Fit grows the plate to 142 mm
            // for the second row. Slots the module does not need are hidden, not destroyed.
            const int Slots = 4;
            const float RowHeight = 52f;
            const float Padding = 38f;
            var tallest = RowHeight * (Slots / 2) + Padding;

            var root = Root("dock_popup_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(240f, tallest);

            var plate = Panel("plate", rt, 240f, tallest, Load("ui_rounded_r48"), Plate);
            plate.raycastTarget = false;

            var popup = root.AddComponent<DockPopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("plate").objectReferenceValue = plate.rectTransform;
            var buttons = so.FindProperty("optionButtons");
            var labels = so.FindProperty("optionLabels");
            var fills = so.FindProperty("optionFills");
            buttons.arraySize = Slots;
            labels.arraySize = Slots;
            fills.arraySize = Slots;

            var seed = new[] { "Schematic", "Realistic", "Option 3", "Option 4" };
            for (var i = 0; i < Slots; i++)
            {
                // The slot is the collider and the button (DockPopup hides unused slots through it); its
                // "move" child is the face a press pushes in.
                var slot = Rect($"option_{i}", rt, 108f, 44f);
                var column = i % 2 == 0 ? -58f : 58f;
                var row = -6f - RowHeight * (i / 2);
                slot.anchoredPosition = new Vector2(column, row);

                var fill = Panel("move", slot, 108f, 44f, Load("ui_rounded_r32"), i == 0 ? Cyan : Plate);
                var text = Label("label", fill.rectTransform, 100f, 12f, seed[i],
                    5f, i == 0 ? OnAccent : Ink, TextAlignmentOptions.Center, FontWeight.Medium);
                text.rectTransform.anchoredPosition = Vector2.zero;

                var button = Pressable(slot.gameObject, 108f, 44f, fill.gameObject);

                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
                labels.GetArrayElementAtIndex(i).objectReferenceValue = text;
                fills.GetArrayElementAtIndex(i).objectReferenceValue = fill;
            }

            // Pinned to the plate's top-right corner so DockPopup.Fit, which re-heights the plate for two or four
            // slots, keeps it in the corner; a centre-relative offset set for the tall case sat outside the short
            // one. With one row the 24 mm box clears the first option by a millimetre.
            var close = SquareButton("close_button", rt, TargetMm, GlyphMm, Load("icon_close"), Plate, Ink);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-4f - TargetMm * 0.5f, -4f - TargetMm * 0.5f);
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

        /// <summary>
        /// A destination tag: a dark card carrying a name and, under it, a small line of caps saying what kind
        /// of thing it is.
        ///
        /// <b>Two lines because the player asked for the one card they already liked, everywhere.</b> The
        /// Milky Way map inherited two treatments: the original app's large markers, of which "Solar System"
        /// and "Galactic Center" survived, and our own single-line pills on everything else. The large ones
        /// read better - a name alone tells you what a place is called, and the line under it tells you what
        /// you are about to go and look at - so this is now the one treatment, and the two legacy markers are
        /// retired by <c>LegacyPoiCleanup</c> rather than left drawing alongside.
        ///
        /// <b>The card is 24 mm tall, not 16.</b> Two lines of type at 5 mm and 3.2 mm with the 4 mm of
        /// breathing room GDD 8.4 asks for do not fit in the old 16 mm plate; a card that keeps the old height
        /// would either clip the subtitle or squash both. <c>DestinationTagBuilder</c> fits the width to
        /// whichever of the two lines is longer.
        ///
        /// A tag whose module has no second line hides the subtitle and centres the name on its own, which is
        /// what keeps this usable for the body labels in the orbit model as well as for the map.
        /// </summary>
        /// <summary>Where the name sits, in canvas units above the label's centre.</summary>
        private const float LabelNameYMm = 4.5f;

        /// <summary>Where the second line sits, in canvas units below the label's centre.</summary>
        private const float LabelSecondLineYMm = -5f;

        private static void BuildLabelButton()
        {
            const float w = 60f;
            const float h = 24f;

            var root = Root("label_button_prefab", true);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(w, h);

            var grow = Rect("grow", rt, w, h);
            var pill = Panel("pill", grow, w, h, Load("ui_rounded_r32"), Plate);

            // The name a little above centre, the subtitle a little below. Not by the same amount: the name's
            // box is 8 mm and the subtitle's is 5, so +4.5 and -5 put 3 mm of air between their facing edges and
            // leave 3.5 mm above the name and 4.5 mm below the subtitle. The slight bottom weighting is
            // deliberate - the leader line leaves the card's bottom edge, and a subtitle sitting too close to
            // where the hairline starts reads as attached to it.
            //
            // The two offsets used to live in DestinationTagBuilder, which rewrote the name's Y on every Milky
            // Way tag it fitted. That builder is gone (CS-170: the map is the original markers again), so the
            // label prefab owns them now; GalaxyPins is the one remaining user of the label.
            var text = Label("name", grow, w - 10f, 8f, "Crab Nebula", 5f, Ink,
                             TextAlignmentOptions.Center, FontWeight.SemiBold);
            text.rectTransform.anchoredPosition = new Vector2(0f, LabelNameYMm);

            // Caps with a little tracking, which is what makes a subtitle read as a category rather than as a
            // second, quieter name. TMP takes character spacing in ems of the current size.
            // 3.6 and not a hair smaller: 3.6 mm is the point below which a label 1.2 m away stops being
            // readable, and a subtitle set under that number would contradict the label's own floor.
            var second = Label("second", grow, w - 10f, 5f, "SUPERNOVA REMNANT", 3.6f, InkSecondary,
                               TextAlignmentOptions.Center, FontWeight.Medium);
            second.rectTransform.anchoredPosition = new Vector2(0f, LabelSecondLineYMm);
            second.characterSpacing = 6f;

            var outline = Panel("selected_outline", grow, w + 4f, h + 4f, Load("ui_rounded_r32"), Cyan);
            outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            outline.transform.SetAsFirstSibling();

            var leader = Panel("leader", rt, 0.4f, 0.4f, null, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            leader.raycastTarget = false;

            var box = grow.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, 2f);
            var interactable = grow.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(grow.gameObject, box, interactable);

            var label = root.AddComponent<LabelButton>();
            var so = new SerializedObject(label);
            so.FindProperty("pill").objectReferenceValue = pill;
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("secondLine").objectReferenceValue = second;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("growTarget").objectReferenceValue = grow;
            so.FindProperty("selectedOutline").objectReferenceValue = outline.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "label_button_prefab");
        }
    }
}
