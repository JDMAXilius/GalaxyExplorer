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

        public event Action Started;
        public event Action Drained;

        public float Loudness { get; private set; }
        public bool IsPlaying { get; private set; }

        private readonly float[] _ring = new float[Capacity];
        private readonly object _lock = new object();
        private int _read, _write, _buffered;
        private bool _finishing;
        private float _level;
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
            Loudness = Mathf.Lerp(Loudness, _level, 20f * Time.deltaTime);

            if (!IsPlaying && _buffered > Rate / 5)
            {
                _source.Play();
                IsPlaying = true;
                Started?.Invoke();
            }
            else if (IsPlaying && _finishing && _buffered == 0)
            {
                Clear();
                Drained?.Invoke();
            }
        }
    }
}
