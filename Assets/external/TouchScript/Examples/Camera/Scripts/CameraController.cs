// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using UnityEngine;
using TouchScript.Gestures.TransformGestures;
using UnityEngine.XR;

namespace TouchScript.Examples.CameraControl
{
    /// <exclude />
    public class CameraController : MonoBehaviour
    {
        public ScreenTransformGesture TwoFingerMoveGesture;
        public ScreenTransformGesture ManipulationGesture;
        public float PanSpeed = 200f;
        public float RotationSpeed = 200f;
        public float ZoomSpeed = 10f;

        public Transform Pivot = null;
        public Transform EntityToMove = null;

        private void Awake()
        {
            //pivot = transform.Find("Pivot");
            //cam = transform.Find("Pivot/Camera");
        }

        private void Start()
        {
            if (!XRSettings.isDeviceActive) // Unity 6: XRDevice.isPresent removed
            {
                this.enabled = false;
            }
        }

        private void OnEnable()
        {
            TwoFingerMoveGesture.Transformed += twoFingerTransformHandler;
            ManipulationGesture.Transformed += manipulationTransformedHandler;
        }

        private void OnDisable()
        {
            TwoFingerMoveGesture.Transformed -= twoFingerTransformHandler;
            ManipulationGesture.Transformed -= manipulationTransformedHandler;
        }

        // Galaxy Explorer: the mouse is handled by DesktopMouseInput; this controller only reacts to touch.
        private static bool IsMouseDriven(ScreenTransformGesture gesture)
        {
            foreach (var pointer in gesture.ActivePointers)
            {
                if (pointer.Type == TouchScript.Pointers.Pointer.PointerType.Mouse)
                {
                    return true;
                }
            }
            return false;
        }

        private void manipulationTransformedHandler(object sender, System.EventArgs e)
        {
            if (IsMouseDriven(ManipulationGesture))
            {
                return;
            }

            var rotation = Quaternion.Euler(ManipulationGesture.DeltaPosition.y/Screen.height*RotationSpeed,
                -ManipulationGesture.DeltaPosition.x/Screen.width*RotationSpeed,
                ManipulationGesture.DeltaRotation);
            Pivot.localRotation *= rotation;
            //cam.transform.localPosition += Vector3.forward*(ManipulationGesture.DeltaScale - 1f)*ZoomSpeed;
            Pivot.transform.localPosition += Vector3.forward * (ManipulationGesture.DeltaScale - 1f) * ZoomSpeed;
        }

        private void twoFingerTransformHandler(object sender, System.EventArgs e)
        {
            if (IsMouseDriven(TwoFingerMoveGesture))
            {
                return;
            }

            //pivot.localPosition += pivot.rotation*TwoFingerMoveGesture.DeltaPosition*PanSpeed;
            //pivot.localRotation *= Quaternion.Euler(TwoFingerMoveGesture.DeltaRotation, 0.0f, 0.0f);
            EntityToMove.localPosition += Pivot.rotation*TwoFingerMoveGesture.DeltaPosition*PanSpeed;
            Pivot.localRotation *= Quaternion.Euler(TwoFingerMoveGesture.DeltaRotation, 0.0f, 0.0f);
        }
    }
}