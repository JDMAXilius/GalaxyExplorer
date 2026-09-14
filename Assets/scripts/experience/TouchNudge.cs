// Licensed under the MIT License. See LICENSE in the project root for license information.

using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// A body that answers a touch (CS-173). Brush a fingertip, a hand ray or the mouse across a planet or a moon
    /// without grabbing it and the sphere turns a little with the stroke, then springs back to where it was:
    /// right to left turns it right to left, left to right the other way. Not a grab and not a spin: the body
    /// stays exactly where it is and ends exactly as it started, which is what makes it read as a nudge.
    ///
    /// <para><b>How it turns without fighting anything.</b> The visual under a body is spun on its axis every frame
    /// by <c>ConstantRotateAxis</c>, a body at rest is snapped onto its home anchor by <c>ForceSolver</c>, and a held
    /// body is posed by <c>ManipulationHandler</c>. All of that keeps working because this never writes a rotation:
    /// it turns the body's tilt node by the <i>change</i> in nudge angle each frame, about the world's up, so the
    /// axial spin composes with it and when the spring lands back on zero the net rotation is exactly none.</para>
    ///
    /// <para><b>What counts as a touch.</b> Focus from any pointer while the body is at rest (Root or Free). A body
    /// that is being pulled or held ignores the stroke - the hand is moving the whole body then, not brushing it.
    /// The stroke is the pointer's movement across the body along the camera's right axis: the fingertip itself
    /// for a near hand, the point on the ray level with the body for a hand ray or the mouse. One radius of
    /// travel turns the body <see cref="degreesPerRadius"/>, capped at <see cref="maxDegrees"/>.</para>
    ///
    /// <para>Added to every <c>ForceSolver</c> at run time (see <c>ForceSolver.Awake</c>), so the planets, the
    /// Sun and every moon get it without a prefab edit; one placed on a prefab by hand keeps its own tuning.</para>
    /// </summary>
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

            // Half the body's width, measured once per touch: bodies change size between arrangements and
            // grow when pulled, and a stroke is judged against the size it has now.
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
            // Leave the body exactly as it was: undo whatever part of the nudge is still applied.
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

            // The stroke.
            if (_pointer != null && _pointer.IsActive && AtRest() && _camera != null && TryLateral(out var x))
            {
                if (_hasLast)
                {
                    // Surface moving with the finger: a stroke to the right (+x) turns the front face to the
                    // right, which is a negative turn about up.
                    _angle = Mathf.Clamp(_angle - (x - _last) / _radius * degreesPerRadius, -maxDegrees, maxDegrees);
                }

                _last = x;
                _hasLast = true;
            }
            else
            {
                _hasLast = false;
            }

            // The spring back to rest, running whether or not a finger is on the body - so a finger that stops
            // moving feels the body ease back under it.
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
