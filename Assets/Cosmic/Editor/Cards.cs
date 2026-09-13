using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cosmic.Editor
{
    internal static class Cards
    {
        const string Onboarding = "Assets/Textures/onboarding_sprites/";

        internal static int Build(Theme theme)
        {
            var made = 0;
            made += Ui.Save("panel_body", root => MakePanel(root, theme, Panel.Variant.Body)) ? 1 : 0;
            made += Ui.Save("panel_scene", root => MakePanel(root, theme, Panel.Variant.Scene)) ? 1 : 0;
            made += Ui.Save("panel_moon", root => MakePanel(root, theme, Panel.Variant.Moon)) ? 1 : 0;
            made += Ui.Save("label_card", root => MakeLabel(root, theme, Label.Style.Card)) ? 1 : 0;
            made += Ui.Save("label_name", root => MakeLabel(root, theme, Label.Style.Name)) ? 1 : 0;
            made += Ui.Save("label_moon", root => MakeLabel(root, theme, Label.Style.Moon)) ? 1 : 0;
            made += Ui.Save("toast", root => MakeToast(root, theme)) ? 1 : 0;
            made += Ui.Save("about", root => MakeAbout(root, theme)) ? 1 : 0;
            return made;
        }

        static void MakePanel(GameObject root, Theme theme, Panel.Variant variant)
        {
            var width = variant == Panel.Variant.Moon ? 120f : theme.panelWidthMm;
            var rect = Ui.Canvas(root, width, variant == Panel.Variant.Moon ? 60f : 176f);
            root.AddComponent<CanvasGroup>();
            var panel = root.AddComponent<Panel>();
            var y = 0f;
            var title = Top(Ui.Text("title", rect, width, 20f, "Title", theme.titleMm, theme.ink, TextAlignmentOptions.TopLeft), y);
            y += variant == Panel.Variant.Moon ? 16f : 22f;
            var subtitle = Top(Ui.Text("subtitle", rect, width, 8f, "SUBTITLE", theme.subtitleMm, theme.accentCyan, TextAlignmentOptions.TopLeft), y);
            subtitle.characterSpacing = 8f;
            if (variant != Panel.Variant.Body) subtitle.gameObject.SetActive(false);
            else y += 12f;
            var labels = new List<Object>();
            var values = new List<Object>();
            TMP_Text paragraph;
            if (variant == Panel.Variant.Moon)
            {
                Stat(rect, theme, labels, values, 0, 0f, y, width);
                y += 20f;
                paragraph = Top(Ui.Text("paragraph", rect, width, 24f, string.Empty, theme.bodyMm * 0.8f, theme.ink, TextAlignmentOptions.TopLeft), y);
            }
            else
            {
                paragraph = Top(Ui.Text("paragraph", rect, width, variant == Panel.Variant.Scene ? 110f : 62f, string.Empty, theme.bodyMm, theme.ink, TextAlignmentOptions.TopLeft), y);
                y += variant == Panel.Variant.Scene ? 114f : 68f;
                if (variant == Panel.Variant.Body)
                {
                    Top(Ui.Image("divider", rect, width, 0.25f, null, new Color(theme.accentCyan.r, theme.accentCyan.g, theme.accentCyan.b, 0.35f)), y).raycastTarget = false;
                    y += 8f;
                    for (var i = 0; i < 4; i++) Stat(rect, theme, labels, values, i, i % 2 == 0 ? 0f : 83f, y + (i < 2 ? 0f : 22f), 78f);
                    y += 46f;
                }
            }
            var instruction = Top(Ui.Text("instruction", rect, width, 20f, string.Empty, theme.bodyMm, theme.inkSecondary, TextAlignmentOptions.TopLeft), y);
            Content.Set(panel, "variant", variant);
            Content.Set(panel, "theme", theme);
            Content.Set(panel, "title", title);
            Content.Set(panel, "subtitle", subtitle);
            Content.Set(panel, "paragraph", paragraph);
            Content.Set(panel, "instruction", instruction);
            Content.SetArray(panel, "statLabels", labels);
            Content.SetArray(panel, "statValues", values);
        }

        static void Stat(RectTransform parent, Theme theme, List<Object> labels, List<Object> values, int index, float x, float y, float width)
        {
            var cell = Ui.Rect($"stat_{index}", parent, width, 20f);
            cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0f, 1f);
            cell.anchoredPosition = new Vector2(x, -y);
            var label = Top(Ui.Text("label", cell, width, 6f, "LABEL", theme.labelMm, theme.inkSecondary, TextAlignmentOptions.TopLeft), 0f);
            label.characterSpacing = 8f;
            var value = Top(Ui.Text("value", cell, width, 12f, "0", theme.statMm, theme.ink, TextAlignmentOptions.TopLeft), 7f);
            value.richText = true;
            labels.Add(label);
            values.Add(value);
        }

        static T Top<T>(T graphic, float y) where T : Graphic
        {
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -y);
            return graphic;
        }

        static void MakeLabel(GameObject root, Theme theme, Label.Style style)
        {
            var card = style == Label.Style.Card;
            var rect = Ui.Canvas(root, card ? theme.tagSizeMm.x : 80f, card ? theme.tagSizeMm.y : 12f);
            var label = root.AddComponent<Label>();
            Content.Set(label, "style", style);
            Content.Set(label, "theme", theme);
            if (!card)
            {
                var text = Ui.Text("name", rect, 80f, 12f, "Name", style == Label.Style.Moon ? theme.moonSmallMm : theme.labelMm, theme.ink, TextAlignmentOptions.Center);
                Content.Set(label, "nameText", text);
                return;
            }
            var lift = 30f;
            var leader = Ui.Image("leader", rect, 1.2f, lift, null, new Color(1f, 1f, 1f, 0.7f));
            leader.rectTransform.pivot = new Vector2(0.5f, 1f);
            leader.rectTransform.anchoredPosition = new Vector2(0f, -theme.tagSizeMm.y * 0.5f);
            leader.raycastTarget = false;
            var grow = Ui.Rect("grow", rect, theme.tagSizeMm.x, theme.tagSizeMm.y);
            var outline = Ui.Image("outline", grow, theme.tagSizeMm.x + 2f, theme.tagSizeMm.y + 2f, Ui.Sprite("ui_rounded_r16"), theme.accentCyan);
            outline.raycastTarget = false;
            outline.gameObject.SetActive(false);
            var plate = Ui.Image("plate", grow, theme.tagSizeMm.x, theme.tagSizeMm.y, Ui.Sprite("ui_rounded_r16"), theme.tagDark);
            var name = Ui.Text("name", plate.rectTransform, theme.tagSizeMm.x, 8f, "Name", theme.tagMm, theme.ink, TextAlignmentOptions.Center);
            name.rectTransform.anchoredPosition = new Vector2(0f, 4.5f);
            var second = Ui.Text("second", plate.rectTransform, theme.tagSizeMm.x, 5f, "SECOND", theme.tagMm * 0.72f, theme.inkSecondary, TextAlignmentOptions.Center);
            second.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            second.characterSpacing = 6f;
            Content.Set(label, "plate", plate);
            Content.Set(label, "nameText", name);
            Content.Set(label, "secondText", second);
            Content.Set(label, "leader", leader);
            Content.Set(label, "outline", outline.gameObject);
            Content.Set(label, "grow", grow);
            Content.Set(label, "liftMm", lift);
        }

        static void MakeToast(GameObject root, Theme theme)
        {
            var rect = Ui.Canvas(root, 200f, 120f);
            var group = root.AddComponent<CanvasGroup>();
            var toast = root.AddComponent<Toast>();
            var art = Ui.Image("art", rect, 120f, 80f, null, Color.white);
            art.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            art.preserveAspect = true;
            art.raycastTarget = false;
            var message = Ui.Text("message", rect, 190f, 30f, string.Empty, 8f, theme.ink, TextAlignmentOptions.Center);
            message.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            Ui.Image("hit", rect, 200f, 120f, null, new Color(0f, 0f, 0f, 0f));
            Content.Set(toast, "theme", theme);
            Content.Set(toast, "group", group);
            Content.Set(toast, "message", message);
            Content.Set(toast, "art", art);
            var serialized = new SerializedObject(toast);
            var hints = serialized.FindProperty("hints");
            var lines = new[] { "Reach out, put your finger and thumb together, and pull a planet toward you.", "Pinch with both hands and move them apart to make what you are holding bigger." };
            var frames = new[] { "onboarding_pull_sprite", "onboarding_hold_sprite" };
            hints.arraySize = 2;
            for (var i = 0; i < 2; i++)
            {
                var hint = hints.GetArrayElementAtIndex(i);
                hint.FindPropertyRelative("line").stringValue = lines[i];
                hint.FindPropertyRelative("loopSeconds").floatValue = 3f;
                hint.FindPropertyRelative("holdSeconds").floatValue = 6f;
                var art0 = hint.FindPropertyRelative("frames");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Onboarding}{frames[i]}.png");
                if (sprite == null) Content.Miss($"{Onboarding}{frames[i]}.png");
                art0.arraySize = sprite != null ? 1 : 0;
                if (sprite != null) art0.GetArrayElementAtIndex(0).objectReferenceValue = sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MakeAbout(GameObject root, Theme theme)
        {
            var rect = Ui.Canvas(root, 240f, 200f);
            root.AddComponent<CanvasGroup>();
            var about = root.AddComponent<About>();
            Ui.Image("plate", rect, 240f, 200f, Ui.Sprite("ui_rounded_r32"), theme.tagDark).raycastTarget = false;
            Top(Ui.Text("title", rect, 230f, 12f, "About", theme.subtitleMm * 1.5f, theme.ink, TextAlignmentOptions.TopLeft), 6f).rectTransform.anchoredPosition = new Vector2(5f, -6f);
            var close = Ui.Button("close", rect, 9f, 9f, Ui.Sprite("ui_rounded_r8"), theme.tagDark);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(110.5f, 90.5f);
            Ui.Image("glyph", (RectTransform)close.transform, 5f, 5f, Ui.Sprite("icon_close"), theme.ink);
            var credits = Top(Ui.Text("credits", rect, 230f, 50f, About.Attribution, 4f, theme.ink, TextAlignmentOptions.TopLeft), 22f);
            credits.rectTransform.anchoredPosition = new Vector2(5f, -22f);
            var view = Ui.Rect("view", rect, 230f, 100f);
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0f, 1f);
            view.anchoredPosition = new Vector2(5f, -76f);
            view.gameObject.AddComponent<RectMask2D>();
            var licence = Top(Ui.Text("licence", view, 226f, 600f, string.Empty, 3f, theme.inkSecondary, TextAlignmentOptions.TopLeft), 0f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = licence.rectTransform;
            scroll.horizontal = false;
            scroll.viewport = view;
            var version = Top(Ui.Text("version", rect, 230f, 8f, "version", 3.5f, theme.inkSecondary, TextAlignmentOptions.BottomLeft), 186f);
            version.rectTransform.anchoredPosition = new Vector2(5f, -186f);
            Content.Set(about, "theme", theme);
            Content.Set(about, "credits", credits);
            Content.Set(about, "licence", licence);
            Content.Set(about, "version", version);
            Content.Set(about, "close", close);
        }
    }
}
