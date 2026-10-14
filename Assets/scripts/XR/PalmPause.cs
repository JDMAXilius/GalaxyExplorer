// Licensed under the MIT License. See LICENSE in the project root for license information.

using CosmicSimulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// The one pose shortcut (CS-289, D-H5): an open hand held out with its palm toward the content pauses the
    /// narration, and the same pose again resumes it from the same word. <c>N</c> does the same on the desktop.
    ///
    /// <para>The pose is measured from the joints through <see cref="XRInputRig"/>, so the XR Hands Gestures sample
    /// is not needed: the palm faces away along the line of sight (the palm menu's pose is the opposite one, palm
    /// to the face), the fingers are open, the hand is out in front of the head, and it is held for
    /// <see cref="HoldSeconds"/>. Nothing fires while something is held, and a fired pose has to be dropped
    /// before it can fire again.</para>
    ///
    /// Installs itself; nothing in a scene or prefab refers to it.
    /// </summary>
    public class PalmPause : MonoBehaviour
    {
        private const float HoldSeconds = 0.4f;
        private const float FacingDegrees = 35f;
        private const float OpenCurl = 0.25f;
        private const float ReachMetres = 0.25f;
        private const float InViewDegrees = 45f;

        private float _heldFor;
        private bool _latched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<PalmPause>() != null)
            {
                return;
            }

            var host = new GameObject(nameof(PalmPause));
            DontDestroyOnLoad(host);
            host.AddComponent<PalmPause>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.nKey.wasPressedThisFrame && !XRSettings.isDeviceActive)
            {
                Toggle();
                return;
            }

            if (!XRSettings.isDeviceActive)
            {
                return;
            }

            if (!Posed())
            {
                _heldFor = 0f;
                _latched = false;
                return;
            }

            if (_latched)
            {
                return;
            }

            _heldFor += Time.unscaledDeltaTime;
            if (_heldFor >= HoldSeconds)
            {
                _latched = true;
                Toggle();
            }
        }

        private static bool Posed()
        {
            var rig = XRInputRig.Instance;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (rig == null || head == null || ManipulationHandler.Held != null)
            {
                return false;
            }

            return Posed(rig, head, true) || Posed(rig, head, false);
        }

        private static bool Posed(XRInputRig rig, Transform head, bool left)
        {
            if (!rig.TryGetPalmNormal(left, out var normal) || !rig.TryGetFingerCurl(left, out var curl) ||
                curl > OpenCurl || Vector3.Angle(normal, head.forward) > FacingDegrees)
            {
                return false;
            }

            var palm = left ? rig.LeftPalm : rig.RightPalm;
            if (palm == null)
            {
                return false;
            }

            var toPalm = palm.position - head.position;
            return Vector3.Dot(toPalm, head.forward) >= ReachMetres && Vector3.Angle(toPalm, head.forward) <= InViewDegrees;
        }

        private static void Toggle()
        {
            var narration = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            if (narration == null)
            {
                narration = FindAnyObjectByType<VOManager>();
            }

            if (narration == null)
            {
                return;
            }

            if (narration.IsPaused)
            {
                narration.Resume();
                SwitchNotice.Say("Narration resumed");
            }
            else if (narration.IsPlaying)
            {
                // Pause only holds a clip that is sounding; in the gap between two clips there is nothing to hold,
                // so say nothing rather than claim a pause that did not happen.
                narration.Pause();
                if (narration.IsPaused)
                {
                    SwitchNotice.Say("Narration paused");
                }
            }
        }
    }
}
