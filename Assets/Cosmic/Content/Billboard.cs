using UnityEngine;

namespace Cosmic
{
    public class Billboard : MonoBehaviour
    {
        [SerializeField] Vector3 offsetEulerDegrees;
        [SerializeField] bool yawOnly;
        [SerializeField] Camera target;

        Camera cam;

        public Vector3 OffsetEulerDegrees { get => offsetEulerDegrees; set => offsetEulerDegrees = value; }

        void LateUpdate()
        {
            var view = target != null ? target : cam != null ? cam : cam = Camera.main;
            if (view == null) return;
            Tween.Billboard(transform, view.transform, yawOnly);
            if (offsetEulerDegrees != Vector3.zero) transform.rotation *= Quaternion.Euler(offsetEulerDegrees);
        }
    }
}
