// p5_run.cs - Phase 5. Cosmic/Verify/P5 Run: drives the dock, popup, utility window, panel and label with synthetic mouse and keyboard events for about 15 s and writes [P5] PASS / FAIL / SKIP lines plus one [P5] DONE n/m.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_run.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Run");
        result.Log(ok
            ? "P5 Run: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Run: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
