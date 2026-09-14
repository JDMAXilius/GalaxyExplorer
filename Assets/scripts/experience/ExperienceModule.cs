// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>How much of the player's room is visible while an experience is open.</summary>
    public enum EnvironmentMode
    {
        /// <summary>The room as it is.</summary>
        Passthrough,

        /// <summary>The room at about half brightness, so virtual objects read against it.</summary>
        Dimmed,

        /// <summary>Dimmed, plus a soft black disc behind the subject.</summary>
        BlackHalo,

        /// <summary>No room at all: black space and stars.</summary>
        FullBlack,
    }

    /// <summary>Where an entry appears: a tile on the dock, or a destination opened from the Milky Way map.</summary>
    public enum ExperienceKind
    {
        DockTile,
        Destination,
    }

    /// <summary>Title and prose for a panel that describes a place rather than a body.</summary>
    [Serializable]
    public class ScenePanelCopy
    {
        public string Title;

        [TextArea(2, 6)]
        public string[] Paragraphs = Array.Empty<string>();

        [Tooltip("Shown last, in the secondary colour: what the player can do here.")]
        [TextArea(1, 3)]
        public string Instruction;
    }

    /// <summary>
    /// One place the player can be: a dock tile (Milky Way, Solar System, ...) or a destination opened from the
    /// Milky Way map (a nebula). Holds everything that differs between places, so the code that switches between
    /// them has no special cases. Authored in <c>docs/copy/</c> and imported by <c>CopyImporter</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Cosmic Simulation/Experience Module", fileName = "experience_module")]
    public class ExperienceModule : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable key, matching the heading in docs/copy/. Never shown to the player.")]
        public string Id;

        public ExperienceKind Kind = ExperienceKind.DockTile;

        [Tooltip("Written on the dock tile's picture.")]
        public string DisplayName;

        [Tooltip("Second line on the tile, only for the tiles that offer a layout choice.")]
        public string SecondLine;

        [Tooltip("Picture of the place, rendered from the scene.")]
        public Sprite DockThumbnail;

        [Header("Content")]
        [Tooltip("Scene loaded additively for a dock tile. Destinations leave this empty and use ContentPrefab.")]
        public string SceneName;

        [Tooltip("Spawned in front of the player for a destination overlay.")]
        public GameObject ContentPrefab;

        public EnvironmentMode Environment = EnvironmentMode.Dimmed;

        public ScenePanelCopy Panel = new ScenePanelCopy();

        [Tooltip("Named arrangements of this experience's objects. The first is the one it opens with.")]
        public LayoutPreset[] Layouts = Array.Empty<LayoutPreset>();

        [Header("Audio")]
        public AudioClip Narration;
        public AudioClip Ambience;

        public bool HasLayoutChoice => Layouts != null && Layouts.Length > 1;

        [Tooltip("Places this tile offers instead of opening one itself: the Galaxies tile lists the galaxies " +
                 "we know, and picking one opens it as its own place, exactly as Andromeda's tile does. Leave " +
                 "empty for a tile that simply opens.")]
        public ExperienceModule[] Places = Array.Empty<ExperienceModule>();

        /// <summary>
        /// Whether this tile offers a choice of places. A single entry is still a choice worth showing - a
        /// list of one is what a second galaxy being added looks like on the way - so unlike
        /// <see cref="HasLayoutChoice"/> this does not require two.
        /// </summary>
    }
}
