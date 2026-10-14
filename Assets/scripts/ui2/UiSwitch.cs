// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>The on/off picture at the end of a settings row. The row is the press target; this only shows state.</summary>
    public class UiSwitch : MonoBehaviour
    {
        [SerializeField] private Image track = null;
        [SerializeField] private RectTransform knob = null;

        [SerializeField]
        [Tooltip("How far the knob sits from the middle of the track, in canvas units.")]
        private float travel = 15f;

        private bool _on;

        public bool On
        {
            get => _on;
            set
            {
                _on = value;
                track.color = value ? UiTheme.Accent : UiTheme.Divider;
                knob.anchoredPosition = new Vector2(value ? travel : -travel, 0f);
            }
        }
    }
}
