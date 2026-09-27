using System.Collections.Generic;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation.Being
{
    /// <summary>
    /// The old tree's half of the being: what it can see, what it can do, and how it is grabbed. The being itself
    /// lives in <c>Assets/Being</c> and knows nothing about this tree.
    /// </summary>
    public class Host : MonoBehaviour, Cosmic.Companion.IHost, IGEPointerHandler
    {
        public static Cosmic.Companion.Being Current { get; private set; }

        private Cosmic.Companion.Being _being;
        private ManipulationHandler _hands;

        public static void Toggle(Cosmic.Companion.Being prefab)
        {
            // Current stays set through the fade: OnDestroy clears it, so a second press during the fade is a no-op.
            if (Current != null)
            {
                Current.Dismiss();
                return;
            }
            if (prefab == null)
            {
                return;
            }
            Current = Instantiate(prefab);
            Current.gameObject.AddComponent<Host>();
        }

        public bool Held => _hands != null && _hands.IsManipulating;

        private void Awake()
        {
            _being = GetComponent<Cosmic.Companion.Being>();
            gameObject.AddComponent<GEInteractable>();
            _hands = gameObject.AddComponent<ManipulationHandler>();
            _hands.HostTransform = transform;
            _hands.ManipulationType = ManipulationHandler.HandMovementType.OneAndTwoHanded;
            gameObject.AddComponent<TouchNudge>();
            _being.Attach(this);
        }

        private void OnDestroy()
        {
            if (Current == _being)
            {
                Current = null;
            }
            Duck(false);
        }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            var attach = eventData?.Pointer != null ? eventData.Pointer.AttachTransform : null;
            _being.Press(attach != null ? attach.position : transform.position);
            if (eventData?.Pointer != null)
            {
                _hands.OnPointerDown(eventData);
            }
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
            if (eventData?.Pointer != null)
            {
                _hands.OnPointerUp(eventData);
            }
            _being.Release();
        }

        public void OnPointerClicked(GEPointerEventData eventData) { }

        public void Click() => AudioService.Instance?.PlayClip(AudioId.Select);

        public Cosmic.Companion.Situation Situation()
        {
            var director = ExperienceDirector.Instance;
            var current = director != null ? director.Current : null;
            var rig = FindAnyObjectByType<LayoutRig>();
            var held = new List<string>();
            foreach (var force in FindObjectsByType<ForceSolver>(FindObjectsSortMode.None))
            {
                if (force.ForceState == ForceSolver.State.Free || force.ForceState == ForceSolver.State.Manipulation)
                {
                    held.Add(force.name.Replace("body_", ""));
                }
            }
            return new Cosmic.Companion.Situation
            {
                place = current != null ? (string.IsNullOrEmpty(current.DisplayName) ? current.Id : current.DisplayName) : "",
                layout = rig != null && rig.Current != null ? rig.Current.Id : "",
                held = held.ToArray(),
            };
        }

        public void Act(string name, string id)
        {
            switch (name)
            {
                case "open_place":
                    ExperienceDirector.Instance?.Switch(id);
                    break;
                case "pull_body":
                    FindAnyObjectByType<LayoutRig>()?.Find(id)?.Force?.OnPointerDown();
                    break;
                case "restore":
                    FreePlacementAnchor.RestoreAll();
                    foreach (var rig in FindObjectsByType<LayoutRig>(FindObjectsSortMode.None))
                    {
                        rig.Restore();
                    }
                    break;
            }
        }

        // The being is the one voice in the room while it is awake: the beds drop under it and the narrator stops.
        public void Duck(bool on)
        {
            MusicController.Ducked = on;
            AmbienceController.Ducked = on;
            if (on)
            {
                FindAnyObjectByType<VOManager>()?.Stop(true);
            }
        }
    }
}
