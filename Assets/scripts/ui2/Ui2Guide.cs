// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Companion = Cosmic.Companion;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 guide panel (docs/design/v2, S15 to S22): says what the companion is doing in words, offers
    /// the one action that fits, and explains a missing microphone or a failed connection. Opening the dock's
    /// Guide brings the companion out; leaving it sends the companion away.
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
        [Tooltip("Two suggestion buttons: the first asks a question, the second gives a command.")]
        private UiButton[] questions = Array.Empty<UiButton>();

        [SerializeField] private TMP_Text[] questionLabels = Array.Empty<TMP_Text>();

        [Header("Transcript")]
        [SerializeField] private GameObject transcript = null;
        [SerializeField] private TMP_Text transcriptText = null;
        [SerializeField] private UiButton primary = null;
        [SerializeField] private UiButton secondary = null;
        [SerializeField] private Sprite micIcon = null;
        [SerializeField] private Sprite stopIcon = null;
        [SerializeField] private Sprite retryIcon = null;
        [SerializeField] private Sprite closeIcon = null;

        // What a player can ask. One is picked each time the guide comes out.
        private static readonly string[] Questions =
        {
            "Why does Saturn have rings?",
            "How big is the Milky Way?",
            "What is a nebula made of?",
            "How far away is Andromeda?",
            "What is at the centre of our galaxy?",
            "Why is Mars red?",
            "Could anything live on Europa?",
            "How old is the Sun?",
            "What is the Cosmic Web?",
            "Why does Uranus spin on its side?",
            "How do stars form?",
            "What is a planetary nebula?",
            "How long is a day on Venus?",
            "What will happen to the Sun?",
        };

        // What a player can tell it to do. Each line maps to one of the guide's actions (open_place, pull_body,
        // restore), so every suggestion is something it can really carry out.
        private static readonly string[] Commands =
        {
            "Take me to Orion.",
            "Take me to the Crab Nebula.",
            "Show me the Pillars of Creation.",
            "Take me to Andromeda.",
            "Show me the Whirlpool Galaxy.",
            "Open the Solar System.",
            "Show me the planets in a row.",
            "Take me to the Galactic Center.",
            "Show me the Cosmic Web.",
            "Take me to the Helix Nebula.",
            "Show me HD 110067.",
            "Put everything back.",
        };

        // Only offered when the planets stand in a row, where pulling one out is possible.
        private static readonly string[] RowCommands =
        {
            "Pull out Saturn.",
            "Show me Jupiter up close.",
            "Bring Earth over here.",
            "Pull out Mars.",
        };

        private readonly string[] _picked = new string[2];
        private string _saidBefore = string.Empty;
        private Status _status = (Status)(-1);
        private bool _wasGuide;
        private bool _requesting;

        public Status Current => _status;

        private void Start()
        {
            primary.Clicked += OnPrimary;
            secondary.Clicked += OnSecondary;
            for (var i = 0; i < questions.Length; i++)
            {
                var slot = i;
                questions[i].Clicked += () => Being.Host.Current?.Ask(_picked[slot]);
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
                Pick();
                Summon();
            }
            else if (!guide && _wasGuide && Being.Host.Current != null)
            {
                Being.Host.Current.Dismiss();
            }

            _wasGuide = guide;
        }

        /// <summary>A new question and a new command, different from the ones shown last time.</summary>
        private void Pick()
        {
            var director = ExperienceDirector.Instance;
            var inRow = director != null && director.Current != null && director.Current.Id == "solar_system_planets";
            _picked[0] = Another(Questions, _picked[0]);
            _picked[1] = inRow && UnityEngine.Random.value < 0.5f ? Another(RowCommands, _picked[1]) : Another(Commands, _picked[1]);
            for (var i = 0; i < questionLabels.Length && i < _picked.Length; i++)
            {
                questionLabels[i].text = _picked[i];
            }
        }

        private static string Another(string[] pool, string last)
        {
            var pick = pool[UnityEngine.Random.Range(0, pool.Length)];
            return pick == last && pool.Length > 1 ? pool[(Array.IndexOf(pool, pick) + 1) % pool.Length] : pick;
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

                if (status == Status.Thinking && _status != Status.Speaking)
                {
                    _saidBefore = being != null ? being.Said : string.Empty;
                }

                _status = status;
                Paint();
            }

            PaintTranscript();

            // The bars breathe with the microphone while listening and rest otherwise.
            var loud = status == Status.Listening && being != null ? Mathf.Clamp01(being.MicLevel * 8f) : 0f;
            var scale = Mathf.Lerp(wave.rectTransform.localScale.y, 1f + loud * 0.5f, 0.3f);
            wave.rectTransform.localScale = new Vector3(1f, scale, 1f);
        }

        private void Paint()
        {
            var active = _status == Status.Listening || _status == Status.Speaking;
            var trouble = _status == Status.Unavailable || _status == Status.PermissionNeeded ||
                          _status == Status.NoMicrophone;
            phase.color = active ? UiTheme.Accent : UiTheme.Ink;
            wave.color = active ? UiTheme.Accent : trouble ? UiTheme.InkSecondary : UiTheme.Ink;

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
                    detail = "Use your microphone to ask the guide questions. You can keep exploring without it.";
                    action = "Enable microphone";
                    icon = micIcon;
                    other = "Not now";
                    break;
                case Status.NoMicrophone:
                    heading = "No microphone";
                    detail = "Your microphone is unavailable. Choose another device or check microphone access.";
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

            // While the guide is working on an answer or giving it, the suggestions give way to what it says.
            var answering = _status == Status.Thinking || _status == Status.Speaking;
            var canAsk = !trouble && _status != Status.Connecting && Being.Host.Current != null;
            foreach (var question in questions)
            {
                question.gameObject.SetActive(!trouble && !answering);
                question.Interactable = canAsk;
            }

            transcript.SetActive(answering);
        }

        private void PaintTranscript()
        {
            if (!transcript.activeSelf)
            {
                return;
            }

            var being = Being.Host.Current;
            var heard = being != null ? being.Heard : string.Empty;
            var said = being != null && being.Said != _saidBefore ? being.Said : string.Empty;
            var text = (string.IsNullOrEmpty(heard) ? string.Empty : "<color=#AAB6C2>You: " + heard + "</color>\n") +
                       (string.IsNullOrEmpty(said) ? "<color=#AAB6C2>…</color>" : said);

            // The newest words matter most; a long answer shows its end.
            const int Room = 260;
            if (said.Length > Room)
            {
                text = "…" + said.Substring(said.Length - Room);
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
