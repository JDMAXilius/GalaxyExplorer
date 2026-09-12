// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer;
using GalaxyExplorer.XR;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The dock again, drawn flat in the bottom-right corner of a monitor.
    ///
    /// The world dock parks 0.7 m in front of the player and 0.7 m below eye level, which is exactly right in a
    /// headset — you glance down and it is there. On a desktop the camera looks level and never tilts, so that
    /// same dock sits below the frustum and the player cannot reach it at all. This is the mirror GDD 8.5 asks
    /// for: the seven tiles, the passthrough preview, and Recenter / Mute / Help, on a screen-space overlay.
    ///
    /// It is a *mirror*, not a second dock. Everything that could drift — which modules get a tile and in what
    /// order, what a tile does when it is chosen, which one is underlined — comes from the shared helpers on
    /// <see cref="DockController"/>, so a change to the dock is a change to both of them.
    ///
    /// Shown and hidden with Tab, and open the first time the app runs.
    /// </summary>
    [DisallowMultipleComponent]
    public class DesktopDock : MonoBehaviour
    {
        private const string VisiblePrefsKey = "CosmicSimulation.DesktopDockVisible";

        // AudioListener.volume is a float, so "off" is a threshold rather than an equality.
        private const float MutedVolume = 0.001f;

        // accent/cyan, docs/ui/spec.md §1. The two toggles wear it while they are on.
        private static readonly Color Accent = new Color32(0x6C, 0xCF, 0xDD, 0xFF);

        [Header("Contents")]
        [SerializeField]
        [Tooltip("Everything that hides with the dock. The tile row, the controls and the pop-up all live under it.")]
        private RectTransform plate;

        [SerializeField] private DockTile tilePrefab;
        [SerializeField] private RectTransform tileRow;
        [SerializeField] private DockPopup popup;

        [Header("Controls")]
        [SerializeField] private Button passthroughButton;
        [SerializeField] private Button recenterButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private Button helpButton;

        [SerializeField]
        [Tooltip("Tinted with the accent while the room is forced visible.")]
        private Image passthroughGlyph;

        [SerializeField]
        [Tooltip("Tinted with the accent while the app is muted.")]
        private Image muteGlyph;

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Distance between tile centres, in screen pixels — this canvas is not on the millimetre scale " +
                 "the world-space UI prefabs use.")]
        private float tilePitch = 108f;

        [SerializeField]
        [Tooltip("Gap in screen pixels between the top of a tile and the bottom of its layout pop-up.")]
        private float popupGap = 10f;

        private readonly List<DockTile> _tiles = new List<DockTile>();
        private DesktopMenuManager _menu;
        private bool _visible = true;
        private bool _warnedNoHelp;

        public static DesktopDock Instance { get; private set; }

        public IReadOnlyList<DockTile> Tiles => _tiles;

        public bool IsVisible => _visible;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            ExperienceDirector.ExperienceChanged -= OnExperienceChanged;
            if (popup != null)
            {
                popup.LayoutChosen -= DockController.ChooseLayout;
            }
        }

        private void Start()
        {
            // Not Awake: GalaxyExplorerManager decides the platform in its own Awake, and until it has, the
            // default PlatformId is HoloLensGen1 rather than anything true. By Start every Awake has run.
            if (!GalaxyExplorerManager.IsDesktop)
            {
                // In a headset the world dock is already where the player expects it, and a screen-space
                // overlay would be pasted across both eyes at the near plane.
                Instance = null;
                gameObject.SetActive(false);
                return;
            }

            EnsureEventSystem();
            Build();
            WireControls();

            ExperienceDirector.ExperienceChanged += OnExperienceChanged;
            if (popup != null)
            {
                popup.LayoutChosen += DockController.ChooseLayout;

                // The pop-up's options are GEButtons, driven by a poke or a hand ray. Neither exists on a
                // monitor, so each one gets a uGUI Button that presses it. Wired here rather than saved into
                // the prefab so there is exactly one path from a click to the GEButton.
                foreach (var geButton in popup.GetComponentsInChildren<GEButton>(true))
                {
                    var button = geButton.GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(geButton.Click);
                    }
                }
            }

            // Open on first run (GDD 8.5), and afterwards however the player last left it. The field is set to
            // the opposite first so SetVisible applies the state instead of short-circuiting on a match.
            var open = PlayerPrefs.GetInt(VisiblePrefsKey, 1) == 1;
            _visible = !open;
            SetVisible(open);
            PaintControls();
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

            foreach (var module in DockController.TileModules())
            {
                var tile = Instantiate(tilePrefab, tileRow);
                tile.name = "tile_" + module.Id;
                tile.Bind(module);
                tile.OnChosen.AddListener(OnTileChosen);

                // The world tile is poked or hit by a ray through a collider; a screen-space tile has neither,
                // so the click arrives from uGUI and ends up in the same DockTile.Choose.
                var button = tile.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    button.onClick.AddListener(tile.Choose);
                }

                _tiles.Add(tile);
            }

            // Centred on the row the same way the world dock centres its own, so the two read alike whatever
            // the module count turns out to be.
            var span = (_tiles.Count - 1) * tilePitch;
            for (var i = 0; i < _tiles.Count; i++)
            {
                ((RectTransform)_tiles[i].transform).anchoredPosition =
                    new Vector2(i * tilePitch - span * 0.5f, 0f);
            }

            DockController.MarkActive(_tiles, ExperienceDirector.Instance.Current);
        }

        private void WireControls()
        {
            if (passthroughButton != null)
            {
                passthroughButton.onClick.AddListener(TogglePassthrough);
            }

            if (recenterButton != null)
            {
                recenterButton.onClick.AddListener(Recenter);
            }

            if (muteButton != null)
            {
                muteButton.onClick.AddListener(ToggleMute);
            }

            if (helpButton != null)
            {
                helpButton.onClick.AddListener(ToggleHelp);
            }
        }

        /// <summary>
        /// A screen-space canvas is clicked through the EventSystem, and nothing in this project has needed
        /// that until now: every other pointer goes through <see cref="GEPointer"/> and a physics raycast, so
        /// the one EventSystem in the app (on <c>main_camera_prefab</c>) carries no input module at all.
        /// Without a module every button here is dead, which is a worse failure than an extra component.
        /// </summary>
        private static void EnsureEventSystem()
        {
            var system = EventSystem.current;
            if (system == null)
            {
                system = FindAnyObjectByType<EventSystem>();
            }

            if (system == null)
            {
                system = new GameObject("event_system", typeof(EventSystem)).GetComponent<EventSystem>();
            }

            // Added this way the module assigns itself the package's default UI actions, so point and click
            // work without an actions asset of ours to keep in sync.
            if (system.GetComponent<BaseInputModule>() == null)
            {
                system.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        // ---------- visibility

        public void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (plate != null)
            {
                plate.gameObject.SetActive(visible);
            }

            if (!visible && popup != null)
            {
                popup.Close();
            }

            PlayerPrefs.SetInt(VisiblePrefsKey, visible ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Toggle() => SetVisible(!_visible);

        private void Update()
        {
            // Tab, per GDD 8.5. Read here rather than in DesktopMouseInput, which already spends Tab folding
            // away the legacy DesktopMenuManager row and only while one is on screen. Where both are present
            // Tab moves both, which is the right answer while the old row is still being retired.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                Toggle();
            }

            // The room state and the mute state both change from elsewhere — P, the world dock's own
            // passthrough button, the legacy HUD — so the two toggles read them rather than remember them.
            PaintControls();
        }

        // ---------- choices

        private void OnTileChosen(DockTile tile)
        {
            DockController.Choose(tile, popup);

            // DockPopup lifts itself forty millimetres above the tile it belongs to. On this canvas a
            // millimetre is not a unit of anything, so put it where it belongs in pixels instead.
            if (popup != null && popup.IsOpen)
            {
                PlacePopupAbove(tile);
            }
        }

        // The pop-up and the tile row are both centre-anchored children of the plate and nothing here is
        // rotated or scaled, so plate-local positions simply add up.
        private void PlacePopupAbove(DockTile tile)
        {
            var popupRect = (RectTransform)popup.transform;
            var tileRect = (RectTransform)tile.transform;

            popupRect.anchoredPosition = new Vector2(
                tileRow.anchoredPosition.x + tileRect.anchoredPosition.x,
                tileRow.anchoredPosition.y + tileRect.anchoredPosition.y +
                (tileRect.rect.height + popupRect.rect.height) * 0.5f + popupGap);

            popupRect.localRotation = Quaternion.identity; // DockPopup copied the tile's world rotation
            popupRect.SetAsLastSibling();
        }

        private void OnExperienceChanged(ExperienceModule module) => DockController.MarkActive(_tiles, module);

        // ---------- controls

        /// <summary>Shows the room whatever the open experience asked for, or stops doing so.</summary>
        public void TogglePassthrough() => EnvironmentController.Instance?.TogglePassthrough();

        /// <summary>Puts things back in front of the player.</summary>
        public void Recenter()
        {
            // Two things answer to the word. The world dock re-parks itself, which matters the moment this
            // save is opened in the headset; and the desktop camera goes back to where it started, which is
            // the half of it the player can actually see on a monitor.
            DockController.Instance?.Recenter();
            DesktopMouseInput.Instance?.ResetView();
        }

        /// <summary>Silences everything, or brings it back.</summary>
        public void ToggleMute()
        {
            var menu = Menu();
            if (menu != null)
            {
                // The legacy HUD owns the stored preference and its own icon; going through it keeps one
                // answer to "is this app muted".
                menu.OnMuteButtonPressed();
            }
            else
            {
                AudioListener.volume = AudioListener.volume <= MutedVolume ? 1f : 0f;
            }

            PaintControls();
        }

        /// <summary>Opens or closes the controls overlay.</summary>
        public void ToggleHelp()
        {
            var menu = Menu();
            if (menu != null)
            {
                menu.OnHelpButtonPressed();
                return;
            }

            // The overlay itself belongs to the legacy desktop HUD. Say so once rather than shipping a button
            // that quietly does nothing.
            if (!_warnedNoHelp)
            {
                _warnedNoHelp = true;
                Debug.LogWarning(
                    "DesktopDock: no DesktopMenuManager in this scene, so Help has no controls overlay to open.",
                    this);
            }
        }

        private void PaintControls()
        {
            if (passthroughGlyph != null)
            {
                var forced = EnvironmentController.Instance != null &&
                             EnvironmentController.Instance.PassthroughForced;
                passthroughGlyph.color = forced ? Accent : Color.white;
            }

            if (muteGlyph != null)
            {
                muteGlyph.color = AudioListener.volume <= MutedVolume ? Accent : Color.white;
            }
        }

        // Looked up lazily: the legacy HUD lives in a view scene that is loaded and unloaded under us, so a
        // reference taken once at startup goes stale.
        private DesktopMenuManager Menu()
        {
            if (_menu == null)
            {
                _menu = FindAnyObjectByType<DesktopMenuManager>();
            }

            return _menu;
        }
    }
}
