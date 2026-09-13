// p5_enter_play.cs - Phase 5. Cosmic/Verify/P5 Enter Play: enters play mode by assigning EditorApplication.isPlaying; poll isPlaying afterwards, it reads false on this frame.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_enter_play.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Enter Play");
        result.Log(ok
            ? "P5 Enter Play: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Enter Play: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
