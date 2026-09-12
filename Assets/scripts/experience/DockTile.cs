// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// One place you can go, drawn as a picture of it with the name written across the bottom.
    ///
    /// Four states, all of them movement rather than colour, because the tile is mostly photograph and a tint
    /// would fight the image: idle sits flush, hover lifts six millimetres toward the player and brightens,
    /// a poke pushes four millimetres in, and the open one keeps a cyan underline along its bottom edge.
    ///
    /// Only the three tiles whose module offers a layout choice show a second line, and only those open a
    /// <see cref="DockPopup"/>; the rest switch straight over.
    /// </summary>
    [DisallowMultipleComponent]
    public class DockTile : MonoBehaviour, IGEFocusHandler
    {
        private const float HoverLiftMetres = 0.006f;
        private const float PressDepthMetres = 0.004f;
        private const float MoveSeconds = 0.09f;

        [SerializeField] private Image thumbnail;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text secondLine;
        [SerializeField] private GameObject activeUnderline;
        [SerializeField] private GEButton button;

        [SerializeField]
        [Tooltip("Moved for the hover lift and the press. Defaults to this transform.")]
        private Transform moveTarget;

        [SerializeField]
        [Tooltip("Raised when the tile is chosen.")]
        private UnityEvent<DockTile> onChosen = new UnityEvent<DockTile>();

        private Transform _move;
        private Vector3 _restLocalPosition;
        private float _lift;      // 0 flush, 1 hovered
        private float _press;     // 0 out, 1 pushed in
        private bool _hovered;

        public ExperienceModule Module { get; private set; }
        public UnityEvent<DockTile> OnChosen => onChosen;
        public bool IsActive { get; private set; }

        private void Awake()
        {
            _move = moveTarget != null ? moveTarget : transform;
            _restLocalPosition = _move.localPosition;

            if (button == null)
            {
                button = GetComponentInChildren<GEButton>();
            }

            if (button != null)
            {
                button.OnClick.AddListener(Choose);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.OnClick.RemoveListener(Choose);
            }
        }

        public void Bind(ExperienceModule module)
        {
            Module = module;
            if (module == null)
            {
                return;
            }

            if (thumbnail != null)
            {
                thumbnail.sprite = module.DockThumbnail;
                // Until CS-031 renders the real pictures, a tile with no thumbnail keeps its placeholder fill
                // rather than showing a white rectangle.
                thumbnail.enabled = module.DockThumbnail != null;
            }

            if (nameLabel != null)
            {
                nameLabel.text = module.DisplayName;
            }

            if (secondLine != null)
            {
                var line = module.SecondLine;
                secondLine.gameObject.SetActive(!string.IsNullOrEmpty(line));
                if (!string.IsNullOrEmpty(line))
                {
                    secondLine.text = line;
                }
            }

            SetActiveTile(false);
        }

        public void SetActiveTile(bool active)
        {
            IsActive = active;
            if (activeUnderline != null)
            {
                activeUnderline.SetActive(active);
            }
        }

        public void Choose()
        {
            if (isActiveAndEnabled)
            {
                _press = 1f;
                onChosen.Invoke(this);
            }
        }

        public void OnFocusEnter(GEFocusEventData eventData) => _hovered = true;

        public void OnFocusExit(GEFocusEventData eventData) => _hovered = false;

        private void Update()
        {
            var wantLift = _hovered ? 1f : 0f;
            var step = Time.deltaTime / MoveSeconds;
            _lift = Mathf.MoveTowards(_lift, wantLift, step);
            _press = Mathf.MoveTowards(_press, 0f, step);

            // The dock plate faces the player, so "toward the player" is the tile's own -Z.
            var z = -_lift * HoverLiftMetres + _press * PressDepthMetres;
            _move.localPosition = _restLocalPosition + new Vector3(0f, 0f, z);
        }
    }
}
