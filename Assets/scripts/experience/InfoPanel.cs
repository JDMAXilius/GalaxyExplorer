// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// Body, scene or moon text panel beside its target, filled from <see cref="BodyInfo"/> or
    /// <see cref="ExperienceModule"/>. No backing plate over passthrough (GDD 8.3); many open at once, so no singleton.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class InfoPanel : MonoBehaviour
    {
        public enum Variant
        {
            Body,
            Scene,
            Moon
        }

        [SerializeField]
        private Variant variant = Variant.Body;

        [Header("Text")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text paragraph;
        [SerializeField] private TMP_Text instruction;

        [SerializeField]
        [Tooltip("Hairline between the prose and the stats. Only the body variant has stats, so the scene and " +
                 "moon variants hide it rather than draw a rule under nothing.")]
        private Graphic divider;

        [SerializeField]
        [Tooltip("Gap between the last paragraph and the instruction on a scene panel, in canvas units — one " +
                 "unit is one millimetre on this canvas.")]
        private float sceneInstructionGapUnits = 8f;

        [Header("Stats, four cells for the body variant")]
        [SerializeField] private Transform statGrid;
        [SerializeField] private TMP_Text[] statLabels = new TMP_Text[0];
        [SerializeField] private TMP_Text[] statValues = new TMP_Text[0];

        [Header("Placement")]
        [SerializeField]
        [Tooltip("What the panel sits beside. The body, or the object the scene panel describes.")]
        private Transform target;

        [SerializeField]
        [Tooltip("Renderer used to find the target's edge. Falls back to the target's own renderers.")]
        private Renderer targetBounds;

        [SerializeField]
        [Tooltip("Gap between the target's edge and the panel, in metres. The GDD asks for 30 mm.")]
        private float gapMetres = 0.03f;

        [SerializeField]
        [Tooltip("Metres per canvas unit, so the panel keeps one physical size whatever the target does.")]
        private float metresPerUnit = 0.001f;

        [SerializeField]
        [Tooltip("Seconds to fade in or out.")]
        private float fadeSeconds = 0.35f;

        [SerializeField]
        [Tooltip("How fast the panel follows. Zero snaps.")]
        private float followLerpTime = 0.08f;

        [Header("Visibility")]
        [SerializeField]
        [Tooltip("Body and moon panels follow this solver's state. Leave empty and call Show/Hide yourself.")]
        private ForceSolver body;

        private CanvasGroup _group;
        private Camera _camera;
        private bool _shown;

        private bool _sounded;
        private float _side = 1f;
        private List<(RectTransform rect, float y, float height)> _rows;
        private AudioClip _narration;
        private const float RowGapUnits = 3f;
        private const float SideSwitchDistance = 0.12f;

        public Variant PanelVariant => variant;
        public Transform Target => target;

        // ---------- text size (GDD 11, F-03)
        // Scales the whole panel, not the fonts: rows are laid out at a fixed width, so a bigger font would reflow
        // the prose into the stats.
        private const string TextScalePrefsKey = "CosmicSimulation.PanelTextScale";

        private static float textScale = 1f;
        private static bool textScaleLoaded;

        /// <summary>
        /// Panel scale over the design (GDD 11: x1.0, x1.25, x1.5), persisted. Read lazily so it is right before any
        /// panel's Awake has run.
        /// </summary>
        public static float TextScale
        {
            get
            {
                if (!textScaleLoaded)
                {
                    textScale = Mathf.Clamp(PlayerPrefs.GetFloat(TextScalePrefsKey, 1f), 1f, 2f);
                    textScaleLoaded = true;
                }

                return textScale;
            }

            set
            {
                var wanted = Mathf.Clamp(value, 1f, 2f);
                if (textScaleLoaded && Mathf.Approximately(textScale, wanted))
                {
                    return;
                }

                textScale = wanted;
                textScaleLoaded = true;
                PlayerPrefs.SetFloat(TextScalePrefsKey, wanted);
                PlayerPrefs.Save();
            }
        }

        private void OnDestroy() => Registry<InfoPanel>.Remove(this);

        private void Awake()
        {
            Registry<InfoPanel>.Add(this);
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _camera = Camera.main;

            if (target == null && body != null)
            {
                target = body.transform;
            }

            if (divider == null)
            {
                // Prefabs built before this field existed leave it unassigned; find the hairline by name.
                var found = transform.Find("divider");
                divider = found != null ? found.GetComponent<Graphic>() : null;
            }
        }

        // ---------- binding

        /// <summary>Fills a body or moon panel. The variant follows the data: a moon is a body that orbits one.</summary>
        public void Bind(BodyInfo info)
        {
            if (info == null)
            {
                return;
            }

            Info = info;
            variant = info.IsMoon ? Variant.Moon : Variant.Body;
            _narration = info.IsMoon ? info.Narration : null;

            Set(title, info.DisplayName);
            Set(paragraph, info.Paragraph);

            if (variant == Variant.Moon)
            {
                // Stats the grid has no cell for overflow into the subtitle, after "MOON OF".
                var stats = new System.Collections.Generic.List<Stat>();
                foreach (var stat in info.Stats)
                {
                    if (stat.Label != "ORBITS")
                    {
                        stats.Add(stat);
                    }
                }

                var parts = new System.Text.StringBuilder();
                if (info.Orbits != null)
                {
                    parts.Append("MOON OF ").Append(info.Orbits.DisplayName.ToUpperInvariant());
                }

                for (var i = statLabels.Length; i < stats.Count; i++)
                {
                    parts.Append(parts.Length > 0 ? "   " : "").Append(stats[i].Label.ToUpperInvariant()).Append(' ').Append(stats[i].ToRichText().ToUpperInvariant());
                }

                Set(subtitle, parts.ToString());
                ShowStats(stats);
            }
            else
            {
                Set(subtitle, info.Subtitle != null ? info.Subtitle.ToUpperInvariant() : null);
                ShowStats(info.Stats);
            }

            Set(instruction, null);
            Layout();
        }

        /// <summary>Fills a scene panel from an experience's copy.</summary>
        public void Bind(ExperienceModule module)
        {
            if (module == null)
            {
                return;
            }

            var copy = module.Panel;
            if (copy == null)
            {
                return;
            }

            variant = Variant.Scene;

            var prose = copy.Paragraphs != null ? string.Join("\n\n", copy.Paragraphs) : null;

            Set(title, string.IsNullOrEmpty(copy.Title) ? module.DisplayName : copy.Title);
            Set(subtitle, null);
            Set(paragraph, prose);
            Set(instruction, copy.Instruction);
            ShowStats(null);
            Layout();
        }

        // Each row keeps its designed position unless the row above has grown into it, so copy that fits looks
        // as designed and copy that does not never overlaps. A scene panel's instruction follows its prose.
        private void Layout()
        {
            if (_rows == null)
            {
                _rows = new List<(RectTransform rect, float y, float height)>();
                foreach (var part in new Component[] { title, subtitle, paragraph, divider, statGrid, instruction })
                {
                    if (part != null)
                    {
                        var rect = (RectTransform)part.transform;
                        _rows.Add((rect, rect.anchoredPosition.y, rect.sizeDelta.y));
                    }
                }
            }

            var bottom = 0f;
            var first = true;
            foreach (var (rect, designedY, designedHeight) in _rows)
            {
                if (!rect.gameObject.activeSelf)
                {
                    continue;
                }

                var height = designedHeight;
                var text = rect.GetComponent<TMP_Text>();
                if (text != null)
                {
                    var width = rect.rect.width > 1f ? rect.rect.width : rect.sizeDelta.x;
                    var preferred = text.GetPreferredValues(text.text, width, 0f).y;
                    height = variant == Variant.Scene && text == paragraph ? preferred : Mathf.Max(preferred, designedHeight);
                }

                var y = first ? designedY : Mathf.Min(designedY, bottom - RowGapUnits);
                if (variant == Variant.Scene && text == instruction)
                {
                    y = bottom - sceneInstructionGapUnits;
                }

                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                bottom = y - height;
                first = false;
            }
        }

        private void ShowStats(System.Collections.Generic.IList<Stat> stats)
        {
            var count = stats != null ? stats.Count : 0;
            if (statGrid != null)
            {
                statGrid.gameObject.SetActive(count > 0);
            }

            if (divider != null)
            {
                divider.gameObject.SetActive(count > 0);
            }

            for (var i = 0; i < statLabels.Length; i++)
            {
                var used = i < count;
                var cell = statLabels[i] != null ? statLabels[i].transform.parent : null;
                if (cell != null)
                {
                    cell.gameObject.SetActive(used);
                }

                if (!used)
                {
                    continue;
                }

                var stat = stats[i];
                Set(statLabels[i], stat.Label != null ? stat.Label.ToUpperInvariant() : null);
                if (i < statValues.Length)
                {
                    // ToRichText writes the exponent as <sup>, so mass reads 5.97 x 10^24 kg properly.
                    Set(statValues[i], stat.ToRichText());
                }
            }
        }

        private static void Set(TMP_Text field, string text)
        {
            if (field == null)
            {
                return;
            }

            field.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text))
            {
                field.text = text;
            }
        }

        // ---------- visibility

        /// <summary>The body or moon this panel describes; null for a scene panel.</summary>
        public BodyInfo Info { get; private set; }

        /// <summary>Whether the panel is asking to be up right now (a body out of its layout, or shown).</summary>
        public bool IsOpen => WantsToShow();

        /// <summary>
        /// Set by the UI 2.0 shell while it is up: every legacy panel stays invisible, because the new
        /// information card describes the place and the body instead. Sounds and narration are unchanged.
        /// </summary>
        public static bool Suppressed { get; set; }

        public void Show() => _shown = true;

        public void Hide() => _shown = false;

        public void SetTarget(Transform newTarget, Renderer bounds = null)
        {
            target = newTarget;
            targetBounds = bounds;
        }

        private bool WantsToShow()
        {
            if (body == null)
            {
                return _shown;
            }

            switch (body.ForceState)
            {
                case ForceSolver.State.Attraction:
                case ForceSolver.State.Free:
                case ForceSolver.State.Manipulation:
                    return true;
                case ForceSolver.State.Dwell:
                    return body.PreviousForceState != ForceSolver.State.Root;
                default:
                    return false;
            }
        }

        private void LateUpdate()
        {
            // Edge-triggered here, not in Show()/Hide(): callers drive those every frame, and body-driven opens
            // never call them.
            var want = WantsToShow();
            if (want != _sounded)
            {
                _sounded = want;
                AudioService.Instance?.PlayClip(want ? AudioId.CardSelect : AudioId.CardDeselect);

                // Planets narrate from their own solver; a moon's plain solver does not, so its card does.
                if (want && _narration != null && GalaxyExplorerManager.IsInitialized && VOManager.AutoStart)
                {
                    GalaxyExplorerManager.Instance.VoManager.PlayClip(_narration, allowReplay: true, replaceQueue: true);
                }
            }

            // UI 2.0 replaces the scene panels with its own card; a body's or moon's card stays beside it.
            var wanted = want && !(Suppressed && Info == null) ? 1f : 0f;
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

            Place();
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

            var camera = _camera.transform;
            var centre = target.position;
            var radius = TargetRadius();

            // Side nearer the view centre, with hysteresis so it does not flip at the midline.
            var right = camera.right;
            var offsetFromCentre = Vector3.Dot(centre - camera.position, right);
            var wantedSide = offsetFromCentre > SideSwitchDistance ? -1f
                : offsetFromCentre < -SideSwitchDistance ? 1f
                : _side;
            _side = wantedSide;

            var goal = centre + right * (_side * (radius + gapMetres));

            transform.position = followLerpTime <= 0f
                ? goal
                : Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-Time.deltaTime / followLerpTime));

            transform.rotation = Quaternion.LookRotation(transform.position - camera.position, camera.up);

            // Constant physical size even under a scaled parent; TextScale is read here because this runs every
            // visible frame anyway.
            var parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            var scale = metresPerUnit * TextScale / Mathf.Max(0.0001f, parentScale);
            transform.localScale = new Vector3(scale, scale, scale);
        }

        private float TargetRadius()
        {
            if (targetBounds != null)
            {
                return targetBounds.bounds.extents.magnitude;
            }

            var found = target.GetComponentInChildren<Renderer>();
            return found != null ? found.bounds.extents.magnitude : 0.05f;
        }
    }
}
