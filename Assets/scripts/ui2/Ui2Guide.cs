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
        [SerializeField] private UiButton[] questions = Array.Empty<UiButton>();
        [SerializeField] private string[] questionTexts = Array.Empty<string>();
        [SerializeField] private UiButton primary = null;
        [SerializeField] private UiButton secondary = null;
        [SerializeField] private Sprite micIcon = null;
        [SerializeField] private Sprite stopIcon = null;
        [SerializeField] private Sprite retryIcon = null;
        [SerializeField] private Sprite closeIcon = null;

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
                var text = questionTexts[i];
                questions[i].Clicked += () => Being.Host.Current?.Ask(text);
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
                Summon();
            }
            else if (!guide && _wasGuide && Being.Host.Current != null)
            {
                Being.Host.Current.Dismiss();
            }

            _wasGuide = guide;
        }

        private static void Summon()
        {
            if (Being.Host.Current == null && Companion.Mic.Permitted && Microphone.devices.Length > 0)
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

            if (Microphone.devices.Length == 0)
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

            var status = Read();
            if (status != _status)
            {
                _status = status;
                Paint();
            }

            // The bars breathe with the microphone while listening and rest otherwise.
            var being = Being.Host.Current;
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

            var canAsk = !trouble && _status != Status.Connecting && Being.Host.Current != null;
            foreach (var question in questions)
            {
                question.gameObject.SetActive(!trouble);
                question.Interactable = canAsk;
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
