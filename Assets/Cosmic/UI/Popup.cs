using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cosmic
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Popup : MonoBehaviour
    {
        [SerializeField] Theme theme;
        [SerializeField] Button[] buttons = new Button[2];
        [SerializeField] Image[] fills = new Image[2];
        [SerializeField] TMP_Text[] labels = new TMP_Text[2];
        [SerializeField] float fadeSeconds = 0.25f;

        public event Action<Place, Layout> Picked;

        CanvasGroup group;
        Place place;
        Layout[] options = Array.Empty<Layout>();
        bool showing, ready;

        public Place Place => place;
        public bool Showing => showing;

        public void Open(Place owner, Layout active)
        {
            Init();
            place = owner;
            options = owner != null && owner.layouts != null ? owner.layouts : Array.Empty<Layout>();
            for (var i = 0; i < buttons.Length; i++)
            {
                var has = i < options.Length && options[i] != null;
                buttons[i].gameObject.SetActive(has);
                if (has && labels[i] != null) labels[i].text = options[i].title;
            }
            Mark(active);
            showing = true;
            group.blocksRaycasts = true;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PopupOpen, transform);
        }

        public void Close()
        {
            if (!showing) return;
            showing = false;
            group.blocksRaycasts = false;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PopupClose, transform);
        }

        public void Mark(Layout active)
        {
            if (theme == null) return;
            for (var i = 0; i < fills.Length; i++)
            {
                var on = i < options.Length && options[i] == active && active != null;
                if (fills[i] != null) fills[i].color = on ? theme.accentCyan : theme.tagDark;
                if (labels[i] != null) labels[i].color = on ? new Color(theme.tagDark.r, theme.tagDark.g, theme.tagDark.b, 1f) : theme.ink;
            }
        }

        void OnEnable()
        {
            Init();
            for (var i = 0; i < buttons.Length; i++)
            {
                var index = i;
                if (buttons[i] != null) buttons[i].onClick.AddListener(() => Pick(index));
            }
        }

        void OnDisable()
        {
            for (var i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].onClick.RemoveAllListeners();
        }

        void LateUpdate() => group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));

        void Pick(int index)
        {
            if (index >= options.Length) return;
            Mark(options[index]);
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            Picked?.Invoke(place, options[index]);
            Close();
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (theme != null && theme.font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
        }
    }
}
