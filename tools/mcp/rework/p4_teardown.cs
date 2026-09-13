// p4_teardown.cs - Phase 4, step seven. Removes the `cosmic_verify` root and the two place instances
// under it from the open scene and saves it. It keeps the rig instance, the place and body prefabs, the
// baked clouds and the point materials: none of those is a fixture, they are the content Phase 6 builds
// the one shipping scene out of.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_teardown.cs
//
// Play mode writes runtime values into shared materials, and Phase 4 draws more of them than any phase
// before it. Check `git status` before committing and revert anything under Assets/materials/ or
// Assets/Cosmic/Prefabs/materials/ that the session touched.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P4 teardown: refused, the editor is in play mode - the save would be thrown away. " +
                       "Run tools/mcp/rework/p4_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Teardown");
        result.Log(ok
            ? "P4 teardown: ran Cosmic/Verify/P4 Teardown. Read the [P4] teardown line with " +
              "Unity_GetConsoleLogs for how many roots were removed and from which scene."
            : "P4 teardown: the menu item Cosmic/Verify/P4 Teardown does not exist - Cosmic.Editor did not compile.");
    }
}
