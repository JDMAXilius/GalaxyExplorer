// app_check.cs - runs Cosmic Simulation/Verify/App Check against the shipping app (main_scene).
//
//   node tools/mcp/umcp.js run tools/mcp/legacy/app_check.cs
//   cat Logs/app_check.log
//
// Enter play mode first (tools/mcp/enter_play_mode.cs) and let the app boot. The walk clicks every dock
// tile and a Milky Way destination tag with real mouse events and scores what happens; its verdict is the
// [APP] lines in Logs/app_check.log, not the relay's OK/NOT-OK.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("APP: refused, the editor is not in play mode. Run tools/mcp/enter_play_mode.cs first.");
            return;
        }
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic Simulation/Verify/App Check");
        result.Log(ok
            ? "APP: started. Read the verdict: cat Logs/app_check.log"
            : "APP: the menu item does not exist - Assets/scripts/Editor/AppCheck.cs did not compile.");
    }
}
