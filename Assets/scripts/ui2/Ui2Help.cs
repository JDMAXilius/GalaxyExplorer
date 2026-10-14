// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// Help and first use in UI 2.0 (docs/design/v2, S29 to S32): three short lessons (palm, pinch, two hands)
    /// that run once in the headset and can be replayed from the Help index. "Try it" steps aside and waits
    /// for the player to do the thing; Skip moves on.
    /// </summary>
    public class Ui2Help : MonoBehaviour
    {
        private const string SeenKey = "CosmicSimulation.Ui2LessonsSeen";
        private const float TrySeconds = 30f;

        private static readonly string[] Titles =
        {
            "Make the menu come to <color=#37E1FD>you</color>",
            "Pinch to <color=#37E1FD>grab</color>",
            "Two hands to change <color=#37E1FD>size</color>",
        };

        private static readonly string[] Names =
        {
            "Make the menu come to you", "Pinch to grab", "Two hands to change size",
        };

        private static readonly string[] Texts =
        {
            "Turn an open palm toward you to show <color=#37E1FD>Explore.</color>",
            "Bring your finger and thumb together and pull a planet toward you.",
            "Pinch with both hands and move them apart or together.",
        };

        [Header("Lesson")]
        [SerializeField] private GameObject lesson = null;
        [SerializeField] private TMP_Text lessonTitle = null;
        [SerializeField] private Image hero = null;
        [SerializeField] private TMP_Text heroText = null;
        [SerializeField] private Image[] rowImages = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] rowTitles = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] rowTexts = Array.Empty<TMP_Text>();
        [SerializeField] private UiButton tryIt = null;
        [SerializeField] private UiButton skip = null;
        [SerializeField] private Sprite[] pictures = Array.Empty<Sprite>();

        [Header("Index")]
        [SerializeField] private GameObject index = null;
        [SerializeField] private UiButton[] replay = Array.Empty<UiButton>();
        [SerializeField] private UiButton indexBack = null;
        [SerializeField] private UiButton indexBackButton = null;
        [SerializeField] private UiButton indexClose = null;

        // -1 is the index page; 0 to 2 is a lesson.
        private int _lesson = -1;
        private int _waiting = -1;
        private float _waitUntil;
        private bool _moved;
        private bool _single;
        private Vector3 _scaleAtStart;

        public static bool Seen
        {
            get => PlayerPrefs.GetInt(SeenKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SeenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private IEnumerator Start()
        {
            tryIt.Clicked += Try;
            skip.Clicked += Advance;
            indexBack.Clicked += () => Ui2Shell.Instance.Show(Ui2Shell.Overlay.Settings);
            indexBackButton.Clicked += () => Ui2Shell.Instance.Show(Ui2Shell.Overlay.Settings);
            indexClose.Clicked += () => Ui2Shell.Instance.Show(Ui2Shell.Overlay.None);
            for (var i = 0; i < replay.Length; i++)
            {
                var which = i;
                replay[i].Clicked += () =>
                {
                    _single = true;
                    _lesson = which;
                    Paint();
                };
            }

            DockController.Moved += OnMenuMoved;
            Ui2Shell.Instance.Changed += Paint;
            Paint();

            // First use: once a place is open, in the headset only. The lessons are about hands.
            if (Seen)
            {
                yield break;
            }

            while (ExperienceDirector.Instance == null || ExperienceDirector.Instance.Current == null ||
                   ExperienceDirector.Instance.IsSwitching)
            {
                yield return null;
            }

            yield return new WaitForSeconds(2f);
            if (!GalaxyExplorerManager.IsDesktop && Ui2Shell.Instance.Open == Ui2Shell.Overlay.None)
            {
                _single = false;
                _lesson = 0;
                Ui2Shell.Instance.Show(Ui2Shell.Overlay.Help);
            }
        }

        private void OnDestroy()
        {
            DockController.Moved -= OnMenuMoved;
            if (Ui2Shell.Instance != null)
            {
                Ui2Shell.Instance.Changed -= Paint;
            }
        }

        private void OnMenuMoved() => _moved = true;

        private void Paint()
        {
            var open = Ui2Shell.Instance.Open == Ui2Shell.Overlay.Help;
            if (!open && _waiting < 0)
            {
                // Closed from outside (Esc, the gear): the next Help opens on the index.
                _lesson = -1;
            }

            lesson.SetActive(open && _lesson >= 0);
            index.SetActive(open && _lesson < 0);
            if (!open || _lesson < 0)
            {
                return;
            }

            lessonTitle.text = Titles[_lesson];
            hero.sprite = pictures[_lesson];
            hero.GetComponent<AspectRatioFitter>().aspectRatio = pictures[_lesson].rect.width / pictures[_lesson].rect.height;
            heroText.text = Texts[_lesson];
            for (var i = 0; i < rowImages.Length; i++)
            {
                // The two rows are the other two lessons, in order.
                var other = (_lesson + 1 + i) % Names.Length;
                rowImages[i].sprite = pictures[other];
                rowImages[i].GetComponent<AspectRatioFitter>().aspectRatio =
                    pictures[other].rect.width / pictures[other].rect.height;
                rowTitles[i].text = Names[other];
                rowTexts[i].text = Texts[other].Replace("<color=#37E1FD>", string.Empty).Replace("</color>", string.Empty);
            }
        }

        private void Try()
        {
            _waiting = _lesson;
            _waitUntil = Time.unscaledTime + TrySeconds;
            _moved = false;
            var held = Ui2Explore.Instance != null ? Ui2Explore.Instance.SelectedRoot : null;
            _scaleAtStart = held != null ? held.localScale : Vector3.zero;
            Ui2Shell.Instance.Show(Ui2Shell.Overlay.None);
        }

        private void Update()
        {
            if (_waiting < 0)
            {
                return;
            }

            var explore = Ui2Explore.Instance;
            var held = explore != null ? explore.SelectedRoot : null;
            var done = false;
            switch (_waiting)
            {
                case 0:
                    done = _moved;
                    break;
                case 1:
                    done = explore != null && explore.Selected != null;
                    break;
                case 2:
                    if (held != null && _scaleAtStart == Vector3.zero)
                    {
                        _scaleAtStart = held.localScale;
                    }

                    done = held != null && Mathf.Abs(held.localScale.x / Mathf.Max(0.0001f, _scaleAtStart.x) - 1f) > 0.08f;
                    break;
            }

            if (done)
            {
                Ui2Shell.Instance.Notify("Done: " + Names[_waiting].ToLowerInvariant() + ".", 2.5f);
            }

            if (done || Time.unscaledTime > _waitUntil)
            {
                _waiting = -1;
                Advance();
            }
        }

        /// <summary>The next lesson, or out: after the last one, or after a single lesson replayed from the index.</summary>
        private void Advance()
        {
            _waiting = -1;
            if (_single || _lesson >= Names.Length - 1)
            {
                var replayed = _single;
                _single = false;
                _lesson = -1;
                Seen = true;
                Ui2Shell.Instance.Show(replayed ? Ui2Shell.Overlay.Help : Ui2Shell.Overlay.None);
                return;
            }

            _lesson++;
            Ui2Shell.Instance.Show(Ui2Shell.Overlay.Help);
            Paint();
        }
    }
}
