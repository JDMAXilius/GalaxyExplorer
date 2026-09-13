// p4_run.cs - Phase 4, step five. Starts the scripted content test on the two places p4_setup.cs put in
// the scene and returns immediately.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_run.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'      # ~20 s later
//
// Execute returns straight away on purpose: the sequence spans two 0.8 s layout tweens, a 1 s realism
// tween, 2.5 s of watching mercury move along its orbit and a 0.8 s restore tween, so a synchronous
// command could only ever report its first frame. It runs off EditorApplication.update, which ticks in
// play mode, and writes [P4] PASS / [P4] FAIL / [P4] SKIP lines plus one [P4] DONE n/m line at the end.
// Running this again while it is still going reports the step it has reached rather than starting a
// second sequence; to start over, leave play mode and enter it again, which clears the statics with the
// domain reload.
//
// What it drives, in order: the galaxy's command buffer on Camera.main; relative_size then solar_row,
// asserting every anchor moved and landed within 5 mm of its slot; solar_schematic, asserting Orbit runs
// at realism 0 and mercury travels; solar_realistic, asserting realism reaches 1 and the sun anchor
// collapses to 0.1 mm; the orbit ring command buffer; the sun's touch brightness under the desktop
// cursor; and a drag-and-restore on the milky_way root.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P4 run: refused, not in play mode. Run tools/mcp/rework/p4_enter_play.cs, poll " +
                       "isPlaying, then run this.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Run");
        result.Log(ok
            ? "P4 run: started. It takes about 16 s. Wait, then read the console:\n" +
              "  node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'\n" +
              "PASS looks like `[P4] DONE 28/28` with no `[P4] FAIL`, plus one `[P4] SKIP` line: the alpha-0\n" +
              "draw, which is not observable from script because the command buffer records the same draw at any\n" +
              "alpha. The _Age advance is asserted through Points.Instance(layer)."
            : "P4 run: the menu item Cosmic/Verify/P4 Run does not exist - Cosmic.Editor did not compile.");
    }
}
