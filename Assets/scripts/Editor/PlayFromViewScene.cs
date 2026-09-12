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

    /// <summary>
    /// The one view scene that is open, or null if that is not the situation.
    ///
    /// <para><b>Counts view scenes, not scenes.</b> This used to require <c>sceneCount == 1</c>, which meant it
    /// armed only when a view scene was open entirely by itself - and it almost never is, because working on
    /// anything needs <c>core_systems_scene</c> (the director, the dock, the audio) and <c>main_scene</c>
    /// alongside it. So the quick start silently did not arm, play mode ran the full intro, and the intro's
    /// placement step had a view scene already sitting in the room that it did not put there. It never reached
    /// its galaxy stage, never raised <c>OnIntroFinished</c>, and <c>ExperienceDirector.Switch</c> then refused
    /// every dock tile and every destination tag for the rest of the session - an app in which nothing at all
    /// responds, with no error anywhere to say why.</para>
    ///
    /// <para>Two or more view scenes open at once is still refused: which one the player should be standing in
    /// is then genuinely ambiguous, and guessing would be worse than running the intro.</para>
    /// </summary>
    private static string OpenViewScene()
    {
        string found = null;

        for (var i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var name = EditorSceneManager.GetSceneAt(i).name;
            if (!ViewScenes.Contains(name))
            {
                continue;
            }

            if (found != null)
            {
                return null;
            }

            found = name;
        }

        return found;
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
