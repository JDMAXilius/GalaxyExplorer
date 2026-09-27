using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmic.Companion
{
    public class Mic
    {
        public const int Rate = 24000;
        const int FrameSamples = Rate / 20;
        const int RingSeconds = 10;
        const string DeviceKey = "Companion.Microphone";

        public event Action<byte[]> Frame;

        public static bool InUse { get; private set; }

        public static string Device
        {
            get => PlayerPrefs.GetString(DeviceKey, "");
            set { PlayerPrefs.SetString(DeviceKey, value ?? ""); PlayerPrefs.Save(); }
        }

        public bool Armed => clip != null;
        public bool Listening { get; private set; }
        public float Level { get; private set; }
        public string Label { get; private set; } = "default";

        readonly List<short> pending = new List<short>(FrameSamples * 4);
        AudioClip clip;
        string device;
        float[] read = new float[0];
        int last;
        double phase;
        float lastSample;

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

        public static IEnumerator Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permitted) yield break;
            var answered = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered = true;
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionRequestDismissed += _ => answered = true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
            for (var waited = 0f; !answered && waited < 30f; waited += Time.unscaledDeltaTime) yield return null;
#else
            yield break;
#endif
        }

        public static string Resolve()
        {
            var wanted = Device;
            if (string.IsNullOrEmpty(wanted)) return null;
            foreach (var name in Microphone.devices)
                if (name == wanted) return name;
            return null;
        }

        // Opened once while the being is out, so listening starts on the next frame instead of waiting on the driver.
        public bool Arm()
        {
            if (clip != null) return true;
            if (!Permitted || Microphone.devices.Length == 0) { Label = Permitted ? "no microphone" : "permission denied"; return false; }
            device = Resolve();
            Label = device ?? "default";
            if (Microphone.IsRecording(device)) Microphone.End(device);
            clip = Microphone.Start(device, true, RingSeconds, Rate);
            if (clip == null) return false;
            last = 0;
            phase = 0;
            lastSample = 0f;
            pending.Clear();
            InUse = true;
            return true;
        }

        public void Start()
        {
            if (Listening || clip == null) return;
            Poll();
            pending.Clear();
            Listening = true;
        }

        public void Stop()
        {
            if (!Listening) return;
            Poll();
            Listening = false;
            if (pending.Count > 0) Emit(pending.ToArray(), 0, pending.Count);
            pending.Clear();
        }

        public void Close()
        {
            Listening = false;
            if (clip != null) Microphone.End(device);
            clip = null;
            Level = 0f;
            InUse = false;
        }

        public void Poll()
        {
            if (clip == null) return;
            var pos = Microphone.GetPosition(device);
            if (pos < 0 || pos == last) return;
            var available = pos - last;
            if (available < 0) available += clip.samples;
            if (read.Length < available) read = new float[available];
            var tail = Math.Min(available, clip.samples - last);
            Fetch(0, last, tail);
            if (available > tail) Fetch(tail, 0, available - tail);
            last = pos;
            var sum = 0f;
            for (var i = 0; i < available; i++) sum += read[i] * read[i];
            Level = Mathf.Sqrt(sum / available);
            Resample(available, clip.frequency);
            if (!Listening) { pending.Clear(); return; }
            while (pending.Count >= FrameSamples)
            {
                var frame = new short[FrameSamples];
                pending.CopyTo(0, frame, 0, FrameSamples);
                pending.RemoveRange(0, FrameSamples);
                Emit(frame, 0, FrameSamples);
            }
        }

        void Fetch(int into, int from, int count)
        {
            var tmp = new float[count];
            clip.GetData(tmp, from);
            Array.Copy(tmp, 0, read, into, count);
        }

        void Resample(int count, int inputRate)
        {
            if (count <= 0) return;
            if (inputRate == Rate)
            {
                for (var i = 0; i < count; i++) pending.Add(Pcm(read[i]));
                lastSample = read[count - 1];
                return;
            }
            var step = (double)inputRate / Rate;
            var pos = phase;
            while (pos < count)
            {
                var i = (int)pos;
                var a = i == 0 ? lastSample : read[i - 1];
                var b = read[Math.Min(i, count - 1)];
                pending.Add(Pcm(a + (b - a) * (float)(pos - i)));
                pos += step;
            }
            phase = pos - count;
            lastSample = read[count - 1];
        }

        static short Pcm(float f) => (short)Mathf.RoundToInt(Mathf.Clamp(f, -1f, 1f) * short.MaxValue);

        void Emit(short[] samples, int offset, int count)
        {
            if (count <= 0) return;
            var bytes = new byte[count * 2];
            for (var i = 0; i < count; i++)
            {
                var s = samples[offset + i];
                bytes[i * 2] = (byte)s;
                bytes[i * 2 + 1] = (byte)(s >> 8);
            }
            Frame?.Invoke(bytes);
        }
    }
}
