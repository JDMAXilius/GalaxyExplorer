// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Base class for solvers: components that compute a goal pose each frame and move their transform toward it.
    /// Ported from MRTK v2's Solver (same serialized fields, so existing solver data is preserved).
    /// </summary>
    [RequireComponent(typeof(SolverHandler))]
    public abstract class Solver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("If true, the position and orientation will be calculated, but not applied, for other components to use")]
        private bool updateLinkedTransform = false;

        [SerializeField]
        [Tooltip("Position lerp multiplier")]
        private float moveLerpTime = 0.1f;

        [SerializeField]
        [Tooltip("Rotation lerp multiplier")]
        private float rotateLerpTime = 0.1f;

        [SerializeField]
        [Tooltip("Scale lerp multiplier")]
        private float scaleLerpTime = 0;

        [SerializeField]
        [Tooltip("If true, the Solver will respect the object's original scale values")]
        private bool maintainScale = true;

        [SerializeField]
        [Tooltip("Working output is smoothed if true. Otherwise, snapped")]
        private bool smoothing = true;

        [SerializeField]
        [Tooltip("If > 0, this solver will deactivate after this much time, even if the state is still active")]
        private float lifetime = 0;

        private float currentLifetime;

        [SerializeField]
        [HideInInspector]
        protected SolverHandler SolverHandler;

        protected Vector3 GoalPosition;
        protected Quaternion GoalRotation;
        protected Vector3 GoalScale;

        public Vector3 WorkingPosition
        {
            get => updateLinkedTransform ? SolverHandler.GoalPosition : transform.position;
            protected set
            {
                if (updateLinkedTransform)
                {
                    SolverHandler.GoalPosition = value;
                }
                else
                {
                    transform.position = value;
                }
            }
        }

        public Quaternion WorkingRotation
        {
            get => updateLinkedTransform ? SolverHandler.GoalRotation : transform.rotation;
            protected set
            {
                if (updateLinkedTransform)
                {
                    SolverHandler.GoalRotation = value;
                }
                else
                {
                    transform.rotation = value;
                }
            }
        }

        public Vector3 WorkingScale
        {
            get => updateLinkedTransform ? SolverHandler.GoalScale : transform.localScale;
            protected set
            {
                if (updateLinkedTransform)
                {
                    SolverHandler.GoalScale = value;
                }
                else
                {
                    transform.localScale = value;
                }
            }
        }

        protected virtual void OnValidate()
        {
            if (SolverHandler == null)
            {
                SolverHandler = GetComponent<SolverHandler>();
            }
        }

        protected virtual void Awake()
        {
            if (SolverHandler == null)
            {
                SolverHandler = GetComponent<SolverHandler>();
            }

            if (updateLinkedTransform && SolverHandler == null)
            {
                Debug.LogError("No SolverHandler component found on " + name + " when UpdateLinkedTransform was set to true! Disabling UpdateLinkedTransform.");
                updateLinkedTransform = false;
            }

            GoalScale = maintainScale ? transform.localScale : Vector3.one;
        }

        protected virtual void OnEnable()
        {
            if (SolverHandler != null)
            {
                SnapGoalTo(SolverHandler.GoalPosition, SolverHandler.GoalRotation);
            }

            currentLifetime = 0;
        }

        public abstract void SolverUpdate();

        public void SolverUpdateEntry()
        {
            currentLifetime += SolverHandler.DeltaTime;

            if (lifetime > 0 && currentLifetime >= lifetime)
            {
                enabled = false;
                return;
            }

            SolverUpdate();
        }

        public virtual void SnapTo(Vector3 position, Quaternion rotation)
        {
            SnapGoalTo(position, rotation);

            WorkingPosition = position;
            WorkingRotation = rotation;
        }

        public virtual void SnapGoalTo(Vector3 position, Quaternion rotation)
        {
            GoalPosition = position;
            GoalRotation = rotation;
        }

        public virtual void AddOffset(Vector3 offset)
        {
            GoalPosition += offset;
        }

        public static Vector3 SmoothTo(Vector3 source, Vector3 goal, float deltaTime, float lerpTime)
        {
            return Vector3.Lerp(source, goal, lerpTime.Equals(0.0f) ? 1f : deltaTime / lerpTime);
        }

        public static Quaternion SmoothTo(Quaternion source, Quaternion goal, float deltaTime, float lerpTime)
        {
            return Quaternion.Slerp(source, goal, lerpTime.Equals(0.0f) ? 1f : deltaTime / lerpTime);
        }

        public void UpdateWorkingToGoal()
        {
            if (smoothing)
            {
                WorkingPosition = SmoothTo(WorkingPosition, GoalPosition, SolverHandler.DeltaTime, moveLerpTime);
                WorkingRotation = SmoothTo(WorkingRotation, GoalRotation, SolverHandler.DeltaTime, rotateLerpTime);
                WorkingScale = SmoothTo(WorkingScale, GoalScale, SolverHandler.DeltaTime, scaleLerpTime);
            }
            else
            {
                WorkingPosition = GoalPosition;
                WorkingRotation = GoalRotation;
                WorkingScale = GoalScale;
            }
        }

        public void UpdateWorkingPositionToGoal()
        {
            WorkingPosition = smoothing ? SmoothTo(WorkingPosition, GoalPosition, SolverHandler.DeltaTime, moveLerpTime) : GoalPosition;
        }

        public void UpdateWorkingRotationToGoal()
        {
            WorkingRotation = smoothing ? SmoothTo(WorkingRotation, GoalRotation, SolverHandler.DeltaTime, rotateLerpTime) : GoalRotation;
        }

        public void UpdateWorkingScaleToGoal()
        {
            WorkingScale = smoothing ? SmoothTo(WorkingScale, GoalScale, SolverHandler.DeltaTime, scaleLerpTime) : GoalScale;
        }
    }
}
