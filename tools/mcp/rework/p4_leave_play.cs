// p4_leave_play.cs - Phase 4, step six. Stops play mode. It does NOT quit the editor.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_leave_play.cs
//
// isPaused is cleared before isPlaying and both are set again on delayCall: after a crash-out isPlaying
// can read a stale True, and compiling against a stale True is how scripts get edited during play mode.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying && !UnityEditor.EditorApplication.isPaused)
        {
            result.Log("P4 stop: not in play mode, nothing to do.");
            return;
        }

        if (!UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Leave Play"))
        {
            UnityEditor.EditorApplication.isPaused = false;
            UnityEditor.EditorApplication.isPlaying = false;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            UnityEditor.EditorApplication.isPaused = false;
            UnityEditor.EditorApplication.isPlaying = false;
        };

        result.Log("P4 stop: leaving play mode. The editor stays open.\n" +
                   "Confirm with tools/mcp/compile.ps1 (it reports playing=) before editing any script.");
    }
}
