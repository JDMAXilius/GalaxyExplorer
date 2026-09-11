using System;
using System.Collections.Generic;
using Pools;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Plays the app's UI and effect sounds (by <see cref="AudioId"/> or clip) from pooled audio sources and drives the
/// music mixer snapshots. Created on first use from the "AudioServiceProfile" asset in a Resources folder.
/// </summary>
public class AudioService : IAudioService
{
    private const string ProfileResourceName = "AudioServiceProfile";

    private static float SameClipCoolDownTime = .05f;
    private static AudioService instance;

    private Dictionary<AudioId, AudioInfo> audioClipCache;
    private Dictionary<Transform, List<PoolableAudioSource>> playingCache;
    private Dictionary<string, DateTime> lastPlayedTimes;
    private ObjectPooler objectPooler;
    private Transform mainCameraTransform;
    private AudioServiceProfile audioProfile;

    /// <summary>The app-wide audio service; null outside play mode.</summary>
    public static IAudioService Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                instance = new AudioService(Resources.Load<AudioServiceProfile>(ProfileResourceName));
            }

            return instance;
        }
    }

    // Play mode can start without a domain reload; drop the service from the previous session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private AudioService(AudioServiceProfile profile)
    {
        audioProfile = profile;
        if (audioProfile == null)
        {
            Debug.LogError($"AudioService: no {ProfileResourceName} asset found in a Resources folder; sounds are disabled.");
        }

        playingCache = new Dictionary<Transform, List<PoolableAudioSource>>();
        lastPlayedTimes = new Dictionary<string, DateTime>();
        if (audioProfile != null)
        {
            audioClipCache = new Dictionary<AudioId, AudioInfo>();
            foreach (var audioInfo in audioProfile.audioClips)
            {
                if (!audioClipCache.ContainsKey(audioInfo.audioId))
                {
                    audioClipCache.Add(audioInfo.audioId,audioInfo);
                }
            }
        }

        objectPooler = ObjectPooler.CreateObjectPool<PoolableAudioSource>(8);

        GetTarget(null);

    }

    public void PlayClip(AudioId audioId, Transform target, float volume)
    {
        if (audioClipCache != null && audioClipCache.ContainsKey(audioId))
        {
            var audioInfo = audioClipCache[audioId];
            PlayClip(audioInfo.clip, target, volume == -1 ? audioInfo.volume : volume);
        }
    }

    public void PlayClip(AudioId audioId, out AudioSource playedSource, Transform target, float volume)
    {
        playedSource = null;
        if (audioClipCache != null && audioClipCache.ContainsKey(audioId))
        {
            var audioInfo = audioClipCache[audioId];
            PlayClip(audioInfo.clip, out playedSource, target, volume == -1 ? audioInfo.volume : volume, PlayOptions.PlayOnce);
        }
    }

    public void PlayClip(AudioClip clip, Transform target, float volume)
    {
        AudioSource source;
        PlayClip(clip, out source, target, volume, PlayOptions.PlayOnce);
    }

    public void PlayClip(AudioClip clip, out AudioSource playedSource, Transform target, float volume, PlayOptions playOptions)
    {
        if (clip == null)
        {
            playedSource = null;
            return;
        }

        if (lastPlayedTimes.ContainsKey(clip.name))
        {
            var lastPlayTime = lastPlayedTimes[clip.name];
            if ((DateTime.UtcNow - lastPlayTime).TotalSeconds < SameClipCoolDownTime)
            {
                playedSource = null;
                return;
            }
            
        }
        var source = GetTargetSource(GetTarget(target));
        playedSource = source.AudioSource;
        source.PlayClip(clip, playOptions:playOptions);
        lastPlayedTimes[clip.name] = DateTime.UtcNow;
    }

    public bool TryTransitionMixerSnapshot(string name, float transitionTime)
    {
        bool transitioned = false;

        if (audioProfile != null && audioProfile.musicAudioMixer)
        {
            AudioMixerSnapshot snapshot = audioProfile.musicAudioMixer.FindSnapshot(name);

            if (snapshot)
            {
                snapshot.TransitionTo(transitionTime);
                transitioned = true;
            }
            else
            {
                Debug.LogWarning("Couldn't find AudioMixer Snapshot with name " + name);
            }
        }

        return transitioned;
    }

    private Transform GetTarget(Transform target)
    {
        if (mainCameraTransform == null)
        {
            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
        }
        return target == null ? mainCameraTransform : target;
    }

    private PoolableAudioSource GetTargetSource(Transform target)
    {
        PoolableAudioSource source = null;
        List<PoolableAudioSource> sources = new List<PoolableAudioSource>();;
        if (playingCache.ContainsKey(target))
        {
            sources = playingCache[target];
            foreach (var poolableAudioSource in sources)
            {
                if (poolableAudioSource != null && !poolableAudioSource.IsPlaying)
                {
                    source = poolableAudioSource;
                    break;
                }
            }
        }
        // Equals operator is overriden! will return true if audio source pool is not active!
        if (source == null)
        {
            source = objectPooler.GetNextObject<PoolableAudioSource>(parent:target);
            sources.Add(source);
            source.onReturnToPool += OnPoolableAudioSourceReturned;
        }
        playingCache[target] = sources;
        return source;
    }

    private void OnPoolableAudioSourceReturned(APoolable source, Transform parent)
    {
        var poolableAudioSource = source as PoolableAudioSource;
        if (poolableAudioSource == null)
        {
            return;
        }
        if(playingCache.TryGetValue(parent, out var sources))
        {
            sources.Remove(poolableAudioSource);
        }
    }
}
