// p6_build.cs - Phase 6. Cosmic/Verify/P6 Build: runs Cosmic/Build/Main Scene twice and checks Assets/Cosmic/Scenes/main.unity: App, Director, Anchor and Panels wired, seven dock places, one MainCamera, first in Build Settings, GUID stable.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_build.cs
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
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Build");
        result.Log(ok
            ? "P6 Build: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P6 Build: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
