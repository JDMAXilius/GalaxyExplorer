// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using GalaxyExplorer.XR;
using UnityEngine;

namespace GalaxyExplorer
{
    /// <summary>
    /// Opens <see cref="URL"/> in the system browser when selected.
    /// </summary>
    public class Hyperlink : MonoBehaviour, IGEPointerHandler
    {
        [SerializeField]
        private string URL;

        private float _coolDownDuration = 1f;
        private bool _inCoolDown = false;

        public void OpenURL()
        {
            if (!string.IsNullOrEmpty(URL) && !_inCoolDown)
            {
                Application.OpenURL(URL);

                // Selection can arrive from several interactors at once; ignore repeats for a moment.
                StartCoroutine(CoolDown());
            }
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            OpenURL();
            eventData?.Use();
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
        }

        public void OnPointerClicked(GEPointerEventData eventData)
        {
        }

        private IEnumerator CoolDown()
        {
            _inCoolDown = true;

            yield return new WaitForSeconds(_coolDownDuration);

            _inCoolDown = false;
        }

        private void OnDisable()
        {
            _inCoolDown = false;
        }
    }
}
