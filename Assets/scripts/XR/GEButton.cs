// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyExplorer.XR
{
    /// <summary>
    /// A 3D button pressed by poking it with a finger/controller, selecting it with a ray (pinch or trigger),
    /// or clicking it with the mouse (via <see cref="MouseInputEventRouter"/>). Replaces MRTK v2's Interactable
    /// for the app's buttons; <see cref="OnClick"/> keeps the name, so existing click listeners carry over.
    /// </summary>
    public class GEButton : MonoBehaviour, IGEPointerHandler
    {
        private const float CoolDownSeconds = 0.3f;

        public bool Enabled = true;

        public UnityEvent OnClick = new UnityEvent();

        [SerializeField]
        [Tooltip("Played at the button when it is clicked.")]
        private AudioClip clickSound = null;

        private bool _coolingDown;
        private GEPressVisual _pressVisual;

        private void Awake()
        {
            _pressVisual = GetComponent<GEPressVisual>();
        }

        private void OnDisable()
        {
            _coolingDown = false;
        }

        /// <summary>Clicks the button as if it had been pressed.</summary>
        public void Click()
        {
            if (!Enabled || !isActiveAndEnabled || _coolingDown)
            {
                return;
            }

            if (_pressVisual != null)
            {
                _pressVisual.Press();
            }

            if (clickSound != null && AudioService.Instance != null)
            {
                AudioService.Instance.PlayClip(clickSound, transform);
            }

            OnClick.Invoke();
            StartCoroutine(CoolDown());
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            // A poke fires as soon as the finger pushes the button in, like a physical button.
            if (eventData?.Pointer != null && eventData.Pointer.IsPoke)
            {
                Click();
                eventData.Use();
            }
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
        }

        public void OnPointerClicked(GEPointerEventData eventData)
        {
            // Rays click on release, so a pinch can be cancelled by moving off the button.
            if (eventData?.Pointer == null || !eventData.Pointer.IsPoke)
            {
                Click();
                eventData?.Use();
            }
        }

        private IEnumerator CoolDown()
        {
            _coolingDown = true;
            yield return new WaitForSeconds(CoolDownSeconds);
            _coolingDown = false;
        }
    }
}
