// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// The focused panels of the UI 2.0 shell prefab: Settings (S23 to S27), Guide (S15 to S22), Help and the
    /// lessons (S29 to S32) and the notice bar (S33 to S35). Called by <see cref="Ui2Builder.BuildShell"/>;
    /// positions are pixels of the approved Figma frame.
    /// </summary>
    internal static class Ui2PanelsBuilder
    {
        // ---------- Settings. Figma: Settings/Panel at 753, 76, 501 x 824.

        internal static void BuildSettings(RectTransform frame, GameObject root)
        {
            var panel = Ui2Kit.Panel(frame, "Settings", 753f, 76f, 501f, 824f, 26f);
            TextMeshProUGUI note, value;

            var main = Ui2Kit.Page(panel, "main");
            Ui2Kit.Label(main, "title", 35f, 35f, 300f, 42f, "Settings", 36f, UiTheme.Ink, FontWeight.Medium);
            var close = Ui2Kit.IconButton(main, "close", 416f, 31f, 50f, "v2_icon_close", 24f);

            var sound = Ui2Kit.Row(main, "sound", 35f, 90f, 431f, 67f, "Sound", "Galaxy sounds and ambient audio",
                                   null, false, out note, out value, false);
            var soundSwitch = Ui2Kit.Switch((RectTransform)sound.transform, 363f, 14f);
            var narration = Ui2Kit.Row(main, "narration", 35f, 165f, 431f, 67f, "Narration",
                                       "Spoken guides and information", null, false, out note, out value, false);
            var narrationSwitch = Ui2Kit.Switch((RectTransform)narration.transform, 363f, 14f);

            Ui2Kit.Label(main, "text_size", 35f, 246f, 150f, 27f, "Text size", 22f, UiTheme.Ink);
            Ui2Kit.Label(main, "text_size_note", 35f, 275f, 150f, 22f, "Card text", 17f, UiTheme.InkSecondary);
            var sizes = Ui2Kit.Segmented(main, "sizes", 176f, 253f, new[] { 111f, 82f, 87f },
                                         new[] { "Standard", "Large", "Larger" });

            var microphone = Ui2Kit.Row(main, "microphone", 35f, 330f, 431f, 67f, "Microphone device",
                                        "Input for voice features", null, true, out note, out value);
            var microphoneValue = value;
            Ui2Kit.Divider(main, 35f, 406f, 431f);
            var help = Ui2Kit.Row(main, "help", 35f, 415f, 431f, 67f, "Help", "Replay the lessons", "v2_icon_help", true,
                                  out note, out value);
            var about = Ui2Kit.Row(main, "about", 35f, 491f, 431f, 67f, "About", "Version and credits", "v2_icon_info",
                                   true, out note, out value);
            Ui2Kit.Divider(main, 35f, 567f, 431f);
            var bring = Ui2Kit.Row(main, "bring", 35f, 577f, 431f, 67f, "Bring menu here",
                                   "Move the menu closer to you", "v2_icon_move", true, out note, out value);
            var restore = Ui2Kit.Row(main, "restore", 35f, 652f, 431f, 67f, "Restore scene",
                                     "Put objects in this place back", "v2_icon_retry", true, out note, out value);
            var quit = Ui2Kit.Row(main, "quit", 35f, 728f, 431f, 69f, "Quit", "Close Cosmic Simulation XR",
                                  "v2_icon_quit", true, out note, out value, true, true);

            // Device chooser (S24).
            var devices = Ui2Kit.Page(panel, "devices");
            var devicesBack = Ui2Kit.IconButton(devices, "back", 31f, 36f, 40f, "v2_icon_back", 22f, false);
            Ui2Kit.Label(devices, "title", 79f, 35f, 300f, 42f, "Microphone", 36f, UiTheme.Ink, FontWeight.Medium);
            var devicesClose = Ui2Kit.IconButton(devices, "close", 416f, 31f, 50f, "v2_icon_close", 24f);
            Ui2Kit.Label(devices, "intro", 35f, 95f, 431f, 27f, "Choose the microphone the guide listens to.", 20f,
                         UiTheme.InkSecondary);
            var deviceRows = new UiButton[4];
            var deviceNotes = new TMP_Text[4];
            for (var i = 0; i < deviceRows.Length; i++)
            {
                deviceRows[i] = Ui2Kit.Row(devices, "device_" + i, 35f, 136f + 81f * i, 431f, 67f, "Device",
                                           "Available", "v2_icon_mic", false, out note, out value);
                deviceNotes[i] = note;
            }

            Ui2Kit.Divider(devices, 35f, 464f, 431f);
            Ui2Kit.Label(devices, "level_label", 35f, 479f, 431f, 22f, "Test level", 17f, UiTheme.InkSecondary);
            var track = Ui2Builder.Box("level", devices, 35f, 515f, 431f, 10f);
            Ui2Builder.Fill(Ui2Builder.Rect("track", track, 431f, 10f), "ui_rounded_r64_fill", 5f,
                            new Color(1f, 1f, 1f, 0.18f));
            var level = Ui2Builder.Rect("fill", track, 0f, 10f);
            level.anchorMin = level.anchorMax = level.pivot = new Vector2(0f, 0.5f);
            level.anchoredPosition = Vector2.zero;
            Ui2Builder.Fill(level, "ui_rounded_r64_fill", 5f, UiTheme.Accent);
            Ui2Kit.Label(devices, "level_note", 35f, 539f, 431f, 44f,
                         "Speak to check it. The bar moves when the microphone hears you.", 17f, UiTheme.InkSecondary,
                         FontWeight.Regular, true);
            var empty = Ui2Kit.Label(devices, "empty", 35f, 597f, 431f, 44f, string.Empty, 17f, Ui2Kit.Attention,
                                     FontWeight.Regular, true);
            var devicesDone = Ui2Kit.Primary(devices, "done", 35f, 655f, 133f, 60f, "Done", null);

            // About (S25).
            var aboutPage = Ui2Kit.Page(panel, "about");
            var aboutBack = Ui2Kit.IconButton(aboutPage, "back", 31f, 36f, 40f, "v2_icon_back", 22f, false);
            Ui2Kit.Label(aboutPage, "title", 79f, 35f, 300f, 42f, "About", 36f, UiTheme.Ink, FontWeight.Medium);
            var aboutClose = Ui2Kit.IconButton(aboutPage, "close", 416f, 31f, 50f, "v2_icon_close", 24f);
            Ui2Kit.Label(aboutPage, "name", 35f, 95f, 431f, 30f, "Cosmic Simulation XR", 24f, UiTheme.Ink,
                         FontWeight.Medium);
            var version = Ui2Kit.Label(aboutPage, "version", 35f, 139f, 431f, 22f, "Version", 17f,
                                       UiTheme.InkSecondary);
            Ui2Kit.Divider(aboutPage, 35f, 175f, 431f);
            Ui2Kit.Label(aboutPage, "lineage", 35f, 190f, 431f, 54f,
                         "Built on Galaxy Explorer by Microsoft, used under the MIT licence.", 20f, UiTheme.Ink,
                         FontWeight.Regular, true);
            Ui2Kit.Label(aboutPage, "credits", 35f, 258f, 431f, 44f,
                         "Imagery and data credits are listed with the project's source.", 17f, UiTheme.InkSecondary,
                         FontWeight.Regular, true);
            Ui2Kit.Divider(aboutPage, 35f, 316f, 431f);
            var source = Ui2Kit.Row(aboutPage, "source", 35f, 331f, 431f, 67f, "Source", "Project repository",
                                    "v2_icon_link", true, out note, out value);
            var aboutDone = Ui2Kit.Secondary(aboutPage, "close_button", 35f, 412f, 140f, 60f, "Close", null);

            // Confirmation (S26, S27): sits over the page it was asked from.
            var confirm = Ui2Kit.Page(panel, "confirm");
            Ui2Builder.Fill(Ui2Builder.Rect("scrim", confirm, 501f, 824f), "ui_rounded_r64_fill", 26f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.86f));
            var box = Ui2Kit.Panel(confirm, "box", 25f, 290f, 451f, 232f, 22f);
            var confirmTitle = Ui2Kit.Label(box, "title", 28f, 28f, 395f, 60f, "Quit Cosmic Simulation XR?", 24f,
                                            UiTheme.Ink, FontWeight.Medium, true);
            var confirmBody = Ui2Kit.Label(box, "body", 28f, 92f, 395f, 48f, string.Empty, 18f, UiTheme.InkSecondary,
                                           FontWeight.Regular, true);
            var confirmYes = Ui2Kit.Primary(box, "yes", 28f, 148f, 150f, 60f, "Quit", null, "Restore");
            var confirmNo = Ui2Kit.Secondary(box, "no", 190f, 148f, 233f, 60f, "Keep exploring", null);

            var settings = root.AddComponent<Ui2Settings>();
            var so = new SerializedObject(settings);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("main").objectReferenceValue = main.gameObject;
            so.FindProperty("devices").objectReferenceValue = devices.gameObject;
            so.FindProperty("about").objectReferenceValue = aboutPage.gameObject;
            so.FindProperty("close").objectReferenceValue = close;
            so.FindProperty("sound").objectReferenceValue = sound;
            so.FindProperty("soundSwitch").objectReferenceValue = soundSwitch;
            so.FindProperty("narration").objectReferenceValue = narration;
            so.FindProperty("narrationSwitch").objectReferenceValue = narrationSwitch;
            Ui2Kit.Objects(so.FindProperty("sizes"), sizes);
            so.FindProperty("microphone").objectReferenceValue = microphone;
            so.FindProperty("microphoneValue").objectReferenceValue = microphoneValue;
            so.FindProperty("help").objectReferenceValue = help;
            so.FindProperty("aboutRow").objectReferenceValue = about;
            so.FindProperty("bring").objectReferenceValue = bring;
            so.FindProperty("restore").objectReferenceValue = restore;
            so.FindProperty("quit").objectReferenceValue = quit;
            so.FindProperty("devicesBack").objectReferenceValue = devicesBack;
            so.FindProperty("devicesClose").objectReferenceValue = devicesClose;
            Ui2Kit.Objects(so.FindProperty("deviceRows"), deviceRows);
            Ui2Kit.Objects(so.FindProperty("deviceNotes"), deviceNotes);
            so.FindProperty("level").objectReferenceValue = level;
            so.FindProperty("empty").objectReferenceValue = empty;
            so.FindProperty("devicesDone").objectReferenceValue = devicesDone;
            so.FindProperty("aboutBack").objectReferenceValue = aboutBack;
            so.FindProperty("aboutClose").objectReferenceValue = aboutClose;
            so.FindProperty("version").objectReferenceValue = version;
            so.FindProperty("source").objectReferenceValue = source;
            so.FindProperty("aboutDone").objectReferenceValue = aboutDone;
            so.FindProperty("confirm").objectReferenceValue = confirm.gameObject;
            so.FindProperty("confirmTitle").objectReferenceValue = confirmTitle;
            so.FindProperty("confirmBody").objectReferenceValue = confirmBody;
            so.FindProperty("confirmYes").objectReferenceValue = confirmYes;
            so.FindProperty("confirmNo").objectReferenceValue = confirmNo;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        // ---------- Guide. Figma: Guide/Panel at 998, 118, 444 x 632.

        internal static void BuildGuide(RectTransform frame, GameObject root)
        {
            var panel = Ui2Kit.Panel(frame, "Guide", 998f, 118f, 444f, 632f, 24f);
            Ui2Builder.Pin(panel, Ui2Builder.TopRight);
            Ui2Kit.Glyph(panel, "v2_icon_planet", 27f, 31f, 52f, UiTheme.Accent);
            Ui2Kit.Label(panel, "title", 95f, 36f, 320f, 42f, "COSMIC GUIDE", 30f, UiTheme.Ink, FontWeight.SemiBold);

            var wave = Ui2Kit.Glyph(panel, "v2_icon_wave", 186f, 113f, 72f, UiTheme.Accent);
            var phase = Ui2Kit.Label(panel, "phase", 27f, 195f, 390f, 42f, "Ready to help", 32f, UiTheme.Ink,
                                     FontWeight.SemiBold, false, TextAlignmentOptions.Top);
            var helper = Ui2Kit.Label(panel, "helper", 27f, 247f, 390f, 81f, "Ask about what you see.", 20f,
                                      UiTheme.InkSecondary, FontWeight.Regular, true, TextAlignmentOptions.Top);

            var texts = new[] { "Why does Saturn have rings?", "Take me to Orion." };
            var questions = new UiButton[texts.Length];
            var questionLabels = new TMP_Text[texts.Length];
            for (var i = 0; i < texts.Length; i++)
            {
                var q = Ui2Builder.Box("question_" + i, panel, 27f, 304f + 84f * i, 390f, 62f);
                Ui2Builder.Fill(Ui2Builder.Rect("fill", q, 390f, 62f), "ui_rounded_r64_fill", 22f,
                                Ui2Builder.WithAlpha(UiTheme.Deep, 0.6f));
                Ui2Builder.Fill(Ui2Builder.Rect("edge", q, 390f, 62f), "ui_rounded_r64_ring", 22f,
                                new Color(1f, 1f, 1f, 0.3f));
                var wash = Ui2Builder.Fill(Ui2Builder.Rect("wash", q, 390f, 62f), "ui_rounded_r64_fill", 22f,
                                           Color.clear);
                Ui2Kit.Glyph(q, "v2_icon_chat", 21f, 17f, 28f, UiTheme.Accent);
                questionLabels[i] = Ui2Kit.Label(q, "label", 65f, 18f, 310f, 27f, texts[i], 20f, UiTheme.Ink);
                q.gameObject.AddComponent<CanvasGroup>();
                questions[i] = Ui2Kit.Press(q, 390f, 62f, wash, null, null, null, true);
            }

            // While the guide answers, the two suggestions give way to what it says (same place, same plate).
            var transcript = Ui2Builder.Box("transcript", panel, 27f, 304f, 390f, 146f);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", transcript, 390f, 146f), "ui_rounded_r64_fill", 22f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.6f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", transcript, 390f, 146f), "ui_rounded_r64_ring", 22f,
                            new Color(1f, 1f, 1f, 0.3f));
            Ui2Kit.Glyph(transcript, "v2_icon_chat", 21f, 17f, 28f, UiTheme.Accent);
            var transcriptText = Ui2Kit.Label(transcript, "text", 65f, 16f, 305f, 116f, string.Empty, 18f, UiTheme.Ink,
                                              FontWeight.Regular, true);
            transcriptText.overflowMode = TextOverflowModes.Ellipsis;
            transcript.gameObject.SetActive(false);

            var primary = Ui2Kit.Primary(panel, "primary", 27f, 543f, 230f, 60f, "Start listening", "v2_icon_mic",
                                         "Enable microphone");
            var secondary = Ui2Kit.Secondary(panel, "secondary", 269f, 543f, 148f, 60f, "Dismiss", null,
                                             "Keep exploring");

            var guide = root.AddComponent<Ui2Guide>();
            var so = new SerializedObject(guide);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("phase").objectReferenceValue = phase;
            so.FindProperty("helper").objectReferenceValue = helper;
            so.FindProperty("wave").objectReferenceValue = wave;
            Ui2Kit.Objects(so.FindProperty("questions"), questions);
            Ui2Kit.Objects(so.FindProperty("questionLabels"), questionLabels);
            so.FindProperty("transcript").objectReferenceValue = transcript.gameObject;
            so.FindProperty("transcriptText").objectReferenceValue = transcriptText;
            so.FindProperty("primary").objectReferenceValue = primary;
            so.FindProperty("secondary").objectReferenceValue = secondary;
            so.FindProperty("micIcon").objectReferenceValue = Ui2Builder.Icon("v2_icon_mic");
            so.FindProperty("stopIcon").objectReferenceValue = Ui2Builder.Icon("v2_icon_stop");
            so.FindProperty("retryIcon").objectReferenceValue = Ui2Builder.Icon("v2_icon_retry");
            so.FindProperty("closeIcon").objectReferenceValue = Ui2Builder.Icon("v2_icon_close");
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        // ---------- Help. Figma: Help/LessonPanel at 898, 85, 567 x 742; Help index 567 x 673.

        internal static void BuildHelp(RectTransform frame, GameObject root)
        {
            var palm = Ui2Kit.Photo("lesson_palm");
            var pinch = Ui2Kit.Photo("lesson_pinch");
            var hands = Ui2Kit.Photo("lesson_two_hands");

            var lesson = Ui2Kit.Panel(frame, "Lesson", 898f, 85f, 567f, 742f, 26f);
            Ui2Builder.Pin(lesson, Ui2Builder.TopRight);
            var lessonTitle = Ui2Kit.Label(lesson, "title", 37f, 35f, 493f, 96f, "Make the menu come to you", 40f,
                                           UiTheme.Ink, FontWeight.SemiBold, true);
            var hero = Ui2Kit.Picture(lesson, "hero", 37f, 149f, 493f, 162f, 14f, palm);
            hero.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var heroText = Ui2Kit.Label(lesson, "text", 37f, 329f, 493f, 54f, string.Empty, 22f, UiTheme.Ink,
                                        FontWeight.Regular, true);
            Ui2Kit.Divider(lesson, 37f, 401f, 493f);
            var rowImages = new Image[2];
            var rowTitles = new TMP_Text[2];
            var rowTexts = new TMP_Text[2];
            for (var i = 0; i < 2; i++)
            {
                var top = 423f + 112f * i;
                rowImages[i] = Ui2Kit.Picture(lesson, "row_" + i, 37f, top, 160f, 92f, 12f, i == 0 ? pinch : hands);
                rowImages[i].GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                rowTitles[i] = Ui2Kit.Label(lesson, "row_title_" + i, 219f, top, 311f, 27f, "Lesson", 21f,
                                            UiTheme.Ink, FontWeight.SemiBold);
                rowTexts[i] = Ui2Kit.Label(lesson, "row_text_" + i, 219f, top + 31f, 311f, 66f, string.Empty, 18f,
                                           UiTheme.InkSecondary, FontWeight.Regular, true);
            }

            var tryIt = Ui2Kit.Primary(lesson, "try", 37f, 651f, 239f, 60f, "Try it", "v2_icon_play");
            var skip = Ui2Kit.Secondary(lesson, "skip", 288f, 651f, 242f, 60f, "Skip", null);

            var index = Ui2Kit.Panel(frame, "Help", 898f, 85f, 567f, 673f, 26f);
            Ui2Builder.Pin(index, Ui2Builder.TopRight);
            TextMeshProUGUI note, value;
            var indexBack = Ui2Kit.IconButton(index, "back", 33f, 40f, 40f, "v2_icon_back", 22f, false);
            Ui2Kit.Label(index, "title", 81f, 37f, 300f, 46f, "Help", 40f, UiTheme.Ink, FontWeight.Medium);
            var indexClose = Ui2Kit.IconButton(index, "close", 480f, 35f, 50f, "v2_icon_close", 24f);
            Ui2Kit.Label(index, "replay", 37f, 99f, 493f, 22f, "Replay a lesson", 17f, UiTheme.InkSecondary);
            var names = new[] { "Make the menu come to you", "Pinch to grab", "Two hands to change size" };
            var notes = new[] { "Open palm toward you", "Finger and thumb together", "Pinch with both hands" };
            var replay = new UiButton[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                replay[i] = Ui2Kit.Row(index, "lesson_" + i, 37f, 135f + 81f * i, 493f, 67f, names[i], notes[i],
                                       "v2_icon_help", true, out note, out value);
            }

            Ui2Kit.Divider(index, 37f, 378f, 493f);
            Ui2Kit.Label(index, "other", 37f, 393f, 493f, 22f, "Controller and desktop", 17f, UiTheme.InkSecondary);
            Ui2Kit.Label(index, "other_text", 37f, 429f, 493f, 108f,
                         "Controller: point at something and press the trigger. Desktop: click to select, and press " +
                         "Esc to close a panel.", 20f, UiTheme.Ink, FontWeight.Regular, true).lineSpacing = 8f;
            var back = Ui2Kit.Secondary(index, "back_button", 37f, 582f, 133f, 60f, "Back", null);

            var help = root.AddComponent<Ui2Help>();
            var so = new SerializedObject(help);
            so.FindProperty("lesson").objectReferenceValue = lesson.gameObject;
            so.FindProperty("lessonTitle").objectReferenceValue = lessonTitle;
            so.FindProperty("hero").objectReferenceValue = hero;
            so.FindProperty("heroText").objectReferenceValue = heroText;
            Ui2Kit.Objects(so.FindProperty("rowImages"), rowImages);
            Ui2Kit.Objects(so.FindProperty("rowTitles"), rowTitles);
            Ui2Kit.Objects(so.FindProperty("rowTexts"), rowTexts);
            so.FindProperty("tryIt").objectReferenceValue = tryIt;
            so.FindProperty("skip").objectReferenceValue = skip;
            Ui2Kit.Objects(so.FindProperty("pictures"), new Object[] { palm, pinch, hands });
            so.FindProperty("index").objectReferenceValue = index.gameObject;
            Ui2Kit.Objects(so.FindProperty("replay"), replay);
            so.FindProperty("indexClose").objectReferenceValue = indexClose;
            so.FindProperty("indexBack").objectReferenceValue = indexBack;
            so.FindProperty("indexBackButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            lesson.gameObject.SetActive(false);
            index.gameObject.SetActive(false);
        }

        // ---------- Notice. Figma: Feedback/Notice at 463, 36, 560 x 58.

        internal static void BuildNotice(RectTransform frame, out GameObject notice, out TMP_Text text)
        {
            var bar = Ui2Builder.Box("Notice", frame, 463f, 36f, 560f, 58f);
            Ui2Builder.Pin(bar, Ui2Builder.Top);
            Ui2Builder.Fill(Ui2Builder.Rect("fill", bar, 560f, 58f), "ui_rounded_r64_fill", 18f,
                            Ui2Builder.WithAlpha(UiTheme.Deep, 0.92f));
            Ui2Builder.Fill(Ui2Builder.Rect("edge", bar, 560f, 58f), "ui_rounded_r64_ring", 18f,
                            new Color(1f, 1f, 1f, 0.18f));
            Ui2Kit.Glyph(bar, "v2_icon_info", 22f, 17f, 24f, UiTheme.Accent);
            text = Ui2Kit.Label(bar, "message", 61f, 18f, 478f, 22f, string.Empty, 18f, UiTheme.Ink);
            notice = bar.gameObject;
        }
    }
}
