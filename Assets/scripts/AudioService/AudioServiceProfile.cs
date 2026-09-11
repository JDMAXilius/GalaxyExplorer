using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sound settings for <see cref="AudioService"/>. The asset must be named "AudioServiceProfile" and live in a
/// Resources folder so the service can load it on first use.
/// </summary>
[CreateAssetMenu(menuName = "Galaxy Explorer/Audio Service Profile", fileName = "AudioServiceProfile")]
public class AudioServiceProfile : ScriptableObject
{
    [SerializeField] public AudioMixer musicAudioMixer;
    [SerializeField] public List<AudioInfo> audioClips;
}

[Serializable]
public class AudioInfo
{
    public AudioId audioId;
    public AudioClip clip;
    [Range(0, 2)] public float volume = 1;
}

[Serializable]
public enum AudioId
{
    // Don't reorder or insert new items into enum, always add them to the end.
    // Unity Serializes enums as ints which would result in a
    // different sound being played if order is changed.
    None = 0,
    Focus,
    Select,
    CardSelect,
    CardDeselect,
    ToolboxShow,
    ToolBoxHide,
    ForcePull,
    ForceDwell,
    ManipulationStart,
    ManipulationEnd,
}

[Serializable]
public enum PlayOptions
{
    None = 0,
    PlayOnce,
    Loop
}
