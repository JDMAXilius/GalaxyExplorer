// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Remembers where the spawner put a piece of content, so Restore can bring it back.
    ///
    /// <para><b>Why this is not <see cref="FreePlacementSolver"/>.</b> That component says the same thing —
    /// nothing snaps back on its own, but everything can come home (GDD 5.2) — for a <i>body</i>: it is
    /// <c>[RequireComponent(typeof(ForceSolver))]</c>, home is the solver's live <c>RootTransform</c>, and it
    /// reads the solver's state to decide what to do. The three things the player can arrange that are not
    /// bodies — a nebula overlay opened from the Milky Way map, the Cosmic Web, Andromeda — have no
    /// <c>ForceSolver</c> and belong to no <see cref="LayoutRig"/>, so neither branch of the desktop's Restore
    /// nor <see cref="ExperienceDirector.RestoreEverything"/> could reach them at all (CS-107). This is the
    /// same promise for content whose home is simply the pose it was spawned in.</para>
    ///
    /// <para><b>Home is captured, not computed.</b> Nothing on the object knows where it was meant to be: a
    /// destination overlay is placed by <c>ExperienceDirector.OpenDestination</c> from the tag the player
    /// pinched, and a <c>ContentPrefab</c> is spawned under <c>experience_content_root</c> at whatever the
    /// prefab authored. So the spawner hands it over with <see cref="CaptureHome(Transform)"/>, at the one
    /// moment the pose is the intended one: after the instance is positioned and <b>before the grow-in</b>,
    /// which writes <c>localScale</c> every frame for its duration and passes through zero. A capture taken
    /// any later would record a fraction of the real size and Restore would shrink the object to it.</para>
    ///
    /// <para><b>The pose is stored in the parent's frame</b>, for the reason <see cref="FreePlacementSolver"/>
    /// reads its root live: prefab content hangs off the <c>ViewLoader</c>, which the intro moves and rotates
    /// to place the experience in the room, and a home remembered in world space would send the object back to
    /// where the room used to be. A destination overlay is spawned unparented, where the two are the same.</para>
    ///
    /// <para><b>Deliberately no auto-restore of strays.</b> The fourth rule of GDD 5.2 — out of reach for five
    /// seconds and it comes back — is a body rule, and <see cref="FreePlacementSolver"/> already carries a
    /// switch to turn it off "for objects meant to be left far away". All three objects this is built into are
    /// exactly that: the Cosmic Web is a volume the player stands inside, and a nebula can legitimately be
    /// pushed out to arm's length and beyond. So this component has no <c>Update</c> at all.</para>
    /// </summary>
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
            Resolve();
            EnsureCaptured();
        }

        private void OnDisable()
        {
            // Stopped explicitly rather than assumed dead: deactivating the GameObject does kill a running
            // coroutine, but merely clearing `enabled` does not, and a restore that went on writing the
            // transform of an object somebody had just switched off would be a ghost nobody could explain.
            if (_restore != null)
            {
                StopCoroutine(_restore);
                _restore = null;
            }
        }

        // Nothing here may assume Awake has run: this component is also added at spawn time by
        // CaptureHome(Transform) and used in the same frame, which the project has been bitten by twice.
        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            // includeInactive, because the answer decides who owns this transform for the object's whole life
            // and must not depend on which parents happen to be switched off this instant.
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
        /// Records the current local pose as home. Called by the spawner once the object is where it belongs,
        /// and again by nothing else — a second capture would adopt wherever the player last left it.
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

            // No capture means Resolve stood this anchor down because a ForceSolver owns the object. Home is
            // still (0,0,0)/identity/one here, and writing that would fling a body to its parent's origin.
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

            // As in Restore: an anchor that never captured has no home to write, only a default one.
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

        /// <summary>
        /// Brings home everything the player has arranged that no solver and no rig owns. This is what R, and
        /// the desktop's Recenter, add to the two passes they already make.
        /// </summary>
        public static void RestoreAll()
        {
            foreach (var anchor in FindObjectsByType<FreePlacementAnchor>(FindObjectsSortMode.None))
            {
                anchor.Restore();
            }
        }

        /// <summary>The same, without the travel: an experience is being torn down and nobody will see it.</summary>
        public static void RestoreAllImmediate()
        {
            foreach (var anchor in FindObjectsByType<FreePlacementAnchor>(FindObjectsSortMode.None))
            {
                anchor.RestoreImmediate();
            }
        }

        /// <summary>
        /// Hands a freshly spawned piece of content to this system: every transform in it that a
        /// <see cref="ManipulationHandler"/> can move, and that no <c>ForceSolver</c> owns, gets an anchor and
        /// records the pose it is in right now.
        ///
        /// <para>The anchor is added when it is missing rather than only being captured, so that content built
        /// before CS-107 — or rebuilt by a builder nobody has re-run — still comes home. It can only ever land
        /// on an object that something can move and that nothing else brings back, which is precisely the set
        /// this ticket is about.</para>
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
            }
        }

        private void ApplyHome()
        {
            transform.localPosition = _homePosition;
            transform.localRotation = _homeRotation;
            transform.localScale = _homeScale;
        }

        // There is deliberately no "already home, skip it" test. The nebula overlays billboard themselves, so
        // their rotation never matches the captured one and the test would fire every time; drop rotation from
        // it and a turned-but-not-moved Andromeda would never be straightened. Running the lerp on an object
        // that is already home costs nothing anyone can see.
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

                // Cubic ease-out, the same curve FreePlacementSolver uses. Nothing here bounces (GDD 8.7).
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
