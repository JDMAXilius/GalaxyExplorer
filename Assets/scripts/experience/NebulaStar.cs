// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The star at the centre of a nebula: one blown-out point with a halo and a set of spikes.
    ///
    /// <para><b>Why this is its own component and not part of the field.</b> The gas is volumetric and the
    /// star is not - it is a point source, drawn as a billboard, and trying to express it as density in the
    /// marched volume would need a resolution the field does not have and could never make a sharp core. Two
    /// representations because there are two kinds of thing here: one diffuse, one a point.</para>
    ///
    /// <para><b>It is the reason the picture is composed.</b> Every reference image for this work is built
    /// around a central star - the gas is arranged about it, lit by it, thrown outward by it. A nebula without
    /// one reads as a cloud that happens to glow. This is cheap to draw and it does more for the look than
    /// anything else of comparable cost.</para>
    ///
    /// <para>It needs <see cref="Bloom"/> on the camera. The colour it writes goes well above 1, and bloom is
    /// what turns that into a halo; without it this is a bright cross and nothing more.</para>
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

            // Built at render time as well as on enable, for the same reason NebulaField is: a component
            // configured by a build script reaches a usable state well after its OnEnable has been and gone,
            // and edit-mode Update ticks only when the editor feels like ticking.
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
            if (_material != null) Push();
        }

        private void EnsureRenderer()
        {
            if (_renderer != null && _material != null) return;

            var shader = Shader.Find("CosmicSimulation/NebulaStar");
            if (shader == null)
            {
                Debug.LogError("NebulaStar: shader 'CosmicSimulation/NebulaStar' not found.", this);
                enabled = false;
                return;
            }

            // Explicit null checks rather than ?? - UnityEngine.Object overloads ==, which ?? never consults,
            // so ?? keeps a fake-null and the next line throws. That exact mistake cost this feature a whole
            // session; see the note in NebulaField.
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
        public void Configure(Color starColour, float starIntensity, float metres)
        {
            colour = starColour;
            intensity = starIntensity;
            sizeMetres = metres;
            if (_material != null) Push();
        }

        /// <summary>
        /// A unit quad. Its bounds are set far larger than the quad itself because the vertex stage moves the
        /// corners in view space: Unity culls on the bounds it was given, and a half-metre box around a flare
        /// that is drawn metres wide would pop out of view as the player turned their head.
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
