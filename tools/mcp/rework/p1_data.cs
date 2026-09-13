// p1_data.cs - CS-127. Runs Cosmic/Import Copy twice and checks that the second run changed no GUID,
// then prints the parity command the terminal runs next and the residual loss classes to expect.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p1_data.cs
//   python3 tools/parity/check_data.py        # <- afterwards, from the repo root
//
// Import Copy writes assets, so this takes a few seconds and the reply may arrive after a refresh.
// "Unity not detected" on the next call is the domain reload, not a fault.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P1: refused, the editor is in play mode. Leave play mode first - importing assets " +
                       "under play is how a half-written asset gets committed.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P1 Import And Check");
        result.Log(ok
            ? "P1: ran Cosmic/Verify/P1 Import And Check. Read the [P1] lines with Unity_GetConsoleLogs,\n" +
              "then run, from the repo root: python3 tools/parity/check_data.py"
            : "P1: the menu item Cosmic/Verify/P1 Import And Check does not exist - Cosmic.Editor did not " +
              "compile. Run `pwsh tools/mcp/compile.ps1` first.");
    }
}
