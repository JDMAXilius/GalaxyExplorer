// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.Hands;

namespace CosmicSimulation
{
    /// <summary>
    /// The row of places, and the few controls that live under it.
    ///
    /// The dock builds itself from <see cref="ExperienceDirector.Modules"/> rather than from tiles laid out by
    /// hand, so adding an experience is a data change. It parks three quarters of a metre in front of the
    /// player at chest height, tilted up toward the face, and stays put until the player drags the bar beneath
    /// it, asks to recentre, or summons it — it does not follow the head around, which would make it impossible
    /// to look away from. It re-parks itself once, when the intro hands the room over, because the player has
    /// usually turned since the dock first parked during the logo.
    ///
    /// Chest height is derived from the player rather than fixed, because a seated player and a standing one do
    /// not have the same chest (GDD 11). The height is a fraction of the eye height with a floor under it, and it
    /// is sampled once per recentre: re-deriving it every frame would make the dock ride up and down as the
    /// player leans, which is the following behaviour the parking rule exists to avoid.
    ///
    /// In the headset the dock is <em>summoned</em>, never hidden by a gesture (D-H2): either palm turned toward
    /// the face and held briefly brings it to that hand's side, shown, facing the player. The old left-palm-up
    /// toggle hid the dock on a resting hand often enough that players lost it (CS-275). Hiding is a button's
    /// job, and Tab's on the desktop.
    /// </summary>
    public class DockController : MonoBehaviour
    {
        [Header("Contents")]
        [SerializeField] private DockTile tilePrefab;
        [SerializeField] private Transform tileRow;
        [SerializeField] private DockPopup popup;

        [Header("Controls")]
        [SerializeField] private GEButton passthroughButton;
        [SerializeField] private GEButton recenterButton;
        [SerializeField] private GEButton helpButton;
        [SerializeField] private GEButton utilityButton;
        [SerializeField] private GEButton beingButton;
        [SerializeField] private Cosmic.Companion.Being beingPrefab;
        [SerializeField] private Transform dragBar;

        // The two things the retired palm menu did that nothing else does (CS-277, D-H1). Back was already
        // superseded by the dock's own tiles and Mode by the passthrough button.
        [SerializeField] private GEButton resetButton;
        [SerializeField] private GEButton aboutButton;

        [Header("Utility window")]
        [SerializeField]
        [Tooltip("Settings window opened by the button under the dock. Instantiated on first use as a sibling " +
                 "of the dock, the way the layout pop-up is, so it does not inherit the dock's tilt or its " +
                 "millimetre canvas scale.")]
        private UtilityWindow utilityWindowPrefab;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Metres in front of the player.")]
        private float distanceMetres = 0.75f;

        [SerializeField]
        [Tooltip("Dock height as a fraction of the player's eye height, sampled at recentre. 0.70 of a 1.6 m " +
                 "eye height is 1.12 m, a little under the sternum; the 0.55 it started at parked the tiles at " +
                 "the waist, below where a player looks for them (CS-275).")]
        [Range(0.2f, 1f)]
        private float heightFractionOfHead = 0.70f;

        [SerializeField]
        [Tooltip("Metres above the floor the dock never drops below, however low the player is sitting. Below " +
                 "this the tiles are hard to reach over a lap or a desk.")]
        private float minimumHeightMetres = 0.7f;

        [SerializeField]
        [Tooltip("Eye height in metres assumed when there is no headset to measure. Desktop only: in a headset " +
                 "the head is measured, and a head that has not been measured yet is waited for.")]
        private float assumedEyeHeightMetres = 1.6f;

        [SerializeField]
        [Tooltip("Metres. A head reported below this in a headset has not been tracked yet — the dock waits for " +
                 "a real pose rather than guessing where the floor is.")]
        private float trackedHeadMinimumMetres = 0.5f;

        [SerializeField]
        [Tooltip("Seconds to wait for that first head pose before parking the dock relative to the head anyway.")]
        private float poseWaitSeconds = 2f;

        [SerializeField]
        [Tooltip("Degrees tilted up toward the face.")]
        private float tiltDegrees = 25f;

        [SerializeField]
        [Tooltip("Distance between tile centres in the tile row's own local units. Tiles are 110 mm wide " +
                 "with a 4 mm gap, so 0.114 on a metre-scaled dock and 114 on a millimetre-scaled canvas.")]
        private float tilePitch = 114f;

        [Header("Summon")]
        [SerializeField]
        [Tooltip("Seconds the palm must stay turned toward the face before the dock comes to that hand.")]
        private float palmDwellSeconds = 0.5f;

        [SerializeField]
        [FormerlySerializedAs("palmUpThreshold")]
        [Tooltip("How squarely the palm must face the head: the cosine of the angle between the palm normal and " +
                 "the line from the palm to the eyes. 1 is dead on.")]
        [Range(0f, 1f)]
        private float palmFacingThreshold = 0.7f;

        [SerializeField]
        [Tooltip("Which axis of the palm joint points out of the palm. Confirmed on device by CS-274; in the " +
                 "OpenXR joint convention +Y is the back of the hand, so Down is the likely answer.")]
        private PalmAxis palmAxis = PalmAxis.Down;

        [SerializeField]
        [Tooltip("Metres. How far to the player's left or right a summoned dock may park: the hand's offset " +
                 "from the head, clamped to this, so an arm flung wide does not put the dock out of reach.")]
        private float summonReachMetres = 0.5f;

        public enum PalmAxis
        {
            Up,
            Down,
            Forward,
            Back
        }

        private readonly List<DockTile> _tiles = new List<DockTile>();
        private UtilityWindow _utility;
        private bool _warnedNoUtility;
        private Camera _camera;
        private bool _visible = true;

        // One dwell per hand: left is 0, right is 1. Latched so a palm held toward the face summons once per
        // raise rather than every frame it stays there.
        private readonly float[] _palmTimers = new float[2];
        private readonly bool[] _palmLatched = new bool[2];

        // The rig's own say on whether a summon is wanted right now, cached per rig rather than searched for per
        // frame: the interactors (anything held, by either hand or a controller) and the system-gesture
        // detectors (the Meta palm-at-eye-level gesture, which is the same pose as the summon).
        private XRInputRig _signalsRig;
        private XRBaseInteractor[] _interactors = System.Array.Empty<XRBaseInteractor>();
        private MetaSystemGestureDetector[] _systemGestures = System.Array.Empty<MetaSystemGestureDetector>();

        // The one automatic re-park, when the intro lets go of the room.
        private bool _parkedAfterIntro;

        private GlobalMenuManager _menus;
        private GalaxyExplorer.AboutSlate _about;

        // Sampled at recentre only. See the class comment for why this is not recomputed per frame.
        private float _heightMetres;
        private bool _heightSampled;

        // A recentre that arrived before the headset had a pose, waiting for one.
        private bool _recenterPending;
        private float _poseWaitedSeconds;
        private bool _posedOnce;

        public static DockController Instance { get; private set; }

        public IReadOnlyList<DockTile> Tiles => _tiles;

        public bool IsVisible => _visible;

        /// <summary>Metres above the floor the dock was parked at by the last recentre.</summary>
        public float HeightMetres => _heightSampled
            ? _heightMetres
            : Mathf.Max(assumedEyeHeightMetres * heightFractionOfHead, minimumHeightMetres);

        private void Awake()
        {
            Instance = this;
            _camera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ExperienceDirector.ExperienceChanged -= OnExperienceChanged;
            if (popup != null)
            {
                popup.LayoutChosen -= OnLayoutChosen;
            }
        }

        private void Start()
        {
            Build();
            Recenter();

            ExperienceDirector.ExperienceChanged += OnExperienceChanged;
            if (popup != null)
            {
                popup.LayoutChosen += OnLayoutChosen;
            }

            if (passthroughButton != null)
            {
                passthroughButton.OnClick.AddListener(TogglePassthrough);
            }

            if (recenterButton != null)
            {
                recenterButton.OnClick.AddListener(Recenter);
            }

            // This button existed, was shown and hidden with the rest, and had no listener on it at all:
            // SetVisible toggled its GameObject and nothing was ever subscribed. GDD 8.6 and F-32 want Help
            // to replay the two hint cards, so that is what it now does.
            if (helpButton != null)
            {
                helpButton.OnClick.AddListener(ShowHelp);
            }

            if (utilityButton != null)
            {
                utilityButton.OnClick.AddListener(ToggleUtility);
            }

            if (beingButton != null)
            {
                beingButton.OnClick.AddListener(ToggleBeing);
            }

            if (resetButton != null)
            {
                resetButton.OnClick.AddListener(ResetPlaces);
            }

            if (aboutButton != null)
            {
                aboutButton.OnClick.AddListener(ShowAbout);
            }

            // Wired here as well as lazily, so the bar is draggable from the first frame rather than from the
            // first LateUpdate. See the drag bar section for what this corrects and why.
            DragHandler();

            HideBehindTheDesktopMirror();
        }

        /// <summary>
        /// One dock at a time (GDD 3.1). On a monitor the dock the player uses is <see cref="DesktopDock"/>'s
        /// screen-space mirror, so this one takes its furniture down.
        ///
        /// <para>It was already meant to be out of sight there — see <see cref="TryEyeHeight"/>: the dock parks
        /// 0.72 m below the eye line and the desktop camera never tilts, so the frustum was doing the hiding.
        /// That is a coincidence of numbers rather than a decision, and it stops being true the moment anything
        /// moves the camera, widens the field of view, or scales the content around it — and when it stops being
        /// true the player gets a second row of the same seven tiles floating behind the one they are using,
        /// which is exactly what "three menus on top of each other" looked like. Say it instead of relying on
        /// it.</para>
        ///
        /// <para>Only the visuals go. The dock object stays alive and so does everything asked of it from the
        /// mirror: <see cref="Recenter"/> (which is also how the desktop's Restore reaches
        /// <c>FreePlacementAnchor</c>), <see cref="ToggleUtility"/> on U, and the utility window it spawns —
        /// that window places itself in front of the camera on a desktop rather than beside this dock.</para>
        ///
        /// <para>Guarded on a mirror actually existing. <c>DesktopDock.Instance</c> is set in its Awake, so it is
        /// already decided by the time any Start runs; a desktop scene carrying no mirror keeps this dock, on
        /// the grounds that one dock the player has to look down at beats none at all.</para>
        /// </summary>
        private void HideBehindTheDesktopMirror()
        {
            if (!GalaxyExplorer.GalaxyExplorerManager.IsDesktop || DesktopDock.Instance == null)
            {
                return;
            }

            // Quietly: this is the dock never having been shown, not the player putting it away, and the hide
            // clip at startup would be a sound with nothing behind it.
            SetVisible(false, true);
        }

        /// <summary>Replays the one-time hint cards. F-05 and F-32: Help is how a player asks to see them again.</summary>
        public void ShowHelp() => HintCards.Replay();

        // ---------- the utility window

        /// <summary>Opens or closes the settings window (GDD 8.2): scale, mute, narration, text size.</summary>
        public void ToggleUtility() => EnsureUtility()?.Toggle();

        public void ToggleBeing() => Being.Host.Toggle(beingPrefab);

        // ---------- what the palm menu used to do (CS-277)

        /// <summary>Sends every pulled body home. The legacy menu's Reset, routed through the same manager.</summary>
        public void ResetPlaces()
        {
            if (_menus == null)
            {
                _menus = FindAnyObjectByType<GlobalMenuManager>(FindObjectsInactive.Include);
            }

            if (_menus != null)
            {
                _menus.OnResetButtonPressed();
            }
        }

        /// <summary>Opens or closes the About slate (GDD 8.6). The legacy menu's About.</summary>
        public void ShowAbout()
        {
            // Found with inactive objects included: the slate switches its own GameObject off to hide, and
            // GlobalMenuManager's reference, taken from the active objects at its Start, is null when the
            // slate happened to be hidden then.
            if (_about == null)
            {
                _about = FindAnyObjectByType<GalaxyExplorer.AboutSlate>(FindObjectsInactive.Include);
            }

            if (_about != null)
            {
                _about.ToggleAboutButton();
            }
        }

        /// <summary>The settings window, made the first time anybody asks for it. Null if none was assigned.</summary>
        public UtilityWindow Utility => EnsureUtility();

        // Spawned on demand rather than shipped inside the dock prefab: it is a sibling, not a child, because a
        // child would inherit both the dock's 25-degree tilt and the 0.001 scale of the dock's own canvas. This
        // is the same arrangement ExperienceWiring gives the layout pop-up.
        private UtilityWindow EnsureUtility()
        {
            if (_utility != null)
            {
                return _utility;
            }

            if (utilityWindowPrefab == null)
            {
                if (!_warnedNoUtility)
                {
                    _warnedNoUtility = true;
                    Debug.LogWarning(
                        "DockController: no utility window prefab is assigned, so the settings button does " +
                        "nothing. Run Cosmic Simulation > Build UI Prefabs, which builds it and assigns it.",
                        this);
                }

                return null;
            }

            _utility = Instantiate(utilityWindowPrefab, transform.parent);
            _utility.name = "utility_window";
            _utility.gameObject.SetActive(false);
            return _utility;
        }

        // ---------- building

        private void Build()
        {
            if (tilePrefab == null || tileRow == null || ExperienceDirector.Instance == null)
            {
                return;
            }

            foreach (var tile in _tiles)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }

            _tiles.Clear();

            var count = 0;
            foreach (var module in TileModules())
            {
                var tile = Instantiate(tilePrefab, tileRow);
                tile.name = "tile_" + module.Id;
                tile.Bind(module);
                tile.OnChosen.AddListener(OnTileChosen);
                _tiles.Add(tile);
                count++;
            }

            // Centre the row on the plate so it stays symmetrical whatever the module count is.
            var span = (count - 1) * tilePitch;
            for (var i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].transform.localPosition = new Vector3(i * tilePitch - span * 0.5f, 0f, 0f);
            }

            MarkActive(ExperienceDirector.Instance.Current);
        }

        // ---------- placement

        /// <summary>
        /// Raised once the dock has finished being put somewhere new: the end of a hand drag, a recentre, or
        /// <see cref="MoveTo"/>. Surfaces that open from the dock and park themselves beside it — the utility
        /// window (GDD 8.2), a hint card, the switch notice — re-place from this rather than polling, so that
        /// carrying the dock across the room does not leave them behind (CS-121).
        ///
        /// Deliberately the *end* of a drag and not every frame of one. <see cref="FaceThePlayer"/> re-imposes
        /// the dock's own upright tilt from LateUpdate, so for the duration of a grab the rotation anything
        /// reads in Update is the hand's roll rather than the dock's pose; a surface that followed that would
        /// roll with the wrist while the dock stayed level. Nothing has to track the dock mid-drag — the GDD
        /// asks only that these surfaces end up beside it.
        /// </summary>
        public static event System.Action Moved;

        // The end of every route that puts the dock somewhere new.
        private void Placed()
        {
            // The dock's own pop-up is moved from here rather than by DockPopup subscribing to the event: the
            // desktop mirror instantiates a second DockPopup whose placement is screen-space and measured in
            // pixels (DesktopDock.PlacePopupAbove), and a world-space re-place would throw that one out of
            // frame the moment somebody pressed Recenter.
            if (popup != null && popup.IsOpen)
            {
                popup.Reposition();
            }

            Moved?.Invoke();
        }

        /// <summary>Puts the dock back in front of the player.</summary>
        public void Recenter()
        {
            // Before the camera guard, deliberately. Recenter means "put things back where they belong", and
            // the content with no ForceSolver and no LayoutRig — the nebula overlays, the Cosmic Web, Andromeda
            // — has no other route home in the headset (CS-107). A missing camera stops the dock re-parking; it
            // must not also strand a galaxy the player pushed across the room.
            FreePlacementAnchor.RestoreAll();

            // A recentre with no camera has not been refused, it has nothing to measure from yet: whatever the
            // pending flag was stays, so a wait already under way keeps waiting.
            var result = Park(0f);
            if (result != ParkResult.NoCamera)
            {
                _recenterPending = result == ParkResult.NoPose;
            }
        }

        private enum ParkResult
        {
            Parked,
            NoCamera,
            NoPose
        }

        // The parking itself, shared by a recentre (dead ahead) and a summon (ahead, but over to the side the
        // summoning hand is on). One routine so the height rule, the drag hand-over and the tidy-up cannot
        // drift apart between the two.
        private ParkResult Park(float lateralMetres)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return ParkResult.NoCamera;
                }
            }

            var head = _camera.transform;

            // The flattened look direction. Degenerate only when the player is looking straight up or down,
            // where world forward is as good an answer as any.
            Flatten(head.forward, out var forward);

            // One eye height answers both questions, so the floor the dock measures up from and the height it
            // measures cannot disagree with each other. When the eye height cannot be had from the same place as
            // the head position, nothing is parked at all — see TryEyeHeight.
            if (!TryEyeHeight(out var eyeHeight))
            {
                return ParkResult.NoPose;
            }

            _heightMetres = Mathf.Max(eyeHeight * heightFractionOfHead, minimumHeightMetres);
            _heightSampled = true;

            // A hand on the bar and a recentre are two answers to "where does the dock go", and the handler
            // holds the pose it captured when the grab began: park the dock now and the next frame of the drag
            // would pull it straight back out again. Letting go for the player is the only honest way to let
            // Recenter win. The handler drops its pointers when it is disabled and ignores the release of a
            // pointer it no longer holds, so nothing is left stuck; the player simply pinches again. Done here
            // rather than at the top because a recentre that gives up above has not moved anything, and taking
            // a grab away for nothing would be worse than leaving it.
            var grab = DragHandler();
            if (grab != null && grab.IsManipulating)
            {
                grab.enabled = false;
                grab.enabled = true;
            }

            var origin = new Vector3(head.position.x, head.position.y - eyeHeight, head.position.z);
            var right = Vector3.Cross(Vector3.up, forward);
            transform.position = origin + forward * distanceMetres + right * lateralMetres + Vector3.up * _heightMetres;

            // Turned toward the player rather than along the look direction: the same thing dead ahead, and
            // the difference between a dock that faces you and one you read at a slant when it is off to a side.
            Flatten(transform.position - head.position, out var awayFromPlayer);
            transform.rotation = ParkedRotation(awayFromPlayer);

            // Parking is one of the routes that puts the dock somewhere new, so it owes the same tidying as the
            // others: without this the pop-up stays hanging where the dock used to be, and nothing listening to
            // Moved hears that it went anywhere.
            Placed();
            return ParkResult.Parked;
        }

        // The two halves of the parking maths, shared by the two things that aim the dock: a recentre, which
        // points it along the flattened way the player is looking, and a drag, which points it back along the
        // line from the player to wherever the bar was let go. They are the same two steps — flatten, then
        // pitch up by tiltDegrees — and they live together so that a change to the tilt cannot reach only one
        // of them and leave a dragged dock at a different angle from a parked one (GDD 8.1).

        /// <summary>
        /// Flattens a direction onto the floor plane. A dock that kept the head's pitch would end up face down
        /// on the floor or face up at the ceiling. False when there was no horizontal component to keep.
        /// </summary>
        private static bool Flatten(Vector3 direction, out Vector3 flat)
        {
            flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f)
            {
                flat = Vector3.forward;
                return false;
            }

            flat.Normalize();
            return true;
        }

        /// <summary>The dock's resting orientation, given the flattened direction its face looks along — which
        /// is away from the player, since the plate reads from behind its own forward axis.</summary>
        private Quaternion ParkedRotation(Vector3 flatForward) =>
            Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f);

        // The eye height and the floor have to come out of the same measurement, or they contradict each other.
        //
        // In the headset the rig tracks floor-relative, so the head's own y *is* the eye height and the floor is
        // simply what it measures down to. Before the first pose the head reads about zero while the XR device
        // already reports itself active, and answering that with a nominal 1.6 m — as this used to — mixes a
        // trusted constant into an untrusted position: the floor comes out 1.6 m below a head that is standing on
        // it, and the dock parks 0.72 m underground until somebody presses Recenter. Rather than guess, this
        // reports failure and Recenter tries again next frame.
        //
        // Desktop is not that case wearing a different hat, it is its own case: there is no head there at all.
        // The camera never moves — DesktopMouseInput pans, orbits and zooms the content pivot, not the camera —
        // so measuring down from it with the nominal eye height is a constant, and it puts the dock the same
        // 0.72 m below the eye line it sits at in a headset. That is deliberate: it is what keeps the world dock
        // out of the desktop frustum, where DesktopDock's screen-space mirror is the dock the player uses. (It
        // did previously sit at a fixed height above world zero instead, which is the same constant offset by
        // wherever the scene happens to put the camera, and is not tied to what the player can see.)
        private bool TryEyeHeight(out float eyeHeight)
        {
            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                eyeHeight = assumedEyeHeightMetres;
                return true;
            }

            eyeHeight = _camera != null ? _camera.transform.position.y : 0f;
            if (eyeHeight > trackedHeadMinimumMetres)
            {
                _posedOnce = true;
                return true;
            }

            // A head that has been seen once and now reads low is a player who is low — lying down, or a child —
            // not a missing pose, and the floor still comes out of the same reading it does. Only the very first
            // pose is worth waiting for; without this, pressing Recenter while lying down would do nothing for
            // two seconds.
            if (_posedOnce)
            {
                return true;
            }

            // Waiting has to end somewhere: an XR device that reports itself active while a pose never arrives
            // would otherwise leave the dock wherever the prefab put it, forever. After a moment the desktop rule
            // is used instead — below the head rather than above the floor, which is at least consistent with
            // where the player is looking from, and one press of Recenter fixes it once a pose does exist.
            if (_poseWaitedSeconds >= poseWaitSeconds)
            {
                eyeHeight = assumedEyeHeightMetres;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Puts the whole dock somewhere and turns it back toward the player. The bar does not go through
        /// here — it drives the transform directly through its <see cref="ManipulationHandler"/> — but a
        /// caller with a pose in mind still needs one call that leaves the dock facing the right way.
        /// </summary>
        public void MoveTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            FaceThePlayer();
            Placed();
        }

        private void FaceThePlayer()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            // Straight above or below the head there is no horizontal direction to face and any answer would
            // be arbitrary, so the last good one is kept.
            if (!Flatten(transform.position - _camera.transform.position, out var awayFromPlayer))
            {
                return;
            }

            transform.rotation = ParkedRotation(awayFromPlayer);
        }

        // ---------- the drag bar
        //
        // GDD 8.1: the 60 mm bar under the dock "moves the whole dock (it re-tilts toward the player)". Three
        // things have to be true for that and none of them was, which is why the bar has never moved anything.
        //
        // 1. The pinch has to arrive. ManipulationHandler is deliberately not an IGEPointerHandler — on a body
        //    the ForceSolver is the handler ExecuteHierarchy finds and it forwards the grab itself, at the point
        //    its state machine allows one — so a handler with no solver above it is reached only through
        //    ManipulationPointerRouter (CS-106). The bar had the handler and no router.
        // 2. The handler has to drag the dock. Its host defaults to its own transform, so the first working
        //    pinch would have pulled the bar out from under the dock and left the dock where it stood.
        // 3. The dock has to stay upright and dock-sized while it is dragged. The handler's one-handed path
        //    writes rotation from the hand pose and its two-handed path writes scale; a dock that rolls with a
        //    wrist or grows by the hand span is not a dock. Held below in LateUpdate rather than by asking the
        //    handler for an exception, because LateUpdate runs after every Update and so cannot lose the race
        //    with the handler's own.
        //
        // 1 and 2 are written by UiPrefabBuilder.BuildDock, which is the durable form; they are re-asserted
        // here because the dock prefab in the repo predates them and a fix that waits for a menu item to be
        // re-run is not a fix. The grab sound is the one part that does wait for the rebuild: playing it from
        // here as well would double it once the prefab carries playGrabSounds.

        private ManipulationHandler _dragHandler;
        private bool _dragBarWired;
        private bool _dragging;
        private Vector3 _dragScale;

        /// <summary>
        /// The handler on the drag bar, wired to drag this dock. Resolved from the first caller rather than in
        /// Awake, because the router it adds may be added the same frame it is used.
        /// </summary>
        private ManipulationHandler DragHandler()
        {
            if (_dragBarWired)
            {
                return _dragHandler;
            }

            _dragBarWired = true;
            if (dragBar == null)
            {
                return null;
            }

            _dragHandler = dragBar.GetComponent<ManipulationHandler>();
            if (_dragHandler == null)
            {
                // A dock whose bar is only a decoration. Nothing to drag with, so nothing to correct either.
                return null;
            }

            _dragHandler.HostTransform = transform;
            _dragHandler.ManipulationType = ManipulationHandler.HandMovementType.OneHandedOnly;

            if (dragBar.GetComponent<ManipulationPointerRouter>() == null)
            {
                dragBar.gameObject.AddComponent<ManipulationPointerRouter>();
            }

            return _dragHandler;
        }

        /// <summary>True while a hand or a ray is holding the bar.</summary>
        private bool IsBeingDragged
        {
            get
            {
                var handler = DragHandler();
                return handler != null && handler.IsManipulating;
            }
        }

        // Runs after every Update, including the manipulation handler's, so what it writes is what the frame
        // ends with. The handler is left owning the position — that is the whole gesture — and the dock keeps
        // the two parts of its pose that are not the player's to set by hand.
        private void LateUpdate()
        {
            // Unity's null comparison deliberately, not a type pattern: a handler destroyed with a bar that was
            // torn down under us is not null to the CLR, and asking it anything would throw.
            var handler = DragHandler();
            var dragging = handler != null && handler.IsManipulating;

            if (dragging && !_dragging)
            {
                // Captured per grab, not once at startup, so this can never undo a size set between grabs.
                _dragScale = transform.localScale;

                // The layout pop-up hangs 40 mm above the tile that opened it and is placed once, so the dock
                // would slide out from under it. It is a momentary choice, not a window: shut it.
                if (popup != null)
                {
                    popup.Close();
                }
            }

            if (dragging)
            {
                transform.localScale = _dragScale;
            }

            // On the release frame too, so the last word on the tilt belongs to the dock rather than to the hand
            // that let go — the re-tilt GDD 8.1 asks for.
            if (dragging || _dragging)
            {
                FaceThePlayer();
            }

            // After that FaceThePlayer, not before it: everything listening reads the dock's pose, and on the
            // release frame the levelled pose is only true once the line above has written it.
            if (!dragging && _dragging)
            {
                Placed();

                // The settings window is placed beside the dock when it opens and never again, so it has to be
                // told the dock has moved. Open() on an already-open window is exactly that and nothing else:
                // it re-places it, with no sound and no repaint. Asked only of a window that exists and is
                // open, so this can neither create one nor re-open one the player closed mid-drag.
                if (_utility != null && _utility.IsOpen)
                {
                    _utility.Open();
                }
            }

            _dragging = dragging;
        }

        // ---------- visibility

        public void SetVisible(bool visible) => SetVisible(visible, false);

        // The body of SetVisible. "quiet" skips the show/hide clip, for the one caller that is not the player
        // asking: the desktop mirror taking this dock's furniture down at startup, which is not a dock being put
        // away and should not sound like one.
        private void SetVisible(bool visible, bool quiet)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;

            // Guarded by the equality check above, so a held palm that re-asserts the same state stays silent.
            if (!quiet)
            {
                AudioService.Instance?.PlayClip(visible ? AudioId.DockShow : AudioId.DockHide);
            }

            if (tileRow != null)
            {
                tileRow.gameObject.SetActive(visible);
            }

            // The plate is the builder's child named "plate", found rather than wired so the prefab needs no rebuild.
            var plate = transform.Find("plate");
            if (plate != null)
            {
                plate.gameObject.SetActive(visible);
            }

            if (dragBar != null)
            {
                dragBar.gameObject.SetActive(visible);
            }

            if (passthroughButton != null)
            {
                passthroughButton.gameObject.SetActive(visible);
            }

            if (recenterButton != null)
            {
                recenterButton.gameObject.SetActive(visible);
            }

            if (helpButton != null)
            {
                helpButton.gameObject.SetActive(visible);
            }

            if (beingButton != null)
            {
                beingButton.gameObject.SetActive(visible);
            }

            if (utilityButton != null)
            {
                utilityButton.gameObject.SetActive(visible);
            }

            if (resetButton != null)
            {
                resetButton.gameObject.SetActive(visible);
            }

            if (aboutButton != null)
            {
                aboutButton.gameObject.SetActive(visible);
            }

            if (!visible && popup != null)
            {
                popup.Close();
            }

            // Hidden with the dock rather than left floating: it opened from the dock and belongs beside it.
            // Asked for only if one was ever made — this must not be what brings the window into existence.
            if (!visible && _utility != null)
            {
                _utility.Close();
            }
        }

        public void Toggle() => SetVisible(!_visible);

        private void Update()
        {
            // Not while the player has hold of the bar: a recentre that is still waiting for a head pose runs
            // every frame, and it would fight the drag for the same transform. The wait is resumed on release.
            if (_recenterPending && !IsBeingDragged)
            {
                _poseWaitedSeconds += Time.unscaledDeltaTime;
                Recenter();
            }

            WatchPalms();
            WatchKeyboard();
        }

        // The desktop equivalent of the settings button under the dock. Read here rather than in
        // DesktopMouseInput for the same reason DesktopDock reads Tab for itself: the control belongs to the
        // dock, and U is not spoken for anywhere else (GDD 5.3).
        private void WatchKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.uKey.wasPressedThisFrame)
            {
                ToggleUtility();
            }
        }

        // ---------- summoning (CS-275)

        private void WatchPalms()
        {
            var rig = XRInputRig.Instance;
            if (rig == null)
            {
                ResetPalm(0);
                ResetPalm(1);
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            CacheRigSignals(rig);

            // Not while anything is held. A summon re-parks the dock, and the bar's handler holds the pose it
            // captured when the grab began, so parking under a live drag would be undone next frame; and a
            // palm that turns toward the face while the other hand is carrying a planet is a player looking at
            // the planet, not asking for a menu. Not during the system gesture either — that is this same pose
            // at eye level, and Meta owns it.
            var suppressed = _dragging || AnythingHeld() || SystemGestureActive();

            WatchPalm(0, rig.LeftHandTracked ? rig.LeftPalm : null, suppressed);
            WatchPalm(1, rig.RightHandTracked ? rig.RightPalm : null, suppressed);
        }

        private void WatchPalm(int hand, Transform palm, bool suppressed)
        {
            if (palm == null || _camera == null)
            {
                ResetPalm(hand);
                return;
            }

            // The dwell is not counted while suppressed, but the latch is left alone: a palm still raised when
            // the grab ends has to turn away and back before it summons.
            if (suppressed)
            {
                _palmTimers[hand] = 0f;
                return;
            }

            // Facing the head, not facing up: the dot of the palm normal with the line from the palm to the
            // eyes, so it reads the same wherever the hand is held — low by the hip or out to the side.
            var toHead = _camera.transform.position - palm.position;
            var facing = toHead.sqrMagnitude > 1e-4f &&
                         Vector3.Dot(PalmNormal(palm), toHead.normalized) >= palmFacingThreshold;
            if (!facing)
            {
                ResetPalm(hand);
                return;
            }

            _palmTimers[hand] += Time.deltaTime;
            if (_palmTimers[hand] >= palmDwellSeconds && !_palmLatched[hand])
            {
                _palmLatched[hand] = true;
                Summon(palm);
            }
        }

        private void ResetPalm(int hand)
        {
            _palmTimers[hand] = 0f;
            _palmLatched[hand] = false;
        }

        /// <summary>
        /// Brings the dock to the hand that asked for it: shown, parked at the usual distance and height but
        /// over on that hand's side, facing the player. Only ever shows — nothing the hands do hides the dock.
        /// </summary>
        private void Summon(Transform palm)
        {
            var head = _camera.transform;
            Flatten(head.forward, out var forward);
            var right = Vector3.Cross(Vector3.up, forward);

            // The hand's offset from the head across the look direction, which is "that hand's side" without
            // depending on how far forward the arm happens to be.
            var lateral = Mathf.Clamp(Vector3.Dot(palm.position - head.position, right), -summonReachMetres, summonReachMetres);

            // Shown before it is parked, so everything that re-places itself from Moved sees a visible dock.
            SetVisible(true);
            Park(lateral);
        }

        private void CacheRigSignals(XRInputRig rig)
        {
            if (_signalsRig == rig)
            {
                return;
            }

            // Inactive included: the controllers' interactors are switched off while hands are the input, and
            // the detectors sit on hand objects that come and go with tracking.
            _signalsRig = rig;
            _interactors = rig.GetComponentsInChildren<XRBaseInteractor>(true);
            _systemGestures = rig.GetComponentsInChildren<MetaSystemGestureDetector>(true);
        }

        // Anything selected by any interactor on the rig: a body, a galaxy, the bar, the being, a button
        // mid-press. ForceSolver keeps no static "held" flag, and the interactors are the one place every grab
        // in the headset passes through.
        private bool AnythingHeld()
        {
            foreach (var interactor in _interactors)
            {
                if (interactor != null && interactor.isActiveAndEnabled && interactor.hasSelection)
                {
                    return true;
                }
            }

            return false;
        }

        // Either hand's detector; they carry no handedness of their own, and a summon during the system gesture
        // on the other hand is not worth having either.
        private bool SystemGestureActive()
        {
            foreach (var detector in _systemGestures)
            {
                if (detector != null && detector.isActiveAndEnabled &&
                    detector.systemGestureState.Value == MetaSystemGestureDetector.SystemGestureState.Started)
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 PalmNormal(Transform palm)
        {
            switch (palmAxis)
            {
                case PalmAxis.Down: return -palm.up;
                case PalmAxis.Forward: return palm.forward;
                case PalmAxis.Back: return -palm.forward;
                default: return palm.up;
            }
        }

        // ---------- choices
        //
        // The four static helpers below are the parts the desktop mirror (<see cref="DesktopDock"/>) has to
        // agree with: which modules get a tile and in what order, what a tile means when it is chosen, what a
        // layout choice does, and which tile is underlined. They live here so a change to the dock is a change
        // to both docks and the mirror cannot quietly grow a second opinion.

        /// <summary>The modules that get a tile, in dock order.</summary>
        public static IEnumerable<ExperienceModule> TileModules()
        {
            var director = ExperienceDirector.Instance;
            if (director == null)
            {
                yield break;
            }

            foreach (var module in director.Modules)
            {
                if (module != null && module.Kind == ExperienceKind.DockTile)
                {
                    yield return module;
                }
            }
        }

        /// <summary>
        /// What choosing a tile does: a place offering more than one arrangement opens its pop-up, anything
        /// else switches straight over.
        /// </summary>
        public static void Choose(DockTile tile, DockPopup popup)
        {
            if (tile == null || tile.Module == null)
            {
                return;
            }

            // Layouts only. A tile's job is to open its place: the Galaxies tile opens the sphere of galaxies,
            // and which galaxy you go to next is chosen inside that sphere - from a pinpoint on the galaxy
            // itself or from the bar across the top of it - not from a list that replaces the view.
            if (tile.Module.HasLayoutChoice && popup != null)
            {
                popup.Open(tile);
                return;
            }

            if (popup != null)
            {
                popup.Close();
            }

            ExperienceDirector.Instance?.Switch(tile.Module);
        }

        /// <summary>Opens the place the player picked from a tile that offers several.</summary>
        public static void ChoosePlace(ExperienceModule place)
        {
            if (place == null)
            {
                return;
            }

            ExperienceDirector.Instance?.Switch(place);
        }

        /// <summary>Commits the layout the player picked in a pop-up.</summary>
        public static void ChooseLayout(ExperienceModule module, LayoutPreset layout)
        {
            var director = ExperienceDirector.Instance;
            if (director == null)
            {
                return;
            }

            if (director.Current != module)
            {
                director.Switch(module);
            }

            // The layout itself is applied by whatever owns the bodies; CS-040 fills these presets in.
            LayoutRequested?.Invoke(module, layout);
        }

        /// <summary>Underlines the tile for the open place and clears the rest.</summary>
        public static void MarkActive(IEnumerable<DockTile> tiles, ExperienceModule module)
        {
            foreach (var tile in tiles)
            {
                if (tile != null)
                {
                    tile.SetActiveTile(tile.Module == module);
                }
            }
        }

        private void OnTileChosen(DockTile tile) => Choose(tile, popup);

        private void OnLayoutChosen(ExperienceModule module, LayoutPreset layout) => ChooseLayout(module, layout);

        /// <summary>Raised when the player picks a layout for an experience.</summary>
        public static event System.Action<ExperienceModule, LayoutPreset> LayoutRequested;

        private void OnExperienceChanged(ExperienceModule module)
        {
            MarkActive(module);

            // The first place to open after the intro is the intro handing over (ExperienceDirector adopts or
            // switches to it only once OnIntroFinished has been raised and the transition has settled). The dock
            // parked during the logo, before the player had turned to face anything; parking it again here
            // puts it where they are now looking. Once only — later switches are the player's own, and a dock
            // that jumped on every one of them would be following them around by another name.
            if (_parkedAfterIntro || module == null)
            {
                return;
            }

            var director = ExperienceDirector.Instance;
            if (director == null || director.IntroRunning)
            {
                return;
            }

            // Park, not Recenter: Recenter also sends every pulled galaxy and nebula home, which nobody asked for.
            _parkedAfterIntro = Park(0f) == ParkResult.Parked;
        }

        private void MarkActive(ExperienceModule module) => MarkActive(_tiles, module);

        private void TogglePassthrough() => EnvironmentController.Instance?.TogglePassthrough();
    }
}
