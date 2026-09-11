// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// Pushes a button's moving visuals in and lets them spring back when the button is pressed.
    /// Replaces MRTK v2's PressableButton (same field names, so existing prefab references carry over).
    /// </summary>
    public class GEPressVisual : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The object that is being pushed.")]
        private GameObject movingButtonVisuals = null;

        [SerializeField]
        [Tooltip("How far (world meters) the visuals travel into the button when pressed.")]
        private float pressDepth = 0.006f;

        [SerializeField]
        [Tooltip("Speed for retracting the moving button visuals on release.")]
        private float returnRate = 25.0f;

        // Same event names as MRTK's PressableButton, so their listeners (e.g. the click sound) carry over.
        public UnityEvent TouchBegin = new UnityEvent();
        public UnityEvent TouchEnd = new UnityEvent();
        public UnityEvent ButtonPressed = new UnityEvent();
        public UnityEvent ButtonReleased = new UnityEvent();

        private Vector3 _restLocalPosition;
        private Coroutine _pressRoutine;

        private void Awake()
        {
            if (movingButtonVisuals != null)
            {
                _restLocalPosition = movingButtonVisuals.transform.localPosition;
            }
        }

        private void OnDisable()
        {
            if (movingButtonVisuals != null)
            {
                movingButtonVisuals.transform.localPosition = _restLocalPosition;
            }

            _pressRoutine = null;
        }

        public void Press()
        {
            TouchBegin.Invoke();
            ButtonPressed.Invoke();
            if (movingButtonVisuals == null || !isActiveAndEnabled)
            {
                ButtonReleased.Invoke();
                TouchEnd.Invoke();
                return;
            }

            if (_pressRoutine != null)
            {
                StopCoroutine(_pressRoutine);
            }

            _pressRoutine = StartCoroutine(PressRoutine());
        }

        private IEnumerator PressRoutine()
        {
            var visuals = movingButtonVisuals.transform;
            var parent = visuals.parent;
            // Push along the button's forward axis (into the button), expressed in the visuals' parent space.
            var pushDirection = parent != null ? parent.InverseTransformDirection(transform.forward) : transform.forward;
            var pushedLocalPosition = _restLocalPosition + pushDirection * pressDepth;

            visuals.localPosition = pushedLocalPosition;
            yield return new WaitForSeconds(0.08f);

            while ((visuals.localPosition - _restLocalPosition).sqrMagnitude > 1e-10f)
            {
                visuals.localPosition = Vector3.Lerp(visuals.localPosition, _restLocalPosition, 1f - Mathf.Exp(-returnRate * Time.deltaTime));
                yield return null;
            }

            visuals.localPosition = _restLocalPosition;
            _pressRoutine = null;
            ButtonReleased.Invoke();
            TouchEnd.Invoke();
        }
    }
}
