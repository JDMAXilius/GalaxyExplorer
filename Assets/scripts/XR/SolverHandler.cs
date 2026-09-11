// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Drives the <see cref="Solver"/> components on this GameObject and resolves the transform they track.
    /// Ported from MRTK v2's SolverHandler; tracked controllers and hand joints now come from <see cref="XRInputRig"/>.
    /// </summary>
    public class SolverHandler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tracked object to calculate position and orientation from. If you want to manually override and use a scene object, use the TransformTarget field.")]
        private TrackedObjectType trackedObjectToReference = TrackedObjectType.Head;

        public TrackedObjectType TrackedObjectToReference
        {
            get => trackedObjectToReference;
            set
            {
                if (trackedObjectToReference != value)
                {
                    trackedObjectToReference = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("Add an additional offset of the tracked object to base the solver on. Useful for tracking something like a halo position above your head or off the side of a controller.")]
        private Vector3 additionalOffset;

        public Vector3 AdditionalOffset
        {
            get => additionalOffset;
            set
            {
                additionalOffset = value;
                transformTarget = MakeOffsetTransform(transformTarget);
            }
        }

        [SerializeField]
        [Tooltip("Add an additional rotation on top of the tracked object. Useful for tracking what is essentially the up or right/left vectors.")]
        private Vector3 additionalRotation;

        public Vector3 AdditionalRotation
        {
            get => additionalRotation;
            set
            {
                additionalRotation = value;
                transformTarget = MakeOffsetTransform(transformTarget);
            }
        }

        [SerializeField]
        [Tooltip("Manual override for TrackedObjectToReference if you want to use a scene object. Leave empty if you want to use head, motion-tracked controllers, or motion-tracked hands.")]
        private Transform transformTarget;

        public Transform TransformTarget
        {
            get => transformTarget;
            set => transformTarget = value;
        }

        [SerializeField]
        [Tooltip("Whether or not this SolverHandler calls SolverUpdate() every frame. Only one SolverHandler should manage SolverUpdate().")]
        private bool updateSolvers = true;

        public bool UpdateSolvers
        {
            get => updateSolvers;
            set => updateSolvers = value;
        }

        public Vector3 GoalPosition { get; set; }

        public Quaternion GoalRotation { get; set; }

        public Vector3 GoalScale { get; set; }

        public float DeltaTime { get; set; }

        private bool RequiresOffset => AdditionalOffset.sqrMagnitude != 0 || AdditionalRotation.sqrMagnitude != 0;

        protected readonly List<Solver> solvers = new List<Solver>();

        private float lastUpdateTime;

        private GameObject transformWithOffset;

        private void Awake()
        {
            GoalScale = Vector3.one;
            DeltaTime = Time.deltaTime;
            lastUpdateTime = Time.realtimeSinceStartup;

            solvers.AddRange(GetComponents<Solver>());
        }

        private void Start()
        {
            // TransformTarget overrides TrackedObjectToReference
            if (!transformTarget)
            {
                AttachToNewTrackedObject();
            }
        }

        private void Update()
        {
            DeltaTime = Time.realtimeSinceStartup - lastUpdateTime;
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        private void LateUpdate()
        {
            if (!UpdateSolvers)
            {
                return;
            }

            for (int i = 0; i < solvers.Count; ++i)
            {
                Solver solver = solvers[i];

                if (solver.enabled)
                {
                    solver.SolverUpdate();
                }
            }
        }

        protected void OnDestroy()
        {
            DetachFromCurrentTrackedObject();
        }

        public void RefreshTrackedObject()
        {
            DetachFromCurrentTrackedObject();
            AttachToNewTrackedObject();
        }

        protected virtual void DetachFromCurrentTrackedObject()
        {
            transformTarget = null;

            if (transformWithOffset != null)
            {
                Destroy(transformWithOffset);
                transformWithOffset = null;
            }
        }

        protected virtual void AttachToNewTrackedObject()
        {
            var rig = XRInputRig.Instance;
            switch (TrackedObjectToReference)
            {
                case TrackedObjectType.Head:
                    if (Camera.main != null)
                    {
                        TrackTransform(Camera.main.transform);
                    }
                    break;
                case TrackedObjectType.MotionControllerLeft:
                    TrackTransform(rig ? rig.LeftControllerTransform : null);
                    break;
                case TrackedObjectType.MotionControllerRight:
                    TrackTransform(rig ? rig.RightControllerTransform : null);
                    break;
                case TrackedObjectType.HandJointLeft:
                    TrackTransform(rig ? rig.LeftPalm : null);
                    break;
                case TrackedObjectType.HandJointRight:
                    TrackTransform(rig ? rig.RightPalm : null);
                    break;
            }
        }

        private void TrackTransform(Transform newTrackedTransform)
        {
            if (newTrackedTransform == null)
            {
                return;
            }

            transformTarget = RequiresOffset ? MakeOffsetTransform(newTrackedTransform) : newTrackedTransform;
        }

        private Transform MakeOffsetTransform(Transform parentTransform)
        {
            if (transformWithOffset == null)
            {
                transformWithOffset = new GameObject();
                transformWithOffset.transform.parent = parentTransform;
            }

            transformWithOffset.transform.localPosition = Vector3.Scale(AdditionalOffset, transformWithOffset.transform.localScale);
            transformWithOffset.transform.localRotation = Quaternion.Euler(AdditionalRotation);
            transformWithOffset.name = $"{gameObject.name} on {TrackedObjectToReference} with offset {AdditionalOffset}, {AdditionalRotation}";
            return transformWithOffset.transform;
        }
    }
}
