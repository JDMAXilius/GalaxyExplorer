// p4_bake.cs - Phase 4, step one. Runs Cosmic/Build/Bake All twice and checks what came out: five
// Galaxy assets of three layers each, every layer holding a baked PointCloud of the exact point count
// its spec implies (ellipses x starsPerEllipse x armCount), a 40-byte StarVert stride, the 40 000-point
// cosmic web, seven 28 000-point nebula volumes at 0.35 m, and every points_*.mat on Cosmic/Points with
// instancing off. The second bake is the idempotency gate - same GUIDs, no new files.
//
//   pwsh tools/mcp/compile.ps1                              # always first
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_bake.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'
//
// It takes a while: the two bakes generate about 250 000 points each and re-read seven nebula plates
// through a RenderTexture blit. The command returns when both are done, so expect a slow reply rather
// than a fast one followed by results.
//
// Point materials must have instancing OFF. Points uploads its cloud as a StructuredBuffer and draws it
// with DrawProcedural, so there is nothing to instance; an instanced variant only costs a keyword and a
// second shader permutation on device.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P4 bake: refused, the editor is in play mode and Bake All will not write. " +
                       "Run tools/mcp/rework/p4_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Bake");
        result.Log(ok
            ? "P4 bake: ran Cosmic/Verify/P4 Bake. Read the [P4] lines with Unity_GetConsoleLogs.\n" +
              "PASS looks like `[P4] DONE 40/40` with no `[P4] FAIL`. A missing plate or a missing place shows up\n" +
              "first in Bake's own `Cosmic nebulae: plate ... is missing` or `no place at ...` line above them."
            : "P4 bake: the menu item Cosmic/Verify/P4 Bake does not exist - Cosmic.Editor did not compile. " +
              "Run `pwsh tools/mcp/compile.ps1` and read the error CS#### lines.");
    }
}
