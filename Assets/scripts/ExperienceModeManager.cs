// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace GalaxyExplorer
{
    /// <summary>
    /// On Meta Quest 3 the app runs either in passthrough (mixed reality: the content floats in the user's room,
    /// as it did on HoloLens) or in full VR (a star background surrounds the user). Passthrough is the default and
    /// the user's choice is remembered. Other platforms always render as before.
    /// </summary>
    public class ExperienceModeManager : MonoBehaviour
    {
        public enum Mode
        {
            Passthrough,
            VR
        }

        private const string PrefsKey = "GalaxyExplorer.ExperienceMode";

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

            CurrentMode = (Mode)PlayerPrefs.GetInt(PrefsKey, (int)Mode.Passthrough);
            Apply();
            ModeChanged?.Invoke(CurrentMode);
        }

        public void Toggle()
        {
            SetMode(CurrentMode == Mode.VR ? Mode.Passthrough : Mode.VR);
        }

        public void SetMode(Mode mode)
        {
            if (!GalaxyExplorerManager.IsQuest3 || mode == CurrentMode)
            {
                return;
            }

            CurrentMode = mode;
            PlayerPrefs.SetInt(PrefsKey, (int)mode);
            PlayerPrefs.Save();
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
        }
    }
}
