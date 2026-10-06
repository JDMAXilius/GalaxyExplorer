// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.XR;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Silences the app while the player is not in it: the Quest system menu (focus lost) and the app going to the
    /// background (pause). Store requirements Functional.2 and Input.4. Lives on the XR rig.
    ///
    /// <see cref="AudioListener.pause"/> rather than a per-service call: <c>AudioService</c> has no pause and
    /// <c>VOManager.Stop</c> is a fade-out that forgets the clip, whereas the player expects narration to carry on
    /// from where it was. The narration queue is driven by <c>VOManager.Update</c> with wall-clock delays, so the
    /// managers are also disabled, or the queue would advance under the paused listener.
    /// </summary>
    public class AppPause : MonoBehaviour
    {
        private bool _paused;

        private void OnApplicationPause(bool pause) => Set(pause);

        private void OnApplicationFocus(bool focus) => Set(!focus);

        private void OnDisable()
        {
            if (_paused)
            {
                Set(false);
            }
        }

        private void Set(bool paused)
        {
            // Headset only. A relay-driven play-mode run happens in an unfocused editor, which would otherwise
            // silence narration from the first frame; desktop keeps Unity's own run-in-background behaviour.
            // Focus and pause both fire on the same transition on Android; the second is a no-op.
            if (!XRSettings.isDeviceActive || _paused == paused)
            {
                return;
            }

            _paused = paused;
            AudioListener.pause = paused;

            foreach (var manager in FindObjectsByType<VOManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                manager.enabled = !paused;
            }
        }
    }
}
