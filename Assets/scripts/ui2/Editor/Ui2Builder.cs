// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Builds the UI 2.0 prefab from code, the way <see cref="UiPrefabBuilder"/> builds the legacy ones, so a
    /// change to the design is a change here and a rebuild rather than hand edits to a prefab
    /// (docs/design/v2/UNITY_ROADMAP.md). Menu: Cosmic Simulation > UI 2.
    ///
    /// Everything is laid out in the pixels of the approved 1487 x 1058 Figma frame, measured from its
    /// top-left corner, so numbers copy straight across from the design file. Each top-level piece is then
    /// pinned to the edge of the frame it belongs to (rail left, card right, dock bottom), which changes
    /// nothing at the design size and lets the same prefab fit any window on a monitor.
    ///
    /// Hierarchy: ui2_shell / frame / Navigation (Rail, Dock, Settings) | Explore (Breadcrumb, Picker, View,
    /// Compare, Card) | Panels (Guide, Lesson, Help, Settings) | Feedback (Notice).
    /// </summary>
    public static class Ui2Builder
    {
        private const string SpriteFolder = "Assets/ui/figma/";
        private const string OutputFolder = "Assets/prefabs/ui2/Resources";

        internal const float FrameWidth = 1487f;
        internal const float FrameHeight = 1058f;

        // The corner radius of ui_rounded_r64_fill and its ring, in sprite pixels.
        private const float SpriteRadiusPx = 64f;

        internal static readonly Vector2 Left = new Vector2(0f, 0.5f);
        internal static readonly Vector2 TopRight = new Vector2(1f, 1f);
        internal static readonly Vector2 Top = new Vector2(0.5f, 1f);
        internal static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        internal static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        [MenuItem("Cosmic Simulation/UI 2/Build Shell Prefab")]
        public static void BuildShell()
        {
            // Sprites that arrived since the last import rules ran.
            foreach (var folder in new[] { "Assets/ui/figma", "Assets/ui/photos" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var name = System.IO.Path.GetFileName(path);
                    if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null || name.StartsWith("ui_rounded_r64_"))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }

            var root = new GameObject("ui2_shell", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Above the room's dim layer (a camera-attached quad) and the legacy screen canvases (50 to 65).
            canvas.sortingOrder = 70;
            UseFontWeights();
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            canvasRect.localScale = Vector3.one * Ui2Shell.MetresPerPixel;

            var frame = Group("frame", canvasRect);
            var navigation = Group("Navigation", frame);
            var exploring = Group("Explore", frame);
            var panels = Group("Panels", frame);
            var feedback = Group("Feedback", frame);

            var surface = WithAlpha(UiTheme.Deep, 0.94f);
            var edge = new Color(1f, 1f, 1f, 0.18f);

            // Rail: the four contexts. Figma: Navigation/Rail at 36, 97, 114 x 583.
            var rail = Box("Rail", navigation, 36f, 97f, 114f, 583f);
            Rounded("plate", rail, 114f, 583f, 26f, surface);
            Ring("edge", rail, 114f, 583f, 26f, edge);

            var names = new[] { "Cosmic\nWeb", "Galaxies", "Milky Way", "Solar\nSystem" };
            var pictures = new[] { "v2_context_cosmic_web", "v2_context_galaxies", "v2_context_milky_way",
                                   "v2_context_solar_system" };
            var places = new[]
            {
                "cosmic_web",
                "galaxies,andromeda,whirlpool,pinwheel,triangulum",
                "milky_way,sagittarius_a,helix,crab,homunculus,orion,pillars,ngc1501,trumpler14",
                "solar_system,solar_system_planets,hd110067",
            };
            var items = new UiButton[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var top = 28f + 139f * i; // 122 tall, then 8 + 1 + 8 of divider and gaps
                items[i] = RailItem("context_" + i, rail, 5f, top, names[i], Icon(pictures[i]));
                if (i > 0)
                {
                    Rounded("divider_" + i, Box("line_" + i, rail, 26f, top - 9f, 62f, 1f), 62f, 1f, 0.5f,
                            UiTheme.Divider);
                }
            }

            Pin(rail, Left);

            // Dock: Explore, Guide, Room. Figma: Navigation/Dock at 535, 897, 418 x 93.
            var dock = Box("Dock", navigation, 535f, 897f, 418f, 93f);
            Rounded("plate", dock, 418f, 93f, 46.5f, surface);
            Ring("edge", dock, 418f, 93f, 46.5f, edge);
            var explore = Item("explore", dock, 5f, 5f, 135f, 83f, 40f, "Explore", Icon("v2_icon_dock_planet"), 50f);

            // The selected Explore pill is outlined brightly on the left and fades toward the right.
            var fade = explore.transform.Find("outline").GetComponent<Image>();
            fade.sprite = Icon("v2_dock_selected");
            fade.type = Image.Type.Simple;

            var guide = Item("guide", dock, 140f, 5f, 135f, 83f, 40f, "Guide", Icon("v2_companion"), 40f);

            // The companion is a picture, not a glyph: it stands up out of the dock as in the design.
            var orb = (RectTransform)guide.transform.Find("icon");
            orb.sizeDelta = new Vector2(88f, 79f);
            orb.anchoredPosition = new Vector2(0f, 83f * 0.5f + 45f - 79f * 0.5f);
            orb.GetComponent<Image>().color = Color.white;
            var guideSo = new SerializedObject(guide);
            guideSo.FindProperty("tintIcon").boolValue = false;
            guideSo.ApplyModifiedPropertiesWithoutUndo();
            ((RectTransform)guide.transform.Find("label")).anchoredPosition = new Vector2(0f, -83f * 0.5f + 9f + 10f);

            Rounded("divider", Box("line", dock, 275.5f, 23.5f, 1f, 46f), 1f, 46f, 0.5f, UiTheme.Divider);
            var room = Item("room", dock, 277f, 5f, 135f, 83f, 40f, "Room", Icon("v2_icon_dock_cube"), 38f);
            Pin(dock, Bottom);

            // Settings sits in its own corner. Figma: Controls/IconButton at 1378, 928, 60 x 60.
            var corner = Box("Settings", navigation, 1378f, 928f, 60f, 60f);
            Rounded("plate", corner, 60f, 60f, 30f, WithAlpha(UiTheme.Deep, 0.88f));
            Ring("edge", corner, 60f, 60f, 30f, UiTheme.Divider);
            var settings = Item("button", corner, 0f, 0f, 60f, 60f, 30f, null, Icon("v2_icon_gear"), 28f);
            Pin(corner, BottomRight);

            Ui2ExploreBuilder.Build(exploring, root);
            Ui2PanelsBuilder.BuildGuide(panels, root);
            Ui2PanelsBuilder.BuildHelp(panels, root);
            Ui2PanelsBuilder.BuildSettings(panels, root);
            Ui2PanelsBuilder.BuildNotice(feedback, out var notice, out var noticeText);

            foreach (var group in new[] { frame, navigation, exploring, panels, feedback })
            {
                Stretch(group);
            }

            var shell = root.AddComponent<Ui2Shell>();
            var so = new SerializedObject(shell);
            so.FindProperty("frame").objectReferenceValue = frame.gameObject;
            so.FindProperty("notice").objectReferenceValue = notice;
            so.FindProperty("noticeText").objectReferenceValue = noticeText;
            so.FindProperty("rail").objectReferenceValue = rail.gameObject;
            so.FindProperty("explore").objectReferenceValue = explore;
            so.FindProperty("guide").objectReferenceValue = guide;
            so.FindProperty("room").objectReferenceValue = room;
            so.FindProperty("settings").objectReferenceValue = settings;
            Ui2Kit.Objects(so.FindProperty("railItems"), items);
            Ui2Kit.Strings(so.FindProperty("railPlaces"), places);
            Ui2Kit.Strings(so.FindProperty("railNames"),
                           new[] { "Cosmic Web", "Galaxies", "Milky Way", "Solar System" });
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                System.IO.Directory.CreateDirectory(OutputFolder);
                AssetDatabase.Refresh();
            }

            var output = OutputFolder + "/ui2_shell.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, output);
            Object.DestroyImmediate(root);
            Debug.Log("[UI2] built " + output);
        }

        /// <summary>
        /// Points Selawik's semibold and bold weights at their own font assets, so a label set to SemiBold is
        /// drawn with the semibold face rather than the regular one.
        /// </summary>
        private static void UseFontWeights()
        {
            var regular = UiPrefabBuilder.Font();
            var semibold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/selawksb SDF.asset");
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/selawkb SDF.asset");
            if (regular == null || semibold == null || bold == null)
            {
                Debug.LogWarning("[UI2] Selawik semibold or bold font asset not found; headings will draw regular.");
                return;
            }

            var table = regular.fontWeightTable;
            if (table[6].regularTypeface == semibold && table[7].regularTypeface == bold)
            {
                return;
            }

            // Medium has no face of its own in Selawik and stays regular.
            table[6].regularTypeface = semibold;
            table[7].regularTypeface = bold;
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssetIfDirty(regular);
        }

        [MenuItem("Cosmic Simulation/UI 2/Run Check")]
        public static void RunCheck()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[UI2] enter play mode first; the check presses the running shell.");
                return;
            }

            if (!Ui2SelfCheck.Running)
            {
                new GameObject("ui2_check").AddComponent<Ui2SelfCheck>();
            }
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
        /// Navigation/RailItem, 104 x 122: the context's picture, its name under it, and when selected a cyan
        /// outline with a glow and a dot on the rail's left edge.
        /// </summary>
        private static UiButton RailItem(string name, RectTransform parent, float x, float y, string text,
                                         Sprite picture)
        {
            const float W = 104f, H = 122f;
            var root = Box(name, parent, x, y, W, H);
            var glow = Glow(root, W, H, 18f);

            // The dot straddles the rail's left edge, a little above the item's middle (Figma: 18 px at -11, 36).
            var dot = Rect("dot", glow.transform, 18f, 18f);
            dot.anchoredPosition = new Vector2(-W * 0.5f - 2f, H * 0.5f - 45f);
            var ring = dot.gameObject.AddComponent<Image>();
            ring.sprite = Icon("icon_dot");
            ring.color = UiTheme.Accent;
            ring.raycastTarget = false;
            var core = Rect("core", dot, 9f, 9f).gameObject.AddComponent<Image>();
            core.sprite = Icon("icon_dot");
            core.raycastTarget = false;

            var plate = Rounded("wash", root, W, H, 18f, Color.clear);
            var outline = Ring("outline", root, W, H, 18f, UiTheme.Accent);
            outline.enabled = false;

            var lines = text.Contains("\n") ? 2 : 1;
            var pictureTop = lines == 2 ? 6f : 16f;
            var aspect = picture != null ? picture.rect.width / picture.rect.height : 1f;
            var pictureWidth = 64f * Mathf.Max(1f, aspect);
            var image = Box("picture", root, (W - pictureWidth) * 0.5f, pictureTop, pictureWidth, 64f).gameObject
                .AddComponent<Image>();
            image.sprite = picture;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var label = Ui2Kit.Text(Box("label", root, 2f, pictureTop + 72f, W - 4f, 20f * lines), text, 16f,
                                    UiTheme.Ink, TextAlignmentOptions.Center, FontWeight.Medium, false);
            label.lineSpacing = -14f;

            var button = Ui2Kit.Press(root, W, H, plate, outline, label, image, false);
            var so = new SerializedObject(button);
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("tintIcon").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>
        /// Navigation/DockItem and the settings button: hover and selected wash, an outline, a glyph and an
        /// optional label under it, sitting together in the middle.
        /// </summary>
        private static UiButton Item(string name, RectTransform parent, float x, float y, float w, float h,
                                     float radius, string text, Sprite glyph, float glyphSize)
        {
            var root = Box(name, parent, x, y, w, h);

            var glow = Glow(root, w, h, radius);
            var plate = Rounded("wash", root, w, h, radius, Color.clear);
            var outline = Ring("outline", root, w, h, radius, UiTheme.Accent);
            outline.enabled = false;

            var hasLabel = !string.IsNullOrEmpty(text);
            var glyphTop = hasLabel ? (h - glyphSize - 4f - 20f) * 0.5f : (h - glyphSize) * 0.5f;
            var icon = Box("icon", root, (w - glyphSize) * 0.5f, glyphTop, glyphSize, glyphSize).gameObject
                .AddComponent<Image>();
            icon.sprite = glyph;
            icon.preserveAspect = true;
            icon.color = UiTheme.Ink;
            icon.raycastTarget = false;

            TMP_Text label = null;
            if (hasLabel)
            {
                label = Ui2Kit.Text(Box("label", root, 2f, glyphTop + glyphSize + 4f, w - 4f, 20f), text, 16f,
                                    UiTheme.Ink, TextAlignmentOptions.Center, FontWeight.Medium, false);
            }

            var button = Ui2Kit.Press(root, w, h, plate, outline, label, icon, false);
            var so = new SerializedObject(button);
            so.FindProperty("glow").objectReferenceValue = glow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>A named layer the size of the frame. Stretched over its parent once its children are placed.</summary>
        private static RectTransform Group(string name, RectTransform parent)
        {
            return Rect(name, parent, FrameWidth, FrameHeight);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Re-anchors a placed rect to an edge or corner of its parent without moving it, so it keeps its
        /// design-frame margin from that edge when the parent is a different size.
        /// </summary>
        internal static void Pin(RectTransform rect, Vector2 anchor)
        {
            var parent = ((RectTransform)rect.parent).sizeDelta;
            var held = (rect.anchorMin + rect.anchorMax) * 0.5f;
            rect.anchoredPosition += new Vector2((held.x - anchor.x) * parent.x, (held.y - anchor.y) * parent.y);
            rect.anchorMin = rect.anchorMax = anchor;
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

        /// <summary>
        /// The soft cyan halo behind a selected control. The glow sprite is a blurred ring inset 16 px in a
        /// 136 px square with a 48 px corner, so the rect overhangs the control by a third of its radius.
        /// </summary>
        internal static Image Glow(RectTransform root, float w, float h, float radius)
        {
            var over = radius / 3f;
            return Fill(Rect("selected", root, w + over * 2f, h + over * 2f), "ui_rounded_r64_glow",
                        radius * 64f / 48f, WithAlpha(UiTheme.Accent, 0.7f));
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

        internal static Sprite Icon(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (sprite == null)
            {
                Debug.LogError("[UI2] missing sprite " + SpriteFolder + name + ".png");
            }

            return sprite;
        }

        internal static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
