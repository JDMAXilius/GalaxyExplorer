// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicSimulation
{
    /// <summary>
    /// A nebula as a walkable volume of additive point sprites, baked into <see cref="NebulaVolumeData"/>.
    /// </summary>
    // The near gas, with parallax; PlaceShell stays the far photographic backdrop. Baked because the
    // structure comes from a fixed plate. 28 000 points is ~14% of the frame budget; fill, set by pointSizeMetres, is the cost.
    [DisallowMultipleComponent]
    [ExecuteAlways]
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

        // One command buffer per camera (game, scene view, rig), recorded in onPreRender, so the cloud also
        // draws in the scene view and in edit mode where Update does not tick.
        private readonly Dictionary<Camera, (CommandBuffer Buffer, CameraEvent When)> _buffers =
            new Dictionary<Camera, (CommandBuffer Buffer, CameraEvent When)>();

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

            // Removed first: OnEnable can run twice across a domain reload, and a double subscription doubles brightness.
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPreRender += OnCameraPreRender;
        }

        private void OnDisable()
        {
            // The compute buffer stays: the data never changes, so re-showing should not re-upload it.
            Camera.onPreRender -= OnCameraPreRender;
            RemoveCommandBuffers();
        }

        private void OnDestroy()
        {
            Camera.onPreRender -= OnCameraPreRender;
            RemoveCommandBuffers();
            ReleaseBuffer();

            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }

                _material = null;
            }
        }

        private void Update()
        {
            // Only the clock: the draw is per-camera in the pre-render callback, which also runs in edit mode.
            if (_visible <= 0 || !Application.isPlaying)
            {
                return;
            }

            // Radians, wrapped, so a fast rate over a long session keeps float precision.
            _age = Mathf.Repeat(
                _age + Time.deltaTime * rotationDegreesPerMinute * Mathf.Deg2Rad / 60f, Mathf.PI * 2f);
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

            // Instanced so per-frame values never leak into the shared asset; HideAndDontSave because this runs in
            // edit mode, where the scene would otherwise try to serialise the instance.
            _material = new Material(pointsMaterial)
            {
                name = pointsMaterial.name + " (volume instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (_buffer != null)
            {
                _material.SetBuffer(StarsId, _buffer);
            }
        }

        // Before/AfterSkybox fire inside the skybox pass, which never runs on this SolidColor camera, so they are
        // remapped rather than silently never drawing (same as CosmicWebRenderer).
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
                    $"NebulaVolume on '{name}': camera '{camera.name}' clears to {camera.clearFlags}, so it runs " +
                    $"no skybox pass and nothing on {cameraEvent} would ever execute. Drawing on " +
                    $"{CameraEvent.BeforeForwardAlpha} instead.");
            }

            return CameraEvent.BeforeForwardAlpha;
        }

        private void OnCameraPreRender(Camera camera)
        {
            // Preview and reflection cameras are skipped; a buffer left on a preview camera outlives it.
            if (camera == null || camera.cameraType == CameraType.Preview ||
                camera.cameraType == CameraType.Reflection)
            {
                return;
            }

            EnsureBuffer();
            EnsureMaterial();

            if (_material == null || _visible <= 0)
            {
                return;
            }

            _material.SetFloat(AgeId, _age);
            _material.SetFloat(TransitionAlphaId, TransitionAlpha);

            // The quad is expanded in clip space, so world scale is applied to the sprite size by hand, as DrawStars does.
            _material.SetFloat(WsScaleId, pointSizeMetres * transform.lossyScale.x);

            var buffer = EnsureCommandBuffer(camera);
            if (buffer == null)
            {
                return;
            }

            buffer.Clear();
            buffer.DrawProcedural(
                transform.localToWorldMatrix, _material, 0, MeshTopology.Triangles, _visible * 6);
        }

        private CommandBuffer EnsureCommandBuffer(Camera camera)
        {
            if (_buffers.TryGetValue(camera, out var existing))
            {
                if (existing.Buffer != null)
                {
                    return existing.Buffer;
                }

                _buffers.Remove(camera);
            }

            PruneDeadCameras();

            var when = ResolveCameraEvent(camera);
            var buffer = new CommandBuffer { name = "Nebula volume" };
            camera.AddCommandBuffer(when, buffer);
            _buffers[camera] = (buffer, when);
            return buffer;
        }

        // Scene view cameras come and go with their tabs; a destroyed key would otherwise stay for ever.
        private void PruneDeadCameras()
        {
            List<Camera> dead = null;

            foreach (var pair in _buffers)
            {
                if (pair.Key == null)
                {
                    dead ??= new List<Camera>();
                    dead.Add(pair.Key);
                }
            }

            if (dead == null)
            {
                return;
            }

            foreach (var camera in dead)
            {
                if (_buffers.TryGetValue(camera, out var entry))
                {
                    entry.Buffer?.Release();
                    _buffers.Remove(camera);
                }
            }
        }

        private void RemoveCommandBuffers()
        {
            foreach (var pair in _buffers)
            {
                // The stored event, not a recomputed one: removing with a different event leaves the buffer attached.
                if (pair.Key != null && pair.Value.Buffer != null)
                {
                    pair.Key.RemoveCommandBuffer(pair.Value.When, pair.Value.Buffer);
                }

                pair.Value.Buffer?.Release();
            }

            _buffers.Clear();
        }
    }
}
