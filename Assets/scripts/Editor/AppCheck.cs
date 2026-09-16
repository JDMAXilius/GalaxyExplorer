// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using CosmicSimulation;
using CosmicSimulation.Being;
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

        /// <summary>Where the probe is pointing, captured once so the press and release land on one spot.</summary>
        private static Vector2 _aim;
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
                    if (wanted.Module.HasLayoutChoice)
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

            // 3. (Gone with CS-188: the Galaxies tile opens the sphere of galaxies, which step 2 already
            //    proves the way it proves every other tile. It never listed places; the assertion did.)

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

            // 5. The being, through the same pointer layer a player uses: summoned, standing at its offset,
            //    facing the head as the camera turns, carried by a drag without listening, brushed and springing
            //    back, tapped into listening and out of it, dismissed after its fade.
            var beingSettings = AssetDatabase.LoadAssetAtPath<BeingSettings>("Assets/data/being/cosmic_being_settings.asset");
            var dock = UnityEngine.Object.FindAnyObjectByType<DockController>();
            CosmicBeing being = null;
            Transform rig = null;
            var dragged = Vector3.zero;
            var brushed = 0f;
            var beingUsable = false;
            Add(0.3f, () =>
            {
                if (dock == null || beingSettings == null) { Skip("the being: no DockController or no cosmic_being_settings.asset"); return; }
                if (CosmicBeing.Instance != null) { Skip("the being: one is already summoned"); return; }
                rig = Camera.main.transform.root;
                dock.ToggleBeing();
            });
            Add(1.2f, () =>
            {
                if (dock == null || beingSettings == null) return;
                being = CosmicBeing.Instance;
                Check(being != null, "the dock's being button summons the being");
                if (being == null) return;
                var off = Vector3.Distance(being.transform.position, BeingHome(beingSettings));
                Check(off < 0.05f, $"it stands at the settings' offset from the head ({off:0.000} m off)");
                Check(FacingError(being.transform) < 15f, $"it faces the head ({FacingError(being.transform):0} deg off)");
                rig.Rotate(Vector3.up, 40f, Space.World);
            });
            Add(1.5f, () =>
            {
                if (being == null) return;
                var off = Vector3.Distance(being.transform.position, BeingHome(beingSettings));
                Check(off < 0.08f, $"with the camera turned 40 deg it has followed ({off:0.000} m off)");
                Check(FacingError(being.transform) < 15f, $"and still faces the head ({FacingError(being.transform):0} deg off)");
                rig.Rotate(Vector3.up, -40f, Space.World);
            });
            // A drag: press on it, carry the mouse 160 px right over 0.6 s, release.
            var dragFrom = Vector2.zero;
            var dragStart = 0.0;
            var dragOrigin = Vector3.zero;
            Add(1.2f, () =>
            {
                beingUsable = being != null && OnScreen(being.transform);
                if (!beingUsable) { Skip("the being's drag, brush and tap: it is not on screen"); return; }
                dragFrom = ScreenOf(being.transform);
                dragOrigin = being.transform.position;
                _holding = () => Move(dragFrom);
            });
            Add(0.3f, () => { if (beingUsable) { dragStart = EditorApplication.timeSinceStartup; _holding = () => Press(dragFrom + Vector2.right * (float)Mathf.Min(160f, 270f * (float)(EditorApplication.timeSinceStartup - dragStart)), true); } });
            Add(0.7f, () => { if (beingUsable) { _holding = null; Press(dragFrom + Vector2.right * 160f, false); } });
            Add(0.8f, () =>
            {
                if (!beingUsable) return;
                dragged = being.transform.position - dragOrigin;
                Check(dragged.magnitude > 0.03f, $"a press-drag-release carries the being ({dragged.magnitude:0.000} m)");
                Check(being.Current == CosmicBeing.Phase.Idle, $"and does not start it listening ({being.Current})");
            });
            // A brush: the mouse crosses it without pressing, then leaves.
            var brushFrom = Vector2.zero;
            var brushStart = 0.0;
            var nudge = being != null ? being.GetComponent<TouchNudge>() : null;
            Add(0.5f, () =>
            {
                if (!beingUsable) return;
                nudge = being.GetComponent<TouchNudge>();
                brushFrom = ScreenOf(being.transform) - Vector2.right * 40f;
                brushStart = EditorApplication.timeSinceStartup;
                _holding = () =>
                {
                    Move(brushFrom + Vector2.right * (float)Mathf.Min(80f, 200f * (float)(EditorApplication.timeSinceStartup - brushStart)));
                    if (nudge != null) brushed = Mathf.Max(brushed, Mathf.Abs(nudge.Angle));
                };
            });
            Add(0.6f, () => { if (beingUsable) _holding = () => Move(ScreenOf(being.transform) + Vector2.up * 400f); });
            Add(1.6f, () =>
            {
                if (!beingUsable) return;
                _holding = null;
                Check(nudge != null && brushed > 1f, $"a brush turns it ({brushed:0.0} deg at most)");
                Check(nudge != null && Mathf.Abs(nudge.Angle) < 0.5f, $"and it springs back ({(nudge == null ? 0f : nudge.Angle):0.00} deg left)");
            });
            // A tap: press and release on one spot within the tap window.
            Add(0.3f, () => { if (beingUsable) _holding = () => Move(ScreenOf(being.transform)); });
            Add(0.3f, () => { if (beingUsable) _holding = () => Press(ScreenOf(being.transform), true); });
            Add(0.15f, () =>
            {
                if (!beingUsable) return;
                _holding = null;
                Press(ScreenOf(being.transform), false);
            });
            // The release is queued above and the pointer layer handles it on its own update, so the answer is
            // read a step later rather than in the frame the release was sent.
            Add(0.1f, () =>
            {
                if (!beingUsable) return;
                if (being.Greeting || being.Current == CosmicBeing.Phase.Speaking)
                    Check(true, $"a tap speaks the greeting ({being.Current})");
                else if (Microphone.devices.Length == 0) Skip("the being's listening: this machine has no microphone, so a tap goes straight back to idle");
                else Check(being.Current == CosmicBeing.Phase.Listening, $"a tap sets it listening ({being.Current})");
            });
            // With a greeting configured the being speaks it first and only then listens (CS-194).
            var greeting = beingSettings != null && beingSettings.GreetOnTap && beingSettings.GreetingClip != null
                ? beingSettings.GreetingClip.length : 0f;
            Add(greeting > 0f ? greeting + 0.6f : 0f, () =>
            {
                if (!beingUsable || greeting <= 0f) return;
                if (Microphone.devices.Length == 0) Skip("the being's listening after the greeting: this machine has no microphone");
                else Check(being.Current == CosmicBeing.Phase.Listening, $"after the greeting it listens ({being.Current})");
            });
            Add(beingSettings != null ? beingSettings.ListenTimeoutSeconds + 0.8f : 1f, () =>
            {
                if (!beingUsable) return;
                Check(being.Current == CosmicBeing.Phase.Idle, $"and with nothing said it goes idle on the timeout ({being.Current})");
                dock.ToggleBeing();
            });
            Add(0.3f, () => { if (being != null) Check(CosmicBeing.Instance != null, "the dock button starts the being's fade out"); });
            Add(0.9f, () => { if (being != null) Check(CosmicBeing.Instance == null, "and after the fade the being is gone"); });

            // 6. Restore, and the console.
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

        /// <summary>
        /// The tag click, alone, with the pointer's own state read at every stage. The walk can tell you that
        /// a tag click opens nothing; this says which step stopped being true - whether the mouse ever hovered
        /// the tag, whether the press was taken, and what the director did with the release.
        /// </summary>
        [MenuItem("Cosmic Simulation/Verify/Tag Probe")]
        public static void TagProbe()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[TAG] FAIL not in play mode. Press Play on main_scene, then run this.");
                return;
            }

            if (Steps.Count > 0) { Debug.Log($"[TAG] already running, step {_cursor}/{Steps.Count}"); return; }

            _director = UnityEngine.Object.FindAnyObjectByType<ExperienceDirector>();
            if (_director == null) { Debug.LogError("[TAG] FAIL no ExperienceDirector"); return; }

            Steps.Clear();
            _cursor = _passes = _total = _errors = 0;
            try { System.IO.File.Delete(LogPath); } catch (Exception) { }

            Transform tag = null;
            Add(0.5f, () => { Say($"opening the Milky Way (now: {Name(_director.Current)}, intro {_director.IntroRunning})"); _director.Switch("milky_way"); });
            AddWaitWhile(() => _director.IsSwitching || Name(_director.Current) != "milky_way", 20f);
            Add(2.0f, () =>
            {
                Say($"open: {Name(_director.Current)}; POI markers in scene: {UnityEngine.Object.FindObjectsByType<GalaxyExplorer.PointOfInterest>(FindObjectsSortMode.None).Length}");
                tag = FindTag();
                Check(tag != null, "a destination tag is on screen to click");
                if (tag == null) return;
                // Captured once, not recomputed every frame.
                //
                // The tag billboards and the map drifts, so re-aiming at ScreenOf(tag) on every tick moved
                // the pointer a dozen pixels between the press and the release - past DragThresholdPixels,
                // which is exactly how DesktopMouseInput decides a press is a drag. The probe was producing
                // the orbit it then reported as a failure to click.
                _aim = ScreenOf(tag);
                Say($"aiming at {tag.parent?.name ?? tag.name} at screen {_aim}, held fixed from here");
                _holding = () => Move(_aim);
            });
            Add(1.0f, () =>
            {
                if (tag == null) return;
                Say("with the mouse on it: " + Pointer());
                _holding = () => Press(_aim, true);
            });
            Add(0.6f, () =>
            {
                if (tag == null) return;
                Say("with the button down: " + Pointer());
                _holding = null;
                Press(_aim, false);
            });
            Add(1.5f, () =>
            {
                if (tag == null) return;
                Say("after the release: " + Pointer());
                Check(_director.HasOpenDestination || Name(_director.Current) != "milky_way",
                    $"the tag click did something (destination {(_director.OpenDestinationModule == null ? "none" : _director.OpenDestinationModule.Id)}, place {Name(_director.Current)})");
            });

            _due = EditorApplication.timeSinceStartup + Steps[0].Wait;
            EditorApplication.update += Tick;
            Say("start: the tag click, one stage at a time");
        }

        /// <summary>What the desktop pointer thinks it is doing, read off its own fields.</summary>
        private static string Pointer()
        {
            var mouse = UnityEngine.Object.FindAnyObjectByType<GalaxyExplorer.XR.DesktopMouseInput>();
            if (mouse == null) return "no DesktopMouseInput";

            var type = typeof(GalaxyExplorer.XR.DesktopMouseInput);
            var hovered = Field(type, mouse, "_hovered") as GalaxyExplorer.XR.GEInteractable;
            var pressed = Field(type, mouse, "_pressed") as GalaxyExplorer.XR.GEInteractable;
            var orbiting = Field(type, mouse, "_orbiting");
            var device = MouseDevice.current;
            var camera = Field(type, mouse, "_camera") as Camera;
            var main = Camera.main;

            // The ray the pointer would actually cast, from the camera it actually holds. A pointer casting
            // from a camera nobody is looking through hovers nothing and says nothing about why.
            var cast = "no camera";
            if (camera != null && device != null)
            {
                var at = device.position.ReadValue();
                var ray = camera.ScreenPointToRay(new Vector3(at.x, at.y, 0f));
                cast = Physics.Raycast(ray, out var what, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                    ? $"{what.collider.gameObject.name}@{what.distance:0.00}m"
                    : "nothing";
            }

            return $"hovered={(hovered == null ? "NOTHING" : hovered.gameObject.name)}"
                 + $" pressed={(pressed == null ? "none" : pressed.gameObject.name)}"
                 + $" orbiting={orbiting}"
                 + $" device={(device == null ? "null" : device.position.ReadValue().ToString("F0"))}"
                 + $" button={(device != null && device.leftButton.isPressed)}"
                 + $" camera={(camera == null ? "NULL" : camera.name + (camera == main ? "" : $" (not Camera.main, which is {(main == null ? "null" : main.name)})"))}"
                 + $" itsRayMeets={cast}";
        }

        private static object Field(Type type, object instance, string name)
        {
            var field = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(instance);
        }

        private static ExperienceModule FirstModule()
        {
            foreach (var tile in _tiles) if (tile != null && tile.Module != null) return tile.Module;
            return null;
        }

        /// <summary>An option in the pop-up whose label says this, or null. Hidden options do not count.</summary>
        private static Transform FindTag()
        {
            // The map's destinations are the original POI markers (CS-170): a CardPOI's label collider on a
            // nebula, a PlanetPOI's on the Solar System and the Galactic Center. Any on-screen, enabled
            // collider under a live marker will do.
            foreach (var poi in UnityEngine.Object.FindObjectsByType<GalaxyExplorer.PointOfInterest>(FindObjectsSortMode.None))
            {
                if (!poi.isActiveAndEnabled) continue;
                foreach (var collider in poi.GetComponentsInChildren<Collider>(false))
                {
                    if (!collider.enabled || collider.bounds.size == Vector3.zero) continue;
                    if (OnScreen(collider.transform)) return collider.transform;
                }
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

        private static Vector3 BeingHome(BeingSettings settings)
        {
            var head = Camera.main.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            return head.position + forward * settings.DistanceMetres - right * settings.SideMetres + Vector3.up * settings.DropMetres;
        }

        private static float FacingError(Transform being)
        {
            var toHead = Camera.main.transform.position - being.position;
            toHead.y = 0f;
            return Vector3.Angle(being.forward, toHead);
        }

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
