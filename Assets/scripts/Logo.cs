// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEngine;

// Handles functionality for the Logo of the app that appears during introduction flow
namespace GalaxyExplorer
{
    public class Logo : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Distance of Logo in MR")]
        private float LogoDistanceMR = 2.0f;

        private void Start()
        {
            // position the logo and orient it towards the user in MR devices
            if (GalaxyExplorerManager.IsHoloLensGen1 || GalaxyExplorerManager.IsHoloLens2 || GalaxyExplorerManager.IsImmersiveHMD || GalaxyExplorerManager.IsQuest3)
            {
                gameObject.transform.position = Camera.main.transform.position + (Camera.main.transform.forward * LogoDistanceMR);

                Vector3 forwardDirection = gameObject.transform.position - Camera.main.transform.position;
                gameObject.transform.rotation = Quaternion.LookRotation(forwardDirection.normalized);
            }

            MakeSkippable();
        }

        // A pinch, poke or click on the logo ends the logo stage (GDD 2.1). The target is the collider the
        // logo already carries (the fader's box); it had nothing listening on it.
        private void MakeSkippable()
        {
            var box = GetComponentInChildren<BoxCollider>();
            if (box == null || box.GetComponent<GEInteractable>() != null)
            {
                return;
            }

            var interactable = box.gameObject.AddComponent<GEInteractable>();
            PokeSupport.AddPokeFilter(box.gameObject, box, interactable);
            box.gameObject.AddComponent<GEButton>().OnClick.AddListener(() =>
            {
                var intro = FindAnyObjectByType<IntroFlow>();
                if (intro != null)
                {
                    intro.SkipLogo();
                }
            });
        }
    }
}