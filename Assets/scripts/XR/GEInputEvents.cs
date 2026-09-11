// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Receives select (pinch, trigger, poke press, mouse click) events routed by <see cref="GEInteractable"/>.
    /// </summary>
    public interface IGEPointerHandler
    {
        void OnPointerDown(GEPointerEventData eventData);
        void OnPointerUp(GEPointerEventData eventData);
        void OnPointerClicked(GEPointerEventData eventData);
    }

    /// <summary>
    /// Receives hover (focus) enter/exit events routed by <see cref="GEInteractable"/>.
    /// </summary>
    public interface IGEFocusHandler
    {
        void OnFocusEnter(GEFocusEventData eventData);
        void OnFocusExit(GEFocusEventData eventData);
    }

    /// <summary>
    /// Receives notifications when a pointer's focus moves onto or off an object in this hierarchy.
    /// </summary>
    public interface IGEFocusChangedHandler
    {
        void OnBeforeFocusChange(GEFocusEventData eventData);
        void OnFocusChanged(GEFocusEventData eventData);
    }

    public class GEPointerEventData
    {
        public GEPointerEventData(GEPointer pointer, GEInteractable interactable = null, bool isNear = false)
        {
            Pointer = pointer;
            Interactable = interactable;
            IsNear = isNear;
        }

        /// <summary>The pointer that raised the event; null for desktop/mouse or code-driven clicks.</summary>
        public GEPointer Pointer { get; }

        /// <summary>The interactable (collider owner) the pointer selected.</summary>
        public GEInteractable Interactable { get; }

        /// <summary>True when the pointer was within grab distance (near interaction) rather than a far ray.</summary>
        public bool IsNear { get; }

        public bool Used { get; private set; }

        public void Use()
        {
            Used = true;
        }
    }

    public class GEFocusEventData
    {
        public GEFocusEventData(GEPointer pointer, GameObject oldFocusedObject, GameObject newFocusedObject, bool isNear)
        {
            Pointer = pointer;
            OldFocusedObject = oldFocusedObject;
            NewFocusedObject = newFocusedObject;
            IsNear = isNear;
        }

        public GEPointer Pointer { get; }
        public GameObject OldFocusedObject { get; }
        public GameObject NewFocusedObject { get; }

        /// <summary>True when the pointer is within grab distance of the focused object.</summary>
        public bool IsNear { get; }
    }

    public static class GEInputEvents
    {
        /// <summary>
        /// Raised for every select on any <see cref="GEInteractable"/> and for select presses that hit nothing,
        /// mirroring MRTK's global input listeners.
        /// </summary>
        public static event Action<GEPointerEventData> GlobalPointerDown;

        public static void RaiseGlobalPointerDown(GEPointerEventData eventData)
        {
            GlobalPointerDown?.Invoke(eventData);
        }

        private static readonly List<Component> ComponentBuffer = new List<Component>();

        /// <summary>
        /// Invokes <paramref name="action"/> on every enabled <typeparamref name="T"/> on the first GameObject,
        /// starting at <paramref name="target"/> and walking up its parents, that has one. This matches how
        /// MRTK (and Unity's ExecuteEvents.ExecuteHierarchy) routed input to handlers.
        /// </summary>
        public static GameObject ExecuteHierarchy<T>(GameObject target, Action<T> action) where T : class
        {
            for (var t = target != null ? target.transform : null; t != null; t = t.parent)
            {
                ComponentBuffer.Clear();
                t.GetComponents(ComponentBuffer);
                var handled = false;
                // Copy: handlers may add/remove components while being invoked.
                var components = ComponentBuffer.ToArray();
                foreach (var component in components)
                {
                    if (component is T handler && (!(component is Behaviour behaviour) || behaviour.isActiveAndEnabled))
                    {
                        action(handler);
                        handled = true;
                    }
                }

                if (handled)
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
