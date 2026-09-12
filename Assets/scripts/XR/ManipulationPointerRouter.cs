// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Hands a routed select event to a <see cref="ManipulationHandler"/> that nothing else drives.
    ///
    /// <para><b>Why this exists at all.</b> <see cref="ManipulationHandler"/> deliberately does not implement
    /// <see cref="IGEPointerHandler"/>. Hand and ray input arrives through
    /// <see cref="GEInteractable"/> -> <c>GEInputEvents.ExecuteHierarchy</c>, which invokes
    /// <i>every</i> enabled handler on the <i>first</i> GameObject up the chain that has one and then stops. On
    /// a body, <c>ForceSolver</c> is that handler and it calls the manipulation handler itself, at the moment
    /// its state machine says a grab is allowed. If the handler also implemented the interface, both of the
    /// two shapes bodies are built in would break:
    /// <list type="bullet">
    /// <item>the <c>poi_*</c> prefabs put the handler and the interactable on the mesh and the solver on the
    /// parent, so the walk would stop at the mesh and the solver would never hear a pinch at all — no dwell,
    /// no force pull, no attraction;</item>
    /// <item><c>solar_system_planets_content_prefab</c> puts all three on one GameObject, so the handler would
    /// be invoked twice per pinch, once directly and once forwarded by the solver.</item>
    /// </list>
    /// So the delivery is a separate component that is only put on content the solver does not own.</para>
    ///
    /// <para><b>Where it goes.</b> On the same GameObject as the <see cref="ManipulationHandler"/> and the
    /// <see cref="GEInteractable"/> — the nebula overlays, the Cosmic Web volume, Andromeda. Near pinch, far
    /// ray and fingertip poke all arrive as the same select on that interactable, so all three work from this
    /// one path, and two hands work because <see cref="GEInteractable"/> selects in Multiple mode and each
    /// hand arrives as its own pointer.</para>
    ///
    /// <para><b>The guard.</b> If a <c>ForceSolver</c> turns up on this object or above it, this component
    /// disables itself and says so. Disabling rather than merely ignoring the event is the point:
    /// <c>GEInputEvents.ExecuteHierarchy</c> skips disabled behaviours, so the walk carries on to the
    /// solver exactly as it does today, instead of being swallowed here.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ManipulationHandler))]
    public class ManipulationPointerRouter : MonoBehaviour, IGEPointerHandler
    {
        private ManipulationHandler _handler;
        private bool _resolved;

        private void Awake()
        {
            Resolve();
        }

        /// <summary>
        /// The handler to drive, or null when this router has stood down. Resolved from every entry point
        /// because a component added with <c>AddComponent</c> and used in the same frame has not had
        /// <c>Awake</c>.
        /// </summary>
        private ManipulationHandler Handler
        {
            get
            {
                Resolve();
                return _handler;
            }
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            // includeInactive: the answer must not depend on whether a parent happens to be switched off at
            // this instant, because it decides who owns the grab for the lifetime of the object.
            var owner = GetComponentInParent<ForceSolver>(true);
            if (owner != null)
            {
                Debug.LogError(
                    $"{name}: ManipulationPointerRouter sits under the ForceSolver on {owner.name}, which " +
                    "already drives the manipulation handler. Disabling it; remove it from the prefab.", this);
                enabled = false;
                return;
            }

            _handler = GetComponent<ManipulationHandler>();
            if (_handler == null)
            {
                Debug.LogError($"{name}: ManipulationPointerRouter has no ManipulationHandler to drive.", this);
                enabled = false;
            }
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            var handler = Handler;
            if (handler != null)
            {
                handler.OnPointerDown(eventData);
            }
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
            var handler = Handler;
            if (handler != null)
            {
                handler.OnPointerUp(eventData);
            }
        }

        // A grab has no click; the up above has already ended it. Note that implementing the interface makes
        // this object the end of the walk for clicks too. That costs nothing on the three prefabs this is
        // built into — a nebula overlay is instantiated unparented, and the Cosmic Web and Andromeda hang off
        // the director's experience_content_root — none of which has a pointer handler above it. Anything
        // wired differently has to check that before adding this component.
        public void OnPointerClicked(GEPointerEventData eventData)
        {
        }
    }
}
