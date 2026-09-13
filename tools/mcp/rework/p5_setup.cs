// p5_setup.cs - Phase 5. Cosmic/Verify/P5 Setup: instantiates the rig if the scene has none, parks a host MainCamera, and puts the dock, a body panel, a card label and a target sphere under cosmic_verify, then saves the scene.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p5_setup.cs
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
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P5 Setup");
        result.Log(ok
            ? "P5 Setup: started. Read the console: node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'"
            : "P5 Setup: the menu item does not exist - Cosmic.Editor did not compile.");
    }
}
