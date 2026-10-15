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
        // A capture whose write head stops for this long is restarted, at most MaxRestarts times in a row (CS-371).
        const float StallSeconds = 2f;
        const int MaxRestarts = 3;

        public event Action<byte[]> Frame;

        public static bool InUse { get; private set; }

        static string chosenDevice;
        static string[] devices = Array.Empty<string>();
        static float devicesAt = float.NegativeInfinity;
#if UNITY_ANDROID && !UNITY_EDITOR
        static bool permitted;
        static float permittedAt = float.NegativeInfinity;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            chosenDevice = null;
            Asked = false;
            Blocked = false;
            Refresh();
        }

        /// <summary>Re-reads the device list and the permission on next use, after a permission answer.</summary>
        public static void Refresh()
        {
            devicesAt = float.NegativeInfinity;
#if UNITY_ANDROID && !UNITY_EDITOR
            permittedAt = float.NegativeInfinity;
#endif
        }

        public static string Device
        {
            get => chosenDevice ??= PlayerPrefs.GetString(DeviceKey, "");
            set { chosenDevice = value ?? ""; PlayerPrefs.SetString(DeviceKey, chosenDevice); PlayerPrefs.Save(); }
        }

        public static string[] Devices
        {
            get
            {
                if (Time.unscaledTime - devicesAt >= 1f)
                {
                    devicesAt = Time.unscaledTime;
                    devices = Microphone.devices;
                }

                return devices;
            }
        }

        public bool Armed => clip != null;
        public bool Listening { get; private set; }
        public float Level { get; private set; }
        public string Label { get; private set; } = "default";

        /// <summary>The room's quiet level, followed slowly upward and quickly downward.</summary>
        public float NoiseFloor { get; private set; } = 0.002f;

        /// <summary>True while the level is clearly above the room's quiet: someone is talking.</summary>
        public bool Voiced => NoiseFloor >= 0f && Level > Mathf.Max(NoiseFloor * 3f, MinVoiceLevel);

        /// <summary>The microphone sits at the mouth: the Quest's own, or the Link headset mic.</summary>
        public bool NearField
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return device == null || device == Headset(Devices);
#else
                return device != null && device == Headset(Devices);
#endif
            }
        }

        const float MinVoiceLevel = 0.004f;

        readonly List<short> pending = new List<short>(FrameSamples * 4);
        AudioClip clip;
        string device;
        float[] read = new float[0];
        int last;
        float advancedAt;
        int restarts;
        double phase;
        float lastSample;
        float[] filtered = new float[0];
        float[] tail = new float[0];

        public static bool Permitted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (Time.unscaledTime - permittedAt >= 1f)
                {
                    permittedAt = Time.unscaledTime;
                    permitted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
                }

                return permitted;
#else
                return true;
#endif
            }
        }

        /// <summary>
        /// True once the player answered "don't ask again" (on Android 11+ a second deny counts): the system will not
        /// show the request any more, so only the headset's settings can turn the microphone on (CS-371).
        /// </summary>
        public static bool Blocked { get; private set; }

        /// <summary>True once the system has been asked this session.</summary>
        public static bool Asked { get; private set; }

        public static IEnumerator Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permitted) yield break;
            Asked = true;
            var answered = false;
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => { answered = true; Blocked = false; };
            callbacks.PermissionDenied += _ => answered = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => { answered = true; Blocked = true; };
            callbacks.PermissionRequestDismissed += _ => answered = true;
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
            for (var waited = 0f; !answered && waited < 30f; waited += Time.unscaledDeltaTime) yield return null;
            Refresh();
#else
            yield break;
#endif
        }

        /// <summary>The player's chosen device while it is plugged in, otherwise <see cref="Headset"/>; null is the system default.</summary>
        public static string Resolve()
        {
            var list = Devices;
            var wanted = Device;
            if (!string.IsNullOrEmpty(wanted))
                foreach (var name in list)
                    if (name == wanted) return name;
            return Headset(list);
        }

        /// <summary>
        /// The headset's own microphone (CS-371). On the Quest that is "Android audio input"; the camcorder and
        /// voice-recognition entries are presets of the same mic that fail to start on some headsets. Over Link it is the
        /// Oculus virtual device, which Windows does not make the default. Null: use the system default.
        /// </summary>
        public static string Headset(string[] list)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var name in list)
                if (name == "Android audio input") return name;
            foreach (var name in list)
                if (name.IndexOf("camcorder", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("voice recognition", StringComparison.OrdinalIgnoreCase) < 0) return name;
            return null;
#else
            if (!UnityEngine.XR.XRSettings.isDeviceActive) return null;
            // Two passes: a Bluetooth headset is also called "Headset Microphone (...)" and must not beat the Link mic.
            foreach (var name in list)
                if (name.IndexOf("Oculus Virtual Audio Device", StringComparison.OrdinalIgnoreCase) >= 0) return name;
            foreach (var name in list)
                if (name.StartsWith("Headset Microphone", StringComparison.OrdinalIgnoreCase)) return name;
            return null;
#endif
        }

        // Opened once while the being is out, so listening starts on the next frame instead of waiting on the driver.
        public bool Arm()
        {
            if (clip != null) return true;
            if (!Permitted || Devices.Length == 0) { Label = Permitted ? "no microphone" : "permission denied"; return false; }
            device = Resolve();
            Label = device ?? "default";
            if (Microphone.IsRecording(device)) Microphone.End(device);
            clip = Microphone.Start(device, true, RingSeconds, CaptureRate(device));
            if (clip == null) { Debug.LogWarning($"Mic: '{Label}' did not start; devices: {string.Join(", ", Devices)}"); return false; }
            Debug.Log($"Mic: armed '{Label}' at {clip.frequency} Hz (sent at {Rate}), {(NearField ? "near" : "far")} field; devices: {string.Join(", ", Devices)}");
            last = 0;
            phase = 0;
            lastSample = 0f;
            tail = new float[0];
            NoiseFloor = -1f;
            advancedAt = Time.unscaledTime;
            pending.Clear();
            InUse = true;
            return true;
        }

        // The rate the device really records at. Asking a 48 kHz-only mic for 24 kHz made Unity decimate it with no
        // low-pass, and the aliasing it folded into the voice band was part of what the transcriber misheard; this
        // class downsamples with a filter instead.
        static int CaptureRate(string device)
        {
            Microphone.GetDeviceCaps(device, out var min, out var max);
            if (max == 0 || (min <= Rate && Rate <= max)) return Rate;
            return max;
        }

        // Android can stop delivering samples while still reporting a recording (after a pause, or a capture started
        // before the permission landed); a fresh Start is the only cure.
        void Restart()
        {
            if (++restarts > MaxRestarts)
            {
                if (restarts == MaxRestarts + 1) Debug.LogWarning($"Mic: no samples from '{Label}' after {MaxRestarts} restarts; giving up until it is armed again.");
                advancedAt = Time.unscaledTime;
                return;
            }

            Debug.LogWarning($"Mic: '{Label}' stopped delivering samples; restarting it ({restarts}/{MaxRestarts}).");
            var listening = Listening;
            var count = restarts;
            Close();
            restarts = count;
            if (Arm() && listening) Start();
        }

        public void Start()
        {
            if (Listening || clip == null) return;
            Poll();
            if (clip == null) return; // the poll restarted a stalled capture and could not arm it again
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
            restarts = 0;
            if (clip != null) Microphone.End(device);
            clip = null;
            Level = 0f;
            InUse = false;
        }

        public void Poll()
        {
            if (clip == null) return;
            var pos = Microphone.GetPosition(device);
            if (pos < 0 || pos == last)
            {
                if (Time.unscaledTime - advancedAt > StallSeconds) Restart();
                return;
            }

            advancedAt = Time.unscaledTime;
            restarts = 0;
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
            var seconds = (float)available / clip.frequency;
            // Seeded from the first reading; then down fast, up slowly, and very slowly while someone is talking, so a
            // long question cannot raise the floor past the voice but a room louder than the seed is still learned.
            if (NoiseFloor < 0f) NoiseFloor = Level;
            else if (Level < NoiseFloor) NoiseFloor = Mathf.Lerp(NoiseFloor, Level, Mathf.Clamp01(seconds * 4f));
            else NoiseFloor = Mathf.Lerp(NoiseFloor, Level, Mathf.Clamp01(seconds * (Voiced ? 0.03f : 0.5f)));
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
            var src = step >= 1.5 ? LowPass(count, (int)Math.Floor(step)) : read;
            var pos = phase;
            while (pos < count)
            {
                var i = (int)pos;
                var a = i == 0 ? lastSample : src[i - 1];
                var b = src[Math.Min(i, count - 1)];
                pending.Add(Pcm(a + (b - a) * (float)(pos - i)));
                pos += step;
            }
            phase = pos - count;
            lastSample = src[count - 1];
        }

        // A triangular FIR of half-width `half` over the new samples, carried across calls: for 48 to 24 kHz about
        // -19 dB at 12 kHz and a zero at 16 kHz, which keeps most of 12-24 kHz from folding into the voice band.
        float[] LowPass(int count, int half)
        {
            var taps = 2 * half + 1;
            var keep = taps - 1;
            if (tail.Length != keep) tail = new float[keep];
            if (filtered.Length < count) filtered = new float[count];
            var norm = (float)((half + 1) * (half + 1));
            for (var i = 0; i < count; i++)
            {
                var sum = 0f;
                for (var k = 0; k < taps; k++)
                {
                    var j = i - k;
                    var x = j >= 0 ? read[j] : tail[keep + j];
                    sum += (half + 1 - Math.Abs(k - half)) * x;
                }
                filtered[i] = sum / norm;
            }

            if (count >= keep)
            {
                Array.Copy(read, count - keep, tail, 0, keep);
            }
            else
            {
                Array.Copy(tail, count, tail, 0, keep - count);
                Array.Copy(read, 0, tail, keep - count, count);
            }

            return filtered;
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
