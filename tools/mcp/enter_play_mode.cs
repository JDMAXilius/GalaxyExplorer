// enter_play_mode.cs - presses Play. It does NOT quit the editor and it does NOT leave play mode.
//
//   node tools/mcp/umcp.js run tools/mcp/enter_play_mode.cs
//
// Named in full on purpose. A previous session kept a `stop.cs` that left play mode next to an
// `exit.cs` that quit the editor outright, and reaching for the wrong one cost a whole session's
// open scenes. Nothing in tools/mcp/ quits the editor.
//
// Entering play mode reloads the domain, which unloads the assembly this very command is running
// in. The request therefore has to return straight away: the reply below is the last thing this
// assembly says, and "Unity not detected" on the next call for a few seconds afterwards is the
// reload, not a fault.
//
// HOW IT ASKS, AND WHY IT MATTERS (learned the hard way, 12 Sep 2026 - this is CS-119).
// This used to queue EnterPlaymode() on EditorApplication.delayCall. That does not work through the
// relay: delayCall never fires, the command reports success, and play mode simply never starts.
// Four separate approaches were tried across two sessions and all of them were variations on the
// same mistake. Assigning EditorApplication.isPlaying directly DOES work - the property returns
// false on the same frame and isPlayingOrWillChangePlaymode goes true, then the editor is in play
// mode a second or two later. Poll isPlaying; do not trust the immediate reply.
//
// It never opens a scene. EditorSceneManager.OpenScene with anything dirty raises a modal save
// prompt, and a modal dialog blocks the relay outright - no further call gets through until someone
// clicks it by hand in the editor. Open the scenes you want first, then run this. With exactly one
// VIEW scene open - whatever else is open beside it - PlayFromViewScene quick-starts from main_scene
// and skips the intro, which is what smoke.cs wants: the director refuses every switch while the
// intro is running, and an intro that stalls refuses them forever.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying)
        {
            result.Log("PLAY: already in play mode, nothing to do.");
            return;
        }

        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
        {
            result.Log("PLAY: refused, the editor is still compiling or importing. " +
                       "Wait for it (tools/mcp/compile.ps1) and ask again - entering play mid-compile " +
                       "starts the app on assemblies that are about to be swapped.");
            return;
        }

        var scenes = new System.Text.StringBuilder();
        for (var i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
            if (i > 0)
            {
                scenes.Append(", ");
            }

            scenes.Append(scene.name);
            if (scene.isDirty)
            {
                scenes.Append(" (dirty)");
            }
        }

        var quickStart = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene != null;

        // Direct, not delayCall. See the note at the top of this file: delayCall never fires through
        // the relay, so the queued call is dropped and nothing happens.
        UnityEditor.EditorApplication.isPlaying = true;

        result.Log("PLAY: requested. Open scenes: " + scenes + ".\n" +
                   (quickStart
                       ? "playModeStartScene is set, so PlayFromViewScene will quick-start from main_scene " +
                         "and skip the intro."
                       : "playModeStartScene is not set, so this starts from the open scenes and the intro " +
                         "will run - the director refuses switches until it finishes.") + "\n" +
                   "isPlaying is still false on this frame and that is expected - poll it, do not trust " +
                   "this reply. The domain is reloading; expect \"Unity not detected\" for a few seconds.");
    }
}
