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
    /// The exploring half of the UI 2.0 shell prefab: breadcrumb, top picker and the photo-first information
    /// card. Called by <see cref="Ui2Builder.BuildShell"/>; positions are pixels of the approved Figma frame.
    /// </summary>
    internal static class Ui2ExploreBuilder
    {
        private const string PortraitFolder = "Assets/Textures/galaxies/portraits/";
        private const string ThumbFolder = "Assets/ui/thumbnails/";

        internal static void Build(RectTransform frame, GameObject root)
        {
            // Breadcrumb. Figma: Navigation/Breadcrumb, centred on the frame at y 40.
            var crumb = Text(Ui2Builder.Box("breadcrumb", frame, 343f, 36f, 800f, 36f), string.Empty, 24f,
                             UiTheme.Ink, TextAlignmentOptions.Center, FontWeight.Medium, false);

            // Picker. Figma: four Navigation/DestinationTile at x 455, 608, 750, 893, y 93.
            var picker = Ui2Builder.Box("picker", frame, 440f, 84f, 610f, 136f);
            var ids = new[] { "andromeda", "whirlpool", "pinwheel", "triangulum" };
            var names = new[] { "Andromeda", "Whirlpool", "Pinwheel", "Triangulum" };
            var xs = new[] { 15f, 168f, 310f, 453f };
            var tiles = new UiButton[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                tiles[i] = Tile("tile_" + ids[i], picker, xs[i], 9f, names[i], Photo(ids[i]));
            }

            // Card. Figma: Card/Information at 1083, 118, 384 wide, growing downward with its text.
            var card = Ui2Builder.Rect("card", frame, 384f, 600f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(1083f + 192f - frame.sizeDelta.x * 0.5f, frame.sizeDelta.y * 0.5f - 118f);
            Behind(Ui2Builder.Fill(Ui2Builder.Rect("plate", card, 384f, 600f), "ui_rounded_r64_fill", 20f,
                                   WithAlpha(UiTheme.Panel, 0.97f)));
            Behind(Ui2Builder.Fill(Ui2Builder.Rect("edge", card, 384f, 600f), "ui_rounded_r64_ring", 20f,
                                   new Color(1f, 1f, 1f, 0.16f)));

            var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(20, 20, 32, 30);
            stack.spacing = 17f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var photoRoot = Ui2Builder.Rect("photo", card, 344f, 207f);
            photoRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 207f;
            // The mask is drawn, in the deep surface colour: the galaxy portraits are light on transparent.
            Ui2Builder.Fill(photoRoot, "ui_rounded_r64_fill", 12f, UiTheme.Deep);
            photoRoot.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var photo = Cover(photoRoot);

            var title = Text(Ui2Builder.Rect("title", card, 344f, 42f), "ANDROMEDA", 36f, UiTheme.Ink,
                             TextAlignmentOptions.TopLeft, FontWeight.SemiBold, true);
            var body = Text(Ui2Builder.Rect("description", card, 344f, 81f), "Description", 20f, UiTheme.Ink,
                            TextAlignmentOptions.TopLeft, FontWeight.Regular, true);
            body.lineSpacing = 8f;

            var divider = Ui2Builder.Rect("divider", card, 344f, 1f);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
            var line = divider.gameObject.AddComponent<Image>();
            line.color = UiTheme.Divider;
            line.raycastTarget = false;

            var facts = Ui2Builder.Rect("facts", card, 344f, 78f);
            facts.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
            var factLabels = new TMP_Text[2];
            var factValues = new TMP_Text[2];
            var factUnits = new TMP_Text[2];
            for (var i = 0; i < 2; i++)
            {
                var x = i * 192f;
                factLabels[i] = Text(Ui2Builder.Box("label_" + i, facts, x, 0f, 152f, 22f), "Label", 17f,
                                     UiTheme.InkSecondary, TextAlignmentOptions.TopLeft, FontWeight.Regular, false);
                factValues[i] = Text(Ui2Builder.Box("value_" + i, facts, x, 23f, 152f, 30f), "Value", 24f,
                                     UiTheme.Ink, TextAlignmentOptions.TopLeft, FontWeight.SemiBold, false);
                factUnits[i] = Text(Ui2Builder.Box("unit_" + i, facts, x, 55f, 152f, 22f), "unit", 18f,
                                    UiTheme.Ink, TextAlignmentOptions.TopLeft, FontWeight.Regular, false);
            }

            var split = Ui2Builder.Box("split", facts, 171.5f, 4f, 1f, 70f).gameObject.AddComponent<Image>();
            split.color = UiTheme.Divider;
            split.raycastTarget = false;

            var actions = Ui2Builder.Rect("actions", card, 344f, 60f);
            actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            var listen = Primary("listen", actions, 0f, 0f, 167f, 60f, "Listen");
            Secondary("compare", actions, 177f, 0f, 167f, 60f, "Compare");

            var explore = root.AddComponent<Ui2Explore>();
            var so = new SerializedObject(explore);
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("photoRoot").objectReferenceValue = photoRoot.gameObject;
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("listen").objectReferenceValue = listen;
            so.FindProperty("factsRoot").objectReferenceValue = facts.gameObject;
            Objects(so.FindProperty("factLabels"), factLabels);
            Objects(so.FindProperty("factValues"), factValues);
            Objects(so.FindProperty("factUnits"), factUnits);
            so.FindProperty("picker").objectReferenceValue = picker.gameObject;
            so.FindProperty("crumb").objectReferenceValue = crumb;

            var photoIds = new[] { "andromeda", "whirlpool", "pinwheel", "triangulum" };
            Strings(so.FindProperty("photoIds"), photoIds);
            var photos = so.FindProperty("photos");
            photos.arraySize = photoIds.Length;
            for (var i = 0; i < photoIds.Length; i++)
            {
                photos.GetArrayElementAtIndex(i).objectReferenceValue = Photo(photoIds[i]);
            }

            Strings(so.FindProperty("tileIds"), ids);
            var tilesProp = so.FindProperty("tiles");
            tilesProp.arraySize = tiles.Length;
            for (var i = 0; i < tiles.Length; i++)
            {
                tilesProp.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A galaxy's portrait if it imports as a sprite, else its dock thumbnail, else nothing.</summary>
        private static Sprite Photo(string id)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFolder + id + "_portrait.png");
            return sprite != null ? sprite : AssetDatabase.LoadAssetAtPath<Sprite>(ThumbFolder + id + ".png");
        }

        private static UiButton Tile(string name, RectTransform parent, float x, float y, string text, Sprite sprite)
        {
            var root = Ui2Builder.Box(name, parent, x, y, 134f, 118f);

            var picture = Ui2Builder.Box("picture", root, 0f, 0f, 134f, 88f);
            Ui2Builder.Fill(picture, "ui_rounded_r64_fill", 12f, UiTheme.Deep);
            picture.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var image = Cover(picture);
            image.sprite = sprite;
            if (sprite != null)
            {
                image.GetComponent<AspectRatioFitter>().aspectRatio = sprite.rect.width / sprite.rect.height;
            }

            Ui2Builder.Fill(Ui2Builder.Box("edge", root, 0f, 0f, 134f, 88f), "ui_rounded_r64_ring", 12f,
                            new Color(1f, 1f, 1f, 0.3f));
            var glow = Ui2Builder.Glow(picture, 134f, 88f, 12f);
            glow.transform.SetParent(root, false);
            glow.rectTransform.anchoredPosition = picture.anchoredPosition;
            glow.transform.SetAsFirstSibling();
            var outline = Ui2Builder.Fill(Ui2Builder.Box("outline", root, -4f, -4f, 142f, 96f), "ui_rounded_r64_ring",
                                          15f, UiTheme.Accent);
            outline.enabled = false;

            var label = Text(Ui2Builder.Box("label", root, 0f, 98f, 134f, 20f), text, 16f, UiTheme.Ink,
                             TextAlignmentOptions.Center, FontWeight.Medium, false);

            var tile = Pressable(root, 134f, 118f, null, outline, label, false);
            var tileSo = new SerializedObject(tile);
            tileSo.FindProperty("glow").objectReferenceValue = glow;
            tileSo.ApplyModifiedPropertiesWithoutUndo();
            return tile;
        }

        private static UiButton Primary(string name, RectTransform parent, float x, float y, float w, float h, string text)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", root, w, h), "ui_rounded_r64_fill", 18f, UiTheme.Accent);
            var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", root, w, h), "ui_rounded_r64_fill", 18f, Color.clear);
            Glyph(root, "icon_play", w * 0.5f - 50f, (h - 22f) * 0.5f, 22f, UiTheme.Deep);
            var label = Text(Ui2Builder.Box("label", root, w * 0.5f - 18f, (h - 24f) * 0.5f, 90f, 24f), text, 19f,
                             UiTheme.Deep, TextAlignmentOptions.Left, FontWeight.SemiBold, false);
            return Pressable(root, w, h, wash, null, label, true);
        }

        /// <summary>The Compare button: drawn, but not pressable until the compare feature exists (U2-P9).</summary>
        private static void Secondary(string name, RectTransform parent, float x, float y, float w, float h, string text)
        {
            var root = Ui2Builder.Box(name, parent, x, y, w, h);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", root, w, h), "ui_rounded_r64_fill", 30f, WithAlpha(UiTheme.Deep, 0.6f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", root, w, h), "ui_rounded_r64_ring", 30f, new Color(1f, 1f, 1f, 0.75f));
            Glyph(root, "icon_compare", w * 0.5f - 64f, (h - 28f) * 0.5f, 28f, UiTheme.Ink);
            Text(Ui2Builder.Box("label", root, w * 0.5f - 28f, (h - 24f) * 0.5f, 100f, 24f), text, 19f, UiTheme.Ink,
                 TextAlignmentOptions.Left, FontWeight.SemiBold, false);
            root.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
        }

        private static void Glyph(RectTransform parent, string sprite, float x, float y, float size, Color colour)
        {
            var image = Ui2Builder.Box("icon", parent, x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = Ui2Builder.Icon(sprite);
            image.preserveAspect = true;
            image.color = colour;
            image.raycastTarget = false;
        }

        private static UiButton Pressable(RectTransform root, float w, float h, Image wash, Image outline,
                                          TMP_Text label, bool keepColours)
        {
            // Collider first: GEInteractable keeps only the colliders already on its own GameObject.
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
            so.FindProperty("keepColours").boolValue = keepColours;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>An image that fills its masked parent without distortion; the fitter's ratio is set per sprite.</summary>
        private static Image Cover(RectTransform parent)
        {
            var rect = Ui2Builder.Rect("image", parent, parent.sizeDelta.x, parent.sizeDelta.y);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            return image;
        }

        /// <summary>Stretches a graphic over its parent and takes it out of the parent's layout.</summary>
        private static void Behind(Image image)
        {
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private static TextMeshProUGUI Text(RectTransform rect, string text, float size, Color colour,
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

        private static void Objects(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void Strings(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
