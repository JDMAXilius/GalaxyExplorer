// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using HoloToolkit.Unity.InputModule;

using UnityEngine;
using GalaxyExplorer.XR;

/// <summary>
/// Planet script is attached to every planet gameobject, the actual sphere of the planet so user is able to aitap, mouse click or touch the planet
/// </summary>
namespace GalaxyExplorer
{
    public class Planet : MonoBehaviour, IGEPointerHandler, IGEFocusHandler//, IInputClickHandler, IFocusable, IControllerTouchpadHandler
    {
        [SerializeField]
        private PointOfInterest POI = null;

        public void OnPointerUp(GEPointerEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnPointerUp(eventData);
            }
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnPointerDown(eventData);
            }
        }

        public void OnPointerClicked(GEPointerEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnPointerClicked(eventData);
            }
        }

        public void OnBeforeFocusChange(GEFocusEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnBeforeFocusChange(eventData);
            }
        }

        public void OnFocusChanged(GEFocusEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnFocusChanged(eventData);
            }
        }

        public void OnFocusEnter(GEFocusEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnFocusEnter(eventData);
            }
        }

        public void OnFocusExit(GEFocusEventData eventData)
        {
            if (POI != null && POI.isActiveAndEnabled)
            {
                POI.OnFocusExit(eventData);
            }
        }
    }
}