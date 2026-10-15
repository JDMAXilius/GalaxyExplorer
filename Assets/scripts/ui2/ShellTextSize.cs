// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The text-size setting (<see cref="InfoPanel.TextScale"/>) reaching the shell's own text (CS-377), plus a small
    /// lift for the smallest text in the headset. Every shell text sits in a fixed box laid out from the design
    /// frame, so its size is not simply multiplied, which would run lines into their neighbours: it may grow toward
    /// the wanted size only as far as its box holds it (TMP auto-size between the design size and the wanted one),
    /// and never drops below the design size. A paragraph in a roomy box grows the whole way; a single line in a
    /// box cut to its height barely moves.
    ///
    /// Left alone: text a layout group places (the card's, which <see cref="Ui2Explore"/> scales and reflows
    /// itself), text that already fits itself to its box (the guide's suggestions), and the navigation labels and
    /// small chips the owner hands in, where one label growing beside a neighbour that cannot reads as a mistake.
    /// A scrolling transcript has no box to keep to, so it is scaled outright.
    /// </summary>
    public sealed class ShellTextSize
    {
        // Text under this many design pixels is about 0.7 degrees or less at the shell's distance, the bottom of
        // Meta's comfortable range, so in the headset it gets HeadsetLift on top of the setting. Both provisional
        // until U2-P0's headset distance test.
        private const float SmallPx = 18f;
        private const float HeadsetLift = 1.15f;

        private readonly List<(TMP_Text text, float size)> _boxed = new List<(TMP_Text, float)>();
        private readonly List<(TMP_Text text, float size)> _scrolled = new List<(TMP_Text, float)>();
        private float _scale = -1f;
        private bool _headset;

        /// <summary>Records the design size of every text under <paramref name="root"/> that this may resize.</summary>
        public void Collect(Transform root, IEnumerable<Transform> fixedLayout)
        {
            _boxed.Clear();
            _scrolled.Clear();
            var skip = new List<Transform>();
            foreach (var t in fixedLayout)
            {
                if (t != null)
                {
                    skip.Add(t);
                }
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.enableAutoSizing || Under(text.transform, skip))
                {
                    continue;
                }

                var parent = text.transform.parent;
                if (parent != null && parent.GetComponent<LayoutGroup>() != null)
                {
                    continue;
                }

                var scrolls = parent != null && parent.GetComponent<RectMask2D>() != null &&
                              text.textWrappingMode != TextWrappingModes.NoWrap;
                (scrolls ? _scrolled : _boxed).Add((text, text.fontSize));
            }

            _scale = -1f;
        }

        /// <summary>Resizes the collected text; cheap when neither the setting nor the platform has changed.</summary>
        public void Apply(float scale, bool headset)
        {
            if (Mathf.Approximately(scale, _scale) && headset == _headset)
            {
                return;
            }

            _scale = scale;
            _headset = headset;
            foreach (var (text, size) in _boxed)
            {
                if (text == null)
                {
                    continue;
                }

                var wanted = size * scale * (headset && size < SmallPx ? HeadsetLift : 1f);

                // Auto-size off first: TMP only takes a new base size while it is off, and turning it off leaves the
                // last fitted size behind, so the design size is put back before it is (maybe) turned on again.
                text.enableAutoSizing = false;
                text.fontSize = size;
                text.fontSizeMin = size;
                text.fontSizeMax = Mathf.Max(size, wanted);
                text.enableAutoSizing = wanted > size + 0.01f;
            }

            foreach (var (text, size) in _scrolled)
            {
                if (text != null)
                {
                    text.fontSize = size * scale;
                }
            }
        }

        private static bool Under(Transform t, List<Transform> roots)
        {
            foreach (var root in roots)
            {
                if (t.IsChildOf(root))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
