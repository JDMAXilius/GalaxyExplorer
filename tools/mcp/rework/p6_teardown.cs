// p6_teardown.cs - Phase 6. Cosmic/Verify/P6 Teardown: closes the additive main scene without saving and wakes any parked camera.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_teardown.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P6: refused, the editor is in play mode. Run tools/mcp/rework/p6_leave_play.cs first.");
            return;
        }
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Teardown");
        result.Log(ok
            ? "P6 Teardown: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P6 Teardown: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
