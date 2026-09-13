using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ARCameraManager = UnityEngine.XR.ARFoundation.ARCameraManager;
using CameraEvent = UnityEngine.Rendering.CameraEvent;
using EventSystem = UnityEngine.EventSystems.EventSystem;
using InputActionManager = UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager;
using InputSystem = UnityEngine.InputSystem.InputSystem;
using Key = UnityEngine.InputSystem.Key;
using KeyboardDevice = UnityEngine.InputSystem.Keyboard;
using KeyboardState = UnityEngine.InputSystem.LowLevel.KeyboardState;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;
using MouseDevice = UnityEngine.InputSystem.Mouse;
using MouseState = UnityEngine.InputSystem.LowLevel.MouseState;
using NearFarInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;
using XRGeneralGrabTransformer = UnityEngine.XR.Interaction.Toolkit.Transformers.XRGeneralGrabTransformer;
using XRInputModalityManager = UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager;
using XRInteractionManager = UnityEngine.XR.Interaction.Toolkit.XRInteractionManager;
using XROrigin = Unity.XR.CoreUtils.XROrigin;
using XRPokeInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor;
using XRUIInputModule = UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule;

namespace Cosmic.Editor
{
    // Relay-driven test tooling, not a rework system: it is outside the six-editor-script budget in RULES.md.
    public static class Verify
    {
        const string Generated = "Assets/Cosmic/Data/Generated";
        const string LibraryPath = Generated + "/audio_library.asset";
        const string DimPath = "Assets/Cosmic/Prefabs/room_dim.mat";
        const string ActionsPath = "Assets/Cosmic/Input/actions.inputactions";
        const string UiFolder = "Assets/audio/ui_audio_clips/";
        const string MusicFolder = "Assets/audio/music_audio_clips/";
        const string VoicePath = "Assets/audio/vo_audio_clips/vo_intro_audio_clips/vo_intro_01_audio_clip.wav";
        const string AmbiencePath = "Assets/audio/ambience_audio_clips/ambience_earth_audio_clip.wav";
        const string TintShader = "CosmicSimulation/EnvironmentTint";
        const string HostName = "cosmic_verify";
        const string SubjectName = "cosmic_verify_subject";
        const string TweenName = "cosmic_verify_tween";
        const string RigPath = "Assets/Cosmic/Prefabs/rig.prefab";
        const string BodyName = "cosmic_verify_body";
        const float MusicLevel = 0.35f;
        const float DuckedLevel = MusicLevel * 0.55f;
        const float DimAlpha = 0.5f;
        const float Tolerance = 0.05f;
        const float BodyWidthMetres = 0.15f;
        const float BodyHeightMetres = 1.2f;
        // Two metres, not one: Mouse parks its attach 1 m down the ray and Pull.IsFar calls anything within 12 cm of it a near grab, which never dwells.
        const float BodyDistanceMetres = 2f;
        const float PulledWidthMetres = 0.25f;
        const float RestoreOffsetMetres = 0.4f;
        const float StrayMetres = 6f;
        const float DragPixels = 200f;
        const float RawWheelNotch = 120f;
        const float WheelScalePerNotch = 1.1f;
        const string GalaxyFolder = Generated + "/galaxies";
        const string CloudFolder = Generated + "/points";
        const string LayoutFolder = Generated + "/layouts";
        const string MaterialFolder = "Assets/Cosmic/Prefabs/materials";
        const string PlacePrefabFolder = "Assets/Cosmic/Prefabs/places";
        const string BodyPrefabFolder = "Assets/Cosmic/Prefabs/bodies";
        const string PointShaderName = "Cosmic/Points";
        const int PointStrideBytes = 40;
        const int WebPoints = 40000;
        const int NebulaPoints = 28000;
        const int PointMaterials = 23;
        const float NebulaRadiusMetres = 0.35f;
        const float GalaxyAheadMetres = 1.5f;
        const float SystemAheadMetres = 2.5f;
        const float SunReachMetres = 1.2f;
        const float SlotToleranceMetres = 0.005f;
        const float LayoutMovedMetres = 0.1f;
        const float OrbitMovedMetres = 0.02f;
        const float SunScaleAtRealism = 0.0001f;
        const float TouchHigh = 0.5f;
        const float TouchLow = 0.1f;

        // ellipses * starsPerEllipse * armCount, read off BakeGalaxies.SpiralLayers and its per-galaxy overrides.
        static readonly (string id, int clouds, int dust, int stars)[] GalaxyCounts =
        {
            ("milky_way", 6400, 4000, 8320), ("andromeda", 5000, 3200, 9000), ("whirlpool", 5000, 3200, 9000),
            ("pinwheel", 7500, 4800, 13500), ("triangulum", 7500, 4800, 13500),
        };

        static readonly string[] NebulaIds =
            { "helix", "crab", "ngc1501", "homunculus", "orion", "pillars", "trumpler14" };

        // Every generated place except hint_grab and hint_scale, which are Phase 6 cards and own no content prefab.
        static readonly string[] PlaceIds =
        {
            "milky_way", "andromeda", "whirlpool", "pinwheel", "triangulum",
            "helix", "crab", "ngc1501", "homunculus", "orion", "pillars", "trumpler14",
            "cosmic_web", "galaxies", "sagittarius_a", "solar_system", "solar_system_planets", "hd110067",
        };

        static readonly string[] SolarIds =
            { "sun", "mercury", "venus", "earth", "mars", "jupiter", "saturn", "uranus", "neptune", "pluto" };

        static readonly (string id, LayoutKind kind)[] Arrangements =
        {
            ("relative_size", LayoutKind.Relative), ("solar_row", LayoutKind.Row),
            ("solar_schematic", LayoutKind.Schematic), ("solar_realistic", LayoutKind.Realistic),
        };

        class Step
        {
            public float waitSeconds;
            public System.Action run;
        }

        static readonly List<Step> steps = new List<Step>();
        static System.Action<RoomMode> listener, roomListener;
        static System.Action prefListener, restoreListener, holding, cleanup;
        static Cosmic.Audio audio;
        static AudioClip voiceClip, ambienceClip;
        static AudioSource bed, loop;
        static Transform subject;
        static VerifyRunner runner;
        static Grabbable body;
        static Pull pull;
        static Cosmic.Mouse pointer;
        static Hotkeys keys;
        static VerifyProbe probe;
        static Vector3 home;
        static Layout[] arrangementAssets;
        static Points galaxyPoints;
        static Grabbable galaxyGrab;
        static Collider galaxyHull;
        static Rig systemRig;
        static Orbit systemOrbit;
        static Sun star;
        static Transform sunAnchor;
        static string tag = "[P2]";
        static double due;
        static float bedTime;
        static int cursor, passes, total, fires, roomFires, prefFires;

        [MenuItem("Cosmic/Verify/P0 Compile Check")]
        public static void P0()
        {
            var names = new HashSet<string>();
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) names.Add(assembly.GetName().Name);
            var runtime = names.Contains("Cosmic.Runtime");
            var editor = names.Contains("Cosmic.Editor");
            Debug.Log($"[P0] assemblies loaded: Cosmic.Runtime={runtime} Cosmic.Editor={editor} (of {names.Count} total)");
            if (runtime && editor) Debug.Log("[P0] PASS both Cosmic assemblies are present");
            else Debug.LogError("[P0] FAIL a Cosmic assembly is missing; run tools/mcp/compile.ps1 and read the errors");

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ActionsPath);
            var kind = asset == null ? "nothing" : asset.GetType().Name;
            var onDisk = Path.Combine(Directory.GetCurrentDirectory(), ActionsPath);
            if (!File.Exists(onDisk))
            {
                Debug.LogError("[P0] FAIL " + ActionsPath + " does not exist");
                return;
            }

            var maps = new List<string>();
            var counts = new List<int>();
            var section = 0;
            foreach (var raw in File.ReadAllLines(onDisk))
            {
                var indent = raw.Length - raw.TrimStart(' ').Length;
                var line = raw.Trim();
                if (indent == 4 && line.StartsWith("\"controlSchemes\"")) break;
                if (indent == 12 && line.StartsWith("\"name\": \"")) { maps.Add(Quoted(line)); counts.Add(0); section = 0; }
                else if (indent == 12 && line.StartsWith("\"actions\"")) section = 1;
                else if (indent == 12 && line.StartsWith("\"bindings\"")) section = 2;
                else if (indent == 20 && section == 1 && counts.Count > 0 && line.StartsWith("\"name\": \"")) counts[counts.Count - 1]++;
            }

            var report = string.Empty;
            for (var i = 0; i < maps.Count; i++) report += (i > 0 ? ", " : string.Empty) + maps[i] + "=" + counts[i] + " actions";
            Debug.Log($"[P0] {ActionsPath} imports as {kind}; maps: {report}");
            if (kind == "InputActionAsset" && maps.Contains("XR") && maps.Contains("Desktop"))
                Debug.Log("[P0] PASS the actions asset imports as an InputActionAsset with both the XR and Desktop maps");
            else
                Debug.LogError("[P0] FAIL the actions asset did not import as an InputActionAsset with both the XR and Desktop maps");
        }

        [MenuItem("Cosmic/Verify/P1 Import And Check")]
        public static void P1()
        {
            if (!EditorApplication.ExecuteMenuItem("Cosmic/Import Copy"))
            {
                Debug.LogError("[P1] FAIL the menu item Cosmic/Import Copy does not exist; Cosmic.Editor did not compile");
                return;
            }

            var first = Guids();
            EditorApplication.ExecuteMenuItem("Cosmic/Import Copy");
            var second = Guids();

            Debug.Log($"[P1] generated: places={Count("places")} bodies={Count("bodies")} layouts={Count("layouts")}");

            var churned = 0;
            foreach (var pair in first)
                if (!second.TryGetValue(pair.Key, out var guid) || guid != pair.Value) churned++;
            if (churned == 0 && first.Count == second.Count && first.Count > 0)
                Debug.Log($"[P1] PASS a second import left all {first.Count} generated GUIDs unchanged");
            else
                Debug.LogError($"[P1] FAIL a second import churned {churned} GUIDs and the asset count went {first.Count} -> {second.Count}");

            Debug.Log("[P1] now run, from the repo root: python3 tools/parity/check_data.py");
            Debug.Log("[P1] expected residual loss, and nothing else: unowned Narration, ContentPrefab and DockThumbnail " +
                      "references (the rework has no owner for them until Phase 6 wiring); 3 room modes the copy deck does " +
                      "not state, which stay authored; 1 layout, because Copy writes none and layouts/ is a placeholder " +
                      "folder; 11 builder-owned assets. Any other class of loss is a regression in Copy.cs.");
        }

        [MenuItem("Cosmic/Verify/P2 Setup")]
        public static void P2Setup()
        {
            tag = "[P2]";
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), Generated));
            AssetDatabase.Refresh();

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            var madeLibrary = library == null;
            if (madeLibrary)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.entries = new[]
            {
                Entry(Sfx.Focus, "ui_default_focus"),
                Entry(Sfx.Select, "ui_select"),
                Entry(Sfx.PokeRelease, "ui_touch_deselect"),
                Entry(Sfx.DockShow, "ui_handmenu_appear"),
                Entry(Sfx.DockHide, "ui_handmenu_disappear"),
                Entry(Sfx.PopupOpen, "ui_toolbox_show"),
                Entry(Sfx.PopupClose, "ui_toolbox_hide"),
                Entry(Sfx.PanelOpen, "ui_poi_card_select"),
                Entry(Sfx.PanelClose, "ui_poi_card_deselect"),
                Entry(Sfx.Grab, "ui_forcegrab_hold"),
                Entry(Sfx.Release, "ui_forcegrab_release"),
                Entry(Sfx.Pull, "ui_forcegrab_pull"),
                Entry(Sfx.Beam, "ui_tractor_beam"),
                Entry(Sfx.GrowIn, null),
            };
            library.musicByRoom = new[]
            {
                Bed("background_music"),
                Bed("bgm_system"),
                Bed("bgm_galaxy"),
                Bed("bgm_galaxy"),
            };
            EditorUtility.SetDirty(library);

            var shader = Shader.Find(TintShader);
            if (shader == null)
            {
                Debug.LogError("[P2] FAIL shader " + TintShader + " was not found");
                return;
            }

            var dim = AssetDatabase.LoadAssetAtPath<Material>(DimPath);
            var madeDim = dim == null;
            if (madeDim)
            {
                dim = new Material(shader);
                AssetDatabase.CreateAsset(dim, DimPath);
            }
            dim.shader = shader;
            dim.color = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(dim);
            AssetDatabase.SaveAssets();

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[P2] FAIL the active scene has never been saved, and SaveScene would raise a modal dialog " +
                               "that blocks the relay. Open a saved scene in the editor by hand and run this again.");
                return;
            }

            GameObject host = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == HostName) host = root;
            var madeHost = host == null;
            if (madeHost) host = new GameObject(HostName);

            var player = host.GetComponent<Cosmic.Audio>();
            if (player == null) player = host.AddComponent<Cosmic.Audio>();
            var room = host.GetComponent<Room>();
            if (room == null) room = host.AddComponent<Room>();

            Bind(player, "library", library);
            Bind(room, "dimMaterial", dim);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[P2] setup: {LibraryPath} {(madeLibrary ? "created" : "updated")}, {DimPath} {(madeDim ? "created" : "updated")}, " +
                      $"{HostName} {(madeHost ? "created" : "updated")} and saved into {scene.path}. " +
                      $"Camera.main is {(Camera.main != null ? "present" : "absent in edit mode - if none appears in play mode the dim and Black checks will FAIL")}.");
        }

        [MenuItem("Cosmic/Verify/P2 Enter Play")]
        public static void P2EnterPlay()
        {
            if (EditorApplication.isPlaying) { Debug.Log("[P2] already in play mode"); return; }
            EditorApplication.isPlaying = true;
            Debug.Log("[P2] play requested; poll isPlaying, it reads false on this frame");
        }

        [MenuItem("Cosmic/Verify/P2 Leave Play")]
        public static void P2LeavePlay()
        {
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = false;
            Debug.Log("[P2] leaving play mode; the editor stays open");
        }

        [MenuItem("Cosmic/Verify/P2 Run")]
        public static void P2Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[P2] FAIL not in play mode; run Cosmic/Verify/P2 Enter Play, poll isPlaying, then run this");
                return;
            }
            if (steps.Count > 0) { Debug.Log($"[P2] already running, step {cursor}/{steps.Count}"); return; }

            audio = UnityEngine.Object.FindAnyObjectByType<Cosmic.Audio>();
            if (audio == null)
            {
                Debug.LogError("[P2] FAIL no Cosmic.Audio in the loaded scenes; run Cosmic/Verify/P2 Setup, then re-enter play mode");
                return;
            }

            voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoicePath);
            ambienceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbiencePath);
            tag = "[P2]";
            cursor = passes = total = fires = 0;
            bed = loop = null;
            subject = null;
            runner = null;
            holding = null;
            cleanup = () =>
            {
                if (listener != null) { Room.Changed -= listener; listener = null; }
                if (subject != null) UnityEngine.Object.Destroy(subject.gameObject);
                if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
                subject = null;
                runner = null;
                bed = loop = null;
            };
            Build();
            due = EditorApplication.timeSinceStartup + steps[0].waitSeconds;
            EditorApplication.update += Tick;
            Debug.Log($"[P2] start: {steps.Count} steps, about 13 s. Switch Error Pause off first - a FAIL is a LogError and would pause play mode.");
        }

        [MenuItem("Cosmic/Verify/P2 Teardown")]
        public static void P2Teardown()
        {
            tag = "[P2]";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[P2] FAIL the active scene has never been saved; SaveScene would block the relay on a modal dialog");
                return;
            }

            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != HostName && root.name != SubjectName && root.name != TweenName) continue;
                UnityEngine.Object.DestroyImmediate(root);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[P2] teardown: removed {removed} object(s) from {scene.path}. {LibraryPath} and {DimPath} are kept - they are the Phase 6 wiring inputs.");
        }

        static void Build()
        {
            steps.Clear();
            var system = Bed("bgm_system");
            var galaxy = Bed("bgm_galaxy");
            var wasMuted = false;
            var wasScale = 1f;

            Add(0f, () =>
            {
                Room.ForcePassthrough(false);
                Room.Set(RoomMode.Passthrough, 0f);
                listener = _ => fires++;
                Room.Changed += listener;
                fires = 0;
            });
            Add(0.2f, () => Room.Set(RoomMode.Dimmed));
            Add(0.5f, () =>
            {
                var quad = GameObject.Find("room_dim_quad");
                Check(quad != null && quad.activeInHierarchy && quad.GetComponent<MeshRenderer>() != null, "room_dim_quad is an active MeshRenderer");
                var material = quad != null ? quad.GetComponent<Renderer>().sharedMaterial : null;
                Check(material != null && Near(material.color.a, DimAlpha), "the dim reaches alpha " + DimAlpha);
                Check(fires == 1, "Room.Changed fired once entering Dimmed");
                bed = Playing(system);
                bedTime = bed != null ? bed.time : 0f;
                Check(Count(system) == 1, "exactly one music source plays bgm_system");
            });
            Add(0.1f, () => Room.Set(RoomMode.Dimmed));
            Add(0.2f, () =>
            {
                Check(fires == 1, "re-setting Dimmed does not fire Room.Changed again");
                Check(bed != null && bed.isPlaying && bed.clip == system && bed.time > bedTime, "the bed kept playing rather than restarting");
            });
            Add(0f, () => Room.Set(RoomMode.Black));
            Add(0.2f, () =>
            {
                var cam = Camera.main;
                Check(cam != null && Mathf.Approximately(cam.backgroundColor.a, 1f), "Camera.main clears to opaque black");
                Check(fires == 2, "Room.Changed fired entering Black");
            });
            Add(2.5f, () =>
            {
                var source = Playing(galaxy);
                Check(source != null && Near(source.volume, MusicLevel), "bgm_galaxy crossfaded in at " + MusicLevel);
                Check(Idle(source), "the outgoing music source stopped");
            });
            Add(0f, () => Room.ForcePassthrough(true));
            Add(0.2f, () =>
            {
                Check(Room.Effective == RoomMode.Passthrough, "ForcePassthrough overrides Black");
                Check(fires == 3, "ForcePassthrough fired Room.Changed once");
            });
            Add(0f, () =>
            {
                Room.ForcePassthrough(false);
                Room.Set(RoomMode.Halo);
                subject = new GameObject(SubjectName).transform;
                subject.position = new Vector3(0f, 0f, 2f);
                Room.Halo(subject, 0.5f);
            });
            Add(0.7f, () =>
            {
                var quad = GameObject.Find("room_halo_quad");
                var halo = quad != null ? quad.GetComponent<Renderer>() : null;
                Check(halo != null && halo.enabled, "room_halo_quad renders in Halo");
                Check(halo != null && halo.sharedMaterial.color.a > 0.9f, "the halo fades past alpha 0.9");
            });
            Add(0f, () => Room.Forget(subject));
            Add(0.3f, () => Check(GameObject.Find("room_halo_quad") == null, "Room.Forget destroys the halo quad"));
            Add(0f, () => Room.Set(RoomMode.Black));
            Add(2.5f, () =>
            {
                bed = Playing(galaxy);
                Check(bed != null && Near(bed.volume, MusicLevel), "music sits at " + MusicLevel + " before the duck");
                audio.Say(voiceClip);
            });
            Add(0.15f, () => Check(audio.Speaking, "Say puts Audio into Speaking"));
            Add(0.6f, () => Check(bed != null && Near(bed.volume, DuckedLevel), "music ducks to " + DuckedLevel + " under narration"));
            Add(0f, () => audio.StopVoice());
            Add(0.6f, () =>
            {
                Check(!audio.Speaking, "StopVoice clears Speaking");
                Check(bed != null && Near(bed.volume, MusicLevel), "music recovers to " + MusicLevel);
            });
            Add(0f, () =>
            {
                var before = Voices();
                audio.Play(Sfx.Select);
                audio.Play(Sfx.Select);
                Check(Voices() - before == 1, "two unplaced Select calls in one frame debounce to one voice");
            });
            Add(0.2f, () =>
            {
                var before = Voices();
                audio.Play(Sfx.Select, subject);
                audio.Play(Sfx.Select, subject);
                Check(Voices() - before == 2, "two placed Select calls are exempt from the debounce");
            });
            Add(0.2f, () => audio.Ambience(ambienceClip));
            Add(0.5f, () =>
            {
                var found = audio.transform.Find("ambience");
                var source = found != null ? found.GetComponent<AudioSource>() : null;
                Check(source != null && source.isPlaying && source.clip == ambienceClip && source.volume > 0.05f, "Ambience plays and is fading up");
            });
            Add(0f, () =>
            {
                loop = audio.Loop(ambienceClip);
                Check(loop != null && loop.isPlaying && loop.loop, "Loop returns a playing looped source");
            });
            Add(0.2f, () => audio.Release(loop));
            Add(0.2f, () => Check(loop == null, "Release destroys the loop source"));
            Add(0f, () =>
            {
                wasMuted = Prefs.Muted;
                wasScale = Prefs.TextScale;
                Prefs.Muted = false;
                Prefs.Muted = true;
            });
            Add(0.15f, () =>
            {
                Check(Mathf.Approximately(AudioListener.volume, 0f), "Prefs.Muted silences the AudioListener");
                Prefs.TextScale = 1.3f;
            });
            Add(0.15f, () =>
            {
                Check(Mathf.Approximately(Prefs.TextScale, 1.25f), "TextScale 1.3 snaps to 1.25");
                Prefs.Muted = wasMuted;
                Prefs.TextScale = wasScale;
                Debug.Log($"[P2] PlayerPrefs written: Cosmic.Muted={PlayerPrefs.GetInt("Cosmic.Muted", 0)} " +
                          $"Cosmic.VoiceMuted={PlayerPrefs.GetInt("Cosmic.VoiceMuted", 0)} " +
                          $"Cosmic.TextScale={PlayerPrefs.GetFloat("Cosmic.TextScale", 1f)} " +
                          $"Cosmic.HintsSeen={PlayerPrefs.GetInt("Cosmic.HintsSeen", 0)} " +
                          $"Cosmic.LabelsVisible={PlayerPrefs.GetInt("Cosmic.LabelsVisible", 1)}");
            });
            Add(0.15f, () =>
            {
                Check(Mathf.Approximately(AudioListener.volume, wasMuted ? 0f : 1f), "the mute preference is restored");
                var go = new GameObject(TweenName);
                runner = go.AddComponent<VerifyRunner>();
                runner.from = Vector3.zero;
                runner.to = new Vector3(0f, 0f, 1f);
                runner.StartCoroutine(Tween.To(go.transform, runner.to, Quaternion.identity, Vector3.one, 0.4f));
            });
            Add(0.55f, () =>
            {
                Check(runner != null && runner.transform.localPosition == runner.to, "Tween.To lands exactly on target");
                Check(runner != null && runner.worst <= 1.0001f, "Tween.To never overshoots target");
                Room.ForcePassthrough(false);
                Room.Set(RoomMode.Passthrough);
            });
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError($"{tag} FAIL play mode ended mid-run");
                Finish();
                return;
            }
            // Synthetic input has to be re-sent every tick for a press or a cursor position that must survive several frames.
            if (holding != null)
            {
                try { holding(); }
                catch (System.Exception e) { holding = null; Debug.LogError($"{tag} FAIL the input hold threw: {e}"); }
            }
            if (EditorApplication.timeSinceStartup < due) return;

            var step = steps[cursor++];
            try { step.run(); }
            catch (System.Exception e) { total++; Debug.LogError($"{tag} FAIL step {cursor - 1} threw: {e}"); }

            if (cursor >= steps.Count) { Finish(); return; }
            due = EditorApplication.timeSinceStartup + steps[cursor].waitSeconds;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            holding = null;
            if (cleanup != null)
            {
                var last = cleanup;
                cleanup = null;
                try { last(); }
                catch (System.Exception e) { Debug.LogError($"{tag} FAIL the cleanup threw: {e}"); }
            }
            steps.Clear();
            cursor = 0;
            Debug.Log($"{tag} DONE {passes}/{total}");
        }

        static void Add(float waitSeconds, System.Action run) => steps.Add(new Step { waitSeconds = waitSeconds, run = run });

        static void Check(bool ok, string what)
        {
            total++;
            if (ok) { passes++; Debug.Log(tag + " PASS " + what); }
            else Debug.LogError(tag + " FAIL " + what);
        }

        // A skip is neither a pass nor a failure, so it stays out of the DONE n/m count and says why in words.
        static void Skip(string what) => Debug.Log(tag + " SKIP " + what);

        static bool Near(float value, float expected) => Mathf.Abs(value - expected) <= Tolerance;

        static AudioSource Playing(AudioClip clip)
        {
            foreach (var source in audio.GetComponentsInChildren<AudioSource>())
                if (source.name.StartsWith("music_") && source.isPlaying && source.clip == clip) return source;
            return null;
        }

        static int Count(AudioClip clip)
        {
            var n = 0;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>())
                if (source.name.StartsWith("music_") && source.isPlaying && source.clip == clip) n++;
            return n;
        }

        static bool Idle(AudioSource except)
        {
            foreach (var source in audio.GetComponentsInChildren<AudioSource>())
                if (source.name.StartsWith("music_") && source != except && source.isPlaying) return false;
            return true;
        }

        static int Voices()
        {
            var n = 0;
            foreach (var source in audio.GetComponentsInChildren<AudioSource>())
                if (source.name.StartsWith("sfx_") && source.isPlaying) n++;
            return n;
        }

        static AudioLibrary.Entry Entry(Sfx id, string clipName)
        {
            AudioClip clip = null;
            if (clipName != null)
            {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiFolder + clipName + "_audio_clip.wav");
                if (clip == null) Debug.LogError($"[P2] FAIL missing clip {UiFolder}{clipName}_audio_clip.wav for {id}");
            }
            return new AudioLibrary.Entry { id = id, clip = clip, volume = 1f };
        }

        static AudioClip Bed(string clipName) => AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + clipName + "_audio_clip.wav");

        static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{tag} FAIL {target.GetType().Name} has no serialized field '{field}'");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Dictionary<string, string> Guids()
        {
            var map = new Dictionary<string, string>();
            foreach (var sub in new[] { "places", "bodies", "layouts" })
            {
                var folder = Generated + "/" + sub;
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                    map[AssetDatabase.GUIDToAssetPath(guid)] = guid;
            }
            return map;
        }

        static int Count(string sub)
        {
            var folder = Generated + "/" + sub;
            return AssetDatabase.IsValidFolder(folder) ? AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }).Length : 0;
        }

        static string Quoted(string line)
        {
            var start = line.IndexOf('"', line.IndexOf(':')) + 1;
            var end = line.LastIndexOf('"');
            return end > start ? line.Substring(start, end - start) : string.Empty;
        }

        [MenuItem("Cosmic/Verify/P3 Build Rig")]
        public static void P3Rig()
        {
            tag = "[P3]";
            if (steps.Count > 0) { Debug.LogError(tag + " FAIL a verify sequence is live; leave play mode and try again"); return; }
            passes = total = 0;

            if (!EditorApplication.ExecuteMenuItem("Cosmic/Build/Rig"))
            {
                Debug.LogError(tag + " FAIL the menu item Cosmic/Build/Rig does not exist; Cosmic.Editor did not compile");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(RigPath);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (rig == null)
            {
                Debug.LogError(tag + " FAIL " + RigPath + " does not exist after Cosmic/Build/Rig; read the `Cosmic rig ... missing:` line above it");
                return;
            }

            Check(rig.GetComponent<XROrigin>() != null, "the rig root carries XROrigin");
            Check(rig.GetComponent<XRInteractionManager>() != null, "the rig root carries XRInteractionManager");
            Check(rig.GetComponent<InputActionManager>() != null, "the rig root carries InputActionManager");
            Check(rig.GetComponent<XRInputModalityManager>() != null, "the rig root carries XRInputModalityManager");
            Check(rig.GetComponent<Room>() != null, "the rig root carries Room");
            Check(rig.GetComponent<Cosmic.Audio>() != null, "the rig root carries Audio");
            Check(rig.GetComponent<Hotkeys>() != null, "the rig root carries Hotkeys");

            Camera eye = null;
            var eyes = 0;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) { eyes++; eye = camera; }
            Check(eyes == 1, $"exactly one camera in the rig is tagged MainCamera ({eyes})");
            Check(eye != null && eye.GetComponent<TrackedPoseDriver>() != null, "the eye camera has a TrackedPoseDriver");
            Check(eye != null && eye.GetComponent<ARCameraManager>() != null, "the eye camera has an ARCameraManager");

            foreach (var side in new[] { "Left", "Right" })
            {
                var hand = Descendant(rig.transform, side + " Hand");
                Check(hand != null, "the rig has a " + side + " Hand");
                Check(hand != null && hand.GetComponentInChildren<NearFarInteractor>(true) != null, "the " + side + " Hand nests a NearFarInteractor");
                Check(hand != null && hand.GetComponentInChildren<XRPokeInteractor>(true) != null, "the " + side + " Hand nests an XRPokeInteractor");
            }

            Check(rig.GetComponentInChildren<Cosmic.Mouse>(true) != null, "the rig carries the desktop Mouse interactor");
            var systems = rig.GetComponentsInChildren<EventSystem>(true);
            Check(systems.Length == 1, $"exactly one EventSystem in the rig ({systems.Length})");
            Check(systems.Length == 1 && systems[0].GetComponent<XRUIInputModule>() != null, "the EventSystem carries an XRUIInputModule");

            Debug.Log($"{tag} rig inputs: Room.dimMaterial={Wired(rig.GetComponent<Room>(), "dimMaterial")}, " +
                      $"Audio.library={Wired(rig.GetComponent<Cosmic.Audio>(), "library")}. Both are made by Cosmic/Verify/P2 Setup; " +
                      "with dimMaterial null, Room logs its own error the moment play starts and Error Pause would stop the P3 run on it.");

            // Everything above reads the first build; re-importing the prefab invalidates `rig`, so nothing below may touch it.
            EditorApplication.ExecuteMenuItem("Cosmic/Build/Rig");
            var again = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Check(!string.IsNullOrEmpty(guid) && AssetDatabase.AssetPathToGUID(RigPath) == guid,
                  $"a second Cosmic/Build/Rig left the prefab GUID unchanged ({guid})");
            var rebuiltEyes = 0;
            if (again != null)
                foreach (var camera in again.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera")) rebuiltEyes++;
            Check(rebuiltEyes == 1, $"the second build did not duplicate the eye camera ({rebuiltEyes})");
            Check(again != null && again.GetComponentsInChildren<EventSystem>(true).Length == 1, "the second build did not duplicate the EventSystem");
            Debug.Log($"{tag} DONE {passes}/{total}");
        }

        [MenuItem("Cosmic/Verify/P3 Setup")]
        public static void P3Setup()
        {
            tag = "[P3]";
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(tag + " FAIL the editor is in play mode, so anything written now is thrown away when play stops");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError(tag + " FAIL the active scene has never been saved, and SaveScene would raise a modal dialog " +
                               "that blocks the relay. Open a saved scene in the editor by hand and run this again.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (asset == null)
            {
                Debug.LogError(tag + " FAIL " + RigPath + " does not exist; run Cosmic/Verify/P3 Build Rig first");
                return;
            }

            GameObject rig = null, sphere = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (rig == null && root.GetComponentInChildren<Cosmic.Mouse>(true) != null) rig = root;
                if (root.name == BodyName) sphere = root;
            }

            var madeRig = rig == null;
            if (madeRig)
            {
                rig = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            Camera eye = null;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) eye = camera;
            if (eye == null)
            {
                Debug.LogError(tag + " FAIL the rig in the scene has no camera tagged MainCamera; re-run Cosmic/Verify/P3 Build Rig");
                return;
            }

            var strays = string.Empty;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (camera != eye && camera.CompareTag("MainCamera")) strays += " " + camera.name;
            if (strays.Length > 0)
                Debug.LogError(tag + " FAIL more than one active MainCamera is loaded:" + strays + ". Camera.main is then whichever Unity " +
                               "hands back first, and Mouse, Pull and Room would all aim through the wrong one. Deactivate the host scene's " +
                               "camera, or open a saved scene that has none, and run this again.");

            var madeBody = sphere == null;
            if (madeBody)
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = BodyName;
            }

            var view = eye.transform;
            var ahead = view.position + view.forward * BodyDistanceMetres;
            sphere.transform.SetParent(null, true);
            sphere.transform.SetPositionAndRotation(new Vector3(ahead.x, BodyHeightMetres, ahead.z), Quaternion.identity);
            sphere.transform.localScale = Vector3.one * BodyWidthMetres;

            var hull = sphere.GetComponent<SphereCollider>();
            if (hull == null) hull = sphere.AddComponent<SphereCollider>();
            hull.isTrigger = false;
            var grab = sphere.GetComponent<Grabbable>();
            if (grab == null) grab = sphere.AddComponent<Grabbable>();
            var rigid = sphere.GetComponent<Rigidbody>();
            if (rigid != null) { rigid.isKinematic = true; rigid.useGravity = false; }
            if (sphere.GetComponent<XRGeneralGrabTransformer>() == null) sphere.AddComponent<XRGeneralGrabTransformer>();
            var pulling = sphere.GetComponent<Pull>();
            if (pulling == null) pulling = sphere.AddComponent<Pull>();

            var serialized = new SerializedObject(grab);
            var limits = Field(serialized, "limits");
            if (limits != null) limits.enumValueIndex = (int)Grabbable.Limits.Body;
            var returning = Field(serialized, "autoReturn");
            if (returning != null) returning.boolValue = true;
            var width = Field(serialized, "widthAtUnitScaleMetres");
            if (width != null) width.floatValue = 0f;
            var hulls = Field(serialized, "m_Colliders");
            if (hulls != null)
            {
                hulls.arraySize = 1;
                hulls.GetArrayElementAtIndex(0).objectReferenceValue = hull;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var beam = new SerializedObject(pulling);
            var material = Field(beam, "beamMaterial");
            if (material != null) material.objectReferenceValue = null;
            beam.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{tag} setup: rig {(madeRig ? "instantiated from " + RigPath : "found in the scene")}, {BodyName} {(madeBody ? "created" : "updated")} " +
                      $"at {sphere.transform.position} - {BodyWidthMetres} m across, {BodyDistanceMetres} m in front of {eye.name}, saved into {scene.path}. " +
                      $"Pull.beamMaterial is left null on purpose (the beam is optional and draws nothing without it). Grabbable.Bus is a static " +
                      $"and cannot be wired from the editor at all - Cosmic/Verify/P3 Run assigns it from the rig's Audio once play mode is up.");
        }

        [MenuItem("Cosmic/Verify/P3 Enter Play")]
        public static void P3EnterPlay()
        {
            if (EditorApplication.isPlaying) { Debug.Log("[P3] already in play mode"); return; }
            EditorApplication.isPlaying = true;
            Debug.Log("[P3] play requested; poll isPlaying, it reads false on this frame");
        }

        [MenuItem("Cosmic/Verify/P3 Leave Play")]
        public static void P3LeavePlay()
        {
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = false;
            Debug.Log("[P3] leaving play mode; the editor stays open");
        }

        [MenuItem("Cosmic/Verify/P3 Run")]
        public static void P3Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[P3] FAIL not in play mode; run Cosmic/Verify/P3 Enter Play, poll isPlaying, then run this");
                return;
            }
            if (steps.Count > 0) { Debug.Log($"{tag} already running, step {cursor}/{steps.Count}"); return; }

            var found = GameObject.Find(BodyName);
            body = found != null ? found.GetComponent<Grabbable>() : null;
            pull = found != null ? found.GetComponent<Pull>() : null;
            pointer = UnityEngine.Object.FindAnyObjectByType<Cosmic.Mouse>();
            keys = UnityEngine.Object.FindAnyObjectByType<Hotkeys>();
            if (body == null || pull == null)
            {
                Debug.LogError($"[P3] FAIL no active {BodyName} carrying both Grabbable and Pull; run Cosmic/Verify/P3 Setup, then re-enter play mode");
                return;
            }
            if (pointer == null)
            {
                Debug.LogError("[P3] FAIL no Cosmic.Mouse in the loaded scenes; the rig instance is missing, so there is no desktop pointer");
                return;
            }
            if (Camera.main == null)
            {
                Debug.LogError("[P3] FAIL there is no Camera.main; every screen point and every Pull goal is measured from it");
                return;
            }
            if (MouseDevice.current == null || KeyboardDevice.current == null)
            {
                Debug.LogError("[P3] FAIL the input system reports no mouse or no keyboard device, so no synthetic input can be sent");
                return;
            }

            tag = "[P3]";
            if (keys == null)
                Debug.LogError("[P3] FAIL no Hotkeys in the loaded scenes, so the R key does nothing and every Restore check will fail");
            Grabbable.Bus = UnityEngine.Object.FindAnyObjectByType<Cosmic.Audio>();
            cursor = passes = total = 0;
            roomFires = prefFires = 0;
            holding = null;
            BuildP3();
            due = EditorApplication.timeSinceStartup + steps[0].waitSeconds;
            EditorApplication.update += Tick;
            Debug.Log($"[P3] start: {steps.Count} steps, about 20 s, driven by synthetic mouse and keyboard events. " +
                      $"Grabbable.Bus = {(Grabbable.Bus != null ? Grabbable.Bus.name : "null - no Audio in the scene, so grab and pull are silent")}. " +
                      "Switch Error Pause off first, keep the Game view visible, and keep the real mouse off the Game view until it prints DONE.");
        }

        [MenuItem("Cosmic/Verify/P3 Teardown")]
        public static void P3Teardown()
        {
            tag = "[P3]";
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(tag + " FAIL the editor is in play mode, so the save would be thrown away; leave play mode first");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError(tag + " FAIL the active scene has never been saved; SaveScene would block the relay on a modal dialog");
                return;
            }

            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != BodyName && root.GetComponentInChildren<Cosmic.Mouse>(true) == null) continue;
                UnityEngine.Object.DestroyImmediate(root);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{tag} teardown: removed {removed} object(s) from {scene.path}. {RigPath} is kept - it is the rig Phase 6 builds the one scene around.");
        }

        static void BuildP3()
        {
            steps.Clear();
            var t = body.transform;
            var view = Camera.main.transform;
            var point = Vector2.zero;
            var mark = Vector3.zero;
            var spun = Quaternion.identity;
            var startWidth = 0f;
            var pressed = 0;
            var restored = false;
            var empty = false;
            Transform pivot = null;
            var pivotWas = Quaternion.identity;

            Add(0f, () =>
            {
                body.CaptureHome();
                home = t.localPosition;
                probe = body.GetComponent<VerifyProbe>();
                if (probe == null) probe = body.gameObject.AddComponent<VerifyProbe>();
                probe.grab = body;
                probe.pull = pull;
                probe.Arm();
                roomListener = _ => roomFires++;
                Room.Changed += roomListener;
                prefListener = () => prefFires++;
                Prefs.Changed += prefListener;
                if (keys != null)
                {
                    restoreListener = () => { restored = true; body.Restore(); };
                    keys.Restore += restoreListener;
                }
                // Clears whatever the real keyboard holds: a physical shift turns every wheel notch into a push instead of a scale.
                Tap(Key.R, false);
                point = At(t.position);
                holding = () => Move(point);
            });
            Add(0.35f, () =>
            {
                Check(body.isHovered, "the cursor on the body makes the Grabbable hovered");
                Check(pull.Current == Pull.State.Parked, "Pull stays Parked while the dwell runs");
                Check(pull.Beam > 0.01f && pull.Beam < 1f, $"the dwell beam is rising ({pull.Beam:0.00})");
            });
            Add(2.3f, () => Check(probe.sawPulling, "holding the far hover past the 2 s dwell starts the pull"));
            Add(1.5f, () =>
            {
                holding = null;
                Check(pull.Current == Pull.State.Loose, $"the pull ends in Loose ({pull.Current})");
                Check(Mathf.Abs(body.WidthMetres - PulledWidthMetres) <= 0.02f,
                      $"the pulled body measures {PulledWidthMetres} m across ({body.WidthMetres:0.000})");
                var attach = pointer.attachTransform;
                var gap = attach != null ? Vector3.Distance(t.position, attach.position) : -1f;
                Check(gap >= 0f && gap <= 0.1f + body.WidthMetres * 0.5f,
                      $"the pulled body stops at the mouse attach 1 m out, within 0.1 m of its own {body.WidthMetres * 0.5f:0.000} m standoff ({gap:0.000} m)");
                Check(!body.Placed, "Placed is still false: a pull is not a placement");
            });
            Add(0f, () =>
            {
                point = At(t.position);
                probe.Arm();
                pressed = Time.frameCount;
                Press(point, true, false, Vector2.zero);
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.2f, () =>
            {
                Check(body.isSelected, "LMB over the body selects it");
                Check(probe.selectedFrame > 0 && probe.selectedFrame - pressed <= 2,
                      $"the select lands within 2 frames of the press ({probe.selectedFrame - pressed})");
                mark = t.position;
                point += new Vector2(DragPixels, 0f);
                Press(point, true, false, new Vector2(DragPixels, 0f));
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.25f, () =>
            {
                Check(Vector3.Distance(mark, t.position) > 0.05f,
                      $"dragging the cursor {DragPixels} px right carries the held body with it ({Vector3.Distance(mark, t.position):0.000} m)");
                holding = null;
                Press(point, false, false, Vector2.zero);
            });
            Add(0.25f, () =>
            {
                Check(!body.isSelected, "releasing LMB deselects the body");
                Check(body.Placed, "a release away from home marks the body Placed");
            });
            Add(0f, () =>
            {
                point = At(t.position);
                holding = () => Move(point);
                startWidth = body.WidthMetres;
            });
            Add(0.25f, () => Scroll(point, 3f));
            Add(0.25f, () =>
            {
                var want = startWidth * Mathf.Pow(WheelScalePerNotch, 3f);
                Check(Mathf.Abs(body.WidthMetres - want) <= want * 0.05f,
                      $"three wheel notches scale the body by {WheelScalePerNotch}^3 ({body.WidthMetres:0.000} m, wanted {want:0.000})");
                Check(body.WidthMetres <= body.MaxMetres + 0.001f, $"the wheel never takes the body past {body.MaxMetres} m");
                Scroll(point, 60f);
            });
            Add(0.3f, () =>
            {
                Check(Mathf.Abs(body.WidthMetres - body.MaxMetres) <= 0.02f, $"60 notches clamp at {body.MaxMetres} m ({body.WidthMetres:0.000})");
                // At 3 m across and 1.1 m out the camera is inside the sphere, where the ray has no front face to hit and the hover is gone: shrink it back in code.
                body.ScaleBy(PulledWidthMetres / Mathf.Max(0.0001f, body.WidthMetres));
                point = At(t.position);
            });
            Add(0.35f, () =>
            {
                Check(body.isHovered, "the body is hovered again once it is back to arm's length size");
                Scroll(point, -100f);
            });
            Add(0.3f, () =>
            {
                Check(Mathf.Abs(body.WidthMetres - body.MinMetres) <= 0.005f, $"-100 notches clamp at {body.MinMetres} m ({body.WidthMetres:0.000})");
                body.ScaleBy(PulledWidthMetres / Mathf.Max(0.0001f, body.WidthMetres));
            });
            Add(0.2f, () =>
            {
                Check(Mathf.Abs(body.WidthMetres - PulledWidthMetres) <= 0.01f,
                      $"the body is back at {PulledWidthMetres} m across for the rest of the run ({body.WidthMetres:0.000})");
                spun = t.rotation;
                Press(point, false, true, Vector2.zero);
                holding = () => Press(point, false, true, Vector2.zero);
            });
            Add(0.15f, () => Press(point, false, true, new Vector2(DragPixels, 0f)));
            Add(0.2f, () =>
            {
                holding = null;
                Press(point, false, false, Vector2.zero);
                Check(Quaternion.Angle(spun, t.rotation) > 1f,
                      $"a right-drag over the body spins it ({Quaternion.Angle(spun, t.rotation):0.0} degrees)");
            });
            Add(0.1f, () =>
            {
                point = Idle();
                holding = () => Move(point);
                t.position = view.position + view.right * StrayMetres;
            });
            Add(6.5f, () =>
            {
                Check(Vector3.Distance(t.localPosition, home) <= 0.01f,
                      $"a placed body left {StrayMetres} m out of reach comes home by itself ({Vector3.Distance(t.localPosition, home):0.000} m off)");
                Check(!body.Placed, "the automatic return clears Placed");
                Check(pull.Current == Pull.State.Parked, $"the automatic return parks Pull ({pull.Current})");
            });
            Add(0f, () =>
            {
                t.position = home + view.right * RestoreOffsetMetres;
                point = At(t.position);
                holding = () => Move(point);
                restored = false;
            });
            Add(0.45f, () =>
            {
                Check(pull.Beam > 0.01f, $"a far hover on the moved body starts a fresh dwell ({pull.Beam:0.00})");
                Tap(Key.R, true);
            });
            Add(0.1f, () => Tap(Key.R, false));
            Add(1.2f, () =>
            {
                Check(restored, "the R key raises Hotkeys.Restore");
                Check(Vector3.Distance(t.localPosition, home) <= 0.01f,
                      $"Restore tweens the body home ({Vector3.Distance(t.localPosition, home):0.000} m off)");
                Check(pull.Current == Pull.State.Parked, $"Restore parks Pull ({pull.Current})");
                Check(pull.Beam <= 0.001f, $"Restore cancels the dwell that was running ({pull.Beam:0.00})");
            });
            Add(0f, () =>
            {
                point = Idle();
                holding = () => Move(point);
            });
            Add(0.3f, () =>
            {
                empty = !pointer.hasHover;
                var serialized = new SerializedObject(pointer);
                var property = serialized.FindProperty("pivot");
                pivot = property != null ? property.objectReferenceValue as Transform : null;
                if (pivot != null) pivotWas = pivot.localRotation;
                if (!empty) { Skip($"the empty-space press: something in this scene is already under {point}"); return; }
                Press(point, true, false, Vector2.zero);
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.2f, () =>
            {
                if (!empty) return;
                Check(!pointer.hasSelection && !body.isSelected, "LMB on empty space selects nothing");
                point += new Vector2(DragPixels, 0f);
                Press(point, true, false, new Vector2(DragPixels, 0f));
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.25f, () =>
            {
                holding = null;
                Press(point, false, false, Vector2.zero);
                if (!empty) return;
                if (pivot == null) Skip("the orbit: Mouse.pivot is unassigned in the rig, so an empty-space drag has nothing to turn");
                else Check(Quaternion.Angle(pivotWas, pivot.localRotation) > 1f,
                           $"an empty-space drag orbits the pivot ({Quaternion.Angle(pivotWas, pivot.localRotation):0.0} degrees)");
            });
            Add(0.2f, () =>
            {
                Check(roomFires == 0, $"Room.Changed never fired: Phase 3 does not touch the room ({roomFires})");
                Check(prefFires == 0, $"Prefs.Changed never fired: Phase 3 does not touch the preferences ({prefFires})");
            });

            cleanup = () =>
            {
                holding = null;
                if (roomListener != null) { Room.Changed -= roomListener; roomListener = null; }
                if (prefListener != null) { Prefs.Changed -= prefListener; prefListener = null; }
                if (keys != null && restoreListener != null) keys.Restore -= restoreListener;
                restoreListener = null;
                if (probe != null) UnityEngine.Object.Destroy(probe);
                probe = null;
                Tap(Key.R, false);
                var device = MouseDevice.current;
                if (device != null) Move(device.position.ReadValue());
            };
        }

        static Vector2 At(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            var screen = cam.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        static Vector2 Idle()
        {
            var cam = Camera.main;
            return cam != null ? new Vector2(cam.pixelWidth * 0.06f, cam.pixelHeight * 0.94f) : new Vector2(40f, 40f);
        }

        static void Move(Vector2 at) => Send(at, false, false, Vector2.zero, Vector2.zero);

        static void Press(Vector2 at, bool left, bool right, Vector2 delta) => Send(at, left, right, delta, Vector2.zero);

        static void Scroll(Vector2 at, float notches) => Send(at, false, false, Vector2.zero, new Vector2(0f, notches * RawWheelNotch));

        static void Send(Vector2 at, bool left, bool right, Vector2 delta, Vector2 scroll)
        {
            var device = MouseDevice.current;
            if (device == null) return;
            var state = new MouseState { position = at, delta = delta, scroll = scroll };
            InputSystem.QueueStateEvent(device, state.WithButton(MouseButton.Left, left).WithButton(MouseButton.Right, right));
        }

        static void Tap(Key key, bool down)
        {
            var device = KeyboardDevice.current;
            if (device == null) return;
            InputSystem.QueueStateEvent(device, down ? new KeyboardState(key) : new KeyboardState());
        }

        static Transform Descendant(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        static string Wired(UnityEngine.Object owner, string field)
        {
            if (owner == null) return "no component";
            var property = new SerializedObject(owner).FindProperty(field);
            if (property == null) return $"no serialized field '{field}'";
            return property.objectReferenceValue != null ? property.objectReferenceValue.name : "null";
        }

        static SerializedProperty Field(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null) Debug.LogError($"{tag} FAIL {serialized.targetObject.GetType().Name} has no serialized field '{field}'");
            return property;
        }

        [MenuItem("Cosmic/Verify/P4 Bake")]
        public static void P4Bake()
        {
            tag = "[P4]";
            if (!Ready("Bake All")) return;
            passes = total = 0;

            if (!EditorApplication.ExecuteMenuItem("Cosmic/Build/Bake All"))
            {
                Debug.LogError(tag + " FAIL the menu item Cosmic/Build/Bake All does not exist; Cosmic.Editor did not compile");
                return;
            }

            var before = Baked();
            Check(Found("t:Galaxy", GalaxyFolder).Length == GalaxyCounts.Length,
                  $"{GalaxyCounts.Length} Galaxy assets in {GalaxyFolder} ({Found("t:Galaxy", GalaxyFolder).Length})");

            var strode = false;
            foreach (var expected in GalaxyCounts)
            {
                var path = $"{GalaxyFolder}/{expected.id}.asset";
                var galaxy = AssetDatabase.LoadAssetAtPath<Galaxy>(path);
                Check(galaxy != null, path + " exists");
                if (galaxy == null) continue;

                var layers = galaxy.layers;
                var count = layers == null ? 0 : layers.Length;
                Check(count == 3, $"{expected.id} has three layers ({count})");
                if (count != 3) continue;

                var want = new[] { expected.clouds, expected.dust, expected.stars };
                for (var i = 0; i < 3; i++)
                {
                    var cloud = layers[i].points;
                    var name = string.IsNullOrEmpty(layers[i].id) ? "layer " + i : layers[i].id;
                    Check(cloud != null && cloud.Count == want[i],
                          $"{expected.id}/{name} is a baked cloud of {want[i]} points " +
                          $"({(cloud == null ? "layers[" + i + "].points is null" : cloud.Count.ToString())})");
                    if (cloud == null || strode) continue;
                    strode = true;
                    Check(cloud.Stride == PointStrideBytes,
                          $"PointCloud.Stride is {PointStrideBytes} bytes, the ten floats of StarVert ({cloud.Stride})");
                }
            }

            var web = AssetDatabase.LoadAssetAtPath<PointCloud>($"{CloudFolder}/cosmic_web.asset");
            Check(web != null && web.Count == WebPoints,
                  $"cosmic_web.asset holds {WebPoints} points ({(web == null ? "missing" : web.Count.ToString())})");
            if (web != null)
                Debug.Log($"{tag} cosmic_web radius is {web.radiusMetres} m - that is PointSources.WebSettings.Default.radiusMetres, " +
                          "not the 0.35 m of a nebula, and the place prefab's own grab sphere is a separate number.");

            foreach (var id in NebulaIds)
            {
                var cloud = AssetDatabase.LoadAssetAtPath<PointCloud>($"{CloudFolder}/nebula_{id}.asset");
                Check(cloud != null && cloud.Count == NebulaPoints && Mathf.Abs(cloud.radiusMetres - NebulaRadiusMetres) <= 0.001f,
                      $"nebula_{id}.asset is {NebulaPoints} points at {NebulaRadiusMetres} m " +
                      $"({(cloud == null ? "missing" : cloud.Count + " points at " + cloud.radiusMetres + " m")})");
            }

            int mats = 0, instanced = 0, offShader = 0;
            var faults = string.Empty;
            foreach (var guid in Found("t:Material", MaterialFolder))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).StartsWith("points_")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                mats++;
                if (material.enableInstancing) { instanced++; faults += " " + material.name + "(instanced)"; }
                if (material.shader == null || material.shader.name != PointShaderName)
                {
                    offShader++;
                    faults += " " + material.name + "(" + (material.shader == null ? "no shader" : material.shader.name) + ")";
                }
            }

            Check(mats == PointMaterials, $"{PointMaterials} points_*.mat in {MaterialFolder} - 15 galaxy layers, the web and seven nebulae ({mats})");
            Check(mats > 0 && instanced == 0, $"every points_*.mat has instancing off ({instanced} on)");
            Check(mats > 0 && offShader == 0, $"every points_*.mat is on {PointShaderName} ({offShader} are not)");
            if (faults.Length > 0) Debug.Log(tag + " point material faults:" + faults);

            // Everything above reads the first bake; the second one is the idempotency gate and nothing may be read across it but paths and GUIDs.
            EditorApplication.ExecuteMenuItem("Cosmic/Build/Bake All");
            var after = Baked();
            int churned = 0, added = 0;
            foreach (var pair in before)
                if (!after.TryGetValue(pair.Key, out var guid) || guid != pair.Value) churned++;
            foreach (var pair in after)
                if (!before.ContainsKey(pair.Key)) added++;
            Check(before.Count > 0 && churned == 0, $"a second Bake All left all {before.Count} baked GUIDs unchanged ({churned} churned)");
            Check(added == 0, $"a second Bake All wrote no new asset under {GalaxyFolder} or {CloudFolder} ({added} new)");
            Debug.Log($"{tag} DONE {passes}/{total}");
        }

        [MenuItem("Cosmic/Verify/P4 Build")]
        public static void P4Build()
        {
            tag = "[P4]";
            if (!Ready("the Phase 4 builders")) return;
            passes = total = 0;

            foreach (var item in new[] { "Cosmic/Build/Layouts", "Cosmic/Build/Bodies", "Cosmic/Build/Places" })
            {
                if (EditorApplication.ExecuteMenuItem(item)) continue;
                Debug.LogError($"{tag} FAIL the menu item {item} does not exist; Cosmic.Editor did not compile, or that " +
                               "builder has not landed yet. Nothing below it would mean anything, so this stops here.");
                return;
            }

            var before = Built();
            foreach (var wanted in Arrangements)
            {
                var layout = AssetDatabase.LoadAssetAtPath<Layout>($"{LayoutFolder}/{wanted.id}.asset");
                var slots = layout == null || layout.slots == null ? 0 : layout.slots.Length;
                Check(layout != null && layout.kind == wanted.kind && slots == SolarIds.Length,
                      $"{wanted.id}.asset is a {wanted.kind} layout over all {SolarIds.Length} solar bodies " +
                      $"({(layout == null ? "missing" : layout.kind + ", " + slots + " slots")})");
            }

            var missing = string.Empty;
            foreach (var id in PlaceIds)
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/{id}.prefab") == null) missing += " " + id;
            Check(missing.Length == 0,
                  $"a prefab in {PlacePrefabFolder} for each of the {PlaceIds.Length} places that have content " +
                  (missing.Length == 0 ? "(all present)" : "(missing:" + missing + ")"));

            var milky = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/milky_way.prefab");
            var disc = milky != null ? Descendant(milky.transform, "galaxy") : null;
            var points = disc != null ? disc.GetComponent<Points>() : null;
            var layers = points == null || points.layers == null ? 0 : points.layers.Length;
            Check(points != null && layers == 3,
                  $"milky_way.prefab has a `galaxy` child carrying Points with three layers " +
                  $"({(milky == null ? "no prefab" : disc == null ? "no `galaxy` child" : points == null ? "no Points" : layers + " layers")})");
            if (points != null)
            {
                var unwired = 0;
                for (var i = 0; i < layers; i++)
                    if (points.layers[i].cloud == null || points.layers[i].material == null) unwired++;
                Check(layers > 0 && unwired == 0, $"every milky_way Points layer carries a cloud and a material ({unwired} do not)");
            }

            var helix = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/helix.prefab");
            var volume = helix != null ? Descendant(helix.transform, "volume") : null;
            var nebula = volume != null ? volume.GetComponent<Points>() : null;
            var nebulaLayers = nebula == null || nebula.layers == null ? 0 : nebula.layers.Length;
            Check(nebula != null && nebulaLayers == 1,
                  $"helix.prefab has a `volume` child carrying Points with one layer " +
                  $"({(helix == null ? "no prefab" : volume == null ? "no `volume` child" : nebula == null ? "no Points" : nebulaLayers + " layers")})");

            var system = SystemPrefab(out var systemId);
            var rig = system != null ? system.GetComponent<Rig>() : null;
            Check(rig != null, $"{systemId}.prefab carries Rig on its root ({(system == null ? "no prefab" : rig == null ? "no Rig" : "present")})");
            Check(system != null && system.GetComponent<Orbit>() != null, $"{systemId}.prefab carries Orbit on its root");
            if (systemId != "solar_system_planets")
                Debug.Log($"{tag} the rig was found on {systemId}.prefab, not solar_system_planets.prefab. The generated data " +
                          "splits the system in two: solar_system.asset holds the ten bodies and solar_system_planets.asset holds " +
                          "the four layouts. Whichever place the builder chose, P4 Setup and P4 Run follow it.");

            var frame = rig != null ? rig.transform : system != null ? system.transform : null;
            var anchorFaults = string.Empty;
            Transform saturn = null, sunBody = null;
            foreach (var id in SolarIds)
            {
                var anchor = frame != null ? frame.Find("home_" + id) : null;
                if (anchor == null) { anchorFaults += " " + id + "(no anchor)"; continue; }
                var nested = anchor.Find("body_" + id);
                if (nested == null) { anchorFaults += " " + id + "(no body_" + id + ")"; continue; }
                if (id == "saturn") saturn = nested;
                if (id == "sun") sunBody = nested;
                var lacks = string.Empty;
                if (nested.GetComponent<Grabbable>() == null) lacks += "Grabbable ";
                if (nested.GetComponent<Pull>() == null) lacks += "Pull ";
                if (nested.GetComponent<SphereCollider>() == null) lacks += "SphereCollider ";
                if (lacks.Length > 0) anchorFaults += " " + id + "(no " + lacks.TrimEnd() + ")";
            }

            Check(anchorFaults.Length == 0,
                  $"all {SolarIds.Length} home_<id> anchors nest a body_<id> with Grabbable, Pull and a SphereCollider " +
                  (anchorFaults.Length == 0 ? "(all ten)" : "(faults:" + anchorFaults + ")"));
            Check(saturn != null && saturn.Find("rings") != null, "body_saturn has a `rings` child");
            Check(sunBody != null && sunBody.GetComponent<Sun>() != null, "body_sun carries Sun");

            var sunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BodyPrefabFolder}/sun.prefab");
            Check(sunPrefab != null && sunPrefab.GetComponent<Sun>() != null, $"{BodyPrefabFolder}/sun.prefab carries Sun");
            var sunAsset = AssetDatabase.LoadAssetAtPath<Body>($"{Generated}/bodies/sun.asset");
            Check(sunAsset != null && sunAsset.kind == BodyKind.Star,
                  $"the sun Body is BodyKind.Star ({(sunAsset == null ? "missing" : sunAsset.kind.ToString())}) - Rig.BindOrbit binds the " +
                  "first Star anchor as Orbit's centre, and with the sun typed Planet it is handed to Orbit.Bind instead, " +
                  "which has no elements for it and leaves it wherever the last row layout put it");

            // Everything above reads the first build; a rebuild re-imports the prefabs and invalidates every reference held here.
            foreach (var item in new[] { "Cosmic/Build/Layouts", "Cosmic/Build/Bodies", "Cosmic/Build/Places" })
                EditorApplication.ExecuteMenuItem(item);
            var after = Built();
            int churned = 0, added = 0;
            foreach (var pair in before)
                if (!after.TryGetValue(pair.Key, out var guid) || guid != pair.Value) churned++;
            foreach (var pair in after)
                if (!before.ContainsKey(pair.Key)) added++;
            Check(before.Count > 0 && churned == 0, $"a second build left all {before.Count} prefab and layout GUIDs unchanged ({churned} churned)");
            Check(added == 0, $"a second build wrote no new prefab or layout ({added} new)");
            Debug.Log($"{tag} DONE {passes}/{total}");
        }

        [MenuItem("Cosmic/Verify/P4 Setup")]
        public static void P4Setup()
        {
            tag = "[P4]";
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(tag + " FAIL the editor is in play mode, so anything written now is thrown away when play stops");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError(tag + " FAIL the active scene has never been saved, and SaveScene would raise a modal dialog " +
                               "that blocks the relay. Open a saved scene in the editor by hand and run this again.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (asset == null)
            {
                Debug.LogError(tag + " FAIL " + RigPath + " does not exist; run Cosmic/Verify/P3 Build Rig first");
                return;
            }

            GameObject rig = null, host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (rig == null && root.GetComponentInChildren<Cosmic.Mouse>(true) != null) rig = root;
                if (root.name == HostName) host = root;
            }

            var madeRig = rig == null;
            if (madeRig)
            {
                rig = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            Camera eye = null;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) eye = camera;
            if (eye == null)
            {
                Debug.LogError(tag + " FAIL the rig in the scene has no camera tagged MainCamera; re-run Cosmic/Verify/P3 Build Rig");
                return;
            }

            var strays = string.Empty;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (camera != eye && camera.CompareTag("MainCamera")) strays += " " + camera.name;
            if (strays.Length > 0)
                Debug.LogError(tag + " FAIL more than one active MainCamera is loaded:" + strays + ". Camera.main is then whichever Unity " +
                               "hands back first, and Mouse, Points and Sun would all aim through the wrong one. Deactivate the host scene's " +
                               "camera, or open a saved scene that has none, and run this again.");

            var madeHost = host == null;
            if (madeHost) host = new GameObject(HostName);
            if (host.GetComponent<Room>() != null || host.GetComponent<Cosmic.Audio>() != null)
            {
                Debug.LogError(tag + " FAIL " + HostName + " in this scene is the Phase 2 host: it carries Room and Audio, and a second " +
                               "Room next to the rig's is a coin toss over which one is the singleton. Run Cosmic/Verify/P2 Teardown " +
                               "first, then run this again.");
                return;
            }

            host.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var view = eye.transform;
            var galaxy = Spawn(host.transform, "milky_way");
            var system = SystemPrefab(out var systemId);
            var placed = system != null ? Spawn(host.transform, systemId) : null;
            if (galaxy == null || placed == null)
            {
                Debug.LogError($"{tag} FAIL no prefab at {PlacePrefabFolder}/milky_way.prefab or {PlacePrefabFolder}/{systemId}.prefab; " +
                               "run Cosmic/Verify/P4 Build first");
                return;
            }

            var ahead = view.position + view.forward * GalaxyAheadMetres;
            galaxy.transform.SetPositionAndRotation(new Vector3(ahead.x, view.position.y, ahead.z), Quaternion.identity);
            // Floor frame, not eye height: the row and relative layouts carry an absolute 1.2 m slot height of their own.
            var floorAhead = view.position + view.forward * SystemAheadMetres;
            placed.transform.SetPositionAndRotation(new Vector3(floorAhead.x, 0f, floorAhead.z), Quaternion.identity);

            var sun = placed.GetComponentInChildren<Sun>(true);
            var mouse = rig.GetComponentInChildren<Cosmic.Mouse>(true);
            if (sun != null && mouse != null) Bind(sun, "mouse", mouse);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{tag} setup: rig {(madeRig ? "instantiated from " + RigPath : "found in the scene")}, {HostName} " +
                      $"{(madeHost ? "created" : "reused")} with milky_way at {galaxy.transform.position} ({GalaxyAheadMetres} m ahead at eye " +
                      $"height) and {systemId} at {placed.transform.position} ({SystemAheadMetres} m ahead on the floor), saved into {scene.path}. " +
                      $"Sun.mouse = {(sun == null ? "no Sun in the place" : mouse == null ? "no Mouse in the rig" : mouse.name)}: the harness wires " +
                      "it here because Phase 6 is what wires it for real, and without it Sun.Hovering can never return true and the desktop " +
                      "touch test could only ever be skipped. Sun.leftHand and Sun.rightHand stay null on purpose - Sun.Touching only falls " +
                      "through to the mouse while neither hand has moved, and a null hand never moves.");
        }

        [MenuItem("Cosmic/Verify/P4 Enter Play")]
        public static void P4EnterPlay()
        {
            if (EditorApplication.isPlaying) { Debug.Log("[P4] already in play mode"); return; }
            EditorApplication.isPlaying = true;
            Debug.Log("[P4] play requested; poll isPlaying, it reads false on this frame");
        }

        [MenuItem("Cosmic/Verify/P4 Leave Play")]
        public static void P4LeavePlay()
        {
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = false;
            Debug.Log("[P4] leaving play mode; the editor stays open");
        }

        [MenuItem("Cosmic/Verify/P4 Run")]
        public static void P4Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[P4] FAIL not in play mode; run Cosmic/Verify/P4 Enter Play, poll isPlaying, then run this");
                return;
            }
            if (steps.Count > 0) { Debug.Log($"{tag} already running, step {cursor}/{steps.Count}"); return; }

            tag = "[P4]";
            galaxyPoints = null;
            galaxyGrab = null;
            galaxyHull = null;
            systemRig = null;
            systemOrbit = null;
            star = null;
            sunAnchor = null;

            var host = GameObject.Find(HostName);
            if (host == null)
            {
                Debug.LogError($"[P4] FAIL no active {HostName} in the loaded scenes; run Cosmic/Verify/P4 Setup, then re-enter play mode");
                return;
            }

            var disc = host.transform.Find("milky_way");
            galaxyGrab = disc != null ? disc.GetComponent<Grabbable>() : null;
            galaxyHull = disc != null ? disc.GetComponent<Collider>() : null;
            galaxyPoints = disc != null ? disc.GetComponentInChildren<Points>(true) : null;
            systemRig = host.GetComponentInChildren<Rig>(true);
            systemOrbit = systemRig != null ? systemRig.GetComponent<Orbit>() : null;
            star = host.GetComponentInChildren<Sun>(true);
            pointer = UnityEngine.Object.FindAnyObjectByType<Cosmic.Mouse>();
            keys = UnityEngine.Object.FindAnyObjectByType<Hotkeys>();

            if (galaxyPoints == null)
            {
                Debug.LogError($"[P4] FAIL no Points under {HostName}/milky_way; the place prefab was not built or not instantiated");
                return;
            }
            if (systemRig == null)
            {
                Debug.LogError($"[P4] FAIL no Rig under {HostName}; the system place prefab was not built or not instantiated");
                return;
            }
            if (Camera.main == null)
            {
                Debug.LogError("[P4] FAIL there is no Camera.main; every command buffer, screen point and Sun hover is measured from it");
                return;
            }
            if (pointer == null)
            {
                Debug.LogError("[P4] FAIL no Cosmic.Mouse in the loaded scenes; the rig instance is missing, so there is no desktop pointer");
                return;
            }
            if (MouseDevice.current == null || KeyboardDevice.current == null)
            {
                Debug.LogError("[P4] FAIL the input system reports no mouse or no keyboard device, so no synthetic input can be sent");
                return;
            }

            arrangementAssets = new Layout[Arrangements.Length];
            var absent = string.Empty;
            for (var i = 0; i < Arrangements.Length; i++)
            {
                arrangementAssets[i] = AssetDatabase.LoadAssetAtPath<Layout>($"{LayoutFolder}/{Arrangements[i].id}.asset");
                if (arrangementAssets[i] == null) absent += " " + Arrangements[i].id;
            }
            if (absent.Length > 0)
            {
                Debug.LogError($"[P4] FAIL these layout assets are missing:{absent}. Run Cosmic/Verify/P4 Build first.");
                return;
            }

            if (keys == null)
                Debug.LogError("[P4] FAIL no Hotkeys in the loaded scenes, so the R key does nothing and the Restore check will fail");
            Grabbable.Bus = UnityEngine.Object.FindAnyObjectByType<Cosmic.Audio>();
            cursor = passes = total = 0;
            roomFires = 0;
            holding = null;
            BuildP4();
            due = EditorApplication.timeSinceStartup + steps[0].waitSeconds;
            EditorApplication.update += Tick;
            Debug.Log($"[P4] start: {steps.Count} steps, about 16 s, driven by layout applies and synthetic mouse and keyboard events. " +
                      $"Grabbable.Bus = {(Grabbable.Bus != null ? Grabbable.Bus.name : "null - no Audio in the scene, so grab and pull are silent")}. " +
                      "Switch Error Pause off first, keep the Game view visible, and keep the real mouse off it until it prints DONE.");
        }

        [MenuItem("Cosmic/Verify/P4 Teardown")]
        public static void P4Teardown()
        {
            tag = "[P4]";
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(tag + " FAIL the editor is in play mode, so the save would be thrown away; leave play mode first");
                return;
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError(tag + " FAIL the active scene has never been saved; SaveScene would block the relay on a modal dialog");
                return;
            }

            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != HostName) continue;
                removed++;
                UnityEngine.Object.DestroyImmediate(root);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"{tag} teardown: removed {removed} {HostName} root(s) from {scene.path}. The place and body prefabs, the baked " +
                      $"clouds and {RigPath} are all kept - none of them is a fixture.");
        }

        static void BuildP4()
        {
            steps.Clear();
            var view = Camera.main.transform;
            var frame = systemRig.transform;
            var anchors = new Transform[SolarIds.Length];
            var starts = new Vector3[SolarIds.Length];
            var point = Vector2.zero;
            var mark = Vector3.zero;
            var mercury = Vector3.zero;
            var restored = false;
            var hovering = false;

            Add(0f, () =>
            {
                roomListener = _ => roomFires++;
                Room.Changed += roomListener;
                if (keys != null)
                {
                    restoreListener = () => { restored = true; if (galaxyGrab != null) galaxyGrab.Restore(); };
                    keys.Restore += restoreListener;
                }
                // Clears whatever the real keyboard holds, exactly as P3 does before its first synthetic event.
                Tap(Key.R, false);
                var absent = string.Empty;
                for (var i = 0; i < SolarIds.Length; i++)
                {
                    anchors[i] = frame.Find("home_" + SolarIds[i]);
                    if (anchors[i] == null) absent += " " + SolarIds[i];
                    if (SolarIds[i] == "sun") sunAnchor = anchors[i];
                }
                Check(absent.Length == 0, $"the rig holds all {SolarIds.Length} home_<id> anchors" + (absent.Length == 0 ? string.Empty : ", missing:" + absent));
                // Counted by id, not by length: the builder also hands the rig a moon anchor per moon, which belongs there.
                var listed = 0;
                if (systemRig.Bodies != null)
                    foreach (var listedBody in systemRig.Bodies)
                        for (var i = 0; listedBody != null && i < SolarIds.Length; i++)
                            if (listedBody.id == SolarIds[i]) listed++;
                Check(listed == SolarIds.Length,
                      $"Rig.bodies lists all {SolarIds.Length} solar bodies ({listed} of " +
                      $"{(systemRig.Bodies == null ? 0 : systemRig.Bodies.Count)} entries)");
                // Primes the anchors onto the row arc so the two asserted applies below both start from a known radius;
                // the authored anchor positions in the prefab are the builder's business and could be anywhere.
                systemRig.Apply(arrangementAssets[1], 0f);
            });
            Add(0.6f, () =>
            {
                var cam = Camera.main;
                var opaque = cam != null ? cam.GetCommandBuffers(CameraEvent.BeforeForwardOpaque).Length : 0;
                Check(opaque > 0, $"the galaxy Points recorded a command buffer at BeforeForwardOpaque on Camera.main ({opaque})");
                Check(galaxyPoints.LocalCamDir.sqrMagnitude > 0.0001f,
                      $"Points pushes a per-frame _LocalCamDir ({galaxyPoints.LocalCamDir})");
                Skip("the _Age advance: Points keeps its per-layer Material instances private, so there is no way to read _Age " +
                     "from outside. A `public Material Instance(int layer)` on Points would turn this into an assertion; until " +
                     "then the spin is a thing to look at, not to measure.");
                Skip("the Alpha 0 draw: the command buffer still records the same DrawProcedural at any alpha - only the shader " +
                     "discards - so nothing observable from script distinguishes alpha 0 from alpha 1. It is a capture, not a check.");
                for (var i = 0; i < anchors.Length; i++) starts[i] = anchors[i] != null ? anchors[i].localPosition : Vector3.zero;
                systemRig.Apply(arrangementAssets[0]);
            });
            Add(1.5f, () => Settled(Arrangements[0].id, arrangementAssets[0], anchors, starts));
            Add(0f, () =>
            {
                for (var i = 0; i < anchors.Length; i++) starts[i] = anchors[i] != null ? anchors[i].localPosition : Vector3.zero;
                systemRig.Apply(arrangementAssets[1]);
            });
            Add(1.5f, () => Settled(Arrangements[1].id, arrangementAssets[1], anchors, starts));
            Add(0f, () => systemRig.Apply(arrangementAssets[2]));
            Add(0.3f, () =>
            {
                Check(systemOrbit != null && systemOrbit.Running, "a Schematic layout sets Orbit.Running");
                Check(systemOrbit != null && systemOrbit.Count == SolarIds.Length - 1,
                      $"Orbit drives the nine bodies that are not the centre ({(systemOrbit == null ? 0 : systemOrbit.Count)})");
                Check(systemOrbit != null && systemOrbit.Realism <= 0.001f,
                      $"a Schematic layout holds Orbit.Realism at 0 ({(systemOrbit == null ? -1f : systemOrbit.Realism):0.000})");
                mercury = anchors[1] != null ? anchors[1].localPosition : Vector3.zero;
            });
            Add(2.5f, () =>
            {
                var moved = anchors[1] != null ? Vector3.Distance(mercury, anchors[1].localPosition) : 0f;
                Check(moved > OrbitMovedMetres, $"mercury's anchor travels its orbit while Running ({moved:0.000} m in 2.5 s)");
                var cam = Camera.main;
                var alpha = cam != null ? cam.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length : 0;
                Check(alpha > 0, $"the orbit rings recorded a command buffer at AfterForwardAlpha ({alpha}) - zero means Orbit.ringMaterial is unwired");
                systemRig.Apply(arrangementAssets[3]);
            });
            Add(2f, () =>
            {
                Check(systemOrbit != null && systemOrbit.Realism >= 0.999f,
                      $"a Realistic layout tweens Orbit.Realism to 1 ({(systemOrbit == null ? -1f : systemOrbit.Realism):0.000})");
                var scale = sunAnchor != null ? sunAnchor.localScale.x : -1f;
                Check(scale > 0f && Mathf.Abs(scale - SunScaleAtRealism) <= SunScaleAtRealism * 5f,
                      $"the sun anchor shrinks to Orbit.TargetRealismPlanetScale ({SunScaleAtRealism}) at full realism ({scale:0.00000})");
                systemRig.Apply(arrangementAssets[1], 0f);
            });
            Add(0.2f, () =>
            {
                hovering = star != null && sunAnchor != null && Assigned(star, "mouse");
                if (star == null) { Skip("the Sun touch: no Sun component under the host, so the sun body was never built"); return; }
                if (!hovering)
                {
                    Skip("the Sun touch: Sun.mouse is null, which is Phase 6 wiring. Cosmic/Verify/P4 Setup sets it through " +
                         "SerializedObject; re-run setup before entering play mode and this becomes an assertion.");
                    return;
                }
                // The milky_way grab sphere is 0.84 m across the line of sight at 1.5 m, so it would swallow the ray before the sun.
                if (galaxyHull != null) galaxyHull.enabled = false;
                sunAnchor.position = view.position + view.forward * SunReachMetres;
                point = At(sunAnchor.position);
                holding = () => Move(point);
            });
            Add(1f, () =>
            {
                if (!hovering) return;
                Check(star.Touch > TouchHigh, $"the cursor on the sun raises Sun.Touch past {TouchHigh} within 1 s ({star.Touch:0.00})");
                point = Idle();
                holding = () => Move(point);
            });
            Add(1.5f, () =>
            {
                if (hovering) Check(star.Touch < TouchLow, $"Sun.Touch falls back under {TouchLow} within 1.5 s of the cursor leaving ({star.Touch:0.00})");
                if (galaxyHull != null) galaxyHull.enabled = true;
                holding = null;
            });
            Add(0f, () =>
            {
                if (galaxyGrab == null) { Skip("the galaxy grab: no Grabbable on the milky_way place root"); return; }
                galaxyGrab.CaptureHome();
                home = galaxyGrab.transform.localPosition;
                point = At(galaxyGrab.transform.position);
                holding = () => Move(point);
            });
            Add(0.35f, () =>
            {
                if (galaxyGrab == null) return;
                Check(galaxyGrab.isHovered, "the cursor on the milky_way makes its Grabbable hovered");
                Press(point, true, false, Vector2.zero);
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.25f, () =>
            {
                if (galaxyGrab == null) return;
                Check(galaxyGrab.isSelected, "LMB over the milky_way selects it");
                mark = galaxyGrab.transform.position;
                point += new Vector2(DragPixels, 0f);
                Press(point, true, false, new Vector2(DragPixels, 0f));
                holding = () => Press(point, true, false, Vector2.zero);
            });
            Add(0.3f, () =>
            {
                if (galaxyGrab == null) return;
                var moved = Vector3.Distance(mark, galaxyGrab.transform.position);
                Check(moved > 0.05f, $"dragging the cursor {DragPixels} px right carries the galaxy with it ({moved:0.000} m)");
                holding = null;
                Press(point, false, false, Vector2.zero);
            });
            Add(0.3f, () =>
            {
                if (galaxyGrab == null) return;
                Check(!galaxyGrab.isSelected, "releasing LMB deselects the galaxy");
                Check(galaxyGrab.Placed, "a release away from home marks the galaxy Placed");
                point = Idle();
                holding = () => Move(point);
                restored = false;
                Tap(Key.R, true);
            });
            Add(0.1f, () => Tap(Key.R, false));
            Add(1.2f, () =>
            {
                if (galaxyGrab == null) return;
                Check(restored, "the R key raises Hotkeys.Restore");
                var off = Vector3.Distance(galaxyGrab.transform.localPosition, home);
                Check(off <= 0.01f, $"Restore tweens the galaxy home ({off:0.000} m off)");
                Check(!galaxyGrab.Placed, "Restore clears Placed");
            });
            Add(0.2f, () => Check(roomFires == 0, $"Room.Changed never fired: Phase 4 does not touch the room ({roomFires})"));

            cleanup = () =>
            {
                holding = null;
                if (roomListener != null) { Room.Changed -= roomListener; roomListener = null; }
                if (keys != null && restoreListener != null) keys.Restore -= restoreListener;
                restoreListener = null;
                if (galaxyHull != null) galaxyHull.enabled = true;
                Tap(Key.R, false);
                var device = MouseDevice.current;
                if (device != null) Move(device.position.ReadValue());
            };
        }

        static void Settled(string id, Layout layout, Transform[] anchors, Vector3[] starts)
        {
            float nearest = float.MaxValue, worst = 0f;
            var faults = string.Empty;
            for (var i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(starts[i], anchors[i].localPosition));
                if (!SlotAt(layout, SolarIds[i], out var want)) { faults += " " + SolarIds[i] + "(no slot)"; continue; }
                var off = Vector3.Distance(want, anchors[i].localPosition);
                if (off > worst) worst = off;
                if (off > SlotToleranceMetres) faults += $" {SolarIds[i]}({off * 1000f:F1} mm)";
            }

            Check(nearest < float.MaxValue && nearest > LayoutMovedMetres,
                  $"{id} moved every anchor by more than {LayoutMovedMetres} m (the least of them moved {nearest:0.000} m)");
            Check(faults.Length == 0,
                  $"{id} lands every anchor within {SlotToleranceMetres * 1000f:F0} mm of its slot (worst {worst * 1000f:F1} mm)" +
                  (faults.Length == 0 ? string.Empty : ", over:" + faults));
        }

        static bool SlotAt(Layout layout, string id, out Vector3 local)
        {
            local = Vector3.zero;
            if (layout == null || layout.slots == null) return false;
            for (var i = 0; i < layout.slots.Length; i++)
            {
                var slotBody = layout.slots[i].body;
                if (slotBody == null || slotBody.id != id) continue;
                local = layout.slots[i].localPosition;
                return true;
            }

            return false;
        }

        static GameObject Spawn(Transform host, string id)
        {
            var existing = host.Find(id);
            if (existing != null) return existing.gameObject;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/{id}.prefab");
            if (asset == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, host.gameObject.scene);
            instance.name = id;
            instance.transform.SetParent(host, false);
            return instance;
        }

        // The generated data splits the system: solar_system.asset owns the bodies, solar_system_planets.asset owns the layouts.
        static GameObject SystemPrefab(out string id)
        {
            id = "solar_system_planets";
            var preferred = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/{id}.prefab");
            if (preferred != null && preferred.GetComponent<Rig>() != null) return preferred;
            var fallback = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlacePrefabFolder}/solar_system.prefab");
            if (fallback == null || fallback.GetComponent<Rig>() == null) return preferred;
            id = "solar_system";
            return fallback;
        }

        static bool Ready(string what)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError($"{tag} FAIL the editor is in play mode; {what} refuses to write and nothing below it would mean anything");
                return false;
            }

            if (steps.Count > 0)
            {
                Debug.LogError(tag + " FAIL a verify sequence is live; leave play mode and try again");
                return false;
            }

            return true;
        }

        static bool Assigned(UnityEngine.Object owner, string field)
        {
            if (owner == null) return false;
            var property = new SerializedObject(owner).FindProperty(field);
            return property != null && property.objectReferenceValue != null;
        }

        static string[] Found(string filter, string folder) =>
            AssetDatabase.IsValidFolder(folder) ? AssetDatabase.FindAssets(filter, new[] { folder }) : new string[0];

        static Dictionary<string, string> Baked()
        {
            var map = new Dictionary<string, string>();
            foreach (var folder in new[] { GalaxyFolder, CloudFolder })
                foreach (var guid in Found("t:ScriptableObject", folder))
                    map[AssetDatabase.GUIDToAssetPath(guid)] = guid;
            return map;
        }

        static Dictionary<string, string> Built()
        {
            var map = new Dictionary<string, string>();
            foreach (var folder in new[] { PlacePrefabFolder, BodyPrefabFolder })
                foreach (var guid in Found("t:Prefab", folder))
                    map[AssetDatabase.GUIDToAssetPath(guid)] = guid;
            foreach (var guid in Found("t:ScriptableObject", LayoutFolder))
                map[AssetDatabase.GUIDToAssetPath(guid)] = guid;
            return map;
        }
    }

    public class VerifyRunner : MonoBehaviour
    {
        public Vector3 from, to;
        public float worst;

        void LateUpdate()
        {
            var span = to - from;
            var k = Vector3.Dot(transform.localPosition - from, span) / Mathf.Max(1e-6f, span.sqrMagnitude);
            if (k > worst) worst = k;
        }
    }

    public class VerifyProbe : MonoBehaviour
    {
        public Grabbable grab;
        public Pull pull;
        public int selectedFrame = -1;
        public bool sawPulling;
        public float beamPeak;

        public void Arm() => selectedFrame = -1;

        void LateUpdate()
        {
            if (grab != null && selectedFrame < 0 && grab.isSelected) selectedFrame = Time.frameCount;
            if (pull == null) return;
            if (pull.Current == Pull.State.Pulling) sawPulling = true;
            if (pull.Beam > beamPeak) beamPeak = pull.Beam;
        }
    }
}
