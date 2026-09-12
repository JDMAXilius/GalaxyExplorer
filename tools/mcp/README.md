# `tools/mcp/` — driving the Unity editor from a terminal

The harness that used to live in a session scratchpad. It was rebuilt from scratch in at least
three sessions and lost each time, so it is checked in now. Nothing here is compiled into the app:
`tools/` sits outside `Assets/`, so Unity never sees it, these files need no `.meta`, and none of it
ships in the APK.

| File | What it is |
|---|---|
| `umcp.js` | MCP stdio client for the relay. `list`, `call <tool> <json>`, `run <file.cs>`. Node built-ins only. |
| `compile.ps1` | Refresh the asset database, wait out the compile, report compiler errors from `Logs/Editor.log`. |
| `smoke.cs` | The CS-033 walk: every module switched, panel and environment checked, place-and-restore, a screenshot each, console verdict. |
| `enter_play_mode.cs` | Presses Play. Does not open scenes, does not quit the editor. |
| `leave_play_mode.cs` | Stops play. Does not quit the editor. |

Requirements: Node 14+ on `PATH` (no packages), PowerShell 5.1 or 7 for `compile.ps1`, and the
Unity editor open on this project with the relay running.

---

## 1. Start the relay

```powershell
& "$env:USERPROFILE\.unity\relay\relay_win.exe" --mcp --project-path C:\path\to\GalaxyExplorer
```

`umcp.js` spawns that command itself for each invocation, so normally you do **not** start it by
hand — just make sure the editor is open on the project. Override the executable with `--relay=` or
`UNITY_RELAY`, and the project with `--project=` or `UNITY_PROJECT` (it defaults to this repo).

**One client at a time.** The relay is a single shared connection; two clients deadlock. That is the
same reason only one `unity-editor` agent may run at once.

---

## 2. The three commands

```bash
node tools/mcp/umcp.js list
node tools/mcp/umcp.js call Unity_GetConsoleLogs '{}'
node tools/mcp/umcp.js call Unity_Camera_Capture '{}'
node tools/mcp/umcp.js run  tools/mcp/smoke.cs
```

`run` sends the file to `Unity_RunCommand`. Which argument carries the source is read from the
tool's own `inputSchema` rather than assumed, so a rename on the relay side does not break it; force
it with `--tool=<name> --arg key=value` if it ever guesses wrong.

Useful flags: `--timeout=<seconds>` (default 300 — raise it for a long command), `--verbose`
(protocol chatter and the relay's stderr), `--raw` (the whole JSON-RPC result), `--retries` /
`--retry-delay` (the "Unity not detected" retry, which is a domain reload, not a fault).

Exit codes: `0` fine, `1` the tool reported an error, `2` usage or transport failure.

### RunCommand scripts

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result) { /* editor code; result.Log("...") */ }
}
```

Everything in this folder is written **fully qualified with no `using` directives**, because the
runner wraps the file in a preamble of its own and a `using` block that lands after it will not
compile. Copy that style for new scripts.

Runner limits, every one of which has already cost this project a session:

- No `System.Reflection`, and no asset delete or move — put those behind a project menu item and
  call `EditorApplication.ExecuteMenuItem`.
- It answers **NOT-OK whenever anything at all was logged as a warning**, even when the command did
  exactly what it was asked. Read the text, not the status. `smoke.cs` prints its own verdict for
  this reason.
- `Image` resolves to `Unity.AI.Image`. Write `UnityEngine.UI.Image`.
- `Object.GetInstanceID()` does not compile in 6000.6 — use `GetEntityId()` or `GetHashCode()`.
- **Never** call `EditorSceneManager.OpenScene` when a scene may be dirty. The save prompt is modal
  and it blocks the relay outright: no further call gets through until someone clicks it in the
  editor. Open the scenes you want by hand, then drive.
- Never edit a script while in play mode. Confirm `isPlaying=false` first — `compile.ps1` does.

---

## 3. Compile a change

```powershell
pwsh tools/mcp/compile.ps1                 # refresh, wait, report
pwsh tools/mcp/compile.ps1 -ShowWarnings   # list warnings too
pwsh tools/mcp/compile.ps1 -NoRefresh      # just read the result of a compile already running
```

It asks the editor what it is doing, refuses if play mode is running (`-Force` overrides), notes the
current length of `Logs/Editor.log`, refreshes, polls until the editor is neither compiling nor
importing, and then reports only the `error CS####` lines written **since** the refresh.

Exit codes: `0` clean, `1` compiler errors, `2` timed out or the relay never answered, `3` refused
because the editor is in play mode.

The dots while it waits mean: `.` still busy, `_` "Unity not detected" (a domain reload — expected),
`:` a first quiet answer that arrived in the gap between the import finishing and the compile
starting, so it asked again.

---

## 4. Run the smoke walk

```bash
# 1. In the editor, open main_scene — or exactly one view scene, which quick-starts through
#    PlayFromViewScene and skips the intro. Save anything dirty.
node tools/mcp/umcp.js run tools/mcp/enter_play_mode.cs
# 2. wait a few seconds for the domain reload, then:
node tools/mcp/umcp.js run tools/mcp/smoke.cs      # starts the walk, returns immediately
node tools/mcp/umcp.js run tools/mcp/smoke.cs      # run again to poll; prints the report so far
# 3. when it reads VERDICT: PASS or FAIL:
node tools/mcp/umcp.js run tools/mcp/leave_play_mode.cs
```

Output: `Logs/smoke_report.txt` and one PNG per module in `Logs/smoke/`. Neither is committed.

Why it is shaped that way:

- **It will not enter play mode itself.** `EditorApplication.EnterPlaymode()` reloads the domain,
  which unloads the assembly the command is executing in — it would die mid-flight.
- **`Execute` returns immediately.** A switch takes seconds and a scene load takes many frames, so a
  synchronous command could only ever report the first frame. The walk runs from
  `EditorApplication.update`, which ticks in play mode; progress lives in `SessionState` and in the
  report file, because every `run` compiles a fresh assembly with fresh statics.
- **It polls for `ExperienceDirector` instead of concluding.** `core_systems_scene` loads well into
  the boot flow, so an early probe finds nothing and looks exactly like a wiring failure when
  nothing is wrong.
- **It waits for the intro to finish.** `ExperienceDirector.Switch` is refused outright while
  `IntroFlow` is alive, so without the wait every module would fail for a benign reason.

### Reading the verdict

Three outcomes per check, and the difference is the point:

- `PASS` — asserted and true.
- `FAIL` — asserted and false. Any error, exception or assert in the console during the run is also
  a failure, whatever it says.
- `SKIP` — could not be asserted, and the detail says why. A module with neither a scene nor a
  content prefab being refused is a **pass**, not a failure: four of the seven places are
  legitimately sceneless until their content lands. A module with no authored panel copy showing no
  panel is a skip, for the same reason — that is `ExperienceDirector.HasPanelCopy` working.

Warnings are listed either way and only count against the run when they are not on the short benign
list in `smoke.cs`. The console is watched from the moment the run starts; for anything logged
before that, use `Unity_GetConsoleLogs`.

### What the walk does not cover

`smoke.cs` covers CS-033's line as far as the app allows today. It does **not** exercise the dock by
poking it — it calls `ExperienceDirector` directly, so a dead `GEButton` or a mis-wired
`XRPokeFilter` would go unnoticed — and it does not touch hand input, moons, destination tags,
two-handed scaling or the passthrough toggle. It pulls **one** body per module, not every body.
Those are the remaining part of CS-033's acceptance line and want their own pass.

---

## 5. Synthetic input and screenshots

For scripts of your own:

```csharp
UnityEngine.InputSystem.InputSystem.QueueStateEvent(
    UnityEngine.InputSystem.Mouse.current,
    new UnityEngine.InputSystem.LowLevel.MouseState { position = new UnityEngine.Vector2(640f, 360f) }
        .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));

UnityEngine.InputSystem.InputSystem.QueueStateEvent(
    UnityEngine.InputSystem.Keyboard.current,
    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.P));

UnityEngine.ScreenCapture.CaptureScreenshot(path);   // lands at the end of a rendered frame
```

`CaptureScreenshot` writes asynchronously, so wait for the file to exist rather than reading it in
the same call — `smoke.cs` waits up to ten seconds.

---

## 6. After a play-mode session

Play mode writes runtime values into shared materials (`about_material`, `earth_clouds`, the Jupiter
clouds). Check `git status` and revert those before committing.
