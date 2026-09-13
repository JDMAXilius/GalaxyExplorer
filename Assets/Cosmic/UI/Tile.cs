using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cosmic
{
    public class Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Place place;
        public RectTransform move;
        public Image picture;
        public TMP_Text title;
        public TMP_Text second;
        public Image underline;
        public float liftMm = 6f;
        public float pressMm = 4f;

        public event Action<Tile> Clicked;

        float hover, press;
        bool hovered, pressed;

        public bool Active { set { if (underline != null) underline.enabled = value; } }

        void LateUpdate()
        {
            var lift = Mathf.MoveTowards(hover, hovered ? 1f : 0f, Time.unscaledDeltaTime / 0.12f);
            var push = Mathf.MoveTowards(press, pressed ? 1f : 0f, Time.unscaledDeltaTime / 0.08f);
            if (Mathf.Approximately(lift, hover) && Mathf.Approximately(push, press)) return;
            hover = lift;
            press = push;
            if (move != null) move.anchoredPosition3D = new Vector3(0f, hover * liftMm, press * pressMm);
            if (picture != null) picture.color = Color.Lerp(Color.white, new Color(1.15f, 1.15f, 1.15f), hover);
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Focus, transform); }

        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }

        public void OnPointerDown(PointerEventData e) => pressed = true;

        public void OnPointerUp(PointerEventData e) { pressed = false; if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PokeRelease, transform); }

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);
    }
}
