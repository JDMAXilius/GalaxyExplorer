// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MRS.FlowManager;
using System.Collections;
using UnityEngine;

namespace GalaxyExplorer
{
    public class IntroFlow : MonoBehaviour
    {
        /// <summary>
        /// Editor session key holding the view to open directly, set when Play is pressed inside a view scene
        /// (see PlayFromViewScene).
        /// </summary>
        public const string QuickStartViewKey = "GalaxyExplorer.QuickStartView";
        private const string GalaxyViewScene = "galaxy_view_scene";

        [SerializeField]
        [Tooltip("Duration of Logo stage")]
        private float LogoDuration = 5.0f;


        private IntroFlowState currentState = IntroFlowState.kNone;
        private float timer = 0.0f;

        private FlowManager flowManagerScript = null;
        private ViewLoader viewLoaderScript = null;
        private IAudioService audioService;

        private const string WelcomeSnapShot = "01_Welcome";
        private const float TransitionTime = 3.8f;

        public delegate void IntroFinishedCallback();
        public IntroFinishedCallback OnIntroFinished;


        public enum IntroFlowState
        {
            kLogo,              // Logo
            kEarthPinMR,        // Earth pin stage in Desktop platform
            kEarthPinDesktop,   // Earth pin stage in MR platform
            kSolarView,         // Spawn solar system
            kGalaxyView,        // Spawn galaxy view
            kNone
        }

        public void OnStageTransition(int timedstage)
        {
            currentState = (IntroFlowState)timedstage;

            // Intro has finished
            if (currentState == IntroFlowState.kGalaxyView && OnIntroFinished != null)
            {
                OnIntroFinished.Invoke();
            }
        }

        public void OnSceneIsLoaded()
        {
            StartCoroutine(Initialization());
        }

        // Position and rotate transform along the WorldAnchor which is the transform that ViewLoader lives
        // After modifying transform, create world anchor
        public void OnPlacementFinished(Vector3 position)
        {
            // Anchor the content in place
            FindObjectOfType<WorldAnchorHandler>().CreateWorldAnchor(position);

            // if its not Desktop platform then skip the next stage and go directly to solar system stage
            if (GalaxyExplorerManager.IsDesktop && flowManagerScript)
            {
                flowManagerScript.AdvanceStage();
            }
            else if (!GalaxyExplorerManager.IsDesktop && flowManagerScript)
            {
                flowManagerScript.JumpToStage(3);
            }
        }

        void Start()
        {
            StartCoroutine(Initialization());
        }

        private void Update()
        {
            switch (currentState)
            {
                case IntroFlowState.kLogo:
                    timer += Time.deltaTime;

                    if (timer >= LogoDuration)
                    {
                        // if its Desktop platform then jump to earth pin desktop stage
                        if (GalaxyExplorerManager.IsDesktop && flowManagerScript)
                        {
                            flowManagerScript.JumpToStage(2);
                        }
                        else if (!GalaxyExplorerManager.IsDesktop && flowManagerScript)
                        {
                            flowManagerScript.AdvanceStage();
                        }
                    }

                    break;
            }
        }

        private IEnumerator Initialization()
        {
            // ViewLoader of CoreSystems scene needs to be loaded and then continue
            yield return new WaitUntil(() => FindObjectOfType<ViewLoader>() != null);

            // need to wait otherwise the viewloader subscription to callback becomes null in holoLens
            //yield return new WaitForSeconds(1);
            yield return new WaitForEndOfFrame();
            
            audioService = AudioService.Instance;

            PlacementControl placement = FindObjectOfType<PlacementControl>();
            if (placement)
            {
                placement.OnContentPlaced += OnPlacementFinished;
            }

            if (viewLoaderScript == null)
            {
                viewLoaderScript = GalaxyExplorerManager.Instance.ViewLoaderScript;
                if (viewLoaderScript)
                {
                    viewLoaderScript.OnSceneIsLoaded += OnSceneIsLoaded;
                }
            }

            if (flowManagerScript == null)
            {
                flowManagerScript = GalaxyExplorerManager.Instance.FlowManagerHandler;
                if (flowManagerScript)
                {
                    flowManagerScript.OnStageTransition += OnStageTransition;

                    // The flow manager runs the intro stages; a quick start skips them.
                    if (!TryQuickStart())
                    {
                        flowManagerScript.enabled = true;
                    }
                }
            }

            StartCoroutine(PlayWelcomeMusic());

            yield return null;
        }

        // Editor only: open the view the developer pressed Play in, skipping the logo and placement.
        private bool TryQuickStart()
        {
#if UNITY_EDITOR
            var view = UnityEditor.SessionState.GetString(QuickStartViewKey, string.Empty);
            if (string.IsNullOrEmpty(view))
            {
                return false;
            }
            UnityEditor.SessionState.EraseString(QuickStartViewKey);

            // Anchor the content where the intro would have: 2 m in front of the user, a bit lower on a headset.
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var position = head.position + (forward == Vector3.zero ? Vector3.forward : forward) * 2f;
            if (!GalaxyExplorerManager.IsDesktop)
            {
                position += Vector3.down * .5f;
            }
            FindObjectOfType<WorldAnchorHandler>().CreateWorldAnchor(position);

            // The solar system and the galactic center are reached from the galaxy, so Back should lead there.
            if (view != GalaxyViewScene)
            {
                GalaxyExplorerManager.Instance.ViewLoaderScript.AddToBackStack(GalaxyViewScene);
            }

            GalaxyExplorerManager.Instance.TransitionManager.OnIntroFinished();
            GalaxyExplorerManager.Instance.TransitionManager.LoadNextScene(view);
            OnIntroFinished?.Invoke();
            Debug.Log($"[IntroFlow] Quick start: skipped the intro and opened {view}");
            return true;
#else
            return false;
#endif
        }

        private IEnumerator PlayWelcomeMusic()
        {
            yield return new WaitForEndOfFrame();

            audioService.TryTransitionMixerSnapshot(WelcomeSnapShot, TransitionTime);

            yield return null;
        }
    }
}
