// p3_run.cs - CS-140, step three. Starts the scripted desktop feel test on cosmic_verify_body and
// returns immediately.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p3_run.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'      # ~20 s later
//
// Execute returns straight away on purpose: the sequence spans a 2 s dwell, a 0.8 s restore tween and a
// 5 s out-of-reach grace, so a synchronous command could only ever report its first frame. It runs off
// EditorApplication.update, which ticks in play mode, and writes [P3] PASS / [P3] FAIL / [P3] SKIP lines
// plus one [P3] DONE n/m line at the end. Running this again while it is still going reports the step it
// has reached rather than starting a second sequence; to start over, leave play mode and enter it again,
// which clears the statics with the domain reload.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P3 run: refused, not in play mode. Run tools/mcp/rework/p3_enter_play.cs, poll " +
                       "isPlaying, then run this.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P3 Run");
        result.Log(ok
            ? "P3 run: started. It takes about 20 s. Wait, then read the console:\n" +
              "  node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'\n" +
              "PASS looks like `[P3] DONE 31/31` with no `[P3] FAIL`, plus one `[P3] SKIP the orbit` line\n" +
              "while Mouse.pivot is still unassigned in the rig (32/32 once a pivot is wired)."
            : "P3 run: the menu item Cosmic/Verify/P3 Run does not exist - Cosmic.Editor did not compile.");
    }
}
