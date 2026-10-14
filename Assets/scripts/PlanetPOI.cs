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

        /// <summary>Set by UI 2.0: asked before a marker travels. Returning true means a preview was shown instead.</summary>
        public static System.Func<ExperienceModule, bool> Preview { get; set; }

        /// <summary>
        /// Set by UI 2.0: asked with each of <see cref="BodyIds"/> before a body marker with no destination (the
        /// Solar System's orbit view) starts the original scene load. Returning true means the click was handled
        /// on the card, and nothing travels (CC-U2-003).
        /// </summary>
        public static System.Func<string, bool> Picked { get; set; }

        /// <summary>
        /// The ids this marker may stand for, to match against <see cref="BodyInfo.Id"/>, most reliable first: the
        /// marker's own prefab name ("poi_mars_prefab"), then its original scene name without "_view_scene". The
        /// scene names are not uniform (Mercury's is "MercuryViewmercury_view_scene"), so neither alone will do.
        /// </summary>
        public System.Collections.Generic.IEnumerable<string> BodyIds
        {
            get
            {
                for (var t = transform; t != null; t = t.parent)
                {
                    var name = t.name;
                    if (name.StartsWith("poi_") && name.EndsWith("_prefab") && name.Length > "poi__prefab".Length)
                    {
                        yield return name.Substring(4, name.Length - 4 - "_prefab".Length);
                        break;
                    }
                }

                const string sceneSuffix = "_view_scene";
                if (!string.IsNullOrEmpty(SceneToLoad) && SceneToLoad.EndsWith(sceneSuffix))
                {
                    yield return SceneToLoad.Substring(0, SceneToLoad.Length - sceneSuffix.Length);
                }
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
                // UI 2.0 shows a preview card first; the card's own button does the travelling.
                if (Preview != null && Preview(module))
                {
                    return;
                }

                // Not isCardActive: that would freeze the galaxy's spin and switch every marker's collider off,
                // which is the wrong answer when Switch refuses (intro still running, a switch in flight) and
                // the map stays on screen. Switch answers those refusals itself, through SwitchNotice.
                director.Switch(module);
                return;
            }

            if (Picked != null)
            {
                foreach (var id in BodyIds)
                {
                    if (Picked(id))
                    {
                        return;
                    }
                }
            }

            StartCoroutine(OnPointerDownRoutine());
        }

        private IEnumerator OnPointerDownRoutine()
        {
            isCardActive = true;
            yield return StartCoroutine(GalaxyExplorerManager.Instance.CardPoiManager.UpdateActivationOfPOIColliders(false));

            yield return new WaitForSeconds(.3f);
            GalaxyExplorerManager.Instance.TransitionManager.LoadNextScene(SceneToLoad);
            var poiBehaviors = FindObjectsByType<POIBehavior>(FindObjectsSortMode.InstanceID);
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