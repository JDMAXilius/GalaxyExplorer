// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>Whether the whole app is silenced: one stored answer, cached, applied at boot (CS-344).</summary>
    public static class Mute
    {
        private const string Key = "GalaxyExplorer.Muted";

        private static bool? _on;

        public static bool On
        {
            get => _on ??= PlayerPrefs.GetInt(Key, 0) == 1;
            set
            {
                if (On == value)
                {
                    return;
                }

                _on = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _on = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyStored() => AudioListener.volume = On ? 0f : 1f;
    }
}
