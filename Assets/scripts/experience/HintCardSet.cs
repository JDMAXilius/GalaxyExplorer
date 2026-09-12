// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The gesture a hint card is teaching, and therefore what the player can do to answer it. The card also
    /// times out and can be poked or clicked away, so this is only ever the *earned* dismissal.
    /// </summary>
    public enum HintAction
    {
        /// <summary>Nothing to do: the card only times out or is dismissed by hand.</summary>
        None = 0,

        /// <summary>The player put a hand on something and it came with them.</summary>
        Grab = 1,

        /// <summary>The player changed the size of what they were holding.</summary>
        Resize = 2,
    }

    /// <summary>
    /// One of the two cards from GDD 8.6. Authored in <c>docs/copy/hints.md</c> and imported by
    /// <c>HintCardBuilder</c>; nobody types player-facing text into Unity.
    /// </summary>
    [Serializable]
    public class HintCard
    {
        [Tooltip("Stable key, matching the heading in docs/copy/hints.md. Never shown to the player.")]
        public string Id;

        [Tooltip("The bold line at the top of the card.")]
        public string Title;

        [Tooltip("The one sentence under it.")]
        [TextArea(1, 3)]
        public string Line;

        [Tooltip("What the player can do to answer this card early.")]
        public HintAction DismissOn = HintAction.None;

        [Tooltip("Cross-faded in order, on a loop. Two frames is a gesture; one is a still picture.")]
        public Sprite[] Frames = Array.Empty<Sprite>();

        [Tooltip("Show the art twice, the second copy mirrored, so the pair separates and comes back. " +
                 "This is what makes the two-hand card read as two hands.")]
        public bool TwoHanded;

        [Tooltip("Seconds one turn of the loop takes. GDD 8.6 asks for 3.")]
        public float LoopSeconds = 3f;

        [Tooltip("Seconds the card stays up when the player does nothing. GDD 8.6 asks for 6.")]
        public float HoldSeconds = 6f;

        [Tooltip("How far the art travels over one loop, in canvas units - millimetres on the 200 x 120 mm " +
                 "card. Scaled to pixels in desktop mode so the motion reads the same on a monitor.")]
        public float TravelUnits = 14f;
    }

    /// <summary>
    /// Both hint cards, in the order they are shown.
    ///
    /// This lives in a <c>Resources</c> folder on purpose. <see cref="HintCards"/> builds its own canvas and is
    /// created on demand by whoever first asks for a hint, so there is no prefab and no scene object to hang
    /// sprite references off - and a sprite nothing in a scene references is not in the player build at all. One
    /// asset in Resources fixes both: the component finds its own content, and the content is guaranteed to ship.
    /// <c>AudioServiceProfile</c> is loaded the same way for the same reason.
    /// </summary>
    [CreateAssetMenu(menuName = "Cosmic Simulation/Hint Card Set", fileName = "hint_cards")]
    public class HintCardSet : ScriptableObject
    {
        /// <summary>The file name, without extension, inside any <c>Resources</c> folder.</summary>
        public const string ResourceName = "hint_cards";

        [Tooltip("Shown in this order, one after the other.")]
        public HintCard[] Cards = Array.Empty<HintCard>();

        /// <summary>The authored set, or null when nobody has run <c>Cosmic Simulation > Build Hint Cards</c>.</summary>
        public static HintCardSet Load() => Resources.Load<HintCardSet>(ResourceName);
    }
}
