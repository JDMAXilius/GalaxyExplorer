// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Audio;

namespace CosmicSimulation
{
    /// <summary>
    /// The bed of the place the player is in: one looping, non-positional ambience per experience, started when
    /// that place opens and faded away when it closes (GDD 9, "Ambience: per experience ... loops").
    ///
    /// It is not <see cref="MusicController"/> and not <c>AudioService</c>, for two different reasons.
    /// The music is keyed to the <b>room state</b> — four environment modes, three tracks — and deliberately does
    /// not restart when the experience changes underneath it; the ambience is keyed to the <b>experience</b>, of
    /// which there are seven, and must change every time the place does. Folding them together would mean one
    /// component answering to two unrelated events with two unrelated clip tables. The pooled sources in
    /// <c>AudioService</c> are the wrong shape for either: they are spatial, they return themselves to the pool
    /// the moment a clip ends, they refuse the same clip twice inside 50 ms, and they expose no ramp.
    ///
    /// One source, not two. Unlike the music this never has to crossfade: the director stops the outgoing bed at
    /// the top of a switch and asks for the new one at the bottom, a scene load and a 0.6 s grow-in later, so the
    /// fade-out has long finished. Asking for a new bed while the old one is still audible is handled anyway —
    /// the old one finishes fading down and the new one starts — which is a dip rather than a crossfade, and the
    /// only way to reach it is to switch places twice inside one fade.
    ///
    /// Per-body ambience (GDD 9 again: "3D for bodies") is <b>not</b> here. That layer already exists and is
    /// spatial: <c>PlanetForceSolver</c> plays its own clip through <c>AudioService</c>, parented to the body, so
    /// it pans as the player moves the planet around. This bed is 2D on purpose — a place has no direction.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbienceController : MonoBehaviour
    {
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Level a bed plays at. Under the music on purpose: this layer is texture, not melody.")]
        private float volume = 0.35f;

        [SerializeField]
        [Tooltip("Seconds a bed takes to fade in, and the same to fade out. Slower than the 0.6 s grow-in, so a " +
                 "place is already there before its sound has fully arrived.")]
        private float fadeSeconds = 1.5f;

        [SerializeField]
        [Tooltip("Optional mixer group. Left empty the bed goes straight to the listener, so Mute still silences " +
                 "it and no legacy snapshot can mute it behind our back — the same choice MusicController made.")]
        private AudioMixerGroup output;

        private AudioSource _source;
        private AudioClip _pending;
        private float _level;
        private float _target;

        public static AmbienceController Instance { get; private set; }

        /// <summary>The bed that is playing or fading; null when the room is quiet.</summary>
        public AudioClip CurrentBed => _source != null ? _source.clip : null;

        // ---------- what the director calls

        /// <summary>
        /// Plays the bed of the place that has just opened. A null clip means that place has no bed yet — four
        /// of the seven are still owed one — and is the same as silence, not an error.
        /// </summary>
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
        /// Fades the current bed away. Deliberately does not <see cref="Resolve"/>: if no controller exists then
        /// nothing is playing, and making one to silence silence would leave an object behind for the session.
        /// </summary>
        public static void StopBed()
        {
            if (Instance != null)
            {
                Instance.Stop();
            }
        }

        /// <summary>
        /// Finds the controller, or makes one. Nothing in the project wires this up and nothing needs to; an
        /// instance dropped into the boot scene by hand is adopted instead, so its levels can be tuned there.
        /// </summary>
        public static AmbienceController Resolve()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Instance is set in Awake, which has not necessarily run for an object that spawned this frame, so
            // look before making a second one. Inactive ones are not adopted: the fade is driven from Update,
            // which an inactive object never gets, and a bed that cannot fade would play at one level forever.
            var found = FindAnyObjectByType<AmbienceController>();
            if (found != null)
            {
                Instance = found;
                return found;
            }

            var go = new GameObject("ambience");

            // The director unloads whole view scenes on every switch. A bed that lived in one would be destroyed
            // at the exact moment it is asked to change.
            DontDestroyOnLoad(go);

            return go.AddComponent<AmbienceController>();
        }

        // ---------- the fade

        /// <summary>
        /// Fades to a bed. Asking for the one already playing only restores its level — the loop keeps its
        /// position, so re-opening the place you are already in never restarts the sound.
        /// </summary>
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

            // Nothing audible to get out of the way, so start now rather than wait a frame for Update: the
            // caller may have stopped the previous bed a second ago, or there may never have been one.
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
            // Update is the only thing that moves the level, so a disabled controller that left a bed running
            // would leave it running at whatever volume it had reached, for the rest of the session.
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

            // Unscaled, like everything else in the switch sequence: a fade should not depend on a time scale
            // that a loading place may have left somewhere odd.
            _level = Mathf.MoveTowards(_level, _target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));

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
                // All the way out: stop it rather than leave a silent source decoding. A bed is long enough to be
                // imported as Streaming on Android, and a stopped source is the only thing that gives the stream
                // back.
                Release();
                return;
            }

            _source.volume = _level;
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

        /// <summary>
        /// One source, made on first use rather than only in Awake: a component added with <c>AddComponent</c>
        /// and driven the same frame has not necessarily had Awake (CLAUDE.md).
        /// </summary>
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

            // Above the music's 0 in number, which is below it in priority: if the mixer ever has to steal a
            // voice, the bed is the layer the player will miss least.
            _source.priority = 8;
            _source.outputAudioMixerGroup = output;
        }
    }
}
