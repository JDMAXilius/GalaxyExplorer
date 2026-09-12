// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Putting a hand inside the Sun. The surface brightens and a low rumble swells while the hand stays there,
    /// and both ease away when it leaves (GDD 4.1).
    ///
    /// The test is a sphere: a palm closer to the centre than the surface radius is inside. The radius comes from
    /// the renderer's world bounds by default, so it follows the player scaling the Sun up to fill the room.
    ///
    /// Brightness is written through a <see cref="MaterialPropertyBlock"/>. The Sun's material is shared with
    /// whatever else references it, and a per-frame instance would both leak into the asset and cost a draw call
    /// per body; the block writes per-renderer without touching the material. The property name is serialized
    /// because the Sun's shader is still changing — point it at whatever that shader exposes, and if it exposes
    /// nothing by that name this component stays silent and just plays the rumble.
    ///
    /// On desktop there is no hand to put anywhere, so the mouse hovering the Sun stands in for one. That arrives
    /// through the ordinary focus routing, which means this belongs on the same GameObject as the Sun's
    /// <see cref="ForceSolver"/> — the object <c>GEInputEvents.ExecuteHierarchy</c> already stops at.
    /// </summary>
    [DisallowMultipleComponent]
    public class SunTouchResponse : MonoBehaviour, IGEFocusHandler
    {
        [Header("Surface")]
        [SerializeField]
        [Tooltip("Centre of the Sun. Defaults to this transform.")]
        private Transform centre;

        [SerializeField]
        [Tooltip("Renderers that brighten. Empty takes every renderer under this object; the first also gives the touch radius.")]
        private Renderer[] surfaces = new Renderer[0];

        [SerializeField]
        [Tooltip("Touch radius in metres at scale 1, multiplied by the Sun's current scale. Zero reads it from the renderer bounds instead.")]
        private float radiusMetres = 0f;

        [Header("Brightness")]
        [SerializeField]
        [Tooltip("Float property on the Sun's shader that this drives. Absent from the shader means no brightening.")]
        private string brightnessProperty = "_TouchBrightness";

        [SerializeField]
        [Tooltip("Value written while nothing is touching.")]
        private float restValue = 0f;

        [SerializeField]
        [Tooltip("Value written while a hand is fully inside.")]
        private float touchedValue = 1f;

        [Header("Rumble")]
        [SerializeField]
        [Tooltip("Looping rumble. Leave empty and set a clip below to have one built here.")]
        private AudioSource rumble;

        [SerializeField]
        [Tooltip("Used only when no source is assigned above.")]
        private AudioClip rumbleClip;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Volume the rumble swells to.")]
        private float rumbleVolume = 0.6f;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds for the response to swell once a hand is inside.")]
        private float riseSeconds = 0.6f;

        [SerializeField]
        [Tooltip("Seconds for it to ease away once the hand leaves. Slower than the rise, so it reads as a fade rather than a switch.")]
        private float fallSeconds = 0.9f;

        [SerializeField]
        [Tooltip("Desktop: the mouse hovering the Sun counts as a hand inside it.")]
        private bool mouseCountsOnDesktop = true;

        private bool _ready;
        private bool _hasProperty;
        private int _propertyId;
        private MaterialPropertyBlock _block;
        private Transform _centre;
        private Renderer _radiusSource;
        private AudioSource _rumble;
        private bool _pointerInside;
        private float _level;    // 0 untouched, 1 fully touched
        private float _applied = -1f;

        /// <summary>How far into the response we are, 0 to 1. Eased before it reaches the material.</summary>
        public float TouchLevel => _level;

        public bool IsTouched => _level > 0.001f;

        private void Awake() => EnsureInit();

        // The Sun is spawned and its response driven in the same frame when an experience opens, and a caller has
        // no way to know whether Awake has run, so every entry point comes through here rather than trusting it.
        private void EnsureInit()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _block = new MaterialPropertyBlock();
            _centre = centre != null ? centre : transform;

            if (surfaces == null || surfaces.Length == 0)
            {
                surfaces = GetComponentsInChildren<Renderer>(true);
            }

            foreach (var surface in surfaces)
            {
                if (surface != null)
                {
                    _radiusSource = surface;
                    break;
                }
            }

            ResolveProperty();
            ResolveRumble();
        }

        private void ResolveProperty()
        {
            _hasProperty = false;
            if (string.IsNullOrEmpty(brightnessProperty))
            {
                return;
            }

            _propertyId = Shader.PropertyToID(brightnessProperty);
            foreach (var surface in surfaces)
            {
                var material = surface != null ? surface.sharedMaterial : null;
                if (material != null && material.HasProperty(_propertyId))
                {
                    _hasProperty = true;
                    return;
                }
            }
        }

        private void ResolveRumble()
        {
            _rumble = rumble;
            if (_rumble == null && rumbleClip != null)
            {
                _rumble = gameObject.AddComponent<AudioSource>();
                _rumble.clip = rumbleClip;
                _rumble.playOnAwake = false;
                _rumble.spatialBlend = 1f;
                _rumble.dopplerLevel = 0f;
                _rumble.rolloffMode = AudioRolloffMode.Linear;
                _rumble.minDistance = 0.2f;
                _rumble.maxDistance = 8f;
            }

            if (_rumble != null)
            {
                // The rumble has to hold for as long as the hand does, so it loops whatever the source was set to.
                _rumble.loop = true;
                _rumble.volume = 0f;
            }
        }

        /// <summary>Points the brightening at a different shader property, e.g. after the Sun's shader changes.</summary>
        public void SetBrightnessProperty(string propertyName)
        {
            EnsureInit();
            brightnessProperty = propertyName;
            ResolveProperty();
        }

        /// <summary>Desktop and tests: treat a pointer as being inside the Sun.</summary>
        public void SetPointerInside(bool inside)
        {
            EnsureInit();
            _pointerInside = inside;
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            // A Sun switched away from mid-touch must not be left bright and rumbling when it comes back.
            _pointerInside = false;
            _level = 0f;
            Apply();
        }

        private void Update()
        {
            EnsureInit();

            var inside = IsAnythingInside();
            var goal = inside ? 1f : 0f;
            var seconds = inside ? riseSeconds : fallSeconds;
            _level = seconds <= 0f ? goal : Mathf.MoveTowards(_level, goal, Time.deltaTime / seconds);

            if (!Mathf.Approximately(_level, _applied))
            {
                Apply();
            }
        }

        private bool IsAnythingInside()
        {
            var rig = XRInputRig.Instance;
            if (rig != null)
            {
                if (rig.LeftHandTracked && rig.LeftPalm != null && IsInside(rig.LeftPalm.position))
                {
                    return true;
                }

                if (rig.RightHandTracked && rig.RightPalm != null && IsInside(rig.RightPalm.position))
                {
                    return true;
                }

                // With controllers the controller is the hand: reaching into the Sun should still do something.
                if (rig.LeftControllerTracked && rig.LeftControllerTransform != null && IsInside(rig.LeftControllerTransform.position))
                {
                    return true;
                }

                if (rig.RightControllerTracked && rig.RightControllerTransform != null && IsInside(rig.RightControllerTransform.position))
                {
                    return true;
                }

                if (rig.LeftHandTracked || rig.RightHandTracked)
                {
                    // Hands are up and neither is inside. The mouse must not answer for them.
                    return false;
                }
            }

            return mouseCountsOnDesktop && _pointerInside;
        }

        private bool IsInside(Vector3 worldPoint)
        {
            var radius = CurrentRadius();
            return radius > 0f && (worldPoint - _centre.position).sqrMagnitude <= radius * radius;
        }

        private float CurrentRadius()
        {
            if (radiusMetres > 0f)
            {
                return radiusMetres * Mathf.Abs(_centre.lossyScale.x);
            }

            if (_radiusSource == null)
            {
                return 0f;
            }

            // World bounds, so scaling the Sun scales what counts as inside it.
            var extents = _radiusSource.bounds.extents;
            return Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
        }

        private void Apply()
        {
            _applied = _level;
            var eased = Mathf.SmoothStep(0f, 1f, _level);

            if (_hasProperty)
            {
                var value = Mathf.Lerp(restValue, touchedValue, eased);
                foreach (var surface in surfaces)
                {
                    if (surface == null)
                    {
                        continue;
                    }

                    // Read the block back first: another system may already be writing its own properties here.
                    surface.GetPropertyBlock(_block);
                    _block.SetFloat(_propertyId, value);
                    surface.SetPropertyBlock(_block);
                }
            }

            if (_rumble == null)
            {
                return;
            }

            _rumble.volume = rumbleVolume * eased;
            if (eased > 0.001f)
            {
                if (!_rumble.isPlaying && _rumble.clip != null && _rumble.gameObject.activeInHierarchy)
                {
                    _rumble.Play();
                }
            }
            else if (_rumble.isPlaying)
            {
                _rumble.Stop();
            }
        }

        // ---------- desktop pointer

        public void OnFocusEnter(GEFocusEventData eventData)
        {
            // Only the mouse. A hand ray hovering the Sun from across the room is pointing at it, not touching it.
            if (eventData != null && eventData.Pointer != null && eventData.Pointer.IsMouse)
            {
                SetPointerInside(true);
            }
        }

        public void OnFocusExit(GEFocusEventData eventData)
        {
            if (eventData != null && eventData.Pointer != null && eventData.Pointer.IsMouse)
            {
                SetPointerInside(false);
            }
        }
    }
}
