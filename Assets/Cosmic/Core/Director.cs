using System;
using System.Collections;
using UnityEngine;

namespace Cosmic
{
    public class Director : MonoBehaviour
    {
        public static Director Instance { get; private set; }

        [SerializeField] Anchor anchor;
        [SerializeField] Audio audio;
        [SerializeField] Toast toast;
        [SerializeField] Panels panels;
        [SerializeField] float growInSeconds = 0.6f;
        [SerializeField] float destinationDistanceMetres = 1f;
        [SerializeField] float destinationWidthMetres = 0.7f;
        [SerializeField] float mapFadeAlpha = 0.2f;

        public event Action<Place> Changed, DestinationChanged;

        GameObject instance, destination;
        Coroutine switching;
        Rig rig;
        Points mapPoints;

        public Place Current { get; private set; }
        public Place OpenDestination { get; private set; }
        public Layout CurrentLayout { get; private set; }
        public bool Switching => switching != null;
        public GameObject Content => instance;
        public Rig CurrentRig => rig;

        public void Open(Place place)
        {
            if (place == null || Switching) return;
            if (anchor != null && anchor.IntroRunning) { Notice($"Just a moment - the introduction is still running. {place.title} opens as soon as it ends."); return; }
            if (place.content == null) { Notice($"{place.title} is not ready yet. It arrives in a later update."); return; }
            if (place == Current) { Restore(); return; }
            switching = StartCoroutine(Switch(place));
        }

        public void Apply(Layout layout)
        {
            if (rig == null || layout == null) return;
            CurrentLayout = layout;
            Restore();
            rig.Apply(layout);
        }

        public void Restore()
        {
            if (instance == null) return;
            CloseDestination();
            foreach (var grab in instance.GetComponentsInChildren<Grabbable>(true)) grab.Restore();
            if (panels != null) panels.CloseAll();
            if (rig != null && CurrentLayout != null) rig.Apply(CurrentLayout, 0.8f);
        }

        public void Scale(float factor)
        {
            if (instance == null) return;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, factor);
        }

        public void PullBody(int index)
        {
            if (rig == null || index < 0 || index >= rig.Bodies.Count) return;
            var anchorOf = rig.Anchor(rig.Bodies[index]);
            var pull = anchorOf != null ? anchorOf.GetComponentInChildren<Pull>() : null;
            if (pull != null) pull.Begin();
        }

        public void PullMoon()
        {
            if (rig == null) return;
            foreach (var body in rig.Bodies)
            {
                if (body == null || body.id != "moon") continue;
                var pull = rig.Anchor(body)?.GetComponentInChildren<Pull>();
                if (pull != null) pull.Begin();
                return;
            }
        }

        public void OpenDestinationPlace(Place place, Vector3 near)
        {
            if (place == null || Switching) return;
            if (place.content == null) { Notice($"{place.title} is not ready yet. It arrives in a later update."); return; }
            if (!place.HasVolume && place.galaxy == null && place.bodies.Length > 0) { Open(place); return; }
            CloseDestination();
            var view = Camera.main != null ? Camera.main.transform : transform;
            var at = view.position + Anchor.FlatForward(view) * destinationDistanceMetres;
            destination = Instantiate(place.content, at, Quaternion.identity, anchor != null ? anchor.Content : transform);
            destination.name = place.id;
            foreach (var grab in destination.GetComponentsInChildren<Grabbable>(true)) grab.CaptureHome();
            Room.Set(RoomMode.Halo);
            Room.Halo(destination.transform, destinationWidthMetres);
            if (mapPoints != null) mapPoints.Alpha = mapFadeAlpha;
            if (audio != null && place.narration != null) audio.Say(place.narration, true);
            if (panels != null) panels.ShowScene(place, destination.transform);
            OpenDestination = place;
            DestinationChanged?.Invoke(place);
            StartCoroutine(GrowIn(destination.transform));
        }

        public void CloseDestination()
        {
            if (destination == null) return;
            Room.Forget(destination.transform);
            Destroy(destination);
            destination = null;
            if (mapPoints != null) mapPoints.Alpha = 1f;
            if (Current != null) Room.Set(Current.room);
            if (panels != null) panels.CloseScene(OpenDestination);
            OpenDestination = null;
            DestinationChanged?.Invoke(null);
        }

        void Awake() => Instance = this;

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Switch(Place place)
        {
            try
            {
                CloseDestination();
                if (audio != null) { audio.StopVoice(); audio.Ambience(null); }
                if (panels != null) panels.Clear();
                if (instance != null) Destroy(instance);
                rig = null;
                mapPoints = null;
                Room.Set(place.room);
                var root = anchor != null ? anchor.Content : transform;
                instance = Instantiate(place.content, root);
                instance.name = place.id;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                Current = place;
                rig = instance.GetComponent<Rig>();
                mapPoints = instance.GetComponentInChildren<Points>(true);
                CurrentLayout = place.layouts != null && place.layouts.Length > 0 ? place.layouts[0] : null;
                if (rig != null && CurrentLayout != null) rig.Apply(CurrentLayout, 0f);
                foreach (var grab in instance.GetComponentsInChildren<Grabbable>(true)) grab.CaptureHome();
                yield return null;
                yield return GrowIn(instance.transform);
                if (panels != null) panels.Bind(place, instance, rig);
                if (audio != null && place.narration != null) audio.Say(place.narration, true);
                if (audio != null) audio.Ambience(place.ambience);
            }
            finally
            {
                switching = null;
            }
            Changed?.Invoke(place);
        }

        IEnumerator GrowIn(Transform t)
        {
            var target = t.localScale;
            t.localScale = Vector3.zero;
            if (audio != null) audio.Play(Sfx.GrowIn, t);
            var elapsed = 0f;
            while (elapsed < growInSeconds && t != null)
            {
                elapsed += Time.unscaledDeltaTime;
                t.localScale = target * Tween.EaseOutCubic(Mathf.Clamp01(elapsed / growInSeconds));
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        void Notice(string text)
        {
            if (toast != null) toast.Notice(text);
            else Debug.Log($"Director: {text}");
        }
    }
}
