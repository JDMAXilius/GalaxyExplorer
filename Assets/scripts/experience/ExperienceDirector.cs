// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using GalaxyExplorer;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Takes the player from one place to another. The dock asks for a module; this loads its scene, sets how much
    /// of the room shows, starts its narration and grows the content in. There is no drill-down and no Back: every
    /// place is one poke away, which is the whole point of the dock.
    ///
    /// Scene loading still goes through <see cref="ViewLoader"/>, so the existing content root, faders and camera
    /// rig keep working; this class owns the ordering and the state.
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

        private readonly List<GameObject> _destinationObjects = new List<GameObject>();
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

            _switching = StartCoroutine(SwitchRoutine(module));
        }

        public void Switch(string id) => Switch(Find(id));

        private IEnumerator SwitchRoutine(ExperienceModule module)
        {
            var previous = Current;
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

            if (previous != null && !string.IsNullOrEmpty(previous.SceneName))
            {
                GalaxyExplorerManager.Instance.ViewLoaderScript.UnLoadView(previous.SceneName, true);
            }

            if (!string.IsNullOrEmpty(module.SceneName))
            {
                var loaded = false;
                GalaxyExplorerManager.Instance.ViewLoaderScript.LoadViewAsync(module.SceneName, () => loaded = true);
                while (!loaded)
                {
                    yield return null;
                }
            }

            yield return null; // let the scene's own Awake/Start run before we touch its content

            var content = GalaxyExplorerManager.Instance.TransitionManager != null
                ? GalaxyExplorerManager.Instance.TransitionManager.CurrentActiveScene
                : null;
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
