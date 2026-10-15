// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.XR;

namespace CosmicSimulation
{
    /// <summary>
    /// The menu stays world-locked (D-016, owner decision 2), so a player who turns away can lose it. When the UI 2.0
    /// shell has been wholly out of view for <see cref="OutOfViewSeconds"/> this says once where it went and how to
    /// call it back (CS-375). Headset only; at most once every <see cref="RepeatSeconds"/>, and once per time it
    /// leaves view.
    ///
    /// Installs itself; nothing in a scene or prefab refers to it.
    /// </summary>
    public class MenuRecallCue : MonoBehaviour
    {
        private const float OutOfViewSeconds = 3f;
        private const float RepeatSeconds = 60f;

        private const string Message = "The menu is behind you. Show your palm and press Dock to bring it here.";

        // The child Ui2Builder puts everything visible under; it is off until the intro has handed over.
        private const string FrameName = "frame";

        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector3[] _corners = new Vector3[4];
        private float _outFor;
        private bool _saidThisTime;
        private float _quietUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MenuRecallCue>() != null)
            {
                return;
            }

            var host = new GameObject(nameof(MenuRecallCue));
            DontDestroyOnLoad(host);
            host.AddComponent<MenuRecallCue>();
        }

        private void Update()
        {
            var shell = UI2.Ui2Shell.Instance;
            var view = Camera.main;
            if (!XRSettings.isDeviceActive || shell == null || view == null || !Settled(shell))
            {
                _outFor = 0f;
                return;
            }

            if (InView(shell, view))
            {
                _outFor = 0f;
                _saidThisTime = false;
                return;
            }

            _outFor += Time.unscaledDeltaTime;
            if (_saidThisTime || _outFor < OutOfViewSeconds || Time.unscaledTime < _quietUntil || OtherwiseBusy(shell))
            {
                return;
            }

            _saidThisTime = true;
            _quietUntil = Time.unscaledTime + RepeatSeconds;

            // Past UI 2.0's redirect, which would draw the line on the shell itself: the very thing out of view. The
            // legacy notice parks in front of the head when the legacy dock is down, as it always is under UI 2.0.
            var redirect = SwitchNotice.Redirect;
            SwitchNotice.Redirect = null;
            try
            {
                SwitchNotice.Say(Message);
            }
            finally
            {
                SwitchNotice.Redirect = redirect;
            }
        }

        // The intro's real end, as the shell itself judges it, and the shell actually showing.
        private static bool Settled(UI2.Ui2Shell shell)
        {
            var director = ExperienceDirector.Instance;
            if (director == null || director.IntroRunning || !director.IntroComplete || director.IsSwitching)
            {
                return false;
            }

            var frame = shell.transform.Find(FrameName);
            return frame == null || frame.gameObject.activeInHierarchy;
        }

        // Waits rather than talking over a line or a hint card that is already up.
        private static bool OtherwiseBusy(UI2.Ui2Shell shell)
        {
            var notice = SwitchNotice.Instance;
            var hints = HintCards.Instance;
            return shell.NoticeShowing || (notice != null && notice.IsShowing) || (hints != null && hints.IsShowing);
        }

        // The box around the shell's rect against the camera's frustum. The box is larger than the rect, so this
        // errs toward "in view" and the cue only speaks when the menu is certainly gone.
        private bool InView(UI2.Ui2Shell shell, Camera view)
        {
            if (!(shell.transform is RectTransform rect))
            {
                return true;
            }

            rect.GetWorldCorners(_corners);
            var bounds = new Bounds(_corners[0], Vector3.zero);
            for (var i = 1; i < _corners.Length; i++)
            {
                bounds.Encapsulate(_corners[i]);
            }

            GeometryUtility.CalculateFrustumPlanes(view, _planes);
            return GeometryUtility.TestPlanesAABB(_planes, bounds);
        }
    }
}
