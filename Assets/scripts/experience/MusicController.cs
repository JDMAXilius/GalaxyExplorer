// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Audio;

namespace CosmicSimulation
{
    /// <summary>
    /// One looping bed per room state, crossfaded on <see cref="EnvironmentController.ModeChanged"/> (GDD 9), not
    /// per experience: the room also changes on the passthrough toggle, overlays and a failed switch.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class MusicController : MonoBehaviour
    {
        /// <summary>The bed for one room state.</summary>
        [Serializable]
        public class ModeTrack
        {
            public EnvironmentMode Mode;

            public AudioClip Clip;

            [Range(0f, 1f)]
            [Tooltip("Level this bed plays at. Per track, because the three are mastered differently.")]
            public float Volume = 0.35f;
        }

        [SerializeField]
        [Tooltip("One looping bed per room state. A state with no entry, or an entry with no clip, is silent.")]
        private ModeTrack[] tracks = Array.Empty<ModeTrack>();

        [SerializeField]
        [Tooltip("Seconds to fade from one bed to the next. GDD 9 asks for 2 s.")]
        private float crossfadeSeconds = 2f;

        [SerializeField]
        [Tooltip("Optional mixer group. Left empty the beds go straight to the listener, so Mute " +
                 "(AudioListener.volume) still silences them and no snapshot can mute them behind our back.")]
        private AudioMixerGroup output;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of its level the music drops to while narration is speaking. 1 turns ducking off.")]
        private float narrationDuck = 0.55f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Overall music level, on top of each track's own. The being's voice sits above the beds.")]
        private float level = 0.7f;

        [SerializeField]
        [Tooltip("Seconds the duck takes, going down and coming back.")]
        private float duckSeconds = 0.35f;

        // Our own sources, not the AudioService pool (spatial, released when a clip ends, no ramp), and not its
        // mixer groups, which the legacy per-view snapshots mute and unmute and would fight the fade.
        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly float[] _level = new float[2];
        private readonly float[] _target = new float[2];
        private readonly HashSet<EnvironmentMode> _warned = new HashSet<EnvironmentMode>();
        private int _active;
        private float _fadeSpeed = 0.5f;
        private float _duck = 1f;
        private float _external = 1f;

        /// <summary>True while the being is awake: the beds drop the same way they do under narration.</summary>
        public static bool Ducked { get; set; }
        private VOManager _vo;

        public static MusicController Instance { get; private set; }

        /// <summary>The bed being faded up, or the one still fading out; null when nothing is playing.</summary>
        public AudioClip CurrentTrack => _sources[_active] != null ? _sources[_active].clip : null;

        private void Awake()
        {
            EnsureSources();
        }

        private void OnEnable()
        {
            EnvironmentController.ModeChanged += HandleModeChanged;

            // ModeChanged fires only on a change, so read the opening state. No controller (a legitimate scene)
            // means Passthrough, which is what it would have started in.
            SetMode(EnvironmentController.Instance != null
                ? EnvironmentController.Instance.EffectiveMode
                : EnvironmentMode.Passthrough);
        }

        private void OnDisable()
        {
            EnvironmentController.ModeChanged -= HandleModeChanged;

            // Only Update moves the levels, so a bed left running here would stay at its volume for the session.
            Release(0);
            Release(1);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            EnsureSources();

            var wanted = NarrationPlaying() ? narrationDuck : 1f;
            _duck = Mathf.MoveTowards(_duck, wanted, Time.unscaledDeltaTime / Mathf.Max(0.01f, duckSeconds));
            _external = Mathf.MoveTowards(_external, Ducked ? narrationDuck : 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, duckSeconds));

            for (var i = 0; i < 2; i++)
            {
                var source = _sources[i];
                if (source == null)
                {
                    continue;
                }

                _level[i] = Mathf.MoveTowards(_level[i], _target[i], _fadeSpeed * Time.unscaledDeltaTime);

                if (source.clip == null)
                {
                    continue;
                }

                if (_level[i] <= 0f && _target[i] <= 0f)
                {
                    // Stop rather than leave a silent source decoding: clips over 20 s stream on Android, and
                    // only a stopped source gives the stream back.
                    Release(i);
                    continue;
                }

                source.volume = _level[i] * _duck * _external * level;
            }
        }

        // ---------- the fade

        /// <summary>Crossfades to the bed for a room state. The bed already playing keeps its position, so toggling never restarts it.</summary>
        public void SetMode(EnvironmentMode mode, float fadeSeconds = -1f)
        {
            EnsureSources();

            _fadeSpeed = 1f / Mathf.Max(0.01f, fadeSeconds > 0f ? fadeSeconds : crossfadeSeconds);

            var track = Resolve(mode);
            if (track == null)
            {
                if (_warned.Add(mode))
                {
                    Debug.LogWarning(
                        $"MusicController: no track for {mode}, so that room state plays nothing. Assign one on " +
                        "this component, or run Cosmic Simulation/Install Runtime Systems.", this);
                }

                _target[0] = 0f;
                _target[1] = 0f;
                return;
            }

            var other = 1 - _active;

            if (_sources[_active].clip == track.Clip)
            {
                _target[_active] = track.Volume;
                _target[other] = 0f;
                Resume(_active);
                return;
            }

            // A toggle flipped back mid-fade: let the outgoing bed climb from where it is rather than restart it.
            if (_sources[other].clip == track.Clip)
            {
                _active = other;
                other = 1 - _active;
                _target[_active] = track.Volume;
                _target[other] = 0f;
                Resume(_active);
                return;
            }

            // A third bed inside one fade (a failed switch hands the room back the same frame) must cut one; cut
            // the quieter right now, which is not always the outgoing one, so the cut is the least audible.
            var quieter = _level[other] <= _level[_active] ? other : _active;
            var louder = 1 - quieter;

            Release(quieter);
            var source = _sources[quieter];
            source.clip = track.Clip;
            source.volume = 0f;
            source.Play();
            _level[quieter] = 0f;
            _target[quieter] = track.Volume;
            _target[louder] = 0f;
            _active = quieter;
        }

        private void HandleModeChanged(EnvironmentMode mode) => SetMode(mode);

        private ModeTrack Resolve(EnvironmentMode mode)
        {
            if (tracks == null)
            {
                return null;
            }

            foreach (var track in tracks)
            {
                if (track != null && track.Mode == mode && track.Clip != null)
                {
                    return track;
                }
            }

            return null;
        }

        /// <summary>Restarts playback only when it has actually stopped: <c>Play</c> on a live source rewinds it.</summary>
        private void Resume(int index)
        {
            var source = _sources[index];
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        private void Release(int index)
        {
            _level[index] = 0f;
            _target[index] = 0f;

            var source = _sources[index];
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        /// <summary>Made on first use, not only in Awake: a component driven the frame it is added may not have had Awake.</summary>
        private void EnsureSources()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (_sources[0] != null && _sources[1] != null)
            {
                return;
            }

            for (var i = 0; i < 2; i++)
            {
                if (_sources[i] != null)
                {
                    continue;
                }

                var go = new GameObject(i == 0 ? "music_a" : "music_b");
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f; // a bed has no place in the room
                source.volume = 0f;
                source.priority = 0; // music is the last voice the mixer may steal
                source.outputAudioMixerGroup = output;
                _sources[i] = source;
            }
        }

        // ---------- narration

        /// <summary>True while <see cref="VOManager"/> is speaking; polled because it raises no events.</summary>
        private bool NarrationPlaying()
        {
            if (narrationDuck >= 1f)
            {
                return false;
            }

            if (_vo == null && GalaxyExplorerManager.IsInitialized)
            {
                _vo = GalaxyExplorerManager.Instance.VoManager;
            }

            return _vo != null && _vo.IsPlaying;
        }
    }
}
