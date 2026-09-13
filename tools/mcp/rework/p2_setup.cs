// p2_setup.cs - CS-128. Creates or updates the two real assets Phase 6 will wire (audio_library.asset,
// room_dim.mat) and puts a `cosmic_verify` host carrying Cosmic.Audio and Cosmic.Room into the scene
// that is open right now, then saves that scene so the objects survive the play-mode domain reload.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p2_setup.cs
//
// It never opens a scene. EditorSceneManager.OpenScene in Single mode raises a modal save prompt that
// blocks the relay outright. Open the host scene by hand first - a saved one; the menu item refuses an
// untitled scene for the same reason, since SaveScene with no path opens a file dialog.
// Assets/scenes/development_scenes/solar_system_prefab_scene.unity is the recommended host: it is small
// and it already has a camera tagged MainCamera, which the dim and Black checks need.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P2 setup: refused, the editor is in play mode. Anything written now is thrown away " +
                       "when play stops. Run tools/mcp/rework/p2_leave_play.cs first.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P2 Setup");
        result.Log(ok
            ? "P2 setup: ran Cosmic/Verify/P2 Setup against the active scene \"" + scene.name + "\" (" +
              (string.IsNullOrEmpty(scene.path) ? "NEVER SAVED - the menu item will have refused" : scene.path) +
              ").\nRead the [P2] setup line with Unity_GetConsoleLogs: it says what was created versus updated " +
              "and whether Camera.main was found."
            : "P2 setup: the menu item Cosmic/Verify/P2 Setup does not exist - Cosmic.Editor did not compile.");
    }
}
