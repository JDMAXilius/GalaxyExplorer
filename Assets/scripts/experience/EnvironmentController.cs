// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Decides how much of the player's room is visible, and fades between those states.
    ///
    /// Passthrough and full black are the headset's own modes, handled by <see cref="ExperienceModeManager"/>.
    /// Dimming is ours: a black quad parented to the camera, just past the near plane, drawn after the content and
    /// before world-space UI. A halo is the same idea in one spot, a soft black disc that sits behind a faint
    /// object so it reads against a bright room.
    ///
    /// The dock's passthrough button overrides whatever the current experience asked for, until the player
    /// changes it back or moves somewhere else.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EnvironmentController : MonoBehaviour
    {
        private const float DefaultFade = 0.4f;
        private const string TintShaderName = "CosmicSimulation/EnvironmentTint";

        [SerializeField]
        [Tooltip("How dark the room goes in Dimmed mode.")]
        [Range(0f, 1f)]
        private float dimOpacity = 0.5f;

        [SerializeField]
        [Tooltip("Metres in front of the camera; must clear the near plane.")]
        private float dimDistance = 0.06f;

        [SerializeField]
        [Tooltip("Radial black gradient used behind a faint object. Generated when left empty.")]
        private Texture2D haloTexture;

        [SerializeField]
        [Tooltip("Flat tinted transparent shader. Looked up by name when left empty.")]
        private Shader tintShader;

        private Camera _camera;
        private Renderer _dimQuad;
        private Material _dimMaterial;
        private Material _haloMaterial;
        private float _dimCurrent;
        private float _dimTarget;
        private float _dimSpeed = 1f / DefaultFade;
        private readonly List<BlackHalo> _halos = new List<BlackHalo>();
        private EnvironmentMode _broadcast = EnvironmentMode.Passthrough;

        public static EnvironmentController Instance { get; private set; }

        /// <summary>What the open experience asked for, before any override by the player.</summary>
        public EnvironmentMode Mode { get; private set; } = EnvironmentMode.Passthrough;

        /// <summary>True while the player has forced the room visible from the dock.</summary>
        public bool PassthroughForced { get; private set; }

        /// <summary>The state the room is actually in: what the experience asked for, unless the player overrode it.</summary>
        public EnvironmentMode EffectiveMode => PassthroughForced ? EnvironmentMode.Passthrough : Mode;

        /// <summary>
        /// Raised with <see cref="EffectiveMode"/> when it changes, and only then — <see cref="Set"/> is called on
        /// every experience switch, including switches that ask for the state the room is already in, and a
        /// subscriber that re-reacted to those would restart whatever it is driving. Nothing is raised for the
        /// opening state, since there is no change: late subscribers read <see cref="EffectiveMode"/> instead.
        /// </summary>
        public static event Action<EnvironmentMode> ModeChanged;

        // Play mode can start without a domain reload, which would otherwise leave last session's dead
        // subscribers on a static event.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ModeChanged = null;
        }

        private void Awake()
        {
            Instance = this;
            _camera = Camera.main;
            BuildDimQuad();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_dimMaterial != null) Destroy(_dimMaterial);
            if (_haloMaterial != null) Destroy(_haloMaterial);
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null && _dimQuad != null)
                {
                    AttachDimQuad();
                }
            }

            if (!Mathf.Approximately(_dimCurrent, _dimTarget))
            {
                _dimCurrent = Mathf.MoveTowards(_dimCurrent, _dimTarget, _dimSpeed * Time.deltaTime);
                ApplyDim();
            }
        }

        /// <summary>Puts the room into the state an experience asks for.</summary>
        public void Set(EnvironmentMode mode, float fadeSeconds = DefaultFade)
        {
            Mode = mode;
            Apply(fadeSeconds);
        }

        /// <summary>The dock's passthrough button: show the room whatever the experience wanted, or stop doing so.</summary>
        public void SetPassthroughForced(bool forced, float fadeSeconds = DefaultFade)
        {
            PassthroughForced = forced;
            Apply(fadeSeconds);
        }

        public void TogglePassthrough() => SetPassthroughForced(!PassthroughForced);

        private void Apply(float fadeSeconds)
        {
            var effective = EffectiveMode;

            // Full black is the headset's VR mode; everything else keeps passthrough on underneath.
            var wantsVR = effective == EnvironmentMode.FullBlack;
            if (ExperienceModeManager.Instance != null)
            {
                ExperienceModeManager.Instance.SetMode(wantsVR
                    ? ExperienceModeManager.Mode.VR
                    : ExperienceModeManager.Mode.Passthrough);
            }

            _dimTarget = effective == EnvironmentMode.Dimmed || effective == EnvironmentMode.BlackHalo
                ? dimOpacity
                : 0f;
            _dimSpeed = 1f / Mathf.Max(0.01f, fadeSeconds);

            foreach (var halo in _halos)
            {
                if (halo != null)
                {
                    halo.SetVisible(effective == EnvironmentMode.BlackHalo);
                }
            }

            // Last, so anything listening sees a room that has already been told what to do.
            if (effective != _broadcast)
            {
                _broadcast = effective;
                ModeChanged?.Invoke(effective);
            }
        }

        // ---------- the dim quad

        /// <summary>
        /// The flat tinted shader both the dim quad and the halo use. Serialized where possible so the build
        /// keeps it; the name lookup only covers components added at runtime, and it is listed under Always
        /// Included Shaders so that path survives a player build too.
        /// </summary>
        private Shader TintShader()
        {
            if (tintShader == null)
            {
                tintShader = Shader.Find(TintShaderName);
            }

            if (tintShader == null)
            {
                Debug.LogError($"EnvironmentController: shader '{TintShaderName}' is missing; dimming and halos are off.");
            }

            return tintShader;
        }

        private void BuildDimQuad()
        {
            var shader = TintShader();
            if (shader == null)
            {
                return;
            }

            _dimMaterial = new Material(shader) { name = "room_dim_material" };
            _dimMaterial.color = new Color(0f, 0f, 0f, 0f);
            // After the content, before world-space UI (4000), so panels and the dock stay legible.
            _dimMaterial.renderQueue = 3900;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "room_dim_quad";
            Destroy(quad.GetComponent<Collider>());
            _dimQuad = quad.GetComponent<Renderer>();
            _dimQuad.sharedMaterial = _dimMaterial;
            _dimQuad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _dimQuad.receiveShadows = false;
            quad.SetActive(false);

            AttachDimQuad();
        }

        private void AttachDimQuad()
        {
            if (_camera == null || _dimQuad == null)
            {
                return;
            }

            var t = _dimQuad.transform;
            t.SetParent(_camera.transform, false);
            t.localPosition = new Vector3(0f, 0f, dimDistance);
            t.localRotation = Quaternion.identity;

            // Cover the full field of view at that distance, with margin for the eye buffer's wider frustum.
            var height = 2f * dimDistance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var width = height * Mathf.Max(_camera.aspect, 1.6f);
            t.localScale = new Vector3(width * 2.2f, height * 2.2f, 1f);
        }

        private void ApplyDim()
        {
            if (_dimQuad == null)
            {
                return;
            }

            _dimQuad.gameObject.SetActive(_dimCurrent > 0.001f);
            _dimMaterial.color = new Color(0f, 0f, 0f, _dimCurrent);
        }

        // ---------- halos

        /// <summary>A soft black disc that follows a faint object so it reads against the room.</summary>
        public BlackHalo SpawnHalo(Transform target, float diameter)
        {
            if (HaloMaterial() == null)
            {
                return null;
            }

            var go = new GameObject("black_halo");
            var halo = go.AddComponent<BlackHalo>();
            halo.Initialise(target, diameter, HaloMaterial());
            _halos.Add(halo);
            halo.SetVisible(!PassthroughForced && Mode == EnvironmentMode.BlackHalo);
            return halo;
        }

        public void Forget(BlackHalo halo) => _halos.Remove(halo);

        private Material HaloMaterial()
        {
            if (_haloMaterial != null)
            {
                return _haloMaterial;
            }

            var shader = TintShader();
            if (shader == null)
            {
                return null;
            }

            if (haloTexture == null)
            {
                haloTexture = BuildHaloTexture();
            }

            _haloMaterial = new Material(shader) { name = "black_halo_material", mainTexture = haloTexture };
            _haloMaterial.color = Color.white;
            _haloMaterial.renderQueue = 2990; // behind the object it backs, in front of the room
            return _haloMaterial;
        }

        private static Texture2D BuildHaloTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "black_halo_generated" };
            var centre = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                    // Opaque core, fading out well before the edge so there is no visible rim.
                    var a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1f, d));
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 245f));
                }
            }

            tex.SetPixels32(pixels);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }
    }
}
