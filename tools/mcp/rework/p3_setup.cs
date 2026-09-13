// p3_setup.cs - CS-140, step one. Puts an instance of Assets/Cosmic/Prefabs/rig.prefab into the scene
// that is open right now (if one is not there already) and builds cosmic_verify_body next to it: a 15 cm
// sphere two metres in front of the rig camera at 1.2 m, carrying Grabbable, XRGeneralGrabTransformer and
// Pull, then saves the scene so both survive the play-mode domain reload.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p3_setup.cs
//
// It never opens a scene: EditorSceneManager.OpenScene in Single mode raises a modal save prompt that
// blocks the relay outright. Open a saved scene by hand first, and open one with NO camera tagged
// MainCamera in it - the rig brings its own, and two of them make Camera.main arbitrary, which quietly
// aims Mouse, Pull and Room through the wrong eye. The menu item reports that case as a [P3] FAIL.
//
// Two metres, not one, is deliberate: Mouse parks its attach transform 1 m down the ray and Pull treats
// anything within 12 cm of that attach as a near grab, so a body at exactly 1 m would never dwell-pull.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P3 setup: refused, the editor is in play mode. Anything written now is thrown away " +
                       "when play stops. Run tools/mcp/rework/p3_leave_play.cs first.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P3 Setup");
        result.Log(ok
            ? "P3 setup: ran Cosmic/Verify/P3 Setup against the active scene \"" + scene.name + "\" (" +
              (string.IsNullOrEmpty(scene.path) ? "NEVER SAVED - the menu item will have refused" : scene.path) +
              ").\nRead the [P3] setup line with Unity_GetConsoleLogs: it says where the body landed, which " +
              "camera it was measured from, and whether a second MainCamera is in the way."
            : "P3 setup: the menu item Cosmic/Verify/P3 Setup does not exist - Cosmic.Editor did not compile.");
    }
}
