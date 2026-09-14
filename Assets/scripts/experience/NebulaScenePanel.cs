// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Puts a place's own panel up in a scene that has no <see cref="ExperienceDirector"/> to do it.
    ///
    /// <para>In the app the director binds the panel to the module and shows it as the place opens. The seven
    /// nebula scenes have no director - they are one object and a camera - so the panel would sit there bound
    /// to nothing with its alpha at zero, which is what <see cref="InfoPanel.Awake"/> sets it to. This is the
    /// stand-in: same prefab, same Bind call, same text, so what you read in a nebula scene is exactly what
    /// the app will show when the player travels there.</para>
    ///
    /// <para>It runs in edit mode as well, because a panel you have to press play to read is no use when the
    /// question is whether the text is right.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class NebulaScenePanel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The panel to fill. Normally the InfoPanel on this same object.")]
        private InfoPanel panel;

        [SerializeField]
        [Tooltip("The destination this scene is showing. Its title and prose are what the panel reads.")]
        private ExperienceModule module;

        [SerializeField]
        [Tooltip("What the panel turns to face. The scene camera, so it is readable from where you start.")]
        private Transform faces;

        [SerializeField]
        [Tooltip("What the panel is about. InfoPanel parks and scales itself relative to this; without one " +
                 "it collapses to zero scale at the origin, which is what happened the first time.")]
        private Transform subject;

        private bool _bound;

        private void OnEnable()
        {
            _bound = false;
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            if (panel == null || module == null)
            {
                return;
            }

            if (!_bound)
            {
                panel.Bind(module);

                // InfoPanel positions and sizes itself against a target. Without one it sat at the origin at
                // zero scale - present, bound, with the right title on it, and completely invisible.
                if (subject != null)
                {
                    panel.SetTarget(subject);
                }

                panel.Show();
                _bound = true;
            }

            // InfoPanel.Awake sets the group to zero and its own Update fades it in - neither of which happens
            // outside play mode, so in the editor the alpha is written here directly. In play the panel's own
            // fade takes over from the same value and nothing jumps.
            if (!Application.isPlaying)
            {
                var group = panel.GetComponent<CanvasGroup>();
                if (group != null && !Mathf.Approximately(group.alpha, 1f))
                {
                    group.alpha = 1f;
                }
            }

            if (faces != null)
            {
                // Turned to the reader rather than aimed at them: a panel that pitches up and down as the
                // camera rises is harder to read than one that only ever yaws.
                var toward = faces.position - transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 1e-4f)
                {
                    transform.rotation = Quaternion.LookRotation(-toward.normalized, Vector3.up);
                }
            }
        }

        /// <summary>Wired by NebulaSceneBuilder.</summary>
        public void Configure(InfoPanel target, ExperienceModule destination, Transform lookAt,
            Transform about)
        {
            panel = target;
            module = destination;
            faces = lookAt;
            subject = about;
            _bound = false;
        }
    }
}
