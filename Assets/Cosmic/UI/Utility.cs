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
        [SerializeField] TMP_Text[] toggleLabels = Array.Empty<TMP_Text>();
        [SerializeField] Button[] textSizes = Array.Empty<Button>();
        [SerializeField] TMP_Text[] textSizeLabels = Array.Empty<TMP_Text>();
        [SerializeField] MicPicker mic;
        [SerializeField] Button about;
        [SerializeField] Button quit;
        [SerializeField] TMP_Text quitLabel;
        [SerializeField] Button close;
        [SerializeField] float fadeSeconds = 0.25f;
        [SerializeField] float quitArmSeconds = 3f;
        [SerializeField] float minScale = 0.5f;
        [SerializeField] float maxScale = 2f;

        public event Action<float> Scaled;
        public event Action AboutRequested;

        const string QuitIdle = "Quit";
        const string QuitArmed = "Tap again to quit";

        CanvasGroup group;
        Image[] textSizeFaces;
        float armedUntil;
        bool showing, ready, syncing;

        public bool Showing => showing;
        public float Scale => scale != null ? scale.value : 1f;

        public void Open()
        {
            Init();
            showing = true;
            group.blocksRaycasts = true;
            Sync();
            if (mic != null) mic.Begin();
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PopupOpen, transform);
        }

        public void Close()
        {
            if (!showing) return;
            showing = false;
            group.blocksRaycasts = false;
            armedUntil = 0f;
            if (mic != null) mic.End();
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
            for (var i = 0; i < textSizes.Length; i++) { var step = i; textSizes[i].onClick.AddListener(() => OnTextSize(step)); }
            if (about != null) about.onClick.AddListener(OnAbout);
            if (quit != null) quit.onClick.AddListener(OnQuit);
            if (close != null) close.onClick.AddListener(Close);
            Prefs.Changed += Sync;
            Sync();
        }

        void OnDisable()
        {
            if (scale != null) scale.onValueChanged.RemoveListener(OnScale);
            if (mute != null) mute.onValueChanged.RemoveListener(OnMute);
            if (voice != null) voice.onValueChanged.RemoveListener(OnVoice);
            foreach (var button in textSizes) button.onClick.RemoveAllListeners();
            if (about != null) about.onClick.RemoveListener(OnAbout);
            if (quit != null) quit.onClick.RemoveListener(OnQuit);
            if (close != null) close.onClick.RemoveListener(Close);
            Prefs.Changed -= Sync;
        }

        void LateUpdate()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            if (quitLabel != null) quitLabel.text = Time.unscaledTime < armedUntil ? QuitArmed : QuitIdle;
        }

        void OnScale(float value)
        {
            if (scaleValue != null) scaleValue.text = $"x{value:0.0}";
            if (!syncing) Scaled?.Invoke(value);
        }

        void OnMute(bool on) { if (!syncing) Prefs.Muted = on; }

        void OnVoice(bool on) { if (!syncing) Prefs.VoiceMuted = on; }

        void OnTextSize(int step)
        {
            Prefs.TextScale = Prefs.TextScales[Mathf.Clamp(step, 0, Prefs.TextScales.Length - 1)];
            Sync();
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
        }

        void OnQuit()
        {
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            if (Time.unscaledTime >= armedUntil) { armedUntil = Time.unscaledTime + quitArmSeconds; return; }
            armedUntil = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            Paint(0, mute != null && mute.isOn);
            Paint(1, voice != null && voice.isOn);
            for (var i = 0; i < textSizes.Length && i < Prefs.TextScales.Length; i++)
            {
                var on = Mathf.Approximately(Prefs.TextScales[i], Prefs.TextScale);
                if (textSizeFaces[i] != null) textSizeFaces[i].color = on ? Accent : Faint;
                if (i < textSizeLabels.Length && textSizeLabels[i] != null) textSizeLabels[i].color = on ? Dark : Ink;
            }
            if (scaleValue != null && scale != null) scaleValue.text = $"x{scale.value:0.0}";
            syncing = false;
        }

        static readonly Color Faint = new Color(1f, 1f, 1f, 0.12f);

        Color Ink => theme != null ? theme.ink : Color.white;
        Color Accent => theme != null ? theme.accentCyan : Color.cyan;
        Color Dark => theme != null ? new Color(theme.tagDark.r, theme.tagDark.g, theme.tagDark.b, 1f) : Color.black;

        void Paint(int index, bool on)
        {
            if (index < toggleLabels.Length && toggleLabels[index] != null) toggleLabels[index].color = on ? Dark : Ink;
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            textSizeFaces = new Image[textSizes.Length];
            for (var i = 0; i < textSizes.Length; i++) textSizeFaces[i] = textSizes[i] != null ? textSizes[i].targetGraphic as Image : null;
            if (scale != null) { scale.minValue = minScale; scale.maxValue = maxScale; scale.value = 1f; }
            if (theme != null && theme.font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
        }
    }
}
