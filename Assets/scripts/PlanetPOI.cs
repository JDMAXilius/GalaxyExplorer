// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using HoloToolkit.Unity.InputModule;

using System.Collections;
using CosmicSimulation;
using UnityEngine;
using GalaxyExplorer.XR;

/// <summary>
/// Its attached to a poi if the poi is supposed to load a new planet scene when selected.
///
/// On the Milky Way map (CS-170) the two large markers - Solar System, Galactic Center - travel through
/// <see cref="ExperienceDirector.Switch(ExperienceModule)"/> like a dock tile, so the director's own bookkeeping
/// (room, panel, narration, dock selection) follows the click. The original TransitionManager path is kept for
/// any marker that has no module: a director-less scene, or a scene name no module claims.
/// </summary>
namespace GalaxyExplorer
{
    public class PlanetPOI : PointOfInterest
    {
        [SerializeField]
        private string SceneToLoad = "";

        [SerializeField]
        private GameObject Planet = null;

        [SerializeField]
        [Tooltip("Where a click on this marker goes, opened through ExperienceDirector.Switch. Left empty, " +
                 "the module whose SceneName equals SceneToLoad is used; with no director at all, " +
                 "TransitionManager loads SceneToLoad the original way.")]
        private ExperienceModule destination = null;

        public string GetSceneToLoad
        {
            get { return SceneToLoad; }
        }

        public GameObject PlanetObject
        {
            get { return Planet; }
        }

        protected override void Start()
        {
            base.Start();

            Collider[] allPlanetCollders = (Planet) ? Planet.GetComponentsInChildren<Collider>() : new Collider[]{};
            foreach (var item in allPlanetCollders)
            {
                allPoiColliders.Add(item);
            }
        }

        /// <summary>The module a click on this marker opens, or null when the original scene path applies.</summary>
        public ExperienceModule Destination
        {
            get
            {
                if (destination != null)
                {
                    return destination;
                }

                var director = ExperienceDirector.Instance;
                if (director == null || string.IsNullOrEmpty(SceneToLoad))
                {
                    return null;
                }

                foreach (var module in director.Modules)
                {
                    if (module != null && module.SceneName == SceneToLoad)
                    {
                        return module;
                    }
                }

                return null;
            }
        }

        public override void OnPointerDown(GEPointerEventData eventData)
        {
            if (isCoolingDown)
            {
                return;
            }

            base.OnPointerDown(eventData);

            var module = Destination;
            var director = ExperienceDirector.Instance;
            if (module != null && director != null)
            {
                // Not isCardActive: that would freeze the galaxy's spin and switch every marker's collider off,
                // which is the wrong answer when Switch refuses (intro still running, a switch in flight) and
                // the map stays on screen. Switch answers those refusals itself, through SwitchNotice.
                director.Switch(module);
                return;
            }

            StartCoroutine(OnPointerDownRoutine());
        }

        private IEnumerator OnPointerDownRoutine()
        {
            isCardActive = true;
            yield return StartCoroutine(GalaxyExplorerManager.Instance.CardPoiManager.UpdateActivationOfPOIColliders(false));

            yield return new WaitForSeconds(.3f);
            GalaxyExplorerManager.Instance.TransitionManager.LoadNextScene(SceneToLoad);
            var poiBehaviors = FindObjectsOfType<POIBehavior>();
            if (poiBehaviors != null)
            {
                foreach (var poiBehavior in poiBehaviors)
                {
                    poiBehavior.enabled = false;
                }
            }
        }
    }
}