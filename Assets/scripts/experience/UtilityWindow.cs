// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The small window of settings that opens from the dock: a scale slider for the place the player is in,
    /// mute, narration-only mute, the panel text size (GDD 8.2 and 11, contract row F-03), and - owner's
    /// direction, 16 Sep - the microphone the being listens on (<see cref="MicrophoneRow"/>) and Quit.
    ///
    /// <para><b>Quit takes two taps.</b> The first arms it and the label says so; it disarms itself after
    /// <c>quitArmSeconds</c>. A stray pinch passing over the window cannot close the app.</para>
    ///
    /// <para><b>Why these are one window.</b> GDD 8.1 says "Mute lives in the utility window", and 11 asks
    /// for narration-only mute and a text-size setting in the same place. None of them had anywhere to live: the
    /// world dock carries Recenter and Help and nothing else. So this is the app's settings surface, and the
    /// only one — nothing here invents a second mechanism for something that already works. Mute goes through
    /// <c>DesktopMenuManager</c> when that legacy HUD is in the scene, exactly as <see cref="DesktopDock"/>
    /// already does, so there is one stored answer to "is this app muted".</para>
    ///
    /// <para><b>The slider is not uGUI.</b> World-space uGUI is not reachable by hand in this project — there is
    /// no <c>TrackedDeviceGraphicRaycaster</c> anywhere (see <see cref="UiEventSystemInstaller"/>), and every
    /// world-space pointer arrives as a physics hit through <see cref="GEPointer"/>. So the rail carries a
    /// collider and this component reads the pointer's attach transform, which is the same source
    /// <see cref="ManipulationHandler"/> drags objects with. That one path serves a poke, a hand ray and the
    /// mouse alike: <c>DesktopMouseInput</c> rides its mouse attach point along the cursor ray while the button
    /// is held, so a click-drag on the rail works on a monitor with no second implementation.</para>
    ///
    /// <para><b>Placement.</b> Beside the dock in a headset. On the desktop the world dock deliberately parks
    /// below the frustum (see <see cref="DockController"/>), so a window pinned to it would be unreachable;
    /// there it places itself in front of the camera instead, which is the same problem
    /// <see cref="DesktopDock"/> exists to solve, answered without a second prefab because the mouse already
    /// reaches world colliders.</para>
    /// </summary>
    public class UtilityWindow : MonoBehaviour, IGEPointerHandler
    {
        // The key DesktopMenuManager stores mute under. It is a private const over there and that file belongs
        // to the legacy HUD, so the literal is repeated rather than shared — if one moves, both must.
        private const string MutedPrefsKey = "GalaxyExplorer.Muted";

        // docs/ui/spec.md §1. Repeated here the way DockPopup repeats them: these are the runtime copies of the
        // tokens the builder bakes into the prefab.
        private static readonly Color Accent = new Color(0.424f, 0.812f, 0.867f);
        private static readonly Color Plate = new Color(0.055f, 0.078f, 0.094f, 0.8f);
        private static readonly Color OnAccent = new Color(0.055f, 0.078f, 0.094f);
        private static readonly Color InkSecondary = new Color(0.62f, 0.722f, 0.769f);

        [Header("Scale")]
        [SerializeField]
        [Tooltip("The rail the player drags. Its collider is the hit area; its RectTransform is the travel.")]
        private GEInteractable scaleTrack;

        [SerializeField]
        [Tooltip("Cyan bar that grows from the left of the rail. Width is driven in canvas units — one unit is " +
                 "one millimetre on this canvas.")]
        private RectTransform scaleFill;

        [SerializeField] private RectTransform scaleKnob;
        [SerializeField] private TMP_Text scaleValue;

        [SerializeField]
        [Tooltip("Smallest multiple of the size the open experience arrived at.")]
        private float minScale = 0.5f;

        [SerializeField]
        [Tooltip("Largest multiple of the size the open experience arrived at. A ScaleLimits on the content " +
                 "still has the last word, so a model cannot be dragged past the metres GDD 5 allows it.")]
        private float maxScale = 2f;

        [Header("Sound")]
        [SerializeField] private GEButton muteButton;
        [SerializeField] private Image muteFill;
        [SerializeField] private TMP_Text muteLabel;

        [SerializeField] private GEButton narrationButton;
        [SerializeField] private Image narrationFill;
        [SerializeField] private TMP_Text narrationLabel;

        [Header("Text size")]
        [SerializeField] private GEButton[] textSizeButtons = new GEButton[0];
        [SerializeField] private Image[] textSizeFills = new Image[0];
        [SerializeField] private TMP_Text[] textSizeLabels = new TMP_Text[0];

        [SerializeField]
        [Tooltip("The multipliers the three buttons stand for. GDD 11 asks for x1.0, x1.25 and x1.5.")]
        private float[] textSizes = { 1f, 1.25f, 1.5f };

        [Header("Quit")]
        [SerializeField] private GEButton quitButton;
        [SerializeField] private Image quitFill;
        [SerializeField] private TMP_Text quitLabel;

        [SerializeField]
        [Tooltip("Seconds the first tap on Quit stays armed.")]
        private float quitArmSeconds = 3f;

        [Header("Close")]
        [SerializeField] private GEButton closeButton;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Metres to the right of the dock's centre, in a headset. The dock plate is 834 mm wide, so " +
                 "half of it plus half of this window plus a gap.")]
        private float dockSideOffsetMetres = 0.49f;

        [SerializeField]
        [Tooltip("Metres above the dock's centre, in a headset.")]
        private float dockLiftMetres;

        [SerializeField]
        [Tooltip("Metres in front of the camera on the desktop, where the world dock is out of frame.")]
        private float desktopDistanceMetres = 0.5f;

        [SerializeField]
        [Tooltip("Metres right and up from the centre of the desktop view. Negative x keeps it off the middle " +
                 "of the screen, where the experience is.")]
        private Vector2 desktopOffsetMetres = new Vector2(-0.12f, 0f);

        private GEPointer _dragPointer;
        private float _scale = 1f;

        // What the slider is scaling, and the size that counts as x1 for it. Captured on first use rather than
        // on open: an experience grows in over a second or so and capturing mid-grow would call a tenth of the
        // real size "x1".
        private Transform _content;
        private Vector3 _contentBase = Vector3.one;
        private float _applied = 1f;

        private bool _wired;
        private bool _closeRequested;
        private bool _quitRequested;
        private float _quitArmedUntil;
        private bool _paintedArmed;
        private bool _warnedNoContent;

        // What Repaint last wrote, so mute changed from anywhere else (P, the legacy HUD, the desktop dock)
        // shows up here without repainting text every frame.
        private bool _paintedMuted;
        private bool _paintedNarration;
        private float _paintedTextScale = -1f;

        public static UtilityWindow Instance { get; private set; }

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Whether the whole app is silenced. One stored answer, shared with the legacy desktop HUD.</summary>
        public static bool Muted => PlayerPrefs.GetInt(MutedPrefsKey, 0) == 1;

        // Nothing else applies the stored mute in a headset: DesktopMenuManager does it in its own Start and
        // that component only exists in the desktop scenes, so without this a player who muted on the Quest
        // came back to sound. AfterSceneLoad rather than a component's Awake because this window is not
        // instantiated until somebody opens it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyStoredMute()
        {
            AudioListener.volume = Muted ? 0f : 1f;
        }

        /// <summary>Silences everything, or brings it back, and remembers which.</summary>
        public static void SetMuted(bool muted)
        {
            // The legacy HUD owns the preference and its own icon where it exists; going through it keeps the
            // two from disagreeing. Where it does not exist — the headset — the same key is written directly.
            var menu = FindAnyObjectByType<DesktopMenuManager>();
            if (menu != null)
            {
                if (DesktopMenuManager.IsMuted != muted)
                {
                    menu.OnMuteButtonPressed();
                }

                return;
            }

            PlayerPrefs.SetInt(MutedPrefsKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = muted ? 0f : 1f;
        }

        private void Awake() => EnsureInit();

        // Every public entry point calls this. A component added with AddComponent and used the same frame has
        // not necessarily had its Awake, and this window is instantiated and opened in one go.
        private void EnsureInit()
        {
            if (_wired)
            {
                return;
            }

            _wired = true;
            Instance = this;

            if (muteButton != null)
            {
                muteButton.OnClick.AddListener(ToggleMute);
            }

            if (narrationButton != null)
            {
                narrationButton.OnClick.AddListener(ToggleNarration);
            }

            if (closeButton != null)
            {
                closeButton.OnClick.AddListener(RequestClose);
            }

            if (quitButton != null)
            {
                quitButton.OnClick.AddListener(PressQuit);
            }

            for (var i = 0; i < textSizeButtons.Length; i++)
            {
                var index = i;
                if (textSizeButtons[i] != null)
                {
                    textSizeButtons[i].OnClick.AddListener(() => ChooseTextSize(index));
                }
            }

            ExperienceDirector.ExperienceChanged += OnExperienceChanged;
            DockController.Moved += OnDockMoved;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ExperienceDirector.ExperienceChanged -= OnExperienceChanged;
            DockController.Moved -= OnDockMoved;
        }

        // The dock can be carried across the room by the bar under it (CS-108), and this window opened from the
        // dock: GDD 8.2 puts it beside the dock, not beside where the dock used to be. Re-placed when the drag
        // ends rather than followed every frame — see DockController.Moved for why mid-drag is the wrong pose
        // to read.
        private void OnDockMoved()
        {
            if (IsOpen)
            {
                Place();
            }
        }

        // ---------- open and close

        public void Open()
        {
            EnsureInit();
            _closeRequested = false;

            if (IsOpen)
            {
                Place();
                return;
            }

            gameObject.SetActive(true);
            Place();
            Repaint();

            // The pop-up's clips. The toolbox they were recorded for is gone and these two small windows are
            // what "show" and "hide" mean now; see the mapping note on AudioId.
            AudioService.Instance?.PlayClip(AudioId.ToolboxShow);
        }

        public void Close()
        {
            EnsureInit();
            _closeRequested = false;

            if (!IsOpen)
            {
                return;
            }

            _dragPointer = null;
            _quitArmedUntil = 0f;

            // A confirmed Quit that had not run yet must not fire the next time the window opens.
            _quitRequested = false;
            gameObject.SetActive(false);

            // No target: a pooled source parented to this object would be cut off by the SetActive above.
            AudioService.Instance?.PlayClip(AudioId.ToolBoxHide);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// Closes on the next frame. This is what the × is wired to, deliberately: <see cref="GEButton.Click"/>
        /// starts its cool-down coroutine immediately after invoking the listeners, and starting a coroutine on
        /// a GameObject the listener has just deactivated is an error.
        /// </summary>
        public void RequestClose() => _closeRequested = true;

        /// <summary>True between the first tap on Quit and either the second tap or the timeout.</summary>
        public bool QuitArmed => Time.unscaledTime < _quitArmedUntil;

        /// <summary>The Quit button: arms on the first tap, quits on a second tap while armed.</summary>
        public void PressQuit()
        {
            EnsureInit();
            if (!QuitArmed)
            {
                _quitArmedUntil = Time.unscaledTime + quitArmSeconds;
                Repaint();
                return;
            }

            // Next frame, for the same reason as RequestClose: GEButton starts a coroutine after its listeners.
            _quitArmedUntil = 0f;
            _quitRequested = true;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            if (_quitRequested)
            {
                _quitRequested = false;
                Quit();
                return;
            }

            if (_closeRequested)
            {
                _closeRequested = false;
                Close();
                return;
            }

            if (_dragPointer != null)
            {
                if (_dragPointer.IsActive)
                {
                    DragTo(_dragPointer);
                }
                else
                {
                    // A hand that left tracking mid-drag never raises a pointer up.
                    _dragPointer = null;
                }
            }

            if (_paintedMuted != Muted ||
                _paintedNarration != VOManager.NarrationEnabled ||
                !Mathf.Approximately(_paintedTextScale, InfoPanel.TextScale) ||
                _paintedArmed != QuitArmed)
            {
                Repaint();
            }
        }

        // ---------- the rail
        //
        // The rail has a GEInteractable and no handler of its own, so its pointer events walk up the hierarchy
        // to this component (GEInputEvents.ExecuteHierarchy). The buttons in this window each carry their own
        // GEButton, which stops the walk there — so the only thing that reaches these three methods is the rail.

        public void OnPointerDown(GEPointerEventData eventData)
        {
            EnsureInit();
            if (eventData == null || scaleTrack == null || eventData.Interactable != scaleTrack)
            {
                return;
            }

            _dragPointer = eventData.Pointer;
            if (_dragPointer != null)
            {
                // Jump to where it was pressed, like any slider: the press is already a value.
                DragTo(_dragPointer);
            }

            eventData.Use();
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
            if (eventData == null || scaleTrack == null || eventData.Interactable != scaleTrack)
            {
                return;
            }

            _dragPointer = null;
            eventData.Use();
        }

        public void OnPointerClicked(GEPointerEventData eventData)
        {
            // Nothing: the value was set on the way down and followed while held.
        }

        private void DragTo(GEPointer pointer)
        {
            var attach = pointer != null ? pointer.AttachTransform : null;
            var track = scaleTrack != null ? scaleTrack.transform as RectTransform : null;
            if (attach == null || track == null)
            {
                return;
            }

            var rect = track.rect;
            if (rect.width <= 0.001f)
            {
                return;
            }

            // rect.xMin rather than -width/2: a RectTransform's local origin is its pivot, and this stays right
            // if the rail is ever re-anchored.
            var local = track.InverseTransformPoint(attach.position);
            var t = Mathf.Clamp01((local.x - rect.xMin) / rect.width);
            SetScale(Mathf.Lerp(minScale, maxScale, t));
        }

        private void SetScale(float value)
        {
            _scale = Mathf.Clamp(value, minScale, maxScale);
            ApplyScale();
            PaintScale();
        }

        private void ApplyScale()
        {
            var content = CurrentContent();
            if (content == null)
            {
                if (!_warnedNoContent)
                {
                    _warnedNoContent = true;
                    Debug.LogWarning(
                        "UtilityWindow: the scale slider has nothing to scale — no experience content is open " +
                        "(TransitionManager.CurrentActiveScene is empty). The slider will do nothing until one is.",
                        this);
                }

                return;
            }

            if (content != _content)
            {
                _content = content;
                _contentBase = content.localScale;
                _applied = 1f;
            }

            // Somebody else — two hands on the model, a layout transition, the grow-in — has moved the scale
            // since we last wrote it. Re-derive what x1 means rather than yanking the content back to our own
            // idea of it; nothing in this app snaps back on its own.
            var expected = _contentBase * _applied;
            if (_applied > 0.0001f &&
                (content.localScale - expected).sqrMagnitude > 1e-6f * Mathf.Max(1e-6f, expected.sqrMagnitude))
            {
                _contentBase = content.localScale / _applied;
            }

            var desired = _contentBase * _scale;

            // The physical limits in GDD 5 are the last word: a slider must not be able to make a model smaller
            // than 40 cm or wider than 4 m when the same object refuses that from two hands.
            var limits = content.GetComponent<ScaleLimits>();
            if (limits != null)
            {
                desired = limits.ClampScale(desired);
            }

            content.localScale = desired;
            _applied = Mathf.Abs(_contentBase.x) > 0.0001f ? desired.x / _contentBase.x : _scale;

            // Clamped short of what was asked for: move the knob to what actually happened rather than leave it
            // pointing at a size the content refused.
            if (!Mathf.Approximately(_applied, _scale))
            {
                _scale = Mathf.Clamp(_applied, minScale, maxScale);
            }
        }

        /// <summary>
        /// What the slider scales. <c>TransitionManager.CurrentActiveScene</c> is this project's own answer to
        /// "which content root is open" — <see cref="ExperienceDirector"/> writes it on every switch, and the
        /// star background and the anchor handler already read it.
        /// </summary>
        private static Transform CurrentContent()
        {
            if (!GalaxyExplorerManager.IsInitialized)
            {
                return null;
            }

            var transitions = GalaxyExplorerManager.Instance.TransitionManager;
            var content = transitions != null ? transitions.CurrentActiveScene : null;
            return content != null ? content.transform : null;
        }

        private void OnExperienceChanged(ExperienceModule module)
        {
            // A new place arrives at its authored size, so the slider starts again at x1 rather than silently
            // holding the last place's multiplier over it.
            _scale = 1f;
            _applied = 1f;
            _content = null;

            if (IsOpen)
            {
                PaintScale();
            }
        }

        // ---------- the toggles

        private void ToggleMute()
        {
            SetMuted(!Muted);
            Repaint();
        }

        private void ToggleNarration()
        {
            VOManager.NarrationEnabled = !VOManager.NarrationEnabled;
            Repaint();
        }

        private void ChooseTextSize(int index)
        {
            if (index < 0 || index >= textSizes.Length)
            {
                return;
            }

            InfoPanel.TextScale = textSizes[index];
            Repaint();
        }

        private int ChosenTextSize()
        {
            var current = InfoPanel.TextScale;
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < textSizes.Length; i++)
            {
                var distance = Mathf.Abs(textSizes[i] - current);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        // ---------- painting

        private void Repaint()
        {
            _paintedMuted = Muted;
            _paintedNarration = VOManager.NarrationEnabled;
            _paintedTextScale = InfoPanel.TextScale;
            _paintedArmed = QuitArmed;

            // Cyan means "this is switched off".
            // The state is written in words as well, so nothing here depends on reading a colour (GDD 11).
            Paint(muteFill, muteLabel, _paintedMuted, _paintedMuted ? "Sound off" : "Sound on");
            Paint(narrationFill, narrationLabel, !_paintedNarration,
                _paintedNarration ? "Narration on" : "Narration off");

            var chosen = ChosenTextSize();
            for (var i = 0; i < textSizeFills.Length; i++)
            {
                var label = i < textSizeLabels.Length ? textSizeLabels[i] : null;
                Paint(textSizeFills[i], label, i == chosen, null);
            }

            // Armed reads as a warning in the one way this palette allows: the plate goes white.
            if (quitFill != null)
            {
                quitFill.color = _paintedArmed ? Color.white : Plate;
            }

            if (quitLabel != null)
            {
                quitLabel.text = _paintedArmed ? "Tap again to quit" : "Quit Cosmic Simulation";
                quitLabel.color = _paintedArmed ? OnAccent : InkSecondary;
            }

            PaintScale();
        }

        private static void Paint(Image fill, TMP_Text label, bool lit, string text)
        {
            if (fill != null)
            {
                fill.color = lit ? Accent : Plate;
            }

            if (label == null)
            {
                return;
            }

            label.color = lit ? OnAccent : Color.white;
            if (text != null)
            {
                label.text = text;
            }
        }

        private void PaintScale()
        {
            var track = scaleTrack != null ? scaleTrack.transform as RectTransform : null;
            var width = track != null ? track.rect.width : 0f;
            var t = maxScale > minScale ? Mathf.InverseLerp(minScale, maxScale, _scale) : 0f;

            if (scaleKnob != null && width > 0f)
            {
                scaleKnob.anchoredPosition = new Vector2((t - 0.5f) * width, scaleKnob.anchoredPosition.y);
            }

            if (scaleFill != null && width > 0f)
            {
                scaleFill.sizeDelta = new Vector2(t * width, scaleFill.sizeDelta.y);
            }

            if (scaleValue != null)
            {
                // Invariant culture: a decimal comma here would read as a thousands separator in English copy.
                scaleValue.text = _scale.ToString("0.00", CultureInfo.InvariantCulture) + "x";
            }
        }

        // ---------- placement

        private void Place()
        {
            var dock = DockController.Instance;

            // In a headset the window belongs beside the dock it opened from (GDD 8.2). Everywhere else the
            // dock parks 0.7 m below the eye line and a fixed camera never looks down at it, so following it
            // would put this off-screen too. The test is the headset rather than the desktop *platform* because
            // the editor runs the Quest platform with no device attached, which is how this gets smoke-tested.
            var inHeadset = UnityEngine.XR.XRSettings.isDeviceActive && !GalaxyExplorerManager.IsDesktop;
            if (dock != null && inHeadset)
            {
                var t = dock.transform;
                transform.SetPositionAndRotation(
                    t.position + t.right * dockSideOffsetMetres + t.up * dockLiftMetres,
                    t.rotation);
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var view = camera.transform;
            transform.SetPositionAndRotation(
                view.position +
                view.forward * desktopDistanceMetres +
                view.right * desktopOffsetMetres.x +
                view.up * desktopOffsetMetres.y,
                view.rotation);
        }
    }
}
