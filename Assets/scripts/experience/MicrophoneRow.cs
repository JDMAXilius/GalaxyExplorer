// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation.Being;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The microphone row of the settings window: ‹ device ›, a level bar and one line of help (owner's
    /// direction, 16 Sep). The arrows cycle through "System default" and every device Unity can see, and the
    /// choice is <see cref="BeingMic.Device"/>, so the being records from exactly what is shown here.
    ///
    /// <para><b>Why arrows and not a list.</b> A drop-down is a small target that opens more small targets, which
    /// is hard with a hand ray. Two 12 mm buttons are not, and most machines have two or three devices.</para>
    ///
    /// <para><b>The level bar.</b> While the window is open the chosen device is opened in a one-second loop and
    /// the bar shows how loud the last 50 ms were, so a player can see the right microphone is live before
    /// they ever talk to the being. The row lets go of the device whenever the being is recording
    /// (<see cref="BeingMic.InUse"/>) and when the window closes, because two readers of one microphone is not
    /// something every platform allows.</para>
    /// </summary>
    public class MicrophoneRow : MonoBehaviour
    {
        private const int Rate = 16000;
        private const string DefaultName = "System default";

        [SerializeField] private GEButton previousButton;
        [SerializeField] private GEButton nextButton;
        [SerializeField] private TMP_Text deviceLabel;

        [SerializeField]
        [Tooltip("The cyan bar. Its width is driven, in canvas units (millimetres on this canvas).")]
        private RectTransform levelFill;

        [SerializeField] private TMP_Text hintLabel;

        [SerializeField]
        [Tooltip("Width of the bar at full level, in millimetres.")]
        private float levelWidthMm = 110f;

        [SerializeField]
        [Tooltip("RMS is small for ordinary speech; this scales it onto the bar.")]
        private float levelGain = 8f;

        [SerializeField]
        [Tooltip("How fast the bar falls back, in bar-widths a second. It rises instantly.")]
        private float levelFallPerSecond = 2.5f;

        private readonly float[] _window = new float[Rate / 20];
        private AudioClip _clip;
        private string _open;
        private bool _live;
        private bool _wired;
        private bool _asked;

        public bool IsMetering => _live;
        public float Level { get; private set; }
        public string ShownDevice => deviceLabel != null ? deviceLabel.text : string.Empty;
        public string Hint => hintLabel != null ? hintLabel.text : string.Empty;

        /// <summary>Moves the choice one step through "System default" and the plugged-in devices.</summary>
        public void Step(int direction)
        {
            var devices = Microphone.devices;
            var index = -1;
            for (var i = 0; i < devices.Length; i++)
            {
                if (devices[i] == BeingMic.Device)
                {
                    index = i;
                }
            }

            var count = devices.Length + 1;
            var slot = ((index + 1 + direction) % count + count) % count;
            BeingMic.Device = slot == 0 ? string.Empty : devices[slot - 1];
            Release();
            ShowDevice();
        }

        private void Awake() => EnsureInit();

        private void EnsureInit()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;
            if (previousButton != null)
            {
                previousButton.OnClick.AddListener(() => Step(-1));
            }

            if (nextButton != null)
            {
                nextButton.OnClick.AddListener(() => Step(1));
            }
        }

        private void OnEnable()
        {
            EnsureInit();
            ShowDevice();
        }

        private void OnDisable()
        {
            // Asked at most once per opening of the window.
            _asked = false;
            Release();
            Level = 0f;
            DrawLevel();
        }

        private void Update()
        {
            if (BeingMic.InUse)
            {
                Release();
                Say("The guide is listening on this microphone.");
            }
            else if (!_live)
            {
                Acquire();
            }

            if (_live)
            {
                Measure();
            }
            else
            {
                Level = 0f;
            }

            DrawLevel();
        }

        private void Acquire()
        {
            // Permission first: Android may list no devices at all until it is granted.
            if (!BeingMic.Permitted)
            {
                // Asked once per window, not every frame: the system dialog is the player's to answer.
                if (!_asked)
                {
                    _asked = true;
#if UNITY_ANDROID && !UNITY_EDITOR
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
                }
                Say("Allow the microphone to check it.");
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Say("No microphone found.");
                return;
            }

            _open = BeingMic.ResolvedDevice;
            _clip = Microphone.Start(_open, true, 1, Rate);
            _live = _clip != null;
            Say(_live ? "Speak to check it. The bar moves when the mic hears you." : "This microphone could not be opened.");
        }

        private void Measure()
        {
            // GetData wraps round a looping clip, so reading the 50 ms just behind the write head is safe even
            // when that crosses the end of the buffer.
            var start = Microphone.GetPosition(_open) - _window.Length;
            if (start < 0)
            {
                start += _clip.samples;
            }

            _clip.GetData(_window, start);
            var sum = 0f;
            foreach (var s in _window)
            {
                sum += s * s;
            }

            var target = Mathf.Clamp01(Mathf.Sqrt(sum / _window.Length) * levelGain);
            Level = target > Level
                ? target
                : Mathf.MoveTowards(Level, target, levelFallPerSecond * Time.unscaledDeltaTime);
        }

        private void Release()
        {
            if (!_live)
            {
                return;
            }

            _live = false;

            // While the being records it owns the device, and ending it here would cut the being off.
            if (!BeingMic.InUse)
            {
                Microphone.End(_open);
            }

            _clip = null;
        }

        private void ShowDevice()
        {
            if (deviceLabel == null)
            {
                return;
            }

            var chosen = BeingMic.ResolvedDevice;
            deviceLabel.text = chosen ?? (string.IsNullOrEmpty(BeingMic.Device)
                ? DefaultName
                : $"{DefaultName} ({BeingMic.Device} is unplugged)");
        }

        private void Say(string text)
        {
            if (hintLabel != null && hintLabel.text != text)
            {
                hintLabel.text = text;
            }
        }

        private void DrawLevel()
        {
            if (levelFill != null)
            {
                levelFill.sizeDelta = new Vector2(levelWidthMm * Level, levelFill.sizeDelta.y);
            }
        }
    }
}
