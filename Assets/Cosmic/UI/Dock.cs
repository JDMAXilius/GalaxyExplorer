using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

namespace Cosmic
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Dock : MonoBehaviour
    {
        [SerializeField] Theme theme;
        [SerializeField] Hotkeys hotkeys;
        [SerializeField] Toast toast;
        [SerializeField] Tile[] tiles = Array.Empty<Tile>();
        [SerializeField] Popup popup;
        [SerializeField] RectTransform popupHome;
        [SerializeField] Utility utility;
        [SerializeField] Button passthrough;
        [SerializeField] Image passthroughFace;
        [SerializeField] Button recenter;
        [SerializeField] Button help;
        [SerializeField] Button settings;
        [SerializeField] Grabbable bar;
        [SerializeField] float aheadMetres = 0.75f;
        [SerializeField] float heightFraction = 0.55f;
        [SerializeField] float minHeightMetres = 0.7f;
        [SerializeField] float tiltDegrees = 25f;
        [SerializeField] float curveRadiusMetres = 1.2f;
        [SerializeField] float fadeSeconds = 0.35f;
        [SerializeField] float palmSeconds = 0.5f;
        [SerializeField] float palmDot = 0.7f;
        [SerializeField] float metresPerUnit = 0.001f;
        [SerializeField] float popupLiftMm = 40f;

        public event Action<Place> Picked;
        public event Action<Place, Layout> LayoutPicked;
        public event Action Recentered, HelpRequested, AboutRequested;
        public event Action<float> Scaled;

        static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        CanvasGroup group;
        Camera cam;
        Place active;
        Layout activeLayout;
        float palm, shownAlpha = 1f;
        bool visible = true, curved, ready;

        public bool Visible { get => visible; set { visible = value; if (!value) CloseAll(); } }
        public Place Active => active;
        public IReadOnlyList<Tile> Tiles => tiles;

        public void Mark(Place place, Layout layout = null)
        {
            Init();
            active = place;
            activeLayout = layout;
            for (var i = 0; i < tiles.Length; i++) tiles[i].Active = tiles[i].place == place;
            if (popup != null && popup.Showing && popup.Place == place) popup.Mark(layout);
        }

        public void Recenter()
        {
            Init();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var view = cam.transform;
            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var height = Mathf.Max(minHeightMetres, view.position.y * heightFraction);
            var origin = transform.parent != null ? transform.parent.position.y : 0f;
            var goal = new Vector3(view.position.x, origin + height, view.position.z) + forward * aheadMetres;
            transform.SetPositionAndRotation(goal, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f));
            if (bar != null) bar.CaptureHome();
            if (utility != null) utility.ResetScale();
            Recentered?.Invoke();
        }

        void OnEnable()
        {
            Init();
            foreach (var tile in tiles) tile.Clicked += OnTile;
            if (popup != null) popup.Picked += OnLayout;
            if (utility != null) { utility.Scaled += OnScaled; utility.AboutRequested += OnAbout; }
            if (passthrough != null) passthrough.onClick.AddListener(OnPassthrough);
            if (recenter != null) recenter.onClick.AddListener(Recenter);
            if (help != null) help.onClick.AddListener(OnHelp);
            if (settings != null) settings.onClick.AddListener(OnSettings);
            if (bar != null) bar.Released += OnBarReleased;
            if (hotkeys != null) { hotkeys.Dock += ToggleVisible; hotkeys.Utility += OnSettings; hotkeys.Help += OnHelp; hotkeys.Recenter += Recenter; hotkeys.Place += OnHotkeyPlace; hotkeys.Close += CloseAll; }
            Room.Changed += OnRoom;
            OnRoom(Room.Effective);
        }

        void OnDisable()
        {
            foreach (var tile in tiles) tile.Clicked -= OnTile;
            if (popup != null) popup.Picked -= OnLayout;
            if (utility != null) { utility.Scaled -= OnScaled; utility.AboutRequested -= OnAbout; }
            if (passthrough != null) passthrough.onClick.RemoveListener(OnPassthrough);
            if (recenter != null) recenter.onClick.RemoveListener(Recenter);
            if (help != null) help.onClick.RemoveListener(OnHelp);
            if (settings != null) settings.onClick.RemoveListener(OnSettings);
            if (bar != null) bar.Released -= OnBarReleased;
            if (hotkeys != null) { hotkeys.Dock -= ToggleVisible; hotkeys.Utility -= OnSettings; hotkeys.Help -= OnHelp; hotkeys.Recenter -= Recenter; hotkeys.Place -= OnHotkeyPlace; hotkeys.Close -= CloseAll; }
            Room.Changed -= OnRoom;
        }

        void LateUpdate()
        {
            Palm();
            var hidden = toast != null && toast.HintsShowing;
            var goal = visible && !hidden ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, goal, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            group.blocksRaycasts = group.alpha > 0.5f;
            if (popup != null && popup.Showing && popupHome != null) popup.transform.position = PopupGoal();
        }

        Vector3 PopupGoal() => popupHome.position + transform.up * ((popupLiftMm + (theme != null ? theme.dockTileMm.y * 0.5f : 31f)) * metresPerUnit);

        void Palm()
        {
            SubsystemManager.GetSubsystems(Subsystems);
            var up = false;
            foreach (var subsystem in Subsystems)
            {
                var hand = subsystem.leftHand;
                if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) continue;
                up = Vector3.Dot(-pose.up, Vector3.up) > palmDot;
            }
            var was = palm >= palmSeconds;
            palm = up ? palm + Time.unscaledDeltaTime : 0f;
            if (!was && palm >= palmSeconds) ToggleVisible();
        }

        void ToggleVisible()
        {
            Visible = !visible;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(visible ? Sfx.DockShow : Sfx.DockHide, transform);
        }

        void OnTile(Tile tile)
        {
            if (tile.place == null) return;
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            if (tile.place.HasLayoutChoice && popup != null)
            {
                popupHome = tile.move != null ? tile.move : (RectTransform)tile.transform;
                popup.transform.SetPositionAndRotation(PopupGoal(), transform.rotation);
                popup.Open(tile.place, tile.place == active ? activeLayout : tile.place.layouts[0]);
            }
            else if (popup != null) popup.Close();
            Picked?.Invoke(tile.place);
        }

        void OnLayout(Place place, Layout layout)
        {
            activeLayout = layout;
            LayoutPicked?.Invoke(place, layout);
        }

        void OnHotkeyPlace(int index)
        {
            if (index < 0 || index >= tiles.Length || tiles[index].place == null) return;
            Picked?.Invoke(tiles[index].place);
        }

        void OnPassthrough()
        {
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            Room.TogglePassthrough();
        }

        void OnRoom(RoomMode mode)
        {
            if (passthroughFace != null && theme != null) passthroughFace.color = Room.PassthroughForced ? theme.accentCyan : Color.white;
        }

        void OnHelp() { if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform); HelpRequested?.Invoke(); }

        void OnSettings() { if (utility != null) utility.Toggle(); }

        void OnScaled(float value) => Scaled?.Invoke(value);

        void OnAbout() => AboutRequested?.Invoke();

        void OnBarReleased(Grabbable g)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var toHead = transform.position - cam.transform.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toHead.normalized, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f);
        }

        void CloseAll()
        {
            if (popup != null) popup.Close();
            if (utility != null) utility.Close();
        }

        void Curve()
        {
            if (curved) return;
            curved = true;
            var radiusMm = curveRadiusMetres / metresPerUnit;
            foreach (var tile in tiles)
            {
                var rect = (RectTransform)tile.transform;
                var angle = rect.anchoredPosition.x / radiusMm;
                rect.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                rect.anchoredPosition3D = new Vector3(rect.anchoredPosition.x, rect.anchoredPosition.y, radiusMm * (1f - Mathf.Cos(angle)));
            }
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            group = GetComponent<CanvasGroup>();
            transform.localScale = Vector3.one * metresPerUnit;
            Curve();
            if (theme != null && theme.font != null) foreach (var text in GetComponentsInChildren<TMP_Text>(true)) text.font = theme.font;
        }
    }
}
