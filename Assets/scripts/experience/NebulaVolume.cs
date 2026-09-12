// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// Draws a nebula as gas standing in three dimensions: tens of thousands of additive point sprites placed
    /// through a volume, which the player can walk into and around and which has a real front and back.
    ///
    /// <para><b>Why this exists next to <see cref="PlaceShell"/> rather than instead of it.</b> The shell is a
    /// photograph projected onto a dome. It is honest about that and it is the right thing for the <i>far</i>
    /// backdrop, where nothing has parallax anyway and a real photograph beats anything we could model. It is
    /// the wrong thing for the gas you are supposed to be standing in, because a picture of a cloud does not
    /// become a cloud by being large - move your head and it slides with you, which is the single cue that
    /// tells a player they are looking at a wall. So the two divide the job: the shell is the sky behind, this
    /// is the nebula itself.</para>
    ///
    /// <para><b>The points are baked, not generated.</b> <c>NebulaVolumeBuilder</c> reads the destination's own
    /// plate in the editor and writes a <see cref="NebulaVolumeData"/> asset; this loads it and draws it. That
    /// is the galaxies' arrangement (<c>StarsData</c> + <c>DrawStars</c>) rather than the Cosmic Web's
    /// generate-at-runtime one, because a nebula's structure comes from a photograph that cannot change at run
    /// time, and a Quest has better things to do on load than re-derive it.</para>
    ///
    /// <para><b>Cost.</b> One draw call, six vertices per point. At the default 28 000 points that is 56 000
    /// triangles per eye - about 14% of the 400 000-point frame budget in Technical Overview 7.4, and this is
    /// the only heavy thing on screen while a destination is open. The real cost on a tiled mobile GPU is fill
    /// rather than count, which is what <see cref="pointSizeMetres"/> controls and why the near fade in the
    /// shader matters more than it looks.</para>
    ///
    /// <para>The buffer, the command buffer and the material instance are all owned here and all released in
    /// <c>OnDestroy</c>; the material is instanced so that the per-frame age, scale and alpha cannot be written
    /// into the shared asset and land in the next commit.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NebulaVolume : MonoBehaviour
    {
        private static readonly int StarsId = Shader.PropertyToID("_Stars");
        private static readonly int WsScaleId = Shader.PropertyToID("_WSScale");
        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");

        [SerializeField]
        [Tooltip("The baked cloud, written by Cosmic Simulation > Build Nebula Volumes.")]
        private NebulaVolumeData data;

        [SerializeField]
        [Tooltip("Material on CosmicSimulation/NebulaVolume. The builder creates and assigns it.")]
        private Material pointsMaterial;

        [SerializeField]
        [Tooltip("Where in the frame the draw is inserted. See ResolveCameraEvent before changing this.")]
        private CameraEvent cameraEvent = CameraEvent.BeforeForwardAlpha;

        [SerializeField]
        [Tooltip("Half-width of one point sprite in metres, at this object's local scale 1.")]
        private float pointSizeMetres = 0.012f;

        [SerializeField]
        [Tooltip("How far the whole cloud turns about its vertical axis, in degrees per minute. Slow on " +
                 "purpose: enough that the gas is alive, not enough to notice it moving.")]
        private float rotationDegreesPerMinute = 0.4f;

        private ComputeBuffer _buffer;
        private CommandBuffer _commandBuffer;
        private Camera _commandBufferCamera;
        private CameraEvent _commandBufferEvent;
        private Material _material;
        private int _visible;
        private float _age;
        private bool _saidWhereItDraws;
        private bool _saidItIsEmpty;

        /// <summary>Multiplied into everything the cloud draws, for the grow-in and the fade out.</summary>
        public float TransitionAlpha { get; set; } = 1f;

        /// <summary>How many points this is drawing. Zero until the data is uploaded.</summary>
        public int PointCount => _visible;

        private void OnEnable()
        {
            EnsureBuffer();
            EnsureMaterial();
        }

        private void OnDisable()
        {
            // The command buffer goes but the compute buffer stays: a destination that is hidden and shown
            // again should not pay to re-upload 28 000 points, and the data never changes at run time.
            RemoveCommandBuffer();
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
            if (_visible <= 0)
            {
                return;
            }

            EnsureMaterial();
            if (_material == null)
            {
                return;
            }

            // Radians, wrapped: at this rate it would take days to lose precision, but a tuned-up rate and a
            // long session would get there.
            _age = Mathf.Repeat(
                _age + Time.deltaTime * rotationDegreesPerMinute * Mathf.Deg2Rad / 60f, Mathf.PI * 2f);

            _material.SetFloat(AgeId, _age);
            _material.SetFloat(TransitionAlphaId, TransitionAlpha);

            // The quad is expanded in clip space, so the object matrix never reaches it; the world scale has to
            // be applied to the sprite size by hand, exactly as DrawStars does for the galaxy. That is also
            // what makes this behave under a two-handed scale: grow the cloud and its gas grains grow with it.
            _material.SetFloat(WsScaleId, pointSizeMetres * transform.lossyScale.x);

            EnsureCommandBuffer();
            Record();
        }

        // ---------- the data

        private void EnsureBuffer()
        {
            if (_buffer != null)
            {
                return;
            }

            var points = data != null ? data.points : null;
            if (points == null || points.Length == 0)
            {
                if (!_saidItIsEmpty)
                {
                    _saidItIsEmpty = true;
                    Debug.LogError(
                        $"NebulaVolume on '{name}': no baked points. Run Cosmic Simulation > Build Nebula " +
                        "Volumes, which bakes the cloud from the destination's plate and assigns it here.");
                }

                return;
            }

            _buffer = new ComputeBuffer(points.Length, StarVertDescriptor.StructSize);
            _buffer.SetData(points);
            _visible = points.Length;

            if (_material != null)
            {
                _material.SetBuffer(StarsId, _buffer);
            }
        }

        private void ReleaseBuffer()
        {
            _buffer?.Release();
            _buffer = null;
            _visible = 0;
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
            _material = new Material(pointsMaterial) { name = pointsMaterial.name + " (volume instance)" };

            if (_buffer != null)
            {
                _material.SetBuffer(StarsId, _buffer);
            }
        }

        /// <summary>
        /// Where the draw can actually be inserted on <paramref name="camera"/>.
        ///
        /// Inherited wholesale from <see cref="CosmicWebRenderer"/>, where it was learned the hard way: the
        /// built-in pipeline raises <c>BeforeSkybox</c> and <c>AfterSkybox</c> from inside the skybox pass, and
        /// that pass only runs when a camera's clear flags ask for a skybox. This app's camera never does -
        /// <c>main_camera_prefab</c> is authored as <c>SolidColor</c> and <c>ExperienceModeManager.Apply</c>
        /// rewrites the clear flags to <c>SolidColor</c> on every passthrough/VR switch, so Meta's compositor
        /// can key on the eye buffer's alpha. A command buffer parked there is attached, recorded, and never
        /// once executed, and the player sees nothing at all with no error anywhere.
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
                    $"NebulaVolume on '{name}': camera '{camera.name}' clears to {camera.clearFlags}, so it runs " +
                    $"no skybox pass and nothing on {cameraEvent} would ever execute. Drawing on " +
                    $"{CameraEvent.BeforeForwardAlpha} instead.");
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

            _commandBuffer = new CommandBuffer { name = "Nebula volume" };
            camera.AddCommandBuffer(when, _commandBuffer);
            _commandBufferCamera = camera;
            _commandBufferEvent = when;
        }

        private void RemoveCommandBuffer()
        {
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
            if (_commandBuffer == null || _material == null || _visible <= 0)
            {
                return;
            }

            _commandBuffer.Clear();
            _commandBuffer.DrawProcedural(
                transform.localToWorldMatrix, _material, 0, MeshTopology.Triangles, _visible * 6);
        }
    }
}
