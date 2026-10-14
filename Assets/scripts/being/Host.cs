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

        private const float PullWaitSeconds = 15f;

        private Cosmic.Companion.Being _being;
        private ManipulationHandler _hands;
        private string _pull;
        private float _pullUntil;

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
            var interactable = gameObject.AddComponent<GEInteractable>();
            // A fingertip on the sphere, from any side, is a press (CS-276); the poke interactors refuse an
            // interactable that has no filter.
            PokeSupport.AddPokeFilter(gameObject, GetComponent<Collider>(), interactable);
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

        public Cosmic.Companion.Situation Situation() => Read();

        /// <summary>What the player has in front of them; also read by the guide panel for its suggestions.</summary>
        public static Cosmic.Companion.Situation Read()
        {
            var director = ExperienceDirector.Instance;
            var current = director != null ? director.Current : null;
            var rig = Registry<LayoutRig>.First;
            var held = new List<string>();
            var bodies = new List<string>();
            foreach (var force in Registry<ForceSolver>.Active)
            {
                var id = BodyId(force);
                if (id == null)
                {
                    continue;
                }
                if (force.ForceState == ForceSolver.State.Free || force.ForceState == ForceSolver.State.Manipulation)
                {
                    held.Add(id);
                }
                else if (force.ForceState == ForceSolver.State.Root && Pullable(force) && !bodies.Contains(id))
                {
                    bodies.Add(id);
                }
            }

            // The map's markers, minus the place itself: the ids open_place can take from here.
            var destinations = new List<string>();
            foreach (var poi in FindObjectsByType<PointOfInterest>(FindObjectsSortMode.None))
            {
                var module = poi is CardPOI card ? card.Destination : poi is PlanetPOI planet ? planet.Destination : null;
                var entry = module != null ? Named(module.DisplayName, module.Id) : null;
                if (entry != null && module != current && poi.gameObject.activeInHierarchy && !destinations.Contains(entry))
                {
                    destinations.Add(entry);
                }
            }

            var explore = UI2.Ui2Explore.Instance;
            var selected = explore != null ? explore.Selected : null;
            var previewing = explore != null ? explore.Previewing : null;
            return new Cosmic.Companion.Situation
            {
                place = current != null ? (string.IsNullOrEmpty(current.DisplayName) ? current.Id : current.DisplayName) : "",
                placeId = current != null ? current.Id : "",
                layout = rig != null && rig.Current != null ? rig.Current.Id : "",
                held = held.ToArray(),
                selected = selected != null ? Named(selected.DisplayName, selected.Id) : "",
                previewing = previewing != null && previewing != current ? Named(previewing.DisplayName, previewing.Id) : "",
                bodies = bodies.ToArray(),
                destinations = destinations.ToArray(),
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
                    // The being often opens the place and pulls in one breath; the body arrives with the place.
                    _pull = id;
                    _pullUntil = Time.unscaledTime + PullWaitSeconds;
                    TryPull();
                    break;
                case "send_home":
                    var home = Find(id);
                    if (home != null && home.ForceState != ForceSolver.State.Root)
                    {
                        home.ReturnHome();
                    }
                    break;
                case "restore":
                    Arrangement.Restore();
                    break;
                case "play_sound":
                    UI2.Ui2Explore.Instance?.PlaySelectionSound();
                    break;
            }
        }

        private void Update()
        {
            if (_pull != null)
            {
                TryPull();
            }
        }

        private void TryPull()
        {
            var director = ExperienceDirector.Instance;
            if (Time.unscaledTime > _pullUntil)
            {
                _pull = null;
                return;
            }
            if (director != null && director.IsSwitching)
            {
                return;
            }
            // Only from Root: a press on a body that is already out sends it home instead.
            var force = Find(_pull);
            if (force == null)
            {
                return;
            }

            // A moon comes off its planet only while the planet is out: bring the planet out first, then wait.
            var orbit = force.GetComponent<MoonOrbit>();
            if ((orbit != null || force is MoonForceSolver) && !Pullable(force))
            {
                var planet = orbit != null ? orbit.ParentBody : ((MoonForceSolver)force).ParentPlanet;
                if (planet != null && planet.ForceState == ForceSolver.State.Root)
                {
                    planet.OnPointerDown();
                }
                return;
            }

            if (force.ForceState == ForceSolver.State.Root)
            {
                force.OnPointerDown();
            }
            _pull = null;
        }

        /// <summary>The body id a solver answers to: its rig entry in the row of planets, else its <c>poi_&lt;id&gt;_prefab</c>.</summary>
        private static string BodyId(ForceSolver force)
        {
            if (force == null || !force.isActiveAndEnabled)
            {
                return null;
            }
            var rig = Registry<LayoutRig>.First;
            if (rig != null)
            {
                foreach (var body in rig.Bodies)
                {
                    if (body != null && body.Force == force)
                    {
                        return body.Id;
                    }
                }
            }
            // A moon goes by its own copy: in the row it carries a MoonOrbit; in the orbit view it sits under its
            // planet's prefab and would answer to the planet's id.
            var orbit = force.GetComponent<MoonOrbit>();
            if (orbit != null)
            {
                return orbit.Info != null ? orbit.Info.Id : null;
            }
            return force is MoonForceSolver ? MoonId(force) : PlanetPOI.BodyIdOf(force.transform);
        }

        private static string MoonId(ForceSolver force)
        {
            foreach (var panel in force.GetComponentsInChildren<InfoPanel>(true))
            {
                if (panel.Info != null && panel.Info.IsMoon)
                {
                    return panel.Info.Id;
                }
            }
            var up = force.GetComponentInParent<InfoPanel>(true);
            return up != null && up.Info != null && up.Info.IsMoon ? up.Info.Id : null;
        }

        // Pullable now: something of it can be pinched. A moon's colliders stay off until its planet is out.
        private static bool Pullable(ForceSolver force)
        {
            foreach (var collider in force.GetComponentsInChildren<Collider>(false))
            {
                if (collider.enabled)
                {
                    return true;
                }
            }
            return false;
        }

        private static ForceSolver Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            var entry = Registry<LayoutRig>.First?.Find(id);
            if (entry != null && entry.Force != null)
            {
                return entry.Force;
            }
            foreach (var force in Registry<ForceSolver>.Active)
            {
                if (BodyId(force) == id)
                {
                    return force;
                }
            }
            return null;
        }

        private static string Named(string name, string id) =>
            string.IsNullOrEmpty(id) ? null : string.IsNullOrEmpty(name) ? id : $"{name} ({id})";

        // The being is the one voice in the room while it is awake: the beds drop under it and the narrator stops.
        public void Duck(bool on)
        {
            MusicController.Ducked = on;
            AmbienceController.Ducked = on;
            if (on)
            {
                Registry<VOManager>.First?.Stop(true);
            }
        }
    }
}
