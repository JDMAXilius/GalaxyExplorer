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
            var askedDuringIntro = false;
            Add(0.4f, () =>
            {
                Check(_director != null, "the director is in the scene");
                Check(_tiles.Count >= 7, $"the dock carries its tiles ({_tiles.Count})");
                if (!_director.IntroRunning)
                {
                    Skip("the intro click: the intro was already over when the walk started");
                    return;
                }
                if (first == null) return;
                askedDuringIntro = true;
                _director.Switch(first);
            });
            Add(SettleSeconds * 3f, () =>
            {
                // Only assert what was actually exercised: asserting after a skip turns "we did not test this"
                // into a failure, which is worse than saying nothing.
                if (!askedDuringIntro) return;
                Check(!_director.IntroRunning, "a place asked for during the intro ends the intro");
                Check(_director.Current == first, $"and that place opens ({Name(_director.Current)})");
            });

            // 2. Every tile, clicked where it sits on screen.
            foreach (var tile in _tiles)
            {
                var wanted = tile;
                if (wanted == null || wanted.Module == null) continue;
                var clickable = false;
                Add(0.3f, () =>
                {
                    clickable = OnScreen(wanted.transform);
                    if (!clickable)
                    {
                        Skip($"the {wanted.Module.Id} tile: it is not on screen to click");
                        return;
                    }
                    _holding = () => Move(ScreenOf(wanted.transform));
                });
                Add(0.4f, () => { if (clickable) _holding = () => Press(ScreenOf(wanted.transform), true); });
                Add(0.2f, () => { if (clickable) { _holding = null; Press(ScreenOf(wanted.transform), false); } });
                // A switch is a scene load for some places, and a fixed wait would call the slowest of them a
                // failure. Wait for the director to stop switching, up to a bound.
                Add(0.2f, () => { });
                AddWaitWhile(() => _director.IsSwitching, 6f);
                Add(0.4f, () =>
                {
                    // The decision was made before the click, not after: a dock that hides itself on a switch
                    // would otherwise turn a real result into a silent skip.
                    if (!clickable) return;

                    // A tile that offers a choice - the Solar System's layouts, the Galaxies tile's galaxies -
                    // answers with the panel instead of opening something. Asserting that it switched called
                    // correct behaviour a failure twice before this was written down.
                    if (wanted.Module.HasLayoutChoice || wanted.Module.HasPlaceChoice)
                    {
                        var panel = UnityEngine.Object.FindAnyObjectByType<DockPopup>();
                        Check(panel != null && panel.IsOpen,
                            $"clicking the {wanted.Module.Id} tile offers its choices rather than opening ({(panel == null ? "no pop-up" : panel.IsOpen ? "open" : "shut")})");
                        if (panel != null && panel.IsOpen) panel.Close();
                        return;
                    }

                    Check(_director.Current == wanted.Module,
                        $"clicking the {wanted.Module.Id} tile opens it ({Name(_director.Current)})");
                    var room = EnvironmentController.Instance;
                    Check(room != null && room.EffectiveMode == wanted.Module.Environment,
                        $"and the room goes to {wanted.Module.Environment} ({(room == null ? "there is no EnvironmentController" : room.EffectiveMode.ToString())})");
                });
            }

            // 3. The Galaxies tile offers the galaxies we know, and picking one opens it as its own place -
            //    the same switch Andromeda's tile makes. Clicked, not called.
            DockTile galaxiesTile = null;
            foreach (var tile in _tiles)
                if (tile != null && tile.Module != null && tile.Module.Id == "galaxies") galaxiesTile = tile;
            Transform galaxyOption = null;
            Add(0.3f, () =>
            {
                if (galaxiesTile == null) { Skip("the galaxy list: there is no Galaxies tile"); return; }
                Check(galaxiesTile.Module.HasPlaceChoice,
                    $"the Galaxies tile offers places ({(galaxiesTile.Module.Places == null ? 0 : galaxiesTile.Module.Places.Length)})");
                if (!OnScreen(galaxiesTile.transform)) { Skip("the galaxy list: the Galaxies tile is not on screen"); return; }
                _holding = () => Move(ScreenOf(galaxiesTile.transform));
            });
            Add(0.4f, () => { if (galaxiesTile != null) _holding = () => Press(ScreenOf(galaxiesTile.transform), true); });
            Add(0.2f, () => { if (galaxiesTile != null) { _holding = null; Press(ScreenOf(galaxiesTile.transform), false); } });
            Add(SettleSeconds, () =>
            {
                if (galaxiesTile == null) return;
                var popup = UnityEngine.Object.FindAnyObjectByType<DockPopup>();
                Check(popup != null && popup.IsOpen, "clicking the Galaxies tile opens the galaxy list");
                galaxyOption = FindOption(popup, "Whirlpool");
                if (galaxyOption == null) { Skip("the galaxy pick: no Whirlpool option is on screen"); return; }
                _holding = () => Move(ScreenOf(galaxyOption));
            });
            Add(0.4f, () => { if (galaxyOption != null) _holding = () => Press(ScreenOf(galaxyOption), true); });
            Add(0.2f, () => { if (galaxyOption != null) { _holding = null; Press(ScreenOf(galaxyOption), false); } });
            AddWaitWhile(() => _director.IsSwitching, 8f);
            Add(SettleSeconds, () =>
            {
                if (galaxyOption == null) return;
                Check(Name(_director.Current) == "whirlpool",
                    $"picking Whirlpool from the list opens it as its own place ({Name(_director.Current)})");
            });

            // 4. A destination tag on the Milky Way, clicked with the mouse. This is the one the owner asked
            //    about by name, and the one nothing had ever proven.
            Transform tag = null;
            Add(0.3f, () => _director.Switch("milky_way"));
            Add(SettleSeconds * 2f, () =>
            {
                Check(Name(_director.Current) == "milky_way", $"the Milky Way opens ({Name(_director.Current)})");
                tag = FindTag();
                if (tag == null) { Skip("the destination tag click: no tag with a collider is on screen"); return; }
                _holding = () => Move(ScreenOf(tag));
            });
            Add(0.5f, () => { if (tag != null) _holding = () => Press(ScreenOf(tag), true); });
            Add(0.2f, () => { if (tag != null) { _holding = null; Press(ScreenOf(tag), false); } });
            Add(SettleSeconds * 2f, () =>
            {
                if (tag == null) return;
                Check(_director.HasOpenDestination,
                    $"clicking a destination tag on the Milky Way opens it ({(_director.OpenDestinationModule == null ? "nothing" : _director.OpenDestinationModule.Id)})");
                if (!_director.HasOpenDestination) Say($"the tag click met: {WhatIsUnder(ScreenOf(tag))}");
                Tap(Key.Escape, true);
            });
            Add(0.1f, () => Tap(Key.Escape, false));
            Add(SettleSeconds, () => Check(!_director.HasOpenDestination, "Escape closes the destination"));

            // 5. Restore, and the console.
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

        /// <summary>An option in the pop-up whose label says this, or null. Hidden options do not count.</summary>
        private static Transform FindOption(DockPopup popup, string text)
        {
            if (popup == null) return null;
            foreach (var label in popup.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (label.text == null || label.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var option = label.transform.parent != null ? label.transform.parent : label.transform;
                if (OnScreen(option)) return option;
            }
            return null;
        }

        private static Transform FindTag()
        {
            var tags = UnityEngine.Object.FindAnyObjectByType<DestinationTags>();
            if (tags == null) return null;
            foreach (var child in tags.GetComponentsInChildren<Transform>(false))
            {
                if (child == tags.transform || child.GetComponent<Collider>() == null) continue;
                if (OnScreen(child)) return child;
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

        // Polls a condition on the step clock rather than blocking: the walk runs from EditorApplication.update
        // and a spin here would freeze the editor it is measuring.
        private static void AddWaitWhile(Func<bool> busy, float limitSeconds)
        {
            var started = -1.0;
            Add(0.1f, () =>
            {
                if (started < 0) started = EditorApplication.timeSinceStartup;
                if (busy() && EditorApplication.timeSinceStartup - started < limitSeconds)
                {
                    // Re-queue this step by stepping the cursor back one; the clock does the waiting.
                    _cursor--;
                    _due = EditorApplication.timeSinceStartup + 0.1f;
                }
            });
        }

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

        // The desktop dock is a screen-space canvas, and a screen-space canvas already holds its tiles at
        // pixel coordinates: tile_milky_way sits at (1533, 66, 0), which through Camera.WorldToScreenPoint
        // would come back as nonsense and aim every click at nothing. So the canvas decides how a position
        // becomes a screen point, and only objects that are genuinely in the world go through the camera.
        private static Vector2 ScreenOf(Transform thing)
        {
            if (thing == null) return Vector2.zero;
            var canvas = thing.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var through = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                return RectTransformUtility.WorldToScreenPoint(through, thing.position);
            }

            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            var screen = cam.WorldToScreenPoint(thing.position);
            return new Vector2(screen.x, screen.y);
        }

        private static bool Behind(Transform thing)
        {
            if (thing == null) return true;
            if (thing.GetComponentInParent<Canvas>() != null) return false;
            var cam = Camera.main;
            return cam == null || cam.WorldToScreenPoint(thing.position).z <= 0.05f;
        }

        private static bool OnScreen(Transform thing)
        {
            var cam = Camera.main;
            if (cam == null || Behind(thing)) return false;
            var screen = ScreenOf(thing);
            return screen.x > 8f && screen.y > 8f
                   && screen.x < cam.pixelWidth - 8f && screen.y < cam.pixelHeight - 8f;
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
