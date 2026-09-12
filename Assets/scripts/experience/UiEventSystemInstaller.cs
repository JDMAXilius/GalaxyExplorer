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
    /// The app shipped without a usable one. <c>main_camera_prefab</c> carries an <see cref="EventSystem"/>, and
    /// <c>core_systems_scene</c> adds an <c>XRUIInputModule</c> to that instance with every action reference left
    /// empty — enough for uGUI to draw a button and never deliver a press. Every button on the desktop menu and
    /// the desktop dock was dead. Nothing noticed because every *other* pointer in this project goes through
    /// <c>GEPointer</c> and a physics raycast, which does not involve the EventSystem.
    ///
    /// Why it installs from a boot hook rather than from whoever needs it: the app's EventSystem arrives with
    /// <c>core_systems_scene</c>, which is loaded well into the boot flow rather than at frame 0 (<c>main_scene</c>
    /// is build index 0 and carries only a LayerCompositor, which loads the boot scene a frame later). An
    /// installer that ran once at startup would find nothing, make its own, and then be sitting next to a second
    /// EventSystem the moment the boot scene landed. So this runs at startup *and* on every scene load, and it
    /// prefers the app's own EventSystem over the one it made: whichever shows up, the invariant holds.
    ///
    /// Module choice: <see cref="InputSystemUIInputModule"/>. Active Input Handling is "Both" (Android needs
    /// the legacy backend for TouchScript), so <c>StandaloneInputModule</c> would compile - but it reads
    /// <c>UnityEngine.Input</c>, while every pointer this project actually reads comes from the Input System
    /// (<c>Mouse.current</c> in <c>DesktopMouseInput</c>, XRI 3.6 throughout). Two backends reading the cursor
    /// is how you get a click that lands somewhere the hover was not. XRI's <c>XRUIInputModule</c> is the other
    /// candidate and is deliberately not used: it drives world-space uGUI from the hand ray through a
    /// <c>TrackedDeviceGraphicRaycaster</c>, and there is not one in this project — world-space UI here is hit
    /// with physics through <c>GEPointer</c>, not with graphic raycasts. So ours is the module of record and any
    /// other module on the keeper is switched off; see <see cref="Ensure"/> for why switching off is required
    /// rather than merely adding ours.
    /// </summary>
    public static class UiEventSystemInstaller
    {
        // What we have already settled on, so the common path is three reference compares rather than a scan.
        private static EventSystem _system;
        private static BaseInputModule _module;

        // The EventSystem we created, if we had to. Kept so it can stand down when the app's own turns up later
        // in the boot flow.
        private static GameObject _created;

        // Set whenever a scene load or unload could have brought an EventSystem or taken one away. Without it the
        // cheap guard below is never false after the first call - our own EventSystem satisfies it - and the scan
        // that retires the duplicate would never run again, which is exactly what happened: the app's second
        // EventSystem arrives one frame after ours and was never noticed.
        private static bool _scanPending = true;

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
            // Explicit rather than relying on the field initialiser: with Enter Play Mode reload turned off the
            // statics survive from the last session, and a stale "nothing has changed" would skip the first scan.
            _scanPending = true;

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

            // The scan is skipped only while nothing can have changed. Every scene load and unload clears that,
            // because a scene is the only thing that brings an EventSystem with it.
            if (!_scanPending && _system != null && _module != null && EventSystem.current == _system)
            {
                return;
            }

            _scanPending = false;

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

            // Unlike the EventSystem, the module is not up for adoption: ours is the one that runs, and a module
            // the app brought stands down. The app's is an XRUIInputModule, authored onto the camera prefab
            // instance in core_systems_scene with every action reference empty, and XRI's ray interactors add one
            // themselves if they ever find none (UI interaction is on in ge_xr_rig.prefab). Adopting it is what
            // left every screen-space button dead in the first place.
            //
            // Switching it off is required, not tidiness: an EventSystem runs the *first* module in component
            // order that wants to activate, and the authored XRUIInputModule is earlier in that order than
            // anything added at runtime, so simply adding ours next to it changes nothing.
            //
            // Nothing is lost by it. XRUIInputModule reaches world-space uGUI through TrackedDeviceGraphicRaycaster
            // and this project has none - world-space UI is hit with physics through GEPointer - and its pointer
            // path needs the action references the authored instance does not have. Disabled rather than
            // destroyed, so XRI still finds it and does not go looking for somewhere to add another.
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

        /// <summary>
        /// Gives a runtime-added module the package's default UI actions if it did not come up with any.
        ///
        /// <c>AddComponent</c> is not the editor's Add Component: the editor path runs <c>Reset</c>, which is
        /// where the default actions are normally assigned, and <c>Reset</c> never runs at runtime. A module with
        /// a null actions asset reads no pointer at all, which is the dead-button bug this class exists to fix,
        /// wearing a different hat. Cheap to ask, so it is asked rather than assumed.
        /// </summary>
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
                // Never worth taking the app down for: without actions the screen-space UI is dead, which is bad,
                // but the headset does not use it at all.
                Debug.LogWarning("UiEventSystemInstaller: the UI input module came up with no actions and the " +
                                 "package defaults could not be assigned, so screen-space clicks will not be " +
                                 "delivered. " + e.Message);
            }
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
            _scanPending = true;
            Ensure();
            SweepRaycasters();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // The keeper may have gone with the scene, and a scan is the only way to find out.
            _scanPending = true;
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
                // Takes our module with it, which is why nothing else has to remember it.
                Object.Destroy(system.gameObject);
                _created = null;
                _module = null;
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

                // Only canvases that could take a press. A canvas of pure text wants no raycaster and gets none:
                // SwitchNotice builds one deliberately without, so it can never stand between the player and the
                // tile they are about to poke, and telling its author to "add it to the prefab" would be advice
                // about an object with no prefab and a console line that is simply untrue.
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
