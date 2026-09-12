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
        [Tooltip("Metres above the floor.")]
        private float heightMetres = 0.9f;

        [SerializeField]
        [Tooltip("Degrees tilted up toward the face.")]
        private float tiltDegrees = 25f;

        [SerializeField]
        [Tooltip("Metres between tile centres. Tiles are 110 mm wide with a 4 mm gap.")]
        private float tilePitchMetres = 0.114f;

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

        public static DockController Instance { get; private set; }

        public IReadOnlyList<DockTile> Tiles => _tiles;

        public bool IsVisible => _visible;

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

            var modules = ExperienceDirector.Instance.Modules;
            var count = 0;
            foreach (var module in modules)
            {
                if (module == null || module.Kind != ExperienceKind.DockTile)
                {
                    continue;
                }

                var tile = Instantiate(tilePrefab, tileRow);
                tile.name = "tile_" + module.Id;
                tile.Bind(module);
                tile.OnChosen.AddListener(OnTileChosen);
                _tiles.Add(tile);
                count++;
            }

            // Centre the row on the plate so it stays symmetrical whatever the module count is.
            var span = (count - 1) * tilePitchMetres;
            for (var i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].transform.localPosition = new Vector3(i * tilePitchMetres - span * 0.5f, 0f, 0f);
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

            var origin = new Vector3(head.position.x, head.position.y - GuessEyeHeight(), head.position.z);
            transform.position = origin + forward * distanceMetres + Vector3.up * heightMetres;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f);
        }

        // The rig reports floor-relative tracking, but on desktop there is no floor, so fall back to a
        // plausible standing height rather than putting the dock around the player's knees.
        private float GuessEyeHeight()
        {
            var y = _camera != null ? _camera.transform.position.y : 0f;
            return y > 0.5f ? y : 1.6f;
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

        private void OnTileChosen(DockTile tile)
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

        private void OnLayoutChosen(ExperienceModule module, LayoutPreset layout)
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

        /// <summary>Raised when the player picks a layout for an experience.</summary>
        public static event System.Action<ExperienceModule, LayoutPreset> LayoutRequested;

        private void OnExperienceChanged(ExperienceModule module) => MarkActive(module);

        private void MarkActive(ExperienceModule module)
        {
            foreach (var tile in _tiles)
            {
                if (tile != null)
                {
                    tile.SetActiveTile(tile.Module == module);
                }
            }
        }

        private void TogglePassthrough() => EnvironmentController.Instance?.TogglePassthrough();
    }
}
