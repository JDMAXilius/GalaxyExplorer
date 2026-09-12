// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The two one-time hint cards of GDD 8.6, shown one after the other once the intro has placed the Earth pin.
    ///
    /// The intro teaches itself: the original onboarding puts a white line-art hand in the room, cross-fades it
    /// between an open hand and a pinch on a Timeline, and says nothing in writing. These cards are that idea with
    /// a line of text under it - the same sprites, the same cross-fade, no plate behind them - so the first two
    /// things the player is told do not arrive in a third visual language. There is no Timeline and no Animator:
    /// two frames and a sine are cheaper than a playable asset per card and survive being re-sized by the copy
    /// deck changing its mind about how many frames a gesture needs.
    ///
    /// Nothing wires this up. Like <see cref="SwitchNotice"/> it builds its own canvas the first time it is asked
    /// to say something, and the copy and art come from a <see cref="HintCardSet"/> in <c>Resources</c>, so the
    /// intro only has to call <see cref="ShowIfFirstRun"/> and Help only has to call <see cref="Replay"/>.
    ///
    /// Both presentations the app has to support are here, because the card is a card in both: in the headset a
    /// world-space canvas on the millimetre scale, parked in front of the player and pokeable; on the desktop a
    /// screen-space panel low on screen that a left click or the space bar dismisses. Every way of answering a
    /// card has a mouse or keyboard twin - the gesture, the poke, and the six-second timeout.
    /// </summary>
    [DisallowMultipleComponent]
    public class HintCards : MonoBehaviour
    {
        /// <summary>
        /// Set the first time the cards run. Follows the three keys the app already stores (experience mode, mute,
        /// desktop dock visibility): a plain int under the product's own prefix.
        /// </summary>
        public const string SeenPrefsKey = "CosmicSimulation.HintsSeen";

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Seconds a card takes to fade in or out.")]
        private float fadeSeconds = 0.35f;

        [SerializeField]
        [Tooltip("Seconds after a grab before the held object's size is taken as its starting size. A force-pulled " +
                 "planet is still growing when the hand closes on it, and that growth is not the player resizing it.")]
        private float resizeSettleSeconds = 0.5f;

        [SerializeField]
        [Tooltip("Fraction the held object has to change size by before the resize card counts as answered.")]
        private float resizeFraction = 0.12f;

        [Header("In the headset")]
        [SerializeField]
        [Tooltip("Metres in front of the player a card parks when there is no dock to sit above.")]
        private float headDistanceMetres = 1.1f;

        [SerializeField]
        [Tooltip("Metres above eye level; negative is below it. Low enough that the card is not between the " +
                 "player's hand and the thing the card is telling them to grab.")]
        private float headDropMetres = -0.28f;

        [SerializeField]
        [Tooltip("Metres above the dock a replayed card parks, along the dock's own tilted up axis.")]
        private float dockLiftMetres = 0.22f;

        [SerializeField]
        [Tooltip("Degrees the card tilts back toward the player's face, as the dock does (GDD 8.1).")]
        private float tiltDegrees = 20f;

        [SerializeField]
        [Tooltip("Card width in canvas units. One unit is one millimetre on this canvas; GDD 8.6 says 200.")]
        private float worldWidthUnits = 200f;

        [SerializeField]
        [Tooltip("Card height in canvas units - millimetres. GDD 8.6 says 120.")]
        private float worldHeightUnits = 120f;

        [SerializeField]
        [Tooltip("Height of the animation band at the top of the card, in canvas units - millimetres.")]
        private float worldArtHeightUnits = 62f;

        [SerializeField]
        [Tooltip("Title size in canvas units - millimetres of type at the parked distance.")]
        private float worldTitleSizeUnits = 13f;

        [SerializeField]
        [Tooltip("Body line size in canvas units - millimetres.")]
        private float worldLineSizeUnits = 8f;

        [Header("On the desktop")]
        [SerializeField]
        [Tooltip("Pixels from the bottom of the screen, on a 1920x1080 reference. The dock plate ends at 106.")]
        private float desktopBottomOffsetPixels = 150f;

        [SerializeField]
        [Tooltip("Card width in reference pixels.")]
        private float desktopWidthPixels = 520f;

        [SerializeField]
        [Tooltip("Card height in reference pixels.")]
        private float desktopHeightPixels = 312f;

        [SerializeField]
        [Tooltip("Height of the animation band, in reference pixels.")]
        private float desktopArtHeightPixels = 161f;

        [SerializeField]
        [Tooltip("Title size in reference pixels.")]
        private float desktopTitleSizePixels = 34f;

        [SerializeField]
        [Tooltip("Body line size in reference pixels.")]
        private float desktopLineSizePixels = 21f;

        private enum Phase
        {
            Idle,
            In,
            Hold,
            Out,
        }

        private readonly List<Image> _leftFrames = new List<Image>();
        private readonly List<Image> _rightFrames = new List<Image>();
        private readonly List<ManipulationHandler> _watched = new List<ManipulationHandler>();

        private HintCard[] _cards;
        private GameObject _root;
        private CanvasGroup _group;
        private TMP_Text _title;
        private TMP_Text _line;
        private RectTransform _leftArt;
        private RectTransform _rightArt;
        private Vector2 _leftHome;
        private Vector2 _rightHome;
        private Camera _camera;

        private bool _built;
        private bool _desktop;
        private float _unitScale = 1f;

        private Phase _phase = Phase.Idle;
        private int _index;
        private float _elapsed;
        private float _animTime;
        private bool _advanceRequested;
        private bool _actionDone;

        private ManipulationHandler _held;
        private float _heldFor;
        private float _heldStartSize = -1f;

        public static HintCards Instance { get; private set; }

        /// <summary>True while a card is on screen or still fading.</summary>
        public bool IsShowing => _phase != Phase.Idle;

        /// <summary>
        /// Whether this player has already been through the hints. Settable so a "reset first run" path, a test,
        /// or a future About panel can put it back without going through <see cref="Replay"/>.
        /// </summary>
        public static bool HaveBeenSeen
        {
            get => PlayerPrefs.GetInt(SeenPrefsKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SeenPrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // ---------- what other systems call

        /// <summary>
        /// Runs the hints if this player has not seen them. The intro calls this once the Earth pin is placed
        /// (GDD 2.1); calling it again later is free, because the second call does nothing.
        /// </summary>
        public static void ShowIfFirstRun()
        {
            var cards = Resolve();
            if (cards != null)
            {
                cards.Begin(false);
            }
        }

        /// <summary>
        /// Runs the hints whether or not they have been seen. This is what the dock's Help button is for
        /// (GDD 2.1, F-32): one line, no arguments, no state for the caller to keep.
        /// </summary>
        public static void Replay()
        {
            var cards = Resolve();
            if (cards != null)
            {
                cards.Begin(true);
            }
        }

        /// <summary>Forgets that the hints were seen, so the next launch shows them again. Does not show them now.</summary>
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(SeenPrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Takes the current card down and abandons the rest of the sequence.</summary>
        public void Stop() => Finish();

        /// <summary>Answers the current card as if it had been poked or clicked. Public so a dock or a menu can.</summary>
        public void RequestAdvance() => _advanceRequested = true;

        // ---------- lifetime

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            UnwatchManipulation();
            DockController.Moved -= OnDockMoved;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // A replayed card parks above the dock (see Park), and the dock can now be carried away from under it
        // by the bar beneath it (CS-108, CS-121). Only while a card is up; Park re-runs at every ShowCurrent.
        private void OnDockMoved()
        {
            if (_phase != Phase.Idle)
            {
                Park();
            }
        }

        /// <summary>
        /// Finds the hint cards, or makes them. Nothing in the project wires this up and nothing needs to, which
        /// is also what keeps the intro's side of this to a single call.
        /// </summary>
        private static HintCards Resolve()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // Instance is set in Awake, which has not necessarily run for an object that spawned this frame, so
            // look before making a second. Inactive ones are deliberately not adopted: the sequence is driven
            // from Update, which an inactive object never gets, and a stalled card never advances.
            var found = FindAnyObjectByType<HintCards>();
            if (found != null)
            {
                Instance = found;
                return found;
            }

            var go = new GameObject("hint_cards");

            // The intro unloads its own scenes on the way to the galaxy, and a card that lived in one would be
            // destroyed halfway through the sentence that is teaching the player to play the game.
            DontDestroyOnLoad(go);

            var cards = go.AddComponent<HintCards>();
            Instance = cards;
            return cards;
        }

        // ---------- the sequence

        private void Begin(bool force)
        {
            if (!force && HaveBeenSeen)
            {
                return;
            }

            if (!EnsureCards())
            {
                return;
            }

            EnsureBuilt();
            if (_root == null)
            {
                return;
            }

            // Marked on the way in rather than on the way out. The intro plays on every launch, so a player who
            // takes the headset off during the hints would otherwise meet them again every single time; Help
            // replays them for anyone who wanted a second look.
            HaveBeenSeen = true;

            _index = 0;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            UnwatchManipulation();

            if (_cards == null || _index < 0 || _index >= _cards.Length)
            {
                Finish();
                return;
            }

            var card = _cards[_index];
            BuildFrames(card);

            _title.text = card.Title ?? string.Empty;
            _line.text = card.Line ?? string.Empty;

            _advanceRequested = false;
            _actionDone = false;
            _held = null;
            _heldStartSize = -1f;
            _animTime = 0f;
            _elapsed = 0f;
            _phase = Phase.In;

            _root.SetActive(true);
            Park();

            if (card.DismissOn != HintAction.None)
            {
                WatchManipulation();
            }
        }

        private void Finish()
        {
            UnwatchManipulation();
            _phase = Phase.Idle;
            _held = null;

            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void Update()
        {
            if (_phase == Phase.Idle || _cards == null || _index >= _cards.Length)
            {
                return;
            }

            // Unscaled, like everything else onboarding times: the intro's own transitions have been known to
            // leave a time scale somewhere odd, and a hint that never times out is a hint that never goes away.
            var dt = Time.unscaledDeltaTime;
            var card = _cards[_index];

            _animTime += dt;
            Animate(card);

            switch (_phase)
            {
                case Phase.In:
                    if (Fade(1f, dt))
                    {
                        _phase = Phase.Hold;
                        _elapsed = 0f;
                    }

                    break;

                case Phase.Hold:
                    _elapsed += dt;
                    ReadInput(card);
                    WatchHeldObject(card, dt);

                    if (_advanceRequested || _actionDone || _elapsed >= Mathf.Max(0.5f, card.HoldSeconds))
                    {
                        _phase = Phase.Out;
                    }

                    break;

                case Phase.Out:
                    if (Fade(0f, dt))
                    {
                        _index++;
                        ShowCurrent();
                    }

                    break;
            }
        }

        private bool Fade(float target, float dt)
        {
            if (_group == null)
            {
                return true;
            }

            _group.alpha = fadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_group.alpha, target, dt / fadeSeconds);

            return Mathf.Approximately(_group.alpha, target);
        }

        // ---------- answering a card

        /// <summary>
        /// The mouse and keyboard half of dismissing a card, and the desktop half of performing the gesture.
        /// A poke or a far pinch in the headset arrives separately, through the card's own <see cref="GEButton"/>.
        /// </summary>
        private void ReadInput(HintCard card)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.escapeKey.wasPressedThisFrame))
            {
                _advanceRequested = true;
            }

            if (!_desktop)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // On the desktop a left click *is* the grab - DesktopMouseInput pulls what it is over - so the click
            // that answers the first card and the click that dismisses it are the same click either way.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _advanceRequested = true;
            }

            // And the wheel is the two-handed scale (DesktopMouseInput.HandleWheel), which never goes through a
            // ManipulationHandler pointer, so the resize card has to watch for it here or never see it.
            if (card.DismissOn == HintAction.Resize && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
            {
                _actionDone = true;
            }
        }

        /// <summary>
        /// Listens to every <see cref="ManipulationHandler"/> in the scene for the duration of one card. There is
        /// no app-wide "something was grabbed" signal to subscribe to instead, and polling every handler every
        /// frame would cost more than binding them twice in a session.
        /// </summary>
        private void WatchManipulation()
        {
            // Inactive included: a force-pulled body's handler is disabled until ForceSolver reaches Manipulation,
            // and that is exactly the grab this is waiting for.
            foreach (var handler in FindObjectsByType<ManipulationHandler>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (handler == null || IsDockFurniture(handler))
                {
                    continue;
                }

                handler.OnManipulationStarted.AddListener(OnManipulationStarted);
                handler.OnManipulationEnded.AddListener(OnManipulationEnded);
                _watched.Add(handler);
            }
        }

        private void UnwatchManipulation()
        {
            foreach (var handler in _watched)
            {
                if (handler == null)
                {
                    continue;
                }

                handler.OnManipulationStarted.RemoveListener(OnManipulationStarted);
                handler.OnManipulationEnded.RemoveListener(OnManipulationEnded);
            }

            _watched.Clear();
        }

        /// <summary>
        /// Whether a handler belongs to the dock rather than to something in the sky. The cards teach grabbing
        /// and resizing the *content*, so dock furniture must not answer them (CS-120): since the drag bar got
        /// its pointer router, a pinch on the bar satisfied "pull a planet toward you" without a planet.
        ///
        /// The test is the hierarchy and not <c>ManipulationType</c>. One-handed happens to be unique to the
        /// bar today, but it is also the enum's zero value, so any future handler left at the YAML default
        /// would read as dock furniture and stop answering the cards for no visible reason.
        /// </summary>
        private static bool IsDockFurniture(ManipulationHandler handler)
        {
            var dock = DockController.Instance;
            return handler != null && dock != null && handler.transform.IsChildOf(dock.transform);
        }

        private void OnManipulationStarted(ManipulationEventData data)
        {
            if (_phase == Phase.Idle || _cards == null || _index >= _cards.Length)
            {
                return;
            }

            // Checked again here, not only where the listeners are bound: a dock that came into existence after
            // the card went up was not there to be excluded by the subscription.
            var handler = data != null && data.ManipulationSource != null
                ? data.ManipulationSource.GetComponent<ManipulationHandler>()
                : null;

            if (handler == null || IsDockFurniture(handler))
            {
                return;
            }

            var card = _cards[_index];
            if (card.DismissOn == HintAction.Grab)
            {
                _actionDone = true;
                return;
            }

            if (card.DismissOn != HintAction.Resize)
            {
                return;
            }

            _held = handler;
            _heldFor = 0f;
            _heldStartSize = -1f;
        }

        // Only the release of the thing we are actually measuring counts. This used to clear unconditionally,
        // so with a nebula held in one hand and the resize card up, a pinch and release on the dock's drag bar
        // with the other hand threw away the measurement mid-gesture (CS-120). A release we cannot attribute is
        // left alone for the same reason: the card times out on its own, and a dropped measurement is worse
        // than a stale one.
        private void OnManipulationEnded(ManipulationEventData data)
        {
            // The source is the handler's own GameObject (ManipulationHandler.OnPointerUp), so comparing
            // objects is exactly "the handler this recorded" with no second GetComponent to disagree with.
            if (_held == null || data == null || data.ManipulationSource != _held.gameObject)
            {
                return;
            }

            _held = null;
            _heldStartSize = -1f;
        }

        /// <summary>
        /// The resize card is answered by the held object changing size, which is honest about what a two-handed
        /// scale actually does and, unlike counting hands, is also true of the desktop wheel. The settle delay is
        /// why this is not simply "its scale moved": a planet pulled by <c>ForceSolver</c> is still growing into
        /// the hand for a moment after the grab, and that growth is the app, not the player.
        /// </summary>
        private void WatchHeldObject(HintCard card, float dt)
        {
            if (card.DismissOn != HintAction.Resize || _held == null || _actionDone)
            {
                return;
            }

            _heldFor += dt;
            if (_heldFor < resizeSettleSeconds)
            {
                return;
            }

            var size = _held.HostTransform.localScale.x;
            if (_heldStartSize < 0f)
            {
                _heldStartSize = size;
                return;
            }

            if (_heldStartSize > 1e-6f && Mathf.Abs(size - _heldStartSize) / _heldStartSize >= resizeFraction)
            {
                _actionDone = true;
            }
        }

        // ---------- the animation

        private void Animate(HintCard card)
        {
            var count = _leftFrames.Count;
            if (count == 0)
            {
                return;
            }

            var loop = Mathf.Max(0.25f, card.LoopSeconds);
            var phase = Mathf.Repeat(_animTime / loop, 1f);

            // Cross-fade: each frame owns an equal slice of the loop and hands over to the next during it, so a
            // two-frame card reads as one hand closing rather than two pictures alternating.
            var t = phase * count;
            var current = Mathf.Clamp(Mathf.FloorToInt(t), 0, count - 1);
            var next = (current + 1) % count;
            var blend = Mathf.SmoothStep(0f, 1f, t - Mathf.Floor(t));

            for (var i = 0; i < count; i++)
            {
                var alpha = i == current ? 1f - blend : i == next ? blend : 0f;
                if (current == next)
                {
                    alpha = 1f; // a one-frame card is a still picture, not a fade to itself
                }

                SetAlpha(_leftFrames[i], alpha);
                if (i < _rightFrames.Count)
                {
                    SetAlpha(_rightFrames[i], alpha);
                }
            }

            // Out and back over one loop. A sine returns to where it started, so the loop has no seam and needs
            // no separate "return" half in the data.
            var travel = Mathf.Sin(phase * Mathf.PI) * card.TravelUnits * _unitScale;

            if (_leftArt != null)
            {
                _leftArt.anchoredPosition = new Vector2(_leftHome.x - travel, _leftHome.y);
            }

            if (card.TwoHanded && _rightArt != null)
            {
                _rightArt.anchoredPosition = new Vector2(_rightHome.x + travel, _rightHome.y);
            }
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            var colour = image.color;
            colour.a = alpha;
            image.color = colour;
        }

        private void BuildFrames(HintCard card)
        {
            ClearFrames();

            if (card.Frames == null || card.Frames.Length == 0)
            {
                if (_rightArt != null)
                {
                    _rightArt.gameObject.SetActive(false);
                }

                return;
            }

            var artHeight = _desktop ? desktopArtHeightPixels : worldArtHeightUnits;

            foreach (var sprite in card.Frames)
            {
                if (sprite == null)
                {
                    continue;
                }

                _leftFrames.Add(Frame(sprite, _leftArt, artHeight));
                if (card.TwoHanded)
                {
                    _rightFrames.Add(Frame(sprite, _rightArt, artHeight));
                }
            }

            if (_rightArt != null)
            {
                _rightArt.gameObject.SetActive(card.TwoHanded);
            }

            // A single hand sits in the middle of the band; a pair straddles it and drifts apart from there.
            var spread = card.TwoHanded ? artHeight * 0.45f : 0f;
            _leftHome = new Vector2(-spread, 0f);
            _rightHome = new Vector2(spread, 0f);

            if (_leftArt != null)
            {
                _leftArt.anchoredPosition = _leftHome;
            }

            if (_rightArt != null)
            {
                _rightArt.anchoredPosition = _rightHome;
            }
        }

        private Image Frame(Sprite sprite, RectTransform parent, float height)
        {
            var go = new GameObject("frame", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(height, height);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return image;
        }

        private void ClearFrames()
        {
            foreach (var image in _leftFrames)
            {
                if (image != null)
                {
                    Destroy(image.gameObject);
                }
            }

            foreach (var image in _rightFrames)
            {
                if (image != null)
                {
                    Destroy(image.gameObject);
                }
            }

            _leftFrames.Clear();
            _rightFrames.Clear();
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
                // A replay comes from the Help button on the dock, so the answer appears where the player was
                // already looking, sharing the dock's tilt. Parked, not head-following: a card that chases the
                // player cannot be looked away from while they try the gesture it is describing.
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

            // Flattened, for the same reason the dock flattens it: pitched with the head the card ends up on the
            // floor or the ceiling.
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            _root.transform.SetPositionAndRotation(
                head.position + forward * headDistanceMetres + Vector3.up * headDropMetres,
                Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f));
        }

        // ---------- the canvas, built on first use

        private bool EnsureCards()
        {
            if (_cards != null)
            {
                return _cards.Length > 0;
            }

            var set = HintCardSet.Load();
            if (set == null || set.Cards == null || set.Cards.Length == 0)
            {
                _cards = Array.Empty<HintCard>();
                Debug.LogError("HintCards: no hint card set in Resources. " +
                               "Run Cosmic Simulation > Build Hint Cards.", this);
                return false;
            }

            _cards = set.Cards;
            return true;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            // Subscribed with the rest of the one-time setup, where SwitchNotice also keeps its subscription:
            // nothing needs this hook until a card exists to be re-parked, and every route to a card comes
            // through here first.
            DockController.Moved += OnDockMoved;

            // Read here rather than in Awake: GalaxyExplorerManager decides the platform in its own Awake, and
            // until it has, PlatformId is HoloLensGen1 rather than anything true. Nothing asks for a hint before
            // the app is running, so by now the answer is honest. (Same reasoning as SwitchNotice.EnsureBuilt.)
            _desktop = GalaxyExplorerManager.IsDesktop;

            var font = BorrowFont();

            _root = new GameObject("hint_card_canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)_root.transform;
            canvasRect.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();

            _group = _root.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // Nothing on the card takes a mouse ray. On the desktop the player answers the card by clicking the
            // thing behind it, so a card that swallowed clicks would be a card you cannot obey. This does not
            // disarm the headset's poke target below: a CanvasGroup gates the graphic raycaster, not colliders.
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var width = _desktop ? desktopWidthPixels : worldWidthUnits;
            var height = _desktop ? desktopHeightPixels : worldHeightUnits;

            if (_desktop)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 65; // above the desktop dock's 50 and the switch notice's 60

                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // Travel is authored once, in millimetres of card, so the gesture reads the same on a monitor.
                _unitScale = height / Mathf.Max(1f, worldHeightUnits);
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace;
                _root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

                canvasRect.sizeDelta = new Vector2(width, height);

                // One canvas unit is one millimetre, the scale every world-space UI prefab in this project is
                // authored on. Divided by the parent's scale so the card keeps one physical size even if the
                // component was dropped under a scaled rig rather than on the object Resolve makes.
                var parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
                canvasRect.localScale = Vector3.one * (0.001f / parentScale);
                _unitScale = 1f;
            }

            var card = new GameObject("card", typeof(RectTransform));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(canvasRect, false);
            cardRect.sizeDelta = new Vector2(width, height);

            if (_desktop)
            {
                cardRect.anchorMin = new Vector2(0.5f, 0f);
                cardRect.anchorMax = new Vector2(0.5f, 0f);
                cardRect.pivot = new Vector2(0.5f, 0f);
                cardRect.anchoredPosition = new Vector2(0f, desktopBottomOffsetPixels);
            }
            else
            {
                cardRect.anchoredPosition = Vector2.zero;
            }

            BuildContents(cardRect, font, width);

            if (!_desktop)
            {
                BuildPokeTarget(cardRect, width, height);
            }

            _root.SetActive(false);
        }

        private void BuildContents(RectTransform cardRect, TMP_FontAsset font, float width)
        {
            var artHeight = _desktop ? desktopArtHeightPixels : worldArtHeightUnits;
            var titleSize = _desktop ? desktopTitleSizePixels : worldTitleSizeUnits;
            var lineSize = _desktop ? desktopLineSizePixels : worldLineSizeUnits;

            // The stack is measured off the type rather than off a second set of serialized gaps: one number per
            // presentation is one number to keep honest when the copy deck changes the wording.
            var gap = lineSize * 0.6f;
            var titleHeight = titleSize * 1.4f;
            var lineHeight = lineSize * 2.8f; // two lines of the longer sentence at this width

            var art = new GameObject("art", typeof(RectTransform));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(cardRect, false);
            Top(artRect, 0f, width, artHeight);

            _leftArt = Slot("art_left", artRect, artHeight);
            _rightArt = Slot("art_right", artRect, artHeight);

            // The second copy is the first one mirrored, which is what two hands facing each other look like and
            // is why the two-hand card needs no second set of sprites.
            _rightArt.localScale = new Vector3(-1f, 1f, 1f);
            _rightArt.gameObject.SetActive(false);

            _title = Label("title", cardRect, font, titleSize);
            Top((RectTransform)_title.transform, artHeight + gap, width, titleHeight);

            _line = Label("line", cardRect, font, lineSize);
            Top((RectTransform)_line.transform, artHeight + gap + titleHeight + gap * 0.5f, width * 0.94f, lineHeight);
        }

        private RectTransform Slot(string name, RectTransform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private TMP_Text Label(string name, RectTransform parent, TMP_FontAsset font, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }

            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            // No plate behind the card, so the outline is what keeps white text readable against a bright wall as
            // well as a black sky - the same treatment InfoPanel and SwitchNotice get (GDD 8.3).
            label.outlineWidth = 0.12f;
            label.outlineColor = new Color32(0x06, 0x0B, 0x10, 0xFF);
            return label;
        }

        /// <summary>
        /// The card's own hit area, so a poke or a far pinch dismisses it in the headset the way a click does on
        /// the desktop. World only: the desktop card is screen-space and deliberately transparent to the mouse.
        /// </summary>
        private void BuildPokeTarget(RectTransform cardRect, float width, float height)
        {
            var go = new GameObject("hit", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(cardRect, false);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;

            // Before the interactable: GEInteractable keeps only the colliders already on its own GameObject.
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, 2f);

            go.AddComponent<GEInteractable>();
            go.AddComponent<GEButton>().OnClick.AddListener(RequestAdvance);
        }

        private static void Top(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -y);
        }

        /// <summary>
        /// The font the rest of the app is already using, so a hint does not arrive in a different typeface.
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
