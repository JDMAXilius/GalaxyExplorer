using CosmicSimulation;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The palm menu, as the original app had it: turn a palm toward your face and a column of buttons appears
/// beside that hand and follows it; turn the palm away and it goes. One hand at a time, and never while a
/// planet is being held.
///
/// The test is the original's — the angle between the direction the palm faces and the camera's forward, shown
/// above <see cref="_minShowingAngle"/> (130 degrees in both prefabs, so within 50 degrees of facing straight
/// back up the line of sight). What has changed is where the palm direction comes from: the original read it
/// off the menu's own forward, which is the palm joint's rotation turned 90 degrees by the hand tracker, and
/// that is only the palm if the joint's +Y is the back of the hand. On tracked hands it is now measured from
/// joint positions (<see cref="XRInputRig.TryGetPalmNormal"/>), which has no axis to get wrong.
/// </summary>
public class HandMenu : MonoBehaviour
{
    // What the Back button says once it has become the Dock button.
    private const string DockLabel = "Dock";

    [SerializeField]
    private GameObject _menuParent;

    [SerializeField]
    private GameObject _resetButton;

    [SerializeField]
    private GameObject _backButton;

    [SerializeField]
    private float _minShowingAngle = 135f;

    private AttachToControllerSolver _attachToControllerSolver;
    private HandMenuManager _handMenuManager;
    private GlobalMenuManager _globalMenuManager;

    private float _currentAngle = 0f;

    private Transform _cameraTransform;

    // On Quest the Back button's slot belongs to "bring the dock to me". See RetaskBackAsDock.
    private bool _backBringsDock;

    public bool IsCurrentlyVisible { get; private set; } = false;

    private void Start()
    {
        _handMenuManager = FindObjectOfType<HandMenuManager>();
        _globalMenuManager = FindObjectOfType<GlobalMenuManager>();

        _menuParent.SetActive(false);
        IsCurrentlyVisible = false;

        _resetButton.SetActive(false);
        _backButton.SetActive(false);

        _attachToControllerSolver = GetComponent<AttachToControllerSolver>();
        _attachToControllerSolver.TrackingLost += OnTrackingLost;

        _cameraTransform = Camera.main.transform;

        if (GalaxyExplorerManager.IsQuest3)
        {
            RetaskBackAsDock();
        }
    }

    /// <summary>
    /// Back is superseded on Quest — the dock's tiles are how you go anywhere (CS-111) — so its button, its
    /// place in the column and its press behaviour are reused for the one thing the palm menu did not have:
    /// calling the dock over. Done here rather than in the prefab so that it needs no prefab surgery; the
    /// wired Back call is switched off, not removed, and the other platforms keep their Back button.
    /// </summary>
    private void RetaskBackAsDock()
    {
        var button = _backButton != null ? _backButton.GetComponent<GEButton>() : null;
        if (button == null)
        {
            return;
        }

        for (var i = 0; i < button.OnClick.GetPersistentEventCount(); i++)
        {
            button.OnClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        button.OnClick.AddListener(BringDock);

        foreach (var label in _backButton.GetComponentsInChildren<TMP_Text>(true))
        {
            label.text = DockLabel;
        }

        _backBringsDock = true;
    }

    private static void BringDock()
    {
        if (DockController.Instance != null)
        {
            DockController.Instance.BringToPlayer();
        }
    }

    private void Update()
    {
        if (_attachToControllerSolver.IsTracking)
        {
            _currentAngle = CalculateAngle();

            if (_currentAngle > _minShowingAngle)
            {
                bool inManipulationState = (_globalMenuManager.ForceSolverFocusManager != null && _globalMenuManager.ForceSolverFocusManager.IsManipulatingPlanet);

                // Check if the menu is already showing on the other hand
                if (!_handMenuManager.IsHandMenuAlreadyVisible && _handMenuManager.MenuIsIsAvailable && !inManipulationState)
                {
                    UpdateMenuVisibility(true);
                }
            }
            else if (_currentAngle < _minShowingAngle && IsCurrentlyVisible)
            {
                UpdateMenuVisibility(false);
            }
        }
    }

    public void UpdateMenuVisibility(bool isVisible)
    {
        if (IsCurrentlyVisible == isVisible) { return; }

        _menuParent.SetActive(isVisible);

        if (IsCurrentlyVisible != isVisible)
        {
            if (isVisible)
            {
                _handMenuManager.PlayMenuAudio(_menuParent.transform.position, MenuStates.Appearing);
            }
            else
            {
                _handMenuManager.PlayMenuAudio(_menuParent.transform.position, MenuStates.Disappearing);
            }
        }

        IsCurrentlyVisible = isVisible;
    }

    public void UpdateButtonsActive(bool resetIsActive, bool backIsActive)
    {
        // When the POIPlanetFocusManager is present in the currently loaded scenes, this means we are in the solar system or galactic center, so activate the  reset button
        _resetButton?.SetActive(resetIsActive);

        // If there is previous scene then user should able to go back, so activate the back button
        // ...unless it is the Dock button, which is always wanted.
        _backButton?.SetActive(_backBringsDock || backIsActive);
    }

    private void OnTrackingLost()
    {
        UpdateMenuVisibility(false);
    }

    private float CalculateAngle()
    {
        return Vector3.Angle(PalmDirection(), _cameraTransform.forward);
    }

    // The way the palm faces. Measured from the hand's joints when a hand is tracked; otherwise the menu's own
    // forward, as it always was — that is the controller's pose when controllers are in use.
    private Vector3 PalmDirection()
    {
        var rig = XRInputRig.Instance;
        if (rig != null && _handMenuManager != null &&
            rig.TryGetPalmNormal(_handMenuManager.IsLeftMenu(this), out var normal))
        {
            return normal;
        }

        return transform.forward;
    }
}