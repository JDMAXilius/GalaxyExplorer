// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Keeps a GameObject oriented towards the user. Ported from MRTK v2's Billboard.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Specifies the axis about which the object will rotate.")]
        [SerializeField]
        private PivotAxis pivotAxis = PivotAxis.XY;

        [Tooltip("Specifies the target we will orient to. If no target is specified, the main camera will be used.")]
        [SerializeField]
        private Transform targetTransform;

        public PivotAxis PivotAxis
        {
            get => pivotAxis;
            set => pivotAxis = value;
        }

        public Transform TargetTransform => targetTransform;

        private void OnEnable()
        {
            if (targetTransform == null && Camera.main != null)
            {
                targetTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (targetTransform == null)
            {
                return;
            }

            Vector3 directionToTarget = targetTransform.position - transform.position;
            bool useCameraAsUpVector = true;

            switch (pivotAxis)
            {
                case PivotAxis.X:
                    directionToTarget.x = 0.0f;
                    useCameraAsUpVector = false;
                    break;
                case PivotAxis.Y:
                    directionToTarget.y = 0.0f;
                    useCameraAsUpVector = false;
                    break;
                case PivotAxis.Z:
                    directionToTarget.x = 0.0f;
                    directionToTarget.y = 0.0f;
                    break;
                case PivotAxis.XY:
                    useCameraAsUpVector = false;
                    break;
                case PivotAxis.XZ:
                    directionToTarget.x = 0.0f;
                    break;
                case PivotAxis.YZ:
                    directionToTarget.y = 0.0f;
                    break;
            }

            // If we are right next to the camera the rotation is undefined.
            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                return;
            }

            if (useCameraAsUpVector && Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(-directionToTarget, Camera.main.transform.up);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(-directionToTarget);
            }
        }
    }
}
