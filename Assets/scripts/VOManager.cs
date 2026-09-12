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

        // Narration-only mute (GDD 11, CS-076). Static rather than a field on the instance because the setting
        // outlives the component: the VOManager lives in a view scene that is loaded and unloaded under the
        // player, while the preference belongs to the player. Loaded lazily from PlayerPrefs on first read, so
        // it is correct before any Awake has run.
        private const string NarrationMutedPrefsKey = "GalaxyExplorer.NarrationMuted";

        private static bool narrationEnabled = true;
        private static bool narrationLoaded;

        /// <summary>
        /// Whether spoken narration plays. Off silences the voice alone and leaves music and effects be, which
        /// is the point: a player who knows the copy still wants the room to sound like something.
        /// </summary>
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
            foreach (var manager in FindObjectsByType<VOManager>(FindObjectsSortMode.None))
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

        private DateTime playStartTime;
        private float clipLength;

        private IAudioService audioService;
        

        public bool ShouldAudioBlockProgress => DateTime.UtcNow < playStartTime.AddSeconds(clipLength);
        public bool IsPlaying => clipQueue.Count > 0 || nextClip != null || audioSource != null && audioSource.isPlaying;
        public AudioClip CurrentClip => IsPlaying && audioSource != null ? audioSource.clip : null;

        private void Start()
        {
            audioService = AudioService.Instance;
        }

        private void Update()
        {
            if (AudioHelper.FadingOut)
            {
                // Don't process any of queue while fading out
                return;
            }

            if (nextClip)
            {
                nextClipDelay -= Time.deltaTime;

                if (nextClipDelay <= 0.0f)
                {
                    // Fading out sets volume to 0, ensure we're playing at the right
                    // volume every time
                    if (audioSource != null)
                    {
                        audioSource.volume = defaultVolume;
                    }
                    audioService.PlayClip(nextClip, out audioSource);
                    nextClip = null;
                    
                }
            }
            else if (clipQueue != null && clipQueue.Count > 0 && (audioSource == null || !audioSource.isPlaying))
            {
                QueuedAudioClip queuedClip = clipQueue.Dequeue();

                if (queuedClip.clip && (queuedClip.allowReplay || !playedClips.Contains(queuedClip.clip.name)))
                {
                    nextClip = queuedClip.clip;
                    nextClipDelay = queuedClip.delay;
                    if (queuedClip.blockProgress)
                    { 
                        playStartTime = DateTime.UtcNow;
                        clipLength = nextClip.length + queuedClip.delay;
                    }
                    else
                    {
                        playStartTime = DateTime.MinValue;
                        clipLength = 0;
                    }

                    playedClips.Add(nextClip.name);
                }
            }
        }

        // Play clip with no delay and dont replace in queue. This is hooked in the editor in FlowManager
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
            playStartTime = DateTime.MinValue;
            clipLength = 0;

            // Fade out the audio that's currently playing to stop it. Check here to
            // prevent coroutines from stacking up and calling Stop() on audioSource
            // at undesired times. Audio that would be faded out instead would just
            // be skipped over if the queue was cleared, which is what we want.
            if (!AudioHelper.FadingOut)
            {
                StartCoroutine(AudioHelper.FadeOutOverSeconds(audioSource, FadeOutTime));
            }
        }
    }
}