using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation.Being
{
    public class BeingMic : MonoBehaviour
    {
        private const int Rate = 16000;

        public bool IsRecording { get; private set; }

        private bool _stop;

        public void Record(BeingSettings settings, Action<byte[]> done)
        {
            if (!IsRecording)
            {
                StartCoroutine(Run(settings, done));
            }
        }

        public void Stop() => _stop = true;

        private IEnumerator Run(BeingSettings settings, Action<byte[]> done)
        {
            IsRecording = true;
            _stop = false;
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                yield return new WaitForSeconds(0.5f);
            }
            var allowed = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
            var allowed = true;
#endif
            if (!allowed || Microphone.devices.Length == 0)
            {
                IsRecording = false;
                done(null);
                yield break;
            }

            var clip = Microphone.Start(null, false, Mathf.CeilToInt(settings.MaxUtteranceSeconds), Rate);
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
                var position = Microphone.GetPosition(null);
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

            Microphone.End(null);
            IsRecording = false;
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
