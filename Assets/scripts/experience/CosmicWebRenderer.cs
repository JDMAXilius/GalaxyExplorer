// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// Draws the Cosmic Web (GDD 4.7): one DrawProcedural of the <see cref="CosmicWebGenerator"/> buffer.
    /// </summary>
    // Shares StarVertDescriptor and StarQuad.cginc with DrawStars but not its SpiralGalaxy-driven controller.
    // A camera command buffer, not OnPostRender, because an immediate draw reaches only one eye on a headset.
    // Generated at run time over frames (a baked asset would be ~28 MB of YAML); drift is a shader uniform.
    [DefaultExecutionOrder(55)]
    public class CosmicWebRenderer : MonoBehaviour
    {
        /// <summary>
        /// A tenth of the 400 000-sprite frame budget (Technical Overview 7.4); additive fill, not count, is the
        /// cost on a tiled GPU. A default only, not a clamp; CS-066 profiles it on device.
        /// </summary>
        public const int DefaultPointCount = 40000;

        private static readonly int StarsId = Shader.PropertyToID("_Stars");
        private static readonly int WsScaleId = Shader.PropertyToID("_WSScale");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Material using CosmicSimulation/CosmicWebPoints. Instanced at run time, so the asset is never written to.")]
        private Material pointsMaterial;

        [SerializeField]
        [Tooltip("Before the transparent pass: opaque depth already exists so the web is occluded properly, and " +
                 "world-space UI still draws over it. Do not use AfterSkybox — see ResolveCameraEvent.")]
        private CameraEvent cameraEvent = CameraEvent.BeforeForwardAlpha;

        [SerializeField]
        [Tooltip("Half-width of one point sprite in metres, at this object's local scale 1.")]
        private float pointSizeMetres = 0.0032f;

        [SerializeField]
        [Tooltip("Degrees per minute the whole volume drifts about its vertical axis. GDD 4.7 asks for 0.5.")]
        private float rotationDegreesPerMinute = 0.5f;

        [Header("Shape")]
        [SerializeField]
        [Tooltip("Anything makes the same web again. Change it for a different one.")]
        private int seed = 20260912;

        [SerializeField]
        [Tooltip("Point sprites in the whole web. Technical Overview 7.4 caps the whole frame at 400 000 across " +
                 "both eyes; 40 000 is a tenth of that. Raise it here once CS-066 has profiled the volume on device.")]
        private int pointCount = DefaultPointCount;

        [SerializeField]
        [Tooltip("Half-width of the volume in metres, at this object's local scale 1. GDD 4.7 asks for a 5 m volume.")]
        private float volumeRadiusMetres = 2.5f;

        [SerializeField]
        [Tooltip("Voronoi cells along one side of the volume. More cells means a finer web with smaller voids.")]
        private int cellsPerAxis = 7;

        [SerializeField]
        [Tooltip("How far a cell's site may wander from its centre, as a fraction of a cell. Above 0.5 the nearest-site search stops being exact.")]
        [Range(0f, 0.5f)]
        private float siteJitter = 0.45f;

        [Header("Structure mix")]
        [SerializeField]
        [Tooltip("Share of candidates thrown into the voids as faint tracers. These four are shares of attempts, not of the result: a node projection is rejected more often than a wall one.")]
        [Range(0f, 1f)]
        private float voidShare = 0.05f;

        [SerializeField]
        [Tooltip("Share of candidates aimed at the walls between two voids (sheets).")]
        [Range(0f, 1f)]
        private float wallShare = 0.25f;

        [SerializeField]
        [Tooltip("Share of candidates aimed at the edges where three walls meet (filaments).")]
        [Range(0f, 1f)]
        private float filamentShare = 0.50f;

        [SerializeField]
        [Tooltip("Share of candidates aimed at the vertices where four voids meet (nodes).")]
        [Range(0f, 1f)]
        private float nodeShare = 0.20f;

        [SerializeField]
        [Tooltip("Thickness of a sheet, as a fraction of a cell.")]
        private float wallThickness = 0.035f;

        [SerializeField]
        [Tooltip("Radius of a filament, as a fraction of a cell.")]
        private float filamentRadius = 0.030f;

        [SerializeField]
        [Tooltip("Radius of a node cluster, as a fraction of a cell.")]
        private float nodeRadius = 0.070f;

        [Header("Build")]
        [SerializeField]
        [Tooltip("Milliseconds of generation per frame. Zero builds the whole web in one frame, which on a headset is a visible freeze.")]
        private float buildMillisecondsPerFrame = 3f;

        [SerializeField]
        [Tooltip("Draw the points that exist while the rest are still being generated, so the web condenses into view instead of appearing all at once.")]
        private bool revealWhileBuilding = true;

        private ComputeBuffer _buffer;
        private CommandBuffer _commandBuffer;
        private Camera _commandBufferCamera;
        private CameraEvent _commandBufferEvent;
        private Material _material;
        private StarVertDescriptor[] _points;
        private CosmicWebGenerator.Builder _builder;
        private Coroutine _build;
        private int _uploaded;
        private int _visible;
        private float _age;
        private bool _buildComplete;
        private bool _saidWhereItDraws;

        /// <summary>How many points are in the buffer and being drawn.</summary>
        public int PointCount => _visible;

        /// <summary>What <see cref="pointCount"/> was asked for. Generation can fall short of it; see the log.</summary>
        public int RequestedPointCount => pointCount;

        /// <summary>Half-width of the volume in metres at local scale 1, for anything that has to size itself to the web.</summary>
        public float VolumeRadiusMetres => volumeRadiusMetres;

        /// <summary>Fades the whole web out without touching its scale. 1 is fully lit.</summary>
        public float TransitionAlpha { get; set; } = 1f;

        /// <summary>The runtime material instance, or null before the first frame. Never the shared asset.</summary>
        public Material MaterialInstance
        {
            get
            {
                EnsureMaterial();
                return _material;
            }
        }

        /// <summary>Discards the web and builds a new one from the serialized settings. Safe the frame it is added.</summary>
        public void Rebuild()
        {
            if (_build != null)
            {
                StopCoroutine(_build);
                _build = null;
            }

            ReleaseBuffer();
            _builder = null;
            _points = null;
            _uploaded = 0;
            _visible = 0;
            _buildComplete = false;

            // While disabled there is no coroutine runner; OnEnable picks the build up instead.
            if (isActiveAndEnabled)
            {
                _build = StartCoroutine(BuildRoutine());
            }
        }

        // ---------- lifecycle

        private void OnEnable()
        {
            // Awake may not have run for a same-frame AddComponent, so initialisation is lazy.
            EnsureMaterial();

            // Disabling stops the coroutine for good; the build state lives in fields so this resumes it.
            if (!_buildComplete && _build == null)
            {
                _build = StartCoroutine(BuildRoutine());
            }
        }

        private void OnDisable()
        {
            RemoveCommandBuffer();

            // Unity has already stopped the coroutine; clearing the handle is what tells OnEnable to resume it.
            _build = null;
        }

        private void OnDestroy()
        {
            RemoveCommandBuffer();
            ReleaseBuffer();

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }

        private void Update()
        {
            if (_material == null)
            {
                EnsureMaterial();
                if (_material == null)
                {
                    return;
                }
            }

            // Radians, wrapped, so a fast rate over a long session keeps float precision.
            _age = Mathf.Repeat(_age + Time.deltaTime * rotationDegreesPerMinute * Mathf.Deg2Rad / 60f, Mathf.PI * 2f);

            _material.SetFloat(AgeId, _age);
            _material.SetFloat(TransitionAlphaId, TransitionAlpha);

            // The quad is expanded in clip space, so world scale is applied to the sprite size by hand, as DrawStars does.
            _material.SetFloat(WsScaleId, pointSizeMetres * transform.lossyScale.x);

            EnsureCommandBuffer();
            Record();
        }

        // ---------- generation

        private IEnumerator BuildRoutine()
        {
            EnsureMaterial();
            if (_material == null)
            {
                Debug.LogError(
                    $"CosmicWebRenderer on '{name}': no points material. Run " +
                    "Cosmic Simulation -> Build Cosmic Web Content, which creates it and assigns it here.");

                _build = null;
                yield break;
            }

            if (_builder == null)
            {
                var settings = BuildSettings();
                if (settings.PointCount <= 0)
                {
                    _buildComplete = true;
                    _build = null;
                    yield break;
                }

                _builder = new CosmicWebGenerator.Builder(settings);
                _points = new StarVertDescriptor[_builder.Capacity];

                // Separate from _visible: with the reveal off the upload offset must still advance.
                _uploaded = 0;
                _visible = 0;
            }
            else if (_points == null)
            {
                // Defensive: a null staging array here would crash rather than cost a frame.
                _points = new StarVertDescriptor[_builder.Capacity];
            }

            if (_buffer == null)
            {
                _buffer = new ComputeBuffer(_builder.Capacity, StarVertDescriptor.StructSize);
            }

            // Bound every time: the material may be made before the buffer (fresh build) or after it (resumed).
            _material.SetBuffer(StarsId, _buffer);

            while (!_builder.Done)
            {
                var written = _builder.Step(_points, buildMillisecondsPerFrame);
                if (written > 0)
                {
                    _buffer.SetData(_points, _uploaded, _uploaded, written);
                    _uploaded += written;
                    if (revealWhileBuilding)
                    {
                        _visible = _uploaded;
                    }
                }

                if (buildMillisecondsPerFrame <= 0f)
                {
                    break; // an unbudgeted Step runs to completion, so one pass is the whole web
                }

                yield return null;
            }

            _visible = _uploaded;
            _buildComplete = true;

            // Dropped: holding the staging array would double the web's memory for the experience's life.
            _points = null;

            if (_builder.RanShort)
            {
                Debug.LogWarning(
                    $"CosmicWebRenderer on '{name}': the generator ran out of candidates at {_builder.Count} of " +
                    $"{pointCount} points. The structure shares or the snap tolerance are too tight for " +
                    "this cell size; the web is real but thinner than asked for.");
            }
            else
            {
                // With no Renderer, nothing else (frame debugger, stats) shows whether this volume is drawing.
                Debug.Log($"CosmicWebRenderer on '{name}': {_visible:N0} points built and uploaded.");
            }

            _build = null;
        }

        private CosmicWebGenerator.Settings BuildSettings()
        {
            var settings = CosmicWebGenerator.Settings.Default;
            settings.Seed = seed;
            settings.PointCount = Mathf.Max(0, pointCount);
            settings.Radius = Mathf.Max(0.01f, volumeRadiusMetres);
            settings.CellsPerAxis = cellsPerAxis;
            settings.SiteJitter = siteJitter;
            settings.VoidShare = voidShare;
            settings.WallShare = wallShare;
            settings.FilamentShare = filamentShare;
            settings.NodeShare = nodeShare;
            settings.WallThickness = wallThickness;
            settings.FilamentRadius = filamentRadius;
            settings.NodeRadius = nodeRadius;
            return settings;
        }

        // ---------- drawing

        private void EnsureMaterial()
        {
            if (_material != null || pointsMaterial == null)
            {
                return;
            }

            // Instanced so per-frame age, scale and alpha never leak into the shared asset.
            _material = new Material(pointsMaterial) { name = pointsMaterial.name + " (web instance)" };

            if (_buffer != null)
            {
                _material.SetBuffer(StarsId, _buffer);
            }
        }

        // Before/AfterSkybox fire inside the skybox pass, which never runs on this SolidColor camera (passthrough
        // keys on its alpha), so those events are remapped to BeforeForwardAlpha rather than silently never drawing.
        private CameraEvent ResolveCameraEvent(Camera camera)
        {
            var needsSkyboxPass = cameraEvent == CameraEvent.BeforeSkybox || cameraEvent == CameraEvent.AfterSkybox;
            if (!needsSkyboxPass || camera.clearFlags == CameraClearFlags.Skybox)
            {
                return cameraEvent;
            }

            if (!_saidWhereItDraws)
            {
                _saidWhereItDraws = true;

                // Log, not LogWarning: a corrected setting, and a warning would fail the relay's NOT-OK check.
                Debug.Log(
                    $"CosmicWebRenderer on '{name}': camera '{camera.name}' clears to {camera.clearFlags}, so it " +
                    $"runs no skybox pass and nothing on {cameraEvent} would ever execute. Drawing on " +
                    $"{CameraEvent.BeforeForwardAlpha} instead. Re-run Cosmic Simulation -> Build Cosmic Web " +
                    "Content to write the corrected value into the prefab.");
            }

            return CameraEvent.BeforeForwardAlpha;
        }

        private void EnsureCommandBuffer()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                RemoveCommandBuffer();
                return;
            }

            var when = ResolveCameraEvent(camera);
            if (_commandBuffer != null && camera == _commandBufferCamera && when == _commandBufferEvent)
            {
                return;
            }

            RemoveCommandBuffer();

            _commandBuffer = new CommandBuffer { name = "Cosmic web" };
            camera.AddCommandBuffer(when, _commandBuffer);
            _commandBufferCamera = camera;
            _commandBufferEvent = when;
        }

        private void RemoveCommandBuffer()
        {
            // Removed from the event it was added on, which differs from the field when ResolveCameraEvent remapped it.
            if (_commandBuffer != null && _commandBufferCamera != null)
            {
                _commandBufferCamera.RemoveCommandBuffer(_commandBufferEvent, _commandBuffer);
            }

            _commandBuffer?.Release();
            _commandBuffer = null;
            _commandBufferCamera = null;
        }

        private void Record()
        {
            if (_commandBuffer == null)
            {
                return;
            }

            _commandBuffer.Clear();
            if (_buffer == null || _visible <= 0 || _material == null)
            {
                return;
            }

            // Six vertices per point, expanded from SV_VertexID: no geometry shaders under multiview.
            _commandBuffer.DrawProcedural(transform.localToWorldMatrix, _material, 0, MeshTopology.Triangles, _visible * 6);
        }

        private void ReleaseBuffer()
        {
            if (_buffer == null)
            {
                return;
            }

            _buffer.Dispose();
            _buffer = null;
        }
    }
}
