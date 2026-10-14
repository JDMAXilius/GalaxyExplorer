// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// Where a place's content and its panel should sit (CS-347): centring fresh content on the content root, fitting
    /// a whole arrangement inside the desktop view (CS-171), and keeping a panel on screen. Pure measurement; the
    /// <see cref="ExperienceDirector"/> decides when, and owns any easing.
    /// </summary>
    public static class ContentFramer
    {
        /// <summary>Content already centred within this distance is left alone, so authored framing is never nudged.</summary>
        private const float CentringDeadzone = 0.02f;

        /// <summary>Nothing is fitted nearer than this: a moon a hand's length away is not a view.</summary>
        private const float MinFitDistanceMetres = 0.6f;

        /// <summary>A panel whose anchor is nearer the screen's edge than this share of it is moved into view.</summary>
        private const float PanelViewportMargin = 0.08f;

        /// <summary>Where such a panel goes: centred, this far up the view.</summary>
        private const float PanelAboveViewportY = 0.8f;

        /// <summary>
        /// Offsets a fresh content prefab so the middle of what it shows sits on the content root, as scene content
        /// does, and returns half its width in metres so a panel can park beside it (zero when nothing was measured).
        /// </summary>
        // Measured rather than per-place knowledge: Solar Row is authored in room coordinates (1.2 m up). Content with
        // nothing to measure, or already within CentringDeadzone, is untouched.
        public static float Centre(Transform content)
        {
            // A place the player stands inside (the Galaxies shell) is already centred on them; framing would push
            // it two metres out and the panel off screen (owner's direction).
            if (SurroundsViewer(content))
            {
                return 0f;
            }

            if (content == null || !TryMeasureContent(content, out var measured))
            {
                return 0f;
            }

            // The root faces the player, so world-axis extents say nothing about own width; the combined horizontal
            // extent over-estimates, the safe side for a panel clearance.
            var span = new Vector2(measured.extents.x, measured.extents.z).magnitude;

            // The fresh instance sits on the content root, so this offset puts the middle there.
            var offset = content.position - measured.center;
            if (offset.sqrMagnitude >= CentringDeadzone * CentringDeadzone)
            {
                content.position += offset;
            }

            return span;
        }

        /// <summary>
        /// Desktop only: the local position that pushes the content straight away from the camera until every slot of
        /// its arrangement is in view, filling at most <paramref name="margin"/> of the view. False when it should stay put.
        /// </summary>
        // A monitor camera never turns, so a 1.9 m row at 2 m is cut off; in a headset the player turns (GDD 4.1).
        public static bool TryFitTarget(Transform content, float margin, out Vector3 localTarget)
        {
            localTarget = default;

            // A place the player stands inside (Galaxies, Cosmic Web) is left exactly as it is (owner's direction).
            if (content == null || UnityEngine.XR.XRSettings.isDeviceActive || SurroundsViewer(content))
            {
                return false;
            }

            var camera = Camera.main;
            if (camera == null || !TryFrameCorners(content, out var corners))
            {
                return false;
            }

            // In camera space without its scale: centre on the line of sight at the distance where the widest
            // corner touches the margin. tan(half-angle) turns depth into visible half-width.
            var view = camera.transform;
            var toView = Quaternion.Inverse(view.rotation);
            var centre = Vector3.zero;
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = toView * (corners[i] - view.position);
                centre += corners[i] / corners.Length;
            }

            var tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * margin;
            var tanHorizontal = tanVertical * Mathf.Max(0.1f, camera.aspect);
            var distance = MinFitDistanceMetres;
            foreach (var corner in corners)
            {
                var r = corner - centre;
                distance = Mathf.Max(distance, Mathf.Abs(r.x) / tanHorizontal - r.z, Mathf.Abs(r.y) / tanVertical - r.z);
            }

            var offset = view.rotation * (new Vector3(0f, 0f, distance) - centre);
            if (offset.sqrMagnitude < CentringDeadzone * CentringDeadzone)
            {
                return false;
            }

            localTarget = content.parent != null
                ? content.parent.InverseTransformPoint(content.position + offset)
                : content.position + offset;
            return true;
        }

        /// <summary>
        /// On a monitor the fitted place can push a panel off screen; such a panel moves above the view's middle at the same depth.
        /// </summary>
        public static void KeepInView(Transform anchor)
        {
            var camera = Camera.main;
            if (camera == null || UnityEngine.XR.XRSettings.isDeviceActive)
            {
                return;
            }

            var v = camera.WorldToViewportPoint(anchor.position);
            if (v.z > 0f && v.x > PanelViewportMargin && v.x < 1f - PanelViewportMargin &&
                v.y > PanelViewportMargin && v.y < 1f - PanelViewportMargin)
            {
                return;
            }

            anchor.position = camera.ViewportToWorldPoint(new Vector3(0.5f, PanelAboveViewportY, Mathf.Max(v.z, MinFitDistanceMetres)));
        }

        private static bool SurroundsViewer(Transform content)
        {
            var field = content != null ? content.GetComponentInChildren<GalaxyField>(true) : null;
            return field != null && field.CentresOnViewer;
        }

        /// <summary>
        /// World corners of what a place shows: an arrangement's target box from its preset slots, else its renderers.
        /// </summary>
        private static bool TryFrameCorners(Transform content, out Vector3[] corners)
        {
            corners = new Vector3[8];
            var rig = content.GetComponentInChildren<LayoutRig>(true);
            Bounds box;
            Transform space = null;
            if (rig != null && rig.TryGetLayoutBounds(rig.PendingOrCurrent, out box))
            {
                space = rig.transform;
            }
            else if (!TryMeasureRenderers(content, out box))
            {
                return false;
            }

            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? box.min.x : box.max.x,
                    (i & 2) == 0 ? box.min.y : box.max.y,
                    (i & 4) == 0 ? box.min.z : box.max.z);
                corners[i] = space != null ? space.TransformPoint(corner) : corner;
            }

            return true;
        }

        /// <summary>
        /// The content's world centre and rough size; false when nothing is measurable. Only the centre is exact.
        /// </summary>
        private static bool TryMeasureContent(Transform content, out Bounds measured)
        {
            // The rig's authored home anchors beat geometry, and are right before its first Start.
            var rig = content.GetComponentInChildren<LayoutRig>(true);
            if (rig != null && rig.TryGetArrangementBounds(out measured))
            {
                return true;
            }

            return TryMeasureRenderers(content, out measured);
        }

        private static bool TryMeasureRenderers(Transform content, out Bounds measured)
        {
            measured = default;
            var found = false;

            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Effects and text: halos are wider than their body, and panels sit at origin until first shown.
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer ||
                    r is SpriteRenderer || r.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                var bounds = r.bounds;
                if (bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                if (found)
                {
                    measured.Encapsulate(bounds);
                }
                else
                {
                    measured = bounds;
                    found = true;
                }
            }

            return found;
        }
    }
}
