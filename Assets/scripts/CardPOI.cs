// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using HoloToolkit.Unity.InputModule;
using System.Collections;
using UnityEngine;
using GalaxyExplorer.XR;

/// <summary>
/// Its attached to the poi if the poi is supposed to launch a card when selected.
///
/// Retired as of CS-050: the Milky Way map's destinations are <c>CosmicSimulation.LabelButton</c> tags routed
/// by <c>CosmicSimulation.DestinationTags</c>, and this marker switches itself off before it can draw or
/// register anything. Kept, not deleted — see the note on <c>keepLegacyCards</c>.
/// </summary>
namespace GalaxyExplorer
{
    public class CardPOI : PointOfInterest
    {
        [SerializeField]
        [Tooltip("Bring the original Galaxy Explorer POI card back, for diagnosis. Off — the shipping " +
                 "setting — the marker switches itself off in Awake and the LabelButton tags are the only " +
                 "way off the Milky Way map (CS-050).")]
        private bool keepLegacyCards = false;

        [SerializeField]
        private POIContent CardObject = null;

        [SerializeField]
        private Animator CardAnimator = null;

        [SerializeField]
        private AudioClip CardAudio = null;

        private Quaternion cardRotation = Quaternion.identity;
        private Vector3 cardOffset = Vector3.zero;
        private Transform cardOffsetTransform = null; // Transform from which card remains static

        private POIMaterialsFader poiFader = null;

        /// <summary>
        /// Turns the whole marker off before anything it owns can run.
        ///
        /// Switched off rather than removed from the prefabs, and rather than deleted outright, because
        /// <c>poi_prefab</c> and its variant <c>poi_prefab_large</c> are still referenced from
        /// <c>galaxy_pois_prefab</c>, from <c>galaxy_view_scene</c> (the <c>galactic_center</c> marker sits
        /// loose in the scene, not in the POI prefab) and from <c>projector_poi_prototype</c>. Deleting either
        /// would leave a dangling reference in a shipping scene, which is a worse outcome than dead code.
        /// This is the retirement CS-087 gave the planet preview bar: it costs nothing at run time, and the
        /// toggle above brings it back while diagnosing.
        ///
        /// In <c>Awake</c> rather than <c>Start</c> or <c>OnEnable</c> so that <c>PointOfInterest.Awake</c> —
        /// which instantiates the indicator line's material — never runs either.
        /// </summary>
        protected override void Awake()
        {
            if (!keepLegacyCards)
            {
                gameObject.SetActive(false);
                return;
            }

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            // Find poi fader which lives in the same scene as this object and not the one that might exist in the previous scene
            POIMaterialsFader[] allPoiFaders = FindObjectsOfType<POIMaterialsFader>();
            foreach (var fader in allPoiFaders)
            {
                if (fader.gameObject.scene.name == gameObject.scene.name)
                {
                    poiFader = fader;
                    break;
                }
            }

            cardOffsetTransform = transform.parent.parent.parent;

            // Scale card/magic window based on platform
            CardObject.transform.localScale *= GalaxyExplorerManager.MagicWindowScaleFactor;
        }

        private void LateUpdate()
        {
            // If the card of this poi is open, then override the card's and descriptions's position and rotation so these are moved with the rotation animation
            if (CardObject && CardObject.gameObject.activeSelf)
            {
                CardObject.transform.rotation = cardRotation;
                CardObject.transform.position = cardOffsetTransform.position - cardOffset;

                // Card description needs to keep the same distance from the card
            }
        }

        protected override void UpdateState()
        {
            switch (currentState)
            {
                case POIState.kOnFocusExit:
                    timer += Time.deltaTime;

                    if (timer >= restingOnPoiTime)
                    {
                        timer = 0.0f;

                    }

                    break;
            }
        }

        public override void OnPointerDown(GEPointerEventData eventData)
        {
            if (isCoolingDown)
            {
                return;
            }
            base.OnPointerDown(eventData);
            if (CardObject)
            {
                if (!CardObject.gameObject.activeSelf)
                {
                    isCardActive = true;
                    StartCoroutine(GalaxyExplorerManager.Instance.GeFadeManager.FadeContent(poiFader, GEFadeManager.FadeType.FadeOut, GalaxyExplorerManager.Instance.CardPoiManager.POIFadeOutTime, GalaxyExplorerManager.Instance.CardPoiManager.POIOpacityCurve));

                    CardObject.gameObject.SetActive(true);

                    if (CardAnimator)
                    {
                        CardAnimator.SetBool("CardVisible", true);
                    }
                    CardObject.ShowContents();

                    if (CardAudio && GalaxyExplorerManager.Instance.VoManager)
                    {
                        GalaxyExplorerManager.Instance.VoManager.Stop(true);
                        GalaxyExplorerManager.Instance.VoManager.PlayClip(CardAudio);
                    }

                    if (LineBase)
                    {
                        CardObject.transform.position = LineBase.transform.position;
                    }
                    else
                    {
                        CardObject.transform.position = transform.position;
                    }

                    Vector3 forwardDirection = transform.position - Camera.main.transform.position;
                    CardObject.transform.rotation = Quaternion.LookRotation(forwardDirection.normalized, Camera.main.transform.up);
                    cardRotation = CardObject.transform.rotation;

                    cardOffset = cardOffsetTransform.position - CardObject.transform.position;

                    audioService.PlayClip(AudioId.CardSelect);
                }
                else
                {
                    isCardActive = false;

                    StartCoroutine(GalaxyExplorerManager.Instance.GeFadeManager.FadeContent(poiFader, GEFadeManager.FadeType.FadeIn, GalaxyExplorerManager.Instance.CardPoiManager.POIFadeOutTime, GalaxyExplorerManager.Instance.CardPoiManager.POIOpacityCurve));

                    CardObject.HideContents();
                    // TODO this need to be removed and happen in the animation, but it doesnt
                    CardObject.gameObject.SetActive(false);

                    if (CardAnimator)
                    {
                        CardAnimator.SetBool("CardVisible", false);
                    }

                    if (GalaxyExplorerManager.Instance.VoManager)
                    {
                        GalaxyExplorerManager.Instance.VoManager.Stop(true);
                    }

                    audioService?.PlayClip(AudioId.CardDeselect);
                }
            }
        }
        
        public void CloseAnyOpenCard()
        {
            GalaxyExplorerManager.Instance.CardPoiManager.CloseAnyOpenCard();
            GalaxyExplorerManager.Instance.CardPoiManager.OnPointerDown(null);
        }
    }
}