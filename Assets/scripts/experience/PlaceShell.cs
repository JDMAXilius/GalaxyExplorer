// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Puts the player <i>inside</i> a destination instead of in front of one.
    ///
    /// <para><b>What it is.</b> Two inward-facing spheres, both drawn with <c>CosmicSimulation/PlaceShell</c>:
    /// <list type="bullet">
    /// <item>a <b>sky</b> sphere of about 60 m whose centre is glued to the player's head. Because it follows
    /// them it has no parallax and reads as the far distance. It is opaque, and it carries the destination's own
    /// plate as a wide dome-shaped patch plus a procedural star field.</item>
    /// <item>a <b>gas</b> sphere of about 7 m pinned to a point in the room. Transparent, its alpha taken from
    /// one luminance band of the same plate at a different zoom, turning slowly about a tilted axis.</item>
    /// </list>
    /// The gas sphere is the whole idea. A head-locked sphere is a skybox: lean, walk, and nothing changes, so it
    /// stays a painted ball however good the painting is. A second sheet of nebulosity that stays where the room
    /// put it slides against the sky whenever the player moves their head, and that parallax is what says
    /// "volume". Everything else here exists to make that one cue affordable and stable.</para>
    ///
    /// <para><b>Where the depth in the image comes from.</b> The plates are photographs -- three channels, no
    /// alpha, black sky around the subject. <see cref="NebulaOverlay"/>'s cards already solve that by taking a
    /// luminance band per layer, and the shell copies it exactly: the sky takes the whole photograph
    /// (<c>_BandWeight</c> 0) because it is the backdrop, and the gas takes a band only (<c>_BandWeight</c> 1) so
    /// the photograph's black sky is never drawn and the layer is a torn sheet rather than a second solid ball.
    /// PlaceShellBuilder writes those numbers; nothing here needs retuning when a plate changes.</para>
    ///
    /// <para><b>How it coexists with the nebula card overlay.</b> It does not replace it, and the two never
    /// touch. The cards are the <i>object</i>: 70 cm across, grabbable, scalable, held at arm's length, drawn at
    /// queue 3000. The shell is the <i>place</i>: not grabbable, no collider, drawn at 1900 and 2900, behind
    /// everything the player can reach. Opening Orion gives you a nebula you can pick up, standing in a sky made
    /// of the same photograph. The one thing that would have made them fight is the room dimming: a destination
    /// asking for <see cref="EnvironmentMode.BlackHalo"/> puts a 50% black quad at render queue 3900, in front of
    /// the shell, and a soft black disc behind the cards -- both of which would be painted over the sky. So a
    /// shell asks for <see cref="EnvironmentMode.FullBlack"/> on <see cref="Start"/>, which turns the dim quad
    /// off, hides the halo, and takes the headset out of passthrough; <c>ExperienceDirector.ClearDestinations</c>
    /// already restores the experience's own mode when the destination closes, so nothing here has to undo it.
    /// Set <see cref="forceFullBlack"/> false and the module's own <c>Environment</c> is left alone.</para>
    ///
    /// <para><b>Lifetime.</b> The shell is authored as a child of a destination's <c>ContentPrefab</c>, which is
    /// the only hook <c>ExperienceDirector.OpenDestination</c> offers, but it must not behave like one: that root
    /// is billboarded by <see cref="NebulaOverlay"/>, grown in from zero scale, and then moved and scaled by the
    /// player's hands. A sphere around the head inheriting any of that would be a disaster. So on
    /// <see cref="Start"/> it detaches to the scene root and holds its own world transform from then on, and it
    /// watches the transform it came from: when that is destroyed -- the destination closed, or the player moved
    /// somewhere else -- the shell fades out and destroys itself. Dropped into a scene by hand with no parent, it
    /// simply stays.</para>
    ///
    /// <para><b>What it costs, and why it is only two layers.</b> A sphere seen from inside is full-screen
    /// overdraw by definition, and a Quest 3 is fill-bound, not triangle-bound. The geometry is free: the sky is
    /// 64x32 segments (8 192 triangles) and the gas 48x24 (4 608), about 12 800 triangles total, which against
    /// the 400 000-point budget in Technical Overview 7.4 is roughly 1.6% -- a third of what Andromeda spends.
    /// <b>Two draw calls.</b> The cost that matters is fill: about <b>2.0x full-screen overdraw</b> added to the
    /// frame, one layer for the sky and up to one for the gas, on top of the four nebula cards (which are small
    /// on screen) and the dim quad (which the shell switches off, so that one is given back). That is the
    /// affordable version, and it is affordable because of what was left out: one pass, one texture fetch per
    /// fragment, no camera depth texture, no grab pass, no second sky, no per-pixel trigonometry -- the
    /// equirectangular mapping is baked into the mesh UVs -- and <c>clip()</c> on the gas so the large majority
    /// of its fragments, the ones the luminance band rejects, are discarded before the blend instead of being
    /// blended at alpha zero. If it still does not hold frame rate on the headset, the honest next step is to
    /// drop the gas sphere (<see cref="gasMaterial"/> empty) and ship the sky alone: one layer, a skybox with a
    /// real plate on it, still a large improvement on a black room, and half the fill. Do not reach for a third
    /// layer.</para>
    ///
    /// <para><b>Desktop.</b> There is nothing to interact with -- no collider, no <c>GEInteractable</c>, nothing
    /// on an interaction layer -- so mouse and keyboard need no equivalent and neither pointer can be blocked by
    /// it. It looks the same in both modes.</para>
    ///
    /// <para>Runs at 70: after <see cref="NebulaOverlay"/> (55) has billboarded the root we detached from, and
    /// after <see cref="BlackHalo"/> (60), so nothing moves under us after we have placed ourselves.</para>
    /// </summary>
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

            // Detached without preserving the world transform on purpose. The parent is a destination root that
            // ExperienceDirector grows in from localScale zero, so "keep my world transform" could bake a zero
            // scale into us depending on which frame we land on. We want our own world frame regardless: identity
            // at the origin, with the two spheres placed in it explicitly.
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
                    // After OpenDestination's own Set, which ran during the frame that instantiated us. Turning
                    // the room black is what stops the dim quad at queue 3900 and the black halo from being
                    // painted over the sky; ClearDestinations puts the experience's own mode back on close.
                    EnvironmentController.Instance.Set(EnvironmentMode.FullBlack);
                }

                OnRoomModeChanged(EnvironmentController.Instance.EffectiveMode);
            }

            ApplyFade(0f);
        }

        private void OnDestroy()
        {
            EnvironmentController.ModeChanged -= OnRoomModeChanged;

            // The meshes are generated, not assets, so nothing else can be holding them.
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

            // The far clip plane belongs to the camera rig, not to us, and a sky outside it is silently culled
            // and looks exactly like a shell that failed to build. Checked every frame because the rig can swap
            // cameras between desktop and headset.
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
                // A deadzone rather than a follow: inside it the sphere does not move at all, which is the whole
                // source of the parallax. Only once the player has walked away from it does it drift after them,
                // slowly enough that it never reads as the world moving.
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

            // Yaw only. The plate patch sits at the sky sphere's local +Z, so turning the sphere to the player's
            // heading puts the nebula in front of them as the destination opens; taking the pitch and roll as
            // well would tip the horizon with whatever way their head happened to be leaning.
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

        /// <summary>
        /// Through a property block, never through the material. These materials are project assets shared by
        /// every instance of a destination, and a play session that wrote <c>_Fade</c> straight onto them would
        /// leave a runtime value in the next diff.
        /// </summary>
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

        /// <summary>
        /// The dock's passthrough button beats us. A shell is opaque, so leaving it up while the player has
        /// asked to see their room would make the button do nothing at all.
        /// </summary>
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
        /// A unit sphere with true equirectangular UVs -- u is azimuth with 0.5 at local +Z, v is elevation with
        /// 0.5 at the horizon -- and a duplicated seam column, so the shader can place the plate with a scale and
        /// an offset instead of an <c>atan2</c> per pixel, and there is no seam where u wraps.
        ///
        /// <para>Wound outwards, like any ordinary sphere, and the shader culls front faces to leave the inside.
        /// That way the mesh is a plain sphere anyone could reuse rather than a special inside-out one.</para>
        ///
        /// <para>Generated rather than taken from <c>boundry_space_background_stars_model.fbx</c>: see the note
        /// in PlaceShellBuilder for why that model cannot carry a plate.</para>
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

            // No normals and no tangents: the shader lights nothing and reads neither, and a sphere this size
            // must never be frustum-culled by a bounds test taken from the player's position inside it.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
