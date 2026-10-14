// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using TMPro;
using UnityEngine;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// UI 2.0 shell (docs/design/v2): rail, dock, settings, one focused panel at a time. Drives the existing app through
    /// <see cref="ExperienceDirector"/> and <see cref="EnvironmentController"/>; exists only when <see cref="UiTheme.Enabled"/>.
    /// </summary>
    public class Ui2Shell : MonoBehaviour
    {
        /// <summary>What the dock has selected. Hidden is Explore pressed a second time: the scene alone.</summary>
        public enum Mode { Explore, Guide, Hidden }

        public enum Overlay { None, Settings, Help }

        [SerializeField]
        [Tooltip("Everything the player sees. Off while the introduction is running.")]
        private GameObject frame = null;

        [SerializeField] private GameObject rail = null;
        [SerializeField] private UiButton[] railItems = Array.Empty<UiButton>();

        [SerializeField]
        [Tooltip("One entry per rail item: the place ids that belong to that context, comma separated. " +
                 "The first id is the one the item opens.")]
        private string[] railPlaces = Array.Empty<string>();

        [SerializeField] private string[] railNames = Array.Empty<string>();

        [SerializeField] private UiButton explore = null;
        [SerializeField] private UiButton guide = null;
        [SerializeField] private UiButton room = null;
        [SerializeField] private UiButton settings = null;

        [Header("Notice")]
        [SerializeField] private GameObject notice = null;
        [SerializeField] private TMP_Text noticeText = null;

        /// <summary>World size of one 1487 x 1058 design-frame pixel (rail 88 mm, frame 1.15 m). Provisional until U2-P0.</summary>
        public const float MetresPerPixel = 0.00077f;

        // The dock's centre sits this far below the centre of the design frame.
        private const float DockBelowCentrePx = 414.5f;

        // On a monitor the canvas is drawn this far in front of the camera, nearer than anything in the scene.
        private const float ScreenPlaneMetres = 0.3f;

        private Camera _camera;
        private bool _screen;
        private float _settled;
        private float _noticeUntil;
        private string[][] _contexts;

        public static Ui2Shell Instance { get; private set; }

        public Mode Current { get; private set; } = Mode.Explore;

        public Overlay Open { get; private set; } = Overlay.None;

        /// <summary>Raised when anything the shell shows changes: mode, overlay, notice or frame.</summary>
        public event Action Changed;

        /// <summary>True when the rail, picker and card should be showing.</summary>
        public bool ExploreVisible => Current == Mode.Explore && Open == Overlay.None;

        /// <summary>True while a notice is up; the breadcrumb gives way to it.</summary>
        public bool NoticeShowing => notice != null && notice.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!UiTheme.Enabled)
            {
                return;
            }

            var prefab = Resources.Load<GameObject>("ui2_shell");
            if (prefab == null)
            {
                Debug.LogError("[UI2] ui2_shell prefab is missing. Run Cosmic Simulation > UI 2 > Build Shell Prefab.");
                return;
            }

            DontDestroyOnLoad(Instantiate(prefab));
        }

        private void Awake()
        {
            Instance = this;
            _contexts = new string[railPlaces.Length][];
            for (var i = 0; i < railPlaces.Length; i++)
            {
                _contexts[i] = railPlaces[i].Replace(" ", string.Empty).Split(',');
            }

            SwitchNotice.Redirect = TakeNotice;
            frame.SetActive(false);
        }

        private void Start()
        {
            for (var i = 0; i < railItems.Length; i++)
            {
                var index = i;
                railItems[i].Clicked += () => OpenContext(index);
            }

            explore.Clicked += () => Set(Current == Mode.Explore && Open == Overlay.None ? Mode.Hidden : Mode.Explore);
            guide.Clicked += () => Set(Current == Mode.Guide && Open == Overlay.None ? Mode.Explore : Mode.Guide);
            room.Clicked += () => EnvironmentController.Instance?.TogglePassthrough();
            settings.Clicked += () => Show(Open == Overlay.None ? Overlay.Settings : Overlay.None);

            ExperienceDirector.ExperienceChanged += OnPlaceChanged;
            EnvironmentController.ModeChanged += OnRoomChanged;

            if (notice != null)
            {
                notice.SetActive(false);
            }

            OnPlaceChanged(ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null);
            OnRoomChanged(default);
            Apply();
        }

        private void OnDestroy()
        {
            ExperienceDirector.ExperienceChanged -= OnPlaceChanged;
            EnvironmentController.ModeChanged -= OnRoomChanged;
            if (SwitchNotice.Redirect == TakeNotice)
            {
                SwitchNotice.Redirect = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Set(Mode mode)
        {
            Current = mode;
            Open = Overlay.None;
            Apply();
        }

        /// <summary>Opens Settings or Help over the current mode, or closes it with <see cref="Overlay.None"/>.</summary>
        public void Show(Overlay overlay)
        {
            Open = overlay;
            Apply();
        }

        /// <summary>Shows one line across the top for a few seconds: loading, unavailable, failed, restored.</summary>
        public void Notify(string message, float seconds = 4f)
        {
            if (notice == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            noticeText.text = message;
            notice.SetActive(true);
            _noticeUntil = Time.unscaledTime + seconds;
            Changed?.Invoke();
        }

        private bool TakeNotice(string message)
        {
            Notify(message);
            return true;
        }

        public int ContextOf(string placeId)
        {
            if (placeId == null || _contexts == null)
            {
                return -1;
            }

            for (var i = 0; i < _contexts.Length; i++)
            {
                if (Array.IndexOf(_contexts[i], placeId) >= 0)
                {
                    return i;
                }
            }

            return -1;
        }

        public string ContextName(int index) =>
            index >= 0 && index < railNames.Length ? railNames[index] : string.Empty;

        /// <summary>The place a context opens, which is also its top level (the map, the row of planets).</summary>
        public string ContextHome(int index) =>
            index >= 0 && _contexts != null && index < _contexts.Length ? _contexts[index][0] : null;

        private void OpenContext(int index)
        {
            var home = ContextHome(index);
            if (home != null)
            {
                ExperienceDirector.Instance?.Switch(home);
            }
        }

        private void Apply()
        {
            rail.SetActive(ExploreVisible);
            explore.Selected = Current == Mode.Explore;
            guide.Selected = Current == Mode.Guide;
            settings.Selected = Open == Overlay.Settings;
            Changed?.Invoke();
        }

        private void OnPlaceChanged(ExperienceModule module)
        {
            var context = ContextOf(module != null ? module.Id : null);
            for (var i = 0; i < railItems.Length; i++)
            {
                railItems[i].Selected = i == context;
            }
        }

        private void OnRoomChanged(EnvironmentMode mode)
        {
            var env = EnvironmentController.Instance;
            room.SetLabel(env != null && env.EffectiveMode == EnvironmentMode.Passthrough ? "Room on" : "Room off");
        }

        private void Update()
        {
            UiButton.Deliver();

            // Shown once the intro is over and a place is open, held a moment so it does not flash between intro stages.
            var director = ExperienceDirector.Instance;
            var manager = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance : null;
            // The intro's real end, not IntroRunning, which gives up after a timeout while the player may
            // still be standing at the intro's OK button.
            var intro = director == null || !director.IntroComplete || director.Current == null ||
                        director.IsSwitching ||
                        (manager != null && manager.ViewLoaderScript != null && manager.ViewLoaderScript.IsIntro() &&
                         !string.IsNullOrEmpty(director.Current.SceneName));
            _settled = intro ? 0f : _settled + Time.unscaledDeltaTime;
            var ready = _settled > 1.5f;
            if (frame.activeSelf != ready)
            {
                frame.SetActive(ready);
                Changed?.Invoke();
            }

            if (notice != null && notice.activeSelf && Time.unscaledTime > _noticeUntil)
            {
                notice.SetActive(false);
                Changed?.Invoke();
            }

            // Esc backs out one step on a keyboard: the overlay first, then the guide.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Open != Overlay.None)
                {
                    Show(Overlay.None);
                }
                else if (Current == Mode.Guide)
                {
                    Set(Mode.Explore);
                }
            }
        }

        // On a monitor the prefab becomes a screen-space canvas; its groups are edge-anchored, so a different
        // window shape spreads them out instead of cropping them.
        private void FitToScreen()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
                _screen = false;
            }

            if (_camera == null || _screen)
            {
                return;
            }

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = Mathf.Max(ScreenPlaneMetres, _camera.nearClipPlane * 1.5f);
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1487f, 1058f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            _screen = true;
        }

        private void LateUpdate()
        {
            // The legacy docks own parking, dragging and recentering for now, so the shell rides on the world
            // dock's pose and keeps both legacy docks down. On a monitor it rides on the camera instead.
            var dock = DockController.Instance;
            if (dock != null)
            {
                dock.SetVisible(false);
            }

            if (DesktopDock.Instance != null)
            {
                // Not remembered: UI 2.0 hiding the legacy dock is not the player pressing Tab.
                DesktopDock.Instance.SetVisible(false, remember: false);
            }

            if (GalaxyExplorerManager.IsDesktop)
            {
                FitToScreen();
            }
            else if (dock != null)
            {
                // The frame is placed so that its dock lands where the legacy dock is parked.
                var pose = dock.transform;
                transform.SetPositionAndRotation(
                    pose.position + pose.up * (DockBelowCentrePx * MetresPerPixel), pose.rotation);
            }
        }
    }
}
