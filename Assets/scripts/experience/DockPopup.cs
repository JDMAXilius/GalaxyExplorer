// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The little card that opens above a tile offering more than one way to see the same place: Schematic or
    /// Realistic for the orbit model, Solar Row or Relative Size for the planets, and the black hole's two views.
    ///
    /// It floats forty millimetres above the tile that owns it and closes as soon as a choice is made, so it
    /// never sits between the player and the dock. The option already in use is filled cyan.
    /// </summary>
    public class DockPopup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("One per option. Spare buttons are hidden when a module offers fewer.")]
        private List<GEButton> optionButtons = new List<GEButton>();

        [SerializeField] private List<TMP_Text> optionLabels = new List<TMP_Text>();
        [SerializeField] private List<Image> optionFills = new List<Image>();
        [SerializeField] private GEButton closeButton;

        [SerializeField]
        [Tooltip("Metres above the owning tile.")]
        private float liftMetres = 0.04f;

        private static readonly Color Accent = new Color(0.424f, 0.812f, 0.867f);
        private static readonly Color Plate = new Color(0.055f, 0.078f, 0.094f, 0.8f);
        private static readonly Color OnAccent = new Color(0.055f, 0.078f, 0.094f);

        private LayoutPreset[] _layouts = new LayoutPreset[0];
        private int _chosen = -1;
        private bool _opening;

        /// <summary>The tile this belongs to, or null when closed.</summary>
        public DockTile Owner { get; private set; }

        public bool IsOpen => Owner != null;

        /// <summary>Raised with the layout the player picked.</summary>
        public event System.Action<ExperienceModule, LayoutPreset> LayoutChosen;

        private void Awake()
        {
            for (var i = 0; i < optionButtons.Count; i++)
            {
                var index = i;
                if (optionButtons[i] != null)
                {
                    optionButtons[i].OnClick.AddListener(() => Pick(index));
                }
            }

            if (closeButton != null)
            {
                closeButton.OnClick.AddListener(Close);
            }

            // Start closed — but only if we are not being opened right now. A pop-up saved inactive in the
            // scene does not run Awake until something activates it, and the thing that activates it is Open.
            // Deactivating unconditionally here shut the pop-up in the same frame it was asked for, which is
            // why the world dock's layout pop-up could never appear.
            if (!_opening)
            {
                gameObject.SetActive(false);
            }
        }

        public void Open(DockTile tile)
        {
            if (tile == null || tile.Module == null || !tile.Module.HasLayoutChoice)
            {
                Close();
                return;
            }

            _opening = true;
            Owner = tile;
            _layouts = tile.Module.Layouts;
            _chosen = _layouts.Length > 0 ? 0 : -1;

            for (var i = 0; i < optionButtons.Count; i++)
            {
                var used = i < _layouts.Length;
                if (optionButtons[i] != null)
                {
                    optionButtons[i].gameObject.SetActive(used);
                }

                if (used && i < optionLabels.Count && optionLabels[i] != null)
                {
                    optionLabels[i].text = _layouts[i].DisplayName;
                }
            }

            gameObject.SetActive(true);
            PlaceAbove(tile);
            Repaint();

            // ToolboxShow/Hide are the original app's names for this pair of clips; the toolbox is gone and
            // this pop-up is what they describe now. See the mapping note on AudioId.
            AudioService.Instance?.PlayClip(AudioId.ToolboxShow);
        }

        public void Close()
        {
            // Close is also the "make sure it is shut" call — DockController.SetVisible(false) and a tile with
            // no layout choice both land here — so only a pop-up that was actually open sounds a close.
            var wasOpen = IsOpen;

            Owner = null;
            _opening = false;
            gameObject.SetActive(false);

            if (wasOpen)
            {
                // No target: the pooled source would be parented to this object and cut off by the
                // SetActive(false) above.
                AudioService.Instance?.PlayClip(AudioId.ToolBoxHide);
            }
        }

        /// <summary>Marks which option is in use, for when a layout changes from somewhere else.</summary>
        public void SetChosen(LayoutPreset layout)
        {
            _chosen = System.Array.IndexOf(_layouts, layout);
            Repaint();
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= _layouts.Length || Owner == null)
            {
                return;
            }

            _chosen = index;
            var module = Owner.Module;
            var layout = _layouts[index];
            Repaint();

            // Closing first keeps the pop-up from sitting in front of the layout it just started.
            Close();
            LayoutChosen?.Invoke(module, layout);
        }

        private void Repaint()
        {
            for (var i = 0; i < optionFills.Count; i++)
            {
                if (optionFills[i] != null)
                {
                    optionFills[i].color = i == _chosen ? Accent : Plate;
                }

                if (i < optionLabels.Count && optionLabels[i] != null)
                {
                    optionLabels[i].color = i == _chosen ? OnAccent : Color.white;
                }
            }
        }

        private void PlaceAbove(DockTile tile)
        {
            var t = tile.transform;
            transform.SetPositionAndRotation(t.position + t.up * liftMetres, t.rotation);
        }
    }
}
