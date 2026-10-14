// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Puts the player inside a destination: a head-locked opaque sky sphere plus a room-fixed transparent gas
    /// sphere whose parallax against the sky is what reads as volume. Not interactable, so no desktop equivalent.
    /// </summary>
    // Two layers only: Quest 3 is fill-bound and each inside-out sphere is ~1x full-screen overdraw. If frame
    // rate fails, drop the gas layer (gasMaterial empty), never add a third.
    // Order 70: after NebulaOverlay (55) and BlackHalo (60), so nothing moves the root after we place ourselves.
    [DefaultExecutionOrder(70)]
    public class PlaceShell : MonoBehaviour
    {
        private const string FadeProperty = "_Fade";

        [Header("Layers")]
        [SerializeField]
        [Tooltip("Material for the big head-following sphere. Written by PlaceShellBuilder. Required.")]
        private Material skyMaterial;

        [SerializeField]
        [Tooltip("Material for the small room-fixed nebulosity sphere. Leave empty to ship the sky alone, " +
                 "which halves the fill cost.")]
        private Material gasMaterial;

        [Header("Geometry")]
        [SerializeField]
        [Tooltip("Metres. Must stay well inside the camera's far clip plane or the sky is culled away entirely; " +
                 "it is clamped at runtime if it is not.")]
        private float skyRadius = 60f;

        [SerializeField]
        [Tooltip("Metres. Small enough that walking a step gives real parallax against the sky, large enough " +
                 "that the player never reaches its wall.")]
        private float gasRadius = 7f;

        [SerializeField]
        [Tooltip("Longitude and latitude segments of the sky sphere.")]
        private Vector2Int skySegments = new Vector2Int(64, 32);

        [SerializeField]
        [Tooltip("Longitude and latitude segments of the gas sphere.")]
        private Vector2Int gasSegments = new Vector2Int(48, 24);

        [Header("Motion")]
        [SerializeField]
        [Tooltip("Degrees per second the gas sphere turns. Slow enough to read as drift, not as rotation.")]
        private float gasSpinDegreesPerSecond = 0.35f;

        [SerializeField]
        [Tooltip("Degrees the gas sphere's spin axis is tilted off vertical, so the drift is not a carousel.")]
        private float gasTiltDegrees = 24f;

        [SerializeField]
        [Tooltip("Metres the player can move away from the gas sphere's centre before it starts following them. " +
                 "Inside this radius it does not move at all, which is where the parallax comes from.")]
        private float gasFollowDeadzone = 2f;

        [SerializeField]
        [Tooltip("Metres per second the gas sphere drifts after the player once they are past the deadzone.")]
        private float gasFollowSpeed = 0.6f;

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Seconds the shell takes to appear. A surrounding sky fades in; it never grows in from a point.")]
        private float fadeInSeconds = 1.2f;

        [SerializeField]
        [Tooltip("Seconds the shell takes to leave once the destination it belongs to has closed.")]
        private float fadeOutSeconds = 0.35f;

        [SerializeField]
        [Tooltip("Turn the sky so the plate's patch is centred on wherever the player was looking when the " +
                 "destination opened. Off leaves the prefab's own heading.")]
        private bool faceInitialGaze = true;

        [SerializeField]
        [Tooltip("Ask the room for FullBlack while this shell is up. Off leaves the module's own Environment " +
                 "alone, which means a Dimmed or BlackHalo destination paints a 50% black quad over the sky.")]
        private bool forceFullBlack = true;

        [SerializeField]
        [Tooltip("Hide the shell while the player has forced passthrough on from the dock, so the button still " +
                 "does what it says.")]
        private bool hideInPassthrough = true;

        [SerializeField]
        [Tooltip("Layer for the two spheres. Keep it off POI and ForceGrab: pointers look for those.")]
        private int rendererLayer;

        private Camera _camera;
        private Transform _owner;
        private bool _hadOwner;

        private Transform _sky;
        private Transform _gas;
        private Renderer _skyRenderer;
        private Renderer _gasRenderer;
        private Mesh _skyMesh;
        private Mesh _gasMesh;
        private MaterialPropertyBlock _block;

        private Quaternion _skyRotation = Quaternion.identity;
        private Vector3 _gasCentre;
        private Vector3 _gasAxis = Vector3.up;
        private float _gasAngle;
        private bool _placed;

        private float _fade;
        private bool _leaving;
        private bool _roomHidden;

        /// <summary>How much of the shell is currently on screen, 0 to 1.</summary>
        public float Fade => _fade;

        /// <summary>True once the shell has been given a heading and put itself in the world.</summary>
        public bool IsPlaced => _placed;

        private void Start()
        {
            if (skyMaterial == null)
            {
                Debug.LogError($"PlaceShell on '{name}' has no sky material, so there is nothing to show. " +
                               "Run Cosmic Simulation > Attach Place Shells.", this);
                enabled = false;
                return;
            }

            // Detach without keeping world transform: the destination root grows in from scale zero, which could
            // bake a zero scale into us. _owner is watched so the shell fades out when the destination closes.
            _owner = transform.parent;
            _hadOwner = _owner != null;
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            _block = new MaterialPropertyBlock();
            _camera = Camera.main;

            _skyMesh = BuildSphere(skySegments, "place_shell_sky_mesh");
            _sky = BuildLayer("place_shell_sky", _skyMesh, skyMaterial, out _skyRenderer);

            if (gasMaterial != null)
            {
                _gasMesh = BuildSphere(gasSegments, "place_shell_gas_mesh");
                _gas = BuildLayer("place_shell_gas", _gasMesh, gasMaterial, out _gasRenderer);
            }

            _gasAxis = Quaternion.Euler(gasTiltDegrees, 37f, 0f) * Vector3.up;

            EnvironmentController.ModeChanged += OnRoomModeChanged;

            if (EnvironmentController.Instance != null)
            {
                if (forceFullBlack)
                {
                    // Overrides OpenDestination's Set so the dim quad (queue 3900) and halo are not drawn over the
                    // sky; ClearDestinations restores the experience's mode on close.
                    EnvironmentController.Instance.Set(EnvironmentMode.FullBlack);
                }

                OnRoomModeChanged(EnvironmentController.Instance.EffectiveMode);
            }

            ApplyFade(0f);
        }

        private void OnDestroy()
        {
            EnvironmentController.ModeChanged -= OnRoomModeChanged;

            if (_skyMesh != null)
            {
                Destroy(_skyMesh);
            }

            if (_gasMesh != null)
            {
                Destroy(_gasMesh);
            }
        }

        private void LateUpdate()
        {
            if (_sky == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var eye = _camera.transform.position;

            if (!_placed)
            {
                Place(eye);
            }

            // A sky beyond the far clip plane is silently culled; checked per frame because the rig swaps cameras.
            var radius = skyRadius;
            var limit = _camera.farClipPlane * 0.6f;
            if (radius > limit)
            {
                radius = limit;
            }

            _sky.SetPositionAndRotation(eye, _skyRotation);
            _sky.localScale = Vector3.one * radius;

            if (_gas != null)
            {
                // Deadzone, not a follow: the sphere standing still inside it is the source of the parallax.
                var offset = eye - _gasCentre;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > gasFollowDeadzone)
                {
                    var target = _gasCentre + offset.normalized * (distance - gasFollowDeadzone);
                    _gasCentre = Vector3.MoveTowards(_gasCentre, target, gasFollowSpeed * Time.deltaTime);
                }

                _gasAngle += gasSpinDegreesPerSecond * Time.deltaTime;
                _gas.SetPositionAndRotation(_gasCentre, Quaternion.AngleAxis(_gasAngle, _gasAxis) * _skyRotation);
                _gas.localScale = Vector3.one * gasRadius;
            }

            Breathe();
        }

        /// <summary>Gives the shell its heading and its gas centre, once the camera is actually tracking.</summary>
        private void Place(Vector3 eye)
        {
            _placed = true;
            _gasCentre = eye;

            if (!faceInitialGaze)
            {
                return;
            }

            // Yaw only: the plate patch is at local +Z, and taking pitch/roll would tip the horizon with the head.
            var forward = _camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f)
            {
                _skyRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }
        }

        /// <summary>Fades in, fades out when the destination that owned us has gone, and follows the room mode.</summary>
        private void Breathe()
        {
            if (_hadOwner && _owner == null)
            {
                _leaving = true;
            }

            var target = _leaving || _roomHidden ? 0f : 1f;
            var seconds = target > _fade ? fadeInSeconds : fadeOutSeconds;
            var step = seconds > 0.001f ? Time.deltaTime / seconds : 1f;

            _fade = Mathf.MoveTowards(_fade, target, step);
            ApplyFade(_fade);

            if (_leaving && _fade <= 0f)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>Property block, not material: the materials are shared assets and would leak runtime values into diffs.</summary>
        private void ApplyFade(float value)
        {
            if (_block == null)
            {
                return;
            }

            _block.SetFloat(FadeProperty, value);

            if (_skyRenderer != null)
            {
                _skyRenderer.enabled = value > 0.001f;
                _skyRenderer.SetPropertyBlock(_block);
            }

            if (_gasRenderer != null)
            {
                _gasRenderer.enabled = value > 0.001f;
                _gasRenderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>The opaque shell hides in passthrough, or the dock's passthrough button would appear to do nothing.</summary>
        private void OnRoomModeChanged(EnvironmentMode mode)
        {
            _roomHidden = hideInPassthrough && mode == EnvironmentMode.Passthrough;
        }

        // ---------- geometry

        private Transform BuildLayer(string layerName, Mesh mesh, Material material, out Renderer renderer)
        {
            var go = new GameObject(layerName)
            {
                layer = rendererLayer,
            };

            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.allowOcclusionWhenDynamic = false;

            renderer = mr;
            return go.transform;
        }

        /// <summary>
        /// Unit sphere, equirectangular UVs (u = azimuth, 0.5 at +Z) with a duplicated seam column, so the shader
        /// needs no per-pixel atan2. Wound outwards; the shader culls front faces.
        /// </summary>
        private static Mesh BuildSphere(Vector2Int segments, string meshName)
        {
            var lon = Mathf.Clamp(segments.x, 8, 256);
            var lat = Mathf.Clamp(segments.y, 4, 128);

            var vertexCount = (lon + 1) * (lat + 1);
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (var y = 0; y <= lat; y++)
            {
                var v = (float)y / lat;
                var elevation = (v - 0.5f) * Mathf.PI;
                var ce = Mathf.Cos(elevation);
                var se = Mathf.Sin(elevation);

                for (var x = 0; x <= lon; x++)
                {
                    var u = (float)x / lon;
                    var azimuth = (u - 0.5f) * 2f * Mathf.PI;

                    var index = y * (lon + 1) + x;
                    vertices[index] = new Vector3(Mathf.Sin(azimuth) * ce, se, Mathf.Cos(azimuth) * ce);
                    uvs[index] = new Vector2(u, v);
                }
            }

            var triangles = new int[lon * lat * 6];
            var t = 0;
            for (var y = 0; y < lat; y++)
            {
                for (var x = 0; x < lon; x++)
                {
                    var i0 = y * (lon + 1) + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + lon + 1;
                    var i3 = i2 + 1;

                    // Unity's front face is cross(B - A, C - A); this order puts that along the outward radius.
                    triangles[t++] = i0;
                    triangles[t++] = i1;
                    triangles[t++] = i2;

                    triangles[t++] = i1;
                    triangles[t++] = i3;
                    triangles[t++] = i2;
                }
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave,
                indexFormat = vertexCount > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            // No normals or tangents: the shader is unlit.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
