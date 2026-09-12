// leave_play_mode.cs - stops play mode. It does NOT quit the editor.
//
//   node tools/mcp/umcp.js run tools/mcp/leave_play_mode.cs
//
// Named in full for the same reason as enter_play_mode.cs: the old scratchpad pair was `stop.cs`
// (left play mode) and `exit.cs` (quit the editor), and the names were one keystroke apart.
// Nothing in tools/mcp/ quits the editor.
//
// isPaused is cleared before isPlaying, and both are set again on delayCall. That is not belt and
// braces: after a crash-out isPlaying can read a stale True, and setting it false once from inside
// a command that is itself running during the tear-down does not always take. Compiling against a
// stale True is how scripts get edited "during play mode".

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying && !UnityEditor.EditorApplication.isPaused)
        {
            result.Log("STOP: not in play mode, nothing to do.");
            return;
        }

        UnityEditor.EditorApplication.isPaused = false;
        UnityEditor.EditorApplication.isPlaying = false;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            UnityEditor.EditorApplication.isPaused = false;
            UnityEditor.EditorApplication.isPlaying = false;
        };

        result.Log("STOP: leaving play mode. The editor stays open.\n" +
                   "Confirm with tools/mcp/compile.ps1 (it reports playing=) before editing any script.\n" +
                   "Play mode writes runtime values into shared materials - about_material, earth_clouds, " +
                   "the Jupiter clouds. Check `git status` and revert those before committing.");
    }
}
