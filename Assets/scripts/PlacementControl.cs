// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;
using GalaxyExplorer.XR;

namespace GalaxyExplorer
{
    public class PlacementControl : MonoBehaviour
    {
        private bool isPlaced;
        private Camera _cameraMain;
        private PlacementConfirmationButton _confirmationButton;
        private ForceSolver _forceSolver;
        private PlacementRing _placementRing;
        
        [SerializeField]
        private float DesktopDuration = 2.0f;

        [SerializeField]
        private Animator IntroEarthPlacementAnimator;

        [SerializeField]
        [Tooltip("Metres in front of the player the Start button appears once the orb is free (CS-281). It used " +
                 "to hang at the ring's edge, wherever the orb had been pulled to, which put it behind the orb's " +
                 "collider from where the player stood often enough that Start was hard to hit.")]
        private float startButtonDistanceMetres = 0.5f;

        [SerializeField]
        [Tooltip("Metres below the eyes for the Start button when there is no dock to borrow a chest height from.")]
        private float startButtonBelowEyesMetres = 0.4f;

        public delegate void ContentPlacedCallback(Vector3 position);

        public ContentPlacedCallback OnContentPlaced;
        public GEButton PlacementConfirmationButton;
        public Transform ConfirmationButtonOffsetTransform;

        private void Awake()
        {
            _confirmationButton = GetComponentInChildren<PlacementConfirmationButton>();
            _forceSolver = GetComponent<ForceSolver>();
            _placementRing = GetComponentInChildren<PlacementRing>();
        }

        private void Start()
        {
            _cameraMain = Camera.main;
            
            IntroEarthPlacementAnimator.SetTrigger("Intro");

            var buttonOffset = ConfirmationButtonOffsetTransform.localPosition;

            switch (GalaxyExplorerManager.Platform)
            {
                case GalaxyExplorerManager.PlatformId.HoloLensGen1:
                    _placementRing.gameObject.SetActive(false);
                    buttonOffset.z = -.2f;
                    break;
                
                case GalaxyExplorerManager.PlatformId.Desktop:
                    _placementRing.gameObject.SetActive(false);
                    StartCoroutine(ReleaseContent(DesktopDuration));
                    isPlaced = true;
                    StartCoroutine(StartOnboarding(true));
                    return;
                
                case GalaxyExplorerManager.PlatformId.ArticulatedHandsPlatform:
                case GalaxyExplorerManager.PlatformId.ImmersiveHMD:
                case GalaxyExplorerManager.PlatformId.Quest3:
                    buttonOffset.z = -_placementRing.Diameter * .5f;
                    break;
                
                case GalaxyExplorerManager.PlatformId.Phone:
                    //Should never get here
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ConfirmationButtonOffsetTransform.localPosition = buttonOffset;
            
            // if platform is desktop then bypass placement
            if (GalaxyExplorerManager.IsDesktop)
            {
                _placementRing.gameObject.SetActive(false);
                StartCoroutine(ReleaseContent(DesktopDuration));
                isPlaced = true;
                StartCoroutine(StartOnboarding(true));
                return;
            }

            // Position earth pin in front of camera and a bit lower on headsets where the user stands at full height
            var offset = GalaxyExplorerManager.IsImmersiveHMD || GalaxyExplorerManager.IsQuest3 ? Vector3.down * .5f : Vector3.zero;

            gameObject.transform.position =
                _cameraMain.transform.position + _cameraMain.transform.forward * 2f + offset;
            
            PlacementConfirmationButton.OnClick.AddListener(ConfirmPlacement);

            // The scene wires SetToFree to the button's Show; this is where it shows. Listened to here rather
            // than placed once at Start because the orb is let go more than once when the player re-pulls it,
            // and the button goes with the orb's root until the next release re-places it.
            if (_forceSolver != null && GalaxyExplorerManager.IsQuest3)
            {
                _forceSolver.SetToFree.AddListener(PlaceStartButton);
            }

            StartCoroutine(StartOnboarding());
        }

        // In front of the player at chest height, facing them, the way the dock parks — not at the ring's edge
        // below the orb. The ring-edge offset set in Start still stands for the HoloLens branch, which does not
        // reach here. Chest height is the dock's own rule when a dock exists, so the two agree on what "chest"
        // means for this player; otherwise a fixed drop below the eyes.
        private void PlaceStartButton(ForceSolver _)
        {
            if (ConfirmationButtonOffsetTransform == null || _cameraMain == null)
            {
                return;
            }

            var head = _cameraMain.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            var dock = CosmicSimulation.DockController.Instance;
            var height = dock != null ? dock.HeightMetres : head.position.y - startButtonBelowEyesMetres;

            ConfirmationButtonOffsetTransform.position =
                new Vector3(head.position.x, height, head.position.z) + forward * startButtonDistanceMetres;
            ConfirmationButtonOffsetTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private IEnumerator StartOnboarding(bool skipPlacement = false)
        {
            while (!GalaxyExplorerManager.IsInitialized)
            {
                yield return null;
            }
            GalaxyExplorerManager.Instance.OnboardingManager.StartIntro(_forceSolver, skipPlacement);
        }

        private IEnumerator ReleaseContent(float waitingTime)
        {
            IntroEarthPlacementAnimator.SetTrigger("Place");

            GalaxyExplorerManager.Instance.OnboardingManager.OnPlacementConfirmed();
            // have to make sure finished onboarding if vo manager is still fading it out
            while (GalaxyExplorerManager.Instance.VoManager.IsPlaying)
            {
                yield return null;
            }
            yield return new WaitForSeconds(waitingTime);


            OnContentPlaced?.Invoke(transform.position);

            yield return null;
        }

        public void ConfirmPlacement()
        {
            if (!isPlaced)
            {
                StartCoroutine(ReleaseContent(0));
            }
            isPlaced = true;
            _confirmationButton.Hide();
        }
    }
}
