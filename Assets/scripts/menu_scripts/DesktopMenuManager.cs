using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What is left of the inherited desktop HUD: the mute preference, and the controls overlay.
///
/// <para><b>The button row is retired</b> (roadmap 4.3, CS-087 / CS-111). It was a second menu drawn in the same
/// bottom-right corner as <c>DesktopDock</c>, offering a Back button to a drill-down navigation the dock
/// replaced. Everything it did the dock now does: Reset view and Mute and Help are controls under the dock, and
/// About opens from the utility window. <c>menu_managers.prefab</c> ships with the row's root switched off
/// (Cosmic Simulation > Retire Legacy Menus), and this class no longer has a path that switches it back on —
/// the prefab and the code have to agree, because the prefab alone was not enough: <c>GlobalMenuManager</c>
/// pushes <c>SetMenuAvailability(true, ...)</c> at the end of the legacy scene-load flow and that call used to
/// re-activate the root and open the buttons.</para>
///
/// <para>The one thing under that root a player can still reach is the controls overlay, which H, F1 and the
/// dock's Help button open. <see cref="SetHelpVisible"/> lifts the root for as long as the overlay is up and
/// puts it back down afterwards, and takes the button row down with it on the way in.</para>
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
    [Tooltip("Anchored x of the rightmost button, and the spacing between buttons. Retired row: kept so the " +
             "overlay's siblings stay where the prefab put them if anybody ever switches the row back on by hand.")]
    private float _rightmostButtonX = -60f;

    [SerializeField]
    private float _buttonSpacing = 40f;

    private bool _rootOpenedForHelp;

    /// <summary>
    /// Always false now. The row is retired, and callers that ask "is the legacy menu on screen" must get the
    /// truthful answer rather than the availability flag <c>GlobalMenuManager</c> used to push in.
    /// </summary>
    public bool IsVisible { get; private set; } = false;

    // activeInHierarchy, not activeSelf: the overlay sits under the same root as the retired button row, and
    // that root is off except while the overlay is up. An overlay under a switched-off root is not visible, and
    // Esc must not spend itself closing something nobody can see.
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
        // stays because the controls overlay is uGUI too and still has to be clickable.
        CosmicSimulation.UiEventSystemInstaller.Ensure();

        SetMenuAvailability(false, false, false);
        ApplyMute();
    }

    /// <summary>
    /// Was "show or hide the legacy row". Now it only ever puts the row down.
    ///
    /// <para>The signature is unchanged because <c>GlobalMenuManager</c> computes this state for every platform
    /// and calls whichever menu the platform uses; the other platforms' menus are retired on their own
    /// schedule. What changed is the answer: whatever it is told, the row stays off, so the legacy HUD can no
    /// longer appear on top of the dock (GDD 3.1 — one dock).</para>
    /// </summary>
    public void SetMenuAvailability(bool isAvailable, bool resetIsActive, bool backIsActive)
    {
        IsVisible = false;

        // Individually as well as via the root: the controls overlay lifts the root for itself, and none of this
        // may come back up with it.
        SetActiveSafely(_buttonParent, false);
        SetActiveSafely(_resetButton, false);

        // Back is never offered, whatever backIsActive says. Drill-down navigation is retired (roadmap 4.3):
        // TransitionManager's zoom and LoadPrevScene survive as an internal transition helper, but the player
        // moves between places through the dock, and a Back button next to it would go somewhere the dock has no
        // idea about.
        SetActiveSafely(_backButton, false);

        LayoutButtons();

        // The overlay is the only thing under this root a player can open, and it may be open right now — a
        // scene finishing its load while somebody reads the controls must not slam it shut.
        if (IsHelpVisible)
        {
            return;
        }

        SetActiveSafely(_helpPanel, false);
        SetActiveSafely(_menuParent, false);
    }

    // Right-align the row's buttons next to where the "..." button used to be, in this order from left to
    // right. The row is retired and hidden; this runs so the overlay's siblings keep sane anchored positions
    // and so a diagnostic re-activation by hand shows something laid out rather than stacked at the origin.
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

    /// <summary>
    /// Tab's old job: fold the legacy button row away and back. Retired with the row.
    ///
    /// <para>Kept as a method because <c>DesktopMouseInput</c> still calls it, but only when there is no
    /// <c>DesktopDock</c> in the scene at all — and a scene with no dock should not answer Tab with a menu the
    /// rest of the app has stopped maintaining. Tab belongs to the dock (GDD 5.3).</para>
    /// </summary>
    public void OnToggleDesktopButtonVisibility()
    {
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
        // own root back on for as long as it is up, and switches it off again after — so the retired button row
        // is not dragged on screen with it.
        if (visible && _menuParent != null && !_menuParent.activeSelf)
        {
            _menuParent.SetActive(true);
            _rootOpenedForHelp = true;
            SetActiveSafely(_buttonParent, false);
        }

        _helpPanel.SetActive(visible);

        if (!visible && _rootOpenedForHelp)
        {
            _rootOpenedForHelp = false;
            SetActiveSafely(_menuParent, false);
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

    // Every one of these references can legitimately be missing: the pieces of the legacy HUD are being taken
    // out one ticket at a time, and a null here used to be a NullReferenceException in Start that stopped the
    // rest of the retirement from running at all.
    private static void SetActiveSafely(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
