using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cosmic
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Utility : MonoBehaviour
    {
        [SerializeField] Theme theme;
        [SerializeField] Slider scale;
        [SerializeField] TMP_Text scaleValue;
        [SerializeField] Toggle mute;
        [SerializeField] Toggle voice;
        [SerializeField] Button textSize;
        [SerializeField] TMP_Text textSizeValue;
        [SerializeField] Button about;
        [SerializeField] Button close;
        [SerializeField] float fadeSeconds = 0.25f;
        [SerializeField] float minScale = 0.5f;
        [SerializeField] float maxScale = 2f;

        public event Action<float> Scaled;
        public event Action AboutRequested;

        CanvasGroup group;
        bool showing, ready, syncing;

        public bool Showing => showing;
        public float Scale => scale != null ? scale.value : 1f;

        public void Open()
        {
            Init();
            showing = true;
            group.blocksRaycasts = true;
            Sync();
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PopupOpen, transform);
        }

        public void Close()
        {
            if (!showing) return;
            showing = false;
            group.blocksRaycasts = false;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PopupClose, transform);
        }

        public void Toggle()
        {
            if (showing) Close();
            else Open();
        }

        public void ResetScale()
        {
            if (scale != null) scale.value = 1f;
        }

        void OnEnable()
        {
            Init();
            if (scale != null) scale.onValueChanged.AddListener(OnScale);
            if (mute != null) mute.onValueChanged.AddListener(OnMute);
            if (voice != null) voice.onValueChanged.AddListener(OnVoice);
            if (textSize != null) textSize.onClick.AddListener(OnTextSize);
            if (about != null) about.onClick.AddListener(OnAbout);
            if (close != null) close.onClick.AddListener(Close);
            Prefs.Changed += Sync;
            Sync();
        }

        void OnDisable()
        {
            if (scale != null) scale.onValueChanged.RemoveListener(OnScale);
            if (mute != null) mute.onValueChanged.RemoveListener(OnMute);
            if (voice != null) voice.onValueChanged.RemoveListener(OnVoice);
            if (textSize != null) textSize.onClick.RemoveListener(OnTextSize);
            if (about != null) about.onClick.RemoveListener(OnAbout);
            if (close != null) close.onClick.RemoveListener(Close);
            Prefs.Changed -= Sync;
        }

        void LateUpdate() => group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));

        void OnScale(float value)
        {
            if (scaleValue != null) scaleValue.text = $"x{value:0.0}";
            if (!syncing) Scaled?.Invoke(value);
        }

        void OnMute(bool on) { if (!syncing) Prefs.Muted = on; }

        void OnVoice(bool on) { if (!syncing) Prefs.VoiceMuted = on; }

        void OnTextSize()
        {
            Prefs.NextTextScale();
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
        }

        void OnAbout()
        {
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            AboutRequested?.Invoke();
        }

        void Sync()
        {
            syncing = true;
            if (mute != null) mute.isOn = Prefs.Muted;
            if (voice != null) voice.isOn = Prefs.VoiceMuted;
            if (textSizeValue != null) textSizeValue.text = $"x{Prefs.TextScale:0.00}";
            if (scaleValue != null && scale != null) scaleValue.text = $"x{scale.value:0.0}";
            syncing = false;
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (scale != null) { scale.minValue = minScale; scale.maxValue = maxScale; scale.value = 1f; }
            if (theme != null && theme.font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
        }
    }
}
