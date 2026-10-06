// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;

/// <summary>
/// Hand menu button that forces the room visible or lets the experience decide, the same toggle as the dock's
/// passthrough button: the room has one owner, <see cref="EnvironmentController"/>, and this only asks it.
/// Its label names the mode it switches to. Hidden on other platforms.
/// </summary>
[RequireComponent(typeof(GEButton))]
public class ExperienceModeButton : MonoBehaviour
{
    [SerializeField]
    private TMP_Text[] labels = new TMP_Text[0];

    private void Awake()
    {
        GetComponent<GEButton>().OnClick.AddListener(OnClicked);
        EnvironmentController.ModeChanged += OnModeChanged;
    }

    private void Start()
    {
        gameObject.SetActive(GalaxyExplorerManager.IsQuest3);
        Refresh();
    }

    private void OnDestroy()
    {
        EnvironmentController.ModeChanged -= OnModeChanged;
    }

    private void OnClicked()
    {
        EnvironmentController.Instance?.TogglePassthrough();
        Refresh();
    }

    private void OnModeChanged(EnvironmentMode mode) => Refresh();

    private void Refresh()
    {
        var forced = EnvironmentController.Instance != null && EnvironmentController.Instance.PassthroughForced;
        var label = forced ? "VR" : "Room";
        foreach (var text in labels)
        {
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
