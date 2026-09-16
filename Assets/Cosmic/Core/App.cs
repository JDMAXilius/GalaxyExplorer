using UnityEngine;

namespace Cosmic
{
    [DefaultExecutionOrder(-100)]
    public class App : MonoBehaviour
    {
        public static App Instance { get; private set; }

        [SerializeField] Director director;
        [SerializeField] Anchor anchor;
        [SerializeField] Dock dock;
        [SerializeField] Toast toast;
        [SerializeField] About about;
        [SerializeField] Hotkeys hotkeys;
        [SerializeField] Audio audio;
        [SerializeField] Place start;
        [SerializeField] Place[] places;
        [SerializeField] Being beingPrefab;
        [SerializeField] bool playIntro = true;

        public Director Director => director;
        public Being Being { get; private set; }
        public bool Booted { get; private set; }

        void Awake()
        {
            Instance = this;
            Grabbable.Bus = audio;
            if (dock != null) dock.Visible = false;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            if (dock != null)
            {
                dock.Picked += OnPicked;
                dock.LayoutPicked += OnLayout;
                dock.Recentered += OnRecentered;
                dock.HelpRequested += OnHelp;
                dock.AboutRequested += OnAbout;
                dock.Scaled += OnScaled;
                dock.BeingRequested += ToggleBeing;
            }
            if (hotkeys != null)
            {
                hotkeys.Restore += OnRestore;
                hotkeys.Body += OnBody;
                hotkeys.Moon += OnMoon;
                hotkeys.Close += OnClose;
            }
            if (director != null) director.Changed += OnChanged;
        }

        void OnDisable()
        {
            if (dock != null)
            {
                dock.Picked -= OnPicked;
                dock.LayoutPicked -= OnLayout;
                dock.Recentered -= OnRecentered;
                dock.HelpRequested -= OnHelp;
                dock.AboutRequested -= OnAbout;
                dock.Scaled -= OnScaled;
                dock.BeingRequested -= ToggleBeing;
            }
            if (hotkeys != null)
            {
                hotkeys.Restore -= OnRestore;
                hotkeys.Body -= OnBody;
                hotkeys.Moon -= OnMoon;
                hotkeys.Close -= OnClose;
            }
            if (director != null) director.Changed -= OnChanged;
        }

        public Place Find(string id)
        {
            if (places != null)
                foreach (var place in places)
                    if (place != null && place.id == id) return place;
            return null;
        }

        public void ToggleBeing()
        {
            if (Being != null) { Being.Dismiss(); Being = null; }
            else if (beingPrefab != null) Being = Instantiate(beingPrefab);
        }

        void Start()
        {
            if (anchor != null && playIntro) anchor.Begin(Boot);
            else { if (anchor != null) anchor.Recenter(); Boot(); }
        }

        void Boot()
        {
            Booted = true;
            if (dock != null) { dock.Recenter(); dock.Visible = true; }
            if (director != null && start != null) director.Open(start);
            if (toast != null) toast.Hints();
        }

        void OnPicked(Place place)
        {
            if (director == null) return;
            if (director.OpenDestination != null) director.CloseDestination();
            director.Open(place);
        }

        void OnLayout(Place place, Layout layout)
        {
            if (director == null) return;
            if (director.Current != place) { director.Open(place); return; }
            director.Apply(layout);
        }

        void OnChanged(Place place) { if (dock != null) dock.Mark(place, director.CurrentLayout); }

        void OnRecentered() { if (anchor != null && !anchor.IntroRunning) director?.Restore(); }

        void OnRestore() => director?.Restore();

        void OnBody(int index) => director?.PullBody(index);

        void OnMoon() => director?.PullMoon();

        void OnHelp() { if (toast != null) toast.Hints(true); }

        void OnAbout() { if (about != null) about.Toggle(); }

        void OnScaled(float factor) => director?.Scale(factor);

        void OnClose()
        {
            if (about != null && about.Showing) { about.Hide(); return; }
            if (director != null && director.OpenDestination != null) director.CloseDestination();
            else if (anchor != null && anchor.IntroRunning) anchor.Skip();
        }
    }
}
