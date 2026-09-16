using System.Collections;
using UnityEngine;

namespace CosmicSimulation.Being
{
    /// <summary>
    /// What the being looks like. The body is the intro's placement sphere - the same point cloud and rim the
    /// player already met when they put the app on their floor - so the being reads as something the app was
    /// always made of rather than a widget bolted on.
    ///
    /// <para>Three things move: the rim's brightness, the speed the points turn at, and the sphere's size. The
    /// size is what makes it read as alive. A press pops it briefly, which is the "I felt that" the player gets
    /// back from a tap.</para>
    ///
    /// <para><b>The talking pose</b> (owner's direction, 16 Sep) goes inward, not outward. The being is built on
    /// the intro ball's own dense mesh but shows only a sparse share of its points at rest. While it speaks the
    /// rest fade in and every point falls into the sphere to its own depth, harder where its belt of the voice
    /// is loud, so the shell fills in and it reads like the intro's ball.</para>
    /// </summary>
    public class BeingVisual : MonoBehaviour
    {
        [SerializeField] private PlacementObject hologram;
        [SerializeField] private Renderer rim;
        [SerializeField] private float idleSpeed = 2f;
        [SerializeField] private float thinkSpeed = 9f;
        [SerializeField] private float speakSpeed = 5f;
        [SerializeField] private float rimIdle = 0.15f;
        [SerializeField] private float rimListening = 0.55f;
        [SerializeField] private float revealSeconds = 0.6f;

        [Header("Answering a touch")]
        [Tooltip("How far the sphere pops on a press, as a fraction of its size.")]
        [SerializeField] private float pressSwell = 0.14f;

        [Tooltip("Seconds from a press to the sphere being back at its default size. The pop peaks early and " +
                 "eases home over the rest, so every tap reads as one click rather than a lingering swell.")]
        [SerializeField] private float pressSeconds = 1f;

        [Tooltip("Fraction of pressSeconds spent growing. The rest is the ease back.")]
        [SerializeField] private float pressRise = 0.12f;

        [Header("The talking pose")]
        [Tooltip("How far the sphere swells at full voice, as a fraction of its size. 0: the pose goes inward now.")]
        [SerializeField] private float speakSwell;

        [Tooltip("How far a belt of points pushes out at full band energy, as a fraction of the radius. 0: inward now.")]
        [SerializeField] private float articulate;

        [Tooltip("Share of the mesh's points shown at rest. The intro mesh is dense; this keeps the resting look sparse.")]
        [SerializeField] private float restDensity = 0.08f;

        [Tooltip("How far points fall into the sphere while speaking, before the voice adds to it. 0..1 of the radius.")]
        [SerializeField] private float speakInward = 0.35f;

        [Tooltip("How much a loud syllable adds to the inward pull.")]
        [SerializeField] private float voiceInward = 0.45f;

        [Tooltip("How quickly the speaking pose comes and goes, per second.")]
        [SerializeField] private float speakFade = 4f;

        [Tooltip("How quickly the sphere follows the voice. Too fast reads as jitter, too slow as lag.")]
        [SerializeField] private float speakFollow = 18f;

        [Tooltip("How far the touch glow drifts off the surface as a press fades, in metres.")]
        [SerializeField] private float touchDrift = 0.3f;

        private static readonly int Multiplier = Shader.PropertyToID("_Multiplier");
        private static readonly int Blend = Shader.PropertyToID("_Blend");
        private static readonly int Active = Shader.PropertyToID("_Active");
        private static readonly int BandsId = Shader.PropertyToID("_Bands");
        private static readonly int Articulate = Shader.PropertyToID("_Articulate");
        private static readonly int Proximity = Shader.PropertyToID("_ProximityLightData");
        private static readonly int Density = Shader.PropertyToID("_Density");
        private static readonly int Inward = Shader.PropertyToID("_Inward");

        private MaterialPropertyBlock _block;
        private Material _holo;
        private float _reveal;

        private Vector3 _hologramScale = Vector3.one;
        private Vector3 _rimScale = Vector3.one;
        private readonly Vector4[] _lights = new Vector4[12];
        private Vector3 _touch;
        private float _press;
        private float _pressAge = float.MaxValue;
        private float _voice;
        private float _speaking;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _holo = hologram.GetComponent<Renderer>().material;
            for (var i = 0; i < _lights.Length; i++)
            {
                _lights[i] = Vector3.one * 1e6f;
            }

            // Captured once: every swell below is a multiple of the size the builder gave them, so the sphere
            // always returns to exactly the size the builder made it (0.20 m).
            _hologramScale = hologram.transform.localScale;
            if (rim != null)
            {
                _rimScale = rim.transform.localScale;
            }

            SetReveal(0f);
        }

        /// <summary>A press landed at a point. Pops the sphere and flashes the touch colour there.</summary>
        public void Press(Vector3 at)
        {
            _pressAge = 0f;
            _touch = at;
        }

        public void Apply(CosmicBeing.Phase phase, float loudness, Vector4 bands)
        {
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            var level = phase switch
            {
                CosmicBeing.Phase.Listening => rimListening * (0.6f + 0.4f * pulse),
                CosmicBeing.Phase.Thinking => rimIdle + 0.2f * pulse,
                CosmicBeing.Phase.Speaking => Mathf.Clamp01(rimIdle + loudness * 5f),
                _ => rimIdle,
            };

            hologram.Speed = phase switch
            {
                CosmicBeing.Phase.Thinking => thinkSpeed,
                CosmicBeing.Phase.Speaking => speakSpeed,
                _ => idleSpeed,
            };
            // The shader's _Active term grows the sphere and its points as well as tinting them, so it follows the
            // press alone. Held on for a whole listen or answer it kept the being swollen for seconds after a tap;
            // the phase already shows in the rim and the speed.
            _holo.SetFloat(Active, _press);
            _holo.SetVector(BandsId, phase == CosmicBeing.Phase.Speaking ? bands : Vector4.zero);
            _holo.SetFloat(Articulate, articulate);
            _speaking = Mathf.MoveTowards(_speaking, phase == CosmicBeing.Phase.Speaking ? 1f : 0f, speakFade * Time.deltaTime);
            _holo.SetFloat(Density, Mathf.Lerp(restDensity, 1f, _speaking));
            _holo.SetFloat(Inward, Mathf.Clamp01(_speaking * (speakInward + voiceInward * Mathf.Clamp01(loudness))));
            _block.SetFloat(Multiplier, level * _reveal);
            rim.SetPropertyBlock(_block);

            Breathe(phase, loudness);
            Glow();
        }

        /// <summary>
        /// The shader's own touch response: points near a proximity light brighten to _TouchColor and grow.
        /// The light sits on the touched point and drifts outward as the press fades, so the glow blooms
        /// where the finger landed and then leaves.
        /// </summary>
        private void Glow()
        {
            var outward = _touch - transform.position;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
            var light = _press > 0f ? _touch + outward * touchDrift * (1f - _press) : Vector3.one * 1e6f;
            _lights[0] = light;
            _lights[6] = light;
            _holo.SetVectorArray(Proximity, _lights);
        }

        /// <summary>
        /// The sphere's size, frame by frame: the voice while it speaks, plus whatever is left of a press pop.
        /// Both fold into one scale so a press during an answer reads on top of the talking rather than fighting it.
        /// </summary>
        private void Breathe(CosmicBeing.Phase phase, float loudness)
        {
            var dt = Time.deltaTime;

            var wanted = phase == CosmicBeing.Phase.Speaking ? Mathf.Clamp01(loudness) : 0f;
            _voice = Mathf.Lerp(_voice, wanted, Mathf.Clamp01(speakFollow * dt));

            // A fixed-length click: up quickly, then eased back to exactly zero at pressSeconds. Timed rather
            // than decayed, so it always ends at the default size and never asymptotes.
            _pressAge += dt;
            var rise = Mathf.Max(1e-3f, pressSeconds * pressRise);
            if (_pressAge >= pressSeconds)
            {
                _press = 0f;
            }
            else if (_pressAge < rise)
            {
                _press = Mathf.SmoothStep(0f, 1f, _pressAge / rise);
            }
            else
            {
                var back = (_pressAge - rise) / Mathf.Max(1e-3f, pressSeconds - rise);
                _press = 1f - Mathf.SmoothStep(0f, 1f, back);
            }

            // Size only - the grow-in stays the shader's _Blend, the way the intro object does it.
            var factor = 1f + _voice * speakSwell + _press * pressSwell;
            hologram.transform.localScale = _hologramScale * factor;
            if (rim != null)
            {
                rim.transform.localScale = _rimScale * factor;
            }
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
