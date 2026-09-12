using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Cosmic
{
    public enum Sfx
    {
        None,
        Focus,
        Select,
        PokeRelease,
        DockShow,
        DockHide,
        PopupOpen,
        PopupClose,
        PanelOpen,
        PanelClose,
        Grab,
        Release,
        Pull,
        Beam,
        GrowIn
    }

    [CreateAssetMenu(menuName = "Cosmic/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        // A class, not a struct, so a newly added entry starts at full volume instead of silent.
        [System.Serializable]
        public class Entry
        {
            public Sfx id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public Entry[] entries;
        public AudioMixerGroup sfx;
        public AudioMixerGroup voice;
        public AudioMixerGroup music;
        public AudioMixerGroup ambience;
        public AudioClip[] musicByRoom = new AudioClip[4];

        Dictionary<Sfx, Entry> byId;

        public AudioClip Clip(Sfx id) => Lookup(id)?.clip;

        public float Volume(Sfx id) => Lookup(id)?.volume ?? 1f;

        Entry Lookup(Sfx id)
        {
            if (byId == null)
            {
                byId = new Dictionary<Sfx, Entry>();
                if (entries != null)
                    foreach (var entry in entries)
                        if (entry != null)
                            byId[entry.id] = entry;
            }
            return byId.TryGetValue(id, out var found) ? found : null;
        }
    }
}
