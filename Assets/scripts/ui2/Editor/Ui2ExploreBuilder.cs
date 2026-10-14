// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using CosmicSimulation.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The exploring half of the UI 2.0 shell prefab: breadcrumb, top picker, the Solar System's View chip and
    /// menu, the compare controls and the photo-first card. Called by <see cref="Ui2Builder.BuildShell"/>;
    /// positions are pixels of the approved Figma frame (S01, S03, S08, S12, S13).
    /// </summary>
    internal static class Ui2ExploreBuilder
    {
        private const string PortraitFolder = "Assets/Textures/galaxies/portraits/";
        private const string PhotoFolder = "Assets/ui/photos";

        internal static void Build(RectTransform frame, GameObject root)
        {
            // Breadcrumb. Figma: Navigation/Breadcrumb, centred on the frame at y 40.
            var crumb = Ui2Kit.Text(Ui2Builder.Box("Breadcrumb", frame, 343f, 36f, 800f, 36f), string.Empty, 24f,
                                    UiTheme.Ink, TextAlignmentOptions.Center, FontWeight.Medium, false);
            Ui2Builder.Pin(crumb.rectTransform, Ui2Builder.Top);

            // Picker. Figma: Navigation/DestinationTile, four showing from x 455, y 93. It is a carousel: every
            // entry has a tile on one strip, the strip slides behind a mask, and an arrow either side moves it.
            const int Pool = 12;
            var picker = Ui2Builder.Box("Picker", frame, 399f, 84f, 690f, 136f);
            var viewport = Ui2Builder.Box("viewport", picker, 48f, 0f, 588f, 136f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var strip = Ui2Builder.Rect("strip", viewport, 0f, 136f);
            strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = Vector2.zero;
            var tiles = new UiButton[Pool];
            var tilePhotos = new Image[Pool];
            for (var i = 0; i < Pool; i++)
            {
                tiles[i] = Tile("tile_" + i, strip, 8f + 146f * i, 9f, out tilePhotos[i]);
            }

            var prev = Ui2Kit.IconButton(picker, "previous", 4f, 35f, 36f, "v2_icon_back", 20f);
            var next = Ui2Kit.IconButton(picker, "next", 640f, 35f, 36f, "v2_icon_chevron", 20f);
            Ui2Builder.Pin(picker, Ui2Builder.Top);

            // View chip and its menu. Figma: Controls/Chip at 646, 226, 194 x 46; Controls/Menu 266 x 219.
            var chipRoot = Ui2Builder.Box("View", frame, 646f, 226f, 194f, 46f);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", chipRoot, 194f, 46f), "ui_rounded_r64_fill", 23f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.88f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", chipRoot, 194f, 46f), "ui_rounded_r64_ring", 23f,
                            new Color(1f, 1f, 1f, 0.3f));
            var chipWash = Ui2Builder.Fill(Ui2Builder.Rect("wash", chipRoot, 194f, 46f), "ui_rounded_r64_fill", 23f,
                                           Color.clear);
            var chipOutline = Ui2Builder.Fill(Ui2Builder.Rect("outline", chipRoot, 194f, 46f), "ui_rounded_r64_ring",
                                              23f, UiTheme.Accent);
            chipOutline.enabled = false;
            Ui2Kit.Label(chipRoot, "category", 19f, 12f, 44f, 22f, "View", 17f, UiTheme.InkSecondary);
            var chipValue = Ui2Kit.Label(chipRoot, "value", 67f, 13f, 88f, 20f, "Equal Size", 16f, UiTheme.Ink,
                                         FontWeight.SemiBold);
            Ui2Kit.Glyph(chipRoot, "v2_icon_chevron_down", 157f, 11f, 24f, UiTheme.Ink);
            var chip = Ui2Kit.Press(chipRoot, 194f, 46f, chipWash, chipOutline, null, null, true);

            Ui2Builder.Pin(chipRoot, Ui2Builder.Top);

            var menu = Ui2Kit.Panel(frame, "ViewMenu", 610f, 280f, 266f, 219f, 18f);
            Ui2Builder.Pin(menu, Ui2Builder.Top);
            var menuNames = new[] { "Orbits", "Equal Size", "Relative Size", "Other star systems" };
            var menuTops = new[] { 9f, 59f, 109f, 162f };
            var menuItems = new UiButton[menuNames.Length];
            var menuChecks = new GameObject[menuNames.Length];
            Ui2Kit.Divider(menu, 17f, 159f, 232f);
            for (var i = 0; i < menuNames.Length; i++)
            {
                var item = Ui2Builder.Box("item_" + i, menu, 9f, menuTops[i], 248f, 48f);
                var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", item, 248f, 48f), "ui_rounded_r64_fill", 12f,
                                           Color.clear);
                var label = Ui2Kit.Label(item, "label", 16f, 14f, 190f, 20f, menuNames[i], 17f, UiTheme.Ink,
                                         FontWeight.Medium);
                var check = Ui2Kit.Glyph(item, "v2_icon_check", 210f, 12f, 24f, UiTheme.Accent);
                menuChecks[i] = check.gameObject;
                menuItems[i] = Ui2Kit.Press(item, 248f, 48f, wash, null, label, null, false);
            }

            // Compare. Figma S13: Controls/Segmented at 533, 127; S12: Cancel at 668, 232.
            var size = Ui2Kit.Segmented(frame, "CompareSize", 533f, 127f, new[] { 120f, 140f },
                                        new[] { "Equal Size", "Relative Size" });
            var sizeBar = size[0].transform.parent.gameObject;
            Ui2Builder.Pin((RectTransform)sizeBar.transform, Ui2Builder.Top);
            var cancel = Ui2Kit.Secondary(frame, "CompareCancel", 668f, 232f, 151f, 60f, "Cancel", null);
            Ui2Builder.Pin((RectTransform)cancel.transform, Ui2Builder.Top);

            // Card. Figma: Card/Information at 1083, 118, 384 wide, growing downward with its text.
            var card = Ui2Builder.Rect("Card", frame, 384f, 600f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = new Vector2(1083f + 192f - frame.sizeDelta.x * 0.5f, frame.sizeDelta.y * 0.5f - 118f);
            Ui2Builder.Pin(card, Ui2Builder.TopRight);
            Behind(Ui2Builder.Fill(Ui2Builder.Rect("plate", card, 384f, 600f), "ui_rounded_r64_fill", 20f,
                                   Ui2Builder.WithAlpha(UiTheme.Panel, 0.97f)));
            Behind(Ui2Builder.Fill(Ui2Builder.Rect("edge", card, 384f, 600f), "ui_rounded_r64_ring", 20f,
                                   new Color(1f, 1f, 1f, 0.16f)));

            var stack = card.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(21, 21, 33, 30);
            stack.spacing = 17f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // "Back to map" sits 23 px under the card's top edge, 10 px tighter than the padding: the slot is
            // short and the row hangs up out of it.
            var backSlot = Ui2Builder.Rect("back", card, 342f, 12f);
            backSlot.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
            var backRow = Ui2Builder.Box("row", backSlot, 0f, -10f, 150f, 22f);
            var backWash = Ui2Builder.Fill(Ui2Builder.Box("wash", backRow, -8f, -6f, 166f, 34f), "ui_rounded_r64_fill",
                                           12f, Color.clear);
            Ui2Kit.Glyph(backRow, "v2_icon_back", 0f, 0f, 22f, UiTheme.Ink);
            Ui2Kit.Label(backRow, "label", 32f, 0f, 118f, 22f, "Back to map", 17f, UiTheme.Ink);
            var back = Ui2Kit.Press(backRow, 166f, 34f, backWash, null, null, null, true);

            var photoRoot = Ui2Builder.Rect("photo", card, 342f, 207f);
            photoRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 207f;
            Ui2Builder.Fill(photoRoot, "ui_rounded_r64_fill", 12f, UiTheme.Deep);
            photoRoot.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var photo = Half(photoRoot, "first", 0f, 1f);
            var photo2 = Half(photoRoot, "second", 0.5f, 1f);

            var title = Ui2Kit.Text(Ui2Builder.Rect("title", card, 342f, 42f), "ANDROMEDA", 36f, UiTheme.Ink,
                                    TextAlignmentOptions.TopLeft, FontWeight.SemiBold, true);
            var body = Ui2Kit.Text(Ui2Builder.Rect("description", card, 342f, 81f), "Description", 20f, UiTheme.Ink,
                                   TextAlignmentOptions.TopLeft, FontWeight.Regular, true);
            body.lineSpacing = 8f;

            var divider = Ui2Builder.Rect("divider", card, 342f, 1f);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;
            var line = divider.gameObject.AddComponent<Image>();
            line.color = UiTheme.Divider;
            line.raycastTarget = false;

            // Card/FactPair: label, value, and a unit line when the unit is long. 151 wide each, 192 apart.
            var facts = Ui2Builder.Rect("facts", card, 342f, 78f);
            facts.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
            var factLabels = new TMP_Text[2];
            var factValues = new TMP_Text[2];
            var factUnits = new TMP_Text[2];
            for (var i = 0; i < 2; i++)
            {
                var column = Ui2Builder.Rect("fact_" + i, facts, 151f, 78f);
                column.anchorMin = column.anchorMax = column.pivot = new Vector2(0f, 1f);
                column.anchoredPosition = new Vector2(i * 192f, 0f);
                var rows = column.gameObject.AddComponent<VerticalLayoutGroup>();
                rows.spacing = 2f;
                rows.childControlWidth = true;
                rows.childControlHeight = true;
                rows.childForceExpandHeight = false;
                factLabels[i] = Ui2Kit.Text(Ui2Builder.Rect("label", column, 151f, 22f), "Label", 17f,
                                            UiTheme.InkSecondary, TextAlignmentOptions.TopLeft, FontWeight.Regular, false);
                factValues[i] = Ui2Kit.Text(Ui2Builder.Rect("value", column, 151f, 30f), "Value", 24f, UiTheme.Ink,
                                            TextAlignmentOptions.TopLeft, FontWeight.SemiBold, false);
                factUnits[i] = Ui2Kit.Text(Ui2Builder.Rect("unit", column, 151f, 22f), "unit", 18f, UiTheme.Ink,
                                           TextAlignmentOptions.TopLeft, FontWeight.Regular, false);
            }

            var split = Ui2Builder.Rect("split", facts, 1f, 78f);
            split.anchorMin = split.anchorMax = split.pivot = new Vector2(0f, 1f);
            split.anchoredPosition = new Vector2(171f, 0f);
            var splitImage = split.gameObject.AddComponent<Image>();
            splitImage.color = UiTheme.Divider;
            splitImage.raycastTarget = false;

            // Three action rows, one showing at a time: information, destination preview, compare pair.
            var info = Actions(card, "actions");
            var listen = Ui2Kit.Primary(info, "listen", 0f, 0f, 165f, 60f, "Listen", "v2_icon_play", "Resume");
            var compare = Ui2Kit.Secondary(info, "compare", 175f, 0f, 167f, 60f, "Compare", "v2_icon_compare");
            var preview = Actions(card, "actions_preview");
            var enter = Ui2Kit.Primary(preview, "enter", 0f, 0f, 188f, 60f, "Enter nebula", "v2_icon_rocket");
            var previewListen = Ui2Kit.Secondary(preview, "listen", 198f, 0f, 144f, 60f, "Listen", "v2_icon_playcircle", "Resume");
            var pair = Actions(card, "actions_compare");
            var change = Ui2Kit.Primary(pair, "change", 0f, 0f, 190f, 60f, "Change planet", "v2_icon_planet");
            var done = Ui2Kit.Secondary(pair, "done", 200f, 0f, 142f, 60f, "Done", "v2_icon_check");

            var explore = root.AddComponent<Ui2Explore>();
            var so = new SerializedObject(explore);
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("back").objectReferenceValue = back;
            so.FindProperty("photoRoot").objectReferenceValue = photoRoot.gameObject;
            so.FindProperty("photo").objectReferenceValue = photo;
            so.FindProperty("photo2").objectReferenceValue = photo2;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("divider").objectReferenceValue = divider.gameObject;
            so.FindProperty("factsRoot").objectReferenceValue = facts.gameObject;
            so.FindProperty("factSplit").objectReferenceValue = split;
            Ui2Kit.Objects(so.FindProperty("factLabels"), factLabels);
            Ui2Kit.Objects(so.FindProperty("factValues"), factValues);
            Ui2Kit.Objects(so.FindProperty("factUnits"), factUnits);
            so.FindProperty("actionsInfo").objectReferenceValue = info.gameObject;
            so.FindProperty("listen").objectReferenceValue = listen;
            so.FindProperty("compare").objectReferenceValue = compare;
            so.FindProperty("actionsPreview").objectReferenceValue = preview.gameObject;
            so.FindProperty("enter").objectReferenceValue = enter;
            so.FindProperty("previewListen").objectReferenceValue = previewListen;
            so.FindProperty("actionsCompare").objectReferenceValue = pair.gameObject;
            so.FindProperty("change").objectReferenceValue = change;
            so.FindProperty("done").objectReferenceValue = done;
            so.FindProperty("playIcon").objectReferenceValue = Ui2Builder.Icon("v2_icon_play");
            so.FindProperty("pauseIcon").objectReferenceValue = Ui2Builder.Icon("icon_pause");
            so.FindProperty("picker").objectReferenceValue = picker.gameObject;
            so.FindProperty("strip").objectReferenceValue = strip;
            Ui2Kit.Objects(so.FindProperty("tiles"), tiles);
            Ui2Kit.Objects(so.FindProperty("tilePhotos"), tilePhotos);
            so.FindProperty("prev").objectReferenceValue = prev;
            so.FindProperty("next").objectReferenceValue = next;
            so.FindProperty("crumb").objectReferenceValue = crumb;
            so.FindProperty("chip").objectReferenceValue = chip;
            so.FindProperty("chipValue").objectReferenceValue = chipValue;
            so.FindProperty("menu").objectReferenceValue = menu.gameObject;
            Ui2Kit.Objects(so.FindProperty("menuItems"), menuItems);
            Ui2Kit.Objects(so.FindProperty("menuChecks"), menuChecks);
            so.FindProperty("sizeBar").objectReferenceValue = sizeBar;
            so.FindProperty("equalSize").objectReferenceValue = size[0];
            so.FindProperty("relativeSize").objectReferenceValue = size[1];
            so.FindProperty("cancel").objectReferenceValue = cancel;

            // Every BodyInfo in the project, moons included, for markers that carry no data of their own.
            var infos = new List<Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:BodyInfo", new[] { "Assets/data" }))
            {
                var bodyInfo = AssetDatabase.LoadAssetAtPath<BodyInfo>(AssetDatabase.GUIDToAssetPath(guid));
                if (bodyInfo != null)
                {
                    infos.Add(bodyInfo);
                }
            }

            Ui2Kit.Objects(so.FindProperty("bodies"), infos.ToArray());

            // Pictures by id. A card uses card_<id>, a tile uses tile_<id> (both cut from the approved design
            // images); where the design has no picture of something, <id> is the app's own render of it, and a
            // galaxy falls back to its portrait.
            var ids = new List<string>();
            foreach (var path in Directory.GetFiles(PhotoFolder, "*.png"))
            {
                var id = Path.GetFileNameWithoutExtension(path);
                if (id.StartsWith("lesson_"))
                {
                    continue;
                }

                id = id.StartsWith("card_") ? id.Substring(5) : id.StartsWith("tile_") ? id.Substring(5) : id;
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            var cards = new Object[ids.Count];
            var thumbs = new Object[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                var own = Ui2Kit.Photo(ids[i]);
                var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFolder + ids[i] + "_portrait.png");
                cards[i] = Ui2Kit.Photo("card_" + ids[i]) ?? own ?? Ui2Kit.Photo("tile_" + ids[i]) ?? portrait;
                thumbs[i] = Ui2Kit.Photo("tile_" + ids[i]) ?? own ?? Ui2Kit.Photo("card_" + ids[i]) ?? portrait;
            }

            Ui2Kit.Strings(so.FindProperty("photoIds"), ids.ToArray());
            Ui2Kit.Objects(so.FindProperty("photos"), cards);
            Ui2Kit.Objects(so.FindProperty("thumbs"), thumbs);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Actions(RectTransform card, string name)
        {
            var row = Ui2Builder.Rect(name, card, 342f, 60f);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
            return row;
        }

        /// <summary>One side of the card photo. Alone it spans the frame; in a compare pair each takes a half.</summary>
        private static Image Half(RectTransform photoRoot, string name, float fromX, float toX)
        {
            var half = Ui2Builder.Rect(name, photoRoot, 0f, 0f);
            half.anchorMin = new Vector2(fromX, 0f);
            half.anchorMax = new Vector2(toX, 1f);
            half.offsetMin = half.offsetMax = Vector2.zero;
            half.gameObject.AddComponent<RectMask2D>();
            return Ui2Kit.Cover(half);
        }

        /// <summary>Navigation/DestinationTile: a 134 x 88 picture with its name under it.</summary>
        private static UiButton Tile(string name, RectTransform parent, float x, float y, out Image image)
        {
            var root = Ui2Builder.Box(name, parent, x, y, 134f, 118f);

            var picture = Ui2Builder.Box("picture", root, 0f, 0f, 134f, 88f);
            Ui2Builder.Fill(picture, "ui_rounded_r64_fill", 12f, UiTheme.Deep);
            picture.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            image = Ui2Kit.Cover(picture);

            Ui2Builder.Fill(Ui2Builder.Box("edge", root, 0f, 0f, 134f, 88f), "ui_rounded_r64_ring", 12f,
                            new Color(1f, 1f, 1f, 0.3f));
            var glow = Ui2Builder.Glow(picture, 134f, 88f, 12f);
            glow.transform.SetParent(root, false);
            glow.rectTransform.anchoredPosition = picture.anchoredPosition;
            glow.transform.SetAsFirstSibling();
            var outline = Ui2Builder.Fill(Ui2Builder.Box("outline", root, -4f, -4f, 142f, 96f), "ui_rounded_r64_ring",
                                          15f, UiTheme.Accent);
            outline.enabled = false;

            var label = Ui2Kit.Text(Ui2Builder.Box("label", root, -8f, 98f, 150f, 20f), "Name", 16f, UiTheme.Ink,
                                    TextAlignmentOptions.Center, FontWeight.Medium, false);

            var tile = Ui2Kit.Press(root, 134f, 118f, null, outline, label, null, false);
            var tileSo = new SerializedObject(tile);
            tileSo.FindProperty("glow").objectReferenceValue = glow;
            tileSo.ApplyModifiedPropertiesWithoutUndo();
            return tile;
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
    }
}
