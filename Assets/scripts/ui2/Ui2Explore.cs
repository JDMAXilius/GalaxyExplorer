// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The exploring half of the UI 2.0 shell (docs/design/v2, U2-P3): the photo-first information card, the
    /// top picker and the breadcrumb. The card describes the body or moon the player has pulled out, and the
    /// current place when nothing is held. While this is up the legacy panels are suppressed.
    /// </summary>
    public class Ui2Explore : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField] private RectTransform card = null;
        [SerializeField] private GameObject photoRoot = null;
        [SerializeField] private Image photo = null;
        [SerializeField] private TMP_Text title = null;
        [SerializeField] private TMP_Text body = null;
        [SerializeField] private GameObject factsRoot = null;
        [SerializeField] private TMP_Text[] factLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] factValues = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] factUnits = Array.Empty<TMP_Text>();
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

        private const string Dim = "<color=#AAB6C2>";

        private readonly List<InfoPanel> _open = new List<InfoPanel>();
        private ExperienceModule _place;
        private BodyInfo _body;
        private AudioClip _narration;
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
            InfoPanel.Suppressed = true;
            OnPlaceChanged(ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null);

            // There is no selection service yet (U2-P3): which body is out is read off the legacy panels,
            // which already follow each body's ForceSolver.
            InvokeRepeating(nameof(TrackBody), 0.2f, 0.2f);
        }

        private void OnDestroy()
        {
            ExperienceDirector.ExperienceChanged -= OnPlaceChanged;
            InfoPanel.Suppressed = false;
        }

        private void OnPlaceChanged(ExperienceModule module)
        {
            _place = module;
            _body = null;
            _open.Clear();
            Refresh();
        }

        private void TrackBody()
        {
            // The most recently opened panel wins, so pulling a moon off a held planet shows the moon.
            _open.RemoveAll(p => p == null || !p.IsOpen);
            foreach (var panel in FindObjectsByType<InfoPanel>(FindObjectsSortMode.None))
            {
                if (panel.Info != null && panel.IsOpen && !_open.Contains(panel))
                {
                    _open.Add(panel);
                }
            }

            var current = _open.Count > 0 ? _open[_open.Count - 1].Info : null;
            if (current != _body)
            {
                _body = current;
                Refresh();
            }
        }

        private void Refresh()
        {
            card.gameObject.SetActive(_place != null);

            var index = _place != null ? Array.IndexOf(tileIds, _place.Id) : -1;
            picker.SetActive(index >= 0);
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].Selected = i == index;
            }

            if (_place == null)
            {
                crumb.text = string.Empty;
                return;
            }

            var copy = _place.Panel;
            var placeName = !string.IsNullOrEmpty(_place.DisplayName) ? _place.DisplayName : copy.Title;
            Sprite picture;

            if (_body != null)
            {
                crumb.text = Dim + placeName + "     /</color>     " + _body.DisplayName;
                title.text = _body.DisplayName.ToUpperInvariant();
                body.text = _body.Paragraph;
                _narration = _body.Narration;
                picture = null; // no per-body photographs exist yet
                ShowFacts(_body.Stats);
            }
            else
            {
                crumb.text = index >= 0 ? Dim + pickerName + "     /</color>     " + placeName : placeName;
                title.text = placeName.ToUpperInvariant();
                body.text = copy.Paragraphs != null && copy.Paragraphs.Length > 0 ? copy.Paragraphs[0] : string.Empty;
                _narration = _place.Narration;
                picture = PhotoFor(_place);
                ShowFacts(null); // places carry no stats yet
            }

            photoRoot.SetActive(picture != null);
            photo.sprite = picture;
            var fitter = photo.GetComponent<AspectRatioFitter>();
            if (fitter != null && picture != null)
            {
                fitter.aspectRatio = picture.rect.width / picture.rect.height;
            }

            listen.gameObject.SetActive(_narration != null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        private void ShowFacts(Stat[] stats)
        {
            var count = stats != null ? Mathf.Min(stats.Length, factLabels.Length) : 0;
            factsRoot.SetActive(count > 0);
            for (var i = 0; i < factLabels.Length; i++)
            {
                var has = i < count;
                factLabels[i].text = has ? Sentence(stats[i].Label) : string.Empty;
                factValues[i].text = has
                    ? (stats[i].Exponent == 0 ? stats[i].Value : stats[i].Value + "<sup>" + stats[i].Exponent + "</sup>")
                    : string.Empty;
                factUnits[i].text = has ? stats[i].Unit : string.Empty;
            }
        }

        // The copy files write stat labels in capitals for the legacy panel; the card uses sentence case.
        private static string Sentence(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            var lower = label.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        private Sprite PhotoFor(ExperienceModule module)
        {
            var i = Array.IndexOf(photoIds, module.Id);
            return i >= 0 && i < photos.Length && photos[i] != null ? photos[i] : module.DockThumbnail;
        }

        private void ToggleListen()
        {
            var vo = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            if (vo == null || _narration == null)
            {
                return;
            }

            if (vo.IsPlaying && vo.CurrentClip == _narration)
            {
                vo.Stop(true);
            }
            else
            {
                vo.PlayClip(_narration, 0f, true, true);
            }
        }

        private void Update()
        {
            var vo = GalaxyExplorerManager.Instance != null ? GalaxyExplorerManager.Instance.VoManager : null;
            var listening = vo != null && _narration != null && vo.IsPlaying && vo.CurrentClip == _narration;
            if (listening != _listening)
            {
                _listening = listening;
                listen.SetLabel(listening ? "Stop" : "Listen");
            }
        }
    }
}
