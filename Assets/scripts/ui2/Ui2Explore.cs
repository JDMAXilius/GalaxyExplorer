// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using GalaxyExplorer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.UI2
{
    /// <summary>
    /// The exploring half of the UI 2.0 shell (docs/design/v2): the breadcrumb, the top picker, the Solar
    /// System's View chip, the photo-first card in its three forms (information, destination preview, compare
    /// pair) and the compare flow. The card describes the body or moon the player has pulled out, the
    /// destination picked on the map, or the current place. While this is up the legacy panels are suppressed.
    /// </summary>
    public class Ui2Explore : MonoBehaviour
    {
        [Header("Card")]
        [SerializeField] private RectTransform card = null;
        [SerializeField] private UiButton back = null;
        [SerializeField] private GameObject photoRoot = null;
        [SerializeField] private Image photo = null;
        [SerializeField] private Image photo2 = null;
        [SerializeField] private TMP_Text title = null;
        [SerializeField] private TMP_Text body = null;
        [SerializeField] private GameObject divider = null;
        [SerializeField] private GameObject factsRoot = null;
        [SerializeField] private RectTransform factSplit = null;
        [SerializeField] private TMP_Text[] factLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] factValues = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] factUnits = Array.Empty<TMP_Text>();

        [SerializeField] private GameObject actionsInfo = null;
        [SerializeField] private UiButton listen = null;
        [SerializeField] private UiButton compare = null;
        [SerializeField] private GameObject actionsPreview = null;
        [SerializeField] private UiButton enter = null;
        [SerializeField] private UiButton previewListen = null;
        [SerializeField] private GameObject actionsCompare = null;
        [SerializeField] private UiButton change = null;
        [SerializeField] private UiButton done = null;
        [SerializeField] private Sprite playIcon = null;
        [SerializeField] private Sprite pauseIcon = null;

        [SerializeField]
        [Tooltip("Body, moon or place id for each entry of Photos.")]
        private string[] photoIds = Array.Empty<string>();

        [SerializeField] private Sprite[] photos = Array.Empty<Sprite>();

        [SerializeField]
        [Tooltip("The smaller picture for each entry of Photo Ids, used on picker tiles.")]
        private Sprite[] thumbs = Array.Empty<Sprite>();

        [Header("Picker")]
        [SerializeField] private GameObject picker = null;
        [SerializeField] private UiButton[] tiles = Array.Empty<UiButton>();
        [SerializeField] private Image[] tilePhotos = Array.Empty<Image>();
        [SerializeField] private RectTransform strip = null;
        [SerializeField] private float tilePitch = 146f;
        [SerializeField] private int tilesVisible = 4;
        [SerializeField] private int tilesPerStep = 2;

        [SerializeField]
        [Tooltip("How far the View chip rises when there is no picker above it, in design pixels.")]
        private float pickerLift = 133f;
        [SerializeField] private UiButton prev = null;
        [SerializeField] private UiButton next = null;
        [SerializeField] private TMP_Text crumb = null;

        [Header("Solar view")]
        [SerializeField] private UiButton chip = null;
        [SerializeField] private TMP_Text chipValue = null;
        [SerializeField] private GameObject menu = null;
        [SerializeField] private UiButton[] menuItems = Array.Empty<UiButton>();
        [SerializeField] private GameObject[] menuChecks = Array.Empty<GameObject>();

        [Header("Compare")]
        [SerializeField] private GameObject sizeBar = null;
        [SerializeField] private UiButton equalSize = null;
        [SerializeField] private UiButton relativeSize = null;
        [SerializeField] private UiButton cancel = null;

        [Header("Contexts (rail indices)")]
        [SerializeField] private int galaxiesContext = 1;
        [SerializeField] private int milkyWayContext = 2;
        [SerializeField] private int solarContext = 3;
        [SerializeField] private string galaxiesPlace = "galaxies";
        [SerializeField] private string orbitsPlace = "solar_system";
        [SerializeField] private string planetsPlace = "solar_system_planets";
        [SerializeField] private string otherSystemPlace = "hd110067";

        private const string Dim = "<color=#AAB6C2>";
        private const string Slash = "     /</color>     ";
        private static readonly string[] ViewNames = { "Orbits", "Equal Size", "Relative Size", "HD 110067" };

        private struct Entry
        {
            public string Id;
            public string Name;
        }

        private readonly List<InfoPanel> _open = new List<InfoPanel>();
        private readonly List<Entry> _entries = new List<Entry>();
        private ExperienceModule _place;
        private ExperienceModule _preview;
        private BodyInfo _body;
        private LayoutRig _rig;
        private AudioClip _sound;
        private AudioSource _speaker;
        private bool _held;
        private RectTransform _chipRect;
        private RectTransform _menuRect;
        private Vector2 _chipHome;
        private Vector2 _menuHome;
        private int _first;
        private int _pendingLayout = -1;
        private float _pendingSince;
        private float _textScale = 1f;
        private float[] _baseSizes;
        private int _listenState = -1;

        // Compare: the first body is the one that was out when Compare was pressed.
        private bool _choosing;
        private BodyInfo _subject;
        private BodyInfo _second;
        private bool _relative;
        private LayoutPreset _savedLayout;
        private LayoutPreset _pairLayout;

        public static Ui2Explore Instance { get; private set; }

        /// <summary>The body or moon the card is describing, or null.</summary>
        public BodyInfo Selected => _body;

        /// <summary>The transform that carries the selected planet's size, or null (nothing out, or a moon).</summary>
        public Transform SelectedRoot
        {
            get
            {
                var found = _rig != null && _body != null ? _rig.Find(_body.Id) : null;
                return found != null ? found.Root : null;
            }
        }

        /// <summary>True while two bodies stand side by side.</summary>
        public bool Comparing => _second != null;

        private void Awake() => Instance = this;

        private void Start()
        {
            for (var i = 0; i < tiles.Length; i++)
            {
                var slot = i;
                tiles[i].Clicked += () => OnTile(slot);
            }

            prev.Clicked += () => Turn(-1);
            next.Clicked += () => Turn(1);
            listen.Clicked += ToggleListen;
            previewListen.Clicked += ToggleListen;
            compare.Clicked += BeginCompare;
            change.Clicked += BeginCompare;
            done.Clicked += EndCompare;
            cancel.Clicked += () =>
            {
                _choosing = false;
                Refresh();
            };
            equalSize.Clicked += () => SetRelative(false);
            relativeSize.Clicked += () => SetRelative(true);
            back.Clicked += BackToMap;
            enter.Clicked += EnterPreview;
            chip.Clicked += () =>
            {
                menu.SetActive(!menu.activeSelf);
                chip.Selected = menu.activeSelf;
            };
            for (var i = 0; i < menuItems.Length; i++)
            {
                var index = i;
                menuItems[i].Clicked += () => ChooseView(index);
            }

            var texts = new[] { title, body, factLabels[0], factLabels[1], factValues[0], factValues[1], factUnits[0], factUnits[1] };
            _baseSizes = new float[texts.Length];
            for (var i = 0; i < texts.Length; i++)
            {
                _baseSizes[i] = texts[i].fontSize;
            }

            menu.SetActive(false);
            _chipRect = (RectTransform)chip.transform;
            _menuRect = (RectTransform)menu.transform;
            _chipHome = _chipRect.anchoredPosition;
            _menuHome = _menuRect.anchoredPosition;
            ExperienceDirector.ExperienceChanged += OnPlaceChanged;
            PlanetPOI.Preview = OnMarker;
            InfoPanel.Suppressed = true;
            if (Ui2Shell.Instance != null)
            {
                Ui2Shell.Instance.Changed += Refresh;
            }

            OnPlaceChanged(ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null);

            // Which body is out is read off the legacy panels, which already follow each body's ForceSolver.
            InvokeRepeating(nameof(Track), 0.2f, 0.2f);
        }

        private void OnDestroy()
        {
            ExperienceDirector.ExperienceChanged -= OnPlaceChanged;
            if (PlanetPOI.Preview == OnMarker)
            {
                PlanetPOI.Preview = null;
            }

            InfoPanel.Suppressed = false;
            if (Ui2Shell.Instance != null)
            {
                Ui2Shell.Instance.Changed -= Refresh;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------- what is selected

        private void OnPlaceChanged(ExperienceModule module)
        {
            _place = module;
            _preview = null;
            _body = null;
            _rig = null;
            _first = 0;
            _choosing = false;
            _subject = null;
            _second = null;
            _savedLayout = null;
            _pendingSince = Time.unscaledTime;
            _open.Clear();
            menu.SetActive(false);
            chip.Selected = false;
            Refresh();
        }

        private void Track()
        {
            if (_rig == null && _place != null)
            {
                _rig = FindAnyObjectByType<LayoutRig>();
                if (_rig != null)
                {
                    Refresh();
                }
            }

            // A view chosen from another place: the rig applies its own starting arrangement as it wakes, so
            // the request is repeated until the rig has taken it (or a few seconds have passed).
            if (_rig != null && _pendingLayout >= 0 && _place != null && _place.Id == planetsPlace)
            {
                var wanted = _place != null && _place.Layouts != null && _pendingLayout < _place.Layouts.Length
                    ? _place.Layouts[_pendingLayout]
                    : null;
                if (wanted == null || _rig.Current == wanted || Time.unscaledTime - _pendingSince > 6f)
                {
                    _pendingLayout = -1;
                }
                else if (!_rig.IsTransitioning)
                {
                    DockController.ChooseLayout(_place, wanted);
                }
            }

            // The picker replaces the bar of galaxy names the sphere of galaxies used to carry.
            var bar = FindAnyObjectByType<GalaxyBar>();
            if (bar != null)
            {
                bar.gameObject.SetActive(false);
            }

            KeepPulledInFront();

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
            if (current != _body || !Mathf.Approximately(_textScale, InfoPanel.TextScale))
            {
                if (current != _body && current != null)
                {
                    BringIntoView(current.IsMoon ? current.Orbits.Id : current.Id);
                }

                _body = current;
                Refresh();
            }
            else if (chip.gameObject.activeSelf)
            {
                PaintView();
            }
        }

        // ---------- pulled bodies are drawn over everything else
        //
        // A pulled planet or moon is nearer than the rest, but several body shaders draw without regard to
        // depth, so a neighbour could still paint over it. While a body is out its materials move to the end
        // of the draw order (just under the room's dim layer at 3900) and go back when it returns home.

        private readonly List<ForceSolver> _pulled = new List<ForceSolver>();
        private readonly List<ForceSolver> _pulledNow = new List<ForceSolver>();
        private readonly Dictionary<Material, int> _raised = new Dictionary<Material, int>();

        private void KeepPulledInFront()
        {
            _pulledNow.Clear();
            foreach (var solver in FindObjectsByType<ForceSolver>(FindObjectsSortMode.None))
            {
                if (solver.ForceState != ForceSolver.State.Root && solver.ForceState != ForceSolver.State.None)
                {
                    _pulledNow.Add(solver);
                }
            }

            var same = _pulledNow.Count == _pulled.Count;
            for (var i = 0; same && i < _pulledNow.Count; i++)
            {
                same = _pulled.Contains(_pulledNow[i]);
            }

            if (same)
            {
                return;
            }

            foreach (var pair in _raised)
            {
                if (pair.Key != null)
                {
                    pair.Key.renderQueue = pair.Value;
                }
            }

            _raised.Clear();
            _pulled.Clear();
            _pulled.AddRange(_pulledNow);
            foreach (var solver in _pulled)
            {
                var renderers = solver.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0 && solver.transform.parent != null)
                {
                    renderers = solver.transform.parent.GetComponentsInChildren<Renderer>();
                }

                foreach (var renderer in renderers)
                {
                    // Instances, so the shared material assets are never written to.
                    foreach (var material in renderer.materials)
                    {
                        if (material == null || _raised.ContainsKey(material))
                        {
                            continue;
                        }

                        _raised[material] = material.renderQueue;
                        material.renderQueue = 3700 + Mathf.Clamp((material.renderQueue - 2000) / 10, 0, 150);
                    }
                }
            }
        }

        // ---------- picker

        private void BuildEntries(int context)
        {
            _entries.Clear();
            if (_place == null)
            {
                return;
            }

            if (_rig != null && _rig.BodyCount > 1)
            {
                foreach (var b in _rig.Bodies)
                {
                    if (b == null || !b.IsUsable || b.Info == null || (_choosing && b.Info == _subject))
                    {
                        continue;
                    }

                    _entries.Add(new Entry { Id = b.Id, Name = b.Info.DisplayName });
                }
            }
            else if (context == galaxiesContext && !_choosing)
            {
                var hub = ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Find(galaxiesPlace) : null;
                if (hub != null && hub.Places != null)
                {
                    foreach (var p in hub.Places)
                    {
                        if (p != null)
                        {
                            _entries.Add(new Entry { Id = p.Id, Name = ShortName(p) });
                        }
                    }
                }
            }
        }

        private void PaintPicker(string selectedId)
        {
            var last = Mathf.Max(0, _entries.Count - tilesVisible);
            _first = Mathf.Clamp(_first, 0, last);
            for (var i = 0; i < tiles.Length; i++)
            {
                var has = i < _entries.Count;
                tiles[i].gameObject.SetActive(has);
                if (!has)
                {
                    continue;
                }

                var entry = _entries[i];
                tiles[i].SetLabel(entry.Name);
                tiles[i].Selected = entry.Id == selectedId;

                // Only the tiles inside the window can be pressed; the rest are clipped by the strip's mask.
                tiles[i].Interactable = i >= _first && i < _first + tilesVisible;
                var sprite = ThumbFor(entry.Id);
                tilePhotos[i].enabled = sprite != null;
                tilePhotos[i].sprite = sprite;
                Fit(tilePhotos[i], sprite);
            }

            prev.gameObject.SetActive(_first > 0);
            next.gameObject.SetActive(_first < last);
        }

        private void Turn(int direction)
        {
            _first += direction * tilesPerStep;
            Refresh();
        }

        private void BringIntoView(string id)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id != id)
                {
                    continue;
                }

                if (i < _first)
                {
                    _first = i;
                }
                else if (i >= _first + tilesVisible)
                {
                    _first = i - tilesVisible + 1;
                }

                return;
            }
        }

        private void OnTile(int slot)
        {
            var index = slot;
            if (index >= _entries.Count)
            {
                return;
            }

            var id = _entries[index].Id;
            if (_choosing)
            {
                BeginPair(id);
                return;
            }

            if (_rig == null)
            {
                ExperienceDirector.Instance?.Switch(id);
                return;
            }

            if (Comparing)
            {
                return;
            }

            // Pressing a body's tile is the same as pulling it: whatever was out goes home first.
            var wanted = _rig.Find(id);
            if (wanted == null || wanted.Force == null || wanted.Force.ForceState != ForceSolver.State.Root)
            {
                return;
            }

            SendHome();
            wanted.Force.OnPointerDown();
        }

        private void SendHome()
        {
            if (_rig == null)
            {
                return;
            }

            foreach (var b in _rig.Bodies)
            {
                if (b != null && b.Force != null && b.Force.ForceState != ForceSolver.State.Root)
                {
                    b.Force.ReturnHome();
                }
            }
        }

        // ---------- the Solar System's View chip

        private int CurrentView()
        {
            if (_place == null)
            {
                return -1;
            }

            if (_place.Id == orbitsPlace)
            {
                return 0;
            }

            if (_place.Id == otherSystemPlace)
            {
                return 3;
            }

            if (_place.Id != planetsPlace)
            {
                return -1;
            }

            var layout = _savedLayout != null ? _savedLayout : _rig != null ? _rig.PendingOrCurrent : null;
            var index = layout != null && _place.Layouts != null ? Array.IndexOf(_place.Layouts, layout) : 0;
            return index == 1 ? 2 : 1;
        }

        private void PaintView()
        {
            var view = CurrentView();
            chipValue.text = view >= 0 ? ViewNames[view] : string.Empty;
            for (var i = 0; i < menuItems.Length; i++)
            {
                menuItems[i].Selected = i == view;
                menuChecks[i].SetActive(i == view);
            }
        }

        private void ChooseView(int view)
        {
            menu.SetActive(false);
            chip.Selected = false;
            var director = ExperienceDirector.Instance;
            if (director == null || view == CurrentView())
            {
                return;
            }

            if (view == 0 || view == 3)
            {
                director.Switch(view == 0 ? orbitsPlace : otherSystemPlace);
                return;
            }

            var planets = director.Find(planetsPlace);
            if (planets == null || planets.Layouts == null || planets.Layouts.Length < 2)
            {
                return;
            }

            if (director.Current == planets)
            {
                DockController.ChooseLayout(planets, planets.Layouts[view - 1]);
            }
            else
            {
                // The bodies do not exist until the place has opened; Track applies the layout when they do.
                _pendingLayout = view - 1;
                director.Switch(planets);
            }
        }

        // ---------- the Milky Way map

        private bool OnMarker(ExperienceModule module)
        {
            var shell = Ui2Shell.Instance;
            if (shell == null || !shell.ExploreVisible || _place == null ||
                _place.Id != shell.ContextHome(milkyWayContext) || module == _place)
            {
                return false;
            }

            _preview = module;
            AudioService.Instance?.PlayClip(AudioId.Select);
            Refresh();
            return true;
        }

        private void EnterPreview()
        {
            if (_preview == null)
            {
                return;
            }

            Ui2Shell.Instance?.Notify("Opening " + ShortName(_preview) + "…", 3f);
            ExperienceDirector.Instance?.Switch(_preview);
        }

        private void BackToMap()
        {
            if (_preview != null)
            {
                _preview = null;
                Refresh();
                return;
            }

            var shell = Ui2Shell.Instance;
            var home = shell != null ? shell.ContextHome(shell.ContextOf(_place != null ? _place.Id : null)) : null;
            if (home != null)
            {
                ExperienceDirector.Instance?.Switch(home);
            }
        }

        // ---------- compare

        private void BeginCompare()
        {
            var subject = Comparing ? _subject : _body;
            if (_rig == null || subject == null || subject.IsMoon || _rig.Find(subject.Id) == null)
            {
                return;
            }

            _subject = subject;
            _choosing = true;
            _first = 0;
            Refresh();
        }

        private void BeginPair(string id)
        {
            var other = _rig != null ? _rig.Find(id) : null;
            if (other == null || other.Info == null || _subject == null)
            {
                return;
            }

            if (_savedLayout == null)
            {
                _savedLayout = _rig.PendingOrCurrent;
            }

            _second = other.Info;
            _choosing = false;
            SendHome();
            ApplyPair();
            Refresh();
        }

        private void SetRelative(bool relative)
        {
            _relative = relative;
            ApplyPair();
            Refresh();
        }

        private void EndCompare()
        {
            if (_rig != null && _savedLayout != null)
            {
                _rig.Apply(_savedLayout);
            }

            _savedLayout = null;
            _subject = null;
            _second = null;
            _choosing = false;
            Refresh();
        }

        /// <summary>
        /// Stands the two bodies side by side in the middle of the arrangement and shrinks the rest out of
        /// sight, as a temporary arrangement on the same rig; Done puts the saved arrangement back.
        /// </summary>
        private void ApplyPair()
        {
            if (_rig == null || _savedLayout == null || _subject == null || _second == null)
            {
                return;
            }

            var centre = Vector3.zero;
            var count = 0;
            Vector3? firstSlot = null;
            var lastSlot = Vector3.zero;
            foreach (var slot in _savedLayout.Slots)
            {
                centre += slot.LocalPosition;
                count++;
                firstSlot = firstSlot ?? slot.LocalPosition;
                lastSlot = slot.LocalPosition;
            }

            if (count == 0)
            {
                return;
            }

            centre /= count;
            var axis = lastSlot - firstSlot.Value;
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;

            const float Large = 0.34f;
            var sizeA = 0.28f;
            var sizeB = 0.28f;
            if (_relative && Diameter(_subject, out var a) && Diameter(_second, out var b))
            {
                sizeA = a >= b ? Large : Mathf.Max(0.012f, Large * a / b);
                sizeB = b >= a ? Large : Mathf.Max(0.012f, Large * b / a);
            }

            const float Gap = 0.2f;
            if (_pairLayout == null)
            {
                _pairLayout = ScriptableObject.CreateInstance<LayoutPreset>();
                _pairLayout.Id = "ui2_compare_pair";
                _pairLayout.TransitionSeconds = 0.8f;
            }

            var slots = new List<LayoutSlot>();
            foreach (var b2 in _rig.Bodies)
            {
                if (b2 == null || !b2.IsUsable)
                {
                    continue;
                }

                var home = _savedLayout.Find(b2.Id);
                var slot = new LayoutSlot
                {
                    BodyId = b2.Id,
                    LocalPosition = home.HasValue ? home.Value.LocalPosition : centre,
                    LocalEuler = home.HasValue ? home.Value.LocalEuler : Vector3.zero,
                    Scale = 0.0001f,
                };
                if (b2.Info == _subject)
                {
                    slot.LocalPosition = centre - axis * (sizeA * 0.5f + Gap * 0.5f);
                    slot.Scale = sizeA;
                }
                else if (b2.Info == _second)
                {
                    slot.LocalPosition = centre + axis * (sizeB * 0.5f + Gap * 0.5f);
                    slot.Scale = sizeB;
                }

                slots.Add(slot);
            }

            _pairLayout.Slots = slots.ToArray();
            _rig.Apply(_pairLayout);
        }

        private static bool Diameter(BodyInfo info, out float kilometres)
        {
            kilometres = 0f;
            foreach (var stat in info.Stats)
            {
                if (stat.Label != null && stat.Label.ToLowerInvariant().Contains("diameter"))
                {
                    return float.TryParse(stat.Value.Replace(",", string.Empty), NumberStyles.Float,
                                          CultureInfo.InvariantCulture, out kilometres) && kilometres > 0f;
                }
            }

            return false;
        }

        // ---------- painting

        public void Refresh()
        {
            var shell = Ui2Shell.Instance;
            var visible = _place != null && (shell == null || shell.ExploreVisible);
            card.gameObject.SetActive(visible);
            crumb.gameObject.SetActive(visible && (shell == null || !shell.NoticeShowing));
            if (!visible)
            {
                picker.SetActive(false);
                chip.gameObject.SetActive(false);
                menu.SetActive(false);
                sizeBar.SetActive(false);
                cancel.gameObject.SetActive(false);
                return;
            }

            var context = shell != null ? shell.ContextOf(_place.Id) : -1;
            var home = shell != null ? shell.ContextHome(context) : null;
            var placeName = ShortName(_place);

            BuildEntries(context);
            picker.SetActive(_entries.Count > 0 && (!Comparing || _choosing));
            var selectedId = _rig != null && _rig.BodyCount > 1
                ? (_body != null ? (_body.IsMoon ? _body.Orbits.Id : _body.Id) : null)
                : _place.Id;
            PaintPicker(selectedId);

            var solar = context == solarContext && !Comparing && !_choosing;
            chip.gameObject.SetActive(solar);
            if (!solar)
            {
                menu.SetActive(false);
            }
            else
            {
                // With no row of planets above it, the chip takes the row's place.
                var lift = picker.activeSelf ? Vector2.zero : new Vector2(0f, pickerLift);
                _chipRect.anchoredPosition = _chipHome + lift;
                _menuRect.anchoredPosition = _menuHome + lift;
                PaintView();
            }

            sizeBar.SetActive(Comparing && !_choosing);
            cancel.gameObject.SetActive(_choosing);
            equalSize.Selected = !_relative;
            relativeSize.Selected = _relative;

            // Breadcrumb: context, then the place when it is not the context's own top level, then the object.
            var parts = new List<string>();
            if (context >= 0)
            {
                parts.Add(shell.ContextName(context));
                if (placeName != parts[0] && _place.Id != planetsPlace && _place.Id != galaxiesPlace)
                {
                    parts.Add(placeName);
                }
            }
            else
            {
                parts.Add(placeName);
            }

            if (_preview != null)
            {
                parts.Add(ShortName(_preview));
            }
            else if (_body != null)
            {
                parts.Add(_body.DisplayName);
            }

            if (_choosing)
            {
                crumb.text = "Compare " + _subject.DisplayName + " with…";
            }
            else if (Comparing)
            {
                crumb.text = "Compare worlds";
            }
            else
            {
                var last = parts[parts.Count - 1];
                parts.RemoveAt(parts.Count - 1);
                crumb.text = parts.Count > 0 ? Dim + string.Join(Slash + Dim, parts) + Slash + last : last;
            }

            // The card.
            Sprite picture;
            Sprite picture2 = null;
            var nested = context == milkyWayContext && _place.Id != home;
            back.gameObject.SetActive(_preview != null || (nested && _body == null && !Comparing));
            actionsInfo.SetActive(_preview == null && !Comparing);
            actionsPreview.SetActive(_preview != null && !Comparing);
            actionsCompare.SetActive(Comparing);

            if (Comparing)
            {
                title.text = (_subject.DisplayName + " & " + _second.DisplayName).ToUpperInvariant();
                body.text = _relative
                    ? "Both shown at relative size to compare their scale."
                    : "Both shown at equal size to compare their features.";
                _sound = null;
                picture = PhotoFor(_subject.Id);
                picture2 = PhotoFor(_second.Id);
                ShowFacts(PairFact(_subject), PairFact(_second));
            }
            else if (_preview != null)
            {
                title.text = ShortName(_preview).ToUpperInvariant();
                body.text = Opening(_preview.Panel);
                _sound = _preview.Ambience != null ? _preview.Ambience : _preview.Narration;
                picture = PhotoFor(_preview.Id) ?? _preview.DockThumbnail;
                PlaceFacts(_preview);
                enter.SetLabel(_preview.Kind == ExperienceKind.Destination ? "Enter nebula" : "Open");
            }
            else if (_body != null)
            {
                title.text = _body.DisplayName.ToUpperInvariant();
                body.text = Sentences(_body.Paragraph);
                _sound = SoundOf(_body);
                picture = PhotoFor(_body.Id);
                BodyFacts(_body);
            }
            else
            {
                title.text = placeName.ToUpperInvariant();
                body.text = Opening(_place.Panel);
                _sound = _place.Ambience != null ? _place.Ambience : _place.Narration;
                picture = PhotoFor(_place.Id) ?? _place.DockThumbnail;
                PlaceFacts(_place);
            }

            photoRoot.SetActive(picture != null);
            photo.sprite = picture;
            photo2.sprite = picture2;
            photo2.gameObject.SetActive(picture2 != null);
            var half = picture2 != null;
            var photoRect = photo.rectTransform.parent as RectTransform;
            photoRect.anchorMax = new Vector2(half ? 0.5f : 1f, 1f);
            Fit(photo, picture);
            Fit(photo2, picture2);

            compare.Interactable = _body != null && !_body.IsMoon && _rig != null && _rig.Find(_body.Id) != null;
            listen.Interactable = _sound != null;
            previewListen.Interactable = _sound != null;
            _listenState = -1;

            _textScale = InfoPanel.TextScale;
            var texts = new[] { title, body, factLabels[0], factLabels[1], factValues[0], factValues[1], factUnits[0], factUnits[1] };
            for (var i = 0; i < texts.Length; i++)
            {
                texts[i].fontSize = _baseSizes[i] * _textScale;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        private static void Fit(Image image, Sprite sprite)
        {
            var fitter = image != null ? image.GetComponent<AspectRatioFitter>() : null;
            if (fitter != null && sprite != null)
            {
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        // ---------- facts

        private struct Fact
        {
            public string Label;
            public string Value;
            public string Unit;
        }

        private static readonly string[] SecondFactLabels = { "orbital period", "one orbit", "orbit of the galaxy" };

        /// <summary>A body's two headline facts: its diameter, then how long one orbit takes.</summary>
        private void BodyFacts(BodyInfo info)
        {
            Fact? size = null;
            Fact? orbit = null;
            var spare = new List<Fact>();
            foreach (var stat in info.Stats)
            {
                var label = (stat.Label ?? string.Empty).ToLowerInvariant();
                var fact = new Fact
                {
                    Label = Sentence(stat.Label),
                    Value = stat.Exponent == 0 ? stat.Value : stat.Value + "<sup>" + stat.Exponent + "</sup>",
                    Unit = stat.Unit,
                };
                if (size == null && label == "diameter")
                {
                    size = fact;
                }
                else if (orbit == null && Array.IndexOf(SecondFactLabels, label) >= 0)
                {
                    fact.Label = label == "orbit of the galaxy" ? fact.Label : "Orbit";
                    orbit = fact;
                }
                else
                {
                    spare.Add(fact);
                }
            }

            var a = size ?? (spare.Count > 0 ? spare[0] : (Fact?)null);
            var b = orbit ?? (spare.Count > (size == null ? 1 : 0) ? spare[size == null ? 1 : 0] : (Fact?)null);
            ShowFacts(a, b);
        }

        private static Fact? PairFact(BodyInfo info)
        {
            foreach (var stat in info.Stats)
            {
                if (stat.Label != null && stat.Label.ToLowerInvariant() == "diameter")
                {
                    return new Fact { Label = info.DisplayName + " diameter", Value = stat.Value, Unit = stat.Unit };
                }
            }

            return null;
        }

        /// <summary>A place's facts come from its second line, written "Kind - distance" in the copy files.</summary>
        private void PlaceFacts(ExperienceModule module)
        {
            var line = module.SecondLine ?? string.Empty;
            var cut = line.IndexOf(" - ", StringComparison.Ordinal);
            if (cut <= 0 || cut + 3 >= line.Length || !char.IsDigit(line[cut + 3]))
            {
                ShowFacts(null, null);
                return;
            }

            var kind = line.Substring(0, cut).Trim();
            var distance = line.Substring(cut + 3).Trim();
            var space = distance.IndexOf(' ');
            var kindSpace = kind.IndexOf(' ');
            ShowFacts(
                new Fact
                {
                    Label = "Distance",
                    Value = space > 0 ? distance.Substring(0, space) : distance,
                    Unit = space > 0 ? distance.Substring(space + 1) : string.Empty,
                },
                new Fact
                {
                    Label = "Type",
                    Value = kindSpace > 0 ? kind.Substring(0, kindSpace) : kind,
                    Unit = kindSpace > 0 ? kind.Substring(kindSpace + 1) : string.Empty,
                });
        }

        private void ShowFacts(Fact? first, Fact? second)
        {
            var facts = new[] { first, second };
            var any = first.HasValue || second.HasValue;
            factsRoot.SetActive(any);
            divider.SetActive(any);
            var tall = false;
            for (var i = 0; i < factLabels.Length; i++)
            {
                var f = facts[i];
                factLabels[i].text = f.HasValue ? f.Value.Label : string.Empty;
                if (!f.HasValue)
                {
                    factValues[i].text = string.Empty;
                    factUnits[i].text = string.Empty;
                    continue;
                }

                // A short unit rides on the value's line, as "12,742 km" does in the design; a long one wraps under.
                var unit = (f.Value.Unit ?? string.Empty).Trim();
                var joined = f.Value.Value + " " + unit;
                var oneLine = unit.Length > 0 && !f.Value.Value.Contains("<sup>") &&
                              factValues[i].GetPreferredValues(joined).x <= 151f;
                factValues[i].text = oneLine ? joined : f.Value.Value;
                factUnits[i].text = oneLine ? string.Empty : unit;
                tall |= !oneLine && unit.Length > 0;
            }

            var height = (tall ? 78f : 54f) * InfoPanel.TextScale;
            factsRoot.GetComponent<LayoutElement>().preferredHeight = height;
            factSplit.sizeDelta = new Vector2(1f, height);
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

        private static string Opening(ScenePanelCopy copy) =>
            copy != null && copy.Paragraphs != null && copy.Paragraphs.Length > 0 ? Sentences(copy.Paragraphs[0]) : string.Empty;

        /// <summary>The card is a summary: the first sentence, and the second when the two stay short.</summary>
        private static string Sentences(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var first = text.IndexOf(". ", StringComparison.Ordinal);
            if (first < 0)
            {
                return text;
            }

            var second = text.IndexOf(". ", first + 2, StringComparison.Ordinal);
            var end = second > 0 && second < 150 ? second : first;
            return text.Substring(0, end + 1);
        }

        /// <summary>"Andromeda" rather than "Andromeda (M31)": the catalogue number stays in the copy.</summary>
        private static string ShortName(ExperienceModule module)
        {
            var name = !string.IsNullOrEmpty(module.DisplayName) ? module.DisplayName : module.Panel.Title;
            var bracket = name.IndexOf(" (", StringComparison.Ordinal);
            name = bracket > 0 ? name.Substring(0, bracket) : name;
            return name.EndsWith(" Galaxy", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7) : name;
        }

        private Sprite PhotoFor(string id)
        {
            var i = Array.IndexOf(photoIds, id);
            return i >= 0 && i < photos.Length ? photos[i] : null;
        }

        private Sprite ThumbFor(string id)
        {
            var i = Array.IndexOf(photoIds, id);
            return i >= 0 && i < thumbs.Length && thumbs[i] != null ? thumbs[i] : PhotoFor(id);
        }

        // ---------- the sound of the place
        //
        // Narration starts by itself when something is selected. Listen is the other sound: what the planet
        // or the place itself sounds like (its ambience clip), brought up front for as long as the player wants.

        /// <summary>
        /// A body's own sound: its ambience clip, else the loop its solver plays while it is out, else its
        /// narration so the button is never silent.
        /// </summary>
        private AudioClip SoundOf(BodyInfo info)
        {
            if (info.Ambience != null)
            {
                return info.Ambience;
            }

            var found = _rig != null ? _rig.Find(info.Id) : null;
            var solver = found != null ? found.Force as PlanetForceSolver : null;
            return solver != null && solver.AmbientClip != null ? solver.AmbientClip : info.Narration;
        }

        private void ToggleListen()
        {
            if (_sound == null)
            {
                return;
            }

            if (_speaker == null)
            {
                _speaker = gameObject.AddComponent<AudioSource>();
                _speaker.playOnAwake = false;
                _speaker.loop = true;
                _speaker.spatialBlend = 0f;
                _speaker.volume = 0.9f;
            }

            if (_speaker.clip == _sound && _speaker.isPlaying)
            {
                _speaker.Pause();
                _held = true;
            }
            else if (_speaker.clip == _sound && _held)
            {
                _speaker.UnPause();
                _held = false;
            }
            else
            {
                _speaker.clip = _sound;
                _speaker.Play();
                _held = false;
            }
        }

        /// <summary>True while Listen is sounding the selected thing.</summary>
        public bool Listening => _speaker != null && _speaker.isPlaying && _speaker.clip == _sound;

        private void Update()
        {
            var target = new Vector2(-_first * tilePitch, 0f);
            strip.anchoredPosition = Vector2.Lerp(strip.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

            // The sound belongs to what is selected: it stops when the selection moves on.
            if (_speaker != null && _speaker.clip != _sound && (_speaker.isPlaying || _held))
            {
                _speaker.Stop();
                _held = false;
            }

            var state = Listening ? 1 : _speaker != null && _held && _speaker.clip == _sound ? 2 : 0;
            if (state == _listenState)
            {
                return;
            }

            _listenState = state;
            var label = state == 1 ? "Pause" : state == 2 ? "Resume" : "Listen";
            listen.SetLabel(label);
            listen.SetIcon(state == 1 ? pauseIcon : playIcon);
            previewListen.SetLabel(label);
        }
    }
}
