// p3_build_rig.cs - CS-139. Runs Cosmic/Build/Rig twice and checks what came out:
// Assets/Cosmic/Prefabs/rig.prefab with an XROrigin, an interaction manager, the input action manager,
// the modality manager, Room, Audio and Hotkeys on the root; one MainCamera with a TrackedPoseDriver and
// an ARCameraManager; both hands with a NearFarInteractor and an XRPokeInteractor; the desktop Mouse
// interactor; one EventSystem on the XR UI input module. The second build is the idempotency gate -
// same GUID, no duplicated camera or event system.
//
//   pwsh tools/mcp/compile.ps1                              # always first
//   node tools/mcp/umcp.js run tools/mcp/rework/p3_build_rig.cs
//   node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'
//
// Run Cosmic/Verify/P2 Setup before this if room_dim.mat and audio_library.asset do not exist yet: the
// builder wires both into the rig, and a rig with a null Room.dimMaterial logs Room's own error the
// moment play mode starts. The [P3] rig inputs line says which of the two came through.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P3 rig: refused, the editor is in play mode and the prefab write would be lost. " +
                       "Run tools/mcp/rework/p3_leave_play.cs first.");
            return;
        }

        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P3 Build Rig");
        result.Log(ok
            ? "P3 rig: ran Cosmic/Verify/P3 Build Rig. Read the [P3] lines with Unity_GetConsoleLogs.\n" +
              "PASS looks like `[P3] DONE 22/22` with no `[P3] FAIL`. A missing interactor or hand prefab shows up\n" +
              "first in Scene.cs's own `Cosmic rig ... missing:` line, which is printed just above them."
            : "P3 rig: the menu item Cosmic/Verify/P3 Build Rig does not exist - Cosmic.Editor did not compile. " +
              "Run `pwsh tools/mcp/compile.ps1` and read the error CS#### lines.");
    }
}
