using System.Collections;
using UnityEngine;

namespace CosmicSimulation.Being
{
    public class BeingVisual : MonoBehaviour
    {
        [SerializeField] private PlacementObject hologram;
        [SerializeField] private Renderer rim;
        [SerializeField] private float idleSpeed = 2f;
        [SerializeField] private float thinkSpeed = 9f;
        [SerializeField] private float rimIdle = 0.15f;
        [SerializeField] private float rimListening = 0.55f;
        [SerializeField] private float revealSeconds = 0.6f;

        private static readonly int Multiplier = Shader.PropertyToID("_Multiplier");
        private static readonly int Blend = Shader.PropertyToID("_Blend");

        private MaterialPropertyBlock _block;
        private Material _holo;
        private float _reveal;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _holo = hologram.GetComponent<Renderer>().material;
            SetReveal(0f);
        }

        public void Apply(CosmicBeing.Phase phase, float loudness)
        {
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            var level = phase switch
            {
                CosmicBeing.Phase.Listening => rimListening * (0.6f + 0.4f * pulse),
                CosmicBeing.Phase.Thinking => rimIdle + 0.2f * pulse,
                CosmicBeing.Phase.Speaking => Mathf.Clamp01(rimIdle + loudness * 5f),
                _ => rimIdle,
            };

            hologram.Speed = phase == CosmicBeing.Phase.Thinking ? thinkSpeed : idleSpeed;
            hologram.SetActive(phase == CosmicBeing.Phase.Listening || phase == CosmicBeing.Phase.Speaking);
            _block.SetFloat(Multiplier, level * _reveal);
            rim.SetPropertyBlock(_block);
        }

        public IEnumerator Reveal(bool shown)
        {
            var from = _reveal;
            var to = shown ? 1f : 0f;
            for (var t = 0f; t < revealSeconds; t += Time.deltaTime)
            {
                SetReveal(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t / revealSeconds, 3f)));
                yield return null;
            }
            SetReveal(to);
        }

        private void SetReveal(float value)
        {
            _reveal = value;
            _holo.SetFloat(Blend, value);
        }
    }
}
