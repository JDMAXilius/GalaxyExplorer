// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The one line the player gets when a place does not open.
    ///
    /// Until now a refused or failed switch was silent: <see cref="ExperienceDirector"/> logged it and handed the
    /// room back, and a player poking a tile that cannot open saw nothing happen at all. Four of the seven places
    /// have no content yet, so that silence is what most players would meet first.
    ///
    /// Two wordings, because the two cases are not the same thing. A place with neither a scene nor a content
    /// prefab is <b>not built yet</b> — nothing is wrong, it simply arrives later — and it is refused before
    /// anything in the room moves. A place that was meant to open and did not is a <b>failure</b>, and by the time
    /// we know it the previous place is already unloaded and the room has come back as passthrough, which the
    /// player deserves an explanation for.
    ///
    /// There is no prefab behind this. It builds its own canvas the first time it is asked to say something, so
    /// nothing has to be wired in the editor for the message to appear, and a message is never lost because a
    /// scene forgot to carry the object. It follows the two presentation conventions the project already has: in
    /// the headset a world-space canvas on the millimetre scale, parked above the dock, white text with a dark
    /// outline and <b>no plate</b> — over passthrough a plate punches a hole in the room (GDD 8.3), the same
    /// reason <see cref="InfoPanel"/> has none; on the desktop a screen-space line above the
    /// <see cref="DesktopDock"/>, which is where that mode already puts its UI.
    ///
    /// It can neither wedge the dock nor swallow a poke: the canvas has no raycaster, the text is not a raycast
    /// target, and the notice dismisses itself after a few seconds, on Escape, or the moment another place opens.
    /// </summary>
    [DisallowMultipleComponent]
    public class SwitchNotice : MonoBehaviour
    {
        [Header("Copy — docs/copy/ui.md, Messages")]
        [SerializeField]
        [TextArea(1, 3)]
        [Tooltip("Shown when a place has no content yet. {0} is the place's name.")]
        private string notReadyMessage = "{0} is not ready yet. It arrives in a later update.";

        [SerializeField]
        [TextArea(1, 3)]
        [Tooltip("Shown when a place was meant to open and could not. {0} is the place's name.")]
        private string failedMessage = "{0} did not open. Your room is back - pick another place from the menu.";

        [SerializeField]
        [Tooltip("Stands in for the name when a module has none, so the sentence still reads.")]
        private string unnamedPlace = "That place";

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds the notice stays up before it fades itself away.")]
        private float holdSeconds = 5f;

        [SerializeField]
        [Tooltip("Seconds to fade in or out.")]
        private float fadeSeconds = 0.3f;

        [Header("In the headset")]
        [SerializeField]
        [Tooltip("Metres above the dock the notice parks, measured along the dock's own tilted up axis.")]
        private float dockLiftMetres = 0.16f;

        [SerializeField]
        [Tooltip("Metres in front of the player when there is no dock to sit above.")]
        private float headDistanceMetres = 1.2f;

        [SerializeField]
        [Tooltip("Metres above eye level when there is no dock to sit above; negative is below it.")]
        private float headDropMetres = -0.15f;

        [SerializeField]
        [Tooltip("Width of the message, in canvas units. One unit is one millimetre on this canvas.")]
        private float worldWidthUnits = 360f;

        [SerializeField]
        [Tooltip("Height of the message, in canvas units — millimetres. Two lines' worth.")]
        private float worldHeightUnits = 48f;

        [SerializeField]
        [Tooltip("Type size in canvas units — millimetres of cap-to-descender at the parked distance.")]
        private float worldFontSizeUnits = 12f;

        [Header("On the desktop")]
        [SerializeField]
        [Tooltip("Pixels above the bottom of the screen, on a 1920x1080 reference. The dock plate ends at 106.")]
        private float desktopBottomOffsetPixels = 140f;

        [SerializeField]
        [Tooltip("Width of the message in reference pixels.")]
        private float desktopWidthPixels = 900f;

        [SerializeField]
        [Tooltip("Height of the message in reference pixels.")]
        private float desktopHeightPixels = 60f;

        [SerializeField]
        [Tooltip("Type size in reference pixels.")]
        private float desktopFontSizePixels = 22f;

        private GameObject _root;
        private CanvasGroup _group;
        private TMP_Text _label;
        private Camera _camera;
        private bool _built;
        private bool _desktop;
        private bool _showing;
        private float _shownFor;

        public static SwitchNotice Instance { get; private set; }

        /// <summary>True while a message is on screen or still fading out.</summary>
        public bool IsShowing => _showing;

        // ---------- what the director calls

        /// <summary>Says that a place has no content yet. Not an error: it has not been built.</summary>
        public static void NotReady(ExperienceModule module)
        {
            var notice = Resolve();
            if (notice != null)
            {
                notice.Show(notice.Compose(notice.notReadyMessage, module));
            }
        }

        /// <summary>Says that a place was meant to open and did not, which is why the room came back.</summary>
        public static void FailedToOpen(ExperienceModule module)
        {
            var notice = Resolve();
            if (notice != null)
            {
                notice.Show(notice.Compose(notice.failedMessage, module));
            }
        }

        /// <summary>
        /// Puts the place's name into the message. A plain replace rather than <c>string.Format</c>: these
        /// strings are editable in the inspector and the copy deck, and a stray brace typed into one would
        /// otherwise throw a FormatException from inside the very failure path this exists to report.
        /// </summary>
        private string Compose(string template, ExperienceModule module)
        {
            // Never the Id: it is an internal key, and the copy deck says nothing is called by its internal name.
            var place = module != null && !string.IsNullOrEmpty(module.DisplayName)
                ? module.DisplayName
                : unnamedPlace;

            return string.IsNullOrEmpty(template) ? place : template.Replace("{0}", place);
        }

        /// <summary>
        /// Finds the notice, or makes one. Nothing in the project wires this up, and nothing needs to.
        /// </summary>
        private static SwitchNotice Resolve()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Instance is set in Awake, which has not necessarily run for an object that spawned this frame, so
            // look for an existing one before making a second. Inactive ones are deliberately not adopted: the
            // fade and the auto-dismiss are driven from Update, which an inactive object never gets.
            var found = FindAnyObjectByType<SwitchNotice>();
            if (found != null)
            {
                Instance = found;
                return found;
            }

            var go = new GameObject("switch_notice");

            // The director unloads whole view scenes out from under itself on every switch, and a notice that
            // lived in one would be destroyed mid-sentence — or worse, right as it was asked to speak.
            DontDestroyOnLoad(go);

            var notice = go.AddComponent<SwitchNotice>();
            Instance = notice;
            return notice;
        }

        // ---------- showing

        /// <summary>Puts a line of text in front of the player. Re-showing restarts the clock.</summary>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            EnsureBuilt();
            if (_label == null || _root == null)
            {
                return;
            }

            _label.text = message;
            _root.SetActive(true);
            Park();

            _showing = true;
            _shownFor = 0f;
        }

        /// <summary>Fades the notice away now.</summary>
        public void Dismiss() => _showing = false;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ExperienceDirector.ExperienceChanged -= OnExperienceChanged;
        }

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            if (_showing)
            {
                // Unscaled, like everything else the director times: a message about a failure should not depend
                // on a time scale the failure may have left somewhere odd.
                _shownFor += Time.unscaledDeltaTime;

                var keyboard = Keyboard.current;
                if (_shownFor >= holdSeconds ||
                    (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
                {
                    _showing = false;
                }
            }

            var wanted = _showing ? 1f : 0f;
            if (!Mathf.Approximately(_group.alpha, wanted))
            {
                _group.alpha = fadeSeconds <= 0f
                    ? wanted
                    : Mathf.MoveTowards(_group.alpha, wanted, Time.unscaledDeltaTime / fadeSeconds);
            }

            // Switched off once it is invisible rather than left drawing a transparent canvas every frame.
            if (!_showing && _group.alpha <= 0.001f && _root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>A place opened, so whatever we were saying about the last one is stale.</summary>
        private void OnExperienceChanged(ExperienceModule module)
        {
            if (module != null)
            {
                Dismiss();
            }
        }

        // ---------- placement

        private void Park()
        {
            if (_desktop || _root == null)
            {
                return;
            }

            var dock = DockController.Instance;
            if (dock != null && dock.IsVisible)
            {
                // The player was looking at the dock when they poked the tile, so the answer goes where they are
                // already looking: just above it, sharing its tilt. Parked, not head-following — a notice that
                // chases the player cannot be looked away from, which is the rule the dock itself follows.
                var dockTransform = dock.transform;
                _root.transform.SetPositionAndRotation(
                    dockTransform.position + dockTransform.up * dockLiftMetres,
                    dockTransform.rotation);
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var head = _camera.transform;

            // Flattened, for the same reason the dock flattens it: pitched with the head the notice ends up on
            // the floor or the ceiling.
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            _root.transform.SetPositionAndRotation(
                head.position + forward * headDistanceMetres + Vector3.up * headDropMetres,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        // ---------- the canvas, built on first use

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            // Read here rather than in Awake: GalaxyExplorerManager decides the platform in its own Awake, and
            // until it has, PlatformId is HoloLensGen1 rather than anything true. Nothing asks for a notice
            // before the app is running, so by now the answer is honest. (Same reasoning as DesktopDock.Start.)
            _desktop = GalaxyExplorerManager.IsDesktop;

            ExperienceDirector.ExperienceChanged += OnExperienceChanged;

            var font = BorrowFont();

            _root = new GameObject("switch_notice_canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)_root.transform;
            canvasRect.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();

            _group = _root.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // No GraphicRaycaster and nothing interactable: a notice that could take a click would be a notice
            // that could stand between the player and the tile they want to poke next. UiEventSystemInstaller
            // sweeps screen-space canvases for a missing raycaster and leaves this one alone precisely because
            // nothing under it handles an event - so keep it that way, or it will be handed one.
            _group.interactable = false;
            _group.blocksRaycasts = false;

            if (_desktop)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60; // above the desktop dock's 50, so the message is never behind it

                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace;
                _root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

                canvasRect.sizeDelta = new Vector2(worldWidthUnits, worldHeightUnits);

                // One canvas unit is one millimetre, the scale every world-space UI prefab in this project is
                // authored on. Divided by the parent's scale so the notice keeps one physical size even if the
                // component was dropped under a scaled rig rather than on the object Resolve makes.
                var parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
                canvasRect.localScale = Vector3.one * (0.001f / parentScale);
            }

            _label = BuildLabel(canvasRect, font);
            _root.SetActive(false);
        }

        private TMP_Text BuildLabel(RectTransform parent, TMP_FontAsset font)
        {
            var go = new GameObject("message", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }

            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            // No plate behind it, so the outline is what keeps white text readable against a bright wall as well
            // as a black sky — the same treatment InfoPanel and the other world-space text get (GDD 8.3).
            label.outlineWidth = 0.12f;
            label.outlineColor = new Color32(0x06, 0x0B, 0x10, 0xFF);

            if (_desktop)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(desktopWidthPixels, desktopHeightPixels);
                rect.anchoredPosition = new Vector2(0f, desktopBottomOffsetPixels);
                label.fontSize = desktopFontSizePixels;
            }
            else
            {
                rect.sizeDelta = new Vector2(worldWidthUnits, worldHeightUnits);
                rect.anchoredPosition = Vector2.zero;
                label.fontSize = worldFontSizeUnits;
            }

            return label;
        }

        /// <summary>
        /// The font the rest of the app is already using, so a notice does not arrive in a different typeface.
        /// Nothing is loaded by path: <c>TextMeshProUGUI</c> falls back to its own default when this finds
        /// nothing, which is a plain typeface rather than no text at all.
        /// </summary>
        private static TMP_FontAsset BorrowFont()
        {
            var existing = FindAnyObjectByType<TMP_Text>();
            return existing != null ? existing.font : null;
        }
    }
}
