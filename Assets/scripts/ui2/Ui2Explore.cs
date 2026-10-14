// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The exploring half of the UI 2.0 shell (docs/design/v2, U2-P3): the photo-first information card, the
    /// top picker and the breadcrumb. First slice: the card describes the current place, the picker lists the
    /// galaxies, and the legacy scene panel is kept down while this is up. Bodies, moons and the two facts
    /// arrive with the selection service and the photo/fact data.
    /// </summary>
    public class Ui2Explore : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField] private RectTransform card = null;
        [SerializeField] private GameObject photoRoot = null;
        [SerializeField] private Image photo = null;
        [SerializeField] private TMP_Text title = null;
        [SerializeField] private TMP_Text body = null;
        [SerializeField] private UiButton listen = null;

        [SerializeField]
        [Tooltip("Place id for each entry of Photos.")]
        private string[] photoIds = Array.Empty<string>();

        [SerializeField] private Sprite[] photos = Array.Empty<Sprite>();

        [Header("Picker")]
        [SerializeField] private GameObject picker = null;
        [SerializeField] private UiButton[] tiles = Array.Empty<UiButton>();
        [SerializeField] private string[] tileIds = Array.Empty<string>();
        [SerializeField] private string pickerName = "Galaxies";
        [SerializeField] private TMP_Text crumb = null;

        private ExperienceModule _place;
        private bool _listening;

        private void Start()
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                var id = tileIds[i];
                tiles[i].Clicked += () => ExperienceDirector.Instance?.Switch(id);
            }

            listen.Clicked += ToggleListen;
            ExperienceDirector.ExperienceChanged += OnPlaceChanged;
            OnPlaceChanged(ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null);

            // The legacy scene panel is spawned by the director a moment after a place opens.
            InvokeRepeating(nameof(HideLegacyPanels), 0.25f, 0.5f);
        }

        private void OnDestroy()
        {
            ExperienceDirector.ExperienceChanged -= OnPlaceChanged;
        }

        private void OnPlaceChanged(ExperienceModule module)
        {
            _place = module;
            card.gameObject.SetActive(module != null);

            var index = module != null ? Array.IndexOf(tileIds, module.Id) : -1;
            picker.SetActive(index >= 0);
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].Selected = i == index;
            }

            if (module == null)
            {
                crumb.text = string.Empty;
                return;
            }

            var copy = module.Panel;
            var name = !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : copy.Title;
            crumb.text = index >= 0 ? "<color=#AAB6C2>" + pickerName + "     /</color>     " + name : name;

            title.text = name.ToUpperInvariant();
            body.text = copy.Paragraphs != null && copy.Paragraphs.Length > 0 ? copy.Paragraphs[0] : string.Empty;

            var picture = PhotoFor(module);
            photoRoot.SetActive(picture != null);
            photo.sprite = picture;
            var fitter = photo.GetComponent<AspectRatioFitter>();
            if (fitter != null && picture != null)
            {
                fitter.aspectRatio = picture.rect.width / picture.rect.height;
            }

            listen.gameObject.SetActive(module.Narration != null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        private Sprite PhotoFor(ExperienceModule module)
        {
            var i = Array.IndexOf(photoIds, module.Id);
            return i >= 0 && i < photos.Length && photos[i] != null ? photos[i] : module.DockThumbnail;
        }

        private void ToggleListen()
        {
            var vo = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            if (vo == null || _place == null || _place.Narration == null)
            {
                return;
            }

            if (vo.IsPlaying && vo.CurrentClip == _place.Narration)
            {
                vo.Stop(true);
            }
            else
            {
                vo.PlayClip(_place.Narration, 0f, true, true);
            }
        }

        private void Update()
        {
            var vo = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            var listening = vo != null && _place != null && vo.IsPlaying && vo.CurrentClip == _place.Narration;
            if (listening != _listening)
            {
                _listening = listening;
                listen.SetLabel(listening ? "Stop" : "Listen");
            }
        }

        private void HideLegacyPanels()
        {
            foreach (var panel in FindObjectsByType<InfoPanel>(FindObjectsSortMode.None))
            {
                if (panel.PanelVariant == InfoPanel.Variant.Scene)
                {
                    panel.Hide();
                }
            }
        }
    }
}
