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
    /// Builds the desktop dock — the bottom-right mirror of the world dock that a monitor player reaches
    /// (GDD 8.5, Figma board <b>D - Desktop</b>) — and the tile it repeats.
    ///
    /// <b>Read this before copying anything out of <see cref="UiPrefabBuilder"/>.</b> Those prefabs live on a
    /// world-space canvas scaled 0.001, where <b>one canvas unit is one millimetre</b> and the numbers in
    /// <c>docs/ui/spec.md</c> can be typed in directly. This one is a <b>screen-space overlay</b>: a canvas
    /// unit is a screen pixel at the 1920 x 1080 reference resolution, and the sizes below are ordinary pixel
    /// sizes with no relation to the millimetre figures. A 110 pasted across from the dock builder would be a
    /// 110-pixel tile, not a 110-millimetre one — the two happen to look similar, which is exactly what makes
    /// the mistake easy. The one place the two scales meet is <see cref="Corner"/>, which rescales the shared
    /// nine-slice sprites out of millimetres and into pixels.
    ///
    /// Re-running replaces the prefabs in place, so references from scenes survive.
    /// </summary>
    public static class DesktopDockBuilder
    {
        private const string SpriteFolder = "Assets/ui/figma/";
        private const string OutputFolder = "Assets/prefabs/ui";

        // The design tokens, as colours (docs/ui/spec.md §1).
        private static readonly Color Ink = Color.white;
        private static readonly Color InkSecondary = new Color32(0x9E, 0xB8, 0xC4, 0xFF);
        private static readonly Color Cyan = new Color32(0x6C, 0xCF, 0xDD, 0xFF);
        private static readonly Color Plate = new Color32(0x0E, 0x14, 0x18, 0xCC); // 80%
        private static readonly Color OnAccent = new Color32(0x0E, 0x14, 0x18, 0xFF);

        // Canvas.referencePixelsPerUnit, left at Unity's default. Corner() needs it to undo the sprites'
        // millimetre authoring.
        private const float ReferencePixelsPerUnit = 100f;

        // ---------- the layout, in screen pixels at 1920 x 1080
        private const float TileWidth = 104f;
        private const float TileHeight = 58f;
        private const float TilePitch = 108f;   // 104 wide with a 4 px gap
        private const int TileCount = 7;
        private const float TileFootHeight = 28f;

        private const float RowWidth = (TileCount - 1) * TilePitch + TileWidth; // 752
        private const float Pad = 16f;
        private const float ButtonSize = 28f;
        private const float ButtonGap = 8f;
        private const int ButtonCount = 5;      // passthrough, recenter, mute, help, being
        private const float ButtonsWidth = ButtonCount * ButtonSize + (ButtonCount - 1) * ButtonGap; // 136

        private const float PlateWidth = Pad + RowWidth + Pad + ButtonsWidth + Pad; // 936
        private const float PlateHeight = 82f;
        private const float PlateMargin = 24f;  // clear of the screen edge

        private const float PopupWidth = 264f;
        private const float PopupHeight = 92f;
        private const float PopupOptionWidth = 120f;
        private const float PopupOptionHeight = 44f;

        [MenuItem("Cosmic Simulation/Build Desktop Dock")]
        public static void BuildAll()
        {
            // Building in play mode half-finishes and says almost nothing about it. TMP's outline setter
            // reaches through a CanvasRenderer that Awake has not wired on a freshly created object, the
            // NullReferenceException aborts the run partway down, and every prefab it had not reached yet is
            // left silently at its old contents. Refuse instead.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("DesktopDockBuilder: refusing to build in play mode - it aborts partway and leaves "
                               + "stale prefabs behind. Leave play mode and run this again.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();

            var tile = BuildTile();
            BuildDock(tile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Desktop dock prefabs written to {OutputFolder}");
        }

        // ---------- pieces

        private static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (sprite == null)
            {
                Debug.LogError($"DesktopDockBuilder: missing sprite {name}. Run Cosmic Simulation > Reimport UI Sprites.");
            }

            return sprite;
        }

        // One lookup, in UiPrefabBuilder. This was a second copy of it, and both copies asked for
        // "selawik" when the assets are named "selawk", so the desktop mirror silently used Segoe UI
        // too (CS-122). Delegating means the next change cannot fix one and miss the other.
        private static TMP_FontAsset Font()
        {
            return UiPrefabBuilder.Font();
        }

        /// <summary>A centre-anchored box. Anchors are set explicitly so pixel arithmetic on children adds up.</summary>
        private static RectTransform Rect(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static Image Panel(string name, Transform parent, float w, float h, Sprite sprite, Color colour,
                                   float cornerPixels = 0f)
        {
            var rt = Rect(name, parent, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            Corner(image, cornerPixels);
            return image;
        }

        /// <summary>
        /// Draws a shared rounded-rect sprite with a corner of <paramref name="radiusPixels"/> <b>screen</b> pixels.
        ///
        /// The sprites are authored at eight pixels to the millimetre and imported at 8000 pixels per unit so the
        /// world-space prefabs can use millimetres directly (docs/ui/spec.md §7). Left alone on this canvas the
        /// nine-slice corner would come out a fifth of a pixel across. Unity draws a border of
        /// <c>spriteBorder / ((sprite.pixelsPerUnit / canvas.referencePixelsPerUnit) * pixelsPerUnitMultiplier)</c>
        /// canvas units, so the multiplier is what converts one scale to the other.
        /// </summary>
        private static void Corner(Image image, float radiusPixels)
        {
            var sprite = image.sprite;
            if (sprite == null || radiusPixels <= 0f)
            {
                return;
            }

            var border = sprite.border.x;
            if (border <= 0f)
            {
                return;
            }

            // UiSpriteImporter gives a rounded rect a border of the radius plus two pixels, so the arc itself
            // is this much of the border.
            var wantedBorderPixels = radiusPixels * border / Mathf.Max(1f, border - 2f);
            var spriteToCanvas = sprite.pixelsPerUnit / ReferencePixelsPerUnit;
            image.pixelsPerUnitMultiplier = border / (wantedBorderPixels * spriteToCanvas);
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
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            // No outline here, unlike the world panels: this text sits on a dark plate on a monitor rather than
            // over a lit room, and an outline is a shared-material change that leaks into every other label.
            return tmp;
        }

        /// <summary>
        /// A transparent wash over the whole control that the hover and press tint fade in. It doubles as the
        /// click target, which is why it and not the artwork is the raycast target: <c>DockTile.Bind</c>
        /// disables the thumbnail while a module has no picture yet, and a disabled Graphic takes no raycasts.
        /// </summary>
        private static Image Wash(Transform parent, float w, float h, float cornerPixels)
        {
            var wash = Panel("hover_wash", parent, w, h, Load("ui_rounded_r16"), new Color(1f, 1f, 1f, 0.16f),
                cornerPixels);
            wash.raycastTarget = true;
            wash.transform.SetAsLastSibling();
            return wash;
        }

        private static Button Clickable(GameObject host, Graphic wash)
        {
            var button = host.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = wash;

            // The wash carries its own alpha, so the states are pure alpha and nothing clamps.
            var colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(1f, 1f, 1f, 0.5f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            colours.fadeDuration = 0.09f; // the same 90 ms the world tile takes to lift
            button.colors = colours;

            // Tab belongs to the dock, not to uGUI selection, and a focus ring here would be noise.
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private static GameObject Save(GameObject go, string fileName)
        {
            var path = $"{OutputFolder}/{fileName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        // ---------- the tile

        private static DockTile BuildTile()
        {
            var root = new GameObject("desktop_dock_tile_prefab", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(TileWidth, TileHeight);

            var move = Rect("move", rt, TileWidth, TileHeight);

            // Something to see before CS-031 renders the real pictures, and something for the thumbnail to be
            // drawn over once it does.
            Panel("background", move, TileWidth, TileHeight, Load("ui_rounded_r16"), Plate, 6f);

            var picture = Panel("thumbnail", move, TileWidth, TileHeight, Load("ui_rounded_r16"), Color.white, 6f);

            var foot = Panel("foot", move, TileWidth, TileFootHeight, Load("ui_tile_foot"), Color.white);
            Bottom(foot.rectTransform, 0f);

            var name = Label("name", move, TileWidth - 16f, 17f, "Place", 15f, Ink,
                TextAlignmentOptions.BottomLeft, FontWeight.SemiBold);
            BottomLeft(name.rectTransform, 8f, 12f);

            var second = Label("second_line", move, TileWidth - 16f, 12f, "", 11f, InkSecondary,
                TextAlignmentOptions.BottomLeft, FontWeight.Regular);
            BottomLeft(second.rectTransform, 8f, 2f);

            var underline = Panel("active_underline", move, TileWidth, 2f, null, Cyan);
            Bottom(underline.rectTransform, 0f);
            underline.gameObject.SetActive(false);

            var wash = Wash(move, TileWidth, TileHeight, 6f);
            Clickable(move.gameObject, wash);

            var tile = root.AddComponent<DockTile>();
            var so = new SerializedObject(tile);
            so.FindProperty("thumbnail").objectReferenceValue = picture;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("secondLine").objectReferenceValue = second;
            so.FindProperty("activeUnderline").objectReferenceValue = underline.gameObject;
            so.FindProperty("button").objectReferenceValue = null; // no GEButton: the click comes from uGUI
            so.FindProperty("moveTarget").objectReferenceValue = move;

            // The world tile answers a hover by lifting toward the player and a poke by pushing in. Depth is
            // not a thing an overlay canvas has, and the focus events that drive the lift never reach a
            // screen-space object anyway, so the hover wash does that job here.
            so.FindProperty("hoverLift").floatValue = 0f;
            so.FindProperty("pressDepth").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = Save(root, "desktop_dock_tile_prefab");
            return saved != null ? saved.GetComponent<DockTile>() : null;
        }

        // ---------- the dock

        private static void BuildDock(DockTile tilePrefab)
        {
            var root = new GameObject("desktop_dock_prefab", typeof(RectTransform));

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // over the app's own screen UI, under nothing in particular

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = ReferencePixelsPerUnit;

            root.AddComponent<GraphicRaycaster>();

            var plate = Panel("plate", root.transform, PlateWidth, PlateHeight, Load("ui_rounded_r48"), Plate, 12f);
            plate.raycastTarget = true; // a click on the dock is a click on the dock, not on the sky behind it
            plate.rectTransform.anchorMin = new Vector2(1f, 0f);
            plate.rectTransform.anchorMax = new Vector2(1f, 0f);
            plate.rectTransform.pivot = new Vector2(1f, 0f);
            plate.rectTransform.anchoredPosition = new Vector2(-PlateMargin, PlateMargin);

            var half = PlateWidth * 0.5f;

            var row = Rect("tile_row", plate.rectTransform, RowWidth, TileHeight);
            row.anchoredPosition = new Vector2(-half + Pad + RowWidth * 0.5f, 0f);

            // Right of the tiles, in the order the GDD lists them.
            var firstButtonX = -half + Pad + RowWidth + Pad + ButtonSize * 0.5f;

            var passthrough = IconButton("passthrough_button", plate.rectTransform, Load("icon_passthrough"),
                out var passthroughGlyph);
            ((RectTransform)passthrough.transform).anchoredPosition = new Vector2(firstButtonX, 0f);

            var recenter = IconButton("recenter_button", plate.rectTransform, Load("icon_recenter"), out _);
            ((RectTransform)recenter.transform).anchoredPosition =
                new Vector2(firstButtonX + (ButtonSize + ButtonGap), 0f);

            var mute = IconButton("mute_button", plate.rectTransform, Load("icon_mute"), out var muteGlyph);
            ((RectTransform)mute.transform).anchoredPosition =
                new Vector2(firstButtonX + 2f * (ButtonSize + ButtonGap), 0f);

            var help = IconButton("help_button", plate.rectTransform, Load("icon_help"), out _);
            ((RectTransform)help.transform).anchoredPosition =
                new Vector2(firstButtonX + 3f * (ButtonSize + ButtonGap), 0f);

            var being = IconButton("being_button", plate.rectTransform, Load("icon_being"), out _);
            ((RectTransform)being.transform).anchoredPosition =
                new Vector2(firstButtonX + 4f * (ButtonSize + ButtonGap), 0f);

            // Built last so it draws over the tiles. DesktopDock re-anchors it above whichever tile opened it,
            // which is why it is a sibling of the tile row rather than a child of a tile.
            var popup = BuildPopup(plate.rectTransform);

            var dock = root.AddComponent<DesktopDock>();
            var so = new SerializedObject(dock);
            so.FindProperty("plate").objectReferenceValue = plate.rectTransform;
            so.FindProperty("tilePrefab").objectReferenceValue = tilePrefab;
            so.FindProperty("tileRow").objectReferenceValue = row;
            so.FindProperty("popup").objectReferenceValue = popup;
            so.FindProperty("passthroughButton").objectReferenceValue = passthrough;
            so.FindProperty("recenterButton").objectReferenceValue = recenter;
            so.FindProperty("muteButton").objectReferenceValue = mute;
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("beingButton").objectReferenceValue = being;
            so.FindProperty("passthroughGlyph").objectReferenceValue = passthroughGlyph;
            so.FindProperty("muteGlyph").objectReferenceValue = muteGlyph;
            so.FindProperty("tilePitch").floatValue = TilePitch;
            so.ApplyModifiedPropertiesWithoutUndo();

            Save(root, "desktop_dock_prefab");
        }

        private static Button IconButton(string name, Transform parent, Sprite icon, out Image glyph)
        {
            var back = Panel(name, parent, ButtonSize, ButtonSize, Load("ui_rounded_r16"), Plate, 6f);
            glyph = Panel("glyph", back.rectTransform, ButtonSize - 12f, ButtonSize - 12f, icon, Ink);

            var wash = Wash(back.rectTransform, ButtonSize, ButtonSize, 6f);
            return Clickable(back.gameObject, wash);
        }

        // ---------- the layout pop-up
        //
        // The same DockPopup the world dock uses, so the option labels, the cyan fill on the one in use and
        // the close button all behave identically. Its options are GEButtons, which a monitor cannot press —
        // DesktopDock gives each one's uGUI Button a listener at runtime.

        private static DockPopup BuildPopup(Transform parent)
        {
            var rt = Rect("dock_popup", parent, PopupWidth, PopupHeight);

            // Parked above the plate, which is roughly where it opens, so the prefab looks like itself in the
            // editor. It has to stay *active* in the prefab: DockPopup hides itself at the end of its own
            // Awake, and an object saved inactive never runs Awake, so its option buttons would come up
            // unhooked and Open() would switch it straight back off.
            rt.anchoredPosition = new Vector2(0f, (PlateHeight + PopupHeight) * 0.5f + 10f);

            var plate = rt.gameObject.AddComponent<Image>();
            plate.sprite = Load("ui_rounded_r48");
            plate.color = Plate;
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = true;
            Corner(plate, 10f);

            var popup = rt.gameObject.AddComponent<DockPopup>();
            var so = new SerializedObject(popup);
            var buttons = so.FindProperty("optionButtons");
            var labels = so.FindProperty("optionLabels");
            var fills = so.FindProperty("optionFills");
            buttons.arraySize = 2;
            labels.arraySize = 2;
            fills.arraySize = 2;

            for (var i = 0; i < 2; i++)
            {
                var fill = Panel($"option_{i}", rt, PopupOptionWidth, PopupOptionHeight, Load("ui_rounded_r32"),
                    i == 0 ? Cyan : Plate, 8f);
                fill.rectTransform.anchoredPosition = new Vector2(i == 0 ? -64f : 64f, -6f);

                var text = Label("label", fill.rectTransform, PopupOptionWidth - 12f, 18f,
                    i == 0 ? "Schematic" : "Realistic", 15f, i == 0 ? OnAccent : Ink,
                    TextAlignmentOptions.Center, FontWeight.Medium);
                text.rectTransform.anchoredPosition = Vector2.zero;

                var wash = Wash(fill.rectTransform, PopupOptionWidth, PopupOptionHeight, 8f);
                Clickable(fill.gameObject, wash);
                var geButton = fill.gameObject.AddComponent<GEButton>();

                buttons.GetArrayElementAtIndex(i).objectReferenceValue = geButton;
                labels.GetArrayElementAtIndex(i).objectReferenceValue = text;
                fills.GetArrayElementAtIndex(i).objectReferenceValue = fill;
            }

            var close = Panel("close_button", rt, 20f, 20f, Load("ui_rounded_r16"), Plate, 5f);
            close.rectTransform.anchoredPosition = new Vector2(PopupWidth * 0.5f - 18f, PopupHeight * 0.5f - 14f);
            Panel("glyph", close.rectTransform, 12f, 12f, Load("icon_close"), Ink);
            var closeWash = Wash(close.rectTransform, 20f, 20f, 5f);
            Clickable(close.gameObject, closeWash);
            so.FindProperty("closeButton").objectReferenceValue = close.gameObject.AddComponent<GEButton>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return popup;
        }

        // ---------- anchoring helpers

        private static void Bottom(RectTransform rt, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static void BottomLeft(RectTransform rt, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
        }
    }
}
