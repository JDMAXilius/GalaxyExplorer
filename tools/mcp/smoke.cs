// smoke.cs - the end-to-end smoke test of the rework, rewritten against the Cosmic API.
//
// It is the Phase 6 harness in sequence. Each line is one relay command, because a domain reload or a
// play-mode transition ends the command that asked for it:
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_build.cs        # writes Assets/Cosmic/Scenes/main.unity and checks it
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_setup.cs        # opens it additively, parks the host camera
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_enter_play.cs   # then poll isPlaying
//   node tools/mcp/umcp.js run tools/mcp/smoke.cs                  # this file: starts Cosmic/Verify/P6 Run
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'          # ~20 s later: [P6] DONE n/m, no FAIL
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_leave_play.cs
//   node tools/mcp/umcp.js run tools/mcp/rework/p6_teardown.cs
//
// The old smoke harness for the legacy tree is kept beside this file as smoke_legacy.cs until Phase 7
// deletes the tree it drives.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("smoke: refused, not in play mode. Run tools/mcp/rework/p6_setup.cs and p6_enter_play.cs first.");
            return;
        }
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P6 Run");
        result.Log(ok
            ? "smoke: started. Boot, intro, every place, a destination, restore - about 20 s. Then read the console."
            : "smoke: Cosmic/Verify/P6 Run does not exist - Cosmic.Editor did not compile.");
    }
}
