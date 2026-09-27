using System.Collections;
using UnityEngine;

namespace Cosmic.Companion
{
    public class Look : MonoBehaviour
    {
        [SerializeField] Renderer points;
        [SerializeField] Renderer rim;
        [SerializeField] float idleSpin = 2f;
        [SerializeField] float listenSpin = 3f;
        [SerializeField] float thinkSpin = 9f;
        [SerializeField] float speakSpin = 5f;
        [SerializeField] float rimIdle = 0.15f;
        [SerializeField] float rimListening = 0.55f;
        [SerializeField] float rimHeard = 0.45f;
        [SerializeField] float listenBreathHz = 1.2f;
        [SerializeField] float revealSeconds = 0.6f;
        [SerializeField] float pressSwellFraction = 0.14f;
        [SerializeField] float pressSeconds = 1f;
        [SerializeField] float pressRiseFraction = 0.12f;
        [SerializeField] float restDensity = 0.08f;
        [SerializeField] float listenDensity = 0.3f;
        [SerializeField] float speakInward = 0.35f;
        [SerializeField] float voiceInward = 0.45f;
        [SerializeField] float heardInward = 0.25f;
        [SerializeField] float listenActive = 0.35f;
        [SerializeField] float articulateFraction = 0.35f;
        [SerializeField] float poseFadePerSecond = 4f;
        [SerializeField] float followPerSecond = 18f;
        [SerializeField] float touchDriftMetres = 0.3f;

        static readonly int Multiplier = Shader.PropertyToID("_Multiplier");
        static readonly int Blend = Shader.PropertyToID("_Blend");
        static readonly int Active = Shader.PropertyToID("_Active");
        static readonly int BandsId = Shader.PropertyToID("_Bands");
        static readonly int Articulate = Shader.PropertyToID("_Articulate");
        static readonly int SelfTime = Shader.PropertyToID("_SelfTime");
        static readonly int TouchPoint = Shader.PropertyToID("_TouchPoint");
        static readonly int Density = Shader.PropertyToID("_Density");
        static readonly int Inward = Shader.PropertyToID("_Inward");
        static readonly Vector4 Nowhere = new Vector4(1e6f, 1e6f, 1e6f, 0f);

        MaterialPropertyBlock pointBlock, rimBlock;
        Vector3 pointScale = Vector3.one, rimScale = Vector3.one, touch;
        float reveal, press, pressAge = float.MaxValue, voice, heard, speaking, listening, spin;
        bool ready;

        public void Press(Vector3 at)
        {
            Init();
            pressAge = 0f;
            touch = at;
        }

        public void Apply(Being.Phase phase, float loudness, Vector4 bands, float micLevel)
        {
            Init();
            var dt = Time.deltaTime;
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            var breath = 0.5f + 0.5f * Mathf.Sin(Time.time * listenBreathHz * 2f * Mathf.PI);
            speaking = Mathf.MoveTowards(speaking, phase == Being.Phase.Speaking ? 1f : 0f, poseFadePerSecond * dt);
            listening = Mathf.MoveTowards(listening, phase == Being.Phase.Listening ? 1f : 0f, poseFadePerSecond * dt);
            heard = Mathf.Lerp(heard, phase == Being.Phase.Listening ? Mathf.Clamp01(micLevel * 8f) : 0f, Mathf.Clamp01(followPerSecond * dt));
            voice = Mathf.Lerp(voice, phase == Being.Phase.Speaking ? Mathf.Clamp01(loudness) : 0f, Mathf.Clamp01(followPerSecond * dt));
            var glow = phase switch
            {
                Being.Phase.Listening => rimListening * (0.7f + 0.3f * breath) + rimHeard * heard,
                Being.Phase.Thinking => rimIdle + 0.2f * pulse,
                Being.Phase.Speaking => Mathf.Clamp01(rimIdle + voice * 0.85f),
                _ => rimIdle,
            };
            spin += dt * (phase switch { Being.Phase.Thinking => thinkSpin, Being.Phase.Speaking => speakSpin, Being.Phase.Listening => listenSpin, _ => idleSpin });
            press = Pop(dt);
            var factor = 1f + press * pressSwellFraction;
            points.transform.localScale = pointScale * factor;
            if (rim != null) rim.transform.localScale = rimScale * factor;
            var outward = touch - transform.position;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            var density = Mathf.Max(Mathf.Lerp(restDensity, listenDensity, listening), Mathf.Lerp(restDensity, 1f, speaking));
            var inward = Mathf.Clamp01(speaking * (speakInward + voiceInward * voice) + listening * heardInward * heard);
            pointBlock.SetFloat(SelfTime, spin);
            pointBlock.SetFloat(Active, Mathf.Max(press, listening * listenActive));
            pointBlock.SetFloat(Blend, reveal);
            pointBlock.SetVector(BandsId, phase == Being.Phase.Speaking ? bands : Vector4.zero);
            pointBlock.SetFloat(Articulate, articulateFraction);
            pointBlock.SetFloat(Density, density);
            pointBlock.SetFloat(Inward, inward);
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
