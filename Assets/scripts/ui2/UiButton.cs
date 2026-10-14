// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The one pressable every UI 2.0 control is made of. <see cref="GEButton"/> on the same object does the
    /// pressing (poke, ray, controller and mouse all arrive there); this adds the hover and selected looks and
    /// a plain C# event, so a rail item, a dock item, a toggle and a menu row are the same component.
    /// </summary>
    [RequireComponent(typeof(GEButton))]
    public class UiButton : MonoBehaviour, IGEFocusHandler
    {
        [SerializeField] private Image plate = null;
        [SerializeField] private Image outline = null;
        [SerializeField] private Image glow = null;
        [SerializeField] private Image icon = null;
        [SerializeField] private TMP_Text label = null;

        [SerializeField]
        [Tooltip("Off for a picture (a thumbnail keeps its own colours when selected).")]
        private bool tintIcon = true;

        [SerializeField]
        [Tooltip("On for a control with its own fixed colours (a filled primary button): only the wash changes.")]
        private bool keepColours = false;

        private int _hovering;
        private bool _selected;

        public event Action Clicked;

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Refresh();
            }
        }

        public void SetLabel(string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private void Awake()
        {
            GetComponent<GEButton>().OnClick.AddListener(() => Clicked?.Invoke());
            Refresh();
        }

        private void OnDisable()
        {
            _hovering = 0;
            Refresh();
        }

        public void OnFocusEnter(GEFocusEventData eventData)
        {
            // Two hands can point at the same control; only the first arrival is a hover.
            if (_hovering++ == 0)
            {
                AudioService.Instance?.PlayClip(AudioId.Focus);
            }

            Refresh();
        }

        public void OnFocusExit(GEFocusEventData eventData)
        {
            _hovering = Mathf.Max(0, _hovering - 1);
            Refresh();
        }

        private void Refresh()
        {
            var hover = _hovering > 0;
            var ink = _selected ? UiTheme.Accent : UiTheme.Ink;

            if (plate != null)
            {
                plate.color = _selected ? UiTheme.SelectedWash : hover ? UiTheme.HoverWash : Color.clear;
            }

            if (glow != null)
            {
                glow.enabled = _selected;
            }

            if (outline != null)
            {
                outline.enabled = _selected || hover;
                outline.color = _selected ? UiTheme.Accent : new Color(1f, 1f, 1f, 0.4f);
            }

            if (label != null && !keepColours)
            {
                label.color = ink;
                label.fontWeight = _selected ? FontWeight.SemiBold : FontWeight.Medium;
            }

            if (icon != null && tintIcon && !keepColours)
            {
                icon.color = ink;
            }
        }
    }
}
