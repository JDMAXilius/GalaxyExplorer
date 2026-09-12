using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The desktop menu: a row of buttons in the bottom-right corner, opened with the "..." button (or Tab).
/// Buttons that don't apply to the current view are hidden and the rest close up toward the "..." button.
/// </summary>
public class DesktopMenuManager : MonoBehaviour
{
    private const string MutedPrefsKey = "GalaxyExplorer.Muted";

    [SerializeField]
    private GameObject _menuParent;

    [SerializeField]
    private GameObject _buttonParent;

    [SerializeField]
    private GameObject _resetButton;

    [SerializeField]
    private GameObject _backButton;

    [SerializeField]
    private GameObject _aboutButton;

    [SerializeField]
    private GameObject _resetViewButton;

    [SerializeField]
    private GameObject _muteButton;

    [SerializeField]
    private GameObject _helpButton;

    [SerializeField]
    [Tooltip("Controls overlay shown by the help button, H or F1.")]
    private GameObject _helpPanel;

    [SerializeField]
    private Image _muteIcon;

    [SerializeField]
    private TMP_Text _muteLabel;

    [SerializeField]
    private Sprite _soundOnSprite;

    [SerializeField]
    private Sprite _soundOffSprite;

    [SerializeField]
    [Tooltip("Anchored x of the rightmost button, and the spacing between buttons.")]
    private float _rightmostButtonX = -60f;

    [SerializeField]
    private float _buttonSpacing = 40f;

    private bool _openedOnce;
    private bool _rootOpenedForHelp;

    public bool IsVisible { get; private set; } = false;

    // activeInHierarchy, not activeSelf: the overlay sits under the same root as the button row, and that root
    // is switched off whenever the legacy menu is "unavailable". An overlay under a switched-off root is not
    // visible, and Esc must not spend itself closing something nobody can see.
    public bool IsHelpVisible => _helpPanel != null && _helpPanel.activeInHierarchy;

    public static bool IsMuted
    {
        get => PlayerPrefs.GetInt(MutedPrefsKey, 0) == 1;
        private set
        {
            PlayerPrefs.SetInt(MutedPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private void Start()
    {
        // These buttons are uGUI, and until CS-061 the app had an EventSystem with no input module on it, so
        // every one of them was dead. The installer is idempotent and is also hooked to scene loads; this call
        // is here because a menu that cannot be pressed is indistinguishable from a menu that is not there.
        CosmicSimulation.UiEventSystemInstaller.Ensure();

        SetMenuAvailability(false, false, false);

        _backButton.SetActive(false);
        _resetButton.SetActive(false);
        _buttonParent.SetActive(false);
        if (_helpPanel != null)
        {
            _helpPanel.SetActive(false);
        }

        ApplyMute();
    }

    public void SetMenuAvailability(bool isAvailable, bool resetIsActive, bool backIsActive)
    {
        if (isAvailable)
        {
            UpdateButtonsActive(resetIsActive, backIsActive);

            // Open the buttons the first time the menu appears so people find them.
            if (!_openedOnce)
            {
                _openedOnce = true;
                _buttonParent.SetActive(true);
            }
        }
        else if (_helpPanel != null)
        {
            _helpPanel.SetActive(false);
        }

        _menuParent.SetActive(isAvailable);

        IsVisible = isAvailable;
    }

    private void UpdateButtonsActive(bool resetIsActive, bool backIsActive)
    {
        // Reset only applies in the solar system.
        _resetButton.SetActive(resetIsActive);

        // Back is never offered, whatever backIsActive says. Drill-down navigation is retired (roadmap 4.3):
        // TransitionManager's zoom and LoadPrevScene survive as an internal transition helper, but the player
        // moves between places through the dock, and a Back button next to it would go somewhere the dock has
        // no idea about. The parameter stays because GlobalMenuManager computes it for the other platforms'
        // menus, which are retired on their own schedule.
        _backButton.SetActive(false);

        LayoutButtons();
    }

    // Right-align the visible buttons next to the "..." button, in this order from left to right.
    private void LayoutButtons()
    {
        var order = new[] { _helpButton, _muteButton, _resetViewButton, _resetButton, _backButton, _aboutButton };
        var x = _rightmostButtonX;
        for (var i = order.Length - 1; i >= 0; i--)
        {
            var button = order[i];
            if (button == null || !button.activeSelf)
            {
                continue;
            }

            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            x -= _buttonSpacing;
        }
    }

    public void OnToggleDesktopButtonVisibility()
    {
        _buttonParent.SetActive(!_buttonParent.activeSelf);
    }

    public void OnHelpButtonPressed()
    {
        SetHelpVisible(!IsHelpVisible);
    }

    public void SetHelpVisible(bool visible)
    {
        if (_helpPanel == null)
        {
            return;
        }

        // The overlay used to be gated on this menu being "available", a flag only the legacy ViewLoader intro
        // flow ever raises. Since the dock took over navigation that flow may never run, which left H, F1 and
        // the dock's own Help button pressing a panel that could not appear. The overlay therefore switches its
        // own root back on for as long as it is up, and switches it off again after - so the retired button row
        // is not dragged on screen with it.
        if (visible && _menuParent != null && !_menuParent.activeSelf)
        {
            _menuParent.SetActive(true);
            _rootOpenedForHelp = true;
            if (_buttonParent != null)
            {
                _buttonParent.SetActive(false);
            }
        }

        _helpPanel.SetActive(visible);

        if (!visible && _rootOpenedForHelp)
        {
            _rootOpenedForHelp = false;
            if (_menuParent != null && !IsVisible)
            {
                _menuParent.SetActive(false);
            }
        }
    }

    public void OnMuteButtonPressed()
    {
        IsMuted = !IsMuted;
        ApplyMute();
    }

    public void OnResetViewButtonPressed()
    {
        if (DesktopMouseInput.Instance != null)
        {
            DesktopMouseInput.Instance.ResetView();
        }
    }

    private void ApplyMute()
    {
        var muted = IsMuted;
        AudioListener.volume = muted ? 0f : 1f;
        if (_muteIcon != null)
        {
            _muteIcon.sprite = muted ? _soundOffSprite : _soundOnSprite;
        }

        if (_muteLabel != null)
        {
            _muteLabel.text = muted ? "Unmute" : "Mute";
        }
    }
}
