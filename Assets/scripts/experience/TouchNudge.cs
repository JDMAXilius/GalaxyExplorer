// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A pointer brushing across a body at rest turns it a little with the stroke, then springs it back to exactly where it was.
    /// Added to every <c>ForceSolver</c> at run time (<c>ForceSolver.Awake</c>); one placed on a prefab keeps its own tuning.
    /// </summary>
    // Never writes a rotation, only applies the change in angle each frame about world up, so ConstantRotateAxis's
    // spin, ForceSolver and ManipulationHandler all compose with it and the spring ends at a net rotation of zero.
    [DisallowMultipleComponent]
    public class TouchNudge : MonoBehaviour, IGEFocusHandler
    {
        [SerializeField]
        [Tooltip("Degrees the body turns for a stroke one radius long across its face.")]
        private float degreesPerRadius = 70f;

        [SerializeField]
        [Tooltip("The furthest a nudge can turn the body, in degrees, however long the stroke.")]
        private float maxDegrees = 40f;

        [SerializeField]
        [Tooltip("How hard the body is pulled back to rest. Higher snaps back faster.")]
        private float stiffness = 70f;

        [SerializeField]
        [Tooltip("How quickly the return settles. Below 2*sqrt(stiffness) it overshoots a little on the way " +
                 "back, which is the bounce that makes it feel like something was touched.")]
        private float damping = 11f;

        [SerializeField]
        [Tooltip("The node turned by the nudge. Left empty, the body's *_tilt node is used, or failing that its " +
                 "first child - never the body root itself, which the force solver owns.")]
        private Transform target;

        private ForceSolver _body;
        private Camera _camera;
        private GEPointer _pointer;
        private bool _hasLast;
        private float _last;
        private float _radius = 0.05f;

        private float _angle;
        private float _velocity;
        private float _applied;

        /// <summary>The current nudge, in degrees. Zero at rest.</summary>
        public float Angle => _angle;

        private void Awake()
        {
            _body = GetComponentInParent<ForceSolver>();
            _camera = Camera.main;

            if (target == null)
            {
                target = FindTarget();
            }

            if (target == null)
            {
                enabled = false;
            }
        }

        private Transform FindTarget()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name.EndsWith("_tilt", System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        public void OnFocusEnter(GEFocusEventData eventData)
        {
            _pointer = eventData.Pointer;
            _hasLast = false;

            // Measured per touch, not once: bodies change size between arrangements and grow when pulled.
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                var e = renderer.bounds.extents;
                _radius = Mathf.Max(0.01f, Mathf.Max(e.x, e.y, e.z));
            }
        }

        public void OnFocusExit(GEFocusEventData eventData)
        {
            if (_pointer == null || eventData.Pointer == _pointer)
            {
                _pointer = null;
                _hasLast = false;
            }
        }

        private void OnDisable()
        {
            if (target != null && Mathf.Abs(_applied) > 0.0001f)
            {
                target.Rotate(Vector3.up, -_applied, Space.World);
            }

            _angle = _velocity = _applied = 0f;
            _pointer = null;
            _hasLast = false;
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (_pointer != null && _pointer.IsActive && AtRest() && _camera != null && TryLateral(out var x))
            {
                if (_hasLast)
                {
                    // The surface follows the finger: a stroke to +x turns the front face right, a negative turn about up.
                    _angle = Mathf.Clamp(_angle - (x - _last) / _radius * degreesPerRadius, -maxDegrees, maxDegrees);
                }

                _last = x;
                _hasLast = true;
            }
            else
            {
                _hasLast = false;
            }

            // The spring runs even under a finger, so a finger that stops moving feels the body ease back.
            var acceleration = -stiffness * _angle - damping * _velocity;
            _velocity += acceleration * dt;
            _angle += _velocity * dt;

            if (_pointer == null && Mathf.Abs(_angle) < 0.01f && Mathf.Abs(_velocity) < 0.01f)
            {
                _angle = 0f;
                _velocity = 0f;
            }

            var delta = _angle - _applied;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                target.Rotate(Vector3.up, delta, Space.World);
                _applied = _angle;
            }
        }

        // A pulled or held body ignores strokes: the hand is moving the whole body then, not brushing it.
        private bool AtRest()
        {
            if (_body == null)
            {
                return true;
            }

            var state = _body.ForceState;
            return state == ForceSolver.State.Root || state == ForceSolver.State.Free || state == ForceSolver.State.None;
        }

        /// <summary>Where the pointer is across the body, in metres along the camera's right axis.</summary>
        private bool TryLateral(out float x)
        {
            x = 0f;
            var centre = transform.position;
            var attach = _pointer.AttachTransform;
            if (attach == null)
            {
                return false;
            }

            Vector3 point;
            if (_pointer.IsNear(centre) || !_pointer.CanCastFar)
            {
                point = attach.position;
            }
            else
            {
                var origin = _pointer.RayOrigin != null ? _pointer.RayOrigin.position : _camera.transform.position;
                var direction = _pointer.IsMouse
                    ? attach.position - origin
                    : (_pointer.RayOrigin != null ? _pointer.RayOrigin.forward : _camera.transform.forward);
                if (direction.sqrMagnitude < 1e-8f)
                {
                    return false;
                }

                direction.Normalize();
                point = origin + direction * Mathf.Max(0f, Vector3.Dot(centre - origin, direction));
            }

            x = Vector3.Dot(point - centre, _camera.transform.right);
            return true;
        }
    }
}
