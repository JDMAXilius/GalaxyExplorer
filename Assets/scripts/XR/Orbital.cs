// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Follows the tracked object at a fixed offset. Ported from MRTK v2's Orbital solver.
    /// </summary>
    public class Orbital : Solver
    {
        [SerializeField]
        [Tooltip("The desired orientation of this object. Default sets the object to face the TrackedObject/TargetTransform. CameraFacing sets the object to always face the user.")]
        private SolverOrientationType orientationType = SolverOrientationType.FollowTrackedObject;

        [SerializeField]
        [Tooltip("XYZ offset for this object in relation to the TrackedObject/TargetTransform. Mixing local and world offsets is not recommended.")]
        private Vector3 localOffset = new Vector3(0, -1, 1);

        [SerializeField]
        [Tooltip("XYZ offset for this object in worldspace, best used with the YawOnly orientationType. Mixing local and world offsets is not recommended.")]
        private Vector3 worldOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Lock the rotation to a specified number of steps around the tracked object.")]
        private bool useAngleSteppingForWorldOffset = false;

        [Range(2, 24)]
        [SerializeField]
        [Tooltip("The division of steps this object can tether to. Higher the number, the more snapple steps.")]
        private int tetherAngleSteps = 6;

        public override void SolverUpdate()
        {
            var target = SolverHandler.TransformTarget;
            Vector3 desiredPos = target != null ? target.position : Vector3.zero;

            Quaternion targetRot = target != null ? target.rotation : Quaternion.Euler(0, 1, 0);
            Quaternion yawOnlyRot = Quaternion.Euler(0, targetRot.eulerAngles.y, 0);
            desiredPos += SnapToTetherAngleSteps(targetRot) * localOffset;
            desiredPos += SnapToTetherAngleSteps(yawOnlyRot) * worldOffset;

            GoalPosition = desiredPos;
            GoalRotation = CalculateDesiredRotation(desiredPos);

            UpdateWorkingPositionToGoal();
            UpdateWorkingRotationToGoal();
        }

        private Quaternion SnapToTetherAngleSteps(Quaternion rotationToSnap)
        {
            if (!useAngleSteppingForWorldOffset || SolverHandler.TransformTarget == null)
            {
                return rotationToSnap;
            }

            float stepAngle = 360f / tetherAngleSteps;
            int numberOfSteps = Mathf.RoundToInt(SolverHandler.TransformTarget.eulerAngles.y / stepAngle);

            return Quaternion.Euler(rotationToSnap.eulerAngles.x, stepAngle * numberOfSteps, rotationToSnap.eulerAngles.z);
        }

        private Quaternion CalculateDesiredRotation(Vector3 desiredPos)
        {
            var target = SolverHandler.TransformTarget;
            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            Quaternion desiredRot = Quaternion.identity;

            switch (orientationType)
            {
                case SolverOrientationType.YawOnly:
                    desiredRot = Quaternion.Euler(0f, target != null ? target.eulerAngles.y : 0.0f, 0f);
                    break;
                case SolverOrientationType.Unmodified:
                    desiredRot = transform.rotation;
                    break;
                case SolverOrientationType.CameraAligned:
                    desiredRot = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity;
                    break;
                case SolverOrientationType.FaceTrackedObject:
                    desiredRot = target != null ? Quaternion.LookRotation(target.position - desiredPos) : Quaternion.identity;
                    break;
                case SolverOrientationType.CameraFacing:
                    desiredRot = target != null && cameraTransform != null ? Quaternion.LookRotation(cameraTransform.position - desiredPos) : Quaternion.identity;
                    break;
                case SolverOrientationType.FollowTrackedObject:
                    desiredRot = target != null ? target.rotation : Quaternion.identity;
                    break;
            }

            if (useAngleSteppingForWorldOffset)
            {
                desiredRot = SnapToTetherAngleSteps(desiredRot);
            }

            return desiredRot;
        }
    }
}
