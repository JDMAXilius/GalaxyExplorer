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

            // On the Quest, Start is there from the first moment and stays until it is pressed (owner, 6 Oct):
            // the original only showed it once the orb had been pulled, and hid it again on every grab, so a
            // player who never pulled the orb, or who was holding it, had no way to begin. The scene still wires
            // SetToManipulate to the button's Hide; persistent listeners run before these, so Show wins. It is
            // re-placed on each release because the orb may have come to rest where the button was.
            if (_forceSolver != null && GalaxyExplorerManager.IsQuest3)
            {
                _forceSolver.SetToFree.AddListener(PlaceStartButton);
                _forceSolver.SetToManipulate.AddListener(KeepStartShown);
                StartCoroutine(ShowStartWhenPosed());
            }

            StartCoroutine(StartOnboarding());
        }

        // The head reads the floor until the headset reports its first pose, and a button placed from that
        // would sit at the player's feet. Not waited for for ever: a pose that never comes still gets a button.
        private IEnumerator ShowStartWhenPosed()
        {
            for (var waited = 0f; _cameraMain.transform.position.y < 0.5f && waited < 2f; waited += Time.deltaTime)
            {
                yield return null;
            }

            PlaceStartButton(null);
            _confirmationButton.Show();
        }

        private void KeepStartShown(ForceSolver _)
        {
            if (!isPlaced)
            {
                _confirmationButton.Show();
            }
        }

        // The button hangs under the orb's root, so it would ride along whenever the orb is pulled or carried.
        // Held where it was put instead, after everything that moves the orb has run.
        private Pose? _startPose;

        private void LateUpdate()
        {
            if (_startPose.HasValue && !isPlaced && ConfirmationButtonOffsetTransform != null)
            {
                ConfirmationButtonOffsetTransform.SetPositionAndRotation(_startPose.Value.position, _startPose.Value.rotation);
            }
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

            // The orb comes to rest with its ring at the hand that pulled it, which is about where this button
            // goes. A pinch within GEPointer.NearDistance of any orb collider grabs the orb, and the scene hides
            // the button on a grab, so a Start left among the orb's colliders vanished whenever it was pinched
            // at. Step it back towards the player until it is clear of them.
            var origin = new Vector3(head.position.x, height, head.position.z);
            var orbParts = _forceSolver.GetComponentsInChildren<GEInteractable>();
            var distance = startButtonDistanceMetres;
            while (distance > StartButtonNearestMetres && OrbCovers(orbParts, origin + forward * distance))
            {
                distance -= 0.05f;
            }

            _startPose = new Pose(origin + forward * distance, Quaternion.LookRotation(forward, Vector3.up));
            ConfirmationButtonOffsetTransform.SetPositionAndRotation(_startPose.Value.position, _startPose.Value.rotation);
        }

        private const float StartButtonNearestMetres = 0.25f;

        // Whether a point is inside, or within grabbing distance of, anything of the orb's that takes a pinch.
        private bool OrbCovers(GEInteractable[] orbParts, Vector3 point)
        {
            foreach (var part in orbParts)
            {
                if (part.transform.IsChildOf(ConfirmationButtonOffsetTransform))
                {
                    continue;
                }

                foreach (var orbCollider in part.colliders)
                {
                    if (orbCollider == null || !orbCollider.enabled)
                    {
                        continue;
                    }

                    var bounds = orbCollider.bounds;
                    bounds.Expand(2f * (GEPointer.NearDistance + 0.03f));
                    if (bounds.Contains(point))
                    {
                        return true;
                    }
                }
            }

            return false;
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
