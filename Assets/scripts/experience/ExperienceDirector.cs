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
        [Tooltip("Where a module's ContentPrefab is spawned. One is made at the origin when this is left empty.")]
        private Transform contentRoot;

        private readonly List<GameObject> _destinationObjects = new List<GameObject>();
        private readonly HashSet<ExperienceModule> _warnedEmpty = new HashSet<ExperienceModule>();
        private GameObject _prefabContent;
        private bool _ownsContentRoot;
        private Coroutine _switching;

        public static ExperienceDirector Instance { get; private set; }

        public IReadOnlyList<ExperienceModule> Modules => modules;

        public ExperienceModule Current { get; private set; }

        /// <summary>Raised once the new experience's scene is loaded and its content is growing in.</summary>
        public static event Action<ExperienceModule> ExperienceChanged;

        /// <summary>True while a switch is in flight; the dock ignores pokes during one.</summary>
        public bool IsSwitching => _switching != null;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
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

        /// <summary>Opens the module the app starts on. Called once the intro has finished.</summary>
        public void OpenStartModule()
        {
            if (startModule != null && Current == null)
            {
                Switch(startModule);
            }
        }

        /// <summary>Goes to a place. Ignored if it is already open or a switch is running.</summary>
        public void Switch(ExperienceModule module)
        {
            if (module == null || module == Current || IsSwitching)
            {
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

                return;
            }

            _switching = StartCoroutine(SwitchRoutine(module));
        }

        public void Switch(string id) => Switch(Find(id));

        private IEnumerator SwitchRoutine(ExperienceModule module)
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

            if (!string.IsNullOrEmpty(module.SceneName))
            {
                if (!adopted.IsValid())
                {
                    var loaded = false;
                    GalaxyExplorerManager.Instance.ViewLoaderScript.LoadViewAsync(module.SceneName, () => loaded = true);
                    while (!loaded)
                    {
                        yield return null;
                    }
                }
            }
            else
            {
                _prefabContent = Instantiate(module.ContentPrefab, ContentRoot());
                _prefabContent.name = module.Id;
            }

            yield return null; // let the new content's own Awake/Start run before we touch it

            var content = ResolveContent(adopted);
            if (content != null && growInSeconds > 0f)
            {
                yield return GrowIn(content.transform, growInSeconds);
            }

            if (vo != null && module.Narration != null)
            {
                vo.PlayClip(module.Narration, allowReplay: true, replaceQueue: true);
            }

            _switching = null;
            ExperienceChanged?.Invoke(module);
        }

        /// <summary>Scales content up from nothing, the one transition this app uses.</summary>
        private static IEnumerator GrowIn(Transform content, float seconds)
        {
            var target = content.localScale;
            var elapsed = 0f;
            content.localScale = Vector3.zero;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                content.localScale = target * (1f - Mathf.Pow(1f - t, 3f)); // ease out
                yield return null;
            }

            content.localScale = target;
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
        /// <c>IntroFlow</c> mid-sequence, and nothing yet stops a dock poke during onboarding.
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

            if (Current != null && EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.Set(Current.Environment);
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
        }
    }
}
