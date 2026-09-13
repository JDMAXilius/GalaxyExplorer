using System.Collections;
using UnityEngine;

namespace Cosmic
{
    public static class Tween
    {
        public static float EaseOutCubic(float t)
        {
            var u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        // Frame-rate independent exponential approach: smoothSeconds is the time constant, not a duration.
        public static float Smooth(float current, float target, float smoothSeconds, float dt)
        {
            if (smoothSeconds <= 0f)
                return target;
            if (dt <= 0f)
                return current;
            return Mathf.LerpUnclamped(current, target, 1f - Mathf.Exp(-dt / smoothSeconds));
        }

        public static IEnumerator To(Transform t, Vector3 pos, Quaternion rot, Vector3 scale, float seconds, bool local = true)
        {
            if (t == null)
                yield break;
            var fromPos = local ? t.localPosition : t.position;
            var fromRot = local ? t.localRotation : t.rotation;
            var fromScale = t.localScale;
            var elapsed = 0f;
            while (t != null && seconds > 0f && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = EaseOutCubic(elapsed / seconds);
                Write(t, local,
                    Vector3.LerpUnclamped(fromPos, pos, k),
                    Quaternion.SlerpUnclamped(fromRot, rot, k),
                    Vector3.LerpUnclamped(fromScale, scale, k));
                yield return null;
            }
            if (t != null)
                Write(t, local, pos, rot, scale);
        }

        public static IEnumerator Fade(CanvasGroup g, float target, float seconds)
        {
            if (g == null)
                yield break;
            var from = g.alpha;
            var elapsed = 0f;
            while (g != null && seconds > 0f && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = EaseOutCubic(elapsed / seconds);
                g.alpha = Mathf.LerpUnclamped(from, target, k);
                yield return null;
            }
            if (g != null)
                g.alpha = target;
        }

        public static IEnumerator Fade(Material m, int propertyId, float target, float seconds)
        {
            if (m == null)
                yield break;
            var from = m.GetFloat(propertyId);
            var elapsed = 0f;
            while (m != null && seconds > 0f && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = EaseOutCubic(elapsed / seconds);
                m.SetFloat(propertyId, Mathf.LerpUnclamped(from, target, k));
                yield return null;
            }
            if (m != null)
                m.SetFloat(propertyId, target);
        }

        public static void Billboard(Transform t, Transform viewer, bool yawOnly = false)
        {
            if (t == null || viewer == null)
                return;
            var forward = t.position - viewer.position;
            if (yawOnly)
                forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f)
                return;
            t.rotation = Quaternion.LookRotation(forward, yawOnly ? Vector3.up : viewer.up);
        }

        static void Write(Transform t, bool local, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (local)
            {
                t.localPosition = pos;
                t.localRotation = rot;
            }
            else
            {
                t.position = pos;
                t.rotation = rot;
            }
            t.localScale = scale;
        }
    }
}
