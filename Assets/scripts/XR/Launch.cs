// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using UnityEngine.XR.Management;
#endif

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Decides whether the Windows player starts XR (CS-250). <c>-vr</c> starts it; <c>-desktop</c>, or no flag,
    /// never touches the OpenXR runtime, so a player with SteamVR installed can play flat without it launching.
    /// The editor starts it only when <b>Cosmic Simulation → Play in Headset</b> is ticked (off by default), so
    /// Play stays on the desktop even while Meta Quest Link is running.
    ///
    /// Standalone has "Initialize XR on Startup" off (Assets/XR/XRGeneralSettingsPerBuildTarget.asset), so XR
    /// Management does nothing by itself there. This does what <c>XRGeneralSettings</c> does when that box is
    /// ticked, at the same two moments (XRGeneralSettings.cs: InitializeLoaderSync at AfterAssembliesLoaded,
    /// StartSubsystems at BeforeSplashScreen), so the headset is active before any scene's Awake reads
    /// <c>XRSettings.isDeviceActive</c>. Android keeps the box ticked and none of this is compiled there.
    /// </summary>
    public static class Launch
    {
        /// <summary>True when <c>-vr</c> was asked for and no OpenXR runtime would start; the app runs on the desktop.</summary>
        public static bool VRFailed { get; private set; }

        /// <summary>
        /// True when this run is a headset run, already from the first scene's Awake. <c>XRSettings.isDeviceActive</c>
        /// alone is not enough there: over Link the OpenXR session takes a couple of seconds to reach running, and a
        /// desktop decision taken in that gap put the screen-space desktop menu 0.3 m in front of the eyes. The
        /// loader and its display subsystem are in place from AfterAssembliesLoaded, on Android and here alike. A
        /// loader whose display failed to start (Link sometimes refuses a session) is not a headset run.
        /// </summary>
        public static bool Headset
        {
            get
            {
                if (UnityEngine.XR.XRSettings.isDeviceActive)
                {
                    return true;
                }

                var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                var loader = settings != null && settings.Manager != null ? settings.Manager.activeLoader : null;
                return loader != null && loader.GetLoadedSubsystem<UnityEngine.XR.XRDisplaySubsystem>() != null;
            }
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        private static XRManagerSettings _manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitialiseXR()
        {
            VRFailed = false;
            _manager = null;

            var settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null || settings.InitManagerOnStart || !WantsVR())
            {
                return;
            }

            settings.Manager.InitializeLoaderSync();
            if (settings.Manager.activeLoader == null)
            {
                VRFailed = true;
                Debug.LogWarning("[Launch] No VR runtime found; starting on the desktop.");
                return;
            }

            _manager = settings.Manager;
            Application.quitting -= Quit;
            Application.quitting += Quit;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void StartXR()
        {
            if (_manager != null)
            {
                _manager.StartSubsystems();
            }
        }

        // XRGeneralSettings only shuts down a loader it started itself, so this one is ours to stop; otherwise
        // SteamVR keeps listing the app as running.
        private static void Quit()
        {
            Application.quitting -= Quit;
            if (_manager != null && _manager.isInitializationComplete)
            {
                _manager.DeinitializeLoader();
            }

            _manager = null;
        }

#if UNITY_EDITOR
        /// <summary>EditorPrefs key behind the Play in Headset menu toggle (<c>PlayInHeadsetMenu</c>).</summary>
        public const string PlayInHeadsetPref = "CosmicSimulation.PlayInHeadset";
#endif

        private static bool WantsVR()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(PlayInHeadsetPref, false);
#else
            var args = Environment.GetCommandLineArgs();
            return Array.IndexOf(args, "-vr") >= 0 && Array.IndexOf(args, "-desktop") < 0;
#endif
        }
#endif
    }
}
