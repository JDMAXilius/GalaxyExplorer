// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace GalaxyExplorer.XR
{
    // These enums replace the MRTK v2 types of the same names. Their values are identical so that data
    // serialized by the old MRTK components (prefabs, scenes) keeps its meaning after the migration.

    /// <summary>
    /// Tracked object a <see cref="SolverHandler"/> follows.
    /// </summary>
    public enum TrackedObjectType
    {
        Head = 0,
        MotionControllerLeft,
        MotionControllerRight,
        HandJointLeft,
        HandJointRight
    }

    [System.Flags]
    public enum Handedness : byte
    {
        None = 0 << 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Both = Left | Right,
        Other = 1 << 2,
        Any = Other | Left | Right,
    }

    public enum PivotAxis
    {
        XY,
        Y,
        X,
        Z,
        XZ,
        YZ,
        Free
    }

    public enum SolverOrientationType
    {
        FollowTrackedObject = 0,
        FaceTrackedObject,
        YawOnly,
        Unmodified,
        CameraFacing,
        CameraAligned
    }
}
