// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Turns XR Interaction Toolkit hover/select on this GameObject's colliders into the app's focus and pointer
    /// events (<see cref="IGEFocusHandler"/>, <see cref="IGEFocusChangedHandler"/>, <see cref="IGEPointerHandler"/>),
    /// routed to the nearest handler in the parent hierarchy the same way MRTK routed its input events.
    /// One is placed on every collider-owning GameObject that has an input handler above it.
    /// </summary>
    [DisallowMultipleComponent]
    public class GEInteractable : XRBaseInteractable, IFarAttachProvider
    {
        // Keep a far grab point on the object instead of letting the interactor pull it in; planets are
        // pulled by ForceSolver, not by XRI.
        public InteractableFarAttachMode farAttachMode { get; set; } = InteractableFarAttachMode.Far;

        protected override void Awake()
        {
            selectMode = InteractableSelectMode.Multiple; // two-handed manipulation
            base.Awake();

            // Only this object's own colliders: child colliders belong to their own GEInteractable, and XRI
            // allows a collider to be registered with a single interactable. (The base class gathers colliders
            // from children when the list is empty; colliders are registered later, in OnEnable.)
            colliders.RemoveAll(c => c == null || c.gameObject != gameObject);
        }

        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);

            var pointer = GEPointer.For(args.interactorObject);
            if (pointer == null || (pointer.IsFocusLocked && !pointer.Focused.Contains(this)))
            {
                return;
            }

            if (pointer.Focused.Add(this))
            {
                RaiseFocusEnter(pointer);
            }
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);

            var pointer = GEPointer.For(args.interactorObject);
            if (pointer == null || pointer.IsFocusLocked)
            {
                // Locked pointers keep their focus; GEPointer raises the exit when the lock is released.
                return;
            }

            if (pointer.Focused.Remove(this))
            {
                RaiseFocusExit(pointer);
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            RaisePointerDown(GEPointer.For(args.interactorObject));
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            RaisePointerUp(GEPointer.For(args.interactorObject), !args.isCanceled);
        }

        internal void RaisePointerDown(GEPointer pointer)
        {
            var eventData = new GEPointerEventData(pointer, this, pointer != null && pointer.IsNear(this));
            GEInputEvents.ExecuteHierarchy<IGEPointerHandler>(gameObject, h => h.OnPointerDown(eventData));
            GEInputEvents.RaiseGlobalPointerDown(eventData);
        }

        internal void RaisePointerUp(GEPointer pointer, bool clicked)
        {
            var eventData = new GEPointerEventData(pointer, this, pointer != null && pointer.IsNear(this));
            GEInputEvents.ExecuteHierarchy<IGEPointerHandler>(gameObject, h => h.OnPointerUp(eventData));
            if (clicked)
            {
                GEInputEvents.ExecuteHierarchy<IGEPointerHandler>(gameObject, h => h.OnPointerClicked(eventData));
            }
        }

        internal void RaiseFocusEnter(GEPointer pointer)
        {
            var eventData = new GEFocusEventData(pointer, null, gameObject, pointer.IsNear(this));
            GEInputEvents.ExecuteHierarchy<IGEFocusChangedHandler>(gameObject, h => h.OnBeforeFocusChange(eventData));
            GEInputEvents.ExecuteHierarchy<IGEFocusHandler>(gameObject, h => h.OnFocusEnter(eventData));
            GEInputEvents.ExecuteHierarchy<IGEFocusChangedHandler>(gameObject, h => h.OnFocusChanged(eventData));
        }

        internal void RaiseFocusExit(GEPointer pointer)
        {
            var eventData = new GEFocusEventData(pointer, gameObject, null, false);
            GEInputEvents.ExecuteHierarchy<IGEFocusChangedHandler>(gameObject, h => h.OnBeforeFocusChange(eventData));
            GEInputEvents.ExecuteHierarchy<IGEFocusHandler>(gameObject, h => h.OnFocusExit(eventData));
            GEInputEvents.ExecuteHierarchy<IGEFocusChangedHandler>(gameObject, h => h.OnFocusChanged(eventData));
        }
    }
}
