// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// A pickable name over a place you can go (Milky Way tags, orbit-model labels). It only raises
    /// <see cref="OnPicked"/>: whoever listens decides whether that is a scene or an overlay.
    /// </summary>
    [DisallowMultipleComponent]
    public class LabelButton : MonoBehaviour, IGEPointerHandler, IGEFocusHandler
    {
        private const float HoverScale = 1.15f;
        private const float HoverSeconds = 0.12f;
        private const float ClickCooldown = 0.3f;

        [SerializeField]
        [Tooltip("The pill behind the text. Left empty for a plain label with no plate.")]
        private Graphic pill;

        [SerializeField]
        [Tooltip("The name. Its colour flips to dark on hover so it reads against the cyan fill.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("The line under the name - a category or catalogue number, in caps. Optional: a label with " +
                 "nothing to say on a second line hides this and centres the name on its own.")]
        private TMP_Text secondLine;

        [SerializeField]
        [Tooltip("Hairline running back to the point this names. Optional.")]
        private Graphic leader;

        [SerializeField]
        [Tooltip("Scaled on hover. Defaults to this transform.")]
        private Transform growTarget;

        [SerializeField]
        [Tooltip("Shown while this destination is the open one. Optional.")]
        private GameObject selectedOutline;

        [SerializeField]
        [Tooltip("What this label opens. Read by whatever listens to OnPicked.")]
        private ExperienceModule destination;

        [SerializeField]
        [Tooltip("Raised on pinch or click, after the cool-down.")]
        private UnityEvent<LabelButton> onPicked = new UnityEvent<LabelButton>();

        private Color _idleFill = new Color(0.055f, 0.078f, 0.094f, 0.8f); // surface/plate
        private Color _accent = new Color(0.424f, 0.812f, 0.867f);          // accent/cyan
        private Color _idleText = Color.white;
        private Color _idleSecondText = new Color(0.620f, 0.722f, 0.769f); // ink/secondary
        private Color _hoverText = new Color(0.055f, 0.078f, 0.094f);

        private bool _ready;
        private Transform _grow;
        private Vector3 _baseScale;
        private float _hover;      // 0 idle, 1 hovered
        private bool _hovered;
        private float _lastClick = -1f;

        public ExperienceModule Destination => destination;

        /// <summary>Raised when the player picks this label.</summary>
        public UnityEvent<LabelButton> OnPicked => onPicked;

        /// <summary>True while this label's destination is the open one.</summary>
        public bool IsSelected { get; private set; }

        private void Awake()
        {
            EnsureInit();

            // Poke interactors refuse an interactable with no XRPokeFilter (CS-276); the prefab has one, a label
            // built any other way gets it here.
            var interactable = GetComponentInChildren<GEInteractable>(true);
            if (interactable != null)
            {
                PokeSupport.AddPokeFilter(interactable.gameObject, interactable.GetComponent<Collider>(), interactable);
            }
        }

        // Labels are often bound and selected the frame they spawn, before Awake, so every entry point calls this.
        private void EnsureInit()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _grow = growTarget != null ? growTarget : transform;
            _baseScale = _grow.localScale;

            if (pill != null)
            {
                // Authored colours win, so a label can be retinted in the prefab.
                _idleFill = pill.color;
            }

            if (label != null)
            {
                _idleText = label.color;
            }

            if (secondLine != null)
            {
                _idleSecondText = secondLine.color;
            }

            Apply();
        }

        private void OnDisable()
        {
            // A label hidden mid-hover must not come back still grown.
            _hovered = false;
            _hover = 0f;
            Apply();
        }

        public void SetSelected(bool selected)
        {
            if (IsSelected == selected)
            {
                return;
            }

            IsSelected = selected;
            Apply();
        }

        public void SetDestination(ExperienceModule module) => destination = module;

        /// <summary>
        /// Unused, and broken for the shipped leader: it scales Z as for a 3D line, but the leader is a flat canvas
        /// <see cref="Graphic"/>. To fix, size the rect's height to the distance and rotate it about Z in the canvas.
        /// </summary>
        public void PointLeaderAt(Vector3 worldPoint)
        {
            if (leader == null)
            {
                return;
            }

            var t = leader.transform;
            var toPoint = worldPoint - t.position;
            var distance = toPoint.magnitude;
            if (distance < 1e-4f)
            {
                leader.enabled = false;
                return;
            }

            leader.enabled = true;
            t.rotation = Quaternion.LookRotation(toPoint);
            var scale = t.localScale;
            t.localScale = new Vector3(scale.x, scale.y, distance);
        }

        private void Update()
        {
            var target = _hovered ? 1f : 0f;
            if (Mathf.Approximately(_hover, target))
            {
                return;
            }

            _hover = Mathf.MoveTowards(_hover, target, Time.deltaTime / HoverSeconds);
            Apply();
        }

        private void Apply()
        {
            EnsureInit();
            _grow.localScale = _baseScale * Mathf.Lerp(1f, HoverScale, _hover);

            if (pill != null)
            {
                // Graphic.color, not a material instance, so the many tags on screen still batch.
                pill.color = Color.Lerp(_idleFill, _accent, _hover);
            }

            if (label != null)
            {
                label.color = Color.Lerp(_idleText, _hoverText, _hover);
                label.fontStyle = IsSelected ? label.fontStyle | FontStyles.Bold : label.fontStyle & ~FontStyles.Bold;
            }

            if (secondLine != null)
            {
                // Must darken with the name: pale grey on the cyan fill does not read.
                secondLine.color = Color.Lerp(_idleSecondText, _hoverText, _hover);
            }

            if (leader != null && leader.enabled)
            {
                leader.color = IsSelected || _hover > 0.5f
                    ? _accent
                    : new Color(_accent.r, _accent.g, _accent.b, 0.35f); // line/hairline
            }

            if (selectedOutline != null)
            {
                selectedOutline.SetActive(IsSelected);
            }
        }

        // ---------- input

        public void OnFocusEnter(GEFocusEventData eventData)
        {
            // Both hands can point at the same tag; the second arriving is not a new hover.
            if (!_hovered)
            {
                AudioService.Instance?.PlayClip(AudioId.Focus);
            }

            _hovered = true;
        }

        public void OnFocusExit(GEFocusEventData eventData)
        {
            _hovered = false;
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            // A fingertip has no separate click, so a poke fires here; a ray waits for the release below.
            if (eventData != null && eventData.IsNear)
            {
                Pick(eventData);
            }
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
        }

        public void OnPointerClicked(GEPointerEventData eventData)
        {
            if (eventData == null || !eventData.IsNear)
            {
                Pick(eventData);
            }
        }

        private void Pick(GEPointerEventData eventData)
        {
            if (!isActiveAndEnabled || Time.unscaledTime - _lastClick < ClickCooldown)
            {
                return;
            }

            _lastClick = Time.unscaledTime;
            eventData?.Use();

            // Not a GEButton, so it sounds its own select.
            AudioService.Instance?.PlayClip(AudioId.Select);
            onPicked.Invoke(this);
        }
    }
}
