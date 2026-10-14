// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Companion = Cosmic.Companion;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 guide panel (docs/design/v2, S15 to S22): says what the companion is doing in words, keeps the
    /// conversation on screen, offers suggestions made from what the player has in front of them, and explains a
    /// missing microphone or a failed connection. Opening the dock's Guide brings the companion out; leaving it
    /// sends the companion away.
    /// </summary>
    public class Ui2Guide : MonoBehaviour
    {
        /// <summary>What the panel is showing. Derived every frame from the companion, never stored by it.</summary>
        public enum Status { Idle, Connecting, Listening, Thinking, Speaking, Unavailable, PermissionNeeded, NoMicrophone }

        [SerializeField] private GameObject panel = null;
        [SerializeField] private TMP_Text phase = null;
        [SerializeField] private TMP_Text helper = null;
        [SerializeField] private Image wave = null;
        [SerializeField]
        [Tooltip("Suggestion bubbles, filled by GuideSuggestions from the situation.")]
        private UiButton[] questions = Array.Empty<UiButton>();

        [SerializeField] private TMP_Text[] questionLabels = Array.Empty<TMP_Text>();

        [Header("Conversation")]
        [SerializeField] private GameObject transcript = null;
        [SerializeField] private TMP_Text transcriptText = null;
        [SerializeField] private UiButton primary = null;
        [SerializeField] private UiButton secondary = null;
        [SerializeField] private Sprite micIcon = null;
        [SerializeField] private Sprite stopIcon = null;
        [SerializeField] private Sprite retryIcon = null;
        [SerializeField] private Sprite closeIcon = null;

        // The conversation, kept while the app runs so reopening the guide shows it again. Answers stay until
        // newer lines push them up and out of the box.
        private static readonly List<string> Lines = new List<string>();
        private const int MaxLines = 12;
        private const string Dim = "<color=#AAB6C2>";

        private string[] _picked = Array.Empty<string>();
        private string _saidBefore = string.Empty;
        private string _heardBefore = string.Empty;
        private int _turnStart = -1;
        private string _context;
        private float _nextContextCheck;
        private Status _status = (Status)(-1);
        private bool _wasGuide;
        private bool _requesting;

        public Status Current => _status;

        /// <summary>The suggestions on screen now, for a harness.</summary>
        public IReadOnlyList<string> Suggestions => _picked;

        /// <summary>The conversation as shown, for a harness.</summary>
        public IReadOnlyList<string> Conversation => Lines;

        private void Start()
        {
            primary.Clicked += OnPrimary;
            secondary.Clicked += OnSecondary;
            for (var i = 0; i < questions.Length; i++)
            {
                var slot = i;
                questions[i].Clicked += () => AskSuggestion(slot);
            }

            Ui2Shell.Instance.Changed += OnShellChanged;
            OnShellChanged();
        }

        private void OnDestroy()
        {
            if (Ui2Shell.Instance != null)
            {
                Ui2Shell.Instance.Changed -= OnShellChanged;
            }
        }

        private void OnShellChanged()
        {
            var shell = Ui2Shell.Instance;
            var guide = shell.Current == Ui2Shell.Mode.Guide;
            panel.SetActive(guide && shell.Open == Ui2Shell.Overlay.None);

            if (guide && !_wasGuide)
            {
                // A new companion starts a new session: its first words are new, whatever the last one said.
                _saidBefore = string.Empty;
                _heardBefore = string.Empty;
                Pick();
                Summon();
            }
            else if (!guide && _wasGuide && Being.Host.Current != null)
            {
                Being.Host.Current.Dismiss();
            }

            _wasGuide = guide;
        }

        /// <summary>Fresh suggestions for what is in front of the player now, avoiding the ones just shown.</summary>
        private void Pick()
        {
            var s = Being.Host.Read();
            var explore = Ui2Explore.Instance;
            var selected = explore != null ? explore.Selected : null;
            var selectedOut = selected != null && s.held != null && Array.IndexOf(s.held, selected.Id) >= 0;
            var hasSound = explore != null && explore.SelectionSound != null;
            var lines = GuideSuggestions.For(s, selected, selectedOut, hasSound, questions.Length);

            _picked = new string[questions.Length];
            for (var i = 0; i < questions.Length; i++)
            {
                _picked[i] = i < lines.Count ? lines[i] : null;
                if (i < questionLabels.Length)
                {
                    questionLabels[i].text = _picked[i] ?? string.Empty;
                }
            }

            _context = Context(s, selected);
            PaintSuggestions();
        }

        // What the suggestions were made for: when it changes, they are made again.
        private static string Context(Companion.Situation s, BodyInfo selected) =>
            s.placeId + "|" + (selected != null ? selected.Id : "") + "|" + s.previewing + "|" +
            (s.held != null ? string.Join(",", s.held) : "");

        private void AskSuggestion(int slot)
        {
            var being = Being.Host.Current;
            var line = slot < _picked.Length ? _picked[slot] : null;
            if (being == null || string.IsNullOrEmpty(line))
            {
                return;
            }

            Add(Dim + "You: " + line + "</color>");
            _turnStart = -1;
            being.Ask(line);
        }

        private static void Add(string line)
        {
            Lines.Add(line);
            while (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
            }
        }

        private static void Summon()
        {
            if (Being.Host.Current == null && Companion.Mic.Permitted && Cosmic.Companion.Mic.Devices.Length > 0)
            {
                DockController.Instance?.ToggleBeing();
            }
        }

        private static Status Read()
        {
            if (!Companion.Mic.Permitted)
            {
                return Status.PermissionNeeded;
            }

            if (Cosmic.Companion.Mic.Devices.Length == 0)
            {
                return Status.NoMicrophone;
            }

            var being = Being.Host.Current;
            if (being == null)
            {
                return Status.Idle;
            }

            if (!being.Online)
            {
                // The companion sits in Thinking while it is still trying to connect.
                return being.Current == Companion.Being.Phase.Thinking ? Status.Connecting : Status.Unavailable;
            }

            switch (being.Current)
            {
                case Companion.Being.Phase.Listening: return Status.Listening;
                case Companion.Being.Phase.Thinking: return Status.Thinking;
                case Companion.Being.Phase.Speaking: return Status.Speaking;
                default: return Status.Idle;
            }
        }

        private void Update()
        {
            if (!panel.activeSelf)
            {
                return;
            }

            var being = Being.Host.Current;
            var status = Read();
            if (status != _status)
            {
                // Back to listening after an answer: fresh suggestions for the next question.
                if ((_status == Status.Speaking || _status == Status.Thinking) &&
                    (status == Status.Listening || status == Status.Idle))
                {
                    Pick();
                }

                // A spoken question's words arrive after it was asked: they go in front of its answer.
                if (status == Status.Thinking && _status != Status.Speaking)
                {
                    _turnStart = Lines.Count;
                }

                _status = status;
                Paint();
            }

            Capture(being);
            PaintTranscript();

            // A new selection, place or pulled body while the panel is up: suggestions for that.
            if (Time.unscaledTime >= _nextContextCheck && status != Status.Thinking && status != Status.Speaking)
            {
                _nextContextCheck = Time.unscaledTime + 0.5f;
                var explore = Ui2Explore.Instance;
                if (Context(Being.Host.Read(), explore != null ? explore.Selected : null) != _context)
                {
                    Pick();
                }
            }

            // The bars breathe with the microphone while listening and rest otherwise.
            var loud = status == Status.Listening && being != null ? Mathf.Clamp01(being.MicLevel * 8f) : 0f;
            var scale = Mathf.Lerp(wave.rectTransform.localScale.y, 1f + loud * 0.5f, 0.3f);
            wave.rectTransform.localScale = new Vector3(1f, scale, 1f);
        }

        // The companion's newest words into the conversation.
        private void Capture(Companion.Being being)
        {
            if (being == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(being.Said) && being.Said != _saidBefore)
            {
                _saidBefore = being.Said;
                Add(being.Said);
            }

            if (!string.IsNullOrEmpty(being.Heard) && being.Heard != _heardBefore)
            {
                _heardBefore = being.Heard;
                var line = Dim + "You: " + being.Heard + "</color>";
                if (_turnStart >= 0 && _turnStart <= Lines.Count)
                {
                    Lines.Insert(_turnStart, line);
                    while (Lines.Count > MaxLines)
                    {
                        Lines.RemoveAt(0);
                    }
                }
                else
                {
                    Add(line);
                }

                _turnStart = -1;
            }
        }

        private bool Trouble => _status == Status.Unavailable || _status == Status.PermissionNeeded ||
                                _status == Status.NoMicrophone;

        private void Paint()
        {
            var active = _status == Status.Listening || _status == Status.Speaking;
            phase.color = active ? UiTheme.Accent : UiTheme.Ink;
            wave.color = active ? UiTheme.Accent : Trouble ? UiTheme.InkSecondary : UiTheme.Ink;

            string heading, detail, action, other = "Dismiss";
            Sprite icon;
            switch (_status)
            {
                case Status.Connecting:
                    heading = "Connecting";
                    detail = "Connecting to your guide…";
                    action = "Cancel";
                    icon = closeIcon;
                    break;
                case Status.Listening:
                    heading = "Listening";
                    detail = "Ask about what you see.";
                    action = "Stop listening";
                    icon = stopIcon;
                    other = "Mic on";
                    break;
                case Status.Thinking:
                    heading = "Thinking";
                    detail = "Working on your answer.";
                    action = "Cancel";
                    icon = closeIcon;
                    break;
                case Status.Speaking:
                    heading = "Speaking";
                    detail = "Your guide is answering.";
                    action = "Stop speaking";
                    icon = stopIcon;
                    break;
                case Status.Unavailable:
                    heading = "Unavailable";
                    detail = "Your guide could not connect. Try again or keep exploring.";
                    action = "Try again";
                    icon = retryIcon;
                    other = "Keep exploring";
                    break;
                case Status.PermissionNeeded:
                    heading = "Microphone";
                    detail = "Use your microphone to ask the guide questions.";
                    action = "Enable microphone";
                    icon = micIcon;
                    other = "Not now";
                    break;
                case Status.NoMicrophone:
                    heading = "No microphone";
                    detail = "Choose another device or check access.";
                    action = "Choose device";
                    icon = micIcon;
                    other = "Keep exploring";
                    break;
                default:
                    heading = "Ready to help";
                    detail = "Ask about what you see.";
                    action = "Start listening";
                    icon = micIcon;
                    break;
            }

            phase.text = heading;
            helper.text = detail;
            primary.SetLabel(action);
            primary.SetIcon(icon);
            secondary.SetLabel(other);

            // "Mic on" is a statement, not a button: it shows that the microphone is live.
            secondary.Interactable = _status != Status.Listening;
            secondary.GetComponent<CanvasGroup>().alpha = 1f;

            // The conversation and the suggestions stay up through an answer; only a problem replaces them.
            transcript.SetActive(!Trouble);
            PaintSuggestions();
        }

        private void PaintSuggestions()
        {
            var canAsk = !Trouble && _status != Status.Connecting && Being.Host.Current != null;
            for (var i = 0; i < questions.Length; i++)
            {
                questions[i].gameObject.SetActive(!Trouble && i < _picked.Length && _picked[i] != null);
                questions[i].Interactable = canAsk;
            }
        }

        private void PaintTranscript()
        {
            if (!transcript.activeSelf)
            {
                return;
            }

            var waiting = _status == Status.Thinking;
            string text;
            if (Lines.Count == 0 && !waiting)
            {
                text = Dim + "Ask something, or pick a suggestion below.</color>";
            }
            else
            {
                text = string.Join("\n\n", Lines) + (waiting ? "\n\n" + Dim + "…</color>" : string.Empty);
            }

            if (transcriptText.text != text)
            {
                transcriptText.text = text;
            }
        }

        private void OnPrimary()
        {
            var being = Being.Host.Current;
            switch (_status)
            {
                case Status.PermissionNeeded:
                    if (!_requesting)
                    {
                        StartCoroutine(RequestMicrophone());
                    }

                    break;
                case Status.NoMicrophone:
                    Ui2Shell.Instance.Show(Ui2Shell.Overlay.Settings);
                    break;
                case Status.Connecting:
                    Ui2Shell.Instance.Set(Ui2Shell.Mode.Explore);
                    break;
                case Status.Unavailable:
                    // A fresh companion makes a fresh connection.
                    if (being != null)
                    {
                        being.Dismiss();
                    }

                    Invoke(nameof(SummonAgain), 1.5f);
                    break;
                default:
                    if (being == null)
                    {
                        Summon();
                    }
                    else
                    {
                        being.Tap();
                    }

                    break;
            }
        }

        private void SummonAgain()
        {
            if (Ui2Shell.Instance.Current == Ui2Shell.Mode.Guide)
            {
                Summon();
            }
        }

        private System.Collections.IEnumerator RequestMicrophone()
        {
            _requesting = true;
            yield return Companion.Mic.Request();
            _requesting = false;
            if (Companion.Mic.Permitted)
            {
                Summon();
            }
        }

        private static void OnSecondary() => Ui2Shell.Instance.Set(Ui2Shell.Mode.Explore);
    }
}
