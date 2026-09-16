using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cosmic
{
    public class MicPicker : MonoBehaviour
    {
        [SerializeField] Button previous;
        [SerializeField] Button next;
        [SerializeField] TMP_Text deviceName;
        [SerializeField] RectTransform meter;
        [SerializeField] TMP_Text hint;
        [SerializeField] float meterWidthMm = 110f;
        [SerializeField] float meterGain = 8f;
        [SerializeField] float meterFallPerSecond = 2.5f;

        const int Rate = 16000;
        const string Default = "System default";

        readonly float[] window = new float[Rate / 20];
        AudioClip clip;
        string open;
        bool metering, live, asking;
        float level;

        public bool Metering => live;
        public float Level => level;

        public void Begin()
        {
            metering = true;
            Show();
        }

        public void End()
        {
            metering = false;
            Release();
            level = 0f;
            Draw();
        }

        public void Step(int direction)
        {
            var devices = Microphone.devices;
            var index = -1;
            for (var i = 0; i < devices.Length; i++)
                if (devices[i] == Prefs.Microphone) index = i;
            var count = devices.Length + 1;
            var slot = ((index + 1 + direction) % count + count) % count;
            Prefs.Microphone = slot == 0 ? string.Empty : devices[slot - 1];
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            Release();
            Show();
        }

        void OnEnable()
        {
            if (previous != null) previous.onClick.AddListener(Previous);
            if (next != null) next.onClick.AddListener(Next);
            Show();
        }

        void OnDisable()
        {
            if (previous != null) previous.onClick.RemoveListener(Previous);
            if (next != null) next.onClick.RemoveListener(Next);
            End();
        }

        void Previous() => Step(-1);

        void Next() => Step(1);

        void Update()
        {
            if (!metering) return;
            if (BeingMic.InUse) { Release(); Say("The guide is listening on this microphone."); }
            else if (!live && !asking) Acquire();
            if (live) Measure();
            Draw();
        }

        void Acquire()
        {
            if (Microphone.devices.Length == 0) { Say("No microphone found."); return; }
            if (!BeingMic.Permitted()) { StartCoroutine(AskOnce()); return; }
            open = BeingMic.Resolve();
            clip = Microphone.Start(open, true, 1, Rate);
            live = clip != null;
            Say(live ? "Speak to check it. The bar moves when the mic hears you." : "This microphone could not be opened.");
        }

        IEnumerator AskOnce()
        {
            asking = true;
            Say("Allow the microphone to check it.");
            yield return BeingMic.Ask(() => !metering);
            if (!BeingMic.Permitted()) Say("Microphone permission was not given.");
            else asking = false;
        }

        void Measure()
        {
            var position = Microphone.GetPosition(open);
            var start = position - window.Length;
            if (start < 0) start += clip.samples;
            clip.GetData(window, start);
            var target = Mathf.Clamp01(BeingMic.Rms(window) * meterGain);
            level = target > level ? target : Mathf.MoveTowards(level, target, meterFallPerSecond * Time.unscaledDeltaTime);
        }

        void Release()
        {
            if (!live) return;
            live = false;
            if (!BeingMic.InUse) Microphone.End(open);
            clip = null;
        }

        void Show()
        {
            if (deviceName == null) return;
            var chosen = BeingMic.Resolve();
            deviceName.text = chosen ?? (string.IsNullOrEmpty(Prefs.Microphone) ? Default : $"{Default} ({Prefs.Microphone} is unplugged)");
        }

        void Say(string text)
        {
            if (hint != null && hint.text != text) hint.text = text;
        }

        void Draw()
        {
            if (meter != null) meter.sizeDelta = new Vector2(meterWidthMm * level, meter.sizeDelta.y);
        }
    }
}
