using System;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation.Being
{
    [RequireComponent(typeof(AudioSource))]
    public class BeingSpeaker : MonoBehaviour
    {
        private const int Rate = 24000;
        private const int Capacity = Rate * 60;
        private const int SpectrumSize = 256;
        private const float PeakDecay = 0.4f;
        private const float PeakFloor = 0.02f;

        public event Action Started;
        public event Action Drained;

        public float Loudness { get; private set; }
        public Vector4 Bands { get; private set; }
        public bool IsPlaying { get; private set; }

        private readonly float[] _ring = new float[Capacity];
        private readonly float[] _spectrum = new float[SpectrumSize];
        private readonly float[] _bandPeak = { PeakFloor, PeakFloor, PeakFloor, PeakFloor };
        private readonly object _lock = new object();
        private int _read, _write, _buffered;
        private bool _finishing;
        private float _level, _peak = PeakFloor;
        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = AudioClip.Create("being_voice", Rate, 1, Rate, true, Read);
            _source.loop = true;
        }

        public void Enqueue(byte[] pcm16)
        {
            lock (_lock)
            {
                for (var i = 0; i + 1 < pcm16.Length; i += 2)
                {
                    _ring[_write] = (short)(pcm16[i] | (pcm16[i + 1] << 8)) / 32768f;
                    _write = (_write + 1) % Capacity;
                    _buffered = Mathf.Min(_buffered + 1, Capacity);
                }
            }
        }

        /// <summary>
        /// Speaks a clip that ships with the app - the greeting - through the same ring the relay's voice uses,
        /// so the talking pose, the narration mute and <see cref="Drained"/> all behave exactly as for an answer.
        /// The clip is mixed to mono and resampled to the voice rate. It must be decompressed on load, which is
        /// Unity's default for a short WAV; anything else cannot be read back and is skipped with a warning.
        /// </summary>
        public bool Say(AudioClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            // A clip that is not preloaded is still unloaded in a player build, and GetData on it fails.
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }

            var frames = clip.samples;
            var channels = clip.channels;
            var source = new float[frames * channels];
            if (frames == 0 || !clip.GetData(source, 0))
            {
                Debug.LogWarning($"BeingSpeaker: could not read {clip.name}; set its Load Type to Decompress On Load.");
                return false;
            }

            var step = (double)clip.frequency / Rate;
            var count = (int)(frames / step);
            lock (_lock)
            {
                for (var i = 0; i < count; i++)
                {
                    var at = i * step;
                    var f = (int)at;
                    var next = Mathf.Min(f + 1, frames - 1);
                    var t = (float)(at - f);
                    var a = 0f;
                    var b = 0f;
                    for (var c = 0; c < channels; c++)
                    {
                        a += source[f * channels + c];
                        b += source[next * channels + c];
                    }
                    _ring[_write] = Mathf.Lerp(a, b, t) / channels;
                    _write = (_write + 1) % Capacity;
                    _buffered = Mathf.Min(_buffered + 1, Capacity);
                }
            }

            Finish();
            return true;
        }

        public void Finish() => _finishing = true;

        public void Clear()
        {
            lock (_lock)
            {
                _read = _write = _buffered = 0;
            }
            _finishing = false;
            _source.Stop();
            IsPlaying = false;
            Loudness = 0f;
            Bands = Vector4.zero;
        }

        private void Read(float[] data)
        {
            var sum = 0f;
            lock (_lock)
            {
                for (var i = 0; i < data.Length; i++)
                {
                    var s = _buffered > 0 ? _ring[_read] : 0f;
                    if (_buffered > 0)
                    {
                        _read = (_read + 1) % Capacity;
                        _buffered--;
                    }
                    data[i] = s;
                    sum += s * s;
                }
            }
            _level = Mathf.Sqrt(sum / data.Length);
        }

        private void Update()
        {
            _source.mute = !VOManager.NarrationEnabled;
            var dt = Time.deltaTime;

            _peak = Mathf.Max(_level, Mathf.Max(PeakFloor, _peak - PeakDecay * _peak * dt));
            Loudness = Mathf.Lerp(Loudness, Mathf.Clamp01(_level / _peak), 20f * dt);
            Bands = IsPlaying ? Analyse(dt) : Vector4.Lerp(Bands, Vector4.zero, 20f * dt);

            if (!IsPlaying && (_buffered > Rate / 5 || (_finishing && _buffered > 0)))
            {
                _source.Play();
                IsPlaying = true;
                Started?.Invoke();
            }
            else if (_finishing && _buffered == 0)
            {
                Clear();
                Drained?.Invoke();
            }
        }

        private Vector4 Analyse(float dt)
        {
            _source.GetSpectrumData(_spectrum, 0, FFTWindow.Hamming);
            var binHz = AudioSettings.outputSampleRate * 0.5f / SpectrumSize;
            var bands = new Vector4(Band(0f, 300f, binHz), Band(300f, 1000f, binHz), Band(1000f, 3000f, binHz), Band(3000f, 8000f, binHz));
            for (var i = 0; i < 4; i++)
            {
                _bandPeak[i] = Mathf.Max(bands[i], Mathf.Max(PeakFloor, _bandPeak[i] - PeakDecay * _bandPeak[i] * dt));
                bands[i] = Mathf.Clamp01(bands[i] / _bandPeak[i]);
            }
            return Vector4.Lerp(Bands, bands, 25f * dt);
        }

        private float Band(float fromHz, float toHz, float binHz)
        {
            var from = Mathf.Clamp(Mathf.FloorToInt(fromHz / binHz), 0, SpectrumSize - 1);
            var to = Mathf.Clamp(Mathf.CeilToInt(toHz / binHz), from + 1, SpectrumSize);
            var sum = 0f;
            for (var i = from; i < to; i++)
            {
                sum += _spectrum[i];
            }
            return sum / (to - from);
        }
    }
}
