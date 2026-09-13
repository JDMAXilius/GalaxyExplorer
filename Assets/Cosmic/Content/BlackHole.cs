using UnityEngine;

namespace Cosmic
{
    [DisallowMultipleComponent]
    public class BlackHole : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        [SerializeField] Renderer disc;
        [SerializeField] Renderer glow;
        [SerializeField] Vector3 spinAxis = Vector3.up;
        [SerializeField] float spinDegreesPerSecond = -5f;
        [SerializeField] float clipMetres = 0.3f;
        [SerializeField] float fadeStartMetres = 0.3f;
        [SerializeField] float fadeEndMetres = 0.25f;

        MaterialPropertyBlock block;
        Camera cam;
        float extra;
        float applied = -1f;
        bool ready;

        public float Fade
        {
            get => extra;
            set
            {
                Init();
                extra = Mathf.Clamp01(value);
                Push(Proximity(View()));
            }
        }

        public Renderer Disc { get { Init(); return disc; } }

        void Awake() => Init();

        void OnDisable() => applied = -1f;

        void LateUpdate()
        {
            Init();
            Spin(Time.deltaTime);
            var view = View();
            Billboard(view);
            Push(Proximity(view));
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            block = new MaterialPropertyBlock();
            if (disc == null) disc = GetComponentInChildren<Renderer>();
        }

        void Spin(float seconds)
        {
            if (spinAxis.sqrMagnitude <= 0f || spinDegreesPerSecond == 0f) return;
            transform.localRotation *= Quaternion.AngleAxis(spinDegreesPerSecond * seconds, spinAxis.normalized);
        }

        void Billboard(Transform view)
        {
            if (glow == null || view == null) return;
            var card = glow.transform;
            var forward = card.position - view.position;
            if (forward.sqrMagnitude <= 1e-10f) return;
            var up = card.parent != null ? card.parent.up : transform.up;
            card.rotation = Quaternion.LookRotation(forward.normalized, up);
        }

        float Proximity(Transform view)
        {
            if (view == null) return 0f;
            var scale = transform.lossyScale.x;
            var start = fadeStartMetres * scale;
            var end = fadeEndMetres * scale;
            if (start - end <= 0f) return 0f;
            var metres = (transform.position - view.position).magnitude - clipMetres;
            return 1f - Mathf.Clamp01((metres - end) / (start - end));
        }

        void Push(float proximity)
        {
            var fade = 1f - (1f - Mathf.Clamp01(proximity)) * (1f - extra);
            if (Mathf.Approximately(fade, applied)) return;
            applied = fade;
            block.SetFloat(FadeId, fade);
            if (disc != null) disc.SetPropertyBlock(block);
            if (glow != null) glow.SetPropertyBlock(block);
        }

        Transform View()
        {
            if (cam == null) cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
