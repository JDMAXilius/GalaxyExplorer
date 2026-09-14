// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// A name floating over a place you can go: the tags scattered across the Milky Way, and the labels on the
    /// bodies in the orbit model.
    ///
    /// A dark pill with the name in it, a hairline leader running back to the point it belongs to, and three
    /// states — idle, hover (fifteen per cent larger and filled cyan, with the text going dark so it still
    /// reads), and selected (a cyan outline, kept while that destination is open). Pinch it, or click it on
    /// desktop, and it raises <see cref="OnPicked"/>. A label deliberately knows nothing about what it opens:
    /// <c>DestinationTags</c> (gone since CS-170) subscribes to that event and decides whether the module is a scene to
    /// switch to or an overlay to spawn.
    ///
    /// Hover and click arrive through <see cref="GEInputEvents.ExecuteHierarchy{T}"/> from a
    /// <see cref="GEInteractable"/> on the collider, the same path every other interactive object uses, so a
    /// hand ray, a fingertip and the desktop mouse all reach it without special cases.
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

        private void Awake() => EnsureInit();

        // A label is often bound and selected in the same frame it is spawned, and a caller has no way to know
        // whether Awake has run yet, so every entry point goes through this rather than trusting it has.
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
                // Read the authored colour so a designer can retint a label without touching this script.
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

        /// <summary>Marks this label as the open destination, or no longer so.</summary>
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
        /// Points the leader line at a world position and sizes it to reach.
        ///
        /// For a label whose subject moves relative to it. The Milky Way tags do not use this: they hang a
        /// fixed distance straight above a fixed point on the disc, so <c>DestinationTagBuilder</c> sizes the
        /// hairline once, in canvas units, and the billboard about Y keeps it vertical for free.
        ///
        /// <para><b>Unused, and it does not work as written.</b> Nothing calls it. It is shaped for a 3D line —
        /// a cylinder or a LineRenderer — whose length is its local Z: the leader every label actually ships
        /// is a <see cref="Graphic"/>, a flat quad in the canvas's XY plane, so scaling Z does nothing to its
        /// length and turning it to face the point lays it edge-on. This matters because a fanned-out card
        /// layout — the treatment the inherited Milky Way markers used, each card set off to one side on a
        /// long angled leader — is the natural next step for that map, and this is the method it would call.
        /// Making it work means sizing the rect's height to the distance and rotating it about Z within the
        /// canvas, not orienting a transform in the room.</para>
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
                // Graphic.color rather than a material instance: the canvas batches these, so a label costs
                // nothing extra to tint, and several tags are on screen at once.
                pill.color = Color.Lerp(_idleFill, _accent, _hover);
            }

            if (label != null)
            {
                label.color = Color.Lerp(_idleText, _hoverText, _hover);
                label.fontStyle = IsSelected ? label.fontStyle | FontStyles.Bold : label.fontStyle & ~FontStyles.Bold;
            }

            if (secondLine != null)
            {
                // The same journey as the name, from its own dimmer idle colour. Both have to travel or the
                // subtitle stays pale grey on a cyan fill, which is the one combination in the palette that
                // does not read.
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
            // Guarded on the flag rather than fired blind: both hands can be pointing at the same tag, and the
            // second one arriving is not a new hover.
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

            // A label is not a GEButton — it handles pointers itself — so it has to sound its own select.
            AudioService.Instance?.PlayClip(AudioId.Select);
            onPicked.Invoke(this);
        }
    }
}
