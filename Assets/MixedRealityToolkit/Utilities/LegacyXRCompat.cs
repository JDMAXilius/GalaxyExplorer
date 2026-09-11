// Unity 6 upgrade shim. Unity removed UnityEngine.Experimental.XR.Boundary and the XRDevice
// tracking-space API along with legacy XR. These stand-ins keep MRTK's boundary code compiling
// and route the calls to XRInputSubsystem, which is what XR Plug-in Management exposes today.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Microsoft.MixedReality.Toolkit.Utilities
{
    /// <summary>Drop-in for the removed <c>UnityEngine.Experimental.XR.Boundary</c>.</summary>
    public static class LegacyBoundary
    {
        public enum Type
        {
            PlayArea,
            TrackedArea
        }

        /// No modern equivalent; kept so existing assignments compile.
        public static bool visible { get; set; }

        public static bool TryGetGeometry(List<Vector3> geometry, Type boundaryType)
        {
            geometry.Clear();
            var subsystem = LegacyXRDevice.ActiveInputSubsystem();
            return subsystem != null && subsystem.TryGetBoundaryPoints(geometry) && geometry.Count > 0;
        }
    }

    /// <summary>Stand-in for the removed XRDevice tracking-space methods.</summary>
    public static class LegacyXRDevice
    {
        private static readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

        internal static XRInputSubsystem ActiveInputSubsystem()
        {
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var subsystem in subsystems)
            {
                if (subsystem.running) { return subsystem; }
            }
            return null;
        }

        public static bool IsRoomScale()
        {
            var subsystem = ActiveInputSubsystem();
            return subsystem != null && (subsystem.GetTrackingOriginMode() & TrackingOriginModeFlags.Floor) != 0;
        }

        public static bool TrySetRoomScale(bool roomScale)
        {
            var subsystem = ActiveInputSubsystem();
            return subsystem != null && subsystem.TrySetTrackingOriginMode(
                roomScale ? TrackingOriginModeFlags.Floor : TrackingOriginModeFlags.Device);
        }
    }
}
