using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation.Being
{
    /// <summary>
    /// Records one utterance: from the first window loud enough to be speech to a second of quiet after it.
    ///
    /// <para><b>Which microphone.</b> <see cref="Device"/> is the player's choice from the settings window,
    /// remembered between sessions. Empty means the system default, which is what a headset wants; a desktop
    /// often has several (webcam, headset, line in) and the default is not always the one being spoken into.
    /// A remembered device that has since been unplugged falls back to the default rather than failing.</para>
    /// </summary>
    public class BeingMic : MonoBehaviour
    {
        public const int Rate = 16000;
        private const string DevicePrefsKey = "CosmicSimulation.Microphone";

        public static event Action DeviceChanged;

        /// <summary>The chosen device name, or empty for the system default. Remembered between sessions.</summary>
        public static string Device
        {
            get => PlayerPrefs.GetString(DevicePrefsKey, string.Empty);
            set
            {
                value ??= string.Empty;
                if (value == Device)
                {
                    return;
                }
                PlayerPrefs.SetString(DevicePrefsKey, value);
                PlayerPrefs.Save();
                DeviceChanged?.Invoke();
            }
        }

        /// <summary>The device to open: the chosen one if it is still plugged in, otherwise null (the default).</summary>
        public static string ResolvedDevice
        {
            get
            {
                var wanted = Device;
                if (string.IsNullOrEmpty(wanted))
                {
                    return null;
                }
                foreach (var name in Microphone.devices)
                {
                    if (name == wanted)
                    {
                        return name;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// True while any being is recording. The settings window's level meter reads this and lets go of the
        /// device, because two readers of one microphone is not something every platform allows.
        /// </summary>
        public static bool InUse { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => InUse = false;

        /// <summary>Whether the app may open a microphone at all. Always true off Android.</summary>
        public static bool Permitted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return true;
#endif
            }
        }

        public bool IsRecording { get; private set; }

        /// <summary>What the last recording used, for logs.</summary>
        public string DeviceLabel { get; private set; } = "default";

        private bool _stop;
        private string _open;

        public void Record(BeingSettings settings, Action<byte[]> done)
        {
            if (!IsRecording)
            {
                StartCoroutine(Run(settings, done));
            }
        }

        public void Stop() => _stop = true;

        // A coroutine dies with its object without reaching Microphone.End, so dismissing the being mid-listen
        // used to leave the device open for the rest of the session.
        private void OnDisable() => Close();

        private void Close()
        {
            if (IsRecording)
            {
                Microphone.End(_open);
                IsRecording = false;
                InUse = false;
            }
        }

        private IEnumerator Run(BeingSettings settings, Action<byte[]> done)
        {
            IsRecording = true;
            InUse = true;
            _stop = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                // Wait for the player's answer rather than a guessed half second: the system dialog stays up for
                // as long as they take to read it.
                var answered = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                callbacks.PermissionRequestDismissed += _ => answered = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
                var waited = 0f;
                while (!answered && waited < 30f && !_stop)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            var allowed = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            var allowed = true;
#endif
            if (!allowed || Microphone.devices.Length == 0)
            {
                IsRecording = InUse = false;
                DeviceLabel = allowed ? "no microphone" : "permission denied";
                done(null);
                yield break;
            }

            _open = ResolvedDevice;
            DeviceLabel = _open ?? "default";
            // The settings meter may still hold this device for the frame before it sees InUse.
            if (Microphone.IsRecording(_open))
            {
                Microphone.End(_open);
            }

            var clip = Microphone.Start(_open, false, Mathf.CeilToInt(settings.MaxUtteranceSeconds), Rate);
            if (clip == null)
            {
                IsRecording = InUse = false;
                done(null);
                yield break;
            }

            var samples = new List<float>(Rate * 8);
            var window = new float[Rate / 10];
            var last = 0;
            var time = 0f;
            var quiet = 0f;
            var spoke = false;

            while (true)
            {
                yield return null;
                time += Time.deltaTime;
                var position = Microphone.GetPosition(_open);
                while (position - last >= window.Length)
                {
                    clip.GetData(window, last);
                    samples.AddRange(window);
                    last += window.Length;
                    if (Rms(window) > settings.SpeechThreshold)
                    {
                        spoke = true;
                        quiet = 0f;
                    }
                    else
                    {
                        quiet += (float)window.Length / Rate;
                    }
                }

                if (_stop || (spoke && quiet >= settings.SilenceSeconds) || time >= settings.MaxUtteranceSeconds ||
                    (!spoke && time >= settings.ListenTimeoutSeconds))
                {
                    break;
                }
            }

            Close();
            done(spoke ? Pcm16(samples) : null);
        }

        private static float Rms(float[] window)
        {
            var sum = 0f;
            foreach (var s in window)
            {
                sum += s * s;
            }
            return Mathf.Sqrt(sum / window.Length);
        }

        private static byte[] Pcm16(List<float> samples)
        {
            var bytes = new byte[samples.Count * 2];
            for (var i = 0; i < samples.Count; i++)
            {
                var v = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                bytes[i * 2] = (byte)v;
                bytes[i * 2 + 1] = (byte)(v >> 8);
            }
            return bytes;
        }
    }
}
