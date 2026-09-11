// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// App-side view of an XR Interaction Toolkit interactor (a hand or controller ray, poke, or grab).
    /// Replaces MRTK's IMixedRealityPointer for the app's input handlers.
    /// </summary>
    public sealed class GEPointer
    {
        /// <summary>Distance (m) from the interactor's attach point within which interaction counts as "near".</summary>
        public const float NearDistance = 0.12f;

        private static readonly Dictionary<IXRInteractor, GEPointer> Pointers = new Dictionary<IXRInteractor, GEPointer>();

        private bool isFocusLocked;

        private GEPointer(IXRInteractor interactor)
        {
            Interactor = interactor;
        }

        public static GEPointer For(IXRInteractor interactor)
        {
            if (interactor == null)
            {
                return null;
            }

            if (!Pointers.TryGetValue(interactor, out var pointer))
            {
                pointer = new GEPointer(interactor);
                Pointers.Add(interactor, pointer);
            }

            return pointer;
        }

        public IXRInteractor Interactor { get; }

        public Transform Transform => Interactor.transform;

        public Handedness Handedness
        {
            get
            {
                switch (Interactor.handedness)
                {
                    case InteractorHandedness.Left:
                        return Handedness.Left;
                    case InteractorHandedness.Right:
                        return Handedness.Right;
                    default:
                        return Handedness.None;
                }
            }
        }

        public bool IsActive => Interactor is Behaviour behaviour && behaviour.isActiveAndEnabled;

        public bool IsPoke => Interactor is XRPokeInteractor;

        /// <summary>True for interactors that can reach distant objects with a ray (near-far and ray interactors).</summary>
        public bool CanCastFar => Interactor is IXRRayProvider;

        /// <summary>Origin of the far ray (aim pose), or the interactor itself when it has no ray.</summary>
        public Transform RayOrigin => Interactor is IXRRayProvider rayProvider ? rayProvider.GetOrCreateRayOrigin() : Transform;

        /// <summary>The grab/pinch point that follows the hand or controller.</summary>
        public Transform AttachTransform => Interactor.GetAttachTransform(null) ?? Transform;

        /// <summary>The object this pointer is currently driving (set by ForceSolver while pulling a planet).</summary>
        public object FocusTarget { get; set; }

        /// <summary>The interactables this pointer currently focuses (hovers), as seen by the app.</summary>
        internal HashSet<GEInteractable> Focused { get; } = new HashSet<GEInteractable>();

        /// <summary>
        /// While locked, focus stays on the currently focused objects: exits are deferred and other objects
        /// are ignored, so a planet keeps its pointers while it flies toward the hand.
        /// </summary>
        public bool IsFocusLocked
        {
            get => isFocusLocked;
            set
            {
                if (isFocusLocked == value)
                {
                    return;
                }

                isFocusLocked = value;
                if (!isFocusLocked)
                {
                    ResyncFocus();
                }
            }
        }

        public bool IsNear(Vector3 worldPoint)
        {
            return IsPoke || Vector3.Distance(AttachTransform.position, worldPoint) <= NearDistance;
        }

        public bool IsNear(GEInteractable interactable)
        {
            if (IsPoke)
            {
                return true;
            }

            var attachPosition = AttachTransform.position;
            foreach (var collider in interactable.colliders)
            {
                if (collider != null && collider.enabled &&
                    Vector3.Distance(collider.ClosestPoint(attachPosition), attachPosition) <= NearDistance)
                {
                    return true;
                }
            }

            return false;
        }

        // Raise the exits/enters that were suppressed while focus was locked.
        private void ResyncFocus()
        {
            var hovered = new HashSet<GEInteractable>();
            if (Interactor is IXRHoverInteractor hoverInteractor)
            {
                foreach (var interactable in hoverInteractor.interactablesHovered)
                {
                    if (interactable is GEInteractable geInteractable)
                    {
                        hovered.Add(geInteractable);
                    }
                }
            }

            foreach (var interactable in new List<GEInteractable>(Focused))
            {
                if (!hovered.Contains(interactable) || interactable == null)
                {
                    Focused.Remove(interactable);
                    if (interactable != null)
                    {
                        interactable.RaiseFocusExit(this);
                    }
                }
            }

            foreach (var interactable in hovered)
            {
                if (Focused.Add(interactable))
                {
                    interactable.RaiseFocusEnter(this);
                }
            }
        }
    }
}
