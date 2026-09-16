using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cosmic.Editor
{
    public static class UtilityUi
    {
        const float WidthMm = 120f;
        const float HeightMm = 118f;
        const float ContentMm = 110f;
        const float RowMm = 12f;
        const float GapMm = 3f;
        const float HalfMm = (ContentMm - GapMm) * 0.5f;
        static readonly Color Faint = new Color(1f, 1f, 1f, 0.12f);

        public static Utility Make(RectTransform parent, Theme theme, float x, float topMm)
        {
            var rect = Ui.Rect("utility", parent, WidthMm, HeightMm);
            rect.anchoredPosition = new Vector2(x, topMm - HeightMm * 0.5f);
            rect.gameObject.AddComponent<CanvasGroup>();
            Ui.Image("plate", rect, WidthMm, HeightMm, Ui.Sprite("ui_rounded_r32"), theme.tagDark).raycastTarget = false;
            var utility = rect.gameObject.AddComponent<Utility>();
            var top = HeightMm * 0.5f;
            Ui.Text("title", rect, ContentMm, 8f, "Settings", 5f, theme.ink, TextAlignmentOptions.Left).rectTransform.anchoredPosition = new Vector2(0f, top - 9f);
            var close = Ui.Button("close", rect, 9f, 9f, Ui.Sprite("ui_rounded_r8"), theme.tagDark);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(50.5f, top - 9.5f);
            Ui.Image("glyph", (RectTransform)close.transform, 5f, 5f, Ui.Sprite("icon_close"), theme.ink);
            Heading(rect, theme, "scale_label", "Scale", top - 19f);
            var scaleValue = Ui.Text("scale_value", rect, ContentMm, 6f, "x1.0", 4f, theme.ink, TextAlignmentOptions.Right);
            scaleValue.rectTransform.anchoredPosition = new Vector2(0f, top - 19f);
            var slider = Slider(rect, theme, top - 26f);
            Heading(rect, theme, "sound_label", "Sound", top - 35f);
            var mute = Toggle("mute", rect, theme, "Mute all", -HalfMm * 0.5f - GapMm * 0.5f, top - 43f, out var muteLabel);
            var voice = Toggle("voice", rect, theme, "Mute narration", HalfMm * 0.5f + GapMm * 0.5f, top - 43f, out var voiceLabel);
            Heading(rect, theme, "text_label", "Text size", top - 54f);
            var sizes = new List<Object>();
            var sizeLabels = new List<Object>();
            var third = (ContentMm - GapMm * 2f) / 3f;
            for (var i = 0; i < Prefs.TextScales.Length; i++)
            {
                var button = Ui.Button($"text_{i}", rect, third, RowMm, Ui.Sprite("ui_rounded_r8"), Faint);
                ((RectTransform)button.transform).anchoredPosition = new Vector2((i - 1) * (third + GapMm), top - 62f);
                sizes.Add(button);
                sizeLabels.Add(Ui.Text("label", (RectTransform)button.transform, third, 8f, $"x{Prefs.TextScales[i]:0.0#}", 4f, theme.ink, TextAlignmentOptions.Center));
            }
            Heading(rect, theme, "mic_label", "Microphone", top - 73f);
            var mic = Mic(rect, theme, top);
            var about = Ui.Button("about", rect, HalfMm, RowMm, Ui.Sprite("ui_rounded_r8"), Faint);
            ((RectTransform)about.transform).anchoredPosition = new Vector2(-HalfMm * 0.5f - GapMm * 0.5f, top - 106f);
            Ui.Text("label", (RectTransform)about.transform, HalfMm, 8f, "About", 4f, theme.ink, TextAlignmentOptions.Center);
            var quit = Ui.Button("quit", rect, HalfMm, RowMm, Ui.Sprite("ui_rounded_r8"), Faint);
            ((RectTransform)quit.transform).anchoredPosition = new Vector2(HalfMm * 0.5f + GapMm * 0.5f, top - 106f);
            var quitLabel = Ui.Text("label", (RectTransform)quit.transform, HalfMm, 8f, "Quit", 4f, theme.inkSecondary, TextAlignmentOptions.Center);
            Content.Set(utility, "theme", theme);
            Content.Set(utility, "scale", slider);
            Content.Set(utility, "scaleValue", scaleValue);
            Content.Set(utility, "mute", mute);
            Content.Set(utility, "voice", voice);
            Content.SetArray(utility, "toggleLabels", new List<Object> { muteLabel, voiceLabel });
            Content.SetArray(utility, "textSizes", sizes);
            Content.SetArray(utility, "textSizeLabels", sizeLabels);
            Content.Set(utility, "mic", mic);
            Content.Set(utility, "about", about);
            Content.Set(utility, "quit", quit);
            Content.Set(utility, "quitLabel", quitLabel);
            Content.Set(utility, "close", close);
            return utility;
        }

        static MicPicker Mic(RectTransform rect, Theme theme, float top)
        {
            var picker = rect.gameObject.AddComponent<MicPicker>();
            var previous = Arrow("mic_previous", rect, theme, "<", -ContentMm * 0.5f + RowMm * 0.5f, top - 82f);
            var next = Arrow("mic_next", rect, theme, ">", ContentMm * 0.5f - RowMm * 0.5f, top - 82f);
            var nameWidth = ContentMm - RowMm * 2f - 4f;
            var field = Ui.Image("mic_name", rect, nameWidth, RowMm, Ui.Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.04f));
            field.raycastTarget = false;
            field.rectTransform.anchoredPosition = new Vector2(0f, top - 82f);
            var name = Ui.Text("label", field.rectTransform, nameWidth - 4f, 10f, "System default", 3.6f, theme.ink, TextAlignmentOptions.Center);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var track = Ui.Image("mic_track", rect, ContentMm, 2f, Ui.Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.12f));
            track.raycastTarget = false;
            track.rectTransform.anchoredPosition = new Vector2(0f, top - 90f);
            var level = Ui.Image("level", track.rectTransform, 0f, 2f, Ui.Sprite("ui_rounded_r8"), theme.accentCyan);
            level.raycastTarget = false;
            level.rectTransform.anchorMin = level.rectTransform.anchorMax = level.rectTransform.pivot = new Vector2(0f, 0.5f);
            level.rectTransform.anchoredPosition = Vector2.zero;
            var hint = Ui.Text("mic_hint", rect, ContentMm, 5f, "Speak to check it.", 3f, theme.inkSecondary, TextAlignmentOptions.Left);
            hint.rectTransform.anchoredPosition = new Vector2(0f, top - 95f);
            Content.Set(picker, "previous", previous);
            Content.Set(picker, "next", next);
            Content.Set(picker, "deviceName", name);
            Content.Set(picker, "meter", level.rectTransform);
            Content.Set(picker, "hint", hint);
            Content.Set(picker, "meterWidthMm", ContentMm);
            return picker;
        }

        static Button Arrow(string name, RectTransform parent, Theme theme, string glyph, float x, float y)
        {
            var button = Ui.Button(name, parent, RowMm, RowMm, Ui.Sprite("ui_rounded_r8"), Faint);
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, y);
            Ui.Text("glyph", (RectTransform)button.transform, RowMm, RowMm, glyph, 5f, theme.ink, TextAlignmentOptions.Center);
            return button;
        }

        static void Heading(RectTransform parent, Theme theme, string name, string text, float y)
        {
            Ui.Text(name, parent, ContentMm, 6f, text.ToUpperInvariant(), 3.2f, theme.inkSecondary, TextAlignmentOptions.Left).rectTransform.anchoredPosition = new Vector2(0f, y);
        }

        static Slider Slider(RectTransform parent, Theme theme, float y)
        {
            var rect = Ui.Rect("scale", parent, ContentMm, RowMm);
            rect.anchoredPosition = new Vector2(0f, y);
            var track = Ui.Image("track", rect, ContentMm, 2f, Ui.Sprite("ui_rounded_r8"), new Color(1f, 1f, 1f, 0.25f));
            var fillArea = Ui.Rect("fill_area", rect, ContentMm, 2f);
            var fill = Ui.Image("fill", fillArea, ContentMm, 2f, Ui.Sprite("ui_rounded_r8"), theme.accentCyan);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            // Slider sets the fill and handle anchors itself, so any size here is added on top of the span it gives them.
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Ui.Rect("handle_area", rect, ContentMm - 8f, RowMm);
            var handle = Ui.Image("handle", handleArea, 8f, 8f, Ui.Sprite("ui_rounded_r8"), theme.ink);
            handle.rectTransform.sizeDelta = new Vector2(8f, 8f - RowMm);
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            track.raycastTarget = true;
            return slider;
        }

        static Toggle Toggle(string name, RectTransform parent, Theme theme, string label, float x, float y, out TMP_Text text)
        {
            var face = Ui.Image(name, parent, HalfMm, RowMm, Ui.Sprite("ui_rounded_r8"), Faint);
            face.rectTransform.anchoredPosition = new Vector2(x, y);
            var on = Ui.Image("on", face.rectTransform, HalfMm, RowMm, Ui.Sprite("ui_rounded_r8"), theme.accentCyan);
            on.raycastTarget = false;
            text = Ui.Text("label", face.rectTransform, HalfMm - 4f, 8f, label, 4f, theme.ink, TextAlignmentOptions.Center);
            var toggle = face.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = face;
            toggle.graphic = on;
            toggle.isOn = false;
            return toggle;
        }
    }
}
