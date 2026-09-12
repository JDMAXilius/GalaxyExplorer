// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Audio;

namespace CosmicSimulation
{
    /// <summary>
    /// One looping music bed per room state, crossfaded when the room state changes (GDD 9: "one ambient track
    /// per environment mode; crossfade 2 s").
    ///
    /// It keys off <see cref="EnvironmentController.ModeChanged"/> rather than
    /// <see cref="ExperienceDirector.ExperienceChanged"/>, because the room state is what the spec names and it
    /// changes in three places the experience does not: the dock's passthrough toggle, a destination overlay
    /// opening inside the Milky Way map, and a failed switch handing the room back. The director sets the
    /// environment at the top of its switch and starts narration at the bottom, so the fade is over — or nearly —
    /// by the time anyone speaks.
    ///
    /// Two sources, not the <see cref="AudioService"/> pool: pooled sources are spatial, are returned when their
    /// clip ends, refuse the same clip twice within 50 ms and expose no per-source ramp, all of which are right
    /// for a UI blip and wrong for a bed that must loop for an hour and fade rather than stop. The mixer on
    /// <c>AudioServiceProfile</c> is not used either — its groups exist to be muted and unmuted by the legacy
    /// per-view snapshots, which would fight a per-source fade — but <see cref="output"/> is there for the day
    /// there is a plain Music group to route into.
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
        [Tooltip("Seconds the duck takes, going down and coming back.")]
        private float duckSeconds = 0.35f;

        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly float[] _level = new float[2];
        private readonly float[] _target = new float[2];
        private readonly HashSet<EnvironmentMode> _warned = new HashSet<EnvironmentMode>();
        private int _active;
        private float _fadeSpeed = 0.5f;
        private float _duck = 1f;
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

            // Nothing announces the room state on load: EnvironmentController raises ModeChanged only when the
            // state actually changes, and it starts in Passthrough. So the opening bed is read straight off it.
            // A null Instance here is legitimate — a scene with no EnvironmentController at all — and Passthrough
            // is what that controller would have reported anyway.
            SetMode(EnvironmentController.Instance != null
                ? EnvironmentController.Instance.EffectiveMode
                : EnvironmentMode.Passthrough);
        }

        private void OnDisable()
        {
            EnvironmentController.ModeChanged -= HandleModeChanged;

            // Update is the only thing that moves the levels, so a disabled controller that left a bed running
            // would leave it running at whatever volume it had reached, for the rest of the session.
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
                    // All the way out: stop it rather than leave a silent source decoding. Clips over 20 s are
                    // imported as Streaming on Android, and a stopped source is the only thing that gives the
                    // stream back.
                    Release(i);
                    continue;
                }

                source.volume = _level[i] * _duck;
            }
        }

        // ---------- the fade

        /// <summary>
        /// Crossfades to the bed for a room state. Asking for the state that is already playing does nothing —
        /// the bed keeps its position, which is the whole point: the dock's passthrough toggle can be poked
        /// repeatedly and the music never restarts.
        /// </summary>
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

            // Already on this bed: only its level can have changed.
            if (_sources[_active].clip == track.Clip)
            {
                _target[_active] = track.Volume;
                _target[other] = 0f;
                Resume(_active);
                return;
            }

            // The bed we are leaving is the one being asked for again — a toggle flipped back mid-fade. Swap the
            // roles and let it climb from where it is; restarting it would jump the loop audibly.
            if (_sources[other].clip == track.Clip)
            {
                _active = other;
                other = 1 - _active;
                _target[_active] = track.Volume;
                _target[other] = 0f;
                Resume(_active);
                return;
            }

            // A genuinely new bed takes whichever source is quieter *right now*, which is not the same as the one
            // on its way out: for the first half of a crossfade the bed being left is still the louder of the two.
            //
            // Three beds inside one fade is not hypothetical. A scene missing from Build Settings makes the
            // director set the room and then hand it back in the same frame, so FullBlack, Dimmed and Passthrough
            // can all be asked for before Update has moved a single level. The incoming bed is then at zero and
            // has not been heard at all, so cutting it is silent; cutting the other one is a hard stop at full
            // volume, followed by two seconds of nothing underneath the failure notice.
            //
            // Two sources still cannot hold three beds, so something is always cut. This picks the cut nobody
            // hears when there is one, and the smaller of the two when there is not.
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

        /// <summary>
        /// Two sources, made on first use rather than only in Awake: a component added with <c>AddComponent</c>
        /// and driven the same frame has not necessarily had Awake (CLAUDE.md). Two is the whole allocation for
        /// the session — a switch never makes a third.
        /// </summary>
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

        /// <summary>
        /// True while <see cref="VOManager"/> has something to say. Polled rather than subscribed to, because it
        /// raises no events, and this is two field reads. Nothing here touches the narration itself: the duck is
        /// applied to our own sources only.
        /// </summary>
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
