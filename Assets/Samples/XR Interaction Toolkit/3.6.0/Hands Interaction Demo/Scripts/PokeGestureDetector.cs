// LOCAL CHANGE (Cosmic Simulation XR, CS-279): this XRI 3.6.0 sample is edited in place. The poke gesture now
// only counts while the hand's poke interactor is hovering something pokeable. The rig uses the gesture to switch
// the near-far interactor's far casting off, and the pointing pose is also the pose a player adopts to aim the
// ray at a tile a metre away — so as shipped, pointing at a far button killed the ray that would have pressed it.
// Re-importing the sample overwrites this; diff before accepting.

using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace UnityEngine.XR.Interaction.Toolkit.Samples.Hands
{
    /// <summary>
    /// Behavior that provides events for when an <see cref="XRHand"/> starts and ends a poke gesture. The gesture is
    /// detected if the index finger is extended and the middle, ring, and little fingers are curled in, and (local
    /// change) the hand's poke interactor is hovering a target.
    /// </summary>
    public class PokeGestureDetector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Which hand to check for the poke gesture.")]
#if XR_HANDS_1_1_OR_NEWER
        Handedness m_Handedness;
#else
        int m_Handedness;
#endif

        [SerializeField]
        [Tooltip("Local change (CS-279): the poke interactor on this hand. The gesture only counts while it is " +
                 "hovering something pokeable, so pointing at a far tile keeps the ray. Found among this " +
                 "object's children when left empty.")]
        XRPokeInteractor m_PokeInteractor;

        [SerializeField]
        [Tooltip("Called when the hand has started a poke gesture.")]
        UnityEvent m_PokeGestureStarted;

        [SerializeField]
        [Tooltip("Called when the hand has ended a poke gesture.")]
        UnityEvent m_PokeGestureEnded;

#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem m_Subsystem;
        bool m_IsPoking;

        static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
#endif

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void OnEnable()
        {
            if (m_PokeInteractor == null)
                m_PokeInteractor = GetComponentInChildren<XRPokeInteractor>(true);

#if XR_HANDS_1_1_OR_NEWER
            SubsystemManager.GetSubsystems(s_Subsystems);
            if (s_Subsystems.Count == 0)
                return;

            m_Subsystem = s_Subsystems[0];
            m_Subsystem.updatedHands += OnUpdatedHands;
#else
            Debug.LogError("Script requires XR Hands (com.unity.xr.hands) package. Install using Window > Package Manager or click Fix on the related issue in Edit > Project Settings > XR Plug-in Management > Project Validation.", this);
#endif
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        protected void OnDisable()
        {
#if XR_HANDS_1_1_OR_NEWER
            if (m_Subsystem == null)
                return;

            m_Subsystem.updatedHands -= OnUpdatedHands;
            m_Subsystem = null;
#endif
        }

#if XR_HANDS_1_1_OR_NEWER
        void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
        {
            var wasPoking = m_IsPoking;
            switch (m_Handedness)
            {
                case Handedness.Left:
                    if (!HasUpdateSuccessFlag(updateSuccessFlags, XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints))
                        return;

                    var leftHand = subsystem.leftHand;
                    m_IsPoking = IsIndexExtended(leftHand) && IsMiddleGrabbing(leftHand) && IsRingGrabbing(leftHand) &&
                        IsLittleGrabbing(leftHand);
                    break;
                case Handedness.Right:
                    if (!HasUpdateSuccessFlag(updateSuccessFlags, XRHandSubsystem.UpdateSuccessFlags.RightHandJoints))
                        return;

                    var rightHand = subsystem.rightHand;
                    m_IsPoking = IsIndexExtended(rightHand) && IsMiddleGrabbing(rightHand) && IsRingGrabbing(rightHand) &&
                        IsLittleGrabbing(rightHand);
                    break;
            }

            // Local change (CS-279): the pose alone is not a poke. Without a poke interactor to ask, the
            // sample's original behavior stands.
            if (m_PokeInteractor != null)
                m_IsPoking = m_IsPoking && m_PokeInteractor.isActiveAndEnabled && m_PokeInteractor.hasHover;

            if (m_IsPoking && !wasPoking)
                StartPokeGesture();
            else if (!m_IsPoking && wasPoking)
                EndPokeGesture();
        }

        /// <summary>
        /// Determines whether one or more bit fields are set in the flags.
        /// Non-boxing version of <c>HasFlag</c> for <see cref="XRHandSubsystem.UpdateSuccessFlags"/>.
        /// </summary>
        /// <param name="successFlags">The flags enum instance.</param>
        /// <param name="successFlag">The flag to check if set.</param>
        /// <returns>Returns <see langword="true"/> if the bit field or bit fields are set, otherwise returns <see langword="false"/>.</returns>
        static bool HasUpdateSuccessFlag(XRHandSubsystem.UpdateSuccessFlags successFlags, XRHandSubsystem.UpdateSuccessFlags successFlag)
        {
            return (successFlags & successFlag) == successFlag;
        }

        /// <summary>
        /// Returns true if the given hand's index finger tip is farther from the wrist than the index intermediate joint.
        /// </summary>
        /// <param name="hand">Hand to check for the required pose.</param>
        /// <returns>True if the given hand's index finger tip is farther from the wrist than the index intermediate joint, false otherwise.</returns>
        static bool IsIndexExtended(XRHand hand)
        {
            if (!(hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose) &&
                  hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tipPose) &&
                  hand.GetJoint(XRHandJointID.IndexIntermediate).TryGetPose(out var intermediatePose)))
            {
                return false;
            }

            var wristToTip = tipPose.position - wristPose.position;
            var wristToIntermediate = intermediatePose.position - wristPose.position;
            return wristToTip.sqrMagnitude > wristToIntermediate.sqrMagnitude;
        }

        /// <summary>
        /// Returns true if the given hand's middle finger tip is closer to the wrist than the middle proximal joint.
        /// </summary>
        /// <param name="hand">Hand to check for the required pose.</param>
        /// <returns>True if the given hand's middle finger tip is closer to the wrist than the middle proximal joint, false otherwise.</returns>
        static bool IsMiddleGrabbing(XRHand hand)
        {
            if (!(hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose) &&
                  hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out var tipPose) &&
                  hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out var proximalPose)))
            {
                return false;
            }

            var wristToTip = tipPose.position - wristPose.position;
            var wristToProximal = proximalPose.position - wristPose.position;
            return wristToProximal.sqrMagnitude >= wristToTip.sqrMagnitude;
        }

        /// <summary>
        /// Returns true if the given hand's ring finger tip is closer to the wrist than the ring proximal joint.
        /// </summary>
        /// <param name="hand">Hand to check for the required pose.</param>
        /// <returns>True if the given hand's ring finger tip is closer to the wrist than the ring proximal joint, false otherwise.</returns>
        static bool IsRingGrabbing(XRHand hand)
        {
            if (!(hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose) &&
                  hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out var tipPose) &&
                  hand.GetJoint(XRHandJointID.RingProximal).TryGetPose(out var proximalPose)))
            {
                return false;
            }

            var wristToTip = tipPose.position - wristPose.position;
            var wristToProximal = proximalPose.position - wristPose.position;
            return wristToProximal.sqrMagnitude >= wristToTip.sqrMagnitude;
        }

        /// <summary>
        /// Returns true if the given hand's little finger tip is closer to the wrist than the little proximal joint.
        /// </summary>
        /// <param name="hand">Hand to check for the required pose.</param>
        /// <returns>True if the given hand's little finger tip is closer to the wrist than the little proximal joint, false otherwise.</returns>
        static bool IsLittleGrabbing(XRHand hand)
        {
            if (!(hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose) &&
                  hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out var tipPose) &&
                  hand.GetJoint(XRHandJointID.LittleProximal).TryGetPose(out var proximalPose)))
            {
                return false;
            }

            var wristToTip = tipPose.position - wristPose.position;
            var wristToProximal = proximalPose.position - wristPose.position;
            return wristToProximal.sqrMagnitude >= wristToTip.sqrMagnitude;
        }

        void StartPokeGesture()
        {
            m_IsPoking = true;
            m_PokeGestureStarted.Invoke();
        }

        void EndPokeGesture()
        {
            m_IsPoking = false;
            m_PokeGestureEnded.Invoke();
        }
#endif
    }
}
