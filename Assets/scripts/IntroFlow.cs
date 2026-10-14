// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MRS.FlowManager;
using System.Collections;
using UnityEngine;

namespace GalaxyExplorer
{
    public class IntroFlow : MonoBehaviour
    {
        /// <summary>The intro, once it has woken; null when there is none.</summary>
        public static IntroFlow Active { get; private set; }

        /// <summary>Editor session key for the view to open directly; set by PlayFromViewScene.</summary>
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

        /// <summary>True when the editor quick start skipped the intro and opened a view directly.</summary>
        public bool QuickStarted { get; private set; }

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

        /// <summary>
        /// Ends the intro as if it had reached its last stage, so OnIntroFinished and everything downstream still run.
        /// A player asking for a place (dock tile, destination tag) is done watching; refusing the click felt dead.
        /// </summary>
        public bool Skip()
        {
            if (flowManagerScript == null || currentState == IntroFlowState.kGalaxyView)
            {
                return false;
            }

            // Skipping placement would leave the content root at the world origin, on top of the camera.
            if (!_anchored)
            {
                AnchorAhead();
            }

            flowManagerScript.JumpToStage((int)IntroFlowState.kGalaxyView);
            return true;
        }

        private bool _anchored;

        /// <summary>Ends the logo stage now instead of at the end of its timer (GDD 2.1: pinch the logo to skip).</summary>
        public void SkipLogo()
        {
            if (currentState == IntroFlowState.kLogo)
            {
                timer = LogoDuration;
            }
        }

        /// <summary>Anchors the content where the intro would have: 2 m in front of the user, a bit lower on a headset.</summary>
        private void AnchorAhead()
        {
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            var position = head.position + (forward == Vector3.zero ? Vector3.forward : forward) * 2f;
            if (!GalaxyExplorerManager.IsDesktop)
            {
                position += Vector3.down * .5f;
            }
            FindFirstObjectByType<WorldAnchorHandler>().CreateWorldAnchor(position);
            _anchored = true;
        }

        public void OnSceneIsLoaded()
        {
            StartCoroutine(Initialization());
        }

        public void OnPlacementFinished(Vector3 position)
        {
            FindFirstObjectByType<WorldAnchorHandler>().CreateWorldAnchor(position);
            _anchored = true;

            // A headset skips stage 2, the desktop-only Earth pin, and goes straight to the solar system.
            if (GalaxyExplorerManager.IsDesktop && flowManagerScript)
            {
                flowManagerScript.AdvanceStage();
            }
            else if (!GalaxyExplorerManager.IsDesktop && flowManagerScript)
            {
                flowManagerScript.JumpToStage(3);
            }
        }

        private void Awake() => Active = this;

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
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
                        // Stage 2 is the desktop Earth pin; a headset advances to the MR one.
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
            yield return new WaitUntil(() => FindFirstObjectByType<ViewLoader>() != null);

            // Subscribing to ViewLoader on the frame it appears dropped the callback on HoloLens; wait a frame.
            yield return new WaitForEndOfFrame();
            
            audioService = AudioService.Instance;

            PlacementControl placement = FindFirstObjectByType<PlacementControl>();
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
            QuickStarted = true;
            AnchorAhead();

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
