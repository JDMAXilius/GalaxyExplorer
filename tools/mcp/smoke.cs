// smoke.cs - the per-phase smoke walk, run through the MCP relay's Unity_RunCommand.
//
//   node tools/mcp/umcp.js run tools/mcp/smoke.cs
//
// This is CS-033's acceptance line made executable: switch every ExperienceModule, verify the panel
// and the environment for each, exercise place-and-restore on a body, capture a screenshot per
// module, and report whether the console stayed clean. It writes Logs/smoke_report.txt and
// Logs/smoke/*.png; the second line of the report is the verdict.
//
// Four shapes here are forced by how the relay runs code, and undoing any of them breaks the run:
//
//   * No `using` directives, everything fully qualified. The runner compiles this file around its
//     own preamble; a using block that lands after the preamble's own would not compile at all.
//
//   * No System.Reflection anywhere, and no AssetDatabase delete or move. The runner refuses both.
//     (Deleting a stale PNG from Logs/ is plain file IO on a path outside Assets, not an asset
//     operation, so that one is fine.)
//
//   * Execute() returns immediately and the walk runs from EditorApplication.update. A switch takes
//     seconds and a scene load takes many frames, so a synchronous Execute could only ever report
//     the first frame. Invoke it again to poll: progress lives in SessionState and in the report
//     file, not in this assembly's statics, because every `run` compiles a fresh assembly.
//
//   * It refuses to enter play mode itself. EditorApplication.EnterPlaymode() triggers a domain
//     reload, which unloads the assembly this code is running in, mid-command. Enter play first
//     (tools/mcp/enter_play_mode.cs), then run this.
//
// The verdict is this file's own, not the relay's. The relay answers NOT-OK whenever anything was
// logged as a warning, which for this project is most runs; the report separates warnings it knows
// to be benign from the ones that are evidence of a fault.

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log(CosmicSmoke.Invoke());
    }
}

internal static class CosmicSmoke
{
    // ---------------------------------------------------------------- knobs

    // ExperienceDirector arrives with core_systems_scene, which the boot flow loads well after
    // start. An early probe finding nothing is not a wiring failure, so this polls rather than
    // concludes - the number is deliberately larger than any boot we have measured.
    private const double DirectorTimeout = 60.0;

    // Switch() is refused outright while IntroFlow is alive, so without this wait every module
    // would "fail" for an entirely benign reason.
    private const double IntroTimeout = 120.0;

    private const double SwitchTimeout = 40.0;   // the director's own bound is 20 s plus the grow-in
    private const double EnvironmentSettle = 3.0;
    private const double PullTimeout = 20.0;
    private const double RestoreTimeout = 10.0;
    private const double ScreenshotTimeout = 10.0;

    // The scene panel is spawned inside the switch but fades in over a third of a second afterwards.
    private const double PanelTimeout = 5.0;

    // A body at rest rides its orbit anchor, so "back home" is measured against RootTransform as it
    // is now, and the tolerance has to cover the distance that anchor travels while we look at it.
    private const float RestoreTolerance = 0.15f;

    private const float PlacedDrift = 0.02f;     // metres a placed body may move in three frames

    private const string StateKey = "cosmic.smoke.state";
    private const string RunIdKey = "cosmic.smoke.runid";
    private const string BeatKey = "cosmic.smoke.heartbeat";

    // How long without a heartbeat before a run counts as abandoned. A domain reload unloads the
    // driver silently, so this is the only way to tell a live run from a dead one.
    private const double StaleSeconds = 25.0;

    private const string Pass = "PASS";
    private const string Fail = "FAIL";
    private const string Skip = "SKIP";

    // Warnings this project logs in normal operation. They are listed in the report either way, but
    // they do not make the run a failure. Keep this list short: a warning nobody can explain is a
    // finding, not noise.
    private static readonly string[] BenignWarnings =
    {
        "has neither SceneName nor ContentPrefab",   // a sceneless module refusing a poke, by design
        "CanvasRenderer",                            // TMP's material/canvas chatter
        "TextMesh Pro",
        "Assembly-CSharp-Editor",
    };

    // ---------------------------------------------------------------- state (this assembly only)

    private sealed class Check
    {
        public string Module;
        public string Clause;
        public string Verdict;
        public string Detail;
    }

    private sealed class Entry
    {
        public UnityEngine.LogType Type;
        public string Module;
        public string Message;
    }

    private static readonly System.Collections.Generic.List<Check> Checks =
        new System.Collections.Generic.List<Check>();

    private static readonly System.Collections.Generic.List<Entry> Logs =
        new System.Collections.Generic.List<Entry>();

    private static readonly System.Collections.Generic.List<string> Notes =
        new System.Collections.Generic.List<string>();

    private static readonly System.Collections.Generic.List<string> Shots =
        new System.Collections.Generic.List<string>();

    private static readonly System.Collections.Generic.Stack<System.Collections.IEnumerator> Stack =
        new System.Collections.Generic.Stack<System.Collections.IEnumerator>();

    private static string _runId = "";
    private static string _module = "-";
    private static bool _ok;                     // result of the most recent WaitUntil
    private static bool _attached;
    private static double _startedAt;
    private static string _finishedReason;

    // ---------------------------------------------------------------- entry point

    public static string Invoke()
    {
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            return NotPlayingHelp();
        }

        var state = UnityEditor.SessionState.GetString(StateKey, "");
        if (state == "running")
        {
            var age = UnityEditor.EditorApplication.timeSinceStartup - ReadBeat();
            if (age >= 0.0 && age < StaleSeconds)
            {
                return "SMOKE: a run is already in progress (" +
                       UnityEditor.SessionState.GetString(RunIdKey, "?") + "), " +
                       Round(age) + " s since its last heartbeat. Run this again to poll.\n\n" +
                       ReadReport();
            }

            Notes.Add("A previous run (" + UnityEditor.SessionState.GetString(RunIdKey, "?") +
                      ") stopped without finishing - most likely a domain reload unloaded it. " +
                      "Starting fresh.");
        }

        Begin();
        return "SMOKE: started run " + _runId + ". It walks every module and takes a while.\n" +
               "Poll by running this file again, or read " + Relative(ReportPath()) + ".";
    }

    private static string NotPlayingHelp()
    {
        return "SMOKE: not started - the editor is not in play mode, and this harness will not put " +
               "it there itself.\n" +
               "EditorApplication.EnterPlaymode() forces a domain reload, which unloads the assembly " +
               "a RunCommand is executing in, so the command would die mid-flight.\n\n" +
               "Do this instead:\n" +
               "  1. Open main_scene, or exactly one view scene for the PlayFromViewScene quick-start " +
               "(that also skips the intro, which otherwise refuses every switch for the first minute).\n" +
               "  2. node tools/mcp/umcp.js run tools/mcp/enter_play_mode.cs\n" +
               "  3. node tools/mcp/umcp.js run tools/mcp/smoke.cs\n" +
               "Do not open a scene from a RunCommand while anything is dirty: the save prompt is modal " +
               "and it blocks the relay outright.";
    }

    private static void Begin()
    {
        _runId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        _module = "-";
        _startedAt = UnityEditor.EditorApplication.timeSinceStartup;
        _finishedReason = null;

        Checks.Clear();
        Logs.Clear();
        Shots.Clear();
        Stack.Clear();

        UnityEditor.SessionState.SetString(RunIdKey, _runId);
        UnityEditor.SessionState.SetString(StateKey, "running");
        Beat();

        try
        {
            System.IO.Directory.CreateDirectory(ShotDir());
        }
        catch (System.Exception e)
        {
            Notes.Add("Could not make " + ShotDir() + ": " + e.Message + " - screenshots will fail.");
        }

        UnityEngine.Application.logMessageReceived += OnLog;
        UnityEditor.EditorApplication.update += Tick;
        _attached = true;

        Stack.Push(Run());
        WriteReport(false);
    }

    private static void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        UnityEditor.EditorApplication.update -= Tick;
        UnityEngine.Application.logMessageReceived -= OnLog;
    }

    private static void Finish(string reason)
    {
        _finishedReason = reason;
        Detach();
        Stack.Clear();
        UnityEditor.SessionState.SetString(StateKey, "done");
        WriteReport(true);
    }

    // ---------------------------------------------------------------- the driver
    //
    // One step per editor update, with nested iterators pushed onto a stack rather than flattened by
    // hand. EditorApplication.update ticks in play mode too, which is why the walk does not need a
    // MonoBehaviour of its own - and a MonoBehaviour defined in a runner-compiled assembly is
    // exactly the kind of thing that turns into a missing script the moment anything reloads.

    private static void Tick()
    {
        // A newer run supersedes this one: every `run smoke.cs` compiles a fresh assembly, and the
        // old one's update hook would otherwise keep walking alongside it.
        if (UnityEditor.SessionState.GetString(RunIdKey, "") != _runId)
        {
            Detach();
            return;
        }

        // Somebody pressed Stop. Everything the walk holds is about to be destroyed, so it says so
        // rather than filling the report with null reference failures that mean nothing.
        if (!UnityEditor.EditorApplication.isPlaying)
        {
            Record(_module, "harness", Fail, "play mode ended while the walk was running");
            Finish("play mode ended");
            return;
        }

        Beat();

        while (Stack.Count > 0)
        {
            var top = Stack.Peek();
            bool moved;
            try
            {
                moved = top.MoveNext();
            }
            catch (System.Exception e)
            {
                Record("-", "harness", Fail, e.GetType().Name + ": " + e.Message);
                Finish("the harness threw");
                return;
            }

            if (!moved)
            {
                Stack.Pop();
                continue;
            }

            var child = top.Current as System.Collections.IEnumerator;
            if (child != null)
            {
                Stack.Push(child);
                continue;
            }

            return;   // yielded null: come back next tick
        }

        Finish("the walk returned");
    }

    private static System.Collections.IEnumerator WaitUntil(System.Func<bool> condition, double seconds)
    {
        var deadline = UnityEditor.EditorApplication.timeSinceStartup + seconds;
        while (true)
        {
            bool met;
            try
            {
                met = condition();
            }
            catch (System.Exception)
            {
                met = false;   // a null mid-teardown is a "not yet", not a crash
            }

            if (met)
            {
                _ok = true;
                yield break;
            }

            if (UnityEditor.EditorApplication.timeSinceStartup > deadline)
            {
                _ok = false;
                yield break;
            }

            yield return null;
        }
    }

    private static System.Collections.IEnumerator WaitFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            yield return null;
        }
    }

    // ---------------------------------------------------------------- the walk

    private static System.Collections.IEnumerator Run()
    {
        Notes.Add("Unity " + UnityEngine.Application.unityVersion +
                  ", XR device active: " + UnityEngine.XR.XRSettings.isDeviceActive +
                  " (false means desktop mouse mode).");

        yield return WaitUntil(() => CosmicSimulation.ExperienceDirector.Instance != null, DirectorTimeout);
        if (!_ok)
        {
            Record("-", "director", Fail,
                "no ExperienceDirector after " + DirectorTimeout + " s. It arrives with core_systems_scene, " +
                "which the boot flow loads late, so this is a genuine absence rather than an early probe.");
            yield break;
        }

        var dir = CosmicSimulation.ExperienceDirector.Instance;
        Record("-", "director", Pass,
            "found after " + Round(UnityEditor.EditorApplication.timeSinceStartup - _startedAt) + " s");

        yield return WaitUntil(() => !dir.IntroRunning, IntroTimeout);
        if (!_ok)
        {
            Record("-", "intro", Fail,
                "IntroFlow was still running after " + IntroTimeout + " s, and Switch() is refused while it is. " +
                "Open exactly one view scene and press Play so PlayFromViewScene quick-starts past the intro.");
            yield break;
        }

        Record("-", "intro", Pass, "not running, so switches are accepted");

        var env = CosmicSimulation.EnvironmentController.Instance;
        if (env == null)
        {
            Record("-", "environment", Skip,
                "no EnvironmentController is alive; every environment clause below is skipped rather than failed");
        }

        yield return CheckDock();

        var modules = new System.Collections.Generic.List<CosmicSimulation.ExperienceModule>();
        foreach (var m in dir.Modules)
        {
            if (m != null)
            {
                modules.Add(m);
            }
        }

        if (modules.Count == 0)
        {
            Record("-", "modules", Fail, "the director has no modules, so there is nothing to walk");
            yield break;
        }

        Notes.Add(modules.Count + " modules to walk.");
        var startedOn = dir.Current;

        for (var i = 0; i < modules.Count; i++)
        {
            yield return VisitModule(dir, env, modules[i], i + 1);
        }

        _module = "-";

        // Leave the editor roughly where it was found, so a second run is not starting from wherever
        // the last module happened to put it.
        if (startedOn != null && dir.Current != startedOn)
        {
            dir.Switch(startedOn);
            yield return WaitUntil(() => !dir.IsSwitching, SwitchTimeout);
        }

        dir.RestoreEverything();
        Notes.Add("Restored: switched back to " +
                  (startedOn != null ? Name(startedOn) : "(nothing was open at the start)") +
                  " and called RestoreEverything().");
    }

    private static System.Collections.IEnumerator CheckDock()
    {
        var dock = CosmicSimulation.DockController.Instance;
        if (dock == null)
        {
            Record("-", "dock", Skip,
                "no DockController is alive. In a headset it comes with the rig; on desktop the mirror is " +
                "DesktopDock, which is a different component.");
            yield break;
        }

        var expected = 0;
        foreach (var m in CosmicSimulation.DockController.TileModules())
        {
            if (m != null)
            {
                expected++;
            }
        }

        var actual = dock.Tiles != null ? dock.Tiles.Count : 0;
        Record("-", "dock", actual == expected && expected > 0 ? Pass : Fail,
            actual + " tiles built from " + expected + " tile modules");
    }

    private static System.Collections.IEnumerator VisitModule(
        CosmicSimulation.ExperienceDirector dir,
        CosmicSimulation.EnvironmentController env,
        CosmicSimulation.ExperienceModule module,
        int index)
    {
        var id = Name(module);
        _module = id;

        var nothingToOpen = string.IsNullOrEmpty(module.SceneName) && module.ContentPrefab == null;

        if (dir.Current == module)
        {
            Record(id, "switch", Pass,
                "already open when the walk reached it; Switch() is a no-op on the current module");
        }
        else
        {
            yield return WaitUntil(() => !dir.IsSwitching, SwitchTimeout);

            var t0 = UnityEditor.EditorApplication.timeSinceStartup;
            dir.Switch(module);

            // Switch() runs its routine to the first yield inline, so a refusal has already happened
            // by the time it returns and IsSwitching is false again. Two frames of slack keep the
            // "did it start?" question from being answered before it could have.
            yield return WaitFrames(2);
            yield return WaitUntil(() => !dir.IsSwitching, SwitchTimeout);
            var took = UnityEditor.EditorApplication.timeSinceStartup - t0;

            if (nothingToOpen)
            {
                var opened = dir.Current == module;
                Record(id, "switch", opened ? Fail : Pass, opened
                    ? "opened even though it names neither a scene nor a content prefab"
                    : "refused before anything moved, as designed: no SceneName, no ContentPrefab, " +
                      "SwitchNotice.NotReady posted. Four of the seven places are legitimately sceneless " +
                      "until their content lands, so this is not a failure.");
                yield break;
            }

            if (!_ok)
            {
                Record(id, "switch", Fail,
                    "still switching after " + SwitchTimeout + " s - the director's own bound is 20 s, so " +
                    "either the scene never arrived or IsSwitching is wedged");
                yield break;
            }

            Record(id, "switch", dir.Current == module ? Pass : Fail,
                dir.Current == module
                    ? "opened in " + Round(took) + " s"
                    : "finished switching in " + Round(took) + " s but Current is " +
                      (dir.Current != null ? Name(dir.Current) + " - look for an AbandonSwitch error below"
                                           : "null - AbandonSwitch cleared it, so the load failed"));
        }

        if (dir.Current != module)
        {
            yield break;
        }

        if (env != null)
        {
            yield return WaitUntil(() => env.Mode == module.Environment, EnvironmentSettle);
            Record(id, "environment", env.Mode == module.Environment ? Pass : Fail,
                "wanted " + module.Environment + ", room is " + env.Mode +
                (env.PassthroughForced
                    ? " with PassthroughForced on, so the real room shows regardless of the mode"
                    : ""));
        }

        MarkedTile(module, id);

        yield return WaitFrames(2);
        yield return CheckPanel(id, module);

        yield return PlaceAndRestore(id);
        yield return Screenshot(id, index);
    }

    private static void MarkedTile(CosmicSimulation.ExperienceModule module, string id)
    {
        var dock = CosmicSimulation.DockController.Instance;
        if (dock == null || dock.Tiles == null || dock.Tiles.Count == 0)
        {
            return;
        }

        if (module.Kind != CosmicSimulation.ExperienceKind.DockTile)
        {
            return;   // destinations have no tile to underline
        }

        var marked = 0;
        var right = false;
        foreach (var tile in dock.Tiles)
        {
            if (tile == null || !tile.IsActive)
            {
                continue;
            }

            marked++;
            if (tile.Module == module)
            {
                right = true;
            }
        }

        Record(id, "dock tile", marked == 1 && right ? Pass : Fail,
            marked + " tile(s) underlined" + (right ? ", the right one" : ", not this module's"));
    }

    // ---------------------------------------------------------------- panel

    private static System.Collections.IEnumerator CheckPanel(
        string id, CosmicSimulation.ExperienceModule module)
    {
        // The same test the director makes before it spawns anything (HasPanelCopy): a place whose
        // prose has not been written gets no panel, silently and by design. Skipping rather than
        // failing here is the difference between "the panel is broken" and "the copy is not written".
        if (!HasCopy(module))
        {
            Record(id, "panel", Skip,
                "the module has no authored panel copy, so the director shows no panel for it - by design, " +
                "not a fault");
            yield break;
        }

        // The panel fades in over a third of a second, so the first frame after the switch is too
        // early to read its alpha.
        CosmicSimulation.InfoPanel panel = null;
        yield return WaitUntil(() =>
        {
            panel = FindScenePanel(module);
            return panel != null && Alpha(panel) > 0.01f;
        }, PanelTimeout);

        if (panel == null)
        {
            Record(id, "panel", Fail,
                "no scene panel appeared. If the console below carries \"no scene panel prefab is assigned\", " +
                "the fix is the menu item Cosmic Simulation/Wire Scene Panel rather than anything in the code.");
            yield break;
        }

        // What the panel shows when the copy has no title of its own (InfoPanel.Bind falls back).
        var wanted = module.Panel != null && !string.IsNullOrEmpty(module.Panel.Title)
            ? module.Panel.Title
            : module.DisplayName;

        var alpha = Alpha(panel);
        var titled = string.IsNullOrEmpty(wanted) || TextUnder(panel.transform, wanted);

        Record(id, "panel", alpha > 0.01f && titled ? Pass : Fail,
            "'" + panel.name + "' variant " + panel.PanelVariant + ", alpha " + Round(alpha) +
            (string.IsNullOrEmpty(wanted)
                ? ", no title to match"
                : (titled ? ", showing \"" + wanted + "\"" : ", does NOT show \"" + wanted + "\"")));
    }

    private static bool HasCopy(CosmicSimulation.ExperienceModule module)
    {
        var copy = module.Panel;
        if (copy == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(copy.Title) || !string.IsNullOrEmpty(copy.Instruction))
        {
            return true;
        }

        if (copy.Paragraphs != null)
        {
            foreach (var p in copy.Paragraphs)
            {
                if (!string.IsNullOrEmpty(p))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The director names it "<module id>_scene_panel"; the variant is the fallback, because a
    // destination overlay's panel is a Scene variant too and only the name tells them apart.
    private static CosmicSimulation.InfoPanel FindScenePanel(CosmicSimulation.ExperienceModule module)
    {
        var wanted = module.Id + "_scene_panel";
        CosmicSimulation.InfoPanel fallback = null;

        var panels = UnityEngine.Object.FindObjectsByType<CosmicSimulation.InfoPanel>(
            UnityEngine.FindObjectsSortMode.None);

        foreach (var p in panels)
        {
            if (p == null || p.PanelVariant != CosmicSimulation.InfoPanel.Variant.Scene)
            {
                continue;
            }

            if (p.name == wanted)
            {
                return p;
            }

            if (fallback == null)
            {
                fallback = p;
            }
        }

        return fallback;
    }

    private static float Alpha(CosmicSimulation.InfoPanel panel)
    {
        var group = panel.GetComponent<UnityEngine.CanvasGroup>();
        return group != null ? group.alpha : 1f;
    }

    // TMP_Text rather than reflection over the panel's private fields: the runner forbids
    // System.Reflection, and the visible text is the honest thing to assert on anyway.
    private static bool TextUnder(UnityEngine.Transform root, string needle)
    {
        var texts = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t != null && !string.IsNullOrEmpty(t.text) &&
                t.text.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- place and restore

    private static System.Collections.IEnumerator PlaceAndRestore(string id)
    {
        CosmicSimulation.FreePlacementSolver target = null;
        ForceSolver force = null;

        var placers = UnityEngine.Object.FindObjectsByType<CosmicSimulation.FreePlacementSolver>(
            UnityEngine.FindObjectsSortMode.None);

        foreach (var p in placers)
        {
            if (p == null || !p.isActiveAndEnabled)
            {
                continue;
            }

            var f = p.GetComponent<ForceSolver>();
            if (f != null && f.RootTransform != null && f.ForceState == ForceSolver.State.Root)
            {
                target = p;
                force = f;
                break;
            }
        }

        if (target == null)
        {
            Record(id, "place/restore", Skip,
                placers.Length + " FreePlacementSolver(s) found, none of them active, at Root and carrying a " +
                "RootTransform to come home to");
            yield break;
        }

        var body = target.name;

        // The desktop and UI entry point: pulls the object in front of the camera. Using it rather
        // than a synthetic pointer keeps this test about placement rather than about input routing,
        // which DesktopMouseInput covers separately.
        force.OnPointerDown();

        yield return WaitUntil(() => target.IsPlaced, PullTimeout);
        if (!_ok)
        {
            Record(id, "place/restore", Fail,
                "'" + body + "' never reached Free after OnPointerDown; it is in " + force.ForceState);
            yield break;
        }

        // In Free the solver deliberately does nothing, so moving the transform here is the same
        // thing a hand does - nothing is being fought for the pose.
        var moved = target.transform.position + new UnityEngine.Vector3(0.12f, 0.05f, 0f);
        target.transform.position = moved;

        yield return WaitFrames(3);

        var drift = (target.transform.position - moved).magnitude;
        Record(id, "place", drift < PlacedDrift ? Pass : Fail, drift < PlacedDrift
            ? "'" + body + "' stayed where it was put; nothing snapped it back"
            : "'" + body + "' drifted " + Round(drift) + " m in three frames - something is still driving " +
              "it. PostManipulationResetter on an object the player arranges is the usual cause.");

        target.RestoreLayout();
        yield return WaitUntil(() => !target.IsRestoring, RestoreTimeout);
        var finished = _ok;
        yield return WaitFrames(2);

        var home = force.RootTransform != null
            ? force.RootTransform.position
            : target.transform.position;
        var error = (target.transform.position - home).magnitude;
        var atRoot = force.ForceState == ForceSolver.State.Root;

        Record(id, "restore", finished && atRoot && error <= RestoreTolerance ? Pass : Fail,
            (finished ? "" : "still restoring after " + RestoreTimeout + " s; ") +
            "'" + body + "' is " + Round(error) + " m from RootTransform (tolerance " + RestoreTolerance +
            " m, generous because a body at rest rides a moving orbit anchor) and in state " + force.ForceState);
    }

    // ---------------------------------------------------------------- screenshot

    private static System.Collections.IEnumerator Screenshot(string id, int index)
    {
        var file = System.IO.Path.Combine(ShotDir(), index.ToString("00") + "_" + Safe(id) + ".png");

        try
        {
            // Plain file IO on a path outside Assets. The runner's ban is on AssetDatabase deletes
            // and moves; this is neither, and a stale PNG left from the last run would otherwise be
            // reported as this run's evidence.
            if (System.IO.File.Exists(file))
            {
                System.IO.File.Delete(file);
            }
        }
        catch (System.Exception e)
        {
            Notes.Add("Could not clear " + file + ": " + e.Message);
        }

        UnityEngine.ScreenCapture.CaptureScreenshot(file);

        yield return WaitUntil(() => System.IO.File.Exists(file), ScreenshotTimeout);

        if (_ok)
        {
            Shots.Add(Relative(file));
            Record(id, "screenshot", Pass, Relative(file));
        }
        else
        {
            Record(id, "screenshot", Fail,
                "CaptureScreenshot wrote nothing to " + file + " within " + ScreenshotTimeout +
                " s. It writes at the end of a rendered frame, so a Game view that is not rendering " +
                "produces no file.");
        }
    }

    // ---------------------------------------------------------------- console

    private static void OnLog(string condition, string stackTrace, UnityEngine.LogType type)
    {
        if (type == UnityEngine.LogType.Log)
        {
            return;
        }

        Logs.Add(new Entry { Type = type, Module = _module, Message = condition });
    }

    private static bool Benign(Entry e)
    {
        if (e.Type == UnityEngine.LogType.Error ||
            e.Type == UnityEngine.LogType.Exception ||
            e.Type == UnityEngine.LogType.Assert)
        {
            return false;   // an error is never benign here, whatever it says
        }

        foreach (var pattern in BenignWarnings)
        {
            if (e.Message.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- report

    private static void Record(string module, string clause, string verdict, string detail)
    {
        Checks.Add(new Check { Module = module, Clause = clause, Verdict = verdict, Detail = detail });
        WriteReport(false);
    }

    private static void WriteReport(bool finished)
    {
        var sb = new System.Text.StringBuilder();

        var failures = 0;
        var passes = 0;
        var skips = 0;
        foreach (var c in Checks)
        {
            if (c.Verdict == Fail)
            {
                failures++;
            }
            else if (c.Verdict == Pass)
            {
                passes++;
            }
            else
            {
                skips++;
            }
        }

        var errors = 0;
        var benign = 0;
        var suspicious = 0;
        foreach (var e in Logs)
        {
            if (e.Type == UnityEngine.LogType.Error ||
                e.Type == UnityEngine.LogType.Exception ||
                e.Type == UnityEngine.LogType.Assert)
            {
                errors++;
            }
            else if (Benign(e))
            {
                benign++;
            }
            else
            {
                suspicious++;
            }
        }

        var verdict = !finished
            ? "RUNNING"
            : (failures == 0 && errors == 0 ? "PASS" : "FAIL");

        sb.AppendLine("COSMIC SIMULATION XR - SMOKE (CS-033 acceptance)");
        sb.AppendLine("VERDICT: " + verdict +
                      (finished && verdict == "PASS" && suspicious > 0
                          ? "  (with " + suspicious + " warning(s) nobody has classified - read them)"
                          : ""));
        sb.AppendLine("run " + _runId + ", " + Round(UnityEditor.EditorApplication.timeSinceStartup - _startedAt) +
                      " s elapsed" + (finished ? ", finished: " + _finishedReason : ", in progress") +
                      ", written " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("checks: " + passes + " pass, " + failures + " fail, " + skips + " skip");
        sb.AppendLine("console during the run: " + errors + " error(s), " + benign +
                      " benign warning(s), " + suspicious + " unclassified warning(s)");
        sb.AppendLine();
        sb.AppendLine("The relay answers NOT-OK whenever anything at all was logged as a warning. The");
        sb.AppendLine("verdict above is this harness's own: only errors and failed checks make it FAIL.");
        sb.AppendLine("The console is only watched from the moment the run starts - use");
        sb.AppendLine("Unity_GetConsoleLogs for anything logged before that.");
        sb.AppendLine();

        sb.AppendLine("--- checks");
        foreach (var c in Checks)
        {
            sb.AppendLine("[" + c.Verdict + "] " + Pad(c.Module, 22) + Pad(c.Clause, 14) + c.Detail);
        }

        if (Logs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- console");
            foreach (var e in Logs)
            {
                sb.AppendLine("[" + e.Type + (Benign(e) ? "/benign" : "") + "] during " + e.Module + ": " +
                              OneLine(e.Message));
            }
        }

        if (Shots.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- screenshots");
            foreach (var s in Shots)
            {
                sb.AppendLine(s);
            }
        }

        if (Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- notes");
            foreach (var n in Notes)
            {
                sb.AppendLine(n);
            }
        }

        try
        {
            System.IO.File.WriteAllText(ReportPath(), sb.ToString());
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning("smoke: could not write " + ReportPath() + ": " + e.Message);
        }
    }

    private static string ReadReport()
    {
        try
        {
            return System.IO.File.Exists(ReportPath())
                ? System.IO.File.ReadAllText(ReportPath())
                : "(no report file yet)";
        }
        catch (System.Exception e)
        {
            return "(could not read " + ReportPath() + ": " + e.Message + ")";
        }
    }

    // ---------------------------------------------------------------- small helpers

    private static string ProjectRoot()
    {
        return System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
    }

    private static string ReportPath()
    {
        return System.IO.Path.Combine(System.IO.Path.Combine(ProjectRoot(), "Logs"), "smoke_report.txt");
    }

    private static string ShotDir()
    {
        return System.IO.Path.Combine(System.IO.Path.Combine(ProjectRoot(), "Logs"), "smoke");
    }

    private static string Relative(string absolute)
    {
        var root = ProjectRoot();
        return absolute.StartsWith(root) ? absolute.Substring(root.Length).TrimStart('\\', '/') : absolute;
    }

    private static void Beat()
    {
        UnityEditor.SessionState.SetString(BeatKey,
            UnityEditor.EditorApplication.timeSinceStartup.ToString("R",
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private static double ReadBeat()
    {
        double value;
        return double.TryParse(UnityEditor.SessionState.GetString(BeatKey, "0"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value)
            ? value
            : 0.0;
    }

    private static string Name(CosmicSimulation.ExperienceModule module)
    {
        if (module == null)
        {
            return "(null)";
        }

        return string.IsNullOrEmpty(module.Id) ? module.name : module.Id;
    }

    private static string Safe(string id)
    {
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (var c in id)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return sb.ToString();
    }

    private static string OneLine(string text)
    {
        var flat = text.Replace("\r", " ").Replace("\n", " ");
        return flat.Length > 300 ? flat.Substring(0, 300) + " ..." : flat;
    }

    private static string Pad(string text, int width)
    {
        if (text == null)
        {
            text = "";
        }

        return text.Length >= width ? text + " " : text.PadRight(width);
    }

    private static string Round(double value)
    {
        return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
