// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using TMPro;
using UnityEngine;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 settings panel (docs/design/v2, S23 to S27): sound, narration, text size, microphone,
    /// Help, About, Bring menu here, Restore scene and Quit. It writes the same preferences the legacy
    /// utility window wrote, so the two never disagree.
    /// </summary>
    public class Ui2Settings : MonoBehaviour
    {
        private enum Page { Main, Devices, About }

        [SerializeField] private GameObject panel = null;
        [SerializeField] private GameObject main = null;
        [SerializeField] private GameObject devices = null;
        [SerializeField] private GameObject about = null;

        [Header("Main")]
        [SerializeField] private UiButton close = null;
        [SerializeField] private UiButton sound = null;
        [SerializeField] private UiSwitch soundSwitch = null;
        [SerializeField] private UiButton narration = null;
        [SerializeField] private UiSwitch narrationSwitch = null;
        [SerializeField] private UiButton[] sizes = Array.Empty<UiButton>();
        [SerializeField] private float[] sizeValues = { 1f, 1.25f, 1.5f };
        [SerializeField] private UiButton microphone = null;
        [SerializeField] private TMP_Text microphoneValue = null;
        [SerializeField] private UiButton help = null;
        [SerializeField] private UiButton aboutRow = null;
        [SerializeField] private UiButton bring = null;
        [SerializeField] private UiButton restore = null;
        [SerializeField] private UiButton quit = null;

        [Header("Devices")]
        [SerializeField] private UiButton devicesBack = null;
        [SerializeField] private UiButton devicesClose = null;
        [SerializeField] private UiButton[] deviceRows = Array.Empty<UiButton>();
        [SerializeField] private TMP_Text[] deviceNotes = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform level = null;
        [SerializeField] private float levelWidth = 431f;
        [SerializeField] private TMP_Text empty = null;
        [SerializeField] private UiButton devicesDone = null;

        [Header("About")]
        [SerializeField] private UiButton aboutBack = null;
        [SerializeField] private UiButton aboutClose = null;
        [SerializeField] private TMP_Text version = null;
        [SerializeField] private UiButton source = null;
        [SerializeField] private UiButton aboutDone = null;
        [SerializeField] private string sourceUrl = "https://github.com/JDMAXilius/GalaxyExplorer";

        [Header("Confirmation")]
        [SerializeField] private GameObject confirm = null;
        [SerializeField] private TMP_Text confirmTitle = null;
        [SerializeField] private TMP_Text confirmBody = null;
        [SerializeField] private UiButton confirmYes = null;
        [SerializeField] private UiButton confirmNo = null;

        private Page _page;
        private Action _confirmed;
        private bool _quit;
        private string[] _shown = Array.Empty<string>();
        private AudioClip _meter;
        private string _meterDevice;
        private readonly float[] _window = new float[512];

        private void Start()
        {
            var shell = Ui2Shell.Instance;
            close.Clicked += Close;
            devicesClose.Clicked += Close;
            aboutClose.Clicked += Close;
            devicesBack.Clicked += () => Go(Page.Main);
            devicesDone.Clicked += () => Go(Page.Main);
            aboutBack.Clicked += () => Go(Page.Main);
            aboutDone.Clicked += () => Go(Page.Main);

            sound.Clicked += () => Mute.On = !Mute.On;
            narration.Clicked += () => VOManager.NarrationEnabled = !VOManager.NarrationEnabled;
            for (var i = 0; i < sizes.Length; i++)
            {
                var value = sizeValues[i];
                sizes[i].Clicked += () => InfoPanel.TextScale = value;
            }

            microphone.Clicked += () => Go(Page.Devices);
            help.Clicked += () => shell.Show(Ui2Shell.Overlay.Help);
            aboutRow.Clicked += () => Go(Page.About);
            bring.Clicked += () =>
            {
                DockController.Instance?.BringToPlayer();
                shell.Notify("Menu moved in front of you.");
            };
            restore.Clicked += () => Ask("Put everything back?",
                                         "Objects in this place will return to their starting positions.",
                                         "Restore", "Cancel", RestoreScene);
            quit.Clicked += () => Ask("Quit Cosmic Simulation XR?", string.Empty, "Quit", "Keep exploring",
                                      () => _quit = true);
            confirmNo.Clicked += () => confirm.SetActive(false);
            confirmYes.Clicked += () =>
            {
                confirm.SetActive(false);
                _confirmed?.Invoke();
            };
            source.Clicked += () => Application.OpenURL(sourceUrl);
            for (var i = 0; i < deviceRows.Length; i++)
            {
                var index = i;
                deviceRows[i].Clicked += () => ChooseDevice(index);
            }

            version.text = "Version " + Application.version;
            shell.Changed += OnShellChanged;
            OnShellChanged();
        }

        private void OnDestroy()
        {
            if (Ui2Shell.Instance != null)
            {
                Ui2Shell.Instance.Changed -= OnShellChanged;
            }

            StopMeter();
        }

        private static void Close() => Ui2Shell.Instance.Show(Ui2Shell.Overlay.None);

        private void OnShellChanged()
        {
            var open = Ui2Shell.Instance.Open == Ui2Shell.Overlay.Settings;
            if (open == panel.activeSelf)
            {
                return;
            }

            panel.SetActive(open);
            AudioService.Instance?.PlayClip(open ? AudioId.ToolboxShow : AudioId.ToolBoxHide);
            if (open)
            {
                Go(Page.Main);
            }
            else
            {
                StopMeter();
            }
        }

        private void Go(Page page)
        {
            _page = page;
            main.SetActive(page == Page.Main);
            devices.SetActive(page == Page.Devices);
            about.SetActive(page == Page.About);
            confirm.SetActive(false);
            if (page == Page.Devices)
            {
                PaintDevices();
            }
            else
            {
                StopMeter();
            }
        }

        private void Ask(string heading, string detail, string yes, string no, Action confirmed)
        {
            confirmTitle.text = heading;
            confirmBody.text = detail;
            confirmYes.SetLabel(yes);
            confirmNo.SetLabel(no);
            _confirmed = confirmed;
            confirm.SetActive(true);
        }

        /// <summary>Puts this place's content back and nothing else: the menu stays where it is.</summary>
        private static void RestoreScene()
        {
            DockController.Instance?.ResetPlaces();
            // A comparison holds two bodies side by side on purpose; walking them home would end it under the player.
            Arrangement.Restore(bodies: !(Ui2Explore.Instance != null && Ui2Explore.Instance.Comparing));

            Ui2Shell.Instance.Notify("Everything is back in place.");
        }

        private void Update()
        {
            if (_quit)
            {
                // A frame later than the press: GEButton starts a coroutine after its listeners have run.
                _quit = false;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            if (!panel.activeSelf)
            {
                return;
            }

            if (_page == Page.Main)
            {
                soundSwitch.On = !Mute.On;
                narrationSwitch.On = VOManager.NarrationEnabled;
                for (var i = 0; i < sizes.Length; i++)
                {
                    sizes[i].Selected = Mathf.Approximately(InfoPanel.TextScale, sizeValues[i]);
                }

                var device = Cosmic.Companion.Mic.Resolve();
                microphoneValue.text = Cosmic.Companion.Mic.Devices.Length == 0 ? "None found" : device ?? "Default";
            }
            else if (_page == Page.Devices)
            {
                Meter();
            }
        }

        // ---------- microphone

        private void PaintDevices()
        {
            var found = Cosmic.Companion.Mic.Devices;
            var chosen = Cosmic.Companion.Mic.Resolve();
            _shown = new string[Mathf.Min(deviceRows.Length, found.Length + 1)];
            for (var i = 0; i < deviceRows.Length; i++)
            {
                var has = i < _shown.Length && found.Length > 0;
                deviceRows[i].gameObject.SetActive(has);
                if (!has)
                {
                    continue;
                }

                // The first row is "whatever the system uses"; the rest are the devices by name.
                _shown[i] = i == 0 ? null : found[i - 1];
                var current = _shown[i] == chosen;
                deviceRows[i].SetLabel(i == 0 ? "System default" : found[i - 1]);
                deviceRows[i].Selected = current;
                deviceNotes[i].text = current ? "Current device" : "Available";
            }

            empty.text = !Cosmic.Companion.Mic.Permitted
                ? "Microphone access is off. Allow it in the headset's settings to talk to the guide."
                : found.Length == 0 ? "No microphone found. Connect one or check microphone access." : string.Empty;
            StopMeter();
        }

        private void ChooseDevice(int index)
        {
            if (index >= _shown.Length)
            {
                return;
            }

            Cosmic.Companion.Mic.Device = _shown[index] ?? string.Empty;
            PaintDevices();
        }

        /// <summary>Moves the level bar. The guide owns the microphone while it is out, so then the bar rests.</summary>
        private void Meter()
        {
            var value = 0f;
            if (Cosmic.Companion.Mic.InUse || !Cosmic.Companion.Mic.Permitted || Cosmic.Companion.Mic.Devices.Length == 0)
            {
                StopMeter();
            }
            else
            {
                if (_meter == null)
                {
                    _meterDevice = Cosmic.Companion.Mic.Resolve();
                    _meter = Microphone.Start(_meterDevice, true, 1, 16000);
                }

                // The clip loops, so the window just behind the write head may start before 0: wrap it (CS-345).
                var at = _meter != null ? Microphone.GetPosition(_meterDevice) - _window.Length : 0;
                if (_meter != null && at < 0)
                {
                    at += _meter.samples;
                }

                if (_meter != null && at >= 0)
                {
                    _meter.GetData(_window, at);
                    var sum = 0f;
                    foreach (var sample in _window)
                    {
                        sum += sample * sample;
                    }

                    value = Mathf.Clamp01(Mathf.Sqrt(sum / _window.Length) * 6f);
                }
            }

            var width = Mathf.Lerp(level.sizeDelta.x, levelWidth * value, 0.3f);
            level.sizeDelta = new Vector2(width, level.sizeDelta.y);
        }

        private void StopMeter()
        {
            if (_meter != null)
            {
                Microphone.End(_meterDevice);
                _meter = null;
            }
        }
    }
}
