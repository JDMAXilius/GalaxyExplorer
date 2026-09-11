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

    public bool IsVisible { get; private set; } = false;

    public bool IsHelpVisible => _helpPanel != null && _helpPanel.activeSelf;

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
        // Reset only applies in the solar system; Back only when there is a previous view.
        _resetButton.SetActive(resetIsActive);
        _backButton.SetActive(backIsActive);
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
        if (_helpPanel != null)
        {
            _helpPanel.SetActive(visible && IsVisible);
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
