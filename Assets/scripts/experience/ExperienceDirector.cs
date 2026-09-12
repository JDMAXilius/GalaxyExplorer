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
    /// Takes the player from one place to another. The dock asks for a module; this opens its content, sets how
    /// much of the room shows, starts its narration and grows the content in. There is no drill-down and no Back:
    /// every place is one poke away, which is the whole point of the dock.
    ///
    /// A place is either a scene or a prefab. Three of the seven were inherited from Galaxy Explorer as scenes and
    /// keep loading through <see cref="ViewLoader"/>, so the existing content root, faders and camera rig keep
    /// working. The rest are prefabs spawned under a root this director owns: adding scenes for them would buy
    /// nothing but load time and four more things to keep in sync. Both kinds go through the same sequence, so
    /// nothing downstream has to know which it is.
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

        /// <summary>How long the intro's own last-stage load is given to appear before we stop waiting for one.</summary>
        private const float IntroSettleGrace = 0.5f;

        /// <summary>
        /// Content whose middle is already this close to its own origin is left exactly where it is, so the
        /// framing below can never nudge an experience that was authored the way the content root expects.
        /// </summary>
        private const float CentringDeadzone = 0.02f;

        private readonly List<GameObject> _destinationObjects = new List<GameObject>();
        private readonly HashSet<ExperienceModule> _warnedEmpty = new HashSet<ExperienceModule>();
        private GameObject _prefabContent;
        private InfoPanel _scenePanel;
        private InfoPanel _destinationPanel;
        private Transform _scenePanelAnchor;

        /// <summary>
        /// Half the width of the open prefab content, in metres, once it has been framed on the content root.
        /// Zero for a scene-backed place and for content with nothing to measure. Read by the panel anchor so
        /// the panel parks beside the experience rather than inside it.
        /// </summary>
        private float _contentSpanMetres;

        private bool _warnedNoPanelPrefab;
        private bool _ownsContentRoot;
        private Coroutine _switching;
        private bool _switchFinished;
        private IntroFlow _introFlow;
        private bool _introFinished;

        public static ExperienceDirector Instance { get; private set; }

        public IReadOnlyList<ExperienceModule> Modules => modules;

        public ExperienceModule Current { get; private set; }

        /// <summary>
        /// Raised once the new experience's scene is loaded and its content is growing in, and with <c>null</c>
        /// when a switch was abandoned and nothing is open — so nothing goes on claiming to be the current place.
        /// </summary>
        public static event Action<ExperienceModule> ExperienceChanged;

        /// <summary>The destination showing over the current place, or <c>null</c> when none is.</summary>
        public ExperienceModule OpenDestinationModule { get; private set; }

        /// <summary>True while a destination overlay is up over the current place.</summary>
        public bool HasOpenDestination => OpenDestinationModule != null;

        /// <summary>
        /// Raised when a destination opens over the current place, and with <c>null</c> when it closes.
        ///
        /// Separate from <see cref="ExperienceChanged"/> because a destination does not change the place: the
        /// Milky Way map is still <see cref="Current"/> with a nebula floating in front of it. A tag that
        /// watched the other event would light up when its nebula opened and then never learn it had closed.
        /// </summary>
        public static event Action<ExperienceModule> DestinationChanged;

        /// <summary>True while a switch is in flight; the dock ignores pokes during one.</summary>
        public bool IsSwitching => _switching != null;

        /// <summary>
        /// True while the app is still in onboarding. There is no flag anywhere that says so: <see cref="IntroFlow"/>
        /// keeps its stage in a private field, <c>TransitionManager.IsInIntroFlow</c> is already false on the intro's
        /// last stage, and <c>ViewLoader.IsIntro()</c> goes false as soon as the intro loads the solar system. What is
        /// honest is the intro's own end event, so this reads "an IntroFlow exists and it has not raised it yet".
        /// </summary>
        public bool IntroRunning
        {
            get
            {
                if (_introFinished)
                {
                    return false;
                }

                // Bound lazily as well as in Awake: an IntroFlow lives in the boot scene while this lives in
                // core_systems, and nothing guarantees the load order between them stays that way.
                BindIntro();
                return _introFlow != null;
            }
        }

        private void Awake()
        {
            Instance = this;
            BindIntro();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_introFlow != null)
            {
                _introFlow.OnIntroFinished -= HandleIntroFinished;
            }

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

        /// <summary>
        /// Subscribes to the intro's own end signal, which is also the only thing that says onboarding is over.
        /// <c>IntroFlow.OnIntroFinished</c> is raised when the flow reaches its galaxy stage — the last stage of
        /// <c>flow_manager_prefab</c>, reached from the solar-system stage by its 8 s auto-transition on both the
        /// desktop and the headset path — and by the editor quick start, which skips the flow entirely.
        /// </summary>
        private void BindIntro()
        {
            if (_introFlow != null || _introFinished)
            {
                return;
            }

            _introFlow = FindAnyObjectByType<IntroFlow>(FindObjectsInactive.Include);
            if (_introFlow != null)
            {
                _introFlow.OnIntroFinished += HandleIntroFinished;
            }
        }

        private void HandleIntroFinished()
        {
            if (_introFinished)
            {
                return;
            }

            _introFinished = true;
            StartCoroutine(OpenStartModuleWhenSettled());
        }

        /// <summary>
        /// Opens the first experience once the intro has actually let go of the room.
        ///
        /// The intro's last stage raises <c>OnIntroFinished</c> from the stage transition itself, while that same
        /// stage's own events — <c>TransitionManager.OnIntroFinished</c> and <c>LoadNextScene("galaxy_view_scene")</c>
        /// — run a frame or more later, because FlowManager fires stage events from a delayed coroutine. Opening the
        /// start module on the signal alone would race that load and stack two galaxies on each other. So: wait for
        /// the intro's transition to appear, wait for it to end, and only then adopt what it opened.
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

            // Bounded, and we open anyway if it expires: a transition that never ends is a broken intro, and
            // leaving the player in an empty room with no place open is the failure this is here to avoid.
            waited = 0f;
            while (transitions != null && transitions.InTransition && waited < loadTimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            OpenStartModule();
        }

        /// <summary>
        /// Opens the place the app starts in, once onboarding is over.
        ///
        /// Whatever the app is already showing wins over <see cref="startModule"/> when it is one of ours. The intro
        /// ends by opening a place of its own — it loads the galaxy itself — and the editor quick start opens
        /// whichever view the developer pressed Play in. Running the full switch over either would clear the room
        /// the intro has just filled, grow its content in from a point a second time and play the module's narration
        /// on top of the intro's own. So that case is recorded rather than re-opened.
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

        /// <summary>
        /// Records a place the app is already showing as the open one: the dock highlights its tile, later switches
        /// know what they are leaving, and the room takes the environment that place asks for. No content moves.
        /// </summary>
        private void Adopt(ExperienceModule module)
        {
            Current = module;

            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(module.Environment);
            }

            // No content moves, but the place is now open, and the two things that describe it belong to the
            // place rather than to the switch that would otherwise have opened it. Without this the start
            // module — the one the intro hands over, which is the Milky Way — would be the only experience in
            // the app that never showed its panel or played its bed.
            ShowScenePanel(module);
            AmbienceController.PlayBed(module.Ambience);

            ExperienceChanged?.Invoke(module);
        }

        /// <summary>The module whose scene is already loaded, if any: the place the app is in fact showing.</summary>
        private ExperienceModule ModuleForOpenScene()
        {
            // The last view anybody asked for is checked first. The intro passes through the solar system on its way
            // to the galaxy and can leave both loaded for a moment, and the one it asked for last is where it is
            // heading; walking the scene list alone would sometimes name the one it is leaving.
            var current = ViewLoader.CurrentView;
            if (IsSceneLoaded(current))
            {
                var asked = FindModuleByScene(current);
                if (asked != null)
                {
                    return asked;
                }
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

        private static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
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

        /// <summary>Goes to a place. Ignored if it is already open or a switch is running.</summary>
        public void Switch(ExperienceModule module)
        {
            if (module == null || module == Current || IsSwitching)
            {
                return;
            }

            // The dock is meant to be hidden during onboarding, so this is the backstop for the poke that gets
            // through anyway. A switch mid-intro pulls the placement scene out from under IntroFlow and leaves the
            // flow driving content that is no longer there. Refused rather than queued: the intro ends by opening a
            // place of its own, and a poke made a minute earlier arriving on top of that would be a surprise.
            if (IntroRunning)
            {
                Debug.Log($"ExperienceDirector: '{module.Id}' ignored, the intro is still running.", this);
                return;
            }

            // Refuse before anything moves. Half a switch — room cleared, narration stopped, nothing to look at —
            // is worse than an unresponsive tile, and the tile stays live for when the content does arrive. Once
            // per module, or an unfinished tile would fill the log every time the player pokes it.
            if (string.IsNullOrEmpty(module.SceneName) && module.ContentPrefab == null)
            {
                if (_warnedEmpty.Add(module))
                {
                    Debug.LogWarning(
                        $"ExperienceDirector: module '{module.Id}' has neither SceneName nor ContentPrefab, so " +
                        "there is nothing to open. Its tile does nothing until one is set.", module);
                }

                // Outside the once-per-module gate above: the log is for us and repeats are noise, but the
                // player is owed an answer to every poke, and a tile that answers only the first time reads
                // as broken. Worded as "not built yet" rather than as a failure, because that is what it is —
                // four of the seven places are legitimately sceneless until their content lands.
                SwitchNotice.NotReady(module);
                return;
            }

            // StartCoroutine runs the routine up to its first yield before it hands the handle back, so a switch
            // that fails that early — ViewLoader throws outright when a scene is missing from Build Settings — has
            // already run its finally by the time we get here. Storing the handle then would leave IsSwitching true
            // for the rest of the session and refuse every later poke: the wedge this pair of flags exists to stop.
            _switchFinished = false;
            var routine = StartCoroutine(SwitchRoutine(module));
            _switching = _switchFinished ? null : routine;
        }

        public void Switch(string id) => Switch(Find(id));

        private IEnumerator SwitchRoutine(ExperienceModule module)
        {
            // Everything below is inside a try/finally purely so that _switching clears on every exit — the timeout,
            // a throw out of the loader, a throw out of anything the new content runs. A switch that ends without
            // clearing it takes the dock with it, and the room is already empty by then.
            try
            {
                Current = module;

                // Whatever the player pulled out belongs to the place they are leaving.
                ClearDestinations();
                RestoreEverything();

                var vo = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance.VoManager : null;
                if (vo != null)
                {
                    vo.Stop(true);
                }

                // The bed goes out with the narration, at the same beat and for the same reason: both describe
                // the place being left. It fades rather than cuts, and the fade is well over by the time the new
                // one is asked for at the bottom of this routine.
                AmbienceController.StopBed();

                if (EnvironmentController.Instance != null)
                {
                    EnvironmentController.Instance.Set(module.Environment);
                }

                // Everything the last place put in the room goes now, and a prefab instance goes at the same beat a
                // scene is unloaded. Which module we think was previous is not enough to go on: the original Galaxy
                // Explorer boot flow opens view scenes of its own, and leaving those behind stacked duplicate content
                // and duplicate singletons on top of each other.
                var adopted = UnloadOtherViews(module.SceneName);
                DestroyPrefabContent();

                // The panel is the previous place's, so it goes when the previous place does — not earlier, or
                // the room would be described by nothing while the old content is still standing there, and not
                // later, or a switch that fails to load would leave the old place's prose floating over an empty
                // room. It lives under our own content root, which survives the unload, so it has to be told.
                DestroyScenePanel();

                if (!string.IsNullOrEmpty(module.SceneName))
                {
                    if (!adopted.IsValid())
                    {
                        var loaded = false;
                        var failure = BeginLoad(module.SceneName, () => loaded = true);

                        // Bounded, because the callback is not guaranteed to arrive at all: ViewLoader raises it from
                        // inside its own coroutine, and a coroutine that threw is simply never resumed.
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
                    FrameContent(_prefabContent.transform);

                    // Where Restore will bring this back to, taken here because here is the only moment the
                    // pose is the intended one: before the grow-in below, which drives localScale from zero
                    // every frame for its duration (CS-107).
                    FreePlacementAnchor.CaptureHome(_prefabContent.transform);
                }

                yield return null; // let the new content's own Awake/Start run before we touch it

                var content = ResolveContent(adopted);
                if (content != null && growInSeconds > 0f)
                {
                    // Only our own instance may have been framed, and only a framed object needs its offset
                    // driven with the scale; a view scene's root is where its scene put it.
                    yield return GrowIn(content.transform, growInSeconds, content == _prefabContent);
                }

                // Both after the grow-in, and both after the only `yield break` in this routine: a switch that
                // was abandoned because its scene never arrived must not end up narrating, describing or
                // sounding like a place the player is not in. The panel goes up before the voice starts, because
                // it is the written form of the same words.
                ShowScenePanel(module);

                if (vo != null && module.Narration != null)
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
        /// Starts the additive load and reports why it could not start, rather than letting the failure escape.
        /// <see cref="ViewLoader"/> throws when <c>SceneManager.LoadSceneAsync</c> comes back null — a scene missing
        /// from Build Settings, or a typo in a module's SceneName — and that throw lands on this stack, because
        /// StartCoroutine runs a coroutine up to its first yield inline. Returns null when the load is under way.
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
        /// Gives up on a switch whose scene never arrived, in one log line naming the scene.
        ///
        /// The place the player was in is unloaded well before this point, so going back is not on offer: the best
        /// state left is the room itself. Passthrough rather than the module's own environment, because the
        /// alternative is a dimmed or fully black void with nothing in it — on a headset the real room is never
        /// nothing, and on desktop it is the plainest of the four. <see cref="Current"/> is cleared so that no tile
        /// reads as open, and so the same tile can be poked again once the scene is put back in Build Settings.
        /// </summary>
        private void AbandonSwitch(ExperienceModule module, string reason)
        {
            Debug.LogError(
                $"ExperienceDirector: scene '{module.SceneName}' for module '{module.Id}' did not open — {reason}. " +
                "Check that it is enabled in Build Settings and that the module's SceneName matches it. Handing the " +
                "room back in passthrough; the dock stays live and the tile can be poked again.", this);

            // Said out loud as well as logged. This is the case the player cannot make sense of on their own:
            // the place they were in is gone, the room has come back, and nothing else would explain why.
            SwitchNotice.FailedToOpen(module);

            Current = null;

            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(EnvironmentMode.Passthrough);
            }

            ExperienceChanged?.Invoke(null);
        }

        /// <summary>
        /// Scales content up from nothing, the one transition this app uses.
        ///
        /// <paramref name="keepFramed"/> is for content <see cref="FrameContent"/> has offset so that its middle
        /// sits on the content root. Such an object's pivot is no longer its middle, so scale alone would swell it
        /// out of a point somewhere off to one side and walk the middle across the room on the way in. Driving the
        /// offset by the same factor as the scale keeps the middle still: at factor zero the offset is zero too, so
        /// everything starts from the content root, which is where the whoosh belongs. Content that was not framed
        /// has no offset to drive and is left alone, so this is a no-op for every place that already worked.
        /// </summary>
        private static IEnumerator GrowIn(Transform content, float seconds, bool keepFramed = false)
        {
            // No target transform on purpose: the whoosh belongs to the arrival, not to the object, so it
            // must outlive content that a second switch destroys mid-grow. The id has no clip yet, and
            // PlayClip simply does nothing until one is assigned, so this can ship ahead of the sound.
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

                // A destination the player closed mid-grow is already gone, and Unity's fake-null makes the
                // next write a MissingReferenceException rather than a quiet no-op.
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

        // ---------- framing a content prefab on the content root

        /// <summary>
        /// Moves a freshly spawned content prefab so that the middle of what it shows sits on the content root.
        ///
        /// <para>Every scene-backed place puts its content at the content root's own origin — <c>SolarSystemContent</c>
        /// and the galaxy view are both at local zero under the <see cref="ViewLoader"/> — and that origin is where
        /// the room has decided the experience goes: two metres in front of the player and a little below eye
        /// height, turned to face them. A content prefab authored around its own origin therefore lands correctly
        /// with nothing done to it, which is why Andromeda and the Cosmic Web have always been in the right place.</para>
        ///
        /// <para>The solar row was not. Its arrangement is written in room coordinates — <c>SystemLayoutBuilder</c>
        /// stands the bodies 1.2 m above the floor and up to two metres out from the player's feet — so spawning it
        /// at the content root put the planets 1.2 m above and most of a metre beyond the thing the player was
        /// looking at. On the desktop rig that is far enough above the view to leave the room empty but for the
        /// experience's own panel, which parks near the root and so stayed visible: the black screen this fixes.</para>
        ///
        /// <para>Rather than teach the director which places are authored which way, the content is asked where its
        /// middle is and then offset by that. Content that has nothing to measure — a point cloud drawn by a command
        /// buffer has no renderer at all — reports nothing and is not touched, and content already centred on its own
        /// origin falls inside <see cref="CentringDeadzone"/> and is not touched either.</para>
        /// </summary>
        private void FrameContent(Transform content)
        {
            _contentSpanMetres = 0f;

            if (content == null || !TryMeasureContent(content, out var measured))
            {
                return;
            }

            // The content root is turned to face the player, so a box measured on world axes says nothing about
            // the content's own width. The two horizontal extents combined are the widest the box can be in any
            // frame turned about the vertical — an over-estimate rather than an under-estimate, which is the
            // safe direction to be wrong in when the number is only used to keep a panel clear.
            _contentSpanMetres = new Vector2(measured.extents.x, measured.extents.z).magnitude;

            // Moving the transform by this moves the middle by the same, and the middle then lands exactly where
            // the transform is now — the content root's origin, since a fresh instance sits on it.
            var offset = content.position - measured.center;
            if (offset.sqrMagnitude < CentringDeadzone * CentringDeadzone)
            {
                return;
            }

            content.position += offset;
        }

        /// <summary>
        /// Where the content's middle is, in world space, and roughly how far it reaches. False when there is
        /// nothing honest to measure. Only the centre is relied on to be exact; the size is a clearance.
        /// </summary>
        private static bool TryMeasureContent(Transform content, out Bounds measured)
        {
            // An arrangeable place states its own arrangement, which beats guessing from geometry: the home
            // anchors are the authored intent, and they are right even before the rig's first Start has run.
            var rig = content.GetComponentInChildren<LayoutRig>(true);
            if (rig != null && rig.TryGetArrangementBounds(out measured))
            {
                return true;
            }

            return TryMeasureRenderers(content, out measured);
        }

        /// <summary>The box the content's real geometry fills, ignoring everything that is not the thing itself.</summary>
        private static bool TryMeasureRenderers(Transform content, out Bounds measured)
        {
            measured = default;
            var found = false;

            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Effects and text say nothing about where the thing is. A halo or a tractor beam is far wider
                // than what it surrounds, and a panel sits at its prefab's own origin until the first frame it
                // is shown tells it where to go — measuring those would drag the middle somewhere arbitrary.
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer ||
                    r is SpriteRenderer || r.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                var bounds = r.bounds;
                if (bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                if (found)
                {
                    measured.Encapsulate(bounds);
                }
                else
                {
                    measured = bounds;
                    found = true;
                }
            }

            return found;
        }

        // ---------- content: a scene, or a prefab under our own root

        /// <summary>
        /// Where a module's <see cref="ExperienceModule.ContentPrefab"/> is spawned.
        ///
        /// It has to hang off the <see cref="ViewLoader"/>, not the world origin. View-scene content is not
        /// origin-relative: the intro places the experience in the room by moving and rotating the Loader
        /// itself (<c>WorldAnchorHandler.CreateWorldAnchor</c>), and every view scene's root follows it. A
        /// prefab spawned at identity would appear at the player's feet, unrotated and unanchored, while every
        /// scene-backed experience sat two metres away facing them.
        /// </summary>
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
            if (_prefabContent != null)
            {
                Destroy(_prefabContent);
            }

            // Cleared straight away rather than after the deferred Destroy, so the rest of the switch does not
            // mistake a dying instance for the new content.
            _prefabContent = null;

            // The span described content that has just gone. A scene-backed place never sets it, so leaving the
            // last prefab's width behind would push that place's panel out into nothing.
            _contentSpanMetres = 0f;
        }

        // ---------- the open experience's own panel (GDD 3.3, 8.3; contract F-20, F-23, F-25, F-26)

        /// <summary>
        /// Puts up the panel that describes the place the player has just arrived in: its title, its two or
        /// three paragraphs and the line telling them what they can do here.
        ///
        /// A module with no authored copy gets nothing, silently — that is the normal state of a place whose
        /// prose has not been written yet, not a fault. The copy is checked rather than the module: a module
        /// always has a <see cref="ExperienceModule.DisplayName"/>, and <see cref="InfoPanel.Bind"/> falls back
        /// to it, so testing the module alone would give every place a one-word panel.
        /// </summary>
        private void ShowScenePanel(ExperienceModule module)
        {
            // Never two for one place. Switch already tore the previous one down; this is the Adopt path, and
            // the guard against anyone opening the same place twice.
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

        /// <summary>
        /// Instantiates a panel, binds a module's copy to it and shows it. Shared by the experience's own panel
        /// and by a destination overlay's, which differ only in what they sit beside.
        /// </summary>
        private InfoPanel SpawnPanel(ExperienceModule module, Transform target, string name)
        {
            var panel = Instantiate(scenePanelPrefab, ContentRoot());
            panel.name = name;

            // Parked where it belongs before its first LateUpdate, so it fades in where it will live rather
            // than sliding there from the content root's origin.
            panel.transform.position = target.position;

            panel.Bind(module);
            panel.SetTarget(target);
            panel.Show();

            // The prefab carries a GraphicRaycaster (UiPrefabBuilder puts one on every world-space canvas it
            // builds) and this panel has nothing to press. Left raycastable it would be a sheet of text the
            // player's ray could catch on, between them and the experience it describes.
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
        /// What the experience's panel sits beside. An empty object rather than the content itself: a place's
        /// content is a galaxy or a row of planets, whose first renderer says nothing useful about how wide the
        /// whole thing is, and <see cref="InfoPanel"/> measures its target's renderers to find the edge to clear.
        /// An anchor with none falls back to a small radius, so the panel lands where this offset puts it.
        /// </summary>
        private Transform ScenePanelAnchor()
        {
            if (_scenePanelAnchor == null)
            {
                _scenePanelAnchor = new GameObject("scene_panel_anchor").transform;
                _scenePanelAnchor.SetParent(ContentRoot(), false);
            }

            // Re-read every time, so the offset can be dragged in the inspector during play and take effect on
            // the next switch.
            var offset = scenePanelOffset;

            // The authored offset was written when every prefab place was a galaxy a metre or so across sitting on
            // the content root. A row of ten planets is nearly two metres wide about the same point, and the panel
            // would stand between Saturn and Uranus. Pushed out to the edge of whatever is actually there, keeping
            // the side the offset asks for; content narrower than the offset leaves it exactly as authored.
            var wanted = _contentSpanMetres + Mathf.Max(0f, panelClearanceMetres);
            if (_contentSpanMetres > 0f && Mathf.Abs(offset.x) < wanted)
            {
                offset.x = offset.x < 0f ? -wanted : wanted;
            }

            _scenePanelAnchor.localPosition = offset;
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

                // Adopt the first copy of the target and drop the rest: the boot flow having already opened the
                // scene is the normal case at startup, and loading it again is what produced two of everything.
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
        /// Structure, not a place. Mostly derived rather than named: the app boots into the active scene, and
        /// the scene that loads views — the one holding the ViewLoader, and this director with it — is by
        /// definition not a place. (<see cref="GalaxyExplorerManager"/> is no help here: <c>Singleton</c> moves
        /// its root to DontDestroyOnLoad, so it no longer reports the scene it came from.)
        ///
        /// The intro scenes have to be named, though. They are additively loaded like a view but the intro flow
        /// is standing on them: unloading the placement scene pulls <c>PlacementControl</c> out from under
        /// <c>IntroFlow</c> mid-sequence. <see cref="Switch"/> now refuses outright while <see cref="IntroRunning"/>,
        /// so this list is the second line of defence rather than the only one — kept because anything that reaches
        /// an unload without going through Switch would still take the intro's floor away.
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
                // Readers of CurrentActiveScene (StarBackgroundManager, WorldAnchorHandler) would otherwise
                // still hold the content we destroyed on the way in, and Unity's fake-null slips past `?.`.
                if (transitions != null)
                {
                    transitions.CurrentActiveScene = _prefabContent;
                }

                return _prefabContent;
            }

            // A scene we adopted never went through TransitionManager on our account, so its CurrentActiveScene
            // can still be pointing at content we just unloaded. Find this scene's own root and correct it.
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

            // The position above is knowledge only this method has — it comes from the tag the player pinched,
            // not from the prefab — so home is recorded now. Before the grow-in at the bottom of this method,
            // which writes localScale from zero every frame and would otherwise be what got remembered.
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
                vo.PlayClip(destination.Narration, allowReplay: true, replaceQueue: true);
            }

            // A destination's copy is authored in the same place as an experience's, and GDD 8.3 lets several
            // panels be open at once, so the map's own panel stays up while this one joins it. It sits beside
            // the overlay rather than at the experience anchor, and follows it when the player moves it.
            if (scenePanelPrefab != null && HasPanelCopy(destination))
            {
                _destinationPanel = SpawnPanel(destination, instance.transform, destination.Id + "_panel");
            }

            // Announced after everything that could still have refused, and before the grow-in, so the tag is
            // already outlined while the cloud is still swelling out of a point.
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

            // Guarded rather than raised blind: this is called on the way into every OpenDestination and at the
            // head of every Switch, and a listener that was told "nothing is open" on each of those would spend
            // most of its life reacting to a state it was already in.
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
            foreach (var solver in FindObjectsByType<ForceSolver>(FindObjectsSortMode.None))
            {
                solver.ResetToRoot();
                solver.EnableForce = true;
            }

            // Content that is not a body — a nebula overlay, the Cosmic Web, Andromeda — has no solver to reset,
            // so until CS-107 this loop walked straight past it. Snapped rather than eased, to match the line
            // above: the only caller is a switch, and the place this restores is already being taken apart.
            FreePlacementAnchor.RestoreAllImmediate();
        }
    }
}
