using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Cosmic
{
    public class Audio : MonoBehaviour
    {
        [SerializeField] AudioLibrary library;
        [SerializeField] float crossfadeSeconds = 2f;
        [SerializeField, Range(0f, 1f)] float musicLevel = 0.35f;
        [SerializeField, Range(0f, 1f)] float duckMultiplier = 0.55f;
        [SerializeField] float duckSeconds = 0.35f;
        [SerializeField, Range(0f, 1f)] float ambienceLevel = 0.35f;
        [SerializeField] float ambienceFadeSeconds = 1f;

        readonly AudioSource[] musicSources = new AudioSource[2];
        readonly float[] level = new float[2];
        readonly float[] target = new float[2];
        readonly Queue<AudioClip> voiceQueue = new Queue<AudioClip>();
        readonly List<AudioSource> loops = new List<AudioSource>();

        SfxPool sfx;
        AudioSource voiceSource;
        AudioSource ambienceSource;
        AudioClip ambiencePending;
        float ambienceNow, ambienceGoal;
        float fadeSpeed = 0.5f;
        float duck = 1f;
        int active;
        bool built;

        public bool Speaking => voiceSource != null && (voiceSource.isPlaying || voiceQueue.Count > 0);

        public void Play(Sfx id, Transform at = null, float volume = -1f)
        {
            if (library == null) return;
            var clip = library.Clip(id);
            if (clip != null) Play(clip, at, volume < 0f ? library.Volume(id) : volume);
        }

        public void Play(AudioClip clip, Transform at = null, float volume = 1f)
        {
            Build();
            sfx.Play(clip, at, volume);
        }

        public void Say(AudioClip clip, bool replaceQueue = false)
        {
            Build();
            if (clip == null || Prefs.VoiceMuted) return;
            if (replaceQueue) StopVoice();
            voiceQueue.Enqueue(clip);
        }

        public void StopVoice()
        {
            Build();
            voiceQueue.Clear();
            voiceSource.Stop();
            voiceSource.clip = null;
        }

        public void Music(RoomMode mode)
        {
            Build();
            fadeSpeed = 1f / Mathf.Max(0.01f, crossfadeSeconds);
            var clip = Bed(mode);
            var other = 1 - active;
            if (clip == null) { target[0] = 0f; target[1] = 0f; return; }
            if (musicSources[other].clip == clip) { active = other; other = 1 - active; }
            if (musicSources[active].clip == clip)
            {
                target[active] = musicLevel;
                target[other] = 0f;
                Resume(musicSources[active]);
                return;
            }
            // Whichever source is quieter *now*, not the one on its way out: for the first half of a fade the bed being left is still the louder.
            var quieter = level[other] <= level[active] ? other : active;
            Quiet(quieter);
            var source = musicSources[quieter];
            source.clip = clip;
            source.volume = 0f;
            source.Play();
            target[quieter] = musicLevel;
            target[1 - quieter] = 0f;
            active = quieter;
        }

        public void Ambience(AudioClip clip)
        {
            Build();
            if (clip == null) { ambiencePending = null; ambienceGoal = 0f; return; }
            if (ambienceSource.clip == clip) { ambiencePending = null; ambienceGoal = ambienceLevel; Resume(ambienceSource); return; }
            ambiencePending = clip;
            ambienceGoal = 0f;
            if (ambienceNow <= 0f) ApplyAmbience();
        }

        public AudioSource Loop(Sfx id) => library != null ? Loop(library.Clip(id)) : null;

        public AudioSource Loop(AudioClip clip)
        {
            Build();
            if (clip == null) return null;
            var source = Make("loop", library != null ? library.sfx : null, 1f, true);
            source.clip = clip;
            source.Play();
            loops.Add(source);
            return source;
        }

        public void Release(AudioSource source)
        {
            if (source == null || !loops.Remove(source)) return;
            source.Stop();
            Destroy(source.gameObject);
        }

        void OnEnable()
        {
            Build();
            Room.Changed -= OnRoom;
            Room.Changed += OnRoom;
            Prefs.Changed -= OnPrefs;
            Prefs.Changed += OnPrefs;
            OnPrefs();
            Music(Room.Effective);
        }

        void OnDisable()
        {
            Room.Changed -= OnRoom;
            Prefs.Changed -= OnPrefs;
            Quiet(0);
            Quiet(1);
            ambiencePending = null;
            ambienceNow = ambienceGoal = 0f;
            Hush(ambienceSource);
            StopVoice();
            sfx.Stop();
            foreach (var l in loops) if (l != null) l.Stop();
        }

        void OnDestroy()
        {
            foreach (var l in loops) if (l != null) Destroy(l.gameObject);
            loops.Clear();
        }

        void Update()
        {
            Build();
            var dt = Time.unscaledDeltaTime;

            if (voiceQueue.Count > 0 && !voiceSource.isPlaying)
            {
                voiceSource.clip = voiceQueue.Dequeue();
                voiceSource.volume = 1f;
                voiceSource.Play();
            }

            duck = Mathf.MoveTowards(duck, Speaking ? duckMultiplier : 1f, dt / Mathf.Max(0.01f, duckSeconds));

            for (var i = 0; i < 2; i++)
            {
                level[i] = Mathf.MoveTowards(level[i], target[i], fadeSpeed * dt);
                if (musicSources[i].clip == null) continue;
                if (level[i] <= 0f && target[i] <= 0f) Quiet(i);
                else musicSources[i].volume = level[i] * duck;
            }

            ambienceNow = Mathf.MoveTowards(ambienceNow, ambienceGoal, dt / Mathf.Max(0.01f, ambienceFadeSeconds));
            if (ambiencePending != null && ambienceNow <= 0f) ApplyAmbience();
            if (ambienceSource.clip == null) return;
            if (ambienceNow <= 0f && ambienceGoal <= 0f) Hush(ambienceSource);
            else ambienceSource.volume = ambienceNow * duck;
        }

        void OnRoom(RoomMode mode) => Music(mode);

        void OnPrefs()
        {
            AudioListener.volume = Prefs.Muted ? 0f : 1f;
            if (Prefs.VoiceMuted) StopVoice();
        }

        AudioClip Bed(RoomMode mode)
        {
            if (library == null || library.musicByRoom == null) return null;
            var index = (int)mode;
            return index >= 0 && index < library.musicByRoom.Length ? library.musicByRoom[index] : null;
        }

        void ApplyAmbience()
        {
            ambienceSource.Stop();
            ambienceSource.clip = ambiencePending;
            ambiencePending = null;
            ambienceSource.volume = 0f;
            ambienceSource.Play();
            ambienceNow = 0f;
            ambienceGoal = ambienceLevel;
        }

        void Quiet(int index)
        {
            level[index] = target[index] = 0f;
            Hush(musicSources[index]);
        }

        void Build()
        {
            if (built) return;
            built = true;
            var has = library != null;
            sfx = new SfxPool(transform, () => library != null ? library.sfx : null);
            voiceSource = Make("voice", has ? library.voice : null, 0f, false);
            voiceSource.volume = 1f;
            ambienceSource = Make("ambience", has ? library.ambience : null, 0f, true);
            for (var i = 0; i < 2; i++)
                musicSources[i] = Make(i == 0 ? "music_a" : "music_b", has ? library.music : null, 0f, true);
        }

        AudioSource Make(string sourceName, AudioMixerGroup group, float blend, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = blend;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        static void Resume(AudioSource source) { if (source != null && source.clip != null && !source.isPlaying) source.Play(); }

        static void Hush(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }
    }
}
