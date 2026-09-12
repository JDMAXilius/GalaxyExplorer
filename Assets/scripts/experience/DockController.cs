// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The row of places, and the few controls that live under it.
    ///
    /// The dock builds itself from <see cref="ExperienceDirector.Modules"/> rather than from tiles laid out by
    /// hand, so adding an experience is a data change. It parks three quarters of a metre in front of the
    /// player at chest height, tilted up toward the face, and stays put until the player drags the bar beneath
    /// it or asks to recentre — it does not follow the head around, which would make it impossible to look away
    /// from.
    ///
    /// Chest height is derived from the player rather than fixed, because a seated player and a standing one do
    /// not have the same chest (GDD 11). The height is a fraction of the eye height with a floor under it, and it
    /// is sampled once per recentre: re-deriving it every frame would make the dock ride up and down as the
    /// player leans, which is the following behaviour the parking rule exists to avoid.
    ///
    /// Showing and hiding is palm-up on the left hand in the headset (held briefly, so a passing gesture does
    /// not flash it) and Tab on the desktop.
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
        [SerializeField] private Transform dragBar;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Metres in front of the player.")]
        private float distanceMetres = 0.75f;

        [SerializeField]
        [Tooltip("Dock height as a fraction of the player's eye height, sampled at recentre. 0.55 of a 1.6 m " +
                 "eye height is the 0.88 m chest height the dock used to be pinned to.")]
        [Range(0.2f, 1f)]
        private float heightFractionOfHead = 0.55f;

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

        [Header("Show and hide")]
        [SerializeField]
        [Tooltip("Seconds the palm must stay up before the dock appears.")]
        private float palmDwellSeconds = 0.5f;

        [SerializeField]
        [Tooltip("How closely the palm must face up. 1 is exactly up.")]
        [Range(0f, 1f)]
        private float palmUpThreshold = 0.7f;

        [SerializeField]
        [Tooltip("Which axis of the palm joint points out of the palm. Confirm on device (CS-034).")]
        private PalmAxis palmAxis = PalmAxis.Up;

        public enum PalmAxis
        {
            Up,
            Down,
            Forward,
            Back
        }

        private readonly List<DockTile> _tiles = new List<DockTile>();
        private Camera _camera;
        private float _palmTimer;
        private bool _palmLatched;
        private bool _visible = true;

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
        }

        /// <summary>Replays the one-time hint cards. F-05 and F-32: Help is how a player asks to see them again.</summary>
        public void ShowHelp() => HintCards.Replay();

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

        /// <summary>Puts the dock back in front of the player.</summary>
        public void Recenter()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var head = _camera.transform;

            // Flatten the look direction: a dock pitched with the head ends up on the floor or the ceiling.
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            // One eye height answers both questions, so the floor the dock measures up from and the height it
            // measures cannot disagree with each other. When the eye height cannot be had from the same place as
            // the head position, nothing is parked at all — see TryEyeHeight.
            if (!TryEyeHeight(out var eyeHeight))
            {
                _recenterPending = true;
                return;
            }

            _recenterPending = false;
            _heightMetres = Mathf.Max(eyeHeight * heightFractionOfHead, minimumHeightMetres);
            _heightSampled = true;

            var origin = new Vector3(head.position.x, head.position.y - eyeHeight, head.position.z);
            transform.position = origin + forward * distanceMetres + Vector3.up * _heightMetres;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f);
        }

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

        /// <summary>Moves the whole dock, for the drag bar underneath it.</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            FaceThePlayer();
        }

        private void FaceThePlayer()
        {
            if (_camera == null)
            {
                return;
            }

            var toPlayer = _camera.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 1e-4f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up) *
                                 Quaternion.Euler(-tiltDegrees, 0f, 0f);
        }

        // ---------- visibility

        public void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;

            // Guarded by the equality check above, so a held palm that re-asserts the same state stays silent.
            AudioService.Instance?.PlayClip(visible ? AudioId.DockShow : AudioId.DockHide);
            if (tileRow != null)
            {
                tileRow.gameObject.SetActive(visible);
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

            if (!visible && popup != null)
            {
                popup.Close();
            }
        }

        public void Toggle() => SetVisible(!_visible);

        private void Update()
        {
            if (_recenterPending)
            {
                _poseWaitedSeconds += Time.unscaledDeltaTime;
                Recenter();
            }

            WatchPalm();
        }

        private void WatchPalm()
        {
            var rig = XRInputRig.Instance;
            if (rig == null || !rig.LeftHandTracked || rig.LeftPalm == null)
            {
                _palmTimer = 0f;
                _palmLatched = false;
                return;
            }

            var outOfPalm = PalmNormal(rig.LeftPalm);
            var facingUp = Vector3.Dot(outOfPalm, Vector3.up) >= palmUpThreshold;

            if (!facingUp)
            {
                _palmTimer = 0f;
                _palmLatched = false;
                return;
            }

            _palmTimer += Time.deltaTime;
            if (_palmTimer >= palmDwellSeconds && !_palmLatched)
            {
                // Latched so holding the palm up toggles once rather than flickering every frame.
                _palmLatched = true;
                Toggle();
            }
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

        private void OnExperienceChanged(ExperienceModule module) => MarkActive(module);

        private void MarkActive(ExperienceModule module) => MarkActive(_tiles, module);

        private void TogglePassthrough() => EnvironmentController.Instance?.TogglePassthrough();
    }
}
