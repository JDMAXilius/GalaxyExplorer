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
