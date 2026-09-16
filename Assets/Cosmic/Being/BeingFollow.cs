using UnityEngine;

namespace Cosmic
{
    public class BeingFollow : MonoBehaviour
    {
        [SerializeField] BeingSettings settings;
        [SerializeField] Grabbable grab;
        [SerializeField] float turnDegreesPerSecond = 540f;

        Camera cam;
        Vector3 offset, velocity, right = Vector3.right;
        bool placed, held, ready;

        void LateUpdate()
        {
            Init();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;
            // Right stays level so head roll never swings the being; forward keeps the pitch so it rides up and down with the view.
            var level = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (level.sqrMagnitude > 1e-4f) right = Vector3.Cross(Vector3.up, level.normalized);
            var forward = Vector3.ProjectOnPlane(head.forward, right).normalized;
            var up = Vector3.Cross(forward, right);
            if (grab != null && grab.isSelected) { held = true; Face(head); return; }
            if (held)
            {
                var local = transform.position - head.position;
                offset = new Vector3(Vector3.Dot(local, right), Vector3.Dot(local, up), Vector3.Dot(local, forward));
                velocity = Vector3.zero;
                held = false;
            }
            var goal = head.position + right * offset.x + up * offset.y + forward * offset.z;
            transform.position = placed ? Vector3.SmoothDamp(transform.position, goal, ref velocity, settings.followSeconds) : goal;
            placed = true;
            Face(head);
        }

        void Face(Transform head)
        {
            var toHead = head.position - transform.position;
            if (toHead.sqrMagnitude < 1e-6f) return;
            toHead.Normalize();
            var upward = Mathf.Abs(Vector3.Dot(toHead, Vector3.up)) > 0.999f ? head.up : Vector3.up;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toHead, upward), turnDegreesPerSecond * Time.deltaTime);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            offset = new Vector3(-settings.sideMetres, settings.dropMetres, settings.distanceMetres);
        }
    }
}
