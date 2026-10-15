// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// In the headset the shell's notice lives on its own small world-space canvas just below the player's gaze
    /// (CS-376). Inside the shell it went dark with the frame, which is off through the intro, every place switch
    /// and the settle after it: exactly when tracking-lost and failed-to-open notices are raised. It follows the head
    /// lazily, staying put while the gaze wanders less than the follow angle and then gliding back under it, so it
    /// can be read without chasing the eyes. On a monitor the notice stays in the frame and this is never made.
    /// </summary>
    public sealed class Ui2NoticeAnchor : MonoBehaviour
    {
        // The glide stops this close to the gaze: a notice that keeps creeping after the eyes reads as swimming.
        private const float SettleDegrees = 2f;

        // Seconds for the glide to close most of the gap, quick enough to keep up with a turn of the head.
        private const float GlideSeconds = 0.2f;

        private GameObject _notice;
        private Camera _camera;
        private float _distance;
        private float _drop;
        private float _follow;
        private Vector3 _direction;
        private bool _placed;
        private bool _gliding;

        /// <summary>
        /// Moves <paramref name="notice"/> out of the shell onto a canvas of its own with the shell canvas's scale
        /// and pixel settings, so it looks the same. Distance in metres from the eyes; drop and follow in degrees.
        /// </summary>
        public static Ui2NoticeAnchor Adopt(GameObject notice, Canvas shell, float distance, float drop, float follow)
        {
            var host = new GameObject("ui2_notice", typeof(RectTransform));
            DontDestroyOnLoad(host);
            var rect = (RectTransform)host.transform;
            var noticeRect = (RectTransform)notice.transform;
            rect.sizeDelta = noticeRect.sizeDelta;
            rect.localScale = shell.transform.localScale;

            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerID = shell.sortingLayerID;

            // One above the shell: the notice can sit in front of the menu and must not be drawn under it.
            canvas.sortingOrder = shell.sortingOrder + 1;

            // In world space CanvasScaler sets the canvas's scale factor and reference pixels per unit, which the
            // rounded plates' sliced sprites and the text are drawn at; without the shell's values they come out
            // a different weight from the shell they belong to.
            var scaler = host.AddComponent<CanvasScaler>();
            var shellScaler = shell.GetComponent<CanvasScaler>();
            if (shellScaler != null)
            {
                scaler.dynamicPixelsPerUnit = shellScaler.dynamicPixelsPerUnit;
                scaler.referencePixelsPerUnit = shellScaler.referencePixelsPerUnit;
            }

            noticeRect.SetParent(rect, false);
            noticeRect.anchorMin = noticeRect.anchorMax = noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.anchoredPosition = Vector2.zero;

            var anchor = host.AddComponent<Ui2NoticeAnchor>();
            anchor._notice = notice;
            anchor._distance = distance;
            anchor._drop = drop;
            anchor._follow = follow;
            return anchor;
        }

        private void LateUpdate()
        {
            if (_notice == null || !_notice.activeSelf)
            {
                // Hidden: the next notice appears under wherever the player is looking then.
                _placed = false;
                return;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var head = _camera.transform;
            var wanted = Quaternion.AngleAxis(_drop, head.right) * head.forward;
            if (!_placed)
            {
                _direction = wanted;
                _placed = true;
                _gliding = false;
            }
            else
            {
                var off = Vector3.Angle(_direction, wanted);
                _gliding |= off > _follow;
                if (_gliding)
                {
                    _direction = Vector3.Slerp(_direction, wanted,
                                               1f - Mathf.Exp(-Time.unscaledDeltaTime / GlideSeconds)).normalized;
                    _gliding = off > SettleDegrees;
                }
            }

            // Kept upright against the world rather than rolling with the head; near straight up or down there
            // is no world-upright answer, so the head's own up stands in.
            var up = Mathf.Abs(Vector3.Dot(_direction, Vector3.up)) > 0.98f ? head.up : Vector3.up;
            transform.SetPositionAndRotation(head.position + _direction * _distance,
                                             Quaternion.LookRotation(_direction, up));
        }
    }
}
