// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using TMPro;
using UnityEngine;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The UI 2.0 settings panel (docs/design/v2, S23 to S27): sound, narration, labels, text size, microphone,
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
        [SerializeField] private UiButton labels = null;
        [SerializeField] private UiSwitch labelsSwitch = null;
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

        [SerializeField]
        [Tooltip("Asks the system for the microphone. Shown only while it has not been given and can still be asked for.")]
        private UiButton allow = null;

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
        private bool _requesting;
        private bool _paintedPermitted;
        private static Ui2Settings _instance;

        private void Start()
        {
            _instance = this;
            var shell = Ui2Shell.Instance;
            close.Clicked += Close;
            devicesClose.Clicked += Close;
            aboutClose.Clicked += Close;
            devicesBack.Clicked += () => Go(Page.Main);
            devicesDone.Clicked += () => Go(Page.Main);
            // Null until Build Shell Prefab is rerun after CS-373/CS-378; the page must not throw in between.
            if (allow != null)
            {
                allow.Clicked += () =>
                {
                    if (!_requesting)
                    {
                        StartCoroutine(AllowMicrophone());
                    }
                };
            }
            aboutBack.Clicked += () => Go(Page.Main);
            aboutDone.Clicked += () => Go(Page.Main);

            sound.Clicked += () => Mute.On = !Mute.On;
            narration.Clicked += () => VOManager.NarrationEnabled = !VOManager.NarrationEnabled;
            if (labels != null)
            {
                labels.Clicked += () => MoonLabel.ShowLabels = !MoonLabel.ShowLabels;
            }
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
                // The menu only: the palm Dock and the headset's recenter also send moved content home (CS-374).
                DockController.Instance?.ParkMenu();
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

        /// <summary>Opens Settings on its About page: the palm menu's About under UI 2.0 (CS-379).</summary>
        public static void ShowAbout()
        {
            if (Ui2Shell.Instance == null)
            {
                return;
            }

            // Opening Settings lands on its main page (OnShellChanged), so the About page is chosen after.
            Ui2Shell.Instance.Show(Ui2Shell.Overlay.Settings);
            if (_instance != null)
            {
                _instance.Go(Page.About);
            }
        }

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
                if (labelsSwitch != null)
                {
                    labelsSwitch.On = MoonLabel.ShowLabels;
                }
                for (var i = 0; i < sizes.Length; i++)
                {
                    sizes[i].Selected = Mathf.Approximately(InfoPanel.TextScale, sizeValues[i]);
                }

                var device = Cosmic.Companion.Mic.Resolve();
                microphoneValue.overflowMode = TextOverflowModes.Ellipsis;
                microphoneValue.text = !Cosmic.Companion.Mic.Permitted ? "Access off"
                    : Cosmic.Companion.Mic.Devices.Length == 0 ? "None found" : Friendly(device) ?? "Default";
            }
            else if (_page == Page.Devices)
            {
                // Access can also arrive from the headset's own settings while this page is up.
                if (Cosmic.Companion.Mic.Permitted != _paintedPermitted)
                {
                    PaintDevices();
                }

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

                // The first row is Automatic: the headset's own mic, or the system default (CS-371); the rest are the
                // devices by name. A chosen device that is unplugged falls back to Automatic.
                _shown[i] = i == 0 ? null : found[i - 1];
                var automatic = chosen != Cosmic.Companion.Mic.Device;
                var current = i == 0 ? automatic : !automatic && _shown[i] == chosen;
                deviceRows[i].SetLabel(i == 0 ? "Automatic" : found[i - 1]);
                deviceRows[i].Selected = current;
                deviceNotes[i].overflowMode = TextOverflowModes.Ellipsis;
                deviceNotes[i].text = !current ? "Available"
                    : i == 0 ? "Using " + (Friendly(chosen) ?? "the system default") : "Current device";
            }

            _paintedPermitted = Cosmic.Companion.Mic.Permitted;
            empty.text = !_paintedPermitted
                ? Cosmic.Companion.Mic.Blocked
                    ? "Microphone access is off. Turn it on in the headset's settings for this app to talk to the guide."
                    : "Microphone access has not been given yet. Choose Allow microphone to talk to the guide."
                : found.Length == 0 ? "No microphone found. Connect one or check microphone access." : string.Empty;

            // After "don't ask again" the system shows nothing, so the button would do nothing (CS-371).
            if (allow != null)
            {
                allow.gameObject.SetActive(!_paintedPermitted && !Cosmic.Companion.Mic.Blocked);
            }
            StopMeter();
        }

        private System.Collections.IEnumerator AllowMicrophone()
        {
            _requesting = true;
            yield return Cosmic.Companion.Mic.Request();
            _requesting = false;
            if (_page == Page.Devices && panel.activeSelf)
            {
                PaintDevices();
            }
        }

        // The headset's mic by what it is, not by its driver name (the Link one does not fit a row).
        private static string Friendly(string device) =>
            device == null ? null
            : device == "Android audio input" ? "the headset microphone"
            : device.IndexOf("Oculus Virtual Audio Device", StringComparison.OrdinalIgnoreCase) >= 0 ? "the headset microphone (Link)"
            : device;

        private void ChooseDevice(int index)
        {
            if (index >= _shown.Length)
            {
                return;
            }

            Cosmic.Companion.Mic.Device = _shown[index] ?? string.Empty;
            PaintDevices();
        }

        // The meter's capture dies with a pause on Android; the next frame on the page starts a fresh one.
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                StopMeter();
            }
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
            // Not while the guide holds the mic: it already took the device over, and End would stop its capture too.
            if (_meter != null && !Cosmic.Companion.Mic.InUse)
            {
                Microphone.End(_meterDevice);
            }

            _meter = null;
        }
    }
}
