// p4_setup.cs - Phase 4, step three. Puts an instance of Assets/Cosmic/Prefabs/rig.prefab into the
// scene that is open right now (if one is not there already), then a `cosmic_verify` root holding two
// place prefabs: milky_way 1.5 m in front of the rig camera at eye height, and the solar system place
// 2.5 m in front at floor level. It saves the scene so both survive the play-mode domain reload.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_setup.cs
//
// It never opens a scene: EditorSceneManager.OpenScene in Single mode raises a modal save prompt that
// blocks the relay outright. Open a saved scene by hand first, and open one with NO camera tagged
// MainCamera - the rig brings its own, and two of them make Camera.main arbitrary, which quietly aims
// Mouse, Points and Sun through the wrong eye. The menu item reports that case as a [P4] FAIL.
//
// The solar system sits on the floor, not at eye height, because the row and relative layouts carry an
// absolute 1.2 m slot height of their own; parented at eye height every planet would end up at 2.3 m.
//
// Setup also assigns Sun.mouse through SerializedObject. That is Phase 6 wiring and the builder leaves
// it null; without it Sun.Hovering can never return true and the desktop touch test in p4_run.cs could
// only ever be skipped. Sun.leftHand and Sun.rightHand stay null on purpose - Sun.Touching only falls
// through to the mouse while neither hand has moved, and a null hand never moves.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P4 setup: refused, the editor is in play mode. Anything written now is thrown away " +
                       "when play stops. Run tools/mcp/rework/p4_leave_play.cs first.");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Setup");
        result.Log(ok
            ? "P4 setup: ran Cosmic/Verify/P4 Setup against the active scene \"" + scene.name + "\" (" +
              (string.IsNullOrEmpty(scene.path) ? "NEVER SAVED - the menu item will have refused" : scene.path) +
              ").\nRead the [P4] setup line with Unity_GetConsoleLogs: it says where each place landed, which\n" +
              "camera they were measured from, whether a second MainCamera is in the way, and whether Sun.mouse\n" +
              "came through. If it reports the Phase 2 host instead, run Cosmic/Verify/P2 Teardown and try again -\n" +
              "that object is also called cosmic_verify and carries a Room that would fight the rig's."
            : "P4 setup: the menu item Cosmic/Verify/P4 Setup does not exist - Cosmic.Editor did not compile.");
    }
}
