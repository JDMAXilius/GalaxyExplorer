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
    /// Guarantees one live <see cref="EventSystem"/> carrying one input module, so a screen-space canvas can
    /// actually be clicked.
    ///
    /// The app shipped without one. <c>main_camera_prefab</c> carries an <see cref="EventSystem"/> with no
    /// module at all, which is enough for uGUI to draw a button and never deliver a press — every button on
    /// the desktop menu and the desktop dock was dead. Nothing noticed because every *other* pointer in this
    /// project goes through <c>GEPointer</c> and a physics raycast, which does not involve the EventSystem.
    ///
    /// Why it installs from a boot hook rather than from whoever needs it: the one EventSystem in the app
    /// arrives with <c>core_systems_scene</c>, which is loaded well into the boot flow rather than at frame 0.
    /// An installer that ran once at startup would find nothing, make its own, and then be sitting next to a
    /// second EventSystem the moment the boot scene landed. So this runs at startup *and* on every scene load,
    /// and it prefers the app's own EventSystem over the one it made: whichever shows up, the invariant holds.
    ///
    /// Module choice: <see cref="InputSystemUIInputModule"/>. Active Input Handling is "Both" (Android needs
    /// the legacy backend for TouchScript), so <c>StandaloneInputModule</c> would compile - but it reads
    /// <c>UnityEngine.Input</c>, while every pointer this project actually reads comes from the Input System
    /// (<c>Mouse.current</c> in <c>DesktopMouseInput</c>, XRI 3.6 throughout). Two backends reading the cursor
    /// is how you get a click that lands somewhere the hover was not. XRI's <c>XRUIInputModule</c> is the other
    /// candidate and is deliberately not used: it would drive world-space uGUI from the hand ray, and in this
    /// project world-space UI is hit with physics through <c>GEPointer</c>, not with graphic raycasts.
    /// </summary>
    public static class UiEventSystemInstaller
    {
        // What we have already settled on, so the common path is three reference compares rather than a scan.
        private static EventSystem _system;
        private static BaseInputModule _module;

        // The EventSystem and the module we created, if we had to. Kept so each can stand down when the app's
        // own equivalent turns up later in the boot flow.
        private static GameObject _created;
        private static BaseInputModule _createdModule;

        private static bool _hooked;

        // One probe reused for the screen-space test below; a fresh PointerEventData every frame is garbage.
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData _probe;
        private static EventSystem _probeSystem;
        private static int _probeFrame = -1;
        private static Vector2 _probePosition;
        private static bool _probeResult;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // The first scene is already loaded by now and will never raise sceneLoaded for us, so it is
            // handled here; everything after arrives through the hook.
            Ensure();
            SweepRaycasters();
        }

        /// <summary>
        /// Idempotent. Safe to call from an <c>Awake</c>, a <c>Start</c> or every frame; callers should not try
        /// to work out whether it is needed.
        /// </summary>
        public static void Ensure()
        {
            Hook();

            if (_system != null && _module != null && EventSystem.current == _system)
            {
                return;
            }

            EventSystem keeper = null;
            foreach (var candidate in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                // A disabled EventSystem is not in the running for EventSystem.current, so it is not a
                // duplicate either - leave it alone.
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
                // Parented to nothing and kept across loads: the active scene when this runs is often a view
                // scene, and the director unloads those.
                _created = new GameObject("ui_event_system");
                Object.DontDestroyOnLoad(_created);
                keeper = _created.AddComponent<EventSystem>();
            }

            // Same rule as for the EventSystem itself: a module the app brought always beats the one we made.
            // This is not hypothetical - XRI's ray interactors have UI interaction switched on in
            // ge_xr_rig.prefab, and an interactor that finds no XRUIInputModule adds one to the EventSystem by
            // itself. In a headset that one turns up first and we simply adopt it; if it ever turns up second,
            // ours steps aside rather than sitting in front of it in the component order.
            BaseInputModule adopted = null;
            foreach (var candidate in keeper.GetComponents<BaseInputModule>())
            {
                if (candidate != null && candidate != _createdModule)
                {
                    adopted = candidate;
                    break;
                }
            }

            if (adopted != null)
            {
                if (_createdModule != null)
                {
                    Object.Destroy(_createdModule);
                    _createdModule = null;
                }

                _module = adopted;
            }
            else if (_createdModule != null)
            {
                _module = _createdModule;
            }
            else
            {
                // Added this way the module assigns itself the Input System package's default UI actions, so
                // point and click work without an actions asset of ours to keep in sync.
                _createdModule = keeper.gameObject.AddComponent<InputSystemUIInputModule>();
                _module = _createdModule;
            }

            _system = keeper;
        }

        /// <summary>
        /// True when the cursor is over a **screen-space** canvas that can take the click.
        ///
        /// This exists instead of <c>EventSystem.IsPointerOverGameObject()</c> because that answer is true for
        /// any canvas with a <see cref="GraphicRaycaster"/>, and every world-space UI prefab here has one
        /// (<c>UiPrefabBuilder</c> puts it on the canvas it builds). Until this class existed there was no
        /// module, so that call always answered false and the difference never showed. With a module in place
        /// the blanket version would make the desktop mouse stand down over a destination tag, an info panel or
        /// the world dock - the physics raycast would be skipped and hover would never happen - which would
        /// trade dead uGUI buttons for dead labels. World-space UI is hit through <c>GEPointer</c>, so it must
        /// not count as "the UI has this click".
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

            // Asked live rather than reading the module's last-known answer through IsPointerOverGameObject:
            // that answer is a frame behind, covers world-space canvases too, and is worded per pointer id,
            // which each module defines for itself. A raycast we run ourselves has none of those questions.
            // A PointerEventData holds the EventSystem it was made for and will not take another, so it is
            // rebuilt whenever the keeper changes - which happens once, when the boot scene's own turns up.
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
                // GraphicRaycaster requires a Canvas on its own object, and that is the canvas the hit
                // belongs to - a nested canvas would have brought its own raycaster.
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

        /// <summary>Same test at wherever the mouse is now. Desktop only; there is no mouse in a headset.</summary>
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
            Ensure();
            SweepRaycasters();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // The keeper may have gone with the scene. Ensure() is a no-op if it did not.
            Ensure();
        }

        private static bool IsOurs(EventSystem system)
        {
            return _created != null && system != null && system.gameObject == _created;
        }

        private static void Retire(EventSystem system)
        {
            // Disabled first either way: that takes it out of the EventSystem list this frame rather than at
            // the end of it, so EventSystem.current is right immediately.
            system.enabled = false;

            if (IsOurs(system))
            {
                Object.Destroy(system.gameObject);
                _created = null;
                return;
            }

            Debug.LogWarning($"UiEventSystemInstaller: a second EventSystem ('{system.name}') was already in the " +
                             "scene and has been disabled. Unity supports exactly one; check which scene brought it.");
        }

        /// <summary>
        /// An input module alone does not make a canvas clickable - the canvas needs a raycaster of its own.
        /// Both screen-space canvases we ship already have one, so this normally does nothing; it is here so a
        /// canvas built without one fails loudly in the console instead of silently swallowing every press.
        /// Scene load is the only moment new canvases arrive: nothing spawns a screen-space canvas at runtime.
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

                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"UiEventSystemInstaller: screen-space canvas '{canvas.name}' had no GraphicRaycaster " +
                          "and could not be clicked; one was added at runtime. Add it to the prefab.");
            }
        }
    }
}
