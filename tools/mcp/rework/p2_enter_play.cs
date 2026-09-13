// p2_enter_play.cs - CS-129, step one. Presses Play. It does NOT quit the editor and it does NOT open
// a scene.
//
//   node tools/mcp/umcp.js run tools/mcp/rework/p2_enter_play.cs
//
// Assigning EditorApplication.isPlaying is the only thing that works through the relay: delayCall never
// fires, the command reports success, and play mode simply never starts (that was CS-119). isPlaying
// reads false on the frame it is assigned - poll it, do not trust the reply below. Entering play mode
// reloads the domain, so expect "Unity not detected" for a few seconds afterwards.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("P2 play: already in play mode, nothing to do.");
            return;
        }

        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            result.Log("P2 play: refused, the editor is still compiling or importing. Wait it out with " +
                       "tools/mcp/compile.ps1 and ask again.");
            return;
        }

        if (!UnityEditor.EditorApplication.ExecuteMenuItem("Cosmic/Verify/P2 Enter Play"))
        {
            UnityEditor.EditorApplication.isPlaying = true;
        }

        result.Log("P2 play: requested. isPlaying is still false on this frame and that is expected - poll it.\n" +
                   "Switch Error Pause OFF in the console before running p2_run.cs: a FAIL is a Debug.LogError " +
                   "and would pause play mode halfway through the sequence.");
    }
}
