// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Draws a nebula as a marched density field: the gas itself, as opposed to the points that describe it.
    ///
    /// <para>It is a box the player stands inside, with <c>CosmicSimulation/NebulaField</c> on it and the
    /// nebula's baked <see cref="Texture3D"/> in the material. The shader flips the faces and marches from the
    /// eye, so being inside is the normal case rather than an edge case.</para>
    ///
    /// <para><b>Why this sits beside the splats rather than replacing them.</b> The field is continuous and
    /// occludes, which is what makes it read as gas, and it costs the same whatever radius it is given. What it
    /// cannot do is detail: at 64 cubed it holds the shape of the cloud, not the grain of it, and a marched
    /// field on its own looks like beautifully lit fog. The splats carry the grain - individual points the eye
    /// can catch and resolve - and the stars carry the sparkle. Three representations of the same object at
    /// three frequencies, which is what every renderer that does this well ends up doing.</para>
    ///
    /// <para><b>Cost, honestly.</b> This is the expensive one: a fragment marching 28 steps over whatever
    /// fraction of the screen the box covers, and from inside the box that fraction is all of it. On a
    /// tile-based mobile GPU that is the single most expensive thing in this app. It is off by default for
    /// exactly that reason - <see cref="enabled"/> is the toggle the research document promised - and it wants
    /// measuring on the device before it is switched on anywhere that ships.</para>
    /// </summary>
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

        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Material _material;

        private void OnEnable()
        {
            EnsureRenderer();

            // Also built at render time. Edit-mode Update ticks only when the editor feels like ticking, and
            // a field that was configured by a build script after its OnEnable had already run then sat there
            // with no renderer until something happened to poke it. A camera about to render is the one moment
            // the answer is certainly needed, so that is where the check belongs as well.
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

        /// <summary>
        /// Ticked so the field can put itself together whenever it becomes able to, rather than only at the
        /// one moment OnEnable happens to fire.
        ///
        /// <para>It has to work this way. OnEnable runs once, and a component that is added, configured and
        /// saved by a build script - or a prefab instance that is updated underneath an already-open scene -
        /// reaches a usable state well after that moment has passed. Relying on OnEnable alone left every
        /// field in the project with no renderer and no mesh, sitting at scale 1 in a scene that showed
        /// nothing. The check below is two null tests on a frame where there is nothing to do.</para>
        /// </summary>
        private void Update()
        {
            if (_renderer == null || _material == null)
            {
                EnsureRenderer();
            }
        }

        private void EnsureRenderer()
        {
            if (volume == null)
            {
                // Not an error and not a reason to switch off: a field whose volume arrives later - from the
                // builder, or from a prefab reimport - should start drawing when it does.
                return;
            }

            if (_renderer != null && _material != null)
            {
                return;
            }

            var shader = Shader.Find("CosmicSimulation/NebulaField");
            if (shader == null)
            {
                Debug.LogError("NebulaField: shader 'CosmicSimulation/NebulaField' not found.", this);
                enabled = false;
                return;
            }

            // Explicit null checks, not ?? - and this is not style.
            //
            // UnityEngine.Object overloads ==, so a destroyed or missing component compares equal to null
            // while still being a live C# reference. ?? and ?. use the language's own null test, which that
            // overload never sees: they keep the fake-null object, and the next dereference throws. That is
            // exactly what happened here - EnsureRenderer threw a NullReferenceException on the line below on
            // every single tick, so the renderer was never built, the field never drew, and because the throw
            // came out of OnEnable before the Camera.onPreRender subscription, nothing ever retried either.
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

            // Instanced so the per-frame numbers can never be written into the shared asset and land in a commit.
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
            if (Application.isPlaying && _material != null) Push();
        }

        private void Push()
        {
            _material.SetTexture(VolumeId, volume);

            // The mesh is a unit box, so the transform carries the size and the shader is told the half-width
            // it should march in its own space - which is 0.5 for a unit box, whatever metres that becomes.
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
        }

        /// <summary>
        /// Sets the palette and the structure knobs from the builder, so a nebula's look is written down in
        /// one table rather than clicked into seven prefabs.
        /// </summary>
        public void Configure(Texture3D bakedVolume, float radius, Color core, Color shell,
            float floorValue, float contrastValue, float warp)
        {
            volume = bakedVolume;
            radiusMetres = radius;
            coreColour = core;
            shellColour = shell;
            densityFloor = floorValue;
            contrast = contrastValue;
            warpStrength = warp;
        }

        /// <summary>A unit cube. Built rather than referenced so the component needs no prefab wiring.</summary>
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
