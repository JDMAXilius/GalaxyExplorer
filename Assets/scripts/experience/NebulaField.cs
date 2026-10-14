// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A nebula as a ray-marched density field in a box the player stands inside (faces flipped, marched from the eye).
    /// </summary>
    // Holds the cloud's shape (64^3); the splats carry the grain. The most expensive thing in the app, since from
    // inside it covers every pixel, so it is off by default and wants measuring on device.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class NebulaField : MonoBehaviour
    {
        private static readonly int VolumeId = Shader.PropertyToID("_Volume");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");
        private static readonly int ExtinctionId = Shader.PropertyToID("_Extinction");
        private static readonly int FloorId = Shader.PropertyToID("_Floor");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int DetailScaleId = Shader.PropertyToID("_DetailScale");
        private static readonly int DetailStrengthId = Shader.PropertyToID("_DetailStrength");
        private static readonly int WarpScaleId = Shader.PropertyToID("_WarpScale");
        private static readonly int WarpStrengthId = Shader.PropertyToID("_WarpStrength");
        private static readonly int CoreColourId = Shader.PropertyToID("_CoreColour");
        private static readonly int ShellColourId = Shader.PropertyToID("_ShellColour");
        private static readonly int ColourMixId = Shader.PropertyToID("_ColourMix");
        private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
        private const string ShaderName = "CosmicSimulation/NebulaField";

        [SerializeField]
        [Tooltip("Written by Build Nebula Fields. A player build strips any shader reached only by name, so " +
                 "the name lookup is an editor fallback and nothing more.")]
        private Shader shader;

        [SerializeField]
        [Tooltip("The baked density volume, written by Cosmic Simulation > Build Nebula Fields.")]
        private Texture3D volume;

        [SerializeField]
        [Tooltip("Half-width of the field in metres. A field costs the same at any size.")]
        private float radiusMetres = 9f;

        [SerializeField]
        [Range(8, 64)]
        [Tooltip("March steps. Fewer is cheaper and bands more; the shader dithers the start to hide it.")]
        private int steps = 48;

        [SerializeField]
        [Range(0f, 8f)]
        private float density = 2.8f;

        [SerializeField]
        [Range(0f, 8f)]
        private float emission = 7.4f;

        [SerializeField]
        [Range(0f, 8f)]
        [Tooltip("How much the gas blocks what is behind it. This is what makes it read as gas and not glow.")]
        private float extinction = 1.6f;

        [Header("Structure")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Density below this is cut away entirely. This is the knob that buys black space: without " +
                 "it every voxel has a little gas in it and the whole volume is even mist.")]
        private float densityFloor = 0.22f;

        [SerializeField]
        [Range(0.5f, 6f)]
        [Tooltip("Bends what survives the floor. Higher pushes the gas into fewer, denser places.")]
        private float contrast = 2.4f;

        [SerializeField]
        [Range(0.5f, 24f)]
        private float detailScale = 6.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float detailStrength = 0.85f;

        [SerializeField]
        [Range(0.2f, 8f)]
        private float warpScale = 1.9f;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Domain warp. This is what turns round clumps into ribbons and sheets; at zero the field is " +
                 "smooth blobs however much detail is layered on it.")]
        private float warpStrength = 0.75f;

        [Header("Colour")]
        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Colour near the middle. Hot, because the ionising star is in there.")]
        private Color coreColour = new Color(0.55f, 0.75f, 1.6f);

        [SerializeField]
        [ColorUsage(false, true)]
        [Tooltip("Colour at the rim, where the gas is cooler and the light is recombination rather than heat.")]
        private Color shellColour = new Color(1.5f, 0.42f, 0.28f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("How much the two-colour ramp overrides the plate's own colour. Low on purpose: the plate " +
                 "is a photograph of the real object and it should be what you mostly see.")]
        private float colourMix = 0.18f;

        [SerializeField]
        [Tooltip("Compiles out the warp and the procedural detail. They are six noise evaluations per march " +
                 "step and they are what a mobile GPU cannot afford; the shape survives without them.")]
        private bool cheapDetail;

        [Header("Lighting")]
        [SerializeField]
        [Tooltip("Direction the light comes from. Only used when lightStrength is above zero.")]
        private Vector3 lightDirection = new Vector3(0f, 1f, 0.35f);

        [SerializeField]
        [Range(0f, 3f)]
        [Tooltip("Zero for a nebula that glows by itself, which is most of them. Above zero for one that is " +
                 "opaque dust lit from outside - the Pillars, whose shape IS a shadow.")]
        private float lightStrength;

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Material _material;

        private void OnEnable()
        {
            ApplyPlatformBudget();
            EnsureRenderer();

            // Switched off by a missing shader: subscribing anyway would retry and log for every camera every frame.
            if (!enabled) return;

            // Also built just before a camera renders: edit-mode Update is unreliable, and a build script may
            // configure the field after OnEnable has run.
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
        }

        private void OnCameraPreRender(Camera camera)
        {
            if (camera == null || camera.cameraType == CameraType.Preview)
            {
                return;
            }

            if (_renderer == null || _material == null)
            {
                EnsureRenderer();
            }
        }

        // Builds whenever it becomes able to: a field configured or reimported after OnEnable is otherwise never built.
        private void Update()
        {
            if (_renderer == null || _material == null)
            {
                EnsureRenderer();
            }
        }

        // Measured on an RTX 3070 at Quest 3 eye resolution from inside: 0.220 ms/step full, 0.022 ms/step with the
        // warp and detail compiled out (90% of the cost). Desktop numbers; only the ratio transfers to the device.
        private void ApplyPlatformBudget()
        {
            if (Application.isEditor)
            {
                return;
            }

            if (Application.platform == RuntimePlatform.Android)
            {
                cheapDetail = true;
                steps = Mathf.Min(steps, 20);
            }
        }

        private void EnsureRenderer()
        {
            if (volume == null)
            {
                // Not an error: a volume arriving later (builder, reimport) should simply start drawing.
                return;
            }

            if (_renderer != null && _material != null)
            {
                return;
            }

#if UNITY_EDITOR
            // Prefabs built before the field existed carry nothing here; in the editor the name still resolves.
            if (shader == null) shader = Shader.Find(ShaderName);
#endif
            if (shader == null)
            {
                Debug.LogError($"NebulaField: shader '{ShaderName}' not assigned; run Cosmic Simulation > Build Nebula Fields.", this);
                enabled = false;
                return;
            }

            // Explicit == null, not ??: ?? bypasses UnityEngine.Object's overload and keeps a fake-null component.
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            _filter = filter;
            if (_filter.sharedMesh == null) _filter.sharedMesh = Box();

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _renderer = meshRenderer;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // Instanced so per-frame values never leak into the shared asset.
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _renderer.sharedMaterial = _material;
            _renderer.enabled = true;

            Push();
        }

        private void OnDisable()
        {
            Camera.onPreRender -= OnCameraPreRender;
            if (_renderer != null) _renderer.enabled = false;
            if (_material != null) DestroyImmediate(_material);
            _material = null;
        }

        private void OnValidate()
        {
            if (shader == null) shader = Shader.Find(ShaderName);
            if (Application.isPlaying && _material != null) Push();
        }

        private void Push()
        {
            _material.SetTexture(VolumeId, volume);

            // A unit box: the transform carries the size, so the shader's object-space half-width is always 0.5.
            transform.localScale = Vector3.one * (radiusMetres * 2f);
            _material.SetFloat(RadiusId, 0.5f);
            _material.SetFloat(StepsId, steps);
            _material.SetFloat(DensityId, density);
            _material.SetFloat(EmissionId, emission);
            _material.SetFloat(ExtinctionId, extinction);

            _material.SetFloat(FloorId, densityFloor);
            _material.SetFloat(ContrastId, contrast);
            _material.SetFloat(DetailScaleId, detailScale);
            _material.SetFloat(DetailStrengthId, detailStrength);
            _material.SetFloat(WarpScaleId, warpScale);
            _material.SetFloat(WarpStrengthId, warpStrength);

            _material.SetColor(CoreColourId, coreColour);
            _material.SetColor(ShellColourId, shellColour);
            _material.SetFloat(ColourMixId, colourMix);
            _material.SetVector(LightDirectionId, lightDirection.normalized);
            _material.SetFloat(LightStrengthId, lightStrength);

            if (cheapDetail) _material.EnableKeyword("NEBULA_CHEAP");
            else _material.DisableKeyword("NEBULA_CHEAP");
        }

        /// <summary>Sets the palette and structure from the builder, so each nebula's look lives in one table.</summary>
        public void Configure(Shader fieldShader, Texture3D bakedVolume, float radius, Color core, Color shell,
            float floorValue, float contrastValue, float warp, float lit = 0f, Vector3? from = null)
        {
            shader = fieldShader;
            lightStrength = lit;
            if (from.HasValue) lightDirection = from.Value;
            volume = bakedVolume;
            radiusMetres = radius;
            coreColour = core;
            shellColour = shell;
            densityFloor = floorValue;
            contrast = contrastValue;
            warpStrength = warp;
        }

        // Built rather than referenced so the component needs no prefab wiring.
        private static Mesh Box()
        {
            var mesh = new Mesh { name = "nebula_field_box" };
            var h = 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(-h, h, -h),
                new Vector3(-h, -h, h), new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h),
            };

            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                1, 6, 5, 1, 2, 6,
                5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3,
                3, 6, 2, 3, 7, 6,
                4, 1, 5, 4, 0, 1,
            };

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
