// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using GalaxyExplorer.XR;

namespace GalaxyExplorer
{
    public class VOManager : MonoBehaviour
    {
        [Serializable]
        public class QueuedAudioClip
        {
            public AudioClip clip;
            public float delay;
            public bool allowReplay;
            public bool blockProgress;

            public QueuedAudioClip(AudioClip clip, float delay, bool allowReplay, bool blockProgress)
            {
                this.clip = clip;
                this.delay = delay;
                this.allowReplay = allowReplay;
                this.blockProgress = blockProgress;
            }
        }

        // Narration-only mute (GDD 11). Static because it outlives the view scene this component lives in;
        // loaded lazily from PlayerPrefs so it is right before any Awake has run.
        private const string NarrationMutedPrefsKey = "GalaxyExplorer.NarrationMuted";

        private static bool narrationEnabled = true;
        private static bool narrationLoaded;

        /// <summary>Whether narration plays. Voice only: a player who knows the copy still wants music and effects.</summary>
        public static bool NarrationEnabled
        {
            get
            {
                if (!narrationLoaded)
                {
                    narrationEnabled = PlayerPrefs.GetInt(NarrationMutedPrefsKey, 0) == 0;
                    narrationLoaded = true;
                }

                return narrationEnabled;
            }

            set
            {
                // Through the getter, so a first write that happens to match the in-memory default still
                // compares against what was actually stored.
                if (NarrationEnabled == value)
                {
                    return;
                }

                narrationEnabled = value;
                PlayerPrefs.SetInt(NarrationMutedPrefsKey, value ? 0 : 1);
                PlayerPrefs.Save();

                if (!value)
                {
                    StopAll();
                }
            }
        }

        /// <summary>Fades out and clears whatever is being said, wherever it is being said from.</summary>
        private static void StopAll()
        {
            foreach (var manager in CosmicSimulation.Registry<VOManager>.Active)
            {
                // Stop starts a coroutine, which an inactive component cannot do.
                if (manager != null && manager.isActiveAndEnabled)
                {
                    manager.Stop(true);
                }
            }
        }

        [SerializeField]
        private float FadeOutTime = 2.0f;

        private bool VOEnabled = true;

        private AudioSource audioSource;
        private Queue<QueuedAudioClip> clipQueue = new Queue<QueuedAudioClip>();
        private List<string> playedClips = new List<string>();

        private AudioClip nextClip;
        private float nextClipDelay;
        private float defaultVolume;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Narration level. Under the being's voice on purpose.")]
        private float level = 0.8f;

        // Seconds a progress-blocking clip has left. Counted down in Update, not off the wall clock, so it
        // stops while AppPause disables this under the system menu.
        private float blockRemaining;

        private IAudioService audioService;
        

        /// <summary>Whether a place narrates as it opens; off in UI 2.0, where the card's Listen button starts it.</summary>
        public static bool AutoStart { get; set; } = true;

        public bool IsPaused { get; private set; }

        /// <summary>Holds the narration where it is. <see cref="Resume"/> carries on from the same word.</summary>
        public void Pause()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                // The clip and the position are kept here: the audio service takes a silent source back into
                // its pool, so a paused AudioSource cannot be relied on to still be ours.
                pausedClip = audioSource.clip;
                pausedAt = audioSource.time;
                audioSource.Stop();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            // The queue behind the paused clip is kept: it plays on once the resumed clip ends.
            if (IsPaused && pausedClip != null)
            {
                nextClip = pausedClip;
                nextClipDelay = 0f;
                resumeAt = pausedAt;
            }

            pausedClip = null;
            IsPaused = false;
        }

        private AudioClip pausedClip;
        private float pausedAt;
        private float resumeAt;

        // A paused clip neither holds progress nor runs its hold down: the player chose to stop listening, and a gate
        // that stayed shut until they resumed could leave them stuck. Resume brings back what was left of it.
        public bool ShouldAudioBlockProgress => blockRemaining > 0f && !IsPaused;
        public bool IsPlaying => clipQueue.Count > 0 || nextClip != null || audioSource != null && audioSource.isPlaying;
        public AudioClip CurrentClip => IsPlaying && audioSource != null ? audioSource.clip : null;

        private void Awake() => CosmicSimulation.Registry<VOManager>.Add(this);

        private void OnDestroy() => CosmicSimulation.Registry<VOManager>.Remove(this);

        private void Start()
        {
            audioService = AudioService.Instance;
        }

        private void Update()
        {
            if (blockRemaining > 0f && !IsPaused)
            {
                blockRemaining -= Time.deltaTime;
            }

            if (AudioHelper.FadingOut)
            {
                return;
            }

            if (nextClip)
            {
                nextClipDelay -= Time.deltaTime;

                if (nextClipDelay <= 0.0f)
                {
                    // A fade-out leaves the source at volume 0.
                    if (audioSource != null)
                    {
                        audioSource.volume = defaultVolume;
                    }
                    audioService.PlayClip(nextClip, out audioSource);
                    if (audioSource != null)
                    {
                        audioSource.volume *= level;
                        if (resumeAt > 0f)
                        {
                            audioSource.time = Mathf.Min(resumeAt, Mathf.Max(0f, nextClip.length - 0.1f));
                        }
                    }

                    resumeAt = 0f;
                    nextClip = null;
                    
                }
            }
            else if (!IsPaused && clipQueue != null && clipQueue.Count > 0 && (audioSource == null || !audioSource.isPlaying))
            {
                QueuedAudioClip queuedClip = clipQueue.Dequeue();

                if (queuedClip.clip && (queuedClip.allowReplay || !playedClips.Contains(queuedClip.clip.name)))
                {
                    nextClip = queuedClip.clip;
                    nextClipDelay = queuedClip.delay;
                    blockRemaining = queuedClip.blockProgress ? nextClip.length + queuedClip.delay : 0f;

                    playedClips.Add(nextClip.name);
                }
            }
        }

        // Single-argument overload so the intro's FlowManager stages can call it from a UnityEvent.
        public void PlayClip(AudioClip clip)
        {
            PlayClip(clip, 0.0f, false);
        }

        public bool PlayClip(QueuedAudioClip clip, bool replaceQueue = false)
        {
            return PlayClip(clip.clip, clip.delay, clip.allowReplay, replaceQueue, clip.blockProgress);
        }

        public bool PlayClip(AudioClip clip, float delay = 0.0f, bool allowReplay = false, bool replaceQueue = false, bool audioBlocksProgress = false)
        {
            bool clipWillPlay = false;

            // Gated at the queue rather than at the audio source: narration that was never queued cannot
            // resurface when something later drains the queue.
            if (VOEnabled && NarrationEnabled)
            {
                if (replaceQueue)
                {
                    clipQueue.Clear();
                }

                IsPaused = false;
                pausedClip = null;

                clipQueue.Enqueue(new QueuedAudioClip(clip, delay, allowReplay, audioBlocksProgress));

                clipWillPlay = true;
            }

            return clipWillPlay;
        }

        public void Stop(bool clearQueue = false)
        {
            if (clearQueue)
            {
                clipQueue.Clear();
            }

            nextClip = null;
            blockRemaining = 0f;
            IsPaused = false;
            pausedClip = null;

            // One fade at a time, so stacked coroutines cannot Stop() a later clip; a clip that would have
            // faded is skipped anyway once the queue is cleared.
            if (!AudioHelper.FadingOut)
            {
                StartCoroutine(AudioHelper.FadeOutOverSeconds(audioSource, FadeOutTime));
            }
        }
    }
}