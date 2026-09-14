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
