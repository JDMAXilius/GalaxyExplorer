// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A body stays wherever the player leaves it (GDD 5.2), coming home only when asked, on an experience change,
    /// or after straying out of reach. Use instead of <c>PostManipulationResetter</c> on anything the player arranges.
    /// </summary>
    // Home is ForceSolver.RootTransform, read live so a moving layout stays authoritative.
    [RequireComponent(typeof(ForceSolver))]
    public class FreePlacementSolver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds for a body to travel home. The GDD asks for 0.8 s on a layout change.")]
        private float restoreSeconds = 0.8f;

        [SerializeField]
        [Tooltip("Metres from the player beyond which a body counts as lost.")]
        private float strayRadius = 4f;

        [SerializeField]
        [Tooltip("Height below which a body counts as dropped, in the rig's own space.")]
        private float floorHeight = -0.1f;

        [SerializeField]
        [Tooltip("Seconds a body must stay out of reach before it is brought back on its own.")]
        private float strayGrace = 5f;

        [SerializeField]
        [Tooltip("Bring a stray body home without being asked. Off for objects meant to be left far away.")]
        private bool autoRestoreStrays = true;

        private ForceSolver _force;
        private Camera _camera;
        private Coroutine _restore;
        private Vector3 _homeScale = Vector3.one;
        private float _strayTimer;

        /// <summary>True while the body is somewhere the player put it rather than in its layout position.</summary>
        public bool IsPlaced => _force != null &&
                                (_force.ForceState == ForceSolver.State.Free ||
                                 _force.ForceState == ForceSolver.State.Manipulation);

        /// <summary>True while travelling home.</summary>
        public bool IsRestoring => _restore != null;

        private void Awake() => EnsureInit();

        // A layout can restore a body the frame it spawns, before Awake, so entry points initialise lazily.
        private void EnsureInit()
        {
            if (_force != null)
            {
                return;
            }

            _force = GetComponent<ForceSolver>();
            _camera = Camera.main;
            _homeScale = transform.localScale;
        }

        private void OnDisable()
        {
            _restore = null;
            _strayTimer = 0f;
        }

        /// <summary>Overrides the scale a body returns to, for a <see cref="LayoutPreset"/> that sizes bodies itself.</summary>
        public void SetHomeScale(Vector3 scale) => _homeScale = scale;

        /// <summary>Brings the body home. Safe to call when it is already there.</summary>
        public void RestoreLayout() => RestoreLayout(restoreSeconds);

        public void RestoreLayout(float seconds)
        {
            EnsureInit();
            if (!isActiveAndEnabled || _force == null || _force.RootTransform == null)
            {
                return;
            }

            if (_restore != null)
            {
                StopCoroutine(_restore);
            }

            _strayTimer = 0f;
            _restore = StartCoroutine(RestoreRoutine(Mathf.Max(0.01f, seconds)));
        }

        /// <summary>Puts the body home with no animation, for a scene change nobody watches.</summary>
        public void RestoreImmediate()
        {
            if (_force == null)
            {
                return;
            }

            if (_restore != null)
            {
                StopCoroutine(_restore);
                _restore = null;
            }

            _strayTimer = 0f;
            _force.ResetToRoot();
        }

        private void Update()
        {
            if (!autoRestoreStrays || _restore != null || !IsPlaced)
            {
                _strayTimer = 0f;
                return;
            }

            if (_force.ForceState == ForceSolver.State.Manipulation || !OutOfReach())
            {
                _strayTimer = 0f;
                return;
            }

            _strayTimer += Time.deltaTime;
            if (_strayTimer >= strayGrace)
            {
                RestoreLayout();
            }
        }

        private bool OutOfReach()
        {
            if (transform.position.y < floorHeight)
            {
                return true;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return false;
                }
            }

            return Vector3.Distance(_camera.transform.position, transform.position) > strayRadius;
        }

        private IEnumerator RestoreRoutine(float seconds)
        {
            var root = _force.RootTransform;
            var fromPosition = transform.position;
            var fromRotation = transform.rotation;
            var fromScale = transform.localScale;

            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                if (root == null)
                {
                    break;
                }

                // Cubic ease-out: quick away, settling in. Nothing in this app bounces (GDD 8.7).
                var t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / seconds), 3f);
                transform.position = Vector3.Lerp(fromPosition, root.position, t);
                transform.rotation = Quaternion.Slerp(fromRotation, root.rotation, t);
                transform.localScale = Vector3.Lerp(fromScale, _homeScale, t);
                yield return null;
            }

            _restore = null;

            // Hand back to Root rather than setting the final pose: it also unwinds the moons, panel and highlighter.
            _force.ResetToRoot();
        }
    }
}
