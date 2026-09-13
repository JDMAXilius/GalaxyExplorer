// p6_leave_play.cs - Phase 6. Cosmic/Verify/P6 Leave Play: leaves play mode.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_leave_play.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Leave Play");
        result.Log(ok
            ? "P6 Leave Play: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P6 Leave Play: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
