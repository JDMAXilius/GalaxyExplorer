// p2_run.cs - CS-129, step two. Starts the scripted Phase 2 sequence and returns immediately.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p2_run.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'      # ~15 s later
//
// Execute returns straight away on purpose. The sequence spans crossfades and fades measured in whole
// seconds, so a synchronous command could only ever report its first frame. It runs off
// EditorApplication.update, which ticks in play mode, and writes [P2] PASS / [P2] FAIL lines plus one
// [P2] DONE n/m line at the end. Running this a second time while it is still going reports the step
// it has reached rather than starting a second sequence.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P2 run: refused, not in play mode. Run tools/mcp/rework/p2_enter_play.cs, poll " +
                       "isPlaying, then run this.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P2 Run");
        result.Log(ok
            ? "P2 run: started. It takes about 15 s. Wait, then read the console:\n" +
              "  node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'\n" +
              "PASS looks like `[P2] DONE 30/30` with thirty `[P2] PASS` lines and no `[P2] FAIL`."
            : "P2 run: the menu item Cosmic/Verify/P2 Run does not exist - Cosmic.Editor did not compile.");
    }
}
