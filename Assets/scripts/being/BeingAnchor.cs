using UnityEngine;

namespace CosmicSimulation.Being
{
    public class BeingAnchor : MonoBehaviour
    {
        [SerializeField] private BeingSettings settings;

        private Camera _camera;
        private Vector3 _velocity;
        private bool _placed;

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            var head = _camera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var target = head.position + forward * settings.DistanceMetres - right * settings.SideMetres + Vector3.up * settings.DropMetres;

            transform.position = _placed ? Vector3.SmoothDamp(transform.position, target, ref _velocity, settings.FollowSeconds) : target;
            _placed = true;
        }
    }
}
