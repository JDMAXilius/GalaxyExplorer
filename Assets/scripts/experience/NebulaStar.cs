// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The star at a nebula's centre, a billboard rather than density, since the marched volume cannot resolve a sharp core.
    /// Needs <see cref="Bloom"/> on the camera: its colour goes well above 1 and bloom is what makes the halo.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class NebulaStar : MonoBehaviour
    {
        private static readonly int ColourId = Shader.PropertyToID("_Colour");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int SpikeGainId = Shader.PropertyToID("_SpikeGain");
        private static readonly int HaloGainId = Shader.PropertyToID("_HaloGain");
        private const string ShaderName = "CosmicSimulation/NebulaStar";

        [SerializeField]
        [Tooltip("Written by Build Nebula Fields. A player build strips any shader reached only by name, so " +
                 "the name lookup is an editor fallback and nothing more.")]
        private Shader shader;

        [SerializeField]
        [ColorUsage(false, true)]
        private Color colour = new Color(1f, 0.95f, 0.85f);

        [SerializeField]
        [Range(0f, 40f)]
        [Tooltip("Above one on purpose: this has to clear the bloom threshold to get its halo.")]
        private float intensity = 9f;

        [SerializeField]
        [Tooltip("How wide the whole flare is, in metres, at the distance it sits from the player.")]
        private float sizeMetres = 2.5f;

        [SerializeField]
        [Range(0f, 8f)]
        private float spikeGain = 1.1f;

        [SerializeField]
        [Range(0f, 8f)]
        private float haloGain = 0.85f;

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Material _material;

        private void OnEnable()
        {
            EnsureRenderer();

            // A missing shader disables us above; subscribing anyway would retry and log per camera per frame.
            if (!enabled) return;

            // Also built at render time: a build script configures this after OnEnable, and edit-mode Update is unreliable.
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= OnCameraPreRender;
            if (_renderer != null) _renderer.enabled = false;
            if (_material != null) DestroyImmediate(_material);
            _material = null;
        }

        private void OnCameraPreRender(Camera camera)
        {
            if (camera == null || camera.cameraType == CameraType.Preview) return;
            if (_renderer == null || _material == null) EnsureRenderer();
        }

        private void Update()
        {
            if (_renderer == null || _material == null) EnsureRenderer();
        }

        private void OnValidate()
        {
            if (shader == null) shader = Shader.Find(ShaderName);
            if (_material != null) Push();
        }

        private void EnsureRenderer()
        {
            if (_renderer != null && _material != null) return;

#if UNITY_EDITOR
            // Editor fallback only: a player build strips shaders reached by name.
            if (shader == null) shader = Shader.Find(ShaderName);
#endif
            if (shader == null)
            {
                Debug.LogError($"NebulaStar: shader '{ShaderName}' not assigned; run Cosmic Simulation > Build Nebula Fields.", this);
                enabled = false;
                return;
            }

            // Explicit null checks, not ??: ?? ignores UnityEngine.Object's == and keeps a fake null.
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            _filter = filter;
            if (_filter.sharedMesh == null) _filter.sharedMesh = Quad();

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _renderer = meshRenderer;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _renderer.sharedMaterial = _material;
            _renderer.enabled = true;

            Push();
        }

        private void Push()
        {
            _material.SetColor(ColourId, colour);
            _material.SetFloat(IntensityId, intensity);
            _material.SetFloat(SizeId, sizeMetres);
            _material.SetFloat(SpikeGainId, spikeGain);
            _material.SetFloat(HaloGainId, haloGain);
        }

        /// <summary>Sets the look from the builder, so a nebula's star is described in the same table as its gas.</summary>
        public void Configure(Shader starShader, Color starColour, float starIntensity, float metres)
        {
            shader = starShader;
            colour = starColour;
            intensity = starIntensity;
            sizeMetres = metres;
            if (_material != null) Push();
        }

        /// <summary>
        /// A unit quad with oversized bounds: the vertex stage expands it in view space to metres wide, and Unity
        /// culls on the mesh bounds, so tight bounds would pop it out of view as the head turns.
        /// </summary>
        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "nebula_star_quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 64f);
            return mesh;
        }
    }
}
