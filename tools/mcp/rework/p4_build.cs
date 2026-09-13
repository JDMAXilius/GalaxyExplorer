// p4_build.cs - Phase 4, step two. Runs Cosmic/Build/Layouts, Cosmic/Build/Bodies and
// Cosmic/Build/Places in that order, twice, and checks what came out: the four solar layouts, a prefab
// for each of the eighteen places that have content, the milky_way `galaxy` child on Points with three
// wired layers, the helix `volume` child with one, the system place on Rig + Orbit with ten home_<id>
// anchors each nesting a body_<id> that carries Grabbable, Pull and a SphereCollider, saturn's `rings`
// child, and Sun on the sun body. The second run is the idempotency gate - same GUIDs, no new prefabs.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p4_build.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'
//
// Run p4_bake.cs first. Places nests the baked clouds and materials into the place prefabs, so a build
// against an empty Data/Generated/points writes prefabs whose Points layers are all null - which the
// `every milky_way Points layer carries a cloud and a material` assertion catches, one step too late to
// be useful.
//
// The order matters and the wrapper stops at the first missing menu item: Layouts writes slots that
// reference Body assets, Bodies writes the prefabs Places nests, and Places writes the roots the whole
// of P4 Run drives.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P4 build: refused, the editor is in play mode and every prefab write would be lost. " +
                       "Run tools/mcp/rework/p4_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P4 Build");
        result.Log(ok
            ? "P4 build: ran Cosmic/Verify/P4 Build. Read the [P4] lines with Unity_GetConsoleLogs.\n" +
              "PASS looks like `[P4] DONE 17/17` with no `[P4] FAIL`. Watch for the plain `[P4] the rig was found\n" +
              "on solar_system.prefab` line: the generated data puts the ten bodies on solar_system.asset and the\n" +
              "four layouts on solar_system_planets.asset, and the harness follows whichever place the builder\n" +
              "actually put the Rig on."
            : "P4 build: the menu item Cosmic/Verify/P4 Build does not exist - Cosmic.Editor did not compile.");
    }
}
