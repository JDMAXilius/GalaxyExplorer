// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer;
using GalaxyExplorer.XR;
using TMPro;
using UnityEngine;

/// <summary>
/// Hand menu button that switches Quest 3 between passthrough and VR. Its label names the mode it switches to.
/// Hidden on other platforms.
/// </summary>
[RequireComponent(typeof(GEButton))]
public class ExperienceModeButton : MonoBehaviour
{
    [SerializeField]
    private TMP_Text[] labels = new TMP_Text[0];

    private void Awake()
    {
        GetComponent<GEButton>().OnClick.AddListener(OnClicked);
        ExperienceModeManager.ModeChanged += OnModeChanged;
    }

    private void Start()
    {
        gameObject.SetActive(GalaxyExplorerManager.IsQuest3);
        if (ExperienceModeManager.Instance != null)
        {
            OnModeChanged(ExperienceModeManager.Instance.CurrentMode);
        }
    }

    private void OnDestroy()
    {
        ExperienceModeManager.ModeChanged -= OnModeChanged;
    }

    private void OnClicked()
    {
        if (ExperienceModeManager.Instance != null)
        {
            ExperienceModeManager.Instance.Toggle();
        }
    }

    private void OnModeChanged(ExperienceModeManager.Mode mode)
    {
        var label = mode == ExperienceModeManager.Mode.Passthrough ? "VR" : "Room";
        foreach (var text in labels)
        {
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
