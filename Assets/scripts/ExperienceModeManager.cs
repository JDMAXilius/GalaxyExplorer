// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Unity.XR.CompositionLayers.Services;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR.CompositionLayers;

namespace GalaxyExplorer
{
    /// <summary>
    /// On Meta Quest 3 the app runs either in passthrough (mixed reality: the content floats in the user's room,
    /// as it did on HoloLens) or in full VR (a star background surrounds the user). This only drives the headset;
    /// the decision is <c>EnvironmentController</c>'s, which is the one caller of <see cref="SetMode"/> and holds
    /// the saved preference. Nothing is remembered here, so VR can never survive into the next launch's intro.
    /// Other platforms always render as before.
    /// </summary>
    public class ExperienceModeManager : MonoBehaviour
    {
        public enum Mode
        {
            Passthrough,
            VR
        }

        private ARSession _arSession;
        private ARCameraManager _cameraManager;

        public static ExperienceModeManager Instance { get; private set; }

        public static event Action<Mode> ModeChanged;

        public Mode CurrentMode { get; private set; } = Mode.Passthrough;

        /// <summary>
        /// Whether VR-only scenery (the star background) should be shown: on opaque headsets, and on Quest 3 in VR mode.
        /// </summary>
        public static bool ShowsVRScenery =>
            GalaxyExplorerManager.IsImmersiveHMD ||
            (GalaxyExplorerManager.IsQuest3 && Instance != null && Instance.CurrentMode == Mode.VR);

        private void Awake()
        {
            Instance = this;
            _arSession = FindAnyObjectByType<ARSession>(FindObjectsInactive.Include);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            var camera = Camera.main;
            _cameraManager = camera != null ? camera.GetComponent<ARCameraManager>() : null;

            var isQuest = GalaxyExplorerManager.IsQuest3;
            if (_arSession != null)
            {
                _arSession.enabled = isQuest;
            }

            if (!isQuest)
            {
                if (_cameraManager != null)
                {
                    _cameraManager.enabled = false;
                }
                return;
            }

            // CurrentMode may already have been set: EnvironmentController runs its Start before ours.
            Apply();
            ModeChanged?.Invoke(CurrentMode);
        }

        public void SetMode(Mode mode)
        {
            if (!GalaxyExplorerManager.IsQuest3 || mode == CurrentMode)
            {
                return;
            }

            CurrentMode = mode;
            Apply();
            ModeChanged?.Invoke(mode);
        }

        private void Apply()
        {
            var passthrough = CurrentMode == Mode.Passthrough;

            // Meta's compositor shows the passthrough camera wherever the eye buffer's alpha is 0.
            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, passthrough ? 0f : 1f);
            }

            if (_cameraManager != null)
            {
                _cameraManager.enabled = passthrough;
            }

            // One line that proves the whole chain on device. layerProvider=False means the OpenXR
            // "Composition Layers Support" feature is off and no passthrough layer can be submitted.
            var layers = CompositionLayerManager.Instance;
            var subsystem = _cameraManager != null ? _cameraManager.subsystem : null;
            Debug.Log($"[Passthrough] mode={CurrentMode} session={ARSession.state} " +
                      $"sessionEnabled={(_arSession != null && _arSession.enabled)} " +
                      $"cameraManager={(_cameraManager != null ? _cameraManager.enabled.ToString() : "missing")} " +
                      $"cameraSubsystem={(subsystem != null ? (subsystem.running ? "running" : "stopped") : "null")} " +
                      $"clear={(camera != null ? camera.clearFlags.ToString() : "no-camera")} " +
                      $"alpha={(camera != null ? camera.backgroundColor.a : -1f)} " +
                      $"layerProvider={(layers != null && layers.LayerProvider != null)} " +
                      $"layerProviderStarted={OpenXRLayerProvider.isStarted} " +
                      $"layers={(layers != null ? layers.CompositionLayers.Count : -1)}");
        }
    }
}
