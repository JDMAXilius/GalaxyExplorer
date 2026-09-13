using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Cosmic
{
    public class SfxPool
    {
        const int Voices = 8;
        const float SameClipSeconds = 0.05f;

        readonly AudioSource[] sources = new AudioSource[Voices];
        readonly float[] started = new float[Voices];
        readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        readonly Transform parent;
        readonly Func<AudioMixerGroup> group;

        // Asked for per voice, not captured once: voices are made lazily and the library can arrive
        // after the bus was built.
        public SfxPool(Transform parent, Func<AudioMixerGroup> group)
        {
            this.parent = parent;
            this.group = group;
        }

        public void Play(AudioClip clip, Transform at, float volume)
        {
            if (clip == null || parent == null) return;
            var now = Time.unscaledTime;

            // Only non-spatial UI sounds: several bodies growing in on one frame are several sounds.
            if (at == null)
            {
                if (lastPlayed.TryGetValue(clip, out var last) && now - last < SameClipSeconds) return;
                lastPlayed[clip] = now;
            }

            var source = Take(now);
            source.Stop();
            source.transform.position = at != null ? at.position : parent.position;
            source.spatialBlend = at != null ? 1f : 0f;
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }

        public void Stop()
        {
            foreach (var s in sources) if (s != null) s.Stop();
        }

        AudioSource Take(float now)
        {
            var oldest = 0;
            for (var i = 0; i < Voices; i++)
            {
                if (sources[i] == null) sources[i] = Make("sfx_" + i);
                if (!sources[i].isPlaying) { oldest = i; break; }
                if (started[i] < started[oldest]) oldest = i;
            }
            started[oldest] = now;
            return sources[oldest];
        }

        AudioSource Make(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = group != null ? group() : null;
            return source;
        }
    }
}
