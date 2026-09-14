// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using CosmicSimulation;
using UnityEngine;
using GalaxyExplorer.XR;

/// <summary>
/// The Milky Way map's nebula marker: the original Galaxy Explorer point of interest, with its picture card,
/// now used two ways at once (CS-170, 14 Sep 2026).
///
/// <list type="bullet">
/// <item><b>Hover</b> (hand ray or mouse over the label) opens the card with the photograph, the distance and
/// the size, at the marker's base; moving off closes it after a short grace so the card does not flicker
/// when the pointer crosses the label's edge.</item>
/// <item><b>Click</b> (pinch or mouse button) travels: the marker's <see cref="ExperienceModule"/> goes through
/// <see cref="ExperienceDirector.Switch(ExperienceModule)"/>, the same call the dock's tiles make, so the
/// Milky Way map and the dock can never disagree about what a place is.</item>
/// </list>
///
/// The card is a hover hint, not a mode: it never sets <see cref="PointOfInterest.IsCardActive"/>, so
/// <c>CardPOIManager</c> never freezes the galaxy's spin or switches the other markers' colliders off the way it
/// did for the original click-to-open card, and the card's own colliders are switched off once so it can never
/// steal the hover from the label that opened it.
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

        /// <summary>True while the hover card is showing.</summary>
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
                // Scale card/magic window based on platform
                CardObject.transform.localScale *= GalaxyExplorerManager.MagicWindowScaleFactor;

                // The card used to carry a close button and a pressable plate of its own. Under a hover card
                // those would sit between the pointer and the label, take the focus, close the card, give the
                // focus back, and reopen it - a flicker loop. The card is purely visual now, so its colliders
                // are off for good; the label's own collider is on the marker, outside the card, and untouched.
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
            // If the card of this poi is open, then override the card's and descriptions's position and rotation so these are moved with the rotation animation
            if (CardObject && CardObject.gameObject.activeSelf)
            {
                CardObject.transform.rotation = cardRotation;
                CardObject.transform.position = cardOffsetTransform.position - cardOffset;
            }
        }

        protected override void UpdateState()
        {
        }

        // ---------- hover: the card

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
            // TODO this need to be removed and happen in the animation, but it doesnt
            CardObject.gameObject.SetActive(false);

            if (CardAnimator)
            {
                CardAnimator.SetBool("CardVisible", false);
            }
        }

        // ---------- click: travel

        public override void OnPointerDown(GEPointerEventData eventData)
        {
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

            // Switch, and not DockController.Choose: Choose needs a dock tile and opens the layout pop-up beside
            // the dock, which is not where the player is looking when they have just pinched a nebula on the far
            // side of the galaxy. Switch itself answers a refusal - intro running, a switch in flight, a module
            // with nothing to open - through SwitchNotice, so no click is swallowed in silence.
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
