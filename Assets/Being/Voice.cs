using System;
using UnityEngine;

namespace Cosmic.Companion
{
    [RequireComponent(typeof(AudioSource))]
    public class Voice : MonoBehaviour
    {
        public const int Rate = 24000;
        const int Capacity = Rate * 30;
        const int SpectrumSize = 256;
        const float PeakDecay = 0.4f;
        const float PeakFloor = 0.02f;

        public event Action Started, Drained;

        public float Loudness { get; private set; }
        public Vector4 Bands { get; private set; }
        public bool Playing { get; private set; }
        public int ShortReads { get; private set; }
        public int StartLatencyMs { get; private set; }

        readonly float[] ring = new float[Capacity];
        readonly float[] spectrum = new float[SpectrumSize];
        readonly float[] bandPeak = { PeakFloor, PeakFloor, PeakFloor, PeakFloor };
        readonly object gate = new object();
        int read, write, buffered, pendingByte = -1, lastRead;
        bool finishing, ready, starving;
        const float RebufferSeconds = 0.3f;
        float level, peak = PeakFloor, gain = 1f, startSeconds = 0.25f, cueVolume = 0.5f, openedAt, tailLeft = -1f;
        AudioSource source;

        public void Configure(BeingSettings s)
        {
            Init();
            gain = s.voiceGain;
            startSeconds = s.startBufferSeconds;
            cueVolume = s.cueVolume;
            source.spatialBlend = s.spatialBlend;
            source.minDistance = s.fullVolumeMetres;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            source.volume = 1f;
        }

        // A frame may end mid-sample: the odd byte waits for the next frame, or every later sample reads byte-swapped as static.
        public void Enqueue(byte[] pcm16)
        {
            Init();
            lock (gate)
            {
                if (buffered == 0 && !finishing) openedAt = Time.realtimeSinceStartup;
                var i = 0;
                if (pendingByte >= 0 && pcm16.Length > 0)
                {
                    Push((short)(pendingByte | (pcm16[0] << 8)));
                    pendingByte = -1;
                    i = 1;
                }
                for (; i + 1 < pcm16.Length; i += 2) Push((short)(pcm16[i] | (pcm16[i + 1] << 8)));
                if (i < pcm16.Length) pendingByte = pcm16[i];
            }
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
                Debug.LogWarning($"Voice: {clip.name} cannot be read; its load type must be Decompress On Load.", this);
                return false;
            }
            var step = (double)clip.frequency / Rate;
            var count = (int)(frames / step);
            lock (gate)
            {
                openedAt = Time.realtimeSinceStartup;
                for (var i = 0; i < count; i++)
                {
                    var at = i * step;
                    var f = (int)at;
                    var next = Mathf.Min(f + 1, frames - 1);
                    float a = 0f, b = 0f;
                    for (var c = 0; c < channels; c++) { a += data[f * channels + c]; b += data[next * channels + c]; }
                    PushFloat(Mathf.Lerp(a, b, (float)(at - f)) / channels);
                }
            }
            Finish();
            return true;
        }

        public void Cue(AudioClip clip)
        {
            Init();
            if (clip != null) source.PlayOneShot(clip, cueVolume);
        }

        public void Finish() => finishing = true;

        public void Clear()
        {
            Init();
            lock (gate) { read = write = buffered = 0; pendingByte = -1; starving = false; }
            finishing = false;
            tailLeft = -1f;
            if (Playing) source.Stop();
            Playing = false;
            Loudness = 0f;
            Bands = Vector4.zero;
        }

        void Push(short sample) => PushFloat(sample / 32768f);

        void PushFloat(float sample)
        {
            ring[write] = Mathf.Clamp(sample * gain, -1f, 1f);
            write = (write + 1) % Capacity;
            buffered = Mathf.Min(buffered + 1, Capacity);
        }

        void Read(float[] data)
        {
            var sum = 0f;
            lock (gate)
            {
                // Ran dry mid-answer: hold silence until a proper cushion has arrived, rather than playing each late
                // chunk the moment it lands - that is the word-gap-word stutter.
                if (starving && !finishing && buffered < Rate * RebufferSeconds)
                {
                    Array.Clear(data, 0, data.Length);
                    level = 0f;
                    return;
                }
                starving = false;
                var n = Math.Min(data.Length, buffered);
                for (var i = 0; i < n; i++)
                {
                    data[i] = ring[read];
                    sum += data[i] * data[i];
                    read = (read + 1) % Capacity;
                }
                for (var i = n; i < data.Length; i++) data[i] = 0f;
                buffered -= n;
                lastRead = data.Length;
                if (n < data.Length && !finishing)
                {
                    ShortReads++;
                    starving = true;
                }
            }
            level = Mathf.Sqrt(sum / data.Length);
        }

        void Awake() => Init();

        void Update()
        {
            Init();
            var dt = Time.deltaTime;
            peak = Mathf.Max(level, Mathf.Max(PeakFloor, peak - PeakDecay * peak * dt));
            Loudness = Mathf.Lerp(Loudness, Mathf.Clamp01(level / peak), 20f * dt);
            Bands = Playing ? Analyse(dt) : Vector4.Lerp(Bands, Vector4.zero, 20f * dt);
            if (!Playing && (buffered > Rate * startSeconds || (finishing && buffered > 0)))
            {
                StartLatencyMs = Mathf.RoundToInt((Time.realtimeSinceStartup - openedAt) * 1000f);
                ShortReads = 0;
                source.Play();
                Playing = true;
                Started?.Invoke();
            }
            else if (finishing && buffered == 0)
            {
                // Unity reads a streamed clip ahead of what is heard: the last read and the mixer's buffers are
                // still to play when the ring runs dry, and stopping now cuts the final word.
                if (tailLeft < 0f) tailLeft = Tail();
                tailLeft -= Time.unscaledDeltaTime;
                if (tailLeft > 0f) return;
                Clear();
                Drained?.Invoke();
            }
        }

        float Tail()
        {
            AudioSettings.GetDSPBufferSize(out var length, out var count);
            var dsp = (float)length * count / Mathf.Max(1, AudioSettings.outputSampleRate);
            return (float)lastRead / Rate + dsp + 0.1f;
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
            source.playOnAwake = false;
            source.clip = AudioClip.Create("being_voice", Rate, 1, Rate, true, Read);
            source.loop = true;
        }
    }
}
