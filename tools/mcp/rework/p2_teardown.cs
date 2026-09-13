// p2_teardown.cs - CS-130, step two. Removes cosmic_verify (and any stray verify objects) from the open
// scene and saves it. It deliberately keeps Assets/Cosmic/Data/Generated/audio_library.asset and
// Assets/Cosmic/Prefabs/room_dim.mat: those are real assets Phase 6 wires into the scene, not fixtures.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p2_teardown.cs
//
// Play mode writes runtime values into shared materials. Check `git status` before committing and revert
// anything under Assets/materials/ that the session touched.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P2 teardown: refused, the editor is in play mode - the save would be thrown away. " +
                       "Run tools/mcp/rework/p2_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P2 Teardown");
        result.Log(ok
            ? "P2 teardown: ran Cosmic/Verify/P2 Teardown. Read the [P2] teardown line with " +
              "Unity_GetConsoleLogs for how many objects were removed and from which scene."
            : "P2 teardown: the menu item Cosmic/Verify/P2 Teardown does not exist - Cosmic.Editor did not compile.");
    }
}
