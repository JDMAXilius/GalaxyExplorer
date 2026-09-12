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

    // --- appended for CS-070; everything above keeps its number.
    //
    // Six of the ids above already point at the clip a GDD 9 slot asks for, so those slots reuse them rather
    // than growing this list. The names are the ones the original app gave them, which is why a couple read
    // oddly now:
    //   Focus                      hover tick (labels, dock tiles, points of interest)
    //   Select                     select / poke press, on every GEButton and every label
    //   CardSelect / CardDeselect  the body info panel opening and closing — a "card" is a panel here
    //   ToolboxShow / ToolBoxHide  the dock's layout pop-up opening and closing; the toolbox itself is gone,
    //                              and these two ids had no caller left, so the pop-up inherits them
    //   ForceDwell                 the force-pull beam (ui_tractor_beam)
    //   ManipulationStart / End    grab-and-hold and release (ui_forcegrab_hold / _release)
    PokeRelease,        // ui_touch_deselect — a fingertip coming back off a button
    DockShow,           // ui_handmenu_appear
    DockHide,           // ui_handmenu_disappear
    GrowIn,             // GDD 8.7 "soft whoosh". No clip owns this yet; see the sourcing ticket in BACKLOG.
                        // Unassigned is the right state for it: PlayClip no-ops on an id with no clip.
}

[Serializable]
public enum PlayOptions
{
    None = 0,
    PlayOnce,
    Loop
}
