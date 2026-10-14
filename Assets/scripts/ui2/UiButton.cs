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

        [SerializeField]
        [Tooltip("A segment or menu choice: this fill shows when selected and the label turns dark on it.")]
        private Image selectedFill = null;

        private int _hovering;
        private bool _selected;
        private bool _interactable = true;

        public event Action Clicked;

        private static readonly System.Collections.Generic.List<UiButton> Pressed =
            new System.Collections.Generic.List<UiButton>();

        /// <summary>
        /// Delivers the presses of the last frame. A press often hides the control that was pressed, and
        /// <see cref="GEButton"/> starts its cool-down coroutine straight after its listeners, which is an
        /// error on an object that has just been switched off; so listeners hear about it a frame later.
        /// </summary>
        public static void Deliver()
        {
            if (Pressed.Count == 0)
            {
                return;
            }

            var batch = Pressed.ToArray();
            Pressed.Clear();
            foreach (var button in batch)
            {
                if (button != null)
                {
                    button.Clicked?.Invoke();
                }
            }
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Refresh();
            }
        }

        /// <summary>Off: the control stays drawn, dimmed, and cannot be pressed.</summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                foreach (var hit in GetComponents<Collider>())
                {
                    hit.enabled = value;
                }

                var group = GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = value ? 1f : 0.45f;
                }
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
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
            GetComponent<GEButton>().OnClick.AddListener(() => Pressed.Add(this));
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
            if (selectedFill != null)
            {
                selectedFill.enabled = _selected;
                ink = _selected ? UiTheme.Deep : UiTheme.Ink;
            }

            if (plate != null)
            {
                plate.color = _selected && selectedFill == null ? UiTheme.SelectedWash
                    : hover && !_selected ? UiTheme.HoverWash : Color.clear;
            }

            if (glow != null)
            {
                glow.gameObject.SetActive(_selected);
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
