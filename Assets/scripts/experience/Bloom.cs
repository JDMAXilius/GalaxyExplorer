// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// Bloom over the whole scene, as a command buffer on the camera.
    ///
    /// <para><b>Why this exists.</b> There is no post-processing package in this project, and without bloom a
    /// star is one dim pixel. Everything this app draws is emissive - stars, gas, galaxies, the web - and an
    /// emissive renderer without a bloom pass is showing you the numbers rather than the light. It is also the
    /// cheapest large improvement available: one pass that lifts the galaxies, the Cosmic Web and the nebulae
    /// at once, rather than nine hand-tuned constants per effect.</para>
    ///
    /// <para><b>A command buffer, not OnRenderImage.</b> The same reason <c>CosmicWebRenderer</c> and
    /// <c>OrbitalTrail</c> give: an image effect hung off <c>OnRenderImage</c> reaches one eye on a headset,
    /// and a command buffer on the camera reaches both. The pyramid is built at half resolution and down,
    /// which is where the bandwidth is saved on a tile-based GPU - the budget that actually binds on a Quest.
    /// </para>
    ///
    /// <para>The camera must be HDR or there is nothing above 1.0 to bloom and the effect is a blur.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class Bloom : MonoBehaviour
    {
        private const int MaxLevels = 6;

        [SerializeField]
        [Tooltip("Light above this starts to bloom. Below 1 on an HDR camera catches bright gas as well as stars.")]
        [Range(0f, 4f)]
        private float threshold = 0.85f;

        [SerializeField]
        [Tooltip("How gently the threshold comes in. 0 is a hard cut and shows as a ring around bright things.")]
        [Range(0f, 1f)]
        private float knee = 0.6f;

        [SerializeField]
        [Tooltip("How much bloom is added back over the scene.")]
        [Range(0f, 4f)]
        private float intensity = 1.15f;

        [SerializeField]
        [Tooltip("How far the glow spreads between pyramid levels.")]
        [Range(0.3f, 1f)]
        private float scatter = 0.85f;

        [SerializeField]
        [Tooltip("Pyramid depth. Each level halves the resolution; more levels means a wider, softer glow.")]
        [Range(2, MaxLevels)]
        private int levels = 5;

        private Camera _camera;
        private CommandBuffer _buffer;
        private Material _material;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BloomTex = Shader.PropertyToID("_BloomTex");
        private static readonly int FilterId = Shader.PropertyToID("_Filter");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ScatterId = Shader.PropertyToID("_Scatter");
        private readonly int[] _pyramid = new int[MaxLevels];

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            _camera.allowHDR = true;

            var shader = Shader.Find("CosmicSimulation/Bloom");
            if (shader == null)
            {
                Debug.LogError("Bloom: shader 'CosmicSimulation/Bloom' not found, so nothing glows.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            for (var i = 0; i < MaxLevels; i++) _pyramid[i] = Shader.PropertyToID("_BloomLevel" + i);

            _buffer = new CommandBuffer { name = "Cosmic bloom" };
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _buffer);
        }

        private void OnDisable()
        {
            if (_camera != null && _buffer != null) _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _buffer);
            _buffer?.Release();
            _buffer = null;
            if (_material != null) DestroyImmediate(_material);
        }

        private void OnPreRender()
        {
            if (_buffer == null || _material == null) return;

            _buffer.Clear();

            var width = Mathf.Max(1, _camera.pixelWidth / 2);
            var height = Mathf.Max(1, _camera.pixelHeight / 2);
            var format = _camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

            // Soft-knee curve, packed once: the shader should not be doing this arithmetic per pixel.
            var soft = Mathf.Max(1e-4f, threshold * knee);
            _material.SetVector(FilterId, new Vector4(threshold, threshold - soft, 2f * soft, 0.25f / soft));
            _material.SetFloat(IntensityId, intensity);
            _material.SetFloat(ScatterId, scatter);

            var source = BuiltinRenderTextureType.CameraTarget;
            var depth = Mathf.Clamp(levels, 2, MaxLevels);

            _buffer.GetTemporaryRT(_pyramid[0], width, height, 0, FilterMode.Bilinear, format);
            _buffer.SetGlobalTexture(MainTex, source);
            _buffer.Blit(source, _pyramid[0], _material, 0);

            var used = 1;
            for (var i = 1; i < depth; i++)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                if (width < 4 || height < 4) break;

                _buffer.GetTemporaryRT(_pyramid[i], width, height, 0, FilterMode.Bilinear, format);
                _buffer.Blit(_pyramid[i - 1], _pyramid[i], _material, 1);
                used++;
            }

            // Back up the pyramid, each level adding into the one above it.
            for (var i = used - 1; i > 0; i--)
            {
                _buffer.Blit(_pyramid[i], _pyramid[i - 1], _material, 2);
            }

            var composed = Shader.PropertyToID("_BloomComposed");
            _buffer.GetTemporaryRT(composed, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, format);
            _buffer.SetGlobalTexture(BloomTex, _pyramid[0]);
            _buffer.Blit(source, composed, _material, 3);
            _buffer.Blit(composed, source);

            _buffer.ReleaseTemporaryRT(composed);
            for (var i = 0; i < used; i++) _buffer.ReleaseTemporaryRT(_pyramid[i]);
        }
    }
}
