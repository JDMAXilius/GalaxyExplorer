using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation.Being
{
    [Serializable]
    public class BeingContext
    {
        public string place;
        public string placeName;
        public string layout;
        public string[] pulled;
        public string platform;

        public static BeingContext Capture()
        {
            var director = ExperienceDirector.Instance;
            var current = director != null ? director.Current : null;
            var rig = UnityEngine.Object.FindAnyObjectByType<LayoutRig>();
            var pulled = new List<string>();
            foreach (var force in UnityEngine.Object.FindObjectsByType<ForceSolver>(FindObjectsSortMode.None))
            {
                if (force.ForceState == ForceSolver.State.Free || force.ForceState == ForceSolver.State.Manipulation)
                {
                    pulled.Add(force.name.Replace("body_", ""));
                }
            }

            return new BeingContext
            {
                place = current != null ? current.Id : "",
                placeName = current != null ? current.DisplayName : "",
                layout = rig != null && rig.Current != null ? rig.Current.Id : "",
                pulled = pulled.ToArray(),
                platform = UnityEngine.XR.XRSettings.isDeviceActive ? "headset" : "desktop",
            };
        }
    }
}
