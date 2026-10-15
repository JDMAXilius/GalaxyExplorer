// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A moon's name under it: 5 mm in orbit, 18 mm bold while held (GDD 8.4), so it reads as "the one you have".
    /// Placed exactly as <see cref="InfoPanel"/> places itself, or the two drift apart when a moon is scaled.
    /// </summary>
    [DisallowMultipleComponent]
    public class MoonLabel : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField]
        [Tooltip("The name. Lives on this label's own canvas, where one unit is one millimetre.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("Where the name comes from. Bind() sets this at runtime.")]
        private BodyInfo info;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("The moon's solver. Its state decides which of the two styles is showing.")]
        private ForceSolver moon;

        [SerializeField]
        [Tooltip("What the label sits under. Defaults to the moon's transform.")]
        private Transform target;

        [SerializeField]
        [Tooltip("Renderer used to find the moon's edge. Falls back to the target's own renderers.")]
        private Renderer targetBounds;

        [SerializeField]
        [Tooltip("Gap between the moon's lower edge and the label, in metres.")]
        private float gapMetres = 0.01f;

        [SerializeField]
        [Tooltip("Metres per canvas unit, so the label keeps one physical size whatever the moon does.")]
        private float metresPerUnit = 0.001f;

        [SerializeField]
        [Tooltip("How fast the label follows the moon. Zero snaps.")]
        private float followLerpTime = 0.05f;

        [Header("Styles, in canvas units (one unit is one millimetre)")]
        [SerializeField]
        [Tooltip("Text size while the moon is in its orbit. The GDD asks for 5 mm.")]
        private float orbitingSize = 5f;

        [SerializeField]
        [Tooltip("Text size while the moon is held. The GDD asks for 18 mm, bold.")]
        private float heldSize = 18f;

        [SerializeField]
        [Tooltip("Seconds to grow from the small style to the large one, and back.")]
        private float styleSeconds = 0.25f;

        [SerializeField]
        [Tooltip("Seconds to fade in or out. Needs a CanvasGroup on this object; without one the label just shows.")]
        private float fadeSeconds = 0.35f;

        private bool _ready;
        private CanvasGroup _group;

        /// <summary>
        /// The player's app-wide label preference, ANDed with each label's own <c>_shown</c> so toggling never strands
        /// a label over a hidden moon. Static so a label created later still honours it. Kept across sessions, like
        /// <see cref="Mute"/>: the desktop L key and the Settings switch both write it (CS-378).
        /// </summary>
        public static bool ShowLabels
        {
            // Read lazily: PlayerPrefs throws from a static initialiser, which runs before the engine is up.
            get => _showLabels ??= PlayerPrefs.GetInt(ShowLabelsKey, 1) == 1;
            set
            {
                if (ShowLabels == value)
                {
                    return;
                }

                _showLabels = value;
                PlayerPrefs.SetInt(ShowLabelsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private const string ShowLabelsKey = "CosmicSimulation.ShowLabels";
        private static bool? _showLabels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _showLabels = null;

        private Camera _camera;
        private FontStyles _baseStyle;
        private float _style;          // 0 small, 1 large
        private bool _shown = true;
        private bool _heldOverride;

        /// <summary>True while the moon is out of its orbit, so the large style is showing.</summary>
        public bool IsHeld
        {
            get
            {
                if (moon == null)
                {
                    return _heldOverride;
                }

                // Matches MoonForceSolver's own "in orbit" test: dwelling on a moon that is still in its orbit is
                // not holding it, but dwelling on one already pulled out is.
                switch (moon.ForceState)
                {
                    case ForceSolver.State.None:
                    case ForceSolver.State.Root:
                        return false;
                    case ForceSolver.State.Dwell:
                        return moon.PreviousForceState != ForceSolver.State.Root;
                    default:
                        return true;
                }
            }
        }

        public BodyInfo Info => info;

        public Transform Target => target;

        private void Awake() => EnsureInit();

        // A moon and its label are usually spawned and bound in the same frame, and the caller has no way to know
        // whether Awake has run yet, so every entry point comes through here rather than trusting that it has.
        private void EnsureInit()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _group = GetComponent<CanvasGroup>();
            _camera = Camera.main;

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }

            // Bold belongs to the held style alone, so strip it from whatever the prefab was authored with.
            _baseStyle = label != null ? label.fontStyle & ~FontStyles.Bold : FontStyles.Normal;

            if (target == null && moon != null)
            {
                target = moon.transform;
            }

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            ApplyText();
            _style = IsHeld ? 1f : 0f;
            ApplyStyle();
        }

        /// <summary>Names the label. A moon's <see cref="BodyInfo"/> is the same asset its info panel reads.</summary>
        public void Bind(BodyInfo moonInfo)
        {
            EnsureInit();
            info = moonInfo;
            ApplyText();
        }

        /// <summary>Points the label at the moon whose state it follows, and at that moon by default.</summary>
        public void SetMoon(ForceSolver solver)
        {
            EnsureInit();
            moon = solver;
            if (solver != null && target == null)
            {
                target = solver.transform;
            }

            _style = IsHeld ? 1f : 0f;
            ApplyStyle();
        }

        public void SetTarget(Transform newTarget, Renderer bounds = null)
        {
            EnsureInit();
            target = newTarget;
            targetBounds = bounds;
        }

        /// <summary>Forces the large style on a label with no solver of its own (desktop and tests).</summary>
        public void SetHeld(bool held)
        {
            EnsureInit();
            _heldOverride = held;
        }

        public void Show()
        {
            EnsureInit();
            _shown = true;
            ApplyShown();
        }

        public void Hide()
        {
            EnsureInit();
            _shown = false;
            ApplyShown();
        }

        // Without a CanvasGroup there is nothing to fade, so hiding switches the text off outright.
        private void ApplyShown()
        {
            if (_group == null && label != null)
            {
                label.enabled = _shown;
            }
        }

        private void ApplyText()
        {
            if (label == null)
            {
                return;
            }

            var text = info != null ? info.DisplayName : null;
            if (!string.IsNullOrEmpty(text))
            {
                label.text = text;
            }
        }

        private void LateUpdate()
        {
            EnsureInit();

            if (_group != null)
            {
                var wanted = _shown && ShowLabels ? 1f : 0f;
                if (!Mathf.Approximately(_group.alpha, wanted))
                {
                    _group.alpha = fadeSeconds <= 0f
                        ? wanted
                        : Mathf.MoveTowards(_group.alpha, wanted, Time.deltaTime / fadeSeconds);
                }

                if (_group.alpha <= 0.001f)
                {
                    return;
                }
            }
            else if (!_shown)
            {
                return;
            }

            ApplyStyle();
            Place();
        }

        private void ApplyStyle()
        {
            var wanted = IsHeld ? 1f : 0f;
            _style = styleSeconds <= 0f ? wanted : Mathf.MoveTowards(_style, wanted, Time.deltaTime / styleSeconds);

            if (label == null)
            {
                return;
            }

            var blend = Mathf.SmoothStep(0f, 1f, _style);
            label.fontSize = Mathf.Lerp(orbitingSize, heldSize, blend);
            label.fontStyle = _style > 0.5f ? _baseStyle | FontStyles.Bold : _baseStyle;
        }

        private void Place()
        {
            if (target == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var view = _camera.transform;

            // Under the moon as the player sees it, not under it in world space: the label has to stay legible
            // when someone leans over a moon they are holding.
            var goal = target.position - view.up * (TargetRadius() + gapMetres);

            transform.position = followLerpTime <= 0f
                ? goal
                : Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-Time.deltaTime / followLerpTime));

            transform.rotation = Quaternion.LookRotation(transform.position - view.position, view.up);

            // One physical size whatever the moon is scaled to: the label is not parented to the moon, but a
            // prefab may still sit under a scaled rig.
            var parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            var scale = metresPerUnit / Mathf.Max(0.0001f, parentScale);
            transform.localScale = new Vector3(scale, scale, scale);
        }

        private float TargetRadius()
        {
            if (targetBounds != null)
            {
                return targetBounds.bounds.extents.magnitude;
            }

            var found = target.GetComponentInChildren<Renderer>();
            return found != null ? found.bounds.extents.magnitude : 0.02f;
        }
    }
}
