// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

namespace GalaxyExplorer
{
    public class AboutSlate : MonoBehaviour
    {
        public Material AboutMaterial;
        public GameObject Slate;
        public GameObject SlateContentParent;
        public float TransitionDuration = 1.0f;

        [Header("Licence page")]
        [SerializeField, Tooltip("Objects that make up the About page proper. Switched off while the licence page is showing.")]
        private GameObject[] aboutPage = new GameObject[0];

        [SerializeField, Tooltip("Objects that make up the full-licence page, off by default. Leave empty and the licence toggle does nothing.")]
        private GameObject[] licencePage = new GameObject[0];

        private bool _licencesAreShowing;
        private bool _aboutIsActive;
        private ZoomInOut _zoomInOut;
        private Collider[] _otherCollidersInScene;
        private Renderer[] _hyperlinkRenderers;

        private void Start()
        {
            AboutMaterial.SetFloat("_TransitionAlpha", 0f);
            _aboutIsActive = false;

            // Include inactive children: the licence page starts switched off, and anything missing from this cache
            // never gets its alpha driven and so renders at full opacity the moment it is switched on.
            _hyperlinkRenderers = SlateContentParent.GetComponentsInChildren<Renderer>(true);
            SetHyperlinksTransitionAplha((0f));

            _zoomInOut = FindObjectOfType<ZoomInOut>();

            transform.localScale = transform.localScale * GalaxyExplorerManager.SlateScaleFactor;
        }

        private void SceneManager_sceneLoaded(UnityEngine.SceneManagement.Scene arg0, UnityEngine.SceneManagement.LoadSceneMode arg1)
        {
            Hide();
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

            AboutMaterial.SetFloat("_TransitionAlpha", 1f);
            SetHyperlinksTransitionAplha((1f));
        }

        public void ToggleAboutButton()
        {
            if (_aboutIsActive)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// Swaps the About page for the full MIT notice for the inherited Galaxy Explorer code. Hooked to a link on
        /// the slate; the licence has to be readable in the app, not only in the repository.
        /// </summary>
        public void ToggleLicencePage()
        {
            ShowLicences(!_licencesAreShowing);
        }

        public void ShowLicences(bool show)
        {
            if (licencePage == null || licencePage.Length == 0)
            {
                return; // no licence page in this prefab yet; leave the About page exactly as it was
            }

            _licencesAreShowing = show;

            if (aboutPage != null)
            {
                foreach (GameObject page in aboutPage)
                {
                    if (page != null) { page.SetActive(!show); }
                }
            }

            foreach (GameObject page in licencePage)
            {
                if (page != null) { page.SetActive(show); }
            }
        }

        private void Show()
        {
            ShowLicences(false); // the slate always opens on the About page, whatever it was closed on

            EnableOtherCollidersInScene(false);

            transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2f;
            transform.rotation = Camera.main.transform.rotation;

            gameObject.SetActive(true);

            StartCoroutine(AnimateToOpacity(1));
        }

        private void Hide()
        {
            StartCoroutine(AnimateToOpacity(0));
        }

        private IEnumerator AnimateToOpacity(float target)
        {
            var timeLeft = TransitionDuration;

            Slate.SetActive(true);
            SlateContentParent.SetActive(true);

            while (timeLeft > 0)
            {
                float transitionAlpha = Mathf.Lerp(target, 1 - target, timeLeft / TransitionDuration);
                AboutMaterial.SetFloat("_TransitionAlpha", transitionAlpha);
                SetHyperlinksTransitionAplha((transitionAlpha));

                yield return null;

                timeLeft -= Time.deltaTime;
            }

            AboutMaterial.SetFloat("_TransitionAlpha", target);
            SetHyperlinksTransitionAplha(target);

            if (target > 0)
            {
                SlateContentParent.SetActive(true);
                gameObject.SetActive(true);
                _aboutIsActive = true;
            }
            else
            {
                SlateContentParent.SetActive(false);
                Slate.SetActive(false);
                gameObject.SetActive(false);
                _aboutIsActive = false;
                EnableOtherCollidersInScene(true);
            }
        }

        private void SetHyperlinksTransitionAplha(float transitionAplha)
        {
            if (_hyperlinkRenderers == null) { return; }

            foreach (Renderer renderer in _hyperlinkRenderers)
            {
                Color newColor = renderer.material.GetColor("_FaceColor"); ;
                newColor.a = transitionAplha;
                renderer.material.SetColor("_FaceColor", newColor);
            }
        }

        private void EnableOtherCollidersInScene(bool enable)
        {
            if (!enable)
            {
                _otherCollidersInScene = null;

                // These colliders need to be tracked until the about slate gets disabled and the colliders are re-enabled, since this code would otherwise look for the colliders of the next scene and disable them instead
                if (_zoomInOut.GetNextScene != null)
                {
                    _otherCollidersInScene = _zoomInOut.GetNextScene.GetComponentsInChildren<Collider>();
                }
            }

            if (_otherCollidersInScene != null)
            {
                foreach (Collider collider in _otherCollidersInScene)
                {
                    collider.enabled = enable;
                }
            }
        }
    }
}