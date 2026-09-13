// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Puts this object's centre where the player's head is, once, when it appears.
    ///
    /// <para><b>Why a nebula needs this and a galaxy does not.</b> Content is spawned where content is always
    /// spawned: on the anchor the player placed during the intro, a couple of metres in front of them. That is
    /// right for a thing you look at — a galaxy on a table, a solar system you lean over — and wrong for a
    /// thing you are supposed to be standing inside. Without this a nebula is a ball of gas an arm's length
    /// away: you can see it is three-dimensional, and you are still outside it.</para>
    ///
    /// <para><b>Once, not every frame.</b> Following the head would mean the gas moves with you, which is the
    /// exact cue that tells a player they are looking at something painted on the inside of their own eyes
    /// rather than standing in a place. Placed once, the cloud stays put in the room and the player walks
    /// about inside it, which is what gives it parallax and therefore depth.</para>
    ///
    /// <para>Only the position is taken. Rotation stays as authored, because the nebula's orientation carries
    /// the plate's own up, and height is taken with the rest: the middle of a nebula is the middle, not the
    /// middle at floor level.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CentreOnViewer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Metres ahead of the head to place the centre. Zero puts the player exactly in the middle; a " +
                 "small positive number leaves the densest gas just in front of them instead of inside their face.")]
        private float aheadMetres;

        private void OnEnable() => StartCoroutine(Centre());

        private IEnumerator Centre()
        {
            // A frame's grace: the rig's camera may not have been given its tracked pose yet on the first
            // frame of a place, and centring on a camera still at the origin would put the nebula on the floor.
            yield return null;

            var view = Camera.main;
            if (view == null)
            {
                Debug.LogWarning("CentreOnViewer: no Camera.main, so this stays where it was authored.", this);
                yield break;
            }

            var head = view.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : head.forward;

            transform.position = head.position + forward * aheadMetres;
        }
    }
}
