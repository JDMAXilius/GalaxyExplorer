using TMPro;
using UnityEngine;

namespace Cosmic
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Panel : MonoBehaviour
    {
        public enum Variant { Body, Scene, Moon }

        [SerializeField] Variant variant = Variant.Body;
        [SerializeField] Theme theme;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text paragraph;
        [SerializeField] TMP_Text instruction;
        [SerializeField] TMP_Text[] statLabels;
        [SerializeField] TMP_Text[] statValues;
        [SerializeField] float gapMetres = 0.03f;
        [SerializeField] float fadeSeconds = 0.35f;
        [SerializeField] float followSeconds = 0.15f;
        [SerializeField] float sideSwitchMetres = 0.12f;
        [SerializeField] float metresPerUnit = 0.001f;

        CanvasGroup group;
        Camera cam;
        Transform target;
        Renderer bounds;
        int side = 1;
        bool showing, placed, ready;

        public bool Showing => showing;
        public Transform Target => target;

        public void Show(Body body, Transform at, Renderer extent = null)
        {
            Init();
            Fill(body.title, body.subtitle, body.paragraph, null, body.stats);
            Open(at, extent);
        }

        public void Show(in PanelCopy copy, Transform at, Renderer extent = null)
        {
            Init();
            var text = copy.paragraphs != null ? string.Join("\n\n", copy.paragraphs) : string.Empty;
            Fill(copy.title, null, text, copy.instruction, copy.stats);
            Open(at, extent);
        }

        public void Hide()
        {
            showing = false;
            if (group != null) group.blocksRaycasts = false;
        }

        void OnEnable()
        {
            Init();
            Prefs.Changed += Rescale;
            Rescale();
        }

        void OnDisable() => Prefs.Changed -= Rescale;

        void LateUpdate()
        {
            var goal = showing ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            if (group.alpha <= 0f)
            {
                placed = false;
                if (!showing) return;
            }
            if (target == null) return;
            if (cam == null) cam = Camera.main;
            if (cam != null) Place(cam.transform);
        }

        void Open(Transform at, Renderer extent)
        {
            target = at;
            bounds = extent;
            showing = true;
            placed = false;
            group.blocksRaycasts = true;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PanelOpen, transform);
        }

        void Fill(string heading, string sub, string text, string hint, Stat[] stats)
        {
            if (title != null) title.text = heading ?? string.Empty;
            if (subtitle != null)
            {
                subtitle.gameObject.SetActive(!string.IsNullOrEmpty(sub));
                subtitle.text = sub != null ? sub.ToUpperInvariant() : string.Empty;
            }
            if (paragraph != null) paragraph.text = text ?? string.Empty;
            if (instruction != null)
            {
                instruction.gameObject.SetActive(!string.IsNullOrEmpty(hint));
                instruction.text = hint ?? string.Empty;
            }
            var count = stats != null ? stats.Length : 0;
            for (var i = 0; statLabels != null && i < statLabels.Length; i++)
            {
                var shown = i < count && (variant != Variant.Moon || i < 1);
                statLabels[i].transform.parent.gameObject.SetActive(shown);
                if (!shown) continue;
                statLabels[i].text = stats[i].label.ToUpperInvariant();
                if (statValues != null && i < statValues.Length) statValues[i].text = stats[i].ToRichText();
            }
        }

        void Place(Transform view)
        {
            var box = bounds != null ? bounds.bounds : new Bounds(target.position, Vector3.zero);
            var centre = box.center;
            var radius = Mathf.Max(box.extents.x, box.extents.y, box.extents.z);
            var lateral = Vector3.Dot(centre - view.position, view.right);
            if (!placed) side = lateral > sideSwitchMetres * 0.5f ? -1 : 1;
            else if (side > 0 && lateral > sideSwitchMetres) side = -1;
            else if (side < 0 && lateral < -sideSwitchMetres) side = 1;
            var rect = (RectTransform)transform;
            rect.pivot = new Vector2(side > 0 ? 0f : 1f, 0.5f);
            var goal = centre + view.right * (side * (radius + gapMetres));
            var rotation = Quaternion.LookRotation(goal - view.position, view.up);
            if (!placed || followSeconds <= 0f)
            {
                transform.SetPositionAndRotation(goal, rotation);
                placed = true;
                return;
            }
            var k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / followSeconds);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, goal, k), Quaternion.Slerp(transform.rotation, rotation, k));
        }

        void Rescale()
        {
            var parent = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Vector3.one * (metresPerUnit * Prefs.TextScale / Mathf.Max(parent, 1e-5f));
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            if (theme == null || theme.font == null) return;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
        }
    }
}
