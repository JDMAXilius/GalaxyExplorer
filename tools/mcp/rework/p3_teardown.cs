// p3_teardown.cs - CS-140, step five. Removes cosmic_verify_body and the rig instance from the open scene
// and saves it. It keeps Assets/Cosmic/Prefabs/rig.prefab, which is not a fixture: it is the rig Phase 6
// builds the one shipping scene around.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p3_teardown.cs
//
// Play mode writes runtime values into shared materials. Check `git status` before committing and revert
// anything under Assets/materials/ that the session touched.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P3 teardown: refused, the editor is in play mode - the save would be thrown away. " +
                       "Run tools/mcp/rework/p3_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P3 Teardown");
        result.Log(ok
            ? "P3 teardown: ran Cosmic/Verify/P3 Teardown. Read the [P3] teardown line with " +
              "Unity_GetConsoleLogs for how many objects were removed and from which scene."
            : "P3 teardown: the menu item Cosmic/Verify/P3 Teardown does not exist - Cosmic.Editor did not compile.");
    }
}
