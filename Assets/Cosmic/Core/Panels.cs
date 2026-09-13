using System.Collections.Generic;
using UnityEngine;

namespace Cosmic
{
    public class Panels : MonoBehaviour
    {
        [SerializeField] Panel scenePrefab;
        [SerializeField] Panel bodyPrefab;
        [SerializeField] Panel moonPrefab;
        [SerializeField] Label tagPrefab;
        [SerializeField] Label namePrefab;
        [SerializeField] float tagScale = 4f;
        [SerializeField] float tagLiftMetres = 0.12f;
        [SerializeField] float tagTierMetres = 0.104f;
        [SerializeField] float discUnitsPerHalfWidth = 1.05f;

        static readonly (string id, float x, float y, float z)[] Spots =
        {
            ("helix", -0.371f, 0f, 0.594f), ("crab", -0.409f, 0f, -0.716f), ("solar_system", 0.115f, 0f, 0.648f),
            ("sagittarius_a", 0f, 0f, 0f), ("homunculus", -0.772f, 0f, 0.205f), ("orion", -0.047f, 0f, -0.538f),
            ("pillars", -0.669f, 0f, -0.337f), ("ngc1501", 0.364f, 0.06f, -0.547f), ("trumpler14", 0.328f, 0f, -0.176f),
            ("whirlpool", 0.62f, 0f, 0.55f), ("pinwheel", 0.75f, 0f, 0.3f), ("triangulum", 0.7f, 0f, -0.45f),
        };

        readonly Dictionary<Body, Panel> open = new Dictionary<Body, Panel>();
        readonly List<Label> labels = new List<Label>();
        readonly List<Grabbable> watched = new List<Grabbable>();
        Panel scene, destinationPanel;

        public void Bind(Place place, GameObject instance, Rig rig)
        {
            Clear();
            if (place == null || instance == null) return;
            if (!string.IsNullOrEmpty(place.panel.title)) scene = ShowScene(place, instance.transform, scene);
            if (rig != null) Watch(rig, place);
            if (place.destinations != null && place.destinations.Length > 0) Tags(place, instance);
        }

        public Panel ShowScene(Place place, Transform at) { destinationPanel = ShowScene(place, at, destinationPanel); return destinationPanel; }

        public void CloseScene(Place place)
        {
            if (destinationPanel != null) destinationPanel.Hide();
            foreach (var label in labels) if (label != null) label.Selected = false;
        }

        public void CloseAll()
        {
            foreach (var panel in open.Values) if (panel != null) panel.Hide();
        }

        public void Clear()
        {
            foreach (var panel in open.Values) if (panel != null) Destroy(panel.gameObject);
            open.Clear();
            foreach (var label in labels) if (label != null) Destroy(label.gameObject);
            labels.Clear();
            foreach (var grab in watched)
            {
                if (grab == null) continue;
                grab.Grabbed -= OnGrabbed;
                var pull = grab.GetComponent<Pull>();
                if (pull != null) pull.Changed -= OnPulled;
            }
            watched.Clear();
            if (scene != null) scene.Hide();
            if (destinationPanel != null) destinationPanel.Hide();
        }

        Panel ShowScene(Place place, Transform at, Panel reuse)
        {
            if (scenePrefab == null) return reuse;
            if (reuse == null) reuse = Instantiate(scenePrefab, transform);
            reuse.Show(place.panel, at);
            return reuse;
        }

        void Watch(Rig rig, Place place)
        {
            foreach (var body in rig.Bodies)
            {
                var anchor = rig.Anchor(body);
                var grab = anchor != null ? anchor.GetComponentInChildren<Grabbable>(true) : null;
                if (grab == null) continue;
                grab.Grabbed += OnGrabbed;
                watched.Add(grab);
                var pull = grab.GetComponent<Pull>();
                if (pull != null) pull.Changed += OnPulled;
                if (place.layouts == null || place.layouts.Length == 0) NameLabel(body, grab.transform);
            }
        }

        void NameLabel(Body body, Transform at)
        {
            if (namePrefab == null) return;
            var label = Instantiate(namePrefab, at);
            label.transform.localPosition = Vector3.up * 0.8f;
            label.transform.localScale = Vector3.one * 0.001f / Mathf.Max(0.0001f, at.lossyScale.x);
            label.Set(body);
            labels.Add(label);
        }

        void Tags(Place place, GameObject instance)
        {
            if (tagPrefab == null) return;
            var disc = instance.transform.Find("galaxy") ?? instance.transform;
            var radius = place.galaxy != null ? place.galaxy.widthMetres * 0.5f / discUnitsPerHalfWidth : 0.8f;
            var tier = 0;
            foreach (var destination in place.destinations)
            {
                if (destination == null) continue;
                var spot = Spot(destination.id);
                var label = Instantiate(tagPrefab, instance.transform);
                label.transform.localPosition = new Vector3(spot.x * radius, spot.y * radius + tagLiftMetres + tagTierMetres * (tier++ % 4), spot.z * radius);
                label.transform.localScale = Vector3.one * (0.001f * tagScale);
                label.Set(destination);
                label.Picked += OnTag;
                labels.Add(label);
            }
        }

        static Vector3 Spot(string id)
        {
            foreach (var spot in Spots) if (spot.id == id) return new Vector3(spot.x, spot.y, spot.z);
            return Vector3.zero;
        }

        void OnTag(Label label)
        {
            foreach (var other in labels) if (other != null) other.Selected = other == label;
            Director.Instance?.OpenDestinationPlace(label.Destination, label.transform.position);
        }

        void OnGrabbed(Grabbable grab) => Show(grab);

        void OnPulled(Pull pull, Pull.State state)
        {
            if (state == Pull.State.Loose) Show(pull.GetComponent<Grabbable>());
        }

        void Show(Grabbable grab)
        {
            if (grab == null) return;
            var body = BodyOf(grab);
            if (body == null) return;
            if (!open.TryGetValue(body, out var panel) || panel == null)
            {
                var prefab = body.kind == BodyKind.Moon ? moonPrefab : bodyPrefab;
                if (prefab == null) return;
                panel = Instantiate(prefab, transform);
                open[body] = panel;
            }
            if (!panel.Showing) panel.Show(body, grab.transform, grab.GetComponentInChildren<Renderer>());
        }

        Body BodyOf(Grabbable grab)
        {
            var rig = Director.Instance != null ? Director.Instance.CurrentRig : null;
            if (rig == null) return null;
            var id = grab.name.StartsWith("body_") ? grab.name.Substring(5) : grab.name;
            foreach (var body in rig.Bodies) if (body != null && body.id == id) return body;
            return null;
        }
    }
}
