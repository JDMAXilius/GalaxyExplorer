using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cosmic
{
    public class Label : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum Style { Card, Name, Moon }

        [SerializeField] Style style = Style.Card;
        [SerializeField] Theme theme;
        [SerializeField] Place destination;
        [SerializeField] Image plate;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text secondText;
        [SerializeField] Image leader;
        [SerializeField] GameObject outline;
        [SerializeField] RectTransform grow;
        [SerializeField] float hoverScale = 1.15f;
        [SerializeField] float hoverSeconds = 0.12f;
        [SerializeField] float clickCooldownSeconds = 0.3f;
        [SerializeField] float liftMm = 30f;
        [SerializeField] bool billboard = true;

        public event Action<Label> Picked;

        Camera cam;
        float hover, lastClick = -10f;
        bool hovered, selected, held, ready;

        public Place Destination => destination;
        public Style Kind => style;
        public bool Selected { get => selected; set { selected = value; Init(); if (outline != null) outline.SetActive(value); } }
        public bool Held { get => held; set { held = value; Init(); Size(); } }

        public void Set(string name, string second = null)
        {
            Init();
            if (nameText != null) nameText.text = name ?? string.Empty;
            if (secondText != null)
            {
                var has = !string.IsNullOrEmpty(second);
                secondText.gameObject.SetActive(has);
                secondText.text = has ? second.ToUpperInvariant() : string.Empty;
            }
            Fit();
        }

        public void Set(Place place)
        {
            destination = place;
            if (place != null) Set(place.title, place.subtitle);
        }

        public void Set(Body body)
        {
            if (body != null) Set(body.title, style == Style.Card ? body.subtitle : null);
        }

        void OnEnable()
        {
            Init();
            Paint(0f);
            if (Prefs.LabelsVisible == false && style == Style.Name) gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var goal = hovered ? 1f : 0f;
            if (!Mathf.Approximately(hover, goal))
            {
                hover = Mathf.MoveTowards(hover, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, hoverSeconds));
                Paint(hover);
            }
            if (!billboard) return;
            if (cam == null) cam = Camera.main;
            if (cam != null) Tween.Billboard(transform, cam.transform);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (style == Style.Name) return;
            hovered = true;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Focus, transform);
        }

        public void OnPointerExit(PointerEventData e) => hovered = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (style == Style.Name || Time.unscaledTime - lastClick < clickCooldownSeconds) return;
            lastClick = Time.unscaledTime;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            Picked?.Invoke(this);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            if (grow == null) grow = transform as RectTransform;
            if (outline != null) outline.SetActive(selected);
            if (theme != null && nameText != null && theme.font != null) nameText.font = theme.font;
            if (theme != null && secondText != null && theme.font != null) secondText.font = theme.font;
            Size();
            if (leader != null)
            {
                leader.rectTransform.anchoredPosition = new Vector2(0f, -liftMm);
                leader.rectTransform.sizeDelta = new Vector2(leader.rectTransform.sizeDelta.x, liftMm);
            }
        }

        void Size()
        {
            if (theme == null || nameText == null) return;
            nameText.fontSize = style == Style.Moon ? (held ? theme.moonLargeMm : theme.moonSmallMm) : style == Style.Name ? theme.labelMm : theme.tagMm;
            nameText.fontStyle = style == Style.Moon && held ? FontStyles.Bold : FontStyles.Normal;
            if (secondText != null) secondText.fontSize = theme.tagMm * 0.72f;
        }

        void Fit()
        {
            if (style != Style.Card || plate == null || theme == null) return;
            var pad = 10f;
            var width = nameText != null ? nameText.preferredWidth + pad : 0f;
            if (secondText != null && secondText.gameObject.activeSelf) width = Mathf.Max(width, secondText.preferredWidth + pad);
            var size = plate.rectTransform.sizeDelta;
            size.x = Mathf.Max(theme.tagSizeMm.x, width);
            size.y = theme.tagSizeMm.y;
            plate.rectTransform.sizeDelta = size;
        }

        void Paint(float t)
        {
            if (grow != null) grow.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, t);
            if (theme == null) return;
            var dark = new Color(theme.tagDark.r, theme.tagDark.g, theme.tagDark.b, 1f);
            if (plate != null) plate.color = Color.Lerp(theme.tagDark, theme.accentCyan, t);
            if (nameText != null) nameText.color = Color.Lerp(theme.ink, dark, t);
            if (secondText != null) secondText.color = Color.Lerp(theme.inkSecondary, dark, t);
        }
    }
}
