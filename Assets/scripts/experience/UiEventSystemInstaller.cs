// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// Guarantees one live <see cref="EventSystem"/> with one <see cref="InputSystemUIInputModule"/>, so screen-space canvases take clicks.
    /// </summary>
    // Runs at boot and on every scene load because core_systems_scene brings its own EventSystem late; the app's wins.
    // Input System module, not StandaloneInputModule: two backends reading the cursor make clicks land off the hover.
    public static class UiEventSystemInstaller
    {
        private static EventSystem _system;
        private static BaseInputModule _module;

        // Kept so it can stand down when the app's own EventSystem arrives later in the boot flow.
        private static GameObject _created;

        // Set by scene load/unload; without it our own EventSystem satisfies the guard and the duplicate is never retired.
        private static bool _scanPending = true;

        private static bool _hooked;

        // One reused probe; a fresh PointerEventData per frame is garbage.
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData _probe;
        private static EventSystem _probeSystem;
        private static int _probeFrame = -1;
        private static Vector2 _probePosition;
        private static bool _probeResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Explicit because with domain reload off the statics survive from the last play session.
            _scanPending = true;

            // The first scene is already loaded and will never raise sceneLoaded for us.
            Ensure();
            SweepRaycasters();
        }

        /// <summary>Idempotent; safe to call from Awake, Start or every frame.</summary>
        public static void Ensure()
        {
            Hook();

            if (!_scanPending && _system != null && _module != null && EventSystem.current == _system)
            {
                return;
            }

            _scanPending = false;

            EventSystem keeper = null;
            foreach (var candidate in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
            // A disabled EventSystem cannot become current, so it is not a duplicate.
                if (candidate == null || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (keeper == null)
                {
                    keeper = candidate;
                    continue;
                }

            // Ours exists only because nothing else had appeared yet; the app's own always wins.
                if (IsOurs(keeper) && !IsOurs(candidate))
                {
                    Retire(keeper);
                    keeper = candidate;
                    continue;
                }

                Retire(candidate);
            }

            if (keeper == null)
            {
            // Kept across loads: the active scene here is often a view scene, which the director unloads.
                _created = new GameObject("ui_event_system");
                Object.DontDestroyOnLoad(_created);
                keeper = _created.AddComponent<EventSystem>();
            }

            // Other modules must be switched off, not just joined: an EventSystem runs the first module in component
            // order, and the authored XRUIInputModule (empty actions) precedes ours. Disabled, not destroyed, so XRI won't add another.
            var module = keeper.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = keeper.gameObject.AddComponent<InputSystemUIInputModule>();
                EnsureDefaultActions(module);
            }

            if (!module.enabled)
            {
                module.enabled = true;
            }

            foreach (var other in keeper.GetComponents<BaseInputModule>())
            {
                if (other == null || other == module || !other.enabled)
                {
                    continue;
                }

                other.enabled = false;
                Debug.Log($"UiEventSystemInstaller: '{other.GetType().Name}' on '{keeper.name}' was switched off. " +
                          "This app routes screen-space UI through InputSystemUIInputModule and world-space UI " +
                          "through GEPointer, and an EventSystem runs whichever module comes first.");
            }

            _module = module;
            _system = keeper;
        }

        /// <summary>Assigns the package's default UI actions: <c>Reset</c> never runs for a runtime <c>AddComponent</c>.</summary>
        private static void EnsureDefaultActions(InputSystemUIInputModule module)
        {
            if (module == null || module.actionsAsset != null)
            {
                return;
            }

            try
            {
                module.AssignDefaultActions();
            }
            catch (System.Exception e)
            {
            // Not fatal: only the desktop's screen-space UI depends on it.
                Debug.LogWarning("UiEventSystemInstaller: the UI input module came up with no actions and the " +
                                 "package defaults could not be assigned, so screen-space clicks will not be " +
                                 "delivered. " + e.Message);
            }
        }

        /// <summary>
        /// True when the cursor is over a screen-space canvas. Not <c>IsPointerOverGameObject</c>, which also counts
        /// world-space canvases and would stop the desktop mouse hovering tags and panels hit through <c>GEPointer</c>.
        /// </summary>
        public static bool IsPointerOverScreenSpaceUi(Vector2 screenPosition)
        {
            Ensure();

            var system = EventSystem.current;
            if (system == null)
            {
                return false;
            }

            if (_probeFrame == Time.frameCount && _probePosition == screenPosition)
            {
                return _probeResult;
            }

            _probeFrame = Time.frameCount;
            _probePosition = screenPosition;
            _probeResult = false;

            // A live raycast: the module's cached answer is a frame behind and covers world space. A PointerEventData
            // is bound to one EventSystem, so it is rebuilt when the keeper changes.
            if (_probe == null || _probeSystem != system)
            {
                _probe = new PointerEventData(system);
                _probeSystem = system;
            }

            _probe.position = screenPosition;

            Hits.Clear();
            system.RaycastAll(_probe, Hits);

            for (var i = 0; i < Hits.Count; i++)
            {
                // A GraphicRaycaster's own Canvas is the hit's canvas; a nested canvas brings its own raycaster.
                var raycaster = Hits[i].module as GraphicRaycaster;
                if (raycaster == null)
                {
                    continue;
                }

                var canvas = raycaster.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    _probeResult = true;
                    break;
                }
            }

            return _probeResult;
        }

        /// <summary>Same test at the current mouse position. Desktop only.</summary>
        public static bool IsPointerOverScreenSpaceUi()
        {
            var mouse = Mouse.current;
            return mouse != null && IsPointerOverScreenSpaceUi(mouse.position.ReadValue());
        }

        private static void Hook()
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _scanPending = true;
            Ensure();
            SweepRaycasters();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // The keeper may have gone with the scene.
            _scanPending = true;
            Ensure();
        }

        private static bool IsOurs(EventSystem system)
        {
            return _created != null && system != null && system.gameObject == _created;
        }

        private static void Retire(EventSystem system)
        {
            // Disabled first so EventSystem.current is right this frame, not at the end of it.
            system.enabled = false;

            if (IsOurs(system))
            {
                // Takes our module with it.
                Object.Destroy(system.gameObject);
                _created = null;
                _module = null;
                return;
            }

            Debug.LogWarning($"UiEventSystemInstaller: a second EventSystem ('{system.name}') was already in the " +
                             "scene and has been disabled. Unity supports exactly one; check which scene brought it.");
        }

        /// <summary>
        /// Gives an interactive screen-space canvas without a raycaster one, and says so; canvases only arrive with scenes.
        /// </summary>
        private static void SweepRaycasters()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (canvas.GetComponent<GraphicRaycaster>() != null)
                {
                    continue;
                }

                // Pure-text canvases (SwitchNotice) deliberately have no raycaster so they never block a press.
                if (canvas.GetComponentInChildren<IEventSystemHandler>(true) == null)
                {
                    continue;
                }

                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"UiEventSystemInstaller: screen-space canvas '{canvas.name}' had no GraphicRaycaster " +
                          "and could not be clicked; one was added at runtime. Add it to the prefab.");
            }
        }
    }
}
