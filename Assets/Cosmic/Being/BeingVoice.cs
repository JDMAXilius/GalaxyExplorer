using System;
using UnityEngine;

namespace Cosmic
{
    [RequireComponent(typeof(AudioSource))]
    public class BeingVoice : MonoBehaviour
    {
        const int Rate = 24000;
        const int Capacity = Rate * 60;
        const int SpectrumSize = 256;
        const float PeakDecay = 0.4f;
        const float PeakFloor = 0.02f;

        public event Action Started, Drained;

        public float Loudness { get; private set; }
        public Vector4 Bands { get; private set; }
        public bool Playing { get; private set; }

        readonly float[] ring = new float[Capacity];
        readonly float[] spectrum = new float[SpectrumSize];
        readonly float[] bandPeak = { PeakFloor, PeakFloor, PeakFloor, PeakFloor };
        readonly object gate = new object();
        int read, write, buffered;
        bool finishing, ready;
        float level, peak = PeakFloor;
        AudioSource source;

        public void Enqueue(byte[] pcm16)
        {
            Init();
            lock (gate)
                for (var i = 0; i + 1 < pcm16.Length; i += 2)
                    Push((short)(pcm16[i] | (pcm16[i + 1] << 8)) / 32768f);
        }

        public bool Say(AudioClip clip)
        {
            Init();
            if (clip == null) return false;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int frames = clip.samples, channels = clip.channels;
            var data = new float[frames * channels];
            if (frames == 0 || !clip.GetData(data, 0))
            {
                Debug.LogWarning($"BeingVoice: {clip.name} cannot be read; its load type must be Decompress On Load.", this);
                return false;
            }
            var step = (double)clip.frequency / Rate;
            var count = (int)(frames / step);
            lock (gate)
                for (var i = 0; i < count; i++)
                {
                    var at = i * step;
                    var f = (int)at;
                    var next = Mathf.Min(f + 1, frames - 1);
                    float a = 0f, b = 0f;
                    for (var c = 0; c < channels; c++) { a += data[f * channels + c]; b += data[next * channels + c]; }
                    Push(Mathf.Lerp(a, b, (float)(at - f)) / channels);
                }
            Finish();
            return true;
        }

        public void Finish() => finishing = true;

        public void Clear()
        {
            Init();
            lock (gate) read = write = buffered = 0;
            finishing = false;
            source.Stop();
            Playing = false;
            Loudness = 0f;
            Bands = Vector4.zero;
        }

        void Push(float sample)
        {
            ring[write] = sample;
            write = (write + 1) % Capacity;
            buffered = Mathf.Min(buffered + 1, Capacity);
        }

        void Read(float[] data)
        {
            var sum = 0f;
            lock (gate)
                for (var i = 0; i < data.Length; i++)
                {
                    var s = 0f;
                    if (buffered > 0) { s = ring[read]; read = (read + 1) % Capacity; buffered--; }
                    data[i] = s;
                    sum += s * s;
                }
            level = Mathf.Sqrt(sum / data.Length);
        }

        void Awake() => Init();

        void Update()
        {
            source.mute = Prefs.VoiceMuted;
            var dt = Time.deltaTime;
            peak = Mathf.Max(level, Mathf.Max(PeakFloor, peak - PeakDecay * peak * dt));
            Loudness = Mathf.Lerp(Loudness, Mathf.Clamp01(level / peak), 20f * dt);
            Bands = Playing ? Analyse(dt) : Vector4.Lerp(Bands, Vector4.zero, 20f * dt);
            if (!Playing && (buffered > Rate / 5 || (finishing && buffered > 0)))
            {
                source.Play();
                Playing = true;
                Started?.Invoke();
            }
            else if (finishing && buffered == 0)
            {
                Clear();
                Drained?.Invoke();
            }
        }

        Vector4 Analyse(float dt)
        {
            source.GetSpectrumData(spectrum, 0, FFTWindow.Hamming);
            var binHz = AudioSettings.outputSampleRate * 0.5f / SpectrumSize;
            var bands = new Vector4(Band(0f, 300f, binHz), Band(300f, 1000f, binHz), Band(1000f, 3000f, binHz), Band(3000f, 8000f, binHz));
            for (var i = 0; i < 4; i++)
            {
                bandPeak[i] = Mathf.Max(bands[i], Mathf.Max(PeakFloor, bandPeak[i] - PeakDecay * bandPeak[i] * dt));
                bands[i] = Mathf.Clamp01(bands[i] / bandPeak[i]);
            }
            return Vector4.Lerp(Bands, bands, 25f * dt);
        }

        float Band(float fromHz, float toHz, float binHz)
        {
            var from = Mathf.Clamp(Mathf.FloorToInt(fromHz / binHz), 0, SpectrumSize - 1);
            var to = Mathf.Clamp(Mathf.CeilToInt(toHz / binHz), from + 1, SpectrumSize);
            var sum = 0f;
            for (var i = from; i < to; i++) sum += spectrum[i];
            return sum / (to - from);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            source = GetComponent<AudioSource>();
            source.clip = AudioClip.Create("being_voice", Rate, 1, Rate, true, Read);
            source.loop = true;
        }
    }
}
