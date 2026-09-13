// p3_enter_play.cs - CS-140, step two. Presses Play. It does NOT quit the editor and it does NOT open
// a scene.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p3_enter_play.cs
//
// Assigning EditorApplication.isPlaying is the only thing that works through the relay: delayCall never
// fires, the command reports success, and play mode simply never starts (that was CS-119). isPlaying
// reads false on the frame it is assigned - poll it, do not trust the reply below. Entering play mode
// reloads the domain, so expect "Unity not detected" for a few seconds afterwards.
//
// XRInputModalityManager logs one warning per session on a desktop editor - "Hand Tracking Subsystem not
// found or not running" - because there is no headset. It is expected and harmless; it lands after this
// command has already returned, so it is the console you read it in, not this result.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P3 play: already in play mode, nothing to do.");
            return;
        }

        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            result.Log("P3 play: refused, the editor is still compiling or importing. Wait it out with " +
                       "tools/mcp/compile.ps1 and ask again.");
            return;
        }

        if (!UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P3 Enter Play"))
        {
            UnityEditor.EditorApplication.isPlaying = true;
        }

        result.Log("P3 play: requested. isPlaying is still false on this frame and that is expected - poll it.\n" +
                   "Before p3_run.cs: switch Error Pause OFF (a FAIL is a Debug.LogError and would pause play\n" +
                   "mode halfway), make sure a Game view is visible, and keep the real mouse off it - the run\n" +
                   "drives the same device with synthetic events and a physical move fights them.");
    }
}
