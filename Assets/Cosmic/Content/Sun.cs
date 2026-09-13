using UnityEngine;

namespace Cosmic
{
    [DisallowMultipleComponent]
    public class Sun : MonoBehaviour
    {
        static readonly int TouchId = Shader.PropertyToID("_TouchBrightness");
        static readonly int TexStId = Shader.PropertyToID("_MainTex_ST");
        static readonly int ColorAParamsId = Shader.PropertyToID("_ColorAParams");
        static readonly int DistFromCameraId = Shader.PropertyToID("_DistFromCamera");
        static readonly int CurrentScaleId = Shader.PropertyToID("_CurrentScale");

        [SerializeField] Transform body;
        [SerializeField] Renderer[] surfaces;
        [SerializeField] float radiusMetres = 0.5f;
        [SerializeField] float riseSeconds = 0.6f;
        [SerializeField] float fallSeconds = 0.9f;
        [SerializeField] AudioClip rumble;
        [SerializeField, Range(0f, 1f)] float rumbleVolume = 0.6f;
        [SerializeField] float rumbleNearMetres = 0.2f;
        [SerializeField] float rumbleFarMetres = 8f;
        [SerializeField] Transform leftHand;
        [SerializeField] Transform rightHand;
        [SerializeField] Mouse mouse;
        [SerializeField] float handStillMetres = 0.001f;
        [SerializeField] Transform[] flareRoots;
        [SerializeField] Renderer[] flares;
        [SerializeField] Vector2[] flareFrom = { new Vector2(-0.36f, 0f), new Vector2(-0.77f, 0f), new Vector2(-0.46f, 0f) };
        [SerializeField] Vector2[] flareTo = { new Vector2(0.4f, 0f), new Vector2(0.2f, 0f), new Vector2(0.36f, 0f) };
        [SerializeField] float flareScale = 0.0008600589f;
        [SerializeField] Vector2 flareSecondsRange = new Vector2(8f, 15f);
        [SerializeField] Vector2 flareGapSecondsRange = new Vector2(0f, 5f);
        [SerializeField] Renderer glow;
        [SerializeField] AnimationCurve glowDistanceCurve = new AnimationCurve(new Keyframe(0f, 1.9908904f), new Keyframe(0.46519825f, 3.5741355f, 0.5127961f, 0.5127961f), new Keyframe(2.0013375f, 3.89755f));
        [SerializeField] AnimationCurve glowSmoothnessCurve = new AnimationCurve(new Keyframe(0.0045280457f, 0.08130646f), new Keyframe(0.085564405f, 0.0977086f, 0.104671344f, 0.104671344f), new Keyframe(1.9984674f, 0.14964125f));
        [SerializeField] Renderer lensFlare;
        [SerializeField] Transform flareGroup;
        [SerializeField] float spinDegreesPerSecond = -5f;
        [SerializeField] Vector3 flareSpinDegreesPerSecond = new Vector3(5f, 6f, 7f);

        MaterialPropertyBlock block;
        MaterialPropertyBlock[] flareBlocks;
        Vector2[] flareTiling;
        float[] flareWait, flareAge, flareLength;
        Vector4 glowParams = new Vector4(1f, 1f, 0.5f, 0.5f);
        Vector3 leftWas, rightWas;
        AudioSource rumbleSource;
        Camera cam;
        float level, applied = -1f;
        bool ready;

        public float Touch { get { Init(); return level; } }

        void Awake() => Init();

        void OnDisable()
        {
            if (!ready) return;
            level = 0f;
            ApplyTouch(0f);
            Rumbling(0f);
        }

        void Update()
        {
            Init();
            var dt = Time.deltaTime;
            var inside = Touching();
            level = Mathf.MoveTowards(level, inside ? 1f : 0f, dt / Mathf.Max(0.01f, inside ? riseSeconds : fallSeconds));
            var eased = Mathf.SmoothStep(0f, 1f, level);
            if (!Mathf.Approximately(level, applied)) ApplyTouch(eased);
            Rumbling(eased);
            DriveFlares(dt);
            DriveGlow();
            DriveLens();
            Spin(dt);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            block = new MaterialPropertyBlock();
            if (leftHand != null) leftWas = leftHand.position;
            if (rightHand != null) rightWas = rightHand.position;
            if (glow != null && glow.sharedMaterial != null && glow.sharedMaterial.HasProperty(ColorAParamsId)) glowParams = glow.sharedMaterial.GetVector(ColorAParamsId);
            var count = Mathf.Max(flareRoots != null ? flareRoots.Length : 0, flares != null ? flares.Length : 0);
            flareBlocks = new MaterialPropertyBlock[count];
            flareTiling = new Vector2[count];
            flareWait = new float[count];
            flareAge = new float[count];
            flareLength = new float[count];
            for (var i = 0; i < count; i++)
            {
                flareBlocks[i] = new MaterialPropertyBlock();
                var material = Quad(i) != null ? Quad(i).sharedMaterial : null;
                var st = material != null && material.HasProperty(TexStId) ? material.GetVector(TexStId) : new Vector4(1f, 1f, 0f, 0f);
                flareTiling[i] = new Vector2(st.x, st.y);
                flareWait[i] = Random.Range(0f, 1f) + Random.Range(flareGapSecondsRange.x, flareGapSecondsRange.y);
                flareLength[i] = Random.Range(flareSecondsRange.x, flareSecondsRange.y);
            }
        }

        bool Touching()
        {
            // Both hands are sampled every frame whatever the answer, because a hand that has not moved is how the desktop mouse earns its turn.
            var moved = Moved(leftHand, ref leftWas) | Moved(rightHand, ref rightWas);
            if (Inside(leftHand) || Inside(rightHand)) return true;
            return !moved && Hovering();
        }

        bool Moved(Transform hand, ref Vector3 was)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy) return false;
            var now = hand.position;
            var moved = (now - was).sqrMagnitude > handStillMetres * handStillMetres;
            was = now;
            return moved;
        }

        bool Inside(Transform hand) => hand != null && hand.gameObject.activeInHierarchy && Inside(hand.position);

        bool Inside(Vector3 worldPoint)
        {
            var centre = Centre();
            var radius = radiusMetres * Mathf.Abs(centre.lossyScale.x);
            return radius > 0f && (worldPoint - centre.position).sqrMagnitude <= radius * radius;
        }

        bool Hovering()
        {
            if (mouse == null || !mouse.isActiveAndEnabled || surfaces == null) return false;
            if (!mouse.TryGetCurrent3DRaycastHit(out var hit) || hit.collider == null) return false;
            var hitTransform = hit.collider.transform;
            foreach (var surface in surfaces)
                if (surface != null && (hitTransform.IsChildOf(surface.transform) || surface.transform.IsChildOf(hitTransform))) return true;
            return false;
        }

        void ApplyTouch(float eased)
        {
            applied = level;
            if (surfaces == null) return;
            foreach (var surface in surfaces)
            {
                if (surface == null) continue;
                surface.GetPropertyBlock(block);
                block.SetFloat(TouchId, eased);
                surface.SetPropertyBlock(block);
            }
        }

        void Rumbling(float eased)
        {
            if (rumble == null) return;
            if (eased <= 0.001f)
            {
                if (rumbleSource != null && Grabbable.Bus != null) Grabbable.Bus.Release(rumbleSource);
                rumbleSource = null;
                return;
            }

            if (rumbleSource == null)
            {
                if (Grabbable.Bus == null) return;
                rumbleSource = Grabbable.Bus.Loop(rumble);
                if (rumbleSource == null) return;
                rumbleSource.transform.SetParent(Centre(), false);
                rumbleSource.transform.localPosition = Vector3.zero;
                rumbleSource.rolloffMode = AudioRolloffMode.Linear;
                rumbleSource.minDistance = rumbleNearMetres;
                rumbleSource.maxDistance = rumbleFarMetres;
            }

            rumbleSource.volume = rumbleVolume * eased;
        }

        void DriveFlares(float dt)
        {
            for (var i = 0; flareBlocks != null && i < flareBlocks.Length; i++)
            {
                var t = 0f;
                if (flareWait[i] > 0f) flareWait[i] -= dt;
                else
                {
                    var length = Mathf.Max(0.01f, flareLength[i]);
                    flareAge[i] += dt;
                    if (flareAge[i] >= length)
                    {
                        flareAge[i] = 0f;
                        flareWait[i] = Random.Range(flareGapSecondsRange.x, flareGapSecondsRange.y);
                        flareLength[i] = Random.Range(flareSecondsRange.x, flareSecondsRange.y);
                    }
                    else t = flareAge[i] / length;
                }

                var scale = t <= 0f ? 0f
                    : t < 0.5f ? Mathf.Lerp(0f, flareScale, Mathf.SmoothStep(0f, 1f, t * 2f))
                    : Mathf.Lerp(flareScale, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.5f) * 2f));
                var root = flareRoots != null && i < flareRoots.Length ? flareRoots[i] : null;
                if (root != null) root.localScale = Vector3.one * scale;

                var quad = Quad(i);
                if (quad == null) continue;
                var offset = Vector2.Lerp(Pick(flareFrom, i), Pick(flareTo, i), t);
                quad.GetPropertyBlock(flareBlocks[i]);
                flareBlocks[i].SetVector(TexStId, new Vector4(flareTiling[i].x, flareTiling[i].y, offset.x, offset.y));
                quad.SetPropertyBlock(flareBlocks[i]);
            }
        }

        void DriveGlow()
        {
            var view = View();
            if (glow == null || view == null) return;
            var distance = Vector3.Distance(view.position, glow.transform.position);
            var p = glowParams;
            p.x = glowDistanceCurve.Evaluate(distance);
            p.y = 1f / glowSmoothnessCurve.Evaluate(distance);
            glow.GetPropertyBlock(block);
            block.SetVector(ColorAParamsId, p);
            glow.SetPropertyBlock(block);
        }

        void DriveLens()
        {
            var view = View();
            if (lensFlare == null || view == null) return;
            lensFlare.GetPropertyBlock(block);
            block.SetFloat(DistFromCameraId, Vector3.Distance(view.position, lensFlare.transform.position));
            block.SetFloat(CurrentScaleId, lensFlare.transform.lossyScale.x);
            lensFlare.SetPropertyBlock(block);
        }

        void Spin(float dt)
        {
            if (!Mathf.Approximately(spinDegreesPerSecond, 0f)) Centre().localRotation *= Quaternion.AngleAxis(spinDegreesPerSecond * dt, Vector3.up);
            if (flareGroup != null) flareGroup.localRotation *= Quaternion.Euler(flareSpinDegreesPerSecond * dt);
        }

        Transform Centre() => body != null ? body : transform;

        Renderer Quad(int i) => flares != null && i < flares.Length ? flares[i] : null;

        Transform View()
        {
            if (cam == null) cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        static Vector2 Pick(Vector2[] values, int i) => values != null && values.Length > 0 ? values[i % values.Length] : Vector2.zero;
    }
}
