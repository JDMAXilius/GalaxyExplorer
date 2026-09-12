// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>Where one body sits in an arrangement, relative to the experience's content root.</summary>
    [Serializable]
    public struct LayoutSlot
    {
        [Tooltip("Body id, matching BodyInfo.Id.")]
        public string BodyId;

        public Vector3 LocalPosition;
        public Vector3 LocalEuler;

        [Tooltip("The body's diameter in metres. Body prefabs are normalised to a 1 m diameter, so this is " +
                 "also the uniform scale to set. It is a real size, not a multiplier of whatever the prefab " +
                 "happens to be authored at.")]
        public float Scale;
    }

    /// <summary>
    /// A named arrangement the player can switch to from a dock pop-up: Solar Row against Relative Size,
    /// Orbital view against Realistic spacing. Also what "put everything back" restores.
    /// </summary>
    [CreateAssetMenu(menuName = "Cosmic Simulation/Layout Preset", fileName = "layout_preset")]
    public class LayoutPreset : ScriptableObject
    {
        [Tooltip("Stable key, e.g. solar_row.")]
        public string Id;

        [Tooltip("Button label in the pop-up, e.g. Solar Row.")]
        public string DisplayName;

        [Tooltip("Second line on the button, e.g. One line, one size.")]
        public string SecondLine;

        [Tooltip("Seconds the bodies take to animate into this arrangement.")]
        public float TransitionSeconds = 0.8f;

        public LayoutSlot[] Slots = Array.Empty<LayoutSlot>();

        /// <summary>The slot for a body, or null when this arrangement does not place it.</summary>
        public LayoutSlot? Find(string bodyId)
        {
            if (Slots == null)
            {
                return null;
            }

            foreach (var slot in Slots)
            {
                if (slot.BodyId == bodyId)
                {
                    return slot;
                }
            }

            return null;
        }
    }
}
