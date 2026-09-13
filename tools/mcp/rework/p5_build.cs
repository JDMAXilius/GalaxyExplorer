// p5_build.cs - Phase 5. Cosmic/Verify/P5 Build: runs Cosmic/Build/UI twice and checks the theme, the nine UI prefabs, the dock's tiles, popup, utility window and drag-bar collider, and that the second run keeps every GUID.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_build.cs
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
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Build");
        result.Log(ok
            ? "P5 Build: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Build: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
