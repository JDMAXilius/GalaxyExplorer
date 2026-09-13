// p5_teardown.cs - Phase 5. Cosmic/Verify/P5 Teardown: removes cosmic_verify and the rig instance the setup made, wakes any parked camera, and saves the scene; the UI prefabs and theme are kept.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_teardown.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P5: refused, the editor is in play mode. Run tools/mcp/rework/p5_leave_play.cs first.");
            return;
        }
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Teardown");
        result.Log(ok
            ? "P5 Teardown: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Teardown: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
