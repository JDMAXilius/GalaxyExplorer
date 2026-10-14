// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Delivers routed select events to a <see cref="ManipulationHandler"/> on content no <c>ForceSolver</c> owns.
    /// </summary>
    // The handler itself does not implement IGEPointerHandler: ExecuteHierarchy stops at the first handler, which
    // would starve a parent ForceSolver or double-deliver when both share a GameObject. Disables itself under a solver.
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

        /// <summary>Resolved lazily: a component added with AddComponent and used the same frame has not had Awake.</summary>
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

            // includeInactive: ownership of the grab must not depend on which parents are switched off right now.
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

        // Implementing the interface also ends the walk for clicks; harmless only while nothing above this
        // object handles pointers (true for the nebula, Cosmic Web and Andromeda prefabs).
        public void OnPointerClicked(GEPointerEventData eventData)
        {
        }
    }
}
