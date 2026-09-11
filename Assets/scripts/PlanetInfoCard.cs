using UnityEngine;

namespace GalaxyExplorer
{
    /// <summary>
    /// Fact card for a planet or moon (a world-space canvas). It fades in beside the body once the body has been
    /// pulled out of its orbit and fades out when the body goes back. The card faces the viewer, sits on the side of
    /// the body nearer the middle of the view, and keeps the same physical size whatever the body's or the solar
    /// system's scale.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public class PlanetInfoCard : MonoBehaviour
    {
        // How far (meters) the body has to be off the view's center line before the card switches sides.
        private const float SideSwitchDistance = 0.12f;

        [SerializeField]
        [Tooltip("The body this card describes.")]
        private ForceSolver body = null;

        [SerializeField]
        [Tooltip("Renderer whose bounds give the body's size, so the card clears it.")]
        private Renderer bodyRenderer = null;

        [SerializeField]
        [Tooltip("World size of one canvas unit, in meters.")]
        private float metersPerUnit = 0.001f;

        [SerializeField]
        [Tooltip("Gap between the body's edge and the card, in meters.")]
        private float gap = 0.03f;

        [SerializeField]
        private float fadeSeconds = 0.35f;

        [SerializeField]
        private float followLerpTime = 0.08f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _rect;
        private Camera _camera;
        private bool _placed;
        private int _side = 1;

        private bool BodyIsOut
        {
            get
            {
                if (body == null)
                {
                    return false;
                }

                switch (body.ForceState)
                {
                    case ForceSolver.State.Attraction:
                    case ForceSolver.State.Free:
                    case ForceSolver.State.Manipulation:
                        return true;
                    case ForceSolver.State.Dwell:
                        return body.PreviousForceState != ForceSolver.State.Root;
                    default:
                        return false;
                }
            }
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _group = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _canvas.enabled = false;
        }

        private void LateUpdate()
        {
            var show = BodyIsOut;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            _canvas.enabled = _group.alpha > 0f;
            if (!_canvas.enabled)
            {
                _placed = false;
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Place();
        }

        private void Place()
        {
            var cameraTransform = _camera.transform;
            var bounds = bodyRenderer != null ? bodyRenderer.bounds : new Bounds(body.transform.position, Vector3.zero);
            var center = bounds.center;
            var radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            // Put the card on the side of the body that is nearer the middle of the view.
            var lateral = Vector3.Dot(center - cameraTransform.position, cameraTransform.right);
            if (!_placed)
            {
                _side = lateral > SideSwitchDistance * 0.5f ? -1 : 1;
            }
            else if (_side > 0 && lateral > SideSwitchDistance)
            {
                _side = -1;
            }
            else if (_side < 0 && lateral < -SideSwitchDistance)
            {
                _side = 1;
            }
            _rect.pivot = new Vector2(_side > 0 ? 0f : 1f, 0.5f);

            var targetPosition = center + cameraTransform.right * (_side * (radius + gap));
            var targetRotation = Quaternion.LookRotation(targetPosition - cameraTransform.position, cameraTransform.up);

            var parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Vector3.one * (metersPerUnit / Mathf.Max(parentScale, 1e-5f));

            if (!_placed || followLerpTime <= 0f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                _placed = true;
            }
            else
            {
                var t = Mathf.Clamp01(Time.deltaTime / followLerpTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPosition, t),
                    Quaternion.Slerp(transform.rotation, targetRotation, t));
            }
        }
    }
}
