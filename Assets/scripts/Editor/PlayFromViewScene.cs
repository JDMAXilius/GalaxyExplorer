// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq;
using GalaxyExplorer;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Pressing Play while a view scene (galaxy, solar system, galactic center) is open starts the app from
/// main_scene with the core systems, skips the intro, and opens that view directly. Works for desktop (mouse)
/// and headset (Quest Link) testing. The scene you had open is restored when Play mode ends.
/// </summary>
[InitializeOnLoad]
public static class PlayFromViewScene
{
    private const string MainScenePath = "Assets/scenes/main_scene.unity";

    private static readonly string[] ViewScenes =
    {
        "galaxy_view_scene",
        "solar_system_view_scene",
        "galactic_center_view_scene",
    };

    static PlayFromViewScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.activeSceneChangedInEditMode += (_, __) => UpdateStartScene();
        EditorApplication.delayCall += UpdateStartScene;
    }

    private static string OpenViewScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        return EditorSceneManager.sceneCount == 1 && ViewScenes.Contains(active.name) ? active.name : null;
    }

    // Play mode start scene must be set before Play is pressed.
    private static void UpdateStartScene()
    {
        EditorSceneManager.playModeStartScene = OpenViewScene() != null
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath)
            : null;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            var view = OpenViewScene();
            if (view != null)
            {
                SessionState.SetString(IntroFlow.QuickStartViewKey, view);
            }
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseString(IntroFlow.QuickStartViewKey);
            UpdateStartScene();
        }
    }
}
