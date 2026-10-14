// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using CosmicSimulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Picks the hand's look for the room it is in: faint over passthrough, where the real hand is visible
    /// underneath, and more solid in full VR, where it is the only hand there is (D-H3). Lives on the XR rig.
    /// Applied through property blocks so the shared material asset is never written at run time.
    ///
    /// <para>A pinch shows on the hand itself (CS-298): the rim warms toward <see cref="pinchRim"/> and the body
    /// firms up as the index pinch strength rises, because with the sample affordances off nothing else says the
    /// pinch registered. Strength comes from the Meta aim device through an Input System path, so a runtime
    /// without that device simply reads zero.</para>
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

        [SerializeField]
        [Tooltip("Rim colour at a full pinch; the rim blends toward it as the pinch closes.")]
        private Color pinchRim = new Color(1f, 0.93f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Added to the body alpha at a full pinch.")]
        [Range(0f, 0.5f)]
        private float pinchBodyAlpha = 0.15f;

        [SerializeField]
        [Tooltip("Pinch strength below this shows nothing, so a relaxed hand does not glow.")]
        [Range(0f, 0.9f)]
        private float pinchFloor = 0.2f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

        // Created on first use, not in a field initializer: Unity forbids constructing a MaterialPropertyBlock
        // from a MonoBehaviour constructor, and the initializer runs on every prefab load and AddComponent.
        private MaterialPropertyBlock _block;

        private readonly List<SkinnedMeshRenderer> _left = new List<SkinnedMeshRenderer>();
        private readonly List<SkinnedMeshRenderer> _right = new List<SkinnedMeshRenderer>();
        private InputAction _leftPinch, _rightPinch;
        private Look _look;
        private float _leftShown = -1f, _rightShown = -1f, _nextScan;

        private void OnEnable()
        {
            _leftPinch = new InputAction("LeftPinch", InputActionType.Value, "<MetaAimHand>{LeftHand}/pinchStrengthIndex");
            _rightPinch = new InputAction("RightPinch", InputActionType.Value, "<MetaAimHand>{RightHand}/pinchStrengthIndex");
            _leftPinch.Enable();
            _rightPinch.Enable();

            EnvironmentController.ModeChanged += Apply;
            // No event is raised for the opening state; read it.
            var controller = EnvironmentController.Instance;
            Apply(controller != null ? controller.EffectiveMode : EnvironmentMode.Passthrough);
        }

        private void OnDisable()
        {
            EnvironmentController.ModeChanged -= Apply;
            _leftPinch?.Dispose();
            _rightPinch?.Dispose();
            _leftPinch = _rightPinch = null;
        }

        private void Update()
        {
            // The Hand Visualizer can create its meshes after this wakes; look again now and then, never per frame.
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                Scan();
            }

            Show(_left, Strength(_leftPinch), ref _leftShown);
            Show(_right, Strength(_rightPinch), ref _rightShown);
        }

        private void Apply(EnvironmentMode mode)
        {
            _look = mode == EnvironmentMode.FullBlack ? fullVR : passthrough;
            Scan();
            _leftShown = _rightShown = -1f;
            Show(_left, Strength(_leftPinch), ref _leftShown);
            Show(_right, Strength(_rightPinch), ref _rightShown);
        }

        private float Strength(InputAction action)
        {
            var raw = action != null ? action.ReadValue<float>() : 0f;
            return Mathf.Clamp01((raw - pinchFloor) / Mathf.Max(1e-3f, 1f - pinchFloor));
        }

        // Writes only when the shown strength moved, so a still hand costs nothing.
        private void Show(List<SkinnedMeshRenderer> hands, float strength, ref float shown)
        {
            if (Mathf.Abs(strength - shown) < 0.02f || hands.Count == 0)
            {
                return;
            }

            shown = strength;
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            var body = _look.color;
            body.a = Mathf.Clamp01(body.a + pinchBodyAlpha * strength);
            _block.SetColor(ColorId, body);
            _block.SetColor(RimColorId, Color.Lerp(_look.rimColor, pinchRim, strength));
            _block.SetFloat(RimPowerId, _look.rimPower);

            foreach (var hand in hands)
            {
                if (hand != null)
                {
                    hand.SetPropertyBlock(_block);
                }
            }
        }

        private void Scan()
        {
            _left.Clear();
            _right.Clear();

            // Includes inactive: the Hand Visualizer keeps an untracked hand's root off, and both the Meta Quest
            // and Android XR meshes exist whichever one the platform picks.
            foreach (var hand in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (handMaterial != null && hand.sharedMaterial != handMaterial)
                {
                    continue;
                }

                (IsLeft(hand.transform) ? _left : _right).Add(hand);
            }
        }

        // The sample rig names its hand roots "Left ..." and "Right ..."; the nearest named ancestor decides.
        private static bool IsLeft(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                var name = t.name;
                if (name.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }

            return false;
        }
    }
}
