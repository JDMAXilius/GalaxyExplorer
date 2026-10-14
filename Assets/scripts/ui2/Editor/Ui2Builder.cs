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
    /// Builds the UI 2.0 prefabs from code, the way <see cref="UiPrefabBuilder"/> builds the legacy ones, so a
    /// change to the design is a change here and a rebuild rather than hand edits to a prefab
    /// (docs/design/v2/UNITY_ROADMAP.md). Menu: Cosmic Simulation > UI 2.
    ///
    /// Everything here is laid out in the pixels of the approved 1487 x 1058 Figma frame, measured from its
    /// top-left corner, so numbers copy straight across from the design file. The canvas scale turns one of
    /// those pixels into <see cref="Ui2Shell.MetresPerPixel"/> of world.
    /// </summary>
    public static class Ui2Builder
    {
        private const string SpriteFolder = "Assets/ui/figma/";
        private const string ThumbFolder = "Assets/ui/thumbnails/";
        private const string OutputFolder = "Assets/prefabs/ui2/Resources";

        private const float FrameWidth = 1487f;
        private const float FrameHeight = 1058f;

        // The corner radius of ui_rounded_r64_fill and its ring, in sprite pixels.
        private const float SpriteRadiusPx = 64f;

        [MenuItem("Cosmic Simulation/UI 2/Build Shell Prefab")]
        public static void BuildShell()
        {
            // The fill and ring are nine-sliced by UiSpriteImporter; a first import can predate that rule.
            AssetDatabase.ImportAsset(SpriteFolder + "ui_rounded_r64_fill.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SpriteFolder + "ui_rounded_r64_ring.png", ImportAssetOptions.ForceUpdate);

            var root = new GameObject("ui2_shell", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var frame = (RectTransform)root.transform;
            frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            frame.localScale = Vector3.one * Ui2Shell.MetresPerPixel;

            var surface = WithAlpha(UiTheme.Deep, 0.94f);
            var edge = new Color(1f, 1f, 1f, 0.18f);

            // Rail: the four contexts. Figma: Navigation/Rail at 36, 97, 114 x 583.
            var rail = Box("rail", frame, 36f, 97f, 114f, 583f);
            Rounded("plate", rail, 114f, 583f, 26f, surface);
            Ring("edge", rail, 114f, 583f, 26f, edge);

            var names = new[] { "Cosmic\nWeb", "Galaxies", "Milky Way", "Solar\nSystem" };
            var thumbs = new[] { "cosmic_web", "andromeda", "milky_way", "solar_system" };
            var places = new[]
            {
                "cosmic_web",
                "andromeda,galaxies,whirlpool,pinwheel,triangulum",
                "milky_way,sagittarius_a,helix,crab,homunculus,orion,pillars,ngc1501,trumpler14",
                "solar_system_planets,solar_system,hd110067",
            };
            var items = new UiButton[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var top = 28f + 139f * i; // 122 tall, then 8 + 1 + 8 of divider and gaps
                var thumb = AssetDatabase.LoadAssetAtPath<Sprite>(ThumbFolder + thumbs[i] + ".png");
                if (thumb == null)
                {
                    Debug.LogWarning("[UI2] no thumbnail for " + thumbs[i] + "; the rail item will be blank.");
                }

                items[i] = Item("context_" + i, rail, 5f, top, 104f, 122f, 18f, names[i], thumb, 64f, false);
                if (i > 0)
                {
                    Rounded("divider_" + i, Box("line_" + i, rail, 26f, top - 9f, 62f, 1f), 62f, 1f, 0.5f,
                            UiTheme.Divider);
                }
            }

            // Dock: Explore, Guide, Room. Figma: Navigation/Dock at 535, 897, 418 x 93.
            var dock = Box("dock", frame, 535f, 897f, 418f, 93f);
            Rounded("plate", dock, 418f, 93f, 46.5f, surface);
            Ring("edge", dock, 418f, 93f, 46.5f, edge);
            var explore = Item("explore", dock, 5f, 5f, 135f, 83f, 40f, "Explore", Icon("icon_explore"), 46f, true);
            var guide = Item("guide", dock, 140f, 5f, 135f, 83f, 40f, "Guide", Icon("icon_being"), 40f, true);
            Rounded("divider", Box("line", dock, 275.5f, 23.5f, 1f, 46f), 1f, 46f, 0.5f, UiTheme.Divider);
            var room = Item("room", dock, 277f, 5f, 135f, 83f, 40f, "Room", Icon("icon_room"), 38f, true);

            // Settings sits in its own corner. Figma: Controls/IconButton at 1378, 928, 60 x 60.
            var corner = Box("settings", frame, 1378f, 928f, 60f, 60f);
            Rounded("plate", corner, 60f, 60f, 30f, surface);
            Ring("edge", corner, 60f, 60f, 30f, WithAlpha(UiTheme.Divider, 1f));
            var settings = Item("button", corner, 0f, 0f, 60f, 60f, 30f, null, Icon("icon_settings"), 30f, true);

            Ui2ExploreBuilder.Build(frame, root);

            var shell = root.AddComponent<Ui2Shell>();
            var so = new SerializedObject(shell);
            so.FindProperty("rail").objectReferenceValue = rail.gameObject;
            so.FindProperty("explore").objectReferenceValue = explore;
            so.FindProperty("guide").objectReferenceValue = guide;
            so.FindProperty("room").objectReferenceValue = room;
            so.FindProperty("settings").objectReferenceValue = settings;
            var itemsProp = so.FindProperty("railItems");
            var placesProp = so.FindProperty("railPlaces");
            itemsProp.arraySize = items.Length;
            placesProp.arraySize = places.Length;
            for (var i = 0; i < items.Length; i++)
            {
                itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
                placesProp.GetArrayElementAtIndex(i).stringValue = places[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                System.IO.Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            var path = OutputFolder + "/ui2_shell.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[UI2] built " + path);
        }

        [MenuItem("Cosmic Simulation/UI 2/Turn On")]
        public static void TurnOn()
        {
            UiTheme.Enabled = true;
            Debug.Log("[UI2] on. Takes effect the next time the app starts.");
        }

        [MenuItem("Cosmic Simulation/UI 2/Turn Off")]
        public static void TurnOff()
        {
            UiTheme.Enabled = false;
            Debug.Log("[UI2] off. Takes effect the next time the app starts.");
        }

        /// <summary>
        /// One pressable: hover/selected wash, outline ring, a glyph or a round picture, and an optional label.
        /// Collider first, as in <c>UiPrefabBuilder.Pressable</c>: GEInteractable keeps only the colliders already
        /// on its own GameObject.
        /// </summary>
        private static UiButton Item(string name, RectTransform parent, float x, float y, float w, float h,
                                     float radius, string text, Sprite picture, float pictureSize, bool glyph)
        {
            var root = Box(name, parent, x, y, w, h);

            var plate = Rounded("wash", root, w, h, radius, Color.clear);
            var outline = Ring("outline", root, w, h, radius, UiTheme.Accent);
            outline.enabled = false;

            var hasLabel = !string.IsNullOrEmpty(text);
            var lines = hasLabel && text.Contains("\n") ? 2 : 1;
            var labelHeight = 20f * lines;

            // A glyph and its label sit as one block in the middle; a picture sits toward the top with the
            // label under it, as the rail items do in the design.
            var pictureTop = !hasLabel ? (h - pictureSize) * 0.5f
                : glyph ? (h - pictureSize - 4f - labelHeight) * 0.5f
                : 12f - (lines - 1) * 4f;

            Image icon;
            if (glyph)
            {
                icon = Picture("icon", Box("icon", root, (w - pictureSize) * 0.5f, pictureTop, pictureSize, pictureSize),
                               picture, UiTheme.Ink, true);
            }
            else
            {
                // A round picture: a circular mask, and the image scaled to cover it.
                var maskRect = Box("picture", root, (w - pictureSize) * 0.5f, pictureTop, pictureSize, pictureSize);
                var mask = Fill(maskRect, "ui_rounded_r64_fill", pictureSize * 0.5f, Color.white);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var aspect = picture != null ? picture.rect.width / picture.rect.height : 1f;
                var imageRect = Rect("image", maskRect, pictureSize * Mathf.Max(1f, aspect),
                                     pictureSize / Mathf.Min(1f, aspect));
                icon = Picture("image", imageRect, picture, Color.white, false);
            }

            TMP_Text label = null;
            if (hasLabel)
            {
                var labelTop = glyph ? pictureTop + pictureSize + 4f : pictureTop + pictureSize + 8f;
                var tmp = Box("label", root, 2f, labelTop, w - 4f, labelHeight).gameObject
                    .AddComponent<TextMeshProUGUI>();
                tmp.font = UiPrefabBuilder.Font();
                tmp.text = text;
                tmp.fontSize = 16f;
                tmp.lineSpacing = -14f;
                tmp.color = UiTheme.Ink;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontWeight = FontWeight.Medium;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                label = tmp;
            }

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(w, h, 3f);
            var interactable = root.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(root.gameObject, box, interactable);
            root.gameObject.AddComponent<GEButton>();

            var button = root.gameObject.AddComponent<UiButton>();
            var so = new SerializedObject(button);
            so.FindProperty("plate").objectReferenceValue = plate;
            so.FindProperty("outline").objectReferenceValue = outline;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("tintIcon").boolValue = glyph;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>A rect placed the way Figma states it: x and y of its top-left corner inside its parent.</summary>
        internal static RectTransform Box(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = Rect(name, parent, w, h);
            var size = parent.sizeDelta;
            rt.anchoredPosition = new Vector2(x + w * 0.5f - size.x * 0.5f, size.y * 0.5f - y - h * 0.5f);
            return rt;
        }

        internal static RectTransform Rect(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private static Image Rounded(string name, Transform parent, float w, float h, float radius, Color colour)
        {
            return Fill(Rect(name, parent, w, h), "ui_rounded_r64_fill", radius, colour);
        }

        private static Image Ring(string name, Transform parent, float w, float h, float radius, Color colour)
        {
            return Fill(Rect(name, parent, w, h), "ui_rounded_r64_ring", radius, colour);
        }

        internal static Image Fill(RectTransform rect, string sprite, float radius, Color colour)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Icon(sprite);
            image.type = Image.Type.Sliced;

            // A sliced corner is drawn at (corner pixels x canvas reference pixels per unit / sprite pixels per
            // unit) canvas units, divided by this multiplier. Solve for the radius asked for.
            var native = image.sprite != null ? SpriteRadiusPx * 100f / image.sprite.pixelsPerUnit : 1f;
            image.pixelsPerUnitMultiplier = native / Mathf.Max(0.1f, radius);
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Image Picture(string name, RectTransform rect, Sprite sprite, Color colour, bool keepAspect)
        {
            rect.name = name;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = keepAspect;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Icon(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (sprite == null)
            {
                Debug.LogError("[UI2] missing sprite " + SpriteFolder + name + ".png");
            }

            return sprite;
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
