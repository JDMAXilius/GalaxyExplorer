// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using CosmicSimulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using MouseDevice = UnityEngine.InputSystem.Mouse;
using KeyboardDevice = UnityEngine.InputSystem.Keyboard;

namespace GalaxyExplorer.Editor
{
    /// <summary>
    /// The app's acceptance walk, made executable: boot, every dock tile clicked with a real mouse, the room
    /// mode each one asks for, a destination tag on the Milky Way clicked, Escape, restore, and whether the
    /// console stayed clean. It exists because the shipping app had no such check while the rework did, and
    /// every defect this session found - the intro swallowing clicks among them - was found by one.
    ///
    /// Three things here are the lessons the rework's harness paid for, and undoing any of them breaks it:
    ///
    ///   * <see cref="Flush"/> after every queued event. QueueStateEvent only queues; with the Game view
    ///     unfocused, which is every relay-driven run, nothing flushes it and the device keeps reading (0, 0).
    ///   * Tiles are clicked where they are on screen, not by calling Choose(). Calling the handler tests the
    ///     handler; the player's complaint is about the click, and only a click tests the click.
    ///   * The verdict is written to Logs/app_check.log as well as the console. The console holds 200 entries
    ///     and one component logging per frame can bury a whole run before anyone reads it.
    ///
    /// Enter play mode first - this never does it itself, because that reloads the domain and unloads this
    /// assembly mid-run.
    /// </summary>
    public static class AppCheck
    {
        private const string LogPath = "Logs/app_check.log";
        private const float SettleSeconds = 1.6f;

        private struct Step
        {
            public float Wait;
            public Action Run;
        }

        private static readonly List<Step> Steps = new List<Step>();
        private static Action _holding;
        private static double _due;
        private static int _cursor, _passes, _total, _errors;
        private static ExperienceDirector _director;
        private static IReadOnlyList<DockTile> _tiles;

        [MenuItem("Cosmic Simulation/Verify/App Check")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[APP] FAIL not in play mode. Press Play on main_scene, then run this.");
                return;
            }

            if (Steps.Count > 0)
            {
                Debug.Log($"[APP] already running, step {_cursor}/{Steps.Count}");
                return;
            }

            _director = UnityEngine.Object.FindAnyObjectByType<ExperienceDirector>();
            var desktop = UnityEngine.Object.FindAnyObjectByType<DesktopDock>();
            var world = UnityEngine.Object.FindAnyObjectByType<DockController>();
            _tiles = desktop != null && desktop.Tiles != null && desktop.Tiles.Count > 0
                ? desktop.Tiles
                : world != null ? world.Tiles : null;

            Steps.Clear();
            _cursor = _passes = _total = _errors = 0;
            try { System.IO.File.Delete(LogPath); } catch (Exception) { }

            if (_director == null || _tiles == null || _tiles.Count == 0 || Camera.main == null
                || MouseDevice.current == null || KeyboardDevice.current == null)
            {
                Debug.LogError("[APP] FAIL no ExperienceDirector, no dock tiles, no Camera.main, or no input device. " +
                               "Press Play on main_scene and wait for the app to boot before running this.");
                return;
            }

            Application.logMessageReceived += OnLog;

            // 1. The intro must not eat a click. Asking for a place while it runs should end it and open that
            //    place - the defect this walk was written for.
            var first = FirstModule();
            Add(0.4f, () =>
            {
                Check(_director != null, "the director is in the scene");
                Check(_tiles.Count >= 7, $"the dock carries its tiles ({_tiles.Count})");
                if (!_director.IntroRunning)
                {
                    Skip("the intro click: the intro was already over when the walk started");
                    return;
                }
                if (first != null) _director.Switch(first);
            });
            Add(SettleSeconds * 3f, () =>
            {
                if (first == null) return;
                Check(!_director.IntroRunning, "a place asked for during the intro ends the intro");
                Check(_director.Current == first, $"and that place opens ({Name(_director.Current)})");
            });

            // 2. Every tile, clicked where it sits on screen.
            foreach (var tile in _tiles)
            {
                var wanted = tile;
                if (wanted == null || wanted.Module == null) continue;
                Add(0.3f, () =>
                {
                    if (!OnScreen(wanted.transform.position))
                    {
                        Skip($"the {wanted.Module.Id} tile: it is not on screen to click");
                        return;
                    }
                    _holding = () => Move(At(wanted.transform.position));
                });
                Add(0.4f, () => { if (_holding != null) _holding = () => Press(At(wanted.transform.position), true); });
                Add(0.2f, () => { if (_holding != null) { _holding = null; Press(At(wanted.transform.position), false); } });
                Add(SettleSeconds, () =>
                {
                    if (!OnScreen(wanted.transform.position)) return;
                    Check(_director.Current == wanted.Module,
                        $"clicking the {wanted.Module.Id} tile opens it ({Name(_director.Current)})");
                    var room = EnvironmentController.Instance;
                    Check(room == null || room.EffectiveMode == wanted.Module.Environment,
                        $"and the room goes to {wanted.Module.Environment} ({(room == null ? "no controller" : room.EffectiveMode.ToString())})");
                });
            }

            // 3. A destination tag on the Milky Way, clicked with the mouse. This is the one the owner asked
            //    about by name, and the one nothing had ever proven.
            Transform tag = null;
            Add(0.3f, () => _director.Switch("milky_way"));
            Add(SettleSeconds * 2f, () =>
            {
                Check(Name(_director.Current) == "milky_way", $"the Milky Way opens ({Name(_director.Current)})");
                tag = FindTag();
                if (tag == null) { Skip("the destination tag click: no tag with a collider is on screen"); return; }
                _holding = () => Move(At(tag.position));
            });
            Add(0.5f, () => { if (tag != null) _holding = () => Press(At(tag.position), true); });
            Add(0.2f, () => { if (tag != null) { _holding = null; Press(At(tag.position), false); } });
            Add(SettleSeconds * 2f, () =>
            {
                if (tag == null) return;
                Check(_director.HasOpenDestination,
                    $"clicking a destination tag on the Milky Way opens it ({(_director.OpenDestinationModule == null ? "nothing" : _director.OpenDestinationModule.Id)})");
                if (!_director.HasOpenDestination) Say($"the tag click met: {WhatIsUnder(At(tag.position))}");
                Tap(Key.Escape, true);
            });
            Add(0.1f, () => Tap(Key.Escape, false));
            Add(SettleSeconds, () => Check(!_director.HasOpenDestination, "Escape closes the destination"));

            // 4. Restore, and the console.
            Add(0.2f, () => Tap(Key.R, true));
            Add(0.1f, () => Tap(Key.R, false));
            Add(SettleSeconds, () =>
            {
                Check(_director.Current != null && !_director.IsSwitching,
                    $"R restores without leaving the place ({Name(_director.Current)})");
                Check(_errors == 0, $"the console stayed clean through the walk ({_errors} error(s))");
            });

            _due = EditorApplication.timeSinceStartup + Steps[0].Wait;
            EditorApplication.update += Tick;
            Say($"start: {Steps.Count} steps, about {Total():0} s. Every click is a real mouse click at the thing's own screen position.");
        }

        private static ExperienceModule FirstModule()
        {
            foreach (var tile in _tiles) if (tile != null && tile.Module != null) return tile.Module;
            return null;
        }

        private static Transform FindTag()
        {
            var tags = UnityEngine.Object.FindAnyObjectByType<DestinationTags>();
            if (tags == null) return null;
            foreach (var child in tags.GetComponentsInChildren<Transform>(false))
            {
                if (child == tags.transform || child.GetComponent<Collider>() == null) continue;
                if (OnScreen(child.position)) return child;
            }
            return null;
        }

        private static string WhatIsUnder(Vector2 screen)
        {
            var cam = Camera.main;
            if (cam == null) return "no camera";
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            return Physics.Raycast(ray, out var hit, 200f)
                ? $"{hit.collider.gameObject.name} at {hit.distance:0.00} m"
                : "nothing";
        }

        private static float Total()
        {
            var seconds = 0f;
            foreach (var step in Steps) seconds += step.Wait;
            return seconds;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[APP] FAIL play mode ended mid-walk");
                Finish();
                return;
            }

            // Re-sent every tick: a press or a cursor position has to survive several frames to be seen.
            if (_holding != null)
            {
                try { _holding(); }
                catch (Exception e) { _holding = null; Debug.LogError($"[APP] FAIL the input hold threw: {e}"); }
            }

            if (EditorApplication.timeSinceStartup < _due) return;

            var step = Steps[_cursor++];
            try { step.Run(); }
            catch (Exception e) { _total++; Debug.LogError($"[APP] FAIL step {_cursor - 1} threw: {e}"); }

            if (_cursor >= Steps.Count) { Finish(); return; }
            _due = EditorApplication.timeSinceStartup + Steps[_cursor].Wait;
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            _holding = null;
            Steps.Clear();
            _cursor = 0;
            Say($"DONE {_passes}/{_total}");
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (condition != null && condition.StartsWith("[APP]")) return;
            _errors++;
        }

        private static void Add(float wait, Action run) => Steps.Add(new Step { Wait = wait, Run = run });

        private static void Check(bool ok, string what)
        {
            _total++;
            if (ok) { _passes++; Debug.Log("[APP] PASS " + what); }
            else Debug.LogError("[APP] FAIL " + what);
            Write((ok ? "PASS " : "FAIL ") + what);
        }

        private static void Skip(string what) { Debug.Log("[APP] SKIP " + what); Write("SKIP " + what); }

        private static void Say(string what) { Debug.Log("[APP] " + what); Write(what); }

        private static void Write(string line)
        {
            try
            {
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} [APP] {line}" + Environment.NewLine);
            }
            catch (Exception e) { Debug.LogWarning($"[APP] could not write {LogPath}: {e.Message}"); }
        }

        private static string Name(ExperienceModule module) => module == null ? "nothing" : module.Id;

        private static bool OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var screen = cam.WorldToScreenPoint(world);
            return screen.z > 0.05f && screen.x > 8f && screen.y > 8f
                   && screen.x < cam.pixelWidth - 8f && screen.y < cam.pixelHeight - 8f;
        }

        private static Vector2 At(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            var screen = cam.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static void Move(Vector2 at) => Send(at, false);

        private static void Press(Vector2 at, bool down) => Send(at, down);

        private static void Send(Vector2 at, bool left)
        {
            var device = MouseDevice.current;
            if (device == null) return;
            InputSystem.QueueStateEvent(device, new MouseState { position = at }.WithButton(MouseButton.Left, left));
            Flush();
        }

        private static void Tap(Key key, bool down)
        {
            var device = KeyboardDevice.current;
            if (device == null) return;
            InputSystem.QueueStateEvent(device, down ? new KeyboardState(key) : new KeyboardState());
            Flush();
        }

        // Queued events only reach the game when the input system flushes, and with the Game view unfocused
        // it does not. This is what "Lock Input to Game View" does for a person sitting at the editor.
        private static void Flush() => InputSystem.Update();
    }
}
