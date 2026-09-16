using System;
using UnityEngine;

namespace Cosmic
{
    public static class Prefs
    {
        public static readonly float[] TextScales = { 1f, 1.25f, 1.5f };

        const string MutedKey = "Cosmic.Muted";
        const string VoiceMutedKey = "Cosmic.VoiceMuted";
        const string TextScaleKey = "Cosmic.TextScale";
        const string HintsSeenKey = "Cosmic.HintsSeen";
        const string LabelsVisibleKey = "Cosmic.LabelsVisible";
        const string MicrophoneKey = "Cosmic.Microphone";

        public static event Action Changed;

        static bool loaded;
        static bool muted;
        static bool voiceMuted;
        static float textScale = 1f;
        static bool hintsSeen;
        static bool labelsVisible = true;
        static string microphone = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            loaded = false;
        }

        public static bool Muted
        {
            get { Load(); return muted; }
            set
            {
                Load();
                if (muted == value) return;
                muted = value;
                Write(MutedKey, value);
            }
        }

        public static bool VoiceMuted
        {
            get { Load(); return voiceMuted; }
            set
            {
                Load();
                if (voiceMuted == value) return;
                voiceMuted = value;
                Write(VoiceMutedKey, value);
            }
        }

        public static float TextScale
        {
            get { Load(); return textScale; }
            set
            {
                Load();
                var snapped = Snap(value);
                if (Mathf.Approximately(textScale, snapped)) return;
                textScale = snapped;
                PlayerPrefs.SetFloat(TextScaleKey, snapped);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool HintsSeen
        {
            get { Load(); return hintsSeen; }
            set
            {
                Load();
                if (hintsSeen == value) return;
                hintsSeen = value;
                Write(HintsSeenKey, value);
            }
        }

        public static bool LabelsVisible
        {
            get { Load(); return labelsVisible; }
            set
            {
                Load();
                if (labelsVisible == value) return;
                labelsVisible = value;
                Write(LabelsVisibleKey, value);
            }
        }

        public static string Microphone
        {
            get { Load(); return microphone; }
            set
            {
                Load();
                value ??= string.Empty;
                if (microphone == value) return;
                microphone = value;
                PlayerPrefs.SetString(MicrophoneKey, value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static float NextTextScale()
        {
            Load();
            for (var i = 0; i < TextScales.Length; i++)
                if (Mathf.Approximately(TextScales[i], textScale))
                    return TextScales[(i + 1) % TextScales.Length];
            return TextScales[0];
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            voiceMuted = PlayerPrefs.GetInt(VoiceMutedKey, 0) == 1;
            textScale = Snap(PlayerPrefs.GetFloat(TextScaleKey, 1f));
            hintsSeen = PlayerPrefs.GetInt(HintsSeenKey, 0) == 1;
            labelsVisible = PlayerPrefs.GetInt(LabelsVisibleKey, 1) == 1;
            microphone = PlayerPrefs.GetString(MicrophoneKey, string.Empty);
        }

        static void Write(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static float Snap(float value)
        {
            var best = TextScales[0];
            for (var i = 1; i < TextScales.Length; i++)
                if (Mathf.Abs(TextScales[i] - value) < Mathf.Abs(best - value))
                    best = TextScales[i];
            return best;
        }
    }
}
