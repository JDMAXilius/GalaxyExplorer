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

        // docs/copy/ui.md, Messages: narration_paused, narration_resumed.
        private const string NarrationPaused = "Narration paused";
        private const string NarrationResumed = "Narration resumed";

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

        // Two things narrate: the narrator (VOManager) and, under UI 2.0, the card's Listen. If either is held the
        // gesture resumes both; otherwise it holds whichever is sounding. The notice only reports what happened.
        private static void Toggle()
        {
            var narration = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            if (narration == null)
            {
                narration = Registry<VOManager>.First;
            }

            var card = CosmicSimulation.UI2.Ui2Explore.Instance;
            var held = (narration != null && narration.IsPaused) || (card != null && card.ListenHeld);

            if (held)
            {
                if (narration != null && narration.IsPaused)
                {
                    narration.Resume();
                }

                if (card != null)
                {
                    card.HoldListen(false);
                }

                SwitchNotice.Say(NarrationResumed);
                return;
            }

            var paused = false;
            if (narration != null && narration.IsPlaying)
            {
                // Pause only holds a clip that is sounding; in the gap between two clips there is nothing to hold.
                narration.Pause();
                paused = narration.IsPaused;
            }

            if (card != null && card.HoldListen(true))
            {
                paused = true;
            }

            if (paused)
            {
                SwitchNotice.Say(NarrationPaused);
            }
        }
    }
}
