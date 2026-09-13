using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cosmic
{
    [RequireComponent(typeof(CanvasGroup))]
    public class About : MonoBehaviour
    {
        public const string LicenceResource = "legal/galaxy_explorer_license";
        public const string NoticeResource = "legal/cosmic_simulation_xr_notice";
        public const string Attribution = "Galaxy Explorer (c) Microsoft Corporation. Used under the MIT License.";

        [SerializeField] Theme theme;
        [SerializeField] TMP_Text credits;
        [SerializeField] TMP_Text licence;
        [SerializeField] TMP_Text version;
        [SerializeField] Button close;
        [SerializeField] float fadeSeconds = 0.35f;
        [SerializeField] float distanceMetres = 0.9f;
        [SerializeField] float metresPerUnit = 0.001f;

        CanvasGroup group;
        Camera cam;
        bool showing, ready;

        public bool Showing => showing;

        public void Show()
        {
            Init();
            showing = true;
            group.blocksRaycasts = true;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                var view = cam.transform;
                var forward = view.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                var goal = view.position + forward.normalized * distanceMetres;
                transform.SetPositionAndRotation(goal, Quaternion.LookRotation(goal - view.position, Vector3.up));
            }
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PanelOpen, transform);
        }

        public void Hide()
        {
            showing = false;
            if (group != null) group.blocksRaycasts = false;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.PanelClose, transform);
        }

        public void Toggle()
        {
            if (showing) Hide();
            else Show();
        }

        void OnEnable()
        {
            Init();
            if (close != null) close.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            if (close != null) close.onClick.RemoveListener(Hide);
        }

        void LateUpdate()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            transform.localScale = Vector3.one * metresPerUnit;
            if (theme != null && theme.font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
            var notice = Resources.Load<TextAsset>(NoticeResource);
            var mit = Resources.Load<TextAsset>(LicenceResource);
            if (credits != null) credits.text = notice != null ? notice.text : Attribution;
            if (licence != null) licence.text = mit != null ? mit.text : Attribution;
            if (version != null) version.text = Application.productName + " " + Application.version;
        }
    }
}
