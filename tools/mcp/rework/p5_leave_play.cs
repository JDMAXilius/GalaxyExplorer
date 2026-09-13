// p5_leave_play.cs - Phase 5. Cosmic/Verify/P5 Leave Play: leaves play mode; the editor stays open.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_leave_play.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Leave Play");
        result.Log(ok
            ? "P5 Leave Play: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Leave Play: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
