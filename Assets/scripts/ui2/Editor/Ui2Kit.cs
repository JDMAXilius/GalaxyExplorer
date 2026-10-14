// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation.UI2;
using GalaxyExplorer.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The parts every UI 2.0 panel is built from: text, panels, buttons, rows, switches, segments. Positions
    /// are pixels of the approved Figma frame, measured from the parent's top-left corner, and the names
    /// follow the Figma components (Controls/Button, Controls/ListRow, Controls/Segmented, Controls/ToggleRow).
    /// </summary>
    internal static class Ui2Kit
    {
        internal static readonly Color Attention = new Color32(0xF2, 0xC6, 0x6D, 0xFF);

        internal static TextMeshProUGUI Text(RectTransform rect, string text, float size, Color colour,
                                             TextAlignmentOptions alignment, FontWeight weight, bool wrap)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = UiPrefabBuilder.Font();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = alignment;
            tmp.fontWeight = weight;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            return tmp;
        }

        internal static TextMeshProUGUI Label(RectTransform parent, string name, float x, float y, float w, float h,
                                              string text, float size, Color colour,
                                              FontWeight weight = FontWeight.Regular, bool wrap = false,
                                              TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            return Text(Ui2Builder.Box(name, parent, x, y, w, h), text, size, colour, alignment, weight, wrap);
        }

        /// <summary>A focused panel: the dark plate and its hairline edge. Figma: the Panel surface.</summary>
        internal static RectTransform Panel(RectTransform parent, string name, float x, float y, float w, float h,
                                            float radius)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(Ui2Builder.Rect("plate", root, w, h), "ui_rounded_r64_fill", radius,
                            Ui2Builder.WithAlpha(UiTheme.Panel, 0.97f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", root, w, h), "ui_rounded_r64_ring", radius,
                            new Color(1f, 1f, 1f, 0.16f));
            return root;
        }

        /// <summary>A whole page inside a panel: same size as the panel, shown one at a time.</summary>
        internal static RectTransform Page(RectTransform panel, string name)
        {
            return Ui2Builder.Box(name, panel, 0f, 0f, panel.sizeDelta.x, panel.sizeDelta.y);
        }

        internal static Image Glyph(RectTransform parent, string sprite, float x, float y, float size, Color colour)
        {
            var image = Ui2Builder.Box("icon", parent, x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? Ui2Builder.Icon(sprite) : null;
            image.preserveAspect = true;
            image.color = colour;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        internal static void Divider(RectTransform parent, float x, float y, float w)
        {
            var line = Ui2Builder.Box("divider", parent, x, y, w, 1f).gameObject.AddComponent<Image>();
            line.color = UiTheme.Divider;
            line.raycastTarget = false;
        }

        /// <summary>
        /// Makes a rect pressable. Collider first: GEInteractable keeps only the colliders already on its own
        /// GameObject.
        /// </summary>
        internal static UiButton Press(RectTransform root, float w, float h, Image wash, Image outline,
                                       TMP_Text label, Image icon, bool keepColours, Image selectedFill = null)
        {
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, 3f);
            var interactable = root.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(root.gameObject, box, interactable);
            root.gameObject.AddComponent<GEButton>();

            var button = root.gameObject.AddComponent<UiButton>();
            var so = new SerializedObject(button);
            so.FindProperty("plate").objectReferenceValue = wash;
            so.FindProperty("outline").objectReferenceValue = outline;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("keepColours").boolValue = keepColours;
            so.FindProperty("selectedFill").objectReferenceValue = selectedFill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>Controls/Button, Primary: cyan with dark text. An icon and its label sit together in the middle.</summary>
        internal static UiButton Primary(RectTransform parent, string name, float x, float y, float w, float h,
                                         string text, string icon, string widest = null)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", root, w, h), "ui_rounded_r64_fill", 18f, UiTheme.Accent);
            var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", root, w, h), "ui_rounded_r64_fill", 18f, Color.clear);
            return Content(root, w, h, wash, text, icon, UiTheme.Deep, widest);
        }

        /// <summary>Controls/Button, Secondary: an outlined pill.</summary>
        internal static UiButton Secondary(RectTransform parent, string name, float x, float y, float w, float h,
                                           string text, string icon, string widest = null)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", root, w, h), "ui_rounded_r64_fill", h * 0.5f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.6f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", root, w, h), "ui_rounded_r64_ring", h * 0.5f,
                            new Color(1f, 1f, 1f, 0.75f));
            var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", root, w, h), "ui_rounded_r64_fill", h * 0.5f, Color.clear);
            return Content(root, w, h, wash, text, icon, UiTheme.Ink, widest);
        }

        // The icon (24) and the label, 12 apart, centred as one block. When the label changes at run time the
        // block is laid out for the widest wording so nothing spills.
        private static UiButton Content(RectTransform root, float w, float h, Image wash, string text, string icon,
                                        Color ink, string widest)
        {
            var probe = Text(Ui2Builder.Rect("label", root, 10f, 24f), widest ?? text, 19f, ink,
                             TextAlignmentOptions.Left, FontWeight.SemiBold, false);
            var textWidth = Mathf.Ceil(probe.GetPreferredValues(widest ?? text).x) + 2f;
            var iconWidth = icon != null ? 24f + 12f : 0f;
            var left = Mathf.Max(8f, (w - iconWidth - textWidth) * 0.5f);

            Image glyph = null;
            if (icon != null)
            {
                glyph = Glyph(root, icon, left, (h - 24f) * 0.5f, 24f, ink);
            }

            var rect = probe.rectTransform;
            rect.sizeDelta = new Vector2(textWidth, 24f);
            rect.anchoredPosition = new Vector2(left + iconWidth + textWidth * 0.5f - w * 0.5f, 0f);
            probe.text = text;
            probe.transform.SetAsLastSibling();
            root.gameObject.AddComponent<CanvasGroup>();
            var button = Press(root, w, h, wash, null, probe, glyph, true);
            if (glyph != null)
            {
                var so = new SerializedObject(button);
                so.FindProperty("tintIcon").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return button;
        }

        /// <summary>Controls/IconButton: a round plate with one glyph (Close, Back, a pager arrow).</summary>
        internal static UiButton IconButton(RectTransform parent, string name, float x, float y, float size,
                                            string icon, float iconSize, bool plate = true, float turn = 0f)
        {
            var root = Ui2Builder.Box(name, parent, x, y, size, size);
            if (plate)
            {
                Ui2Builder.Fill(Ui2Builder.Rect("fill", root, size, size), "ui_rounded_r64_fill", size * 0.5f,
                                Ui2Builder.WithAlpha(UiTheme.Deep, 0.6f));
                Ui2Builder.Fill(Ui2Builder.Rect("edge", root, size, size), "ui_rounded_r64_ring", size * 0.5f,
                                new Color(1f, 1f, 1f, 0.3f));
            }

            var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", root, size, size), "ui_rounded_r64_fill", size * 0.5f,
                                       Color.clear);
            var glyph = Glyph(root, icon, (size - iconSize) * 0.5f, (size - iconSize) * 0.5f, iconSize, UiTheme.Ink);
            glyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, turn);
            return Press(root, size, size, wash, null, null, glyph, true);
        }

        /// <summary>
        /// Controls/ListRow: optional leading icon, a title over a description, an optional value and a chevron.
        /// The whole row is the press target. <paramref name="note"/> is the description line, for rows whose
        /// second line changes at run time.
        /// </summary>
        internal static UiButton Row(RectTransform parent, string name, float x, float y, float w, float h,
                                     string title, string description, string icon, bool chevron,
                                     out TextMeshProUGUI note, out TextMeshProUGUI value, bool inset = true,
                                     bool outlined = false)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            if (outlined)
            {
                Ui2Builder.Fill(Ui2Builder.Rect("edge", root, w, h), "ui_rounded_r64_ring", 14f,
                                new Color(1f, 1f, 1f, 0.3f));
            }

            var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", root, w, h), "ui_rounded_r64_fill", 14f, Color.clear);
            var outline = Ui2Builder.Fill(Ui2Builder.Rect("outline", root, w, h), "ui_rounded_r64_ring", 14f,
                                          UiTheme.Accent);
            outline.enabled = false;

            var textX = icon != null ? 64f : inset ? 14f : 0f;
            if (icon != null)
            {
                Glyph(root, icon, 14f, (h - 32f) * 0.5f, 32f, UiTheme.Ink);
            }

            var top = (h - 51f) * 0.5f;
            var heading = Label(root, "title", textX, top, w - textX - 60f, 27f, title, 22f, UiTheme.Ink);
            note = Label(root, "description", textX, top + 29f, w - textX - 60f, 22f, description, 17f,
                         UiTheme.InkSecondary);
            value = null;
            if (chevron)
            {
                Glyph(root, "v2_icon_chevron", w - 38f, (h - 24f) * 0.5f, 24f, UiTheme.Ink);
                value = Label(root, "value", w - 250f, (h - 22f) * 0.5f, 195f, 22f, string.Empty, 18f, UiTheme.Ink,
                              FontWeight.Regular, false, TextAlignmentOptions.Right);
            }

            var button = Press(root, w, h, wash, outline, heading, null, false);
            return button;
        }

        /// <summary>The picture of a switch, 68 x 38. The row it sits in is what gets pressed.</summary>
        internal static UiSwitch Switch(RectTransform parent, float x, float y)
        {
            var root = Ui2Builder.Box("switch", parent, x, y, 68f, 38f);
            var track = Ui2Builder.Fill(Ui2Builder.Rect("track", root, 68f, 38f), "ui_rounded_r64_fill", 19f,
                                        UiTheme.Accent);
            var knob = Ui2Builder.Rect("knob", root, 30f, 30f);
            var dot = knob.gameObject.AddComponent<Image>();
            dot.sprite = Ui2Builder.Icon("icon_dot");
            dot.raycastTarget = false;
            knob.anchoredPosition = new Vector2(15f, 0f);

            var view = root.gameObject.AddComponent<UiSwitch>();
            var so = new SerializedObject(view);
            so.FindProperty("track").objectReferenceValue = track;
            so.FindProperty("knob").objectReferenceValue = knob;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        /// <summary>Controls/Segmented: one pill, mutually exclusive choices, the chosen one filled cyan.</summary>
        internal static UiButton[] Segmented(RectTransform parent, string name, float x, float y, float[] widths,
                                             string[] labels)
        {
            var total = 10f;
            foreach (var width in widths)
            {
                total += width;
            }

            var root = Ui2Builder.Box(name, parent, x, y, total, 56f);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", root, total, 56f), "ui_rounded_r64_fill", 28f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.88f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", root, total, 56f), "ui_rounded_r64_ring", 28f,
                            new Color(1f, 1f, 1f, 0.3f));

            var buttons = new UiButton[widths.Length];
            var left = 5f;
            for (var i = 0; i < widths.Length; i++)
            {
                var segment = Ui2Builder.Box("segment_" + i, root, left, 5f, widths[i], 46f);
                var chosen = Ui2Builder.Fill(Ui2Builder.Rect("chosen", segment, widths[i], 46f), "ui_rounded_r64_fill",
                                             23f, UiTheme.Accent);
                chosen.enabled = false;
                var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", segment, widths[i], 46f), "ui_rounded_r64_fill", 23f,
                                           Color.clear);
                var label = Text(Ui2Builder.Rect("label", segment, widths[i], 20f), labels[i], 17f, UiTheme.Ink,
                                 TextAlignmentOptions.Center, FontWeight.Medium, false);
                buttons[i] = Press(segment, widths[i], 46f, wash, null, label, null, false, chosen);
                left += widths[i];
            }

            return buttons;
        }

        /// <summary>A rounded picture frame: the mask, and inside it an image that covers without distortion.</summary>
        internal static Image Picture(RectTransform parent, string name, float x, float y, float w, float h,
                                      float radius, Sprite sprite)
        {
            var frame = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(frame, "ui_rounded_r64_fill", radius, UiTheme.Deep);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var image = Cover(frame);
            image.sprite = sprite;
            if (sprite != null)
            {
                image.GetComponent<AspectRatioFitter>().aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            return image;
        }

        /// <summary>An image that fills its parent without distortion; the fitter's ratio is set per sprite.</summary>
        internal static Image Cover(RectTransform parent)
        {
            var rect = Ui2Builder.Rect("image", parent, parent.sizeDelta.x, parent.sizeDelta.y);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            return image;
        }

        internal static void Objects(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        internal static void Strings(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        internal static Sprite Photo(string id)
        {
            // A plain null, not Unity's "missing object", so callers can chain with ??.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ui/photos/" + id + ".png");
            return sprite != null ? sprite : null;
        }
    }
}
