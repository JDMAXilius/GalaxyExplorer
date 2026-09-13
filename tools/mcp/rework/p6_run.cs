// p6_run.cs - Phase 6. Cosmic/Verify/P6 Run: is the smoke run: skips the logo, clicks the floor to place the content, then opens every dock place in order, opens and closes the Helix destination and restores; about 20 s; ends with [P6] DONE n/m.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_run.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Run");
        result.Log(ok
            ? "P6 Run: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P6 Run: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
