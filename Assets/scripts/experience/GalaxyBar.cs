// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation
{
    /// <summary>
    /// The row across the top of the Galaxies sphere: every galaxy we know, and a way into the ones we have.
    ///
    /// <para>It is always there while that place is open, the way the dock is always there - no toggle, no
    /// pop-up. The pins in the sphere answer "where is Andromeda"; this answers "what else is there", which
    /// is a question you cannot answer by looking around a sphere of four hundred anonymous smudges.</para>
    ///
    /// <para><b>The unbuilt ones are listed too, and greyed.</b> Seven galaxies have research in
    /// <c>docs/research/galaxies.md</c> and no generator yet - an elliptical, a bar, tidal tails. Leaving them
    /// out would say the set is finished; showing them dimmed says what is coming and refuses the click. A
    /// player reads that as a promise; the alternative is a menu that looks complete and is not.</para>
    ///
    /// <para>Shown and hidden by <see cref="ExperienceDirector.ExperienceChanged"/> rather than by whoever
    /// opens the place: the director is the one thing that always knows where the player is, and a bar that
    /// answered to the sphere's own lifetime would flicker through every switch the sphere survives.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class GalaxyBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The place this bar belongs to. It is visible while this one is open and hidden otherwise.")]
        private ExperienceModule host;

        [SerializeField]
        [Tooltip("One per galaxy we can open. Filled by Cosmic Simulation > Build Galaxy Bar.")]
        private List<Button> entryButtons = new List<Button>();

        [SerializeField] private List<TMP_Text> entryLabels = new List<TMP_Text>();
        [SerializeField] private List<Image> entryFills = new List<Image>();

        [SerializeField]
        [Tooltip("The module each entry opens. Empty means the galaxy is not built yet and the entry is dead.")]
        private List<ExperienceModule> entries = new List<ExperienceModule>();

        [SerializeField] private CanvasGroup group;

        private static readonly Color Live = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color Coming = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color Here = new Color(0.424f, 0.812f, 0.867f);
        private static readonly Color Plate = new Color(0.055f, 0.078f, 0.094f, 0.85f);

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();

            for (var i = 0; i < entryButtons.Count; i++)
            {
                var index = i;
                if (entryButtons[i] != null) entryButtons[i].onClick.AddListener(() => Pick(index));
            }
        }

        private void OnEnable()
        {
            ExperienceDirector.ExperienceChanged += OnExperienceChanged;
            Refresh();
        }

        private void OnDisable() => ExperienceDirector.ExperienceChanged -= OnExperienceChanged;

        private void OnExperienceChanged(ExperienceModule module) => Refresh();

        private void Refresh()
        {
            var director = ExperienceDirector.Instance;
            var open = director != null ? director.Current : null;
            var show = host != null && open == host;

            if (group != null)
            {
                group.alpha = show ? 1f : 0f;
                group.blocksRaycasts = show;
                group.interactable = show;
            }

            for (var i = 0; i < entryLabels.Count; i++)
            {
                var module = i < entries.Count ? entries[i] : null;
                var built = module != null;

                if (i < entryButtons.Count && entryButtons[i] != null) entryButtons[i].interactable = built;
                if (entryLabels[i] != null) entryLabels[i].color = built ? Live : Coming;
                if (i < entryFills.Count && entryFills[i] != null)
                {
                    // The galaxy you are standing in is marked, the way the dock underlines its own tile.
                    entryFills[i].color = built && module == open ? Here : Plate;
                }
            }
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= entries.Count) return;

            var module = entries[index];
            if (module == null) return; // not built yet; the entry is a label, not a door

            var director = ExperienceDirector.Instance;
            if (director == null || director.Current == module) return;

            director.Switch(module);
        }
    }
}
