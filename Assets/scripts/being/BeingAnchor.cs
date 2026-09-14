using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation.Being
{
    /// <summary>
    /// Where the being sits and which way it points. It keeps station beside the player rather than in front of
    /// them - close enough to talk to, far enough off-axis not to stand in front of whatever they came to look
    /// at - and turns to face the head, so it is always looking at you however you turn.
    ///
    /// <para><b>It yields to a hand.</b> The being can be grabbed and carried like a planet. While a
    /// <c>ManipulationHandler</c> has it, this writes no position at all. On release it does not snap back:
    /// it re-reads its own offset from wherever it was put down and keeps station from there, so moving it
    /// is a decision the player makes once rather than a tug of war.</para>
    /// </summary>
    public class BeingAnchor : MonoBehaviour
    {
        [SerializeField] private BeingSettings settings;

        [Tooltip("How quickly the being turns to face the head, in degrees a second.")]
        [SerializeField] private float turnDegreesPerSecond = 540f;

        private Camera _camera;
        private ManipulationHandler _hands;
        private Vector3 _velocity;
        private bool _placed;
        private bool _wasHeld;

        /// <summary>The station it keeps, in the head's own yaw frame: right, up, forward.</summary>
        private Vector3 _offset;

        private void Awake()
        {
            _hands = GetComponent<ManipulationHandler>();
            _offset = new Vector3(-settings.SideMetres, settings.DropMetres, settings.DistanceMetres);
        }

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

            var held = _hands != null && _hands.IsManipulating;
            if (held)
            {
                // The hand owns the position. Remember nothing yet: the offset is re-read on release, from
                // wherever the player actually let go.
                _wasHeld = true;
            }
            else
            {
                if (_wasHeld)
                {
                    var local = transform.position - head.position;
                    _offset = new Vector3(
                        Vector3.Dot(local, right),
                        Vector3.Dot(local, Vector3.up),
                        Vector3.Dot(local, forward));
                    _wasHeld = false;
                    _velocity = Vector3.zero;
                }

                var target = head.position + right * _offset.x + Vector3.up * _offset.y + forward * _offset.z;
                transform.position = _placed
                    ? Vector3.SmoothDamp(transform.position, target, ref _velocity, settings.FollowSeconds)
                    : target;
                _placed = true;
            }

            Face(head);
        }

        /// <summary>
        /// Turns the being's front to the head. Turned rather than snapped, so a quick look away sweeps it round
        /// instead of teleporting it, and only about the world's up - a being that pitched to follow a player
        /// looking at their feet would read as falling over.
        /// </summary>
        private void Face(Transform head)
        {
            var toHead = head.position - transform.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var wanted = Quaternion.LookRotation(toHead.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, wanted, turnDegreesPerSecond * Time.deltaTime);
        }
    }
}
