using System.Collections;
using UnityEngine;

namespace Cosmic
{
    public class BeingLook : MonoBehaviour
    {
        [SerializeField] Renderer points;
        [SerializeField] Renderer rim;
        [SerializeField] float idleSpin = 2f;
        [SerializeField] float thinkSpin = 9f;
        [SerializeField] float speakSpin = 5f;
        [SerializeField] float rimIdle = 0.15f;
        [SerializeField] float rimListening = 0.55f;
        [SerializeField] float revealSeconds = 0.6f;
        [SerializeField] float pressSwellFraction = 0.14f;
        [SerializeField] float pressSeconds = 1f;
        [SerializeField] float pressRiseFraction = 0.12f;
        [SerializeField] float speakSwellFraction = 0.3f;
        [SerializeField] float articulateFraction = 0.35f;
        [SerializeField] float speakFollowPerSecond = 18f;
        [SerializeField] float touchDriftMetres = 0.3f;

        static readonly int Multiplier = Shader.PropertyToID("_Multiplier");
        static readonly int Blend = Shader.PropertyToID("_Blend");
        static readonly int Active = Shader.PropertyToID("_Active");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int Articulate = Shader.PropertyToID("_Articulate");
        static readonly int SelfTime = Shader.PropertyToID("_SelfTime");
        static readonly int TouchPoint = Shader.PropertyToID("_TouchPoint");
        static readonly Vector4 Nowhere = new Vector4(1e6f, 1e6f, 1e6f, 0f);

        MaterialPropertyBlock pointBlock, rimBlock;
        Vector3 pointScale = Vector3.one, rimScale = Vector3.one, touch;
        float reveal, press, pressAge = float.MaxValue, voice, spin;
        bool ready;

        public void Press(Vector3 at)
        {
            Init();
            pressAge = 0f;
            touch = at;
        }

        public void Apply(Being.Phase phase, float loudness, Vector4 bands)
        {
            Init();
            var dt = Time.deltaTime;
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            var glow = phase switch
            {
                Being.Phase.Listening => rimListening * (0.6f + 0.4f * pulse),
                Being.Phase.Thinking => rimIdle + 0.2f * pulse,
                Being.Phase.Speaking => Mathf.Clamp01(rimIdle + loudness * 5f),
                _ => rimIdle,
            };
            spin += dt * (phase == Being.Phase.Thinking ? thinkSpin : phase == Being.Phase.Speaking ? speakSpin : idleSpin);
            voice = Mathf.Lerp(voice, phase == Being.Phase.Speaking ? Mathf.Clamp01(loudness) : 0f, Mathf.Clamp01(speakFollowPerSecond * dt));
            press = Pop(dt);
            var factor = 1f + voice * speakSwellFraction + press * pressSwellFraction;
            points.transform.localScale = pointScale * factor;
            if (rim != null) rim.transform.localScale = rimScale * factor;
            var outward = touch - transform.position;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            pointBlock.SetFloat(SelfTime, spin);
            // _Active grows the points as well as tinting them, so it follows the press only; the phase shows in the rim and the spin.
            pointBlock.SetFloat(Active, press);
            pointBlock.SetFloat(Blend, reveal);
            pointBlock.SetVector(BandsId, phase == Being.Phase.Speaking ? bands : Vector4.zero);
            pointBlock.SetFloat(Articulate, articulateFraction);
            pointBlock.SetVector(TouchPoint, press > 0f ? (Vector4)(touch + outward * touchDriftMetres * (1f - press)) : Nowhere);
            points.SetPropertyBlock(pointBlock);
            if (rim == null) return;
            rimBlock.SetFloat(Multiplier, glow * reveal);
            rim.SetPropertyBlock(rimBlock);
        }

        public IEnumerator Reveal(bool shown)
        {
            Init();
            var from = reveal;
            var to = shown ? 1f : 0f;
            for (var t = 0f; t < revealSeconds; t += Time.deltaTime)
            {
                reveal = Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t / revealSeconds, 3f));
                yield return null;
            }
            reveal = to;
        }

        float Pop(float dt)
        {
            pressAge += dt;
            var rise = Mathf.Max(1e-3f, pressSeconds * pressRiseFraction);
            if (pressAge >= pressSeconds) return 0f;
            if (pressAge < rise) return Mathf.SmoothStep(0f, 1f, pressAge / rise);
            return 1f - Mathf.SmoothStep(0f, 1f, (pressAge - rise) / Mathf.Max(1e-3f, pressSeconds - rise));
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            pointBlock = new MaterialPropertyBlock();
            rimBlock = new MaterialPropertyBlock();
            pointScale = points.transform.localScale;
            if (rim != null) rimScale = rim.transform.localScale;
        }
    }
}
