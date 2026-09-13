// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The named galaxies standing in the Galaxies sphere, each where it really is in the sky, each a way in.
    ///
    /// <para><b>Why this is not <see cref="DestinationTags"/>.</b> That one reads a module and decides what a
    /// pick means: a module with a <c>SceneName</c> is switched to, one with only a <c>ContentPrefab</c> is
    /// opened as an overlay <i>over</i> the map you are standing on. Every galaxy here has a content prefab
    /// and no scene name, so the same rule would open Andromeda as a poster hanging in front of the sphere.
    /// A galaxy is a place you travel to, one-to-one with Andromeda's own dock tile, so a pick here is always
    /// <see cref="ExperienceDirector.Switch(ExperienceModule)"/>. The difference is the whole point of the
    /// component and not worth a flag on the other one.</para>
    ///
    /// <para>The pins are written by <c>Cosmic Simulation > Build Galaxy Pins</c>, which places each one along
    /// its galactic bearing and hangs that galaxy's own portrait there. Nothing in here knows about bearings:
    /// by the time this runs, a pin is a label and a picture at a position somebody else worked out.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class GalaxyPins : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The pins this node owns. Left empty, every LabelButton below this object is collected.")]
        private LabelButton[] pins = Array.Empty<LabelButton>();

        private readonly List<LabelButton> _bound = new List<LabelButton>();
        private bool _ready;

        private void OnEnable()
        {
            EnsureInit();
            foreach (var pin in _bound)
            {
                if (pin != null) pin.OnPicked.AddListener(OnPicked);
            }

            ExperienceDirector.ExperienceChanged += OnExperienceChanged;
            RefreshSelection();
        }

        private void OnDisable()
        {
            foreach (var pin in _bound)
            {
                if (pin != null) pin.OnPicked.RemoveListener(OnPicked);
            }

            ExperienceDirector.ExperienceChanged -= OnExperienceChanged;
        }

        private void EnsureInit()
        {
            if (_ready) return;
            _ready = true;

            _bound.Clear();
            if (pins != null && pins.Length > 0)
            {
                foreach (var pin in pins)
                {
                    if (pin != null) _bound.Add(pin);
                }
            }
            else
            {
                _bound.AddRange(GetComponentsInChildren<LabelButton>(true));
            }
        }

        private void OnExperienceChanged(ExperienceModule module) => RefreshSelection();

        /// <summary>Underlines the galaxy you are standing in, if it is one of these.</summary>
        private void RefreshSelection()
        {
            var open = ExperienceDirector.Instance != null ? ExperienceDirector.Instance.Current : null;
            foreach (var pin in _bound)
            {
                if (pin != null) pin.SetSelected(pin.Destination != null && pin.Destination == open);
            }
        }

        private void OnPicked(LabelButton pin)
        {
            if (pin == null || pin.Destination == null) return;

            var director = ExperienceDirector.Instance;
            if (director == null)
            {
                Debug.LogWarning($"GalaxyPins: no ExperienceDirector, so '{pin.Destination.Id}' cannot be opened. " +
                                 "core_systems_scene has to be loaded for a pin to go anywhere.", this);
                return;
            }

            // Already there: a pin for the place you are in is a label, not a door. Re-opening would clear the
            // room and grow the same galaxy in from a point again.
            if (director.Current == pin.Destination) return;

            director.Switch(pin.Destination);
        }
    }
}
