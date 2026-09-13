// p6_setup.cs - Phase 6. Cosmic/Verify/P6 Setup: opens main.unity additively (never Single, so no dialog can block the relay) and parks any other active MainCamera.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_setup.cs
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
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Setup");
        result.Log(ok
            ? "P6 Setup: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P6 Setup: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
