// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// Draws the Cosmic Web (GDD 4.7). Owns the point buffer <see cref="CosmicWebGenerator"/> fills, the
    /// material instance that reads it, and the command buffer that issues the one draw call.
    ///
    /// It is not a second <c>DrawStars</c>. <c>DrawStars</c> is the galaxy's controller: every frame it reads
    /// <c>SpiralGalaxy</c> for tint, ellipse radii, fuzzy-side scale and transition alpha, orders its three
    /// layers by creation order, and switches between an eye buffer and three down-scaled render targets. None
    /// of that exists here, and a <c>SpiralGalaxy</c> stood up to satisfy it would immediately generate a spiral
    /// of its own in <c>Start</c>. What is genuinely shared is reused verbatim: the
    /// <see cref="StarVertDescriptor"/> struct and its stride, <c>cginc/StarQuad.cginc</c>'s vertex-stage quad
    /// expansion, and the <c>CommandBuffer.DrawProcedural</c> call itself.
    ///
    /// The command-buffer lifecycle is <c>OrbitalTrail.OrbitsRenderer</c>'s, for the reason recorded there: an
    /// immediate-mode draw from <c>OnPostRender</c> reaches only one eye on a headset, and a command buffer on
    /// the camera reaches both.
    ///
    /// **Why the points are generated at run time rather than baked.** A baked <c>StarsData</c> asset is how
    /// the galaxy does it, and at 8 320 stars that asset is already 1.9 MB of YAML; the web is fourteen times
    /// larger and would be a 28 MB text asset in the repository. Generation is deterministic from
    /// <see cref="seed"/>, so a run-time build is just as reproducible and costs nothing to store. It is spread
    /// over frames against a millisecond budget and each finished slice is uploaded as it lands, so the web
    /// condenses into view over about a second instead of freezing the headset while it is built.
    ///
    /// **Nothing here moves the transform.** The GDD's 0.5 deg/min drift of the volume is a shader uniform, not
    /// a rotation, so it can never fight a two-handed grab, and the object stays wherever the player put it.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public class CosmicWebRenderer : MonoBehaviour
    {
        /// <summary>
        /// Sprites in the whole web, and a tenth of the 400 000 the whole frame is allowed across both eyes
        /// (Technical Overview 7.4).
        ///
        /// It was 120 000, which is thirty percent of the entire frame budget spent on one decorative volume and
        /// was never measured on a headset. Three reasons for a tenth instead. One decorative object taking a
        /// tenth of the frame's geometry is defensible with no profile in hand; taking a third is not. It is
        /// still 2.3x Andromeda's 17 200 points, which is the only point cloud in this app that has been seen
        /// running well on the device and which reads as a dense galaxy — and Andromeda is looked *at*, where
        /// the web is stood *inside*, so its points spread across the whole field of view instead of crowding
        /// into one disc and the same count covers far more screen. And the real cost of an additive sprite
        /// cloud on a tiled mobile GPU is fill, not count: at roughly 3-4 px across, 40 000 sprites are about
        /// half a million shaded fragments per eye per frame of pure overdraw, where 120 000 were one and a
        /// half million.
        ///
        /// Nothing clamps to it. It is the default of a serialized field, so any value is still reachable from
        /// the inspector, and <c>CosmicWebBuilder</c> writes it into the prefab from a constant of its own.
        /// CS-066 measures frame time on device and moves the number.
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

        /// <summary>
        /// Throws the current web away and builds a new one from the serialized settings. Safe to call the same
        /// frame the component is added.
        /// </summary>
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
            // Awake has not necessarily run when a caller adds this component and uses it the same frame, so
            // everything initialises from wherever it is first needed rather than from one entry point.
            EnsureMaterial();

            // Unity stops a coroutine when its component is disabled and never restarts it, so a web that was
            // still building when its experience closed would have stayed frozen at however many points it had
            // reached. The builder, the staging array and the upload offset are all fields, so this picks the
            // same build up where it stopped rather than starting a new one.
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

            // Radians, wrapped: at 0.5 deg/min this would otherwise take days to lose precision, but a tuned-up
            // rate and a long session would get there.
            _age = Mathf.Repeat(_age + Time.deltaTime * rotationDegreesPerMinute * Mathf.Deg2Rad / 60f, Mathf.PI * 2f);

            _material.SetFloat(AgeId, _age);
            _material.SetFloat(TransitionAlphaId, TransitionAlpha);

            // The quad is expanded in clip space, so the object matrix never reaches it; the world scale has to
            // be applied to the sprite size by hand, exactly as DrawStars does for the galaxy.
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

                // Tracked separately from _visible: with the reveal off, nothing is drawn until the end, but the
                // upload offset still has to advance or every slice would land on top of the first one.
                _uploaded = 0;
                _visible = 0;
            }
            else if (_points == null)
            {
                // Only reachable if a build were interrupted after the staging array was dropped, which cannot
                // happen today — but indexing a null here would be a crash rather than a slow frame.
                _points = new StarVertDescriptor[_builder.Capacity];
            }

            if (_buffer == null)
            {
                _buffer = new ComputeBuffer(_builder.Capacity, StarVertDescriptor.StructSize);
            }

            // Bound here every time rather than once: a material instance can be made after the buffer (a
            // resumed build) or before it (a fresh one), and both orders have to end up with the buffer bound.
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

            // The staging array is the same size as the buffer; holding it would double the web's memory for
            // the life of the experience and it is rebuilt from scratch anyway.
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
                // One line, once, naming the number that actually reached the GPU. Nothing else in the app can
                // say whether this volume is drawing: it has no Renderer, so it appears in no frame debugger
                // hierarchy and no statistics window count.
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

            // Instanced, because the per-frame age, scale and alpha would otherwise be written into the shared
            // asset and land in the next commit (see the working rules in CLAUDE.md).
            _material = new Material(pointsMaterial) { name = pointsMaterial.name + " (web instance)" };

            if (_buffer != null)
            {
                _material.SetBuffer(StarsId, _buffer);
            }
        }

        /// <summary>
        /// Where the draw can actually be inserted on <paramref name="camera"/>.
        ///
        /// This is what made the web invisible. The built-in pipeline raises <c>BeforeSkybox</c> and
        /// <c>AfterSkybox</c> from inside the skybox pass, and that pass only runs when a camera's clear flags
        /// ask for a skybox. This app's camera never does: <c>main_camera_prefab</c> is authored as
        /// <c>SolidColor</c>, and <c>ExperienceModeManager.Apply</c> rewrites the clear flags to
        /// <c>SolidColor</c> on every passthrough/VR switch so Meta's compositor can key on the eye buffer's
        /// alpha. A command buffer parked on <c>AfterSkybox</c> was therefore attached, recorded and never once
        /// executed, and the player saw black. <c>DrawStars</c> — the same DrawProcedural path, and the reason
        /// Andromeda renders — sits on <c>BeforeForwardOpaque</c>, which has no such condition.
        ///
        /// <see cref="cameraEvent"/> now defaults to <c>BeforeForwardAlpha</c>, the same place in the frame the
        /// old value meant (opaque depth laid down, transparents and world-space UI still to come) on a pass
        /// that always runs. The remap below covers the serialized value in a prefab built before this fix, and
        /// says so once, because a silently dead command buffer is exactly the failure this is here to prevent.
        /// </summary>
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

                // Log, not LogWarning: it is a corrected setting, not a fault, and a warning here would fail
                // the editor relay's NOT-OK check on an otherwise good run.
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
            // Removed from the event it was added with, not from the serialized field: those differ whenever
            // ResolveCameraEvent has had to step in, and removing from the wrong event leaves the old buffer
            // attached and drawing for ever.
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

            // Six vertices per point: two triangles of a camera-facing quad, expanded from SV_VertexID by
            // cginc/StarQuad.cginc, because Quest's driver will not run a geometry shader under multiview.
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
