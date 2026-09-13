using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        const float MusicLevel = 0.35f;
        const float DuckedLevel = MusicLevel * 0.55f;
        const float DimAlpha = 0.5f;
        const float Tolerance = 0.05f;

        class Step
        {
            public float waitSeconds;
            public System.Action run;
        }

        static readonly List<Step> steps = new List<Step>();
        static System.Action<RoomMode> listener;
        static Cosmic.Audio audio;
        static AudioClip voiceClip, ambienceClip;
        static AudioSource bed, loop;
        static Transform subject;
        static VerifyRunner runner;
        static double due;
        static float bedTime;
        static int cursor, passes, total, fires;

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
            cursor = passes = total = fires = 0;
            bed = loop = null;
            subject = null;
            runner = null;
            Build();
            due = EditorApplication.timeSinceStartup + steps[0].waitSeconds;
            EditorApplication.update += Tick;
            Debug.Log($"[P2] start: {steps.Count} steps, about 13 s. Switch Error Pause off first - a FAIL is a LogError and would pause play mode.");
        }

        [MenuItem("Cosmic/Verify/P2 Teardown")]
        public static void P2Teardown()
        {
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
                Debug.LogError("[P2] FAIL play mode ended mid-run");
                Finish();
                return;
            }
            if (EditorApplication.timeSinceStartup < due) return;

            var step = steps[cursor++];
            try { step.run(); }
            catch (System.Exception e) { total++; Debug.LogError($"[P2] FAIL step {cursor - 1} threw: {e}"); }

            if (cursor >= steps.Count) { Finish(); return; }
            due = EditorApplication.timeSinceStartup + steps[cursor].waitSeconds;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (listener != null) { Room.Changed -= listener; listener = null; }
            if (subject != null) UnityEngine.Object.Destroy(subject.gameObject);
            if (runner != null) UnityEngine.Object.Destroy(runner.gameObject);
            subject = null;
            runner = null;
            bed = loop = null;
            steps.Clear();
            cursor = 0;
            Debug.Log($"[P2] DONE {passes}/{total}");
        }

        static void Add(float waitSeconds, System.Action run) => steps.Add(new Step { waitSeconds = waitSeconds, run = run });

        static void Check(bool ok, string what)
        {
            total++;
            if (ok) { passes++; Debug.Log("[P2] PASS " + what); }
            else Debug.LogError("[P2] FAIL " + what);
        }

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
                Debug.LogError($"[P2] FAIL {target.GetType().Name} has no serialized field '{field}'");
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
}
