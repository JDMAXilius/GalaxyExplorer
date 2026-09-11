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

        private readonly Transform _mouseAttach, _mouseRayOrigin;
        private bool isFocusLocked;

        private GEPointer(IXRInteractor interactor)
        {
            Interactor = interactor;
        }

        private GEPointer(Transform attach, Transform rayOrigin)
        {
            _mouseAttach = attach;
            _mouseRayOrigin = rayOrigin;
            IsMouse = true;
        }

        /// <summary>
        /// A desktop mouse pointer (no XR interactor). Its attach transform follows the mouse ray; see DesktopMouseInput.
        /// </summary>
        public static GEPointer CreateMouse(Transform attach, Transform rayOrigin)
        {
            return new GEPointer(attach, rayOrigin);
        }

        /// <summary>True for the desktop mouse pointer.</summary>
        public bool IsMouse { get; }

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

        /// <summary>The XR interactor behind this pointer; null for the mouse.</summary>
        public IXRInteractor Interactor { get; }

        public Transform Transform => IsMouse ? _mouseAttach : Interactor.transform;

        public Handedness Handedness
        {
            get
            {
                if (IsMouse)
                {
                    return Handedness.None;
                }

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

        public bool IsActive => IsMouse ? _mouseAttach != null : Interactor is Behaviour behaviour && behaviour.isActiveAndEnabled;

        public bool IsPoke => Interactor is XRPokeInteractor;

        /// <summary>True for pointers that reach distant objects with a ray (near-far and ray interactors, the mouse).</summary>
        public bool CanCastFar => IsMouse || Interactor is IXRRayProvider;

        /// <summary>Origin of the far ray (aim pose, or the camera for the mouse).</summary>
        public Transform RayOrigin => IsMouse ? _mouseRayOrigin
            : Interactor is IXRRayProvider rayProvider ? rayProvider.GetOrCreateRayOrigin() : Transform;

        /// <summary>The grab/pinch point that follows the hand, controller or mouse.</summary>
        public Transform AttachTransform => IsMouse ? _mouseAttach : Interactor.GetAttachTransform(null) ?? Transform;

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
            return !IsMouse && (IsPoke || Vector3.Distance(AttachTransform.position, worldPoint) <= NearDistance);
        }

        public bool IsNear(GEInteractable interactable)
        {
            if (IsMouse)
            {
                return false;
            }

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
            if (IsMouse)
            {
                return; // DesktopMouseInput drives the mouse pointer's focus itself
            }

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
