using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmic
{
    public class BeingMic : MonoBehaviour
    {
        public const int Rate = 16000;

        public static bool InUse { get; private set; }

        public bool Recording { get; private set; }
        public string DeviceLabel { get; private set; } = "default";

        bool stop;
        string open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InUse = false;

        public static string Resolve()
        {
            var wanted = Prefs.Microphone;
            if (string.IsNullOrEmpty(wanted)) return null;
            foreach (var name in Microphone.devices)
                if (name == wanted)
                    return name;
            return null;
        }

        public static bool Permitted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            return true;
#endif
        }

        public static IEnumerator Ask(Func<bool> cancelled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permitted()) yield break;
            var answered = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionRequestDismissed += _ => answered = true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
            var waited = 0f;
            while (!answered && waited < 30f && !cancelled())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
#else
            yield break;
#endif
        }

        public void Record(BeingSettings settings, Action<byte[]> done)
        {
            if (!Recording) StartCoroutine(Run(settings, done));
        }

        public void Stop() => stop = true;

        void OnDisable() => Close();

        void Close()
        {
            if (!Recording) return;
            Microphone.End(open);
            Recording = false;
            InUse = false;
        }

        IEnumerator Run(BeingSettings settings, Action<byte[]> done)
        {
            Recording = true;
            InUse = true;
            stop = false;
            yield return Ask(() => stop);
            if (!Permitted() || Microphone.devices.Length == 0)
            {
                DeviceLabel = Permitted() ? "no microphone" : "permission denied";
                Recording = InUse = false;
                done(null);
                yield break;
            }
            open = Resolve();
            DeviceLabel = open ?? "default";
            // The settings meter may hold the device this frame; it lets go as soon as it sees InUse.
            if (Microphone.IsRecording(open)) Microphone.End(open);
            var clip = Microphone.Start(open, false, Mathf.CeilToInt(settings.maxUtteranceSeconds), Rate);
            if (clip == null)
            {
                Recording = InUse = false;
                done(null);
                yield break;
            }
            var samples = new List<float>(Rate * 8);
            var window = new float[Rate / 10];
            int last = 0;
            float time = 0f, quiet = 0f;
            var spoke = false;
            while (!stop && time < settings.maxUtteranceSeconds && !(spoke && quiet >= settings.silenceSeconds) && !(!spoke && time >= settings.listenTimeoutSeconds))
            {
                yield return null;
                time += Time.deltaTime;
                var position = Microphone.GetPosition(open);
                while (position - last >= window.Length)
                {
                    clip.GetData(window, last);
                    samples.AddRange(window);
                    last += window.Length;
                    if (Rms(window) > settings.speechRms) { spoke = true; quiet = 0f; }
                    else quiet += (float)window.Length / Rate;
                }
            }
            Close();
            done(spoke ? Pcm16(samples) : null);
        }

        public static float Rms(float[] window, int count = -1)
        {
            if (count < 0) count = window.Length;
            var sum = 0f;
            for (var i = 0; i < count; i++) sum += window[i] * window[i];
            return count > 0 ? Mathf.Sqrt(sum / count) : 0f;
        }

        static byte[] Pcm16(List<float> samples)
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
