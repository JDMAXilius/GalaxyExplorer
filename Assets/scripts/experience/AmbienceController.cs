// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Audio;

namespace CosmicSimulation
{
    /// <summary>
    /// One looping 2D bed per experience (GDD 9). Not <see cref="MusicController"/>, which follows the room state
    /// and must not restart on a switch; per-body sound is spatial and lives in <c>PlanetForceSolver</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbienceController : MonoBehaviour
    {
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Level a bed plays at. Under the music on purpose: this layer is texture, not melody.")]
        private float volume = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the level the bed drops to while the being is awake.")]
        private float beingDuck = 0.55f;

        [SerializeField]
        [Tooltip("Seconds a bed takes to fade in, and the same to fade out. Slower than the 0.6 s grow-in, so a " +
                 "place is already there before its sound has fully arrived.")]
        private float fadeSeconds = 1.5f;

        [SerializeField]
        [Tooltip("Optional mixer group. Left empty the bed goes straight to the listener, so Mute still silences " +
                 "it and no legacy snapshot can mute it behind our back — the same choice MusicController made.")]
        private AudioMixerGroup output;

        // Our own source, as AudioService's pooled ones are spatial and cannot ramp. One is enough: the director
        // stops the old bed a scene load before asking for the next, so only a double switch overlaps (as a dip).
        private AudioSource _source;
        private AudioClip _pending;
        private float _level;
        private float _target;
        private float _external = 1f;

        /// <summary>True while the being is awake.</summary>
        public static bool Ducked { get; set; }

        public static AmbienceController Instance { get; private set; }

        /// <summary>The bed that is playing or fading; null when the room is quiet.</summary>
        public AudioClip CurrentBed => _source != null ? _source.clip : null;

        // ---------- what the director calls

        /// <summary>Plays the bed of the place that has just opened. A null clip (no bed yet) means silence, not an error.</summary>
        public static void PlayBed(AudioClip clip)
        {
            if (clip == null)
            {
                StopBed();
                return;
            }

            var controller = Resolve();
            if (controller != null)
            {
                controller.Play(clip);
            }
        }

        /// <summary>
        /// Fades the current bed away. Does not <see cref="Resolve"/>: with no controller nothing is playing, and
        /// making one just to stop it would leave an object behind for the session.
        /// </summary>
        public static void StopBed()
        {
            if (Instance != null)
            {
                Instance.Stop();
            }
        }

        /// <summary>Finds the controller or makes one; one placed in the boot scene is adopted so its levels can be tuned there.</summary>
        public static AmbienceController Resolve()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Awake may not have run on one spawned this frame, so look before making a second. Inactive ones are
            // skipped: the fade runs in Update, and a bed that cannot fade would play at one level forever.
            var found = FindAnyObjectByType<AmbienceController>();
            if (found != null)
            {
                Instance = found;
                return found;
            }

            var go = new GameObject("ambience");

            // The director unloads view scenes on every switch, which is exactly when the bed is asked to change.
            DontDestroyOnLoad(go);

            return go.AddComponent<AmbienceController>();
        }

        // ---------- the fade

        /// <summary>Fades to a bed. Asking for the one already playing only restores its level, so the loop never restarts.</summary>
        public void Play(AudioClip clip)
        {
            EnsureSource();

            if (clip == null)
            {
                Stop();
                return;
            }

            if (_source.clip == clip)
            {
                _pending = null;
                _target = volume;
                Resume();
                return;
            }

            _pending = clip;
            _target = 0f;

            // Nothing audible to fade out, so start now rather than a frame later in Update.
            if (_level <= 0f)
            {
                ApplyPending();
            }
        }

        public void Stop()
        {
            _pending = null;
            _target = 0f;
        }

        private void Awake()
        {
            EnsureSource();
        }

        private void OnDisable()
        {
            // Only Update moves the level, so a bed left running here would stay at its volume for the session.
            Release();
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
            EnsureSource();

            // Unscaled, like the rest of the switch sequence: a loading place may have left the time scale anywhere.
            _level = Mathf.MoveTowards(_level, _target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            _external = Mathf.MoveTowards(_external, Ducked ? beingDuck : 1f, Time.unscaledDeltaTime / 0.35f);

            if (_pending != null && _level <= 0f)
            {
                ApplyPending();
            }

            if (_source.clip == null)
            {
                return;
            }

            if (_level <= 0f && _target <= 0f)
            {
                // Stop rather than leave a silent source decoding: beds stream on Android, and only a stopped
                // source gives the stream back.
                Release();
                return;
            }

            _source.volume = _level * _external;
        }

        private void ApplyPending()
        {
            var clip = _pending;
            _pending = null;

            _source.Stop();
            _source.clip = clip;
            _source.volume = 0f;
            _source.Play();

            _level = 0f;
            _target = volume;
        }

        /// <summary>Restarts playback only when it has actually stopped: <c>Play</c> on a live source rewinds it.</summary>
        private void Resume()
        {
            if (_source != null && _source.clip != null && !_source.isPlaying)
            {
                _source.Play();
            }
        }

        private void Release()
        {
            _level = 0f;
            _target = 0f;

            if (_source == null)
            {
                return;
            }

            _source.Stop();
            _source.clip = null;
            _source.volume = 0f;
        }

        /// <summary>Made on first use, not only in Awake: a component driven the frame it is added may not have had Awake.</summary>
        private void EnsureSource()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (_source != null)
            {
                return;
            }

            var go = new GameObject("ambience_bed");
            go.transform.SetParent(transform, false);

            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f; // a place has no direction; only bodies do
            _source.volume = 0f;

            // Lower priority than the music's 0: if a voice has to be stolen, the bed is missed least.
            _source.priority = 8;
            _source.outputAudioMixerGroup = output;
        }
    }
}
