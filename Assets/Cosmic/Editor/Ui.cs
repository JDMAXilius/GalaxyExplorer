using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace Cosmic.Editor
{
    public static class Ui
    {
        public const string Folder = "Assets/Cosmic/Prefabs/ui";
        public const string ThemePath = "Assets/Cosmic/Data/Generated/theme.asset";
        const string Sprites = "Assets/ui/figma/";
        const string Thumbs = "Assets/ui/thumbnails/";
        const string FontPath = "Assets/Fonts/selawk SDF.asset";
        const string Places = "Assets/Cosmic/Data/Generated/places";
        static readonly string[] Order = { "cosmic_web", "galaxies", "milky_way", "andromeda", "solar_system", "solar_system_planets", "sagittarius_a" };
        static readonly string[] SecondLine = { null, null, null, null, "Orbital view", "Detail view", "Black hole" };

        [MenuItem("Cosmic/Build/UI")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Cosmic UI: refused in play mode."); return; }
            var theme = Theme();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Cosmic/Prefabs", "ui");
            Content.Missing.Clear();
            var made = 0;
            made += Save("dock", root => MakeDock(root, theme)) ? 1 : 0;
            made += Cards.Build(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"Cosmic UI: {made} prefab(s) in {Folder}, theme at {ThemePath}" +
                      (Content.Missing.Count > 0 ? $"; missing: {string.Join(", ", Content.Missing)}" : "."));
        }

        static Theme Theme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<Theme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<Theme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (theme.font == null) Content.Miss(FontPath);
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static void MakeDock(GameObject root, Theme theme)
        {
            var rect = Canvas(root, 834f, 72f);
            var group = root.AddComponent<CanvasGroup>();
            var dock = root.AddComponent<Dock>();
            Image("plate", rect, 834f, 72f, Sprite("ui_rounded_r48"), theme.tagDark).raycastTarget = false;
            var tiles = new List<Object>();
            var places = Content.Load<Place>(Places);
            for (var i = 0; i < Order.Length; i++)
            {
                var place = places.Find(p => p.id == Order[i]);
                var tile = MakeTile(rect, theme, place, Order[i], SecondLine[i], -397f - 16f + 55f + 114f * i);
                tiles.Add(tile);
            }
            var pass = Button("passthrough", rect, 26f, 24f, Sprite("ui_rounded_r8"), Color.white);
            ((RectTransform)pass.transform).anchoredPosition = new Vector2(400f, 0f);
            Image("glyph", (RectTransform)pass.transform, 12f, 12f, Sprite("icon_passthrough"), theme.tagDark).rectTransform.anchoredPosition = new Vector2(0f, 4f);
            Text("word", (RectTransform)pass.transform, 26f, 5f, "Passthrough", 2.6f, theme.tagDark, TextAlignmentOptions.Center).rectTransform.anchoredPosition = new Vector2(0f, -7.5f);
            var under = Rect("under_dock", rect, 834f, 12f);
            under.anchoredPosition = new Vector2(0f, -42f);
            var bar = Image("drag_bar", under, 60f, 1.6f, Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.55f));
            var box = bar.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(60f, 8f, 2f);
            var grab = root.AddComponent<Grabbable>();
            Content.Set(grab, "limits", Grabbable.Limits.Fixed);
            Content.Set(grab, "keepUpright", true);
            Content.SetArray(grab, "m_Colliders", new List<Object> { box });
            var settings = Square("settings", under, 384f, Sprite("icon_mute"), theme);
            var recenter = Square("recenter", under, 396f, Sprite("icon_recenter"), theme);
            var help = Square("help", under, 408f, Sprite("icon_help"), theme);
            var popup = MakePopup(rect, theme);
            var utility = MakeUtility(rect, theme);
            Content.SetArray(dock, "tiles", tiles);
            Content.Set(dock, "theme", theme);
            Content.Set(dock, "popup", popup);
            Content.Set(dock, "utility", utility);
            Content.Set(dock, "passthrough", pass);
            Content.Set(dock, "passthroughFace", pass.GetComponent<Image>());
            Content.Set(dock, "recenter", recenter);
            Content.Set(dock, "help", help);
            Content.Set(dock, "settings", settings);
            Content.Set(dock, "bar", grab);
            group.alpha = 1f;
        }

        static Tile MakeTile(RectTransform parent, Theme theme, Place place, string id, string second, float x)
        {
            var rect = Rect($"tile_{id}", parent, theme.dockTileMm.x, theme.dockTileMm.y);
            rect.anchoredPosition = new Vector2(x, 0f);
            var move = Rect("move", rect, theme.dockTileMm.x, theme.dockTileMm.y);
            var picture = Image("picture", move, theme.dockTileMm.x, theme.dockTileMm.y, Thumb(id), Color.white);
            Image("foot", move, theme.dockTileMm.x, 22f, Sprite("ui_tile_foot"), Color.white).rectTransform.anchoredPosition = new Vector2(0f, -20f);
            var title = Text("title", move, theme.dockTileMm.x - 10f, 8f, place != null ? place.title : id, 5f, theme.ink, TextAlignmentOptions.BottomLeft);
            title.rectTransform.anchoredPosition = new Vector2(0f, second != null ? -20f : -23f);
            var line = Text("second", move, theme.dockTileMm.x - 10f, 5f, second ?? string.Empty, 3.2f, theme.inkSecondary, TextAlignmentOptions.BottomLeft);
            line.rectTransform.anchoredPosition = new Vector2(0f, -27f);
            line.gameObject.SetActive(second != null);
            var underline = Image("active", move, theme.dockTileMm.x, 1.5f, null, theme.accentCyan);
            underline.rectTransform.anchoredPosition = new Vector2(0f, -30.25f);
            underline.enabled = false;
            var tile = rect.gameObject.AddComponent<Tile>();
            tile.place = place;
            tile.move = move;
            tile.picture = picture;
            tile.title = title;
            tile.second = line;
            tile.underline = underline;
            return tile;
        }

        static Popup MakePopup(RectTransform parent, Theme theme)
        {
            var rect = Rect("popup", parent, 240f, 90f);
            rect.anchoredPosition = new Vector2(0f, 200f);
            rect.gameObject.AddComponent<CanvasGroup>();
            Image("plate", rect, 240f, 90f, Sprite("ui_rounded_r32"), theme.tagDark).raycastTarget = false;
            var popup = rect.gameObject.AddComponent<Popup>();
            var buttons = new List<Object>();
            var fills = new List<Object>();
            var labels = new List<Object>();
            for (var i = 0; i < 2; i++)
            {
                var button = Button($"option_{i}", rect, 108f, 66f, Sprite("ui_rounded_r16"), theme.tagDark);
                ((RectTransform)button.transform).anchoredPosition = new Vector2(i == 0 ? -58f : 58f, -2f);
                buttons.Add(button);
                fills.Add(button.GetComponent<Image>());
                labels.Add(Text("label", (RectTransform)button.transform, 100f, 20f, "Option", 6f, theme.ink, TextAlignmentOptions.Center));
            }
            Content.Set(popup, "theme", theme);
            Content.SetArray(popup, "buttons", buttons);
            Content.SetArray(popup, "fills", fills);
            Content.SetArray(popup, "labels", labels);
            return popup;
        }

        static Utility MakeUtility(RectTransform parent, Theme theme)
        {
            var rect = Rect("utility", parent, 120f, 102f);
            rect.anchoredPosition = new Vector2(482f, -15f);
            rect.gameObject.AddComponent<CanvasGroup>();
            Image("plate", rect, 120f, 102f, Sprite("ui_rounded_r32"), theme.tagDark).raycastTarget = false;
            var utility = rect.gameObject.AddComponent<Utility>();
            const float top = 51f;
            Text("title", rect, 110f, 8f, "Settings", 5f, theme.ink, TextAlignmentOptions.Left).rectTransform.anchoredPosition = new Vector2(0f, top - 9f);
            var close = Button("close", rect, 9f, 9f, Sprite("ui_rounded_r8"), theme.tagDark);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(50.5f, top - 9.5f);
            Image("glyph", (RectTransform)close.transform, 5f, 5f, Sprite("icon_close"), theme.ink);
            Text("scale_label", rect, 110f, 6f, "Scale", 4f, theme.inkSecondary, TextAlignmentOptions.Left).rectTransform.anchoredPosition = new Vector2(0f, top - 19f);
            var scaleValue = Text("scale_value", rect, 110f, 6f, "x1.0", 4f, theme.ink, TextAlignmentOptions.Right);
            scaleValue.rectTransform.anchoredPosition = new Vector2(0f, top - 19f);
            var slider = MakeSlider(rect, theme, top - 26f);
            var mute = MakeToggle("mute", rect, theme, "Mute", top - 39f);
            var voice = MakeToggle("voice", rect, theme, "Narration only", top - 51f);
            var text = Button("text_size", rect, 110f, 10f, Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.12f));
            ((RectTransform)text.transform).anchoredPosition = new Vector2(0f, top - 66f);
            Text("label", (RectTransform)text.transform, 100f, 8f, "Text size", 4f, theme.ink, TextAlignmentOptions.Left);
            var textValue = Text("value", (RectTransform)text.transform, 100f, 8f, "x1.00", 4f, theme.accentCyan, TextAlignmentOptions.Right);
            var about = Button("about", rect, 110f, 10f, Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.12f));
            ((RectTransform)about.transform).anchoredPosition = new Vector2(0f, top - 80f);
            Text("label", (RectTransform)about.transform, 100f, 8f, "About", 4f, theme.ink, TextAlignmentOptions.Left);
            Content.Set(utility, "theme", theme);
            Content.Set(utility, "scale", slider);
            Content.Set(utility, "scaleValue", scaleValue);
            Content.Set(utility, "mute", mute);
            Content.Set(utility, "voice", voice);
            Content.Set(utility, "textSize", text);
            Content.Set(utility, "textSizeValue", textValue);
            Content.Set(utility, "about", about);
            Content.Set(utility, "close", close);
            return utility;
        }

        static Slider MakeSlider(RectTransform parent, Theme theme, float y)
        {
            var rect = Rect("scale", parent, 110f, 12f);
            rect.anchoredPosition = new Vector2(0f, y);
            var track = Image("track", rect, 110f, 2f, Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.25f));
            var fillArea = Rect("fill_area", rect, 110f, 2f);
            var fill = Image("fill", fillArea, 110f, 2f, Sprite("ui_rounded_r8"), theme.accentCyan);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            var handleArea = Rect("handle_area", rect, 102f, 12f);
            var handle = Image("handle", handleArea, 8f, 8f, Sprite("ui_rounded_r8"), theme.ink);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            track.raycastTarget = true;
            return slider;
        }

        static Toggle MakeToggle(string name, RectTransform parent, Theme theme, string label, float y)
        {
            var rect = Rect(name, parent, 110f, 10f);
            rect.anchoredPosition = new Vector2(0f, y);
            var box = Image("box", rect, 8f, 8f, Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.25f));
            box.rectTransform.anchoredPosition = new Vector2(-51f, 0f);
            var check = Image("check", rect, 8f, 8f, Sprite("ui_rounded_r8"), theme.accentCyan);
            check.rectTransform.anchoredPosition = new Vector2(-51f, 0f);
            Text("label", rect, 90f, 8f, label, 4f, theme.ink, TextAlignmentOptions.Left).rectTransform.anchoredPosition = new Vector2(8f, 0f);
            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        static Button Square(string name, RectTransform parent, float x, Sprite glyph, Theme theme)
        {
            var button = Button(name, parent, 9f, 6f, Sprite("ui_rounded_r8"), theme.tagDark);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, 0f);
            Image("glyph", (RectTransform)button.transform, 4f, 4f, glyph, theme.ink);
            return button;
        }

        internal static RectTransform Canvas(GameObject root, float widthMm, float heightMm)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(widthMm, heightMm);
            rect.localScale = Vector3.one * 0.001f;
            return rect;
        }

        internal static RectTransform Rect(string name, RectTransform parent, float widthMm, float heightMm)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(widthMm, heightMm);
            return rect;
        }

        internal static Image Image(string name, RectTransform parent, float widthMm, float heightMm, Sprite sprite, Color color)
        {
            var image = Rect(name, parent, widthMm, heightMm).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 8f;
            return image;
        }

        internal static TMP_Text Text(string name, RectTransform parent, float widthMm, float heightMm, string text, float sizeMm, Color color, TextAlignmentOptions align)
        {
            var label = Rect(name, parent, widthMm, heightMm).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = sizeMm;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        internal static Button Button(string name, RectTransform parent, float widthMm, float heightMm, Sprite sprite, Color color)
        {
            var image = Image(name, parent, widthMm, heightMm, sprite, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        internal static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}{name}.png");
            if (sprite == null) Content.Miss($"{Sprites}{name}.png");
            return sprite;
        }

        static Sprite Thumb(string id)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Thumbs}{id}.png");
            if (sprite == null) Content.Miss($"{Thumbs}{id}.png");
            return sprite;
        }

        internal static bool Save(string name, System.Action<GameObject> fill)
        {
            return Content.Write(name, $"{Folder}/{name}.prefab", root => { fill(root); return true; }, out _);
        }
    }
}
