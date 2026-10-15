// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Remembers where the spawner put non-body content (nebula overlays, Cosmic Web, Andromeda) so Restore can bring it back.
    /// </summary>
    // Not FreePlacementSolver: that one requires a ForceSolver (CS-107). Home is stored in the parent's frame
    // because the intro moves the ViewLoader; no stray auto-restore, these are meant to be left far away.
    [DisallowMultipleComponent]
    public class FreePlacementAnchor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds for a moved object to travel home. The GDD asks for 0.8 s, as FreePlacementSolver does.")]
        private float restoreSeconds = 0.8f;

        private ManipulationHandler _handler;
        private Coroutine _restore;
        private bool _resolved;
        private bool _captured;

        private Vector3 _homePosition;
        private Quaternion _homeRotation = Quaternion.identity;
        private Vector3 _homeScale = Vector3.one;

        /// <summary>True while travelling home.</summary>
        public bool IsRestoring => _restore != null;

        private void Awake()
        {
            Registry<FreePlacementAnchor>.Add(this);
            Resolve();
            EnsureCaptured();
        }

        private void OnDestroy() => Registry<FreePlacementAnchor>.Remove(this);

        private void OnDisable()
        {
            // Clearing `enabled` does not stop a running coroutine (only deactivation does), so stop it here.
            if (_restore != null)
            {
                StopCoroutine(_restore);
                _restore = null;
            }
        }

        // Must not assume Awake ran: CaptureHome(Transform) adds this component and uses it the same frame.
        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            // includeInactive: ownership must not depend on which parents are switched off right now.
            var solver = GetComponentInParent<ForceSolver>(true);
            if (solver != null)
            {
                Debug.LogError(
                    $"{name}: FreePlacementAnchor sits under the ForceSolver on {solver.name}, whose home is its " +
                    "own RootTransform and whose way back is FreePlacementSolver. Disabling it; remove it from " +
                    "the prefab.", this);
                enabled = false;
                return;
            }

            _handler = GetComponent<ManipulationHandler>();
        }

        /// <summary>
        /// Records the current local pose as home. Call once, before the grow-in scales from zero; a later
        /// capture would adopt a partial scale or wherever the player left it.
        /// </summary>
        public void CaptureHome()
        {
            Resolve();
            if (!enabled)
            {
                return;
            }

            _captured = true;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;
            _homeScale = transform.localScale;
        }

        private void EnsureCaptured()
        {
            if (!_captured)
            {
                CaptureHome();
            }
        }

        /// <summary>Brings the object home over <c>restoreSeconds</c>. Safe to call when it is already there.</summary>
        public void Restore() => Restore(restoreSeconds);

        public void Restore(float seconds)
        {
            Resolve();
            EnsureCaptured();

            // No capture means a ForceSolver owns this; the default home would fling it to the parent's origin.
            if (!_captured || !isActiveAndEnabled)
            {
                return;
            }

            if (_restore != null)
            {
                StopCoroutine(_restore);
            }

            _restore = StartCoroutine(RestoreRoutine(Mathf.Max(0.01f, seconds)));
        }

        /// <summary>Puts the object home with no animation, for a switch nobody watches.</summary>
        public void RestoreImmediate()
        {
            Resolve();
            EnsureCaptured();

            // As in Restore: never captured means no real home.
            if (!_captured)
            {
                return;
            }

            if (_restore != null)
            {
                StopCoroutine(_restore);
                _restore = null;
            }

            ApplyHome();
        }

        /// <summary>Brings home everything the player arranged that no solver and no rig owns (R / Recenter).</summary>
        public static void RestoreAll()
        {
            foreach (var anchor in Registry<FreePlacementAnchor>.Active)
            {
                anchor.Restore();
            }
        }

        /// <summary>The same, without the travel: an experience is being torn down and nobody will see it.</summary>
        public static void RestoreAllImmediate()
        {
            foreach (var anchor in Registry<FreePlacementAnchor>.Active)
            {
                anchor.RestoreImmediate();
            }
        }

        /// <summary>
        /// Adds and captures an anchor on every transform in fresh content that a <see cref="ManipulationHandler"/>
        /// moves and no <c>ForceSolver</c> owns; added when missing so content from un-rerun builders still comes home.
        /// </summary>
        public static void CaptureHome(Transform content)
        {
            if (content == null)
            {
                return;
            }

            // includeInactive: content may spawn with parts switched off, and they are still movable later.
            foreach (var handler in content.GetComponentsInChildren<ManipulationHandler>(true))
            {
                if (handler == null)
                {
                    continue;
                }

                // The handler need not move its own GameObject, and the anchor has to be on the one that moves.
                var host = handler.HostTransform;
                if (host == null || host.GetComponentInParent<ForceSolver>(true) != null)
                {
                    continue;
                }

                var anchor = host.GetComponent<FreePlacementAnchor>();
                if (anchor == null)
                {
                    anchor = host.gameObject.AddComponent<FreePlacementAnchor>();
                }

                anchor.CaptureHome();

                // The content root is the place two air pinches take hold of (CS-282, CS-286).
                if (host == content)
                {
                    handler.AirScaleClamp = anchor.ClampToHome;
                    ManipulationHandler.Place = handler;
                }
            }
        }

        /// <summary>
        /// Makes an open view scene (Milky Way, Solar System, Sagittarius A*) a place two air pinches move and
        /// scale, and R / Recenter bring home, as prefab places already are (CS-378). Pass the scene's content root,
        /// the object carrying <c>TransformHandler</c>. Call once its zoom-in is over: the zoom drives the same
        /// transform, and home captured mid-zoom would be a partial scale.
        /// </summary>
        // The handler goes on the root's first child: the root is re-pinned to its TransformSource every frame, and
        // the child is what ZoomInOut zooms and FitViewScene frames. No collider and no router, so no ray or poke
        // reaches it and nothing in the scene stops answering; only XRInputRig's air pinches drive it.
        // Headset only: on a monitor the wheel and drag on empty space move the camera, and a handler here would
        // also be the one DesktopMouseInput finds above every POI in the scene.
        public static void CaptureScenePlace(Transform sceneContent)
        {
            if (sceneContent == null || sceneContent.childCount == 0 || !UnityEngine.XR.XRSettings.isDeviceActive)
            {
                return;
            }

            var place = sceneContent.GetChild(0);
            if (place.GetComponentInParent<ForceSolver>(true) != null)
            {
                return;
            }

            if (place.GetComponent<ManipulationHandler>() == null)
            {
                var handler = place.gameObject.AddComponent<ManipulationHandler>();

                // Off until two air pinches switch it on for the gesture (BeginAirManipulation), as on the nebulae.
                handler.enabled = false;

                // No ForceSolver sounds this grab, as on the prefab places.
                handler.PlayGrabSounds = true;
                // Move and scale, never roll: a drifting hand must not tilt a galaxy's disc or an orbital plane.
                handler.TwoHandedType = ManipulationHandler.TwoHandedManipulation.MoveScale;
            }

            // Adds the anchor, captures home and makes this the open place, since the handler's host is the content.
            CaptureHome(place);
        }

        // The settings slider's range (UtilityWindow), measured from the size the place was spawned at.
        private const float MinPlaceScale = 0.5f, MaxPlaceScale = 2f;

        private Vector3 ClampToHome(Vector3 desiredLocalScale)
        {
            if (!_captured || Mathf.Abs(_homeScale.x) < 1e-6f || Mathf.Abs(desiredLocalScale.x) < 1e-6f)
            {
                return desiredLocalScale;
            }

            var factor = desiredLocalScale.x / _homeScale.x;
            return desiredLocalScale * (Mathf.Clamp(factor, MinPlaceScale, MaxPlaceScale) / factor);
        }

        private void ApplyHome()
        {
            transform.localPosition = _homePosition;
            transform.localRotation = _homeRotation;
            transform.localScale = _homeScale;
        }

        // No "already home" early-out: billboarded nebulae never match their captured rotation, and dropping
        // rotation from the test would leave a turned Andromeda crooked.
        private IEnumerator RestoreRoutine(float seconds)
        {
            var fromPosition = transform.localPosition;
            var fromRotation = transform.localRotation;
            var fromScale = transform.localScale;

            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                // A hand taking hold mid-flight wins: nothing in this app pulls against the player's grip.
                if (_handler != null && _handler.IsManipulating)
                {
                    _restore = null;
                    yield break;
                }

                // Cubic ease-out, matching FreePlacementSolver; nothing bounces (GDD 8.7).
                var t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / seconds), 3f);
                transform.localPosition = Vector3.Lerp(fromPosition, _homePosition, t);
                transform.localRotation = Quaternion.Slerp(fromRotation, _homeRotation, t);
                transform.localScale = Vector3.Lerp(fromScale, _homeScale, t);
                yield return null;
            }

            _restore = null;
            ApplyHome();
        }
    }
}
