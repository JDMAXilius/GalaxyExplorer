// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicSimulation
{
    /// <summary>
    /// Switches between places: opens a module's content (a scene via <see cref="ViewLoader"/> or a prefab under our
    /// own root), sets the environment, starts narration and grows it in. No drill-down and no Back.
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Dock tiles, in the order they appear.")]
        private ExperienceModule[] modules = Array.Empty<ExperienceModule>();

        [SerializeField]
        [Tooltip("Opened on launch, after the intro.")]
        private ExperienceModule startModule;

        [SerializeField]
        [Tooltip("Seconds a new experience takes to grow in from a point.")]
        private float growInSeconds = 0.6f;

        [SerializeField]
        [Tooltip("Seconds to wait for a scene before giving up on it and handing the room back to the player.")]
        private float loadTimeoutSeconds = 20f;

        [SerializeField]
        [Tooltip("Where a module's ContentPrefab is spawned. One is made at the origin when this is left empty.")]
        private Transform contentRoot;

        [Header("The open experience's own panel")]
        [SerializeField]
        [Tooltip("Assets/prefabs/ui/info_panel_prefab. Left empty, no experience gets a panel.")]
        private InfoPanel scenePanelPrefab;

        [SerializeField]
        [Tooltip("Where that panel parks, in metres, measured in the content root's frame — so it sits beside " +
                 "the experience wherever the room put it, not beside the world origin.")]
        private Vector3 scenePanelOffset = new Vector3(0.55f, 0.2f, 0f);

        [SerializeField]
        [Tooltip("Metres left between the edge of the content and the experience's own panel. The offset above " +
                 "is pushed out to clear wide content rather than landing inside it.")]
        private float panelClearanceMetres = 0.15f;

        [Header("Desktop framing")]
        [SerializeField]
        [Tooltip("On a monitor the camera never turns, so an arrangement (Solar Row, Relative Size) is pushed " +
                 "back until every body is inside the view. This is how much of the view's half-width and " +
                 "half-height a place may fill: 1 touches the edges, 0.95 leaves a sliver.")]
        [Range(0.5f, 1f)]
        private float desktopFitMargin = 0.95f;

        /// <summary>How long the intro's own last-stage load is given to appear before we stop waiting for one.</summary>
        private const float IntroSettleGrace = 0.5f;

        /// <summary>How long a view scene takes to settle into the fitted spot once its own zoom-in is over.</summary>
        private const float ViewSceneFitSeconds = 0.5f;

        private readonly List<GameObject> _destinationObjects = new List<GameObject>();
        private readonly HashSet<ExperienceModule> _warnedEmpty = new HashSet<ExperienceModule>();
        private GameObject _prefabContent;
        private InfoPanel _scenePanel;
        private InfoPanel _destinationPanel;
        private Transform _scenePanelAnchor;

        /// <summary>Half the framed prefab content's width in metres (zero for scenes), so the panel parks beside it.</summary>
        private float _contentSpanMetres;

        private bool _warnedNoPanelPrefab;
        private bool _ownsContentRoot;
        private LayoutRig _framedRig;
        private Coroutine _fitting;
        private Coroutine _switching;
        private bool _switchFinished;
        private readonly IntroGate _intro = new IntroGate();

        public static ExperienceDirector Instance { get; private set; }

        public IReadOnlyList<ExperienceModule> Modules => modules;

        public ExperienceModule Current { get; private set; }

        /// <summary>
        /// Raised once the new experience is loaded and growing in, and with <c>null</c> when a switch was abandoned.
        /// </summary>
        public static event Action<ExperienceModule> ExperienceChanged;

        /// <summary>The destination showing over the current place, or <c>null</c> when none is.</summary>
        public ExperienceModule OpenDestinationModule { get; private set; }

        /// <summary>True while a destination overlay is up over the current place.</summary>
        public bool HasOpenDestination => OpenDestinationModule != null;

        /// <summary>
        /// Raised when a destination opens, and with <c>null</c> when it closes. Separate from
        /// <see cref="ExperienceChanged"/> because a destination does not change <see cref="Current"/>.
        /// </summary>
        public static event Action<ExperienceModule> DestinationChanged;

        /// <summary>True while a switch is in flight; the dock ignores pokes during one.</summary>
        public bool IsSwitching => _switching != null;

        /// <summary>True while onboarding has the room; see <see cref="IntroGate.Running"/>.</summary>
        public bool IntroRunning => _intro.Running;

        /// <summary>True once onboarding has really ended or there is none; never times out.</summary>
        public bool IntroComplete => _intro.Complete;

        // The place a player asked for while the intro was still running. Opened once the intro lets go.
        private ExperienceModule _queuedFromIntro;

        private void Awake()
        {
            Instance = this;
            _intro.Finished += HandleIntroFinished;
            _intro.Bind();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _intro.Unbind();

            if (_ownsContentRoot && contentRoot != null)
            {
                Destroy(contentRoot.gameObject);
            }
        }

        public ExperienceModule Find(string id)
        {
            foreach (var module in modules)
            {
                if (module != null && module.Id == id)
                {
                    return module;
                }
            }

            return null;
        }

        // ---------- onboarding

        private void HandleIntroFinished()
        {
            StartCoroutine(OpenStartModuleWhenSettled());
        }

        /// <summary>
        /// Opens the first experience once the intro's own galaxy load has finished. FlowManager fires stage events
        /// from a delayed coroutine, so opening on the signal alone would race it and stack two galaxies.
        /// </summary>
        private IEnumerator OpenStartModuleWhenSettled()
        {
            var transitions = GalaxyExplorerManager.IsInitialized
                ? GalaxyExplorerManager.Instance.TransitionManager
                : null;

            var waited = 0f;
            while (transitions != null && !transitions.InTransition && waited < IntroSettleGrace)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // Bounded: a transition that never ends must not leave the player with no place open.
            waited = 0f;
            while (transitions != null && transitions.InTransition && waited < loadTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // The intro asks for the galaxy a frame or more after its signal; until then the current view is the
            // solar system it passed through, so wait for the start module's view (unless quick-started).
            if (_intro.Flow != null && !_intro.Flow.QuickStarted && startModule != null &&
                !string.IsNullOrEmpty(startModule.SceneName))
            {
                waited = 0f;
                while (ViewLoader.CurrentView != startModule.SceneName && waited < loadTimeoutSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // A place the player picked meanwhile wins over what the intro left on screen.
            while (IsSwitching)
            {
                yield return null;
            }

            if (_queuedFromIntro != null)
            {
                var wanted = _queuedFromIntro;
                _queuedFromIntro = null;
                Switch(wanted);
                yield break;
            }

            OpenStartModule();
        }

        /// <summary>
        /// Opens the start place once onboarding is over; a place of ours already on screen (from the intro or the
        /// quick start) is adopted rather than re-opened, so it is not cleared, regrown and narrated twice.
        /// </summary>
        public void OpenStartModule()
        {
            if (Current != null)
            {
                return;
            }

            var open = ModuleForOpenScene();
            if (open != null)
            {
                Adopt(open);
                return;
            }

            if (startModule != null)
            {
                Switch(startModule);
            }
        }

        /// <summary>Records a place already on screen as the open one, without moving any content.</summary>
        private void Adopt(ExperienceModule module)
        {
            Current = module;

            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(module.Environment);
            }

            // The panel and the ambient bed belong to the place, not the switch, so the adopted start module needs them too.
            ShowScenePanel(module);
            AmbienceController.PlayBed(module.Ambience);

            ExperienceChanged?.Invoke(module);
        }

        private ExperienceModule ModuleForOpenScene()
        {
            // The last view asked for wins, loaded or not: at handover the galaxy may still be loading while the
            // solar system is loaded, and adopting that would mislabel the open place.
            var asked = FindModuleByScene(ViewLoader.CurrentView);
            if (asked != null)
            {
                return asked;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var module = FindModuleByScene(scene.name);
                if (module != null)
                {
                    return module;
                }
            }

            return null;
        }

        private ExperienceModule FindModuleByScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            foreach (var module in modules)
            {
                if (module != null && module.SceneName == sceneName)
                {
                    return module;
                }
            }

            return null;
        }

        private GameObject _pivotSource;
        private CentreOnViewer _pivotCentred;
        private GalaxyField _pivotField;

        /// <summary>
        /// The desktop view's turn point (CS-172): the open content's centre, or the content root for a scene. False before anything is open.
        /// </summary>
        public bool TryGetViewPivot(out Vector3 pivot)
        {
            if (_prefabContent != null)
            {
                // Resolved once per place, not on every orbit-drag frame (CS-336).
                if (_pivotSource != _prefabContent)
                {
                    _pivotSource = _prefabContent;
                    _pivotCentred = _prefabContent.GetComponentInChildren<CentreOnViewer>(true);
                    _pivotField = _prefabContent.GetComponentInChildren<GalaxyField>(true);
                }

                var centred = _pivotCentred;
                var field = _pivotField;
                pivot = centred != null ? centred.transform.position
                    : field != null && field.CentresOnViewer ? field.ShellCentre
                    : _prefabContent.transform.position;
                return true;
            }

            if (contentRoot != null)
            {
                pivot = contentRoot.position;
                return true;
            }

            pivot = default;
            return false;
        }

        /// <summary>Goes to a place. Ignored if it is already open or a switch is running.</summary>
        public void Switch(ExperienceModule module)
        {
            // "Already open" means already on screen: the intro can report finished while the solar system is still current.
            if (module == null || IsSwitching || (module == Current && IsShowing(module)))
            {
                return;
            }

            // A mid-intro switch would pull the placement scene out from under IntroFlow.
            if (IntroRunning)
            {
                // A click means the player is done watching: skip the intro and open their pick once the galaxy
                // load settles (the same wait OpenStartModule does, so the two cannot stack).
                if (_intro.TrySkip())
                {
                    _queuedFromIntro = module;
                    Debug.Log($"ExperienceDirector: '{module.Id}' asked for during the intro, so the intro is being skipped and it opens next.", this);
                    return;
                }

                Debug.Log($"ExperienceDirector: '{module.Id}' ignored, the intro is still running.", this);
                SwitchNotice.Busy(module);
                return;
            }

            // Refuse before anything moves: half a switch is worse than an unresponsive tile. Logged once per module.
            if (string.IsNullOrEmpty(module.SceneName) && module.ContentPrefab == null)
            {
                if (_warnedEmpty.Add(module))
                {
                    Debug.LogWarning(
                        $"ExperienceDirector: module '{module.Id}' has neither SceneName nor ContentPrefab, so " +
                        "there is nothing to open. Its tile does nothing until one is set.", module);
                }

                // Every poke gets an answer, outside the once-per-module log gate.
                SwitchNotice.NotReady(module);
                return;
            }

            // StartCoroutine runs inline to the first yield, so a switch that fails that early has already run its
            // finally; storing the handle then would leave IsSwitching true forever.
            _switchFinished = false;
            var routine = StartCoroutine(SwitchRoutine(module));
            _switching = _switchFinished ? null : routine;
        }

        public void Switch(string id) => Switch(Find(id));

        private static bool IsShowing(ExperienceModule module) =>
            string.IsNullOrEmpty(module.SceneName) || module.SceneName == ViewLoader.CurrentView;

        private IEnumerator SwitchRoutine(ExperienceModule module)
        {
            // try/finally so _switching clears on every exit, or the dock stays wedged with the room already empty.
            try
            {
                Current = module;

                // Whatever the player pulled out belongs to the place they are leaving.
                ClearDestinations();
                RestoreEverything();

                // Every place opens from the default view, never where the last one was orbited, panned or zoomed to
                // (owner's direction, 14 Oct 2026): the desktop camera goes home before the new content is framed.
                GalaxyExplorer.XR.DesktopCameraRig.Instance?.ResetView();
                FaceViewer();

                var vo = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance.VoManager : null;
                if (vo != null)
                {
                    vo.Stop(true);
                }

                // The bed fades out with the narration; it describes the place being left.
                AmbienceController.StopBed();

                if (EnvironmentController.Instance != null)
                {
                    EnvironmentController.Instance.Set(module.Environment);
                }

                // Unload every other view scene, not just the previous module's: the inherited boot flow opens
                // its own, and leaving them stacked duplicate content and singletons.
                var adopted = UnloadOtherViews(module.SceneName);
                DestroyPrefabContent();

                // The panel lives under our root, which survives the unload, so it must be torn down explicitly here.
                DestroyScenePanel();

                if (!string.IsNullOrEmpty(module.SceneName))
                {
                    if (!adopted.IsValid())
                    {
                        var loaded = false;
                        var failure = BeginLoad(module.SceneName, () => loaded = true);

                        // Bounded: ViewLoader raises the callback from its own coroutine, and a coroutine that threw is never resumed.
                        var waited = 0f;
                        while (failure == null && !loaded && waited < loadTimeoutSeconds)
                        {
                            waited += Time.unscaledDeltaTime;
                            yield return null;
                        }

                        if (failure == null && !loaded)
                        {
                            failure = $"it was still not loaded after {loadTimeoutSeconds:0} s";
                        }

                        if (failure != null)
                        {
                            AbandonSwitch(module, failure);
                            yield break;
                        }
                    }
                }
                else
                {
                    _prefabContent = Instantiate(module.ContentPrefab, ContentRoot());
                    _prefabContent.name = module.Id;

                    // Before home is captured, because framing is part of where this content belongs.
                    _contentSpanMetres = ContentFramer.Centre(_prefabContent.transform);
                    FitArrangement(_prefabContent.transform, 0f);
                    BindRigFraming(_prefabContent);

                    // Capture home before the grow-in, which drives localScale from zero (CS-107).
                    FreePlacementAnchor.CaptureHome(_prefabContent.transform);
                }

                yield return null; // let the new content's own Awake/Start run before we touch it

                var content = ResolveContent(adopted);
                // By scene name, not ResolveContent, whose fallback can still be the place just left.
                var scene = string.IsNullOrEmpty(module.SceneName) ? default : SceneManager.GetSceneByName(module.SceneName);
                var sceneContent = scene.IsValid() && scene.isLoaded ? FindContent(scene) : null;
                if (sceneContent != null)
                {
                    // Fitted only after TransitionManager's zoom-in, which scales the scene up from nothing.
                    StartCoroutine(FitViewScene(sceneContent.transform));
                }

                if (content != null && growInSeconds > 0f)
                {
                    // Only our own framed instance needs its offset driven with the scale.
                    yield return GrowIn(content.transform, growInSeconds, content == _prefabContent);
                }

                // After the only `yield break`, so an abandoned switch never narrates a place the player is not in.
                // The panel goes up before the voice, as the written form of the same words.
                ShowScenePanel(module);

                if (vo != null && module.Narration != null && VOManager.AutoStart)
                {
                    vo.PlayClip(module.Narration, allowReplay: true, replaceQueue: true);
                }

                AmbienceController.PlayBed(module.Ambience);
            }
            finally
            {
                _switching = null;
                _switchFinished = true;
            }

            ExperienceChanged?.Invoke(module);
        }

        /// <summary>
        /// Starts the additive load and returns why it could not, or null. Catches <see cref="ViewLoader"/>'s throw
        /// for a scene missing from Build Settings, which lands on this stack because StartCoroutine runs inline.
        /// </summary>
        private static string BeginLoad(string sceneName, SceneLoaded onLoaded)
        {
            var loader = GalaxyExplorerManager.IsInitialized
                ? GalaxyExplorerManager.Instance.ViewLoaderScript
                : null;

            if (loader == null)
            {
                return "there is no ViewLoader to load it with";
            }

            try
            {
                loader.LoadViewAsync(sceneName, onLoaded);
                return null;
            }
            catch (Exception e)
            {
                return $"the loader refused it ({e.Message})";
            }
        }

        /// <summary>
        /// Gives up on a switch whose scene never arrived: back to passthrough (the old place is already gone) and
        /// <see cref="Current"/> cleared so the tile can be poked again.
        /// </summary>
        private void AbandonSwitch(ExperienceModule module, string reason)
        {
            Debug.LogError(
                $"ExperienceDirector: scene '{module.SceneName}' for module '{module.Id}' did not open — {reason}. " +
                "Check that it is enabled in Build Settings and that the module's SceneName matches it. Handing the " +
                "room back in passthrough; the dock stays live and the tile can be poked again.", this);

            // Shown as well as logged: nothing else would explain why the place is gone.
            SwitchNotice.FailedToOpen(module);

            Current = null;

            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(EnvironmentMode.Passthrough);
            }

            ExperienceChanged?.Invoke(null);
        }

        /// <summary>
        /// Scales content up from nothing. <paramref name="keepFramed"/> scales a <see cref="ContentFramer.Centre"/> offset
        /// with it, so the middle stays put instead of swelling out from the off-centre pivot.
        /// </summary>
        private static IEnumerator GrowIn(Transform content, float seconds, bool keepFramed = false)
        {
            // No target transform: the sound must outlive content a second switch destroys mid-grow.
            AudioService.Instance?.PlayClip(AudioId.GrowIn);

            var target = content.localScale;
            var targetPosition = keepFramed ? content.localPosition : Vector3.zero;

            var elapsed = 0f;
            content.localScale = Vector3.zero;
            if (keepFramed)
            {
                content.localPosition = Vector3.zero;
            }

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;

                // Unity's fake-null: a destination closed mid-grow would throw on the next write.
                if (content == null)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / seconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f); // ease out
                content.localScale = target * eased;
                if (keepFramed)
                {
                    content.localPosition = targetPosition * eased;
                }

                yield return null;
            }

            if (content == null)
            {
                yield break;
            }

            content.localScale = target;
            if (keepFramed)
            {
                content.localPosition = targetPosition;
            }
        }

        // ---------- keeping a whole arrangement inside the desktop view (CS-171)

        /// <summary>Desktop only: moves the content to where <see cref="ContentFramer.TryFitTarget"/> says it fits.</summary>
        // Later arrangements ease over the preset's time and re-capture home, so Restore returns to the fitted spot.
        private void FitArrangement(Transform content, float seconds)
        {
            if (!ContentFramer.TryFitTarget(content, desktopFitMargin, out var target))
            {
                return;
            }

            if (_fitting != null)
            {
                StopCoroutine(_fitting);
                _fitting = null;
            }

            if (seconds <= 0f)
            {
                content.localPosition = target;
                return;
            }

            _fitting = StartCoroutine(FitRoutine(content, target, seconds));
        }

        private IEnumerator FitViewScene(Transform content)
        {
            // Just the zoom-in: TransitionManager stays in transition until narration ends.
            var zoom = FindFirstObjectByType<ZoomInOut>();
            var waited = 0f;
            while (content != null && (IsSwitching || (zoom != null && !zoom.ZoomInIsDone)) && waited < loadTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (content != null && content.gameObject.activeInHierarchy)
            {
                // The scene root is pinned every frame (TransformHandler/LocationView), so move its child.
                FitArrangement(content.childCount > 0 ? content.GetChild(0) : content, ViewSceneFitSeconds);
            }
        }

        private IEnumerator FitRoutine(Transform content, Vector3 target, float seconds)
        {
            var from = content.localPosition;
            var elapsed = 0f;
            while (elapsed < seconds && content != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                content.localPosition = Vector3.LerpUnclamped(from, target, eased);
                yield return null;
            }

            _fitting = null;
            if (content == null)
            {
                yield break;
            }

            content.localPosition = target;

            // Re-capture so Restore returns to this fitted spot, not the first arrangement's.
            FreePlacementAnchor.CaptureHome(content);
        }

        private void BindRigFraming(GameObject content)
        {
            UnbindRigFraming();

            _framedRig = content != null ? content.GetComponentInChildren<LayoutRig>(true) : null;
            if (_framedRig != null)
            {
                _framedRig.LayoutApplied += OnLayoutApplied;
            }
        }

        private void UnbindRigFraming()
        {
            if (_framedRig != null)
            {
                _framedRig.LayoutApplied -= OnLayoutApplied;
                _framedRig = null;
            }

            if (_fitting != null)
            {
                StopCoroutine(_fitting);
                _fitting = null;
            }
        }

        private void OnLayoutApplied(LayoutPreset layout)
        {
            // The rig's first arrangement lands during the grow-in, which owns the position and was fitted at spawn.
            if (IsSwitching || _prefabContent == null || layout == null)
            {
                return;
            }

            FitArrangement(_prefabContent.transform, Mathf.Max(0f, layout.TransitionSeconds));
        }

        // ---------- content: a scene, or a prefab under our own root

        /// <summary>
        /// Where a <see cref="ExperienceModule.ContentPrefab"/> spawns: under the <see cref="ViewLoader"/>, which the
        /// intro moves into place in the room, so prefabs land where scene content does.
        /// </summary>
        /// <summary>
        /// Headset only: puts the view straight ahead of where the player is looking, at its height and distance, facing
        /// them, so a place opens in front of them wherever they have turned since (owner's direction, 14 Oct 2026).
        /// </summary>
        private static void FaceViewer()
        {
            var camera = Camera.main;
            var loader = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance.ViewLoaderScript : null;
            if (!UnityEngine.XR.XRSettings.isDeviceActive || camera == null || loader == null)
            {
                return;
            }

            var head = camera.transform.position;
            var ahead = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (ahead.sqrMagnitude < 1e-4f)
            {
                return; // looking straight up or down: no heading to face
            }

            var view = loader.transform;
            var flat = view.position - head;
            flat.y = 0f;
            var position = head + ahead.normalized * Mathf.Clamp(flat.magnitude, 1f, 3f);
            position.y = view.position.y;
            view.SetPositionAndRotation(position, Quaternion.LookRotation(ahead.normalized, Vector3.up));
        }

        private Transform ContentRoot()
        {
            if (contentRoot == null)
            {
                var loader = GalaxyExplorerManager.IsInitialized
                    ? GalaxyExplorerManager.Instance.ViewLoaderScript
                    : null;

                contentRoot = new GameObject("experience_content_root").transform;
                contentRoot.SetParent(loader != null ? loader.transform : null, false);
                _ownsContentRoot = true;
            }

            return contentRoot;
        }

        private void DestroyPrefabContent()
        {
            UnbindRigFraming();

            if (_prefabContent != null)
            {
                Destroy(_prefabContent);
            }

            // Cleared now, not after the deferred Destroy, so the switch never mistakes the dying instance for new content.
            _prefabContent = null;

            // A scene-backed place never sets the span, so a stale one would push its panel out.
            _contentSpanMetres = 0f;
        }

        // ---------- the open experience's own panel (GDD 3.3, 8.3; contract F-20, F-23, F-25, F-26)

        /// <summary>Puts up the panel for the place the player just arrived in.</summary>
        // Checks the copy, not the module: InfoPanel.Bind falls back to DisplayName, giving every place a one-word panel.
        private void ShowScenePanel(ExperienceModule module)
        {
            // Never two for one place (the Adopt path).
            DestroyScenePanel();

            if (module == null || !HasPanelCopy(module))
            {
                return;
            }

            if (scenePanelPrefab == null)
            {
                if (!_warnedNoPanelPrefab)
                {
                    _warnedNoPanelPrefab = true;
                    Debug.LogWarning(
                        "ExperienceDirector: no scene panel prefab is assigned, so no experience shows its own " +
                        "panel. Assign Assets/prefabs/ui/info_panel_prefab, or run " +
                        "Cosmic Simulation/Wire Scene Panel.", this);
                }

                return;
            }

            _scenePanel = SpawnPanel(module, ScenePanelAnchor(), module.Id + "_scene_panel");
        }

        private void DestroyScenePanel()
        {
            if (_scenePanel != null)
            {
                Destroy(_scenePanel.gameObject);
            }

            _scenePanel = null;
        }

        /// <summary>Spawns, binds and shows a panel; shared by experience and destination panels.</summary>
        private InfoPanel SpawnPanel(ExperienceModule module, Transform target, string name)
        {
            var panel = Instantiate(scenePanelPrefab, ContentRoot());
            panel.name = name;

            // Parked before its first LateUpdate so it fades in in place rather than sliding from the root's origin.
            panel.transform.position = target.position;

            panel.Bind(module);
            panel.SetTarget(target);
            panel.Show();

            // Not raycastable: it has nothing to press and would catch the ray in front of the experience.
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            return panel;
        }

        /// <summary>True when a module has prose of its own, as opposed to only a name.</summary>
        private static bool HasPanelCopy(ExperienceModule module)
        {
            var copy = module.Panel;
            if (copy == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(copy.Title) || !string.IsNullOrEmpty(copy.Instruction))
            {
                return true;
            }

            if (copy.Paragraphs != null)
            {
                foreach (var paragraph in copy.Paragraphs)
                {
                    if (!string.IsNullOrEmpty(paragraph))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// What the experience's panel sits beside: an empty anchor, because <see cref="InfoPanel"/> measures its target's renderers.
        /// </summary>
        private Transform ScenePanelAnchor()
        {
            if (_scenePanelAnchor == null)
            {
                _scenePanelAnchor = new GameObject("scene_panel_anchor").transform;
                _scenePanelAnchor.SetParent(ContentRoot(), false);
            }

            // Re-read each time so the offset can be tuned in the inspector during play.
            var offset = scenePanelOffset;

            // Push the panel past wide content (a row of planets) on the side the offset asks for.
            var wanted = _contentSpanMetres + Mathf.Max(0f, panelClearanceMetres);
            if (_contentSpanMetres > 0f && Mathf.Abs(offset.x) < wanted)
            {
                offset.x = offset.x < 0f ? -wanted : wanted;
            }

            _scenePanelAnchor.localPosition = offset;
            ContentFramer.KeepInView(_scenePanelAnchor);
            return _scenePanelAnchor;
        }

        /// <summary>
        /// Closes every open view scene except one copy of <paramref name="targetSceneName"/>, whoever opened it,
        /// and returns that survivor — an invalid scene when the target is not open yet and has to be loaded.
        /// </summary>
        private Scene UnloadOtherViews(string targetSceneName)
        {
            var keep = default(Scene);

            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || IsStructuralScene(scene))
                {
                    continue;
                }

                // Adopt the boot flow's copy of the target; loading it again is what produced two of everything.
                if (!keep.IsValid() && !string.IsNullOrEmpty(targetSceneName) && scene.name == targetSceneName)
                {
                    keep = scene;
                    continue;
                }

                SceneManager.UnloadSceneAsync(scene);
            }

            return keep;
        }

        /// <summary>
        /// Intro scenes count as structure, not places: unloading the placement scene breaks <c>IntroFlow</c>
        /// mid-sequence. A backstop for any unload that bypasses Switch's intro guard.
        /// </summary>
        private static readonly string[] IntroScenes =
        {
            "intro_earth_placement_scene",
        };

        private bool IsStructuralScene(Scene scene)
        {
            if (scene == SceneManager.GetActiveScene() || scene == gameObject.scene)
            {
                return true;
            }

            if (System.Array.IndexOf(IntroScenes, scene.name) >= 0)
            {
                return true;
            }

            var loader = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance.ViewLoaderScript : null;
            return loader != null && scene == loader.gameObject.scene;
        }

        /// <summary>The object the grow-in scales: our prefab instance, or the open scene's content root.</summary>
        private GameObject ResolveContent(Scene adopted)
        {
            var transitions = GalaxyExplorerManager.Instance.TransitionManager;

            if (_prefabContent != null)
            {
                // CurrentActiveScene readers would still hold the destroyed content, and fake-null slips past `?.`.
                if (transitions != null)
                {
                    transitions.CurrentActiveScene = _prefabContent;
                }

                return _prefabContent;
            }

            // An adopted scene never went through TransitionManager for us, so correct CurrentActiveScene to its own root.
            if (adopted.IsValid() && adopted.isLoaded)
            {
                var content = FindContent(adopted);
                if (content != null)
                {
                    if (transitions != null)
                    {
                        transitions.CurrentActiveScene = content;
                    }

                    return content;
                }
            }

            return transitions != null ? transitions.CurrentActiveScene : null;
        }

        /// <summary>A view scene's content root is the object carrying its <see cref="TransformHandler"/>.</summary>
        private static GameObject FindContent(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var handler = root.GetComponentInChildren<TransformHandler>(true);
                if (handler != null)
                {
                    return handler.gameObject;
                }
            }

            return null;
        }

        // ---------- destinations (nebulae opened from the Milky Way map)

        /// <summary>Opens a destination in front of the player, inside a black halo, without leaving the map.</summary>
        public GameObject OpenDestination(ExperienceModule destination, Vector3 position, float diameter)
        {
            if (destination == null || destination.ContentPrefab == null)
            {
                return null;
            }

            ClearDestinations();

            var instance = Instantiate(destination.ContentPrefab, position, Quaternion.identity);
            instance.name = destination.Id;
            _destinationObjects.Add(instance);

            // Home is captured here, the only place that knows the pinched tag's position, and before the grow-in.
            FreePlacementAnchor.CaptureHome(instance.transform);

            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(destination.Environment);
                EnvironmentController.Instance.SpawnHalo(instance.transform, diameter);
            }

            var vo = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance.VoManager : null;
            if (vo != null && destination.Narration != null)
            {
                vo.Stop(true);
                if (VOManager.AutoStart)
                {
                    vo.PlayClip(destination.Narration, allowReplay: true, replaceQueue: true);
                }
            }

            // The map's panel stays up (GDD 8.3); this one sits beside the overlay and follows it.
            if (scenePanelPrefab != null && HasPanelCopy(destination))
            {
                _destinationPanel = SpawnPanel(destination, instance.transform, destination.Id + "_panel");
            }

            // Announced before the grow-in so the tag is outlined while the cloud swells.
            OpenDestinationModule = destination;
            DestinationChanged?.Invoke(destination);

            StartCoroutine(GrowIn(instance.transform, growInSeconds));
            return instance;
        }

        /// <summary>Closes any open destination and puts the room back the way the current place wants it.</summary>
        public void ClearDestinations()
        {
            foreach (var go in _destinationObjects)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _destinationObjects.Clear();

            if (_destinationPanel != null)
            {
                Destroy(_destinationPanel.gameObject);
            }

            _destinationPanel = null;

            if (Current != null && EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(Current.Environment);
            }

            // Guarded: this runs at the head of every switch and open, and listeners shouldn't hear "nothing open" each time.
            if (OpenDestinationModule != null)
            {
                OpenDestinationModule = null;
                DestinationChanged?.Invoke(null);
            }
        }

        // ---------- layouts

        /// <summary>Puts every body of the open experience back where its arrangement says it belongs.</summary>
        public void RestoreEverything()
        {
            foreach (var solver in Registry<ForceSolver>.Active)
            {
                solver.ResetToRoot();
                solver.EnableForce = true;
            }

            // Non-body content (CS-107); snapped because the only caller is a switch tearing the place down.
            FreePlacementAnchor.RestoreAllImmediate();
        }
    }
}
