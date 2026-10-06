// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Gives an interactable the <see cref="XRPokeFilter"/> a fingertip needs to select it. Both hand poke
    /// interactors on the rig require one (XRI's <c>requirePokeFilter</c>), so an interactable without it can be
    /// hovered by a finger and never pressed - which is how every dock control came to answer a ray and ignore a
    /// touch (CS-276). <c>UiPrefabBuilder</c> calls this for the prefabs; <c>HintCards</c>, <c>LabelButton</c>
    /// and <c>Host</c> call it for what they assemble at run time.
    /// </summary>
    public static class PokeSupport
    {
        /// <summary>Degrees off the face normal within which a finger's approach still counts as a press.</summary>
        public const float PressAngleDegrees = 45f;

        public static XRPokeFilter AddPokeFilter(GameObject target, Collider collider, XRBaseInteractable interactable)
        {
            if (target == null || collider == null || interactable == null)
            {
                return null;
            }

            var filter = target.GetComponent<XRPokeFilter>();
            if (filter == null)
            {
                filter = target.AddComponent<XRPokeFilter>();
            }

            var data = new PokeThresholdData
            {
                // XRI evaluates PokeAxis.Z along the interactable's -forward: the finger arrives from the face
                // side and travels +Z into it. That is the side every panel here shows the player (DockTile lifts
                // a hovered tile along -Z for the same reason), and the value the legacy hand menu's filters use.
                pokeDirection = PokeAxis.Z,
                pokeAngleThreshold = PressAngleDegrees,
            };

            if (collider is BoxCollider)
            {
                // A flat face: press straight in. No depth offset of its own - the interactor's 5 mm offset is
                // what lets a tracked fingertip trigger a 2 mm plate without sinking through it.
                data.enablePokeAngleThreshold = true;
                data.interactionDepthOffset = 0f;
            }
            else
            {
                // A solid (the being's sphere): any touch is a press, from any side. With the angle check off the
                // filter still measures depth along the axis from the collider's centre, so the offset moves the
                // trigger plane out to the surface; a finger from the side or behind is past it at once.
                data.enablePokeAngleThreshold = false;
                data.interactionDepthOffset = HalfDepth(collider);
            }

            // Configuration before the references: each setter re-runs the filter's Setup, and the last to land
            // is the one that initialises the poke logic with everything in place.
            filter.pokeConfiguration = new PokeThresholdDatumProperty(data);
            filter.pokeCollider = collider;
            filter.pokeInteractable = interactable;
            return filter;
        }

        /// <summary>
        /// World half-extent along the collider's local Z, from the collider's own numbers rather than
        /// <c>Collider.bounds</c>, which is empty until the physics scene has seen the object.
        /// </summary>
        private static float HalfDepth(Collider collider)
        {
            var scale = collider.transform.lossyScale;
            switch (collider)
            {
                case SphereCollider sphere:
                    return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                case CapsuleCollider capsule:
                    return (capsule.direction == 2 ? capsule.height * 0.5f : capsule.radius) * Mathf.Abs(scale.z);
                case BoxCollider box:
                    return box.size.z * 0.5f * Mathf.Abs(scale.z);
                default:
                    return 0f;
            }
        }
    }
}
