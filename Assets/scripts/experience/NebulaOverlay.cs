// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The per-frame half of a nebula overlay: it turns a stack of flat cards into something that reads as a
    /// volume (GDD 4.3, Technical Overview 7.6).
    ///
    /// Three jobs. It points the whole stack at the player, so the cards are always seen face-on and the depth
    /// spread always runs away from the eye rather than sideways. It slides the cards along that axis from a
    /// single spacing figure, so the spread can be tuned in one place and still be right after the player has
    /// scaled the nebula. And it turns each card slowly about its own normal at a slightly different rate, in
    /// alternating directions, which is what makes the layers separate as you move your head — the parallax
    /// alone is too subtle at arm's length.
    ///
    /// It also owns the overlay's one outside dependency. The soft-depth fade in
    /// <c>CosmicSimulation/NebulaCard</c> needs the camera depth texture, which the built-in forward renderer
    /// produces only on request and which costs a depth prepass. Requesting it here, on enable, and putting it
    /// back on disable means the Quest pays for it only while a nebula is actually open, and the shader keyword
    /// is turned on in the same breath so the depth branch is never live without the texture behind it.
    ///
    /// Runs after <c>ManipulationHandler</c> has moved the root and before <see cref="BlackHalo"/>
    /// (execution order 60) places itself behind us.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public class NebulaOverlay : MonoBehaviour
    {
        private const string SoftDepthKeyword = "NEBULA_SOFT_DEPTH";

        // The depth texture is a property of the camera, not of an overlay, so the request is shared. Only one
        // destination is ever open at a time, but a grow-in can overlap a tear-down by a frame.
        private static int _depthRequests;
        private static Camera _depthCamera;
        private static DepthTextureMode _restoreMode;

        [SerializeField]
        [Tooltip("The cards, nearest the player first. Built by NebulaPrefabBuilder.")]
        private Transform[] cards = Array.Empty<Transform>();

        [SerializeField]
        [Tooltip("Metres between neighbouring cards along the view axis, at the prefab's own scale.")]
        private float cardSpacing = 0.1f;

        [SerializeField]
        [Tooltip("Degrees per second the first card turns about its own normal. Later cards alternate direction and turn a little faster.")]
        private float spinDegreesPerSecond = 0.5f;

        [SerializeField]
        [Tooltip("Keep the stack facing the player. Off leaves the prefab's authored orientation alone.")]
        private bool faceThePlayer = true;

        [SerializeField]
        [Tooltip("Ask the camera for a depth texture so the cards can fade where they intersect other geometry. Costs a depth prepass while the overlay is open; with it off the cards still get the near and edge fades.")]
        private bool requestCameraDepth = true;

        private Camera _camera;
        private float _elapsed;
        private bool _holdsDepth;

        /// <summary>How many cards this nebula is made of.</summary>
        public int CardCount => cards != null ? cards.Length : 0;

        /// <summary>Front-to-back depth of the stack in metres, at the prefab's own scale.</summary>
        public float DepthSpread => CardCount < 2 ? 0f : (CardCount - 1) * cardSpacing;

        private void OnEnable()
        {
            _camera = Camera.main;

            if (requestCameraDepth)
            {
                _depthRequests++;
                _holdsDepth = true;
            }

            RefreshDepthRequest();
        }

        private void OnDisable()
        {
            if (_holdsDepth)
            {
                _holdsDepth = false;
                _depthRequests = Mathf.Max(0, _depthRequests - 1);
            }

            RefreshDepthRequest();
        }

        private void LateUpdate()
        {
            if (cards == null || cards.Length == 0)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }

                // The camera may not have existed when we were enabled; the request is still outstanding.
                if (_holdsDepth && _depthCamera == null)
                {
                    RefreshDepthRequest();
                }
            }

            _elapsed += Time.deltaTime;

            // Local +Z points away from the player, so a card's offset along it is its depth into the cloud.
            if (faceThePlayer)
            {
                var toCamera = _camera.transform.position - transform.position;
                if (toCamera.sqrMagnitude > 1e-6f)
                {
                    transform.rotation = Quaternion.LookRotation(-toCamera.normalized, _camera.transform.up);
                }
            }

            var centre = (cards.Length - 1) * 0.5f;
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                {
                    continue;
                }

                card.localPosition = new Vector3(0f, 0f, (i - centre) * cardSpacing);
                card.localRotation = Quaternion.Euler(0f, 0f, _elapsed * SpinRate(i));
            }
        }

        /// <summary>Alternating directions and slightly different speeds; identical rates would look like one card.</summary>
        private float SpinRate(int index) =>
            spinDegreesPerSecond * (index % 2 == 0 ? 1f : -1f) * (1f + 0.35f * index);

        private void RefreshDepthRequest()
        {
            if (_depthRequests <= 0)
            {
                ReleaseCamera();
                Shader.DisableKeyword(SoftDepthKeyword);
                return;
            }

            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
            {
                return; // retried from LateUpdate once there is a camera
            }

            if (_depthCamera != camera)
            {
                ReleaseCamera();
                _depthCamera = camera;
                _restoreMode = camera.depthTextureMode;
                camera.depthTextureMode |= DepthTextureMode.Depth;
            }

            Shader.EnableKeyword(SoftDepthKeyword);
        }

        private static void ReleaseCamera()
        {
            if (_depthCamera != null)
            {
                _depthCamera.depthTextureMode = _restoreMode;
            }

            _depthCamera = null;
        }
    }
}
