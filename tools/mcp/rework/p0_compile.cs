// p0_compile.cs - CS-126. Asks the editor which Cosmic assemblies are loaded and whether the new
// input actions asset imported. Run tools/mcp/compile.ps1 FIRST; this only reports, it cannot compile.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p0_compile.cs
//
// Fully qualified with no `using` directives, like everything in tools/mcp/: the runner wraps the file
// in a preamble of its own and a using block landing after it will not compile. The work is in a menu
// item (Cosmic/Verify/P0 Compile Check) rather than here because the runner's throwaway assembly does
// not reference Cosmic.Runtime, and because a missing menu item is itself the compile verdict.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P0 Compile Check");
        result.Log(ok
            ? "P0: ran Cosmic/Verify/P0 Compile Check. Read the [P0] lines with Unity_GetConsoleLogs.\n" +
              "Expect: PASS both Cosmic assemblies are present, and PASS the actions asset imports as an " +
              "InputActionAsset with both the XR and Desktop maps (XR=10 actions, Desktop=29)."
            : "P0: the menu item Cosmic/Verify/P0 Compile Check does not exist, which means Cosmic.Editor " +
              "did not compile. Run `pwsh tools/mcp/compile.ps1` and read the error CS#### lines.");
    }
}
