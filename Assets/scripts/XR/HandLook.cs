// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using CosmicSimulation;
using UnityEngine;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Picks the hand's look for the room it is in: faint over passthrough, where the real hand is visible
    /// underneath, and more solid in full VR, where it is the only hand there is (D-H3). Lives on the XR rig.
    /// Applied through property blocks so the shared material asset is never written at run time.
    /// </summary>
    public class HandLook : MonoBehaviour
    {
        [Serializable]
        public struct Look
        {
            public Color color;
            public Color rimColor;
            public float rimPower;
        }

        [SerializeField]
        [Tooltip("The hand material (CosmicSimulation/HandOutline). Identifies which renderers under the rig are hands, " +
                 "and keeps the shader in the build. Empty = every skinned mesh under the rig.")]
        private Material handMaterial;

        [SerializeField]
        private Look passthrough = new Look
        {
            color = new Color(0.55f, 0.75f, 1f, 0.18f),
            rimColor = new Color(0.6f, 0.85f, 1f, 1f),
            rimPower = 2.5f,
        };

        [SerializeField]
        private Look fullVR = new Look
        {
            color = new Color(0.55f, 0.75f, 1f, 0.35f),
            rimColor = new Color(0.6f, 0.85f, 1f, 1f),
            rimPower = 2.5f,
        };

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

        // Created on first use, not in a field initializer: Unity forbids constructing a MaterialPropertyBlock
        // from a MonoBehaviour constructor, and the initializer runs on every prefab load and AddComponent.
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            EnvironmentController.ModeChanged += Apply;
            // No event is raised for the opening state; read it.
            var controller = EnvironmentController.Instance;
            Apply(controller != null ? controller.EffectiveMode : EnvironmentMode.Passthrough);
        }

        private void OnDisable()
        {
            EnvironmentController.ModeChanged -= Apply;
        }

        private void Apply(EnvironmentMode mode)
        {
            var look = mode == EnvironmentMode.FullBlack ? fullVR : passthrough;
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(ColorId, look.color);
            _block.SetColor(RimColorId, look.rimColor);
            _block.SetFloat(RimPowerId, look.rimPower);

            // Includes inactive: the Hand Visualizer keeps an untracked hand's root off, and both the Meta Quest
            // and Android XR meshes exist whichever one the platform picks.
            foreach (var hand in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (handMaterial == null || hand.sharedMaterial == handMaterial)
                {
                    hand.SetPropertyBlock(_block);
                }
            }
        }
    }
}
