// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using CosmicSimulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// The hand's look on the XR rig: faint over passthrough where the real hand shows, more solid in full VR,
    /// warming with the pinch, and occluding content in passthrough. Property blocks keep the shared material unwritten.
    /// </summary>
    // The pinch glow is the only pinch feedback with the sample affordances off. Strength comes from the Meta aim
    // device by Input System path, so a runtime without it just reads zero.
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

        [SerializeField]
        [Tooltip("Real hands hide the content behind them while the room shows through (CS-288).")]
        private bool occludeContent = true;

        private const string OccluderPath = "hands/hand_occluder_material";

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
        private Material _occluder;
        private readonly List<SkinnedMeshRenderer> _found = new List<SkinnedMeshRenderer>();
        private readonly List<Material> _materials = new List<Material>();
        private bool _occlude;

        /// <summary>Whether real hands hide the content behind them in passthrough. Applies at once.</summary>
        public bool OccludeContent
        {
            get => occludeContent;
            set
            {
                occludeContent = value;
                if (!isActiveAndEnabled)
                {
                    return;
                }

                var controller = EnvironmentController.Instance;
                Apply(controller != null ? controller.EffectiveMode : EnvironmentMode.Passthrough);
            }
        }
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
            foreach (var hand in _left) if (hand != null) SetOccluder(hand, false);
            foreach (var hand in _right) if (hand != null) SetOccluder(hand, false);
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
            if (_occluder == null)
            {
                _occluder = Resources.Load<Material>(OccluderPath);
            }

            // Where the room really shows: not in full VR, and not on a build that has no passthrough at all (the
            // Windows player runs the headset in VR whatever the mode says), where the hole would read as black.
            _occlude = occludeContent && mode != EnvironmentMode.FullBlack &&
                       ExperienceModeManager.PassthroughAvailable && _occluder != null;
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
            GetComponentsInChildren(true, _found);
            foreach (var hand in _found)
            {
                if (handMaterial != null && hand.sharedMaterial != handMaterial)
                {
                    continue;
                }

                (IsLeft(hand.transform) ? _left : _right).Add(hand);
                SetOccluder(hand, _occlude);
            }
        }

        // An extra material past the last submesh redraws that submesh, so a one-submesh hand is drawn again whole,
        // depth only. Only the renderer's own array changes, never the shared material assets.
        private void SetOccluder(SkinnedMeshRenderer hand, bool on)
        {
            if (_occluder == null)
            {
                return;
            }

            hand.GetSharedMaterials(_materials);
            if (on == _materials.Contains(_occluder))
            {
                return;
            }

            var materials = hand.sharedMaterials;
            var at = Array.IndexOf(materials, _occluder);

            if (on)
            {
                Array.Resize(ref materials, materials.Length + 1);
                materials[materials.Length - 1] = _occluder;
            }
            else
            {
                var kept = new List<Material>(materials);
                kept.RemoveAt(at);
                materials = kept.ToArray();
            }

            hand.sharedMaterials = materials;
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
