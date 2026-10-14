// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Runs a change of scene: fade, zoom in and out, load and unload.</summary>
namespace GalaxyExplorer
{
    public class TransitionManager : MonoBehaviour
    {
        [Header("Scene Transitions")]
        [Tooltip("The first time the galaxy appears, this defines how the scene moves into position.")]
        public AnimationCurve IntroTransitionCurveContentChange;

        [Tooltip("The curve that defines how content moves when transitioning from the galaxy to the solar system scene.")]
        public AnimationCurve GalaxyToSSTransitionCurveContentChange;

        [Tooltip("The curve that defines how content moves when transitioning from the solar system to the galaxy.")]
        public AnimationCurve SSToGalaxyTransitionCurveContentChange;

        [Tooltip("The EXTRA position curve that defines how content moves when transitioning from the solar system to the galaxy and galactic center to galaxy.")]
        public AnimationCurve BackToGalaxyPositionTransitionCurveContentChange;

        [Tooltip("The curve that defines how content moves when transitioning from the solar system to a planet or the sun.")]
        public AnimationCurve SSToPlanetTransitionCurveContentChange;

        [Tooltip("The curve that defines how content moves (position and scale only) when transitioning from a planet or the sun to the solar system.")]
        public AnimationCurve PlanetToSSPositionScaleCurveContentChange;

        [Tooltip("The curve that defines how content moves (rotation only) when transitioning from a planet or the sun to the solar system.")]
        public AnimationCurve PlanetToSSRotationCurveContentChange;

        [Tooltip("The curve that defines how content scales when transitioning from a planet or the sun to the solar system.")]
        public AnimationCurve PlanetToSSScaleCurveContentChange;

        [Tooltip("The curve that defines how content moves when transitioning from a planet to Galactic Center.")]
        public AnimationCurve PlanetToGCPositionCurveContentChange;

        [Tooltip("The curve that defines how content rotates when transitioning from a planet to Galactic Center.")]
        public AnimationCurve PlanetToGCRotationCurveContentChange;

        [Header("OpeningScene")]
        [Tooltip("The time it takes to fully transition from one scene opening and getting into position at the center of the cube or room.")]
        public float TransitionTimeOpeningScene = 3.0f;

        [Tooltip("Drives the opacity of the new scene that was loaded in when transitioning backwards.")]
        public AnimationCurve OpacityCurveEnteringScene;

        [Tooltip("Drives the opacity animation for the next scene that is entering.")]
        public AnimationCurve PlanetToSSTransitionOpacityCurveContentChange;

        [Header("Closing Scene")]
        [Tooltip("How long it takes to completely fade the galaxy scene when transitioning from this scene.")]
        public float GalaxyVisibilityTimeClosingScene = 1.0f;

        [Tooltip("How long it takes to completely fade the solar system scene when transitioning from this scene.")]
        public float SolarSystemVisibilityTimeClosingScene = 1.0f;

        [Tooltip("How long it takes to completely fade a planet or sun scene when transitioning from this scene.")]
        public float PlanetVisibilityTimeClosingScene = 1.0f;

        [Tooltip("Drives the opacity animation for the scene that is closing.")]
        public AnimationCurve OpacityCurveClosingScene;

        [Header("Points Of Interest")]
        [Tooltip("The time it takes for one point of interest to completely fade out.")]
        public float PoiFadeOutDuration = 1.0f;

        [Tooltip("Drives the POI opacity animation for the closing scene.")]
        public AnimationCurve POIOpacityCurveStartTransition;

        [Tooltip("The time it takes for one point of interest to completely fade in.")]
        public float PoiFadeInDuration = 2.0f;

        [Tooltip("Drives the POI opacity animation for the opening scene after it has completely moved into place.")]
        public AnimationCurve POIOpacityCurveEndTransition;

        [Header("Camera Transition")]
        [SerializeField]
        [Tooltip("Transition duration during camera reset.")]
        private float TransitionTimeCamera = 3.0f;

        [Tooltip("Transition curve of camera reset movement.")]
        public AnimationCurve TransitionTimeCameraCurve;

        [Header("Audio Transitions")]
        [SerializeField]
        [Tooltip("AudioSource used in scene transitions for static sound effect.")]
        private AudioSource TransitionAudioSource = null;

        [SerializeField]
        [Tooltip("Movable AudioSource used in scene transitions for moving sound effect.")]
        private AudioSource MovableAudioSource = null;

        [SerializeField]
        private AudioTransition SolarSystemClips = new AudioTransition();

        [SerializeField]
        private AudioTransition PlanetClips = new AudioTransition();

        [SerializeField]
        private AudioTransition BackClips = new AudioTransition();

        [SerializeField]
        private AudioTransition IntroClips = new AudioTransition();

        [Serializable]
        public struct AudioTransition
        {
            public AudioClip StaticClip;
            public AudioClip MovingClip;

            public AudioTransition(AudioClip staticClip, AudioClip movingClip)
            {
                StaticClip = staticClip;
                MovingClip = movingClip;
            }
        }

        private MovableAudioSource movingAudio = null;
        private ZoomInOut ZoomInOutBehaviour = null;
        private GameObject prevSceneLoaded;     // tracks the last scene loaded for transitions when loading new scenes
        private string prevSceneLoadedName = "";

        private bool inTransition = false;
        private bool inForwardTransition = true;
        private bool isFading = false;
        private IntroStage introStage = IntroStage.kInactiveIntro;

        private TransformSource transformSource = null;

        private Quaternion defaultSceneRotation = Quaternion.identity;
        private Vector3 defaultSceneScale = Vector3.one;

        [HideInInspector]
        public UnityEvent OnResetMRSceneToOriginComplete;

        private enum IntroStage
        {
            kActiveIntro,
            kLastStageIntro,
            kInactiveIntro
        }

        public GameObject CurrentActiveScene
        {
            get; set;
        }

        public bool InTransition
        {
            get { return inTransition; }
        }

        public bool IsInIntroFlow
        {
            get { return (introStage != IntroStage.kInactiveIntro && introStage != IntroStage.kLastStageIntro); }
        }

        private void Start()
        {
            if (GalaxyExplorerManager.Instance.ViewLoaderScript == null)
            {
                Debug.LogError("TransitionManager: No ViewLoader found - unable to process transitions.");
                return;
            }

            GalaxyExplorerManager.Instance.GeFadeManager.OnFadeComplete += OnFadeComplete;

            ZoomInOutBehaviour = FindFirstObjectByType<ZoomInOut>();
            movingAudio = FindFirstObjectByType<MovableAudioSource>();
            transformSource = FindFirstObjectByType<TransformSource>();

            defaultSceneRotation = transformSource.transform.rotation;
            defaultSceneScale = transformSource.transform.localScale;
        }

        // Wired up on FlowManager in the editor, so no code reference finds the caller.
        public void OnIntroStarted()
        {
            introStage = IntroStage.kActiveIntro;
        }

        // Wired up on FlowManager in the editor.
        public void OnIntroFinished()
        {
            introStage = IntroStage.kLastStageIntro;
        }

        private void OnFadeComplete(GEFadeManager.FadeType fadeType)
        {
            isFading = false;
        }

        public void UnloadScene(string scene, bool keepItOnStack)
        {
            GalaxyExplorerManager.Instance.ViewLoaderScript.UnLoadView(scene, keepItOnStack);
        }

        public void LoadPrevScene()
        {
            if (!GalaxyExplorerManager.Instance.ViewLoaderScript.IsTherePreviousScene())
            {
                Debug.LogWarning("TransitionManager: There is NO previous scene to go back to.");
                return;
            }

            if (inTransition)
            {
                Debug.LogWarning("TransitionManager: Currently in a transition and cannot change view to new scene until current transition completes.");
                return;
            }

            inTransition = true;
            inForwardTransition = false;
            prevSceneLoaded = FindContent();
            prevSceneLoadedName = (prevSceneLoaded) ? prevSceneLoaded.name : "";
            CurrentActiveScene = null;

            GalaxyExplorerManager.Instance.ViewLoaderScript.PopSceneFromStack();
            GalaxyExplorerManager.Instance.ViewLoaderScript.LoadPreviousScene(PrevSceneLoaded);
        }

        private void PrevSceneLoaded()
        {
            StartCoroutine(NextSceneLoadedCoroutine());
        }

        public void LoadNextScene(string sceneName)
        {
            LoadNextScene(sceneName, true);
        }

        // Intro scenes are not kept on the back stack.
        public void LoadNextIntroScene(string sceneName)
        {
            LoadNextScene(sceneName, false);
        }

        public void LoadNextScene(string sceneName, bool keepOnStack)
        {
            if (inTransition)
            {
                Debug.LogWarning("TransitionManager: Currently in a transition and cannot change view to '" + sceneName + "' until current transition completes.");
                return;
            }

            inTransition = true;
            inForwardTransition = true;
            prevSceneLoaded = FindContent();
            prevSceneLoadedName = (prevSceneLoaded) ? prevSceneLoaded.name : "";
            CurrentActiveScene = null;

            GalaxyExplorerManager.Instance.ViewLoaderScript.LoadViewAsync(sceneName, NextSceneLoaded);

            if (!keepOnStack)
            {
                GalaxyExplorerManager.Instance.ViewLoaderScript.PopSceneFromStack();
            }
        }

        private void NextSceneLoaded()
        {
            StartCoroutine(NextSceneLoadedCoroutine());
        }

        // The root of the newest scene: any TransformHandler that is not the previous scene's.
        private GameObject FindContent()
        {
            TransformHandler[] parentContent = FindObjectsByType<TransformHandler>(FindObjectsSortMode.None);
            foreach (var parent in parentContent)
            {
                if (parent.gameObject != prevSceneLoaded)
                {
                    return parent.gameObject;
                }
            }

            // No TransformHandler means initial placement mode, whose root carries PlacementControl instead.
            PlacementControl placement = FindFirstObjectByType<PlacementControl>();
            return placement?.gameObject;
        }

        private void OnUnloadComplete()
        {
            Debug.Log("All unload functionality has ended");
        }

        /// <summary>Runs the whole transition once the next scene has loaded, forwards or back.</summary>
        private IEnumerator NextSceneLoadedCoroutine()
        {
            GameObject nextSceneContent = FindContent();
            ZoomInOutBehaviour.ZoomInIsDone = false;
            ZoomInOutBehaviour.ZoomOutIsDone = false;

            SceneTransition previousTransition = (prevSceneLoaded) ? prevSceneLoaded.GetComponentInChildren<SceneTransition>() : null;
            SceneTransition newTransition = nextSceneContent.GetComponentInChildren<SceneTransition>();
            CurrentActiveScene = nextSceneContent;

            SetActivationOfTouchscript(false);
            DeactivateOrbitUpdater(newTransition, previousTransition, false);
            SetActivePOIRotationAnimator(false, previousTransition, newTransition);
            UpdateActivationOfPOIs(newTransition, false);
            UpdateActivationOfPOIs(previousTransition, false);

            // Scale new scene to fit inside the volume
            float scaleToFill = transformSource.transform.lossyScale.x;
            float targetSize = newTransition.GetScalar(scaleToFill);
            newTransition.transform.GetChild(0).localScale = Vector3.one * targetSize;

            StartCoroutine(ZoomInOutBehaviour.ZoomInOutInitialization(nextSceneContent, prevSceneLoaded));

            // Hidden at zero scale with colliders off, so it is neither seen nor hit while the previous scene fades.
            if (ZoomInOutBehaviour.GetNextScene)
            {
                ZoomInOutBehaviour.GetNextScene.transform.localScale = Vector3.zero;
                SetCollidersActivation(ZoomInOutBehaviour.GetNextScene.GetComponentsInChildren<Collider>(), false);
            }

            if (previousTransition)
            {
                SetCollidersActivation(previousTransition.GetComponentsInChildren<Collider>(), false);
            }

            yield return new WaitForEndOfFrame();

            StartCoroutine(ZoomInOutSimultaneouslyFlow(previousTransition, newTransition));

            while (!ZoomInOutBehaviour.ZoomOutIsDone)
            {
                yield return null;
            }

            DeactivateOrbitUpdater(newTransition, previousTransition, true);
            UpdateActivationOfPOIs(newTransition, true);

            if (prevSceneLoaded != null)
            {
                UnloadScene(prevSceneLoaded.scene.name, true);
            }

            while (!ZoomInOutBehaviour.ZoomInIsDone)
            {
                yield return null;
            }

            SetActivePOIRotationAnimator(true, previousTransition, newTransition);

            if (introStage != IntroStage.kActiveIntro)
            {
                isFading = true;
                GalaxyExplorerManager.Instance.GeFadeManager.Fade(newTransition.GetComponentInChildren<POIMaterialsFader>(), GEFadeManager.FadeType.FadeIn, PoiFadeInDuration, POIOpacityCurveEndTransition);
            }

            while (isFading)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            
            while (GalaxyExplorerManager.Instance.VoManager.ShouldAudioBlockProgress)
            {
                yield return null;
            }

            if (ZoomInOutBehaviour.GetNextScene && introStage != IntroStage.kActiveIntro)
            {
                SetCollidersActivation(ZoomInOutBehaviour.GetNextScene.GetComponentsInChildren<Collider>(), true);
            }

            SetActivationOfTouchscript(true);

            inTransition = false;
            introStage = (introStage == IntroStage.kLastStageIntro) ? IntroStage.kInactiveIntro : introStage;

            yield return null;
        }

        // Both scenes zoom at once, e.g. into or out of a planet.
        private IEnumerator ZoomInOutSimultaneouslyFlow(SceneTransition previousTransition, SceneTransition newTransition)
        {
            GameObject singlePlanet = null;
            GameObject relatedPlanet = null;
            GetRelatedPlanets(out relatedPlanet, out singlePlanet);

            if (previousTransition)
            {
                if (introStage == IntroStage.kInactiveIntro)
                {
                    isFading = true;
                    GalaxyExplorerManager.Instance.GeFadeManager.Fade(previousTransition.GetComponentInChildren<POIMaterialsFader>(), GEFadeManager.FadeType.FadeOut, PoiFadeOutDuration, POIOpacityCurveStartTransition);
                    while (isFading)
                    {
                        yield return null;
                    }
                }

                isFading = true;
                float fadeTime = GetClosingSceneVisibilityTime();
                GalaxyExplorerManager.Instance.GeFadeManager.FadeExcept(previousTransition.GetComponentsInChildren<Fader>(), typeof(POIMaterialsFader), null, GEFadeManager.FadeType.FadeOut, fadeTime, OpacityCurveClosingScene);
            }

            PlayTransitionAudio(newTransition.transform, inForwardTransition);

            // The same body exists in both scenes; hide one copy.
            if (relatedPlanet && singlePlanet)
            {
                SetRenderersVisibility(newTransition.IsSinglePlanetTransition ? relatedPlanet.transform.parent.gameObject : singlePlanet.transform.parent.gameObject, false);
            }

            GalaxyExplorerManager.Instance.GeFadeManager.SetAlphaOnFader(newTransition.GetComponentInChildren<POIMaterialsFader>(), 0.0f);
            var faderToExclude = default(Fader);

            // Back from a planet: fade the solar system in, except the planet we came from and the POIs.
            if (previousTransition && previousTransition.IsSinglePlanetTransition)
            {
                Fader[] allFaders = newTransition.GetComponentsInChildren<Fader>();
                GalaxyExplorerManager.Instance.GeFadeManager.SetAlphaOnFaderExcept(allFaders, typeof(POIMaterialsFader), 0.0f);

                if (relatedPlanet)
                    GalaxyExplorerManager.Instance.GeFadeManager.SetAlphaOnFader(relatedPlanet.GetComponent<Fader>(), 1.0f);

                isFading = true;
                AnimationCurve opacityCurve = newTransition.gameObject.name.Contains("solar_system") ? PlanetToSSTransitionOpacityCurveContentChange : OpacityCurveEnteringScene;
                GalaxyExplorerManager.Instance.GeFadeManager.FadeExcept(allFaders, typeof(POIMaterialsFader), relatedPlanet, GEFadeManager.FadeType.FadeIn, TransitionTimeOpeningScene, opacityCurve);
            }
            else if ((previousTransition && !previousTransition.IsSinglePlanetTransition && newTransition && !newTransition.IsSinglePlanetTransition))
            {
                if (newTransition.gameObject.name.Contains("SolarSystem"))
                {
                    var sun = GameObject.Find("poi_sun_prefab");
                    var sunFader = sun.GetComponentInChildren<Fader>(true);
                    faderToExclude = sunFader;
                }
                Fader[] allFaders = newTransition.GetComponentsInChildren<Fader>();
                GalaxyExplorerManager.Instance.GeFadeManager.SetAlphaOnFaderExcept(allFaders, typeof(POIMaterialsFader), 0.0f);
                allFaders = allFaders.Where(f => f != faderToExclude).ToArray();

                isFading = true;
                GalaxyExplorerManager.Instance.GeFadeManager.FadeExcept(allFaders, typeof(POIMaterialsFader), null, GEFadeManager.FadeType.FadeIn, TransitionTimeOpeningScene, OpacityCurveEnteringScene);
                GalaxyExplorerManager.Instance.GeFadeManager.Fade(faderToExclude, GEFadeManager.FadeType.FadeIn, 1, POIOpacityCurveStartTransition, 3f);
            }

            if (newTransition.gameObject.scene.name.Contains("galaxy_view_scene"))
            {
                StartCoroutine(ZoomInOutBehaviour.ZoomInOutCoroutine(TransitionTimeOpeningScene, GetContentTransitionCurve(newTransition.gameObject.scene.name), GetContentRotationCurve(newTransition.gameObject.scene.name), GetContentTransitionCurve(newTransition.gameObject.scene.name), BackToGalaxyPositionTransitionCurveContentChange, offset:newTransition.scenePositionOffset));
            }
            else if (previousTransition && previousTransition.IsSinglePlanetTransition)
            {
                StartCoroutine(ZoomInOutBehaviour.ZoomInOutCoroutine(TransitionTimeOpeningScene, GetContentTransitionCurve(newTransition.gameObject.scene.name), GetContentRotationCurve(newTransition.gameObject.scene.name), PlanetToSSScaleCurveContentChange, offset:newTransition.scenePositionOffset));
            }
            else
            {
                StartCoroutine(ZoomInOutBehaviour.ZoomInOutCoroutine(TransitionTimeOpeningScene, GetContentTransitionCurve(newTransition.gameObject.scene.name), GetContentRotationCurve(newTransition.gameObject.scene.name), GetContentTransitionCurve(newTransition.gameObject.scene.name), offset:newTransition.scenePositionOffset));
            }

            StartCoroutine(LightTransitions(previousTransition, newTransition));

            yield return null;
        }

        // Carries the sun's direction across a simultaneous zoom so the lit side does not jump between scenes.
        private IEnumerator LightTransitions(SceneTransition previousTransition, SceneTransition newTransition)
        {
            if (previousTransition == null || newTransition == null)
            {
                yield break;
            }

            GameObject singlePlanet = null;
            GameObject relatedPlanet = null;
            GetRelatedPlanets(out relatedPlanet, out singlePlanet);

            // Into a planet: start its sun where the previous scene's was and ease it home.
            if (newTransition && newTransition.IsSinglePlanetTransition)
            {
                SunLightReceiver sunLight = newTransition.GetComponentInChildren<SunLightReceiver>();
                Transform previousSun = FindByName(previousTransition.transform, "Sun");
                Vector3 initialSunPosition = Vector3.zero;
                if (sunLight && sunLight.Sun && previousSun)
                {
                    initialSunPosition = sunLight.Sun.transform.localPosition;
                    sunLight.Sun.transform.position = singlePlanet.transform.position - (relatedPlanet.transform.position - previousSun.position);

                    float delta = 0.0f;
                    do
                    {
                        delta += Time.deltaTime / TransitionTimeOpeningScene;
                        delta = Mathf.Clamp(delta, 0.0f, 1.0f);
                        sunLight.Sun.transform.localPosition = Vector3.Lerp(sunLight.Sun.transform.localPosition, initialSunPosition, delta);
                        yield return null;
                    } while (delta < 1.0f);
                }
            }
            // Out of a planet: light the related planet from a stand-in sun that starts at the old scene's sun and
            // eases to the new one.
            else if (previousTransition && previousTransition.IsSinglePlanetTransition)
            {
                SunLightReceiver[] allLightReceivers = relatedPlanet.GetComponentsInChildren<SunLightReceiver>();
                Transform newSun = FindByName(newTransition.transform, "Sun");
                if (allLightReceivers.Length > 0 && newSun)
                {
                    GameObject lightPlaceholder = new GameObject();
                    lightPlaceholder.transform.parent = newTransition.ThisSceneObject.transform;

                    SunLightReceiver oldLight = previousTransition.GetComponentInChildren<SunLightReceiver>();
                    lightPlaceholder.transform.position = (oldLight && oldLight.Sun) ? oldLight.Sun.transform.position : Vector3.zero;

                    foreach (var light in allLightReceivers)
                    {
                        light.Sun = lightPlaceholder.transform;
                    }

                    float delta = 0.0f;
                    do
                    {
                        delta += Time.deltaTime / TransitionTimeOpeningScene;
                        delta = Mathf.Clamp(delta, 0.0f, 1.0f);
                        lightPlaceholder.transform.position = Vector3.Lerp(lightPlaceholder.transform.position, newSun.transform.position, delta);
                        yield return null;
                    } while (delta < 1.0f);

                    Destroy(lightPlaceholder);
                }
            }
        }

        // Depth-first; matches on a name that contains the given one.
        private Transform FindByName(Transform parent, string name)
        {
            Transform child = null;

            for (int i = 0; i < parent.childCount; ++i)
            {
                if (parent.GetChild(i).name.Contains(name))
                {
                    return parent.GetChild(i);
                }
                else if (parent.GetChild(i).childCount > 0)
                {
                    child = FindByName(parent.GetChild(i), name);
                    if (child)
                    {
                        return child;
                    }
                }
            }

            return child;
        }

        private void DeactivateOrbitUpdater(SceneTransition newTransition, SceneTransition previousTransition, bool isActive)
        {
            // Stop the related planet orbiting while it zooms into or out of its own scene.
            if (newTransition && newTransition.IsSinglePlanetTransition)
            {
                GameObject singlePlanet = null;
                GameObject relatedPlanet = null;
                GetRelatedPlanets(out relatedPlanet, out singlePlanet);

                if (relatedPlanet && relatedPlanet.GetComponentInChildren<OrbitUpdater>())
                {
                    relatedPlanet.GetComponentInChildren<OrbitUpdater>().enabled = isActive;
                }
                else if (relatedPlanet && relatedPlanet.transform.parent.GetComponent<OrbitUpdater>())
                {
                    relatedPlanet.transform.parent.GetComponent<OrbitUpdater>().enabled = isActive;
                }
            }
            else if (previousTransition && previousTransition.IsSinglePlanetTransition)
            {
                GameObject singlePlanet = null;
                GameObject relatedPlanet = null;
                GetRelatedPlanets(out relatedPlanet, out singlePlanet);

                if (relatedPlanet && relatedPlanet.GetComponentInChildren<OrbitUpdater>())
                {
                    relatedPlanet.GetComponentInChildren<OrbitUpdater>().enabled = isActive;
                }
                else if (relatedPlanet && relatedPlanet.transform.parent.GetComponent<OrbitUpdater>())
                {
                    relatedPlanet.transform.parent.GetComponent<OrbitUpdater>().enabled = isActive;
                }
            }
        }

        // The player cannot move, turn or scale the scene mid-transition on desktop.
        private void SetActivationOfTouchscript(bool enable)
        {
            if (GalaxyExplorerManager.IsDesktop && GalaxyExplorerManager.Instance.CameraControllerHandler != null)
            {
                GalaxyExplorerManager.Instance.CameraControllerHandler.enabled = enable;
            }

            if (GalaxyExplorer.XR.DesktopMouseInput.Instance != null)
            {
                GalaxyExplorer.XR.DesktopMouseInput.Instance.InputEnabled = enable;
            }
        }

        private void UpdateActivationOfPOIs(SceneTransition scene, bool isEnabled)
        {
            if (scene)
            {
                PointOfInterest[] allPOIS = scene.GetComponentsInChildren<PointOfInterest>();
                foreach (var item in allPOIS)
                {
                    item.enabled = isEnabled;
                }
            }
        }

        // The galaxy's POI rotation animator moves the POIs, so it is paused for the transition.
        private void SetActivePOIRotationAnimator(bool isActive, SceneTransition previousTransition, SceneTransition nextTransition)
        {
            if (previousTransition && previousTransition.gameObject.scene.name == "galaxy_view_scene")
            {
                Animator[] allAnimators = previousTransition.GetComponentsInChildren<Animator>();
                foreach (var animator in allAnimators)
                {
                    if (animator.runtimeAnimatorController && animator.runtimeAnimatorController.name.Contains("pois_root_rotation_animator"))
                    {
                        animator.enabled = isActive;
                        break;
                    }
                }
            }
            else if (nextTransition && nextTransition.gameObject.scene.name == "galaxy_view_scene")
            {
                Animator[] allAnimators = nextTransition.GetComponentsInChildren<Animator>();
                foreach (var animator in allAnimators)
                {
                    if (animator.runtimeAnimatorController && animator.runtimeAnimatorController.name.Contains("pois_root_rotation_animator"))
                    {
                        animator.enabled = isActive;
                        break;
                    }
                }
            }
        }

        private void SetCollidersActivation(Collider[] allColliders, bool areActive)
        {
            foreach (var collider in allColliders)
            {
                collider.enabled = areActive;
            }
        }

        private void SetRenderersVisibility(GameObject source, bool isVisible)
        {
            MeshRenderer[] allRenderers = source.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in allRenderers)
            {
                renderer.enabled = isVisible;
            }
        }

        private void GetRelatedPlanets(out GameObject relatedPlanetObject, out GameObject singlePlanetObject)
        {
            PlanetView planetView = FindFirstObjectByType<PlanetView>();

            singlePlanetObject = (planetView) ? planetView.gameObject : null;
            relatedPlanetObject = null;

            PlanetPOI[] allPlanets = FindObjectsByType<PlanetPOI>(FindObjectsSortMode.None);
            foreach (var planet in allPlanets)
            {
                if (planetView && (planetView.GetSceneName == planet.GetSceneToLoad))
                {
                    relatedPlanetObject = planet.PlanetObject;
                    break;
                }
            }
        }

        // For transitions with no single planet (galaxy to solar system or galactic centre): the POI that opened the
        // next scene, or, going back, the POI we return into.
        private void GetRelatedScenes(out GameObject previousRelatedPlanetObjectt, out GameObject nextRelatedPlanetObjectt, SceneTransition previousScene, SceneTransition newScene)
        {
            previousRelatedPlanetObjectt = null;
            nextRelatedPlanetObjectt = null;

            PlanetPOI[] allPreviousPlanets = previousScene.GetComponentsInChildren<PlanetPOI>();
            foreach (var planet in allPreviousPlanets)
            {
                if (planet && planet.GetSceneToLoad == newScene.gameObject.scene.name)
                {
                    previousRelatedPlanetObjectt = planet.PlanetObject;
                    break;
                }
            }

            PlanetPOI[] allNewPlanets = newScene.GetComponentsInChildren<PlanetPOI>();
            foreach (var planet in allNewPlanets)
            {
                if (planet && planet.GetSceneToLoad == previousScene.gameObject.scene.name)
                {
                    nextRelatedPlanetObjectt = planet.PlanetObject;
                    break;
                }
            }
        }

        private AnimationCurve GetContentTransitionCurve(string loadedSceneName)
        {
            if (prevSceneLoadedName.CompareTo("") == 0)
            {
                return IntroTransitionCurveContentChange;
            }

            if (loadedSceneName.Contains("galaxy_view_scene"))
            {
                return SSToGalaxyTransitionCurveContentChange;
            }
            else if (loadedSceneName.Contains("solar_system_view_scene"))
            {
                if (prevSceneLoadedName.Contains("galaxy"))
                {
                    return GalaxyToSSTransitionCurveContentChange;
                }
                else
                {
                    return PlanetToSSPositionScaleCurveContentChange;
                }
            }
            else if (loadedSceneName.Contains("galactic_center_view_scene"))
            {
                if (prevSceneLoadedName.Contains("galaxy"))
                {
                    return GalaxyToSSTransitionCurveContentChange;
                }
                else
                {
                    return PlanetToGCPositionCurveContentChange;
                }
            }

            return SSToPlanetTransitionCurveContentChange;
        }

        private AnimationCurve GetContentRotationCurve(string loadedSceneName)
        {
            if (prevSceneLoadedName.CompareTo("") == 0)
            {
                return IntroTransitionCurveContentChange;
            }

            if (loadedSceneName.Contains("galaxy"))
            {
                return SSToGalaxyTransitionCurveContentChange;
            }
            else if (loadedSceneName.Contains("solar_system_view_scene"))
            {
                if (prevSceneLoadedName.Contains("galaxy"))
                {
                    return GalaxyToSSTransitionCurveContentChange;
                }
                else
                {
                    return PlanetToSSRotationCurveContentChange;
                }
            }
            else if (loadedSceneName.Contains("galactic_center_view_scene"))
            {
                if (prevSceneLoadedName.Contains("galaxy"))
                {
                    return GalaxyToSSTransitionCurveContentChange;
                }
                else
                {
                    return PlanetToGCRotationCurveContentChange;
                }
            }

            return SSToPlanetTransitionCurveContentChange;
        }

        private float GetClosingSceneVisibilityTime()
        {
            if (prevSceneLoadedName.CompareTo("") == 0)
            {
                return 0.0f;
            }

            if (prevSceneLoadedName.Contains("galaxy"))
            {
                return GalaxyVisibilityTimeClosingScene;
            }

            if (prevSceneLoadedName.Contains("solar_system_scene"))
            {
                return SolarSystemVisibilityTimeClosingScene;
            }

            return PlanetVisibilityTimeClosingScene;
        }

        private void PlayTransitionAudio(Transform newContent, bool forwardNavigation = true)
        {
            AudioClip staticClip = null;
            AudioClip movingClip = null;

            if (newContent.gameObject.scene.name == "earth_view_scene")
            {
                return;
            }

            if (!forwardNavigation)
            {
                staticClip = BackClips.StaticClip;
                movingClip = BackClips.MovingClip;
            }
            else if (introStage == IntroStage.kInactiveIntro)
            {
                staticClip = IntroClips.StaticClip;
                movingClip = IntroClips.MovingClip;
            }
            else if (newContent.gameObject.scene.name == "solar_system_view_scene")
            {
                staticClip = SolarSystemClips.StaticClip;
                movingClip = SolarSystemClips.MovingClip;
            }
            else if (!IsInIntroFlow)
            {
                staticClip = PlanetClips.StaticClip;
                movingClip = PlanetClips.MovingClip;
            }

            if (TransitionAudioSource)
            {
                TransitionAudioSource.clip = staticClip;
                TransitionAudioSource.Play();
            }

            if (MovableAudioSource && movingAudio)
            {
                MovableAudioSource.clip = movingClip;
                movingAudio.Setup(newContent.position, Camera.main.transform.position);
                movingAudio.Activate();
            }
        }

        public void ResetMRSceneToOrigin()
        {
            StartCoroutine(ResetMRSceneToOriginCoroutine());
        }

        private IEnumerator ResetMRSceneToOriginCoroutine()
        {
            Vector3 startScale = transformSource.transform.localScale;
            Quaternion startRotation = transformSource.transform.rotation;

            float time = 0.0f;
            float timeFraction = 0.0f;
            do
            {
                time += Time.deltaTime;
                timeFraction = Mathf.Clamp01(time / TransitionTimeCamera);

                float delta = Mathf.Clamp01(TransitionTimeCameraCurve.Evaluate(timeFraction));

                transformSource.transform.localScale = Vector3.Lerp(startScale, defaultSceneScale, delta);
                transformSource.transform.rotation = Quaternion.Slerp(startRotation, defaultSceneRotation, delta);

                yield return null;
            } while (timeFraction < 1f);

            transformSource.transform.localScale = defaultSceneScale;
            transformSource.transform.rotation = defaultSceneRotation;

            OnResetMRSceneToOriginComplete?.Invoke();
        }
    }
}