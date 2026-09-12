// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The text that floats beside whatever the player is looking at.
    ///
    /// Three shapes, one component. A **body** panel carries a title, a subtitle in small caps, a paragraph and
    /// four stats in a two-by-two grid. A **scene** panel drops the stats and takes two or three paragraphs,
    /// the last of which is an instruction in the secondary colour. A **moon** panel is narrower still: a name,
    /// one line of numbers, one sentence.
    ///
    /// There is no plate behind any of them. Over passthrough a plate would punch a hole in the room, so the
    /// text is white with a thin dark outline instead, which stays readable against a bright wall and a black
    /// sky alike (GDD 8.3). Several panels are open at once, so nothing here is a singleton.
    ///
    /// This is <c>PlanetInfoCard</c> rebuilt to read <see cref="BodyInfo"/> and <see cref="ExperienceModule"/>
    /// instead of fields typed into a prefab, and to render mass with a real superscript.
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

        // Last value LateUpdate sounded on, so the open/close clip fires on the change and not every frame.
        private bool _sounded;
        private float _side = 1f;
        private const float SideSwitchDistance = 0.12f;

        public Variant PanelVariant => variant;
        public Transform Target => target;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _camera = Camera.main;

            if (target == null && body != null)
            {
                target = body.transform;
            }

            if (divider == null)
            {
                // info_panel_prefab was built before this field existed, so on that prefab the hairline is a
                // child nothing points at - and a scene panel, which has no stats, would draw a rule across
                // empty space. Found by name once; harmless the day UiPrefabBuilder assigns the field instead.
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

            variant = info.IsMoon ? Variant.Moon : Variant.Body;

            Set(title, info.DisplayName);
            Set(paragraph, info.Paragraph);

            if (variant == Variant.Moon)
            {
                // A moon gets one line instead of a grid: who it orbits, then its numbers.
                var parts = new System.Text.StringBuilder();
                if (info.Orbits != null)
                {
                    parts.Append(info.Orbits.DisplayName.ToUpperInvariant());
                }

                foreach (var stat in info.Stats)
                {
                    if (parts.Length > 0)
                    {
                        parts.Append("   ");
                    }

                    parts.Append(stat.ToRichText());
                }

                Set(subtitle, parts.ToString());
                ShowStats(0, info);
            }
            else
            {
                Set(subtitle, info.Subtitle != null ? info.Subtitle.ToUpperInvariant() : null);
                ShowStats(info.Stats.Length, info);
            }

            Set(instruction, null);
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
            ShowStats(0, null);
            ReflowScene(prose);
        }

        /// <summary>
        /// Moves the instruction under however much prose a place has.
        ///
        /// The prefab's rows are laid out for the body variant, whose paragraph is capped at 55 words and given
        /// a fixed seven lines (<c>UiPrefabBuilder</c>). A scene panel's two or three paragraphs are as long as
        /// the copy deck makes them, so a fixed row would either run the prose through the instruction or leave
        /// a hole under it. Only the scene variant is reflowed: the body panels are already laid out and signed
        /// off against the spec.
        /// </summary>
        private void ReflowScene(string prose)
        {
            if (paragraph == null || instruction == null)
            {
                return;
            }

            var proseRect = paragraph.rectTransform;

            // sizeDelta rather than rect.width when the layout has not been built yet: both rows are anchored to
            // a point at the top-left, where the two are the same number.
            var width = proseRect.rect.width > 1f ? proseRect.rect.width : proseRect.sizeDelta.x;
            var height = string.IsNullOrEmpty(prose) ? 0f : paragraph.GetPreferredValues(prose, width, 0f).y;

            proseRect.sizeDelta = new Vector2(proseRect.sizeDelta.x, height);

            // Top-left pivot, so y runs downwards as negatives and the instruction sits at the paragraph's
            // top minus its height.
            var instructionRect = instruction.rectTransform;
            instructionRect.anchoredPosition = new Vector2(
                instructionRect.anchoredPosition.x,
                proseRect.anchoredPosition.y - height - sceneInstructionGapUnits);
        }

        private void ShowStats(int count, BodyInfo info)
        {
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

                var stat = info.Stats[i];
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

            // A body's panel is open exactly while the body is out of its layout.
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
            // Edge-triggered, not hooked to Show()/Hide(). Those are idempotent setters that callers drive
            // every frame, and real visibility is this recomputed predicate, so hooking them would sound on
            // every spawn and stay silent on a body-driven open.
            var want = WantsToShow();
            if (want != _sounded)
            {
                _sounded = want;
                AudioService.Instance?.PlayClip(want ? AudioId.CardSelect : AudioId.CardDeselect);
            }

            var wanted = want ? 1f : 0f;
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

            // Put the panel on whichever side is closer to the middle of the view, so it does not sit off the
            // edge of the display, and keep it there until the target has clearly crossed over.
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

            // One physical size whatever the target is scaled to: the panel is not parented to the body, but a
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
            return found != null ? found.bounds.extents.magnitude : 0.05f;
        }
    }
}
