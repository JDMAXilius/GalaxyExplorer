using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Cosmic.Editor
{
    public static class Scene
    {
        const float DesktopEyeHeightMetres = 1.36f;

        const string PrefabFolder = "Assets/Cosmic/Prefabs";
        const string RigPath = "Assets/Cosmic/Prefabs/rig.prefab";
        const string ActionsPath = "Assets/Cosmic/Input/actions.inputactions";
        const string DimPath = "Assets/Cosmic/Prefabs/room_dim.mat";
        const string LibraryPath = "Assets/Cosmic/Data/Generated/audio_library.asset";
        const string Interactors = "Assets/Samples/XR Interaction Toolkit/3.6.0/Starter Assets/Prefabs/Interactors/";
        const string HandPrefabs = "Assets/Samples/XR Interaction Toolkit/3.6.0/Hands Interaction Demo/Prefabs/";

        static readonly List<string> Missing = new List<string>();
        static readonly Dictionary<string, InputActionReference> References = new Dictionary<string, InputActionReference>();

        [MenuItem("Cosmic/Build/Rig")]
        public static void BuildRig()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                Debug.LogError($"Cosmic rig: {ActionsPath} is missing, so nothing can be wired.");
                return;
            }

            Missing.Clear();
            ReadReferences();
            var rebuilt = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) != null;
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Cosmic", "Prefabs");

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("rig");
                SceneManager.MoveGameObjectToScene(root, preview);
                Assemble(root, actions);
                PrefabUtility.SaveAsPrefabAsset(root, RigPath);
                Object.DestroyImmediate(root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Cosmic rig {(rebuilt ? "rebuilt" : "created")} -> {RigPath}; " +
                      (Missing.Count > 0 ? $"missing: {string.Join(", ", Missing)}" : "nothing missing"));
        }

        static void Assemble(GameObject root, InputActionAsset actions)
        {
            var offset = Child(root.transform, "Camera Offset");
            offset.localPosition = new Vector3(0f, DesktopEyeHeightMetres, 0f);
            var eye = Eye(offset);
            var left = Hand(offset, "Left");
            var right = Hand(offset, "Right");
            var mouse = Child(offset, "Mouse").gameObject.AddComponent<Mouse>();
            var content = Child(root.transform, "Content");

            var session = Child(root.transform, "AR Session").gameObject.AddComponent<ARSession>();
            session.enabled = false;
            var events = Child(root.transform, "Event System").gameObject;
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();

            // Through SerializedObject because the XROrigin setters move the offset and poke the XR subsystem; floor tracking zeroes that height on a headset and leaves it alone with no subsystem, which is the desktop eye.
            var origin = new SerializedObject(root.AddComponent<XROrigin>());
            origin.FindProperty("m_Camera").objectReferenceValue = eye;
            origin.FindProperty("m_OriginBaseGameObject").objectReferenceValue = root;
            origin.FindProperty("m_CameraFloorOffsetObject").objectReferenceValue = offset.gameObject;
            origin.FindProperty("m_RequestedTrackingOriginMode").enumValueIndex = (int)XROrigin.TrackingOriginMode.Floor;
            origin.FindProperty("m_CameraYOffset").floatValue = DesktopEyeHeightMetres;
            origin.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<XRInteractionManager>();
            root.AddComponent<InputActionManager>().actionAssets = new List<InputActionAsset> { actions };
            var modality = root.AddComponent<XRInputModalityManager>();
            modality.leftHand = left;
            modality.rightHand = right;

            var room = root.AddComponent<Room>();
            Wire(room, "dimMaterial", Optional<Material>(DimPath));
            Wire(room, "passthroughCamera", eye.GetComponent<ARCameraManager>());
            Wire(room, "passthroughSession", session);
            Wire(root.AddComponent<Audio>(), "library", Optional<AudioLibrary>(LibraryPath));
            var hotkeys = root.AddComponent<Hotkeys>();
            Wire(hotkeys, "actions", actions);
            Wire(mouse, "hotkeys", hotkeys);
            Wire(mouse, "pivot", content);
        }

        const string MainScenePath = "Assets/Cosmic/Scenes/main.unity";
        const string UiFolder = "Assets/Cosmic/Prefabs/ui";
        const string Places = "Assets/Cosmic/Data/Generated/places";
        const string EarthPrefab = "Assets/Cosmic/Prefabs/bodies/earth.prefab";
        const string LogoSfx = "Assets/audio/sfx_audio_clips/sfx_intro_logo_enter_audio_clip.wav";
        const string PlacementSfx = "Assets/audio/sfx_audio_clips/sfx_intro_globe_placement_audio_clip.wav";
        static readonly string[] DockOrder = { "cosmic_web", "galaxies", "milky_way", "andromeda", "solar_system", "solar_system_planets", "sagittarius_a" };

        [MenuItem("Cosmic/Build/Main Scene")]
        public static void BuildMain()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Cosmic main scene: refused in play mode."); return; }
            Missing.Clear();
            if (!AssetDatabase.IsValidFolder("Assets/Cosmic/Scenes")) AssetDatabase.CreateFolder("Assets/Cosmic", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var rigAsset = Optional<GameObject>(RigPath);
                var rig = rigAsset != null ? (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, scene) : new GameObject("rig");
                var content = rig.transform.Find("Content") ?? Child(rig.transform, "Content");
                var app = new GameObject("app");
                SceneManager.MoveGameObjectToScene(app, scene);
                var director = app.AddComponent<Director>();
                var anchor = app.AddComponent<Anchor>();
                var panels = app.AddComponent<Panels>();
                var main = app.AddComponent<App>();
                var ui = Child(app.transform, "ui");
                var dock = Nest($"{UiFolder}/dock.prefab", ui);
                var toast = Nest($"{UiFolder}/toast.prefab", ui);
                var about = Nest($"{UiFolder}/about.prefab", ui);
                var hotkeys = rig.GetComponentInChildren<Hotkeys>(true);
                var audio = rig.GetComponentInChildren<Audio>(true);
                var mouse = rig.GetComponentInChildren<Mouse>(true);
                Intro(app.transform, content, anchor, audio);
                Wire(director, "anchor", anchor);
                Wire(director, "audio", audio);
                Wire(director, "toast", toast != null ? toast.GetComponent<Toast>() : null);
                Wire(director, "panels", panels);
                Wire(panels, "scenePrefab", Optional<GameObject>($"{UiFolder}/panel_scene.prefab")?.GetComponent<Panel>());
                Wire(panels, "bodyPrefab", Optional<GameObject>($"{UiFolder}/panel_body.prefab")?.GetComponent<Panel>());
                Wire(panels, "moonPrefab", Optional<GameObject>($"{UiFolder}/panel_moon.prefab")?.GetComponent<Panel>());
                Wire(panels, "tagPrefab", Optional<GameObject>($"{UiFolder}/label_card.prefab")?.GetComponent<Label>());
                Wire(panels, "namePrefab", Optional<GameObject>($"{UiFolder}/label_name.prefab")?.GetComponent<Label>());
                if (dock != null)
                {
                    Wire(dock.GetComponent<Dock>(), "hotkeys", hotkeys);
                    Wire(dock.GetComponent<Dock>(), "toast", toast != null ? toast.GetComponent<Toast>() : null);
                }
                if (toast != null) Wire(toast.GetComponent<Toast>(), "hotkeys", hotkeys);
                if (mouse != null) Wire(mouse, "pivot", content);
                Wire(main, "director", director);
                Wire(main, "anchor", anchor);
                Wire(main, "dock", dock != null ? dock.GetComponent<Dock>() : null);
                Wire(main, "toast", toast != null ? toast.GetComponent<Toast>() : null);
                Wire(main, "about", about != null ? about.GetComponent<About>() : null);
                Wire(main, "hotkeys", hotkeys);
                Wire(main, "audio", audio);
                var places = new List<Object>();
                foreach (var id in DockOrder)
                {
                    var place = AssetDatabase.LoadAssetAtPath<Place>($"{Places}/{id}.asset");
                    if (place == null) Missing.Add($"{Places}/{id}.asset");
                    else places.Add(place);
                }
                var serialized = new SerializedObject(main);
                var list = serialized.FindProperty("places");
                list.arraySize = places.Count;
                for (var i = 0; i < places.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = places[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Wire(main, "start", AssetDatabase.LoadAssetAtPath<Place>($"{Places}/milky_way.asset"));
                EditorSceneManager.SaveScene(scene, MainScenePath);
                Register();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log($"Cosmic main scene -> {MainScenePath}; " + (Missing.Count > 0 ? $"missing: {string.Join(", ", Missing)}" : "nothing missing"));
        }

        static void Intro(Transform app, Transform content, Anchor anchor, Audio audio)
        {
            var logo = Child(app, "logo").gameObject.AddComponent<TMPro.TextMeshPro>();
            logo.text = "Cosmic Simulation XR";
            logo.fontSize = 0.12f;
            logo.alignment = TMPro.TextAlignmentOptions.Center;
            logo.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f);
            var theme = Optional<Theme>("Assets/Cosmic/Data/Generated/theme.asset");
            if (theme != null && theme.font != null) logo.font = theme.font;
            var floor = Child(app, "floor").gameObject;
            floor.AddComponent<BoxCollider>().size = new Vector3(20f, 0.02f, 20f);
            var floorInteractable = floor.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var pin = Nest(EarthPrefab, app);
            if (pin != null)
            {
                pin.name = "pin";
                pin.transform.localScale = Vector3.one * 0.1f;
                foreach (var grab in pin.GetComponentsInChildren<Grabbable>(true)) grab.enabled = false;
                foreach (var pull in pin.GetComponentsInChildren<Pull>(true)) pull.enabled = false;
            }
            Wire(anchor, "content", content);
            Wire(anchor, "logo", logo);
            Wire(anchor, "pin", pin);
            Wire(anchor, "floor", floorInteractable);
            Wire(anchor, "audio", audio);
            Wire(anchor, "logoClip", Optional<AudioClip>(LogoSfx));
            Wire(anchor, "placementClip", Optional<AudioClip>(PlacementSfx));
        }

        static void Register()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == MainScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Camera Eye(Transform parent)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(parent, false);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
            go.AddComponent<AudioListener>();
            var driver = go.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Singleton("HMD Position", "<XRHMD>/centerEyePosition", "Vector3"));
            driver.rotationInput = new InputActionProperty(Singleton("HMD Rotation", "<XRHMD>/centerEyeRotation", "Quaternion"));
            go.AddComponent<ARCameraManager>().enabled = false;
            return camera;
        }

        static GameObject Hand(Transform parent, string side)
        {
            var hand = Child(parent, $"{side} Hand");
            var driver = hand.gameObject.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Reference($"XR/{side} Position"));
            driver.rotationInput = new InputActionProperty(Reference($"XR/{side} Rotation"));

            var nearFar = Nest($"{Interactors}{side}_NearFarInteractor.prefab", hand);
            var interactor = nearFar != null ? nearFar.GetComponent<NearFarInteractor>() : null;
            if (interactor != null)
            {
                interactor.selectInput.inputActionReferencePerformed = Reference($"XR/{side} Select");
                interactor.selectInput.inputActionReferenceValue = null;
                interactor.uiPressInput.inputActionReferencePerformed = Reference($"XR/{side} UI Press");
                interactor.uiPressInput.inputActionReferenceValue = null;
                interactor.activateInput.inputActionReferencePerformed = Reference($"XR/{side} Activate");
                interactor.activateInput.inputActionReferenceValue = null;
                PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
                var attach = nearFar.GetComponent<InteractionAttachController>();
                if (attach != null)
                {
                    attach.manipulationInput.inputActionReference = Reference($"XR/{side} Manipulation");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(attach);
                }
            }

            var poke = Child(hand, $"{side} Poke Pose");
            var pokeDriver = poke.gameObject.AddComponent<TrackedPoseDriver>();
            pokeDriver.positionInput = new InputActionProperty(Reference($"XR/{side} Poke Position"));
            pokeDriver.rotationInput = new InputActionProperty(Reference($"XR/{side} Poke Rotation"));
            Nest($"{Interactors}Poke Interactor.prefab", poke);
            Nest($"{HandPrefabs}{side}HandQuestVisual.prefab", hand);
            return hand.gameObject;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Nest(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Missing.Add(path);
                return null;
            }

            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        static T Optional<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Missing.Add(path);
            return asset;
        }

        static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Missing.Add($"{target.GetType().Name}.{field}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static InputAction Singleton(string name, string binding, string expectedControlType) =>
            new InputAction(name, InputActionType.Value, binding, expectedControlType: expectedControlType);

        static InputActionReference Reference(string key)
        {
            if (References.TryGetValue(key, out var reference)) return reference;
            Missing.Add($"action {key}");
            return null;
        }

        static void ReadReferences()
        {
            References.Clear();
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(ActionsPath))
            {
                var reference = asset as InputActionReference;
                if (reference == null || reference.action == null || reference.action.actionMap == null) continue;
                References[$"{reference.action.actionMap.name}/{reference.action.name}"] = reference;
            }
        }
    }
}
