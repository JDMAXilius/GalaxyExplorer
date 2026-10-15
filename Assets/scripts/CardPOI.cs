// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using CosmicSimulation;
using UnityEngine;
using GalaxyExplorer.XR;

/// <summary>
/// Nebula marker: hover shows the card; click travels through <see cref="ExperienceDirector.Switch(ExperienceModule)"/>
/// like a dock tile, so map and dock agree. The card never sets <see cref="PointOfInterest.IsCardActive"/>, so
/// <c>CardPOIManager</c> neither stops the galaxy's spin nor turns the other markers' colliders off.
/// </summary>
namespace GalaxyExplorer
{
    public class CardPOI : PointOfInterest
    {
        [SerializeField]
        [Tooltip("Where a click on this marker goes. A destination or experience module; opened through " +
                 "ExperienceDirector.Switch, like a dock tile.")]
        private ExperienceModule destination = null;

        [SerializeField]
        [Tooltip("Fallback for the module above: the module id ('crab', 'helix', ...) looked up on the " +
                 "director when no asset is assigned.")]
        private string destinationId = "";

        [SerializeField]
        [Tooltip("How long the card stays open after the pointer leaves the label, in seconds. Long enough " +
                 "to survive a pointer that skims the label's edge, short enough that the card reads as " +
                 "belonging to the hover.")]
        private float hideGraceSeconds = 0.35f;

        [SerializeField]
        private POIContent CardObject = null;

        [SerializeField]
        private Animator CardAnimator = null;

        [SerializeField]
        [Tooltip("Kept for the prefab overrides. The card no longer narrates on hover; the place the click " +
                 "opens has its own narration on its module.")]
        private AudioClip CardAudio = null;

        private Quaternion cardRotation = Quaternion.identity;
        private Vector3 cardOffset = Vector3.zero;
        private Transform cardOffsetTransform = null; // Transform from which card remains static

        private Coroutine hideRoutine = null;
        private bool cardShown = false;

        /// <summary>The module a click on this marker opens, or null when none is wired.</summary>
        public ExperienceModule Destination
        {
            get
            {
                if (destination != null)
                {
                    return destination;
                }

                var director = ExperienceDirector.Instance;
                return director != null && !string.IsNullOrEmpty(destinationId) ? director.Find(destinationId) : null;
            }
        }

        public bool IsCardShown
        {
            get { return cardShown; }
        }

        /// <summary>The narration clip the original card played on open. Not played on hover; kept wired.</summary>
        public AudioClip Narration
        {
            get { return CardAudio; }
        }

        protected override void Start()
        {
            base.Start();

            cardOffsetTransform = transform.parent.parent.parent;

            if (CardObject)
            {
                CardObject.transform.localScale *= GalaxyExplorerManager.MagicWindowScaleFactor;

                // Card colliders would take the focus from the label and close and reopen the card in a loop.
                // The label's own collider is on the marker, outside the card.
                foreach (var collider in CardObject.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }

                CardObject.gameObject.SetActive(false);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            if (cardShown)
            {
                HideCard();
            }
        }

        private void LateUpdate()
        {
            // Follow the galaxy's rotation animation by position only, keeping the rotation the card opened with.
            if (CardObject && CardObject.gameObject.activeSelf)
            {
                CardObject.transform.rotation = cardRotation;
                CardObject.transform.position = cardOffsetTransform.position - cardOffset;
            }
        }

        protected override void UpdateState()
        {
        }

        public override void OnFocusEnter(GEFocusEventData eventData)
        {
            base.OnFocusEnter(eventData);

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            ShowCard();
        }

        public override void OnFocusExit(GEFocusEventData eventData)
        {
            base.OnFocusExit(eventData);

            if (cardShown && hideRoutine == null && isActiveAndEnabled)
            {
                hideRoutine = StartCoroutine(HideAfterGrace());
            }
        }

        private IEnumerator HideAfterGrace()
        {
            yield return new WaitForSeconds(hideGraceSeconds);
            hideRoutine = null;
            HideCard();
        }

        private void ShowCard()
        {
            if (!CardObject || cardShown)
            {
                return;
            }

            cardShown = true;

            CardObject.gameObject.SetActive(true);

            if (CardAnimator)
            {
                CardAnimator.SetBool("CardVisible", true);
            }
            CardObject.ShowContents();

            if (LineBase)
            {
                CardObject.transform.position = LineBase.transform.position;
            }
            else
            {
                CardObject.transform.position = transform.position;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 forwardDirection = transform.position - camera.transform.position;
                CardObject.transform.rotation = Quaternion.LookRotation(forwardDirection.normalized, camera.transform.up);
            }
            cardRotation = CardObject.transform.rotation;

            cardOffset = cardOffsetTransform.position - CardObject.transform.position;
        }

        private void HideCard()
        {
            if (!CardObject || !cardShown)
            {
                return;
            }

            cardShown = false;

            CardObject.HideContents();
            // Here rather than in the hide animation, which does not deactivate the card.
            CardObject.gameObject.SetActive(false);

            if (CardAnimator)
            {
                CardAnimator.SetBool("CardVisible", false);
            }
        }

        public override void OnPointerDown(GEPointerEventData eventData)
        {
            if (IsDoublePress())
            {
                Enter(Destination);
                return;
            }

            if (isCoolingDown)
            {
                return;
            }

            // Plays the select sound, tells CardPOIManager, and starts the cool-down.
            base.OnPointerDown(eventData);

            Travel();
        }

        /// <summary>Opens this marker's place. Public so a harness can drive a marker without synthetic input.</summary>
        public void Travel()
        {
            var module = Destination;
            if (module == null)
            {
                Debug.LogWarning($"CardPOI: '{name}' has no destination module, so a click on it goes nowhere. " +
                                 "Assign one on the marker in galaxy_pois_prefab.", this);
                return;
            }

            var director = ExperienceDirector.Instance;
            if (director == null)
            {
                Debug.LogWarning($"CardPOI: no ExperienceDirector, so '{module.Id}' cannot be opened. " +
                                 "core_systems_scene has to be loaded for the map to go anywhere.", this);
                return;
            }

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            HideCard();

            // UI 2.0 shows a preview card first; the card's own Enter does the travelling.
            if (PlanetPOI.Preview != null && PlanetPOI.Preview(module))
            {
                return;
            }

            // Not DockController.Choose: that opens its pop-up beside the dock, not where the player is looking.
            // Switch reports its own refusals through SwitchNotice, so no click is swallowed in silence.
            director.Switch(module);
        }

        public void CloseAnyOpenCard()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
            HideCard();
        }
    }
}
