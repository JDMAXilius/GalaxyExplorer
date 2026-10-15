// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
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

        [SerializeField]
        [Tooltip("Headset: metres from the eyes to the notice. Nearer than the shell, which parks about 0.8 m out, " +
                 "so a notice in front of the menu is never placed behind it in depth.")]
        private float noticeDistanceMetres = 0.7f;

        [SerializeField]
        [Tooltip("Headset: degrees below the gaze the notice sits, so it reads without covering what is looked at.")]
        private float noticeDropDegrees = 12f;

        [SerializeField]
        [Tooltip("Headset: degrees the gaze may wander from the notice before it glides back under it.")]
        private float noticeFollowDegrees = 25f;

        /// <summary>World size of one 1487 x 1058 design-frame pixel (rail 88 mm, frame 1.15 m). Provisional until U2-P0.</summary>
        public const float MetresPerPixel = 0.00077f;

        // The dock's centre sits this far below the centre of the design frame.
        private const float DockBelowCentrePx = 414.5f;

        // On a monitor the canvas is drawn this far in front of the camera, nearer than anything in the scene.
        private const float ScreenPlaneMetres = 0.3f;

        private Camera _camera;
        private bool _screen;
        private float _noticeUntil;
        private string[][] _contexts;
        private Ui2NoticeAnchor _noticeAnchor;
        private bool _noticeAdopted;
        private readonly ShellTextSize _textSize = new ShellTextSize();

        private GalaxyExplorer.XR.PressEdge _escape;

        public static Ui2Shell Instance { get; private set; }

        public Mode Current { get; private set; } = Mode.Explore;

        public Overlay Open { get; private set; } = Overlay.None;

        /// <summary>Raised when anything the shell shows changes: mode, overlay, notice or frame.</summary>
        public event Action Changed;

        /// <summary>True when the rail, picker and card should be showing.</summary>
        public bool ExploreVisible => Current == Mode.Explore && Open == Overlay.None;

        /// <summary>True while a notice is up, wherever it is drawn.</summary>
        public bool NoticeShowing => notice != null && notice.activeSelf;

        /// <summary>True while a notice is up across the top of the frame, where the breadcrumb gives way to it.</summary>
        public bool NoticeInFrame => NoticeShowing && !_noticeAdopted;

        // GalaxyExplorerManager decides the platform in its Awake; until then Platform reads HoloLensGen1, which is
        // neither answer, so the notice is not moved out of the frame on a guess.
        private static bool InHeadset => GalaxyExplorerManager.IsQuest3;

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

            // The text-size setting reaches the shell (CS-377), but not the rail's and dock's labels or Explore's
            // chips. Read before anything resizes text, so the sizes kept are the design's.
            var fixedLayout = new List<Transform> { rail.transform, explore.transform, guide.transform, room.transform };
            var exploring = GetComponent<Ui2Explore>();
            if (exploring != null)
            {
                fixedLayout.AddRange(exploring.FixedText);
            }

            _textSize.Collect(transform, fixedLayout);
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
            ExperienceDirector.ExperienceStarting += OnPlaceChanged;
            ExperienceDirector.IntroSettled += Evaluate;
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
            ExperienceDirector.ExperienceStarting -= OnPlaceChanged;
            ExperienceDirector.IntroSettled -= Evaluate;
            EnvironmentController.ModeChanged -= OnRoomChanged;
            if (SwitchNotice.Redirect == TakeNotice)
            {
                SwitchNotice.Redirect = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            if (_noticeAnchor != null)
            {
                Destroy(_noticeAnchor.gameObject);
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

        /// <summary>
        /// Shows one line for a few seconds: loading, unavailable, failed, restored. Across the top of the frame on a
        /// monitor; in the headset just below the gaze, so it shows while the frame is hidden (CS-376).
        /// </summary>
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

            Evaluate();
        }

        private void OnRoomChanged(EnvironmentMode mode)
        {
            var env = EnvironmentController.Instance;
            room.SetLabel(env != null && env.EffectiveMode == EnvironmentMode.Passthrough ? "Room on" : "Room off");
        }

        private bool _shownOnce;

        /// <summary>
        /// Shows the shell once the intro is over and a place is open, decided when something changes (the intro
        /// settling, a switch starting or finishing), never by watching every frame. The first time it waits a moment
        /// so it does not flash between intro stages; after that it stays up through every switch, and the new place's
        /// name and card arrive as the switch begins (owner's direction, 14 Oct 2026).
        /// </summary>
        private void Evaluate()
        {
            if (Intro())
            {
                CancelInvoke(nameof(Reveal));
                SetFrame(false);
                return;
            }

            if (_shownOnce)
            {
                SetFrame(true);
            }
            else if (!IsInvoking(nameof(Reveal)))
            {
                Invoke(nameof(Reveal), FirstRevealSeconds);
            }
        }

        private const float FirstRevealSeconds = 1.5f;

        private void Reveal()
        {
            if (!Intro())
            {
                _shownOnce = true;
                SetFrame(true);
            }
        }

        // The intro's real end, not IntroRunning, which gives up after a timeout while the player may still be
        // standing at the intro's OK button.
        private static bool Intro()
        {
            var director = ExperienceDirector.Instance;
            var manager = GalaxyExplorerManager.IsInitialized ? GalaxyExplorerManager.Instance : null;
            return director == null || !director.IntroComplete || director.Current == null ||
                   (manager != null && manager.ViewLoaderScript != null && manager.ViewLoaderScript.IsIntro() &&
                    !string.IsNullOrEmpty(director.Current.SceneName));
        }

        private void SetFrame(bool on)
        {
            if (frame.activeSelf != on)
            {
                frame.SetActive(on);
                Changed?.Invoke();
            }
        }

        private void Update()
        {
            UiButton.Deliver();
            _textSize.Apply(InfoPanel.TextScale, InHeadset);

            if (notice != null && notice.activeSelf && Time.unscaledTime > _noticeUntil)
            {
                notice.SetActive(false);
                Changed?.Invoke();
            }

            // Esc backs out one step on a keyboard: the overlay first, then the guide.
            if (_escape.Read(UnityEngine.InputSystem.Keyboard.current?.escapeKey))
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

            if (!_noticeAdopted && notice != null && InHeadset)
            {
                _noticeAdopted = true;
                _noticeAnchor = Ui2NoticeAnchor.Adopt(notice, GetComponent<Canvas>(), noticeDistanceMetres,
                                                      noticeDropDegrees, noticeFollowDegrees);
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
