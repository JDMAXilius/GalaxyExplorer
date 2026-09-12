// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicSimulation
{
    /// <summary>
    /// The receiving end of the destination tags on the Milky Way map (GDD 4.3).
    ///
    /// <see cref="LabelButton"/> knows how to look like a tag and how to notice a pinch, and then raises
    /// <see cref="LabelButton.OnPicked"/> and stops. Nothing in the project subscribed to that event, which is
    /// why the map's destinations never opened however correct the tag itself was. One of these owns the tags
    /// in a view, routes every pick, and keeps the selected outline on whichever tag names the thing that is
    /// currently open.
    ///
    /// A pick is dispatched on the module, because the nine tags are not all the same kind of thing:
    /// <list type="bullet">
    /// <item>A module with a <c>SceneName</c> — Solar System, Galactic Center — is a dock tile wearing a tag.
    /// It goes through <see cref="ExperienceDirector.Switch(ExperienceModule)"/>, which is the call
    /// <c>DockController.Choose</c> makes, so a tag and a tile can never drift apart.</item>
    /// <item>A module with only a <c>ContentPrefab</c> — the seven nebulae — opens as an overlay *over* the map
    /// through <see cref="ExperienceDirector.OpenDestination"/>, without leaving it.</item>
    /// <item>A module with neither is not built yet, and says so through <see cref="SwitchNotice"/> rather than
    /// swallowing the poke.</item>
    /// </list>
    ///
    /// Nothing here is desktop-specific and nothing needs to be. The tag's <c>BoxCollider</c> and
    /// <c>GEInteractable</c> share a GameObject, which is exactly what <c>DesktopMouseInput</c> raycasts for,
    /// and a mouse click arrives as <c>OnPointerClicked</c> with <c>IsNear</c> false — the same call a far hand
    /// ray makes. The one desktop addition is Escape, and that lives in the key map where the rest of it does.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestinationTags : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The tags this node owns, written by Cosmic Simulation > Build Destination Tags. " +
                 "Left empty, every LabelButton below this object is collected instead.")]
        private LabelButton[] tags = Array.Empty<LabelButton>();

        /// <summary>
        /// Where one tag's subject actually is, and how far above it the tag floats.
        /// </summary>
        [Serializable]
        public struct TagAnchor
        {
            [Tooltip("The point on the map this tag names, in this node's local space.")]
            public Vector3 Point;

            [Tooltip("How far above that point the tag hangs, in metres. Negative hangs it below.")]
            public float Lift;
        }

        [SerializeField]
        [Tooltip("One per tag, in the same order, written by Cosmic Simulation > Build Destination Tags. " +
                 "Left empty, the tags keep whatever pose the prefab authored.")]
        private TagAnchor[] anchors = Array.Empty<TagAnchor>();

        [SerializeField]
        [Tooltip("How far in front of the player a destination overlay opens, in metres. GDD 4.3 asks for 1.0 m.")]
        private float openDistanceMetres = 1f;

        [SerializeField]
        [Tooltip("How far above (+) or below (-) eye level the overlay opens, in metres. A small drop puts a " +
                 "70 cm cloud in the middle of the view rather than in the top half of it.")]
        private float openDropMetres = -0.1f;

        [SerializeField]
        [Tooltip("Width in metres to assume for a destination whose content prefab carries no grab sphere to " +
                 "measure. This is the subject's own width: BlackHalo draws its disc 1.6x larger again.")]
        private float fallbackDiameterMetres = 0.8f;

        private bool _ready;
        private bool _subscribed;

        /// <summary>The tags this node routes, in the order the builder wrote them.</summary>
        public IReadOnlyList<LabelButton> Tags
        {
            get
            {
                EnsureInit();
                return tags;
            }
        }

        private void Awake() => EnsureInit();

        private void OnEnable()
        {
            EnsureInit();
            Subscribe();

            // Read the director's state rather than waiting for its next event: the map is usually adopted
            // (ExperienceDirector.Adopt) well before this view's objects enable, so the ExperienceChanged that
            // would have told us has already been and gone.
            RefreshSelection();
        }

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// Hangs every tag straight up in the room from the point it names, however the map is turned.
        ///
        /// <para><b>Why this is not just the pose the prefab authored.</b> A tag is a child of the map, so a
        /// pose authored as "the point, plus a bit of local up" leans over as soon as the player turns the
        /// galaxy - and the leader line does not lean with it, because <c>Billboard</c> holds the card upright
        /// in the room about world Y. The result is a hairline that points at the floor next to the thing it
        /// is supposed to touch. Recomputing the offset in world space each frame keeps the two agreeing:
        /// whatever the map does, the tag is directly above its point and the leader lands on it.</para>
        ///
        /// <para>The lift is multiplied by the node's own scale so that a galaxy the player has shrunk gets a
        /// proportionally shorter leader rather than a tag floating a fixed metre above a toy. The scale is
        /// uniform by construction here - <c>POIs</c> is (0.4, 2.5, 0.4) under a <c>GrabArea</c> of
        /// (2.5, 0.4, 2.5), whose product is the identity - so one component is the whole story.</para>
        ///
        /// <para>In <c>LateUpdate</c>, after anything that moves the map has moved it. Twelve transform writes
        /// a frame, and only while the map is on screen.</para>
        /// </summary>
        private void LateUpdate()
        {
            if (anchors == null || tags == null || anchors.Length != tags.Length)
            {
                // Either nothing was written, or the two arrays have drifted apart - which would pair a tag
                // with another tag's point, and a label on the wrong object is worse than a label that leans.
                return;
            }

            var scale = transform.lossyScale.y;

            for (var i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                if (tag == null)
                {
                    continue;
                }

                tag.transform.position =
                    transform.TransformPoint(anchors[i].Point) + Vector3.up * (anchors[i].Lift * scale);
            }
        }

        // A tag set can be bound and asked to refresh in the frame it spawns, and a caller has no way to know
        // whether Awake has run, so every entry point goes through this instead of trusting that it has.
        private void EnsureInit()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;

            if (tags == null || tags.Length == 0)
            {
                // Inactive included: a tag hidden by a fade is still one of ours and must still be routed and
                // deselected when its destination closes.
                tags = GetComponentsInChildren<LabelButton>(true);
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;

            foreach (var label in tags)
            {
                if (label != null)
                {
                    label.OnPicked.AddListener(HandlePicked);
                }
            }

            ExperienceDirector.ExperienceChanged += HandleExperienceChanged;
            ExperienceDirector.DestinationChanged += HandleDestinationChanged;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;

            foreach (var label in tags)
            {
                if (label != null)
                {
                    label.OnPicked.RemoveListener(HandlePicked);
                }
            }

            ExperienceDirector.ExperienceChanged -= HandleExperienceChanged;
            ExperienceDirector.DestinationChanged -= HandleDestinationChanged;
        }

        // ---------- routing

        private void HandlePicked(LabelButton label) => Pick(label);

        /// <summary>Opens what a tag names. Public so a test harness can drive a tag without synthetic input.</summary>
        public void Pick(LabelButton label)
        {
            EnsureInit();

            if (label == null)
            {
                return;
            }

            var module = label.Destination;
            if (module == null)
            {
                Debug.LogWarning($"DestinationTags: '{label.name}' has no destination module assigned, so there " +
                                 "is nothing for it to open. Re-run Cosmic Simulation > Build Destination Tags.", label);
                return;
            }

            var director = ExperienceDirector.Instance;
            if (director == null)
            {
                Debug.LogWarning($"DestinationTags: no ExperienceDirector, so '{module.Id}' cannot be opened. " +
                                 "core_systems_scene has to be loaded for the map to go anywhere.", this);
                return;
            }

            if (!string.IsNullOrEmpty(module.SceneName))
            {
                // Deliberately Switch and not DockController.Choose: Choose needs a DockTile and would open the
                // layout pop-up beside the dock, which is not where the player's attention is when they have
                // just pinched something on the far side of the galaxy.
                director.Switch(module);
                return;
            }

            if (module.ContentPrefab == null)
            {
                // The same answer the dock gives an unfinished tile, for the same reason: a tag that does
                // nothing at all reads as broken, and this one is merely early.
                SwitchNotice.NotReady(module);
                return;
            }

            // Picking the open destination again closes it. GDD 4.3 gives the headset a close pinch outside the
            // halo and the desktop Escape, and neither of those is the thing the player is already pointing at.
            if (director.OpenDestinationModule == module)
            {
                director.ClearDestinations();
                return;
            }

            director.OpenDestination(module, OpenPosition(), Diameter(module));
        }

        /// <summary>
        /// Where an overlay opens: in front of the player, not at the tag.
        ///
        /// GDD 4.3 is explicit that the halo appears a metre in front of the player, and it has to be — the tags
        /// sit on a disc that is 1.6 m across and mostly edge-on, so a 70 cm cloud spawned at the tag would be
        /// half buried in the galaxy and, for the far tags, off to one side of the view entirely. The tag says
        /// *what* opens; the head says *where*.
        /// </summary>
        private Vector3 OpenPosition()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                // No head to measure from. The map's own node at least puts the overlay where the galaxy is,
                // which is a great deal closer than the world origin.
                return transform.position;
            }

            var head = camera.transform;

            // Flattened, like every other thing this app parks in front of the player (SwitchNotice, HintCards):
            // a player looking down at the disc should still get the cloud at eye height, not at their feet.
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            return head.position + forward * openDistanceMetres + Vector3.up * openDropMetres;
        }

        /// <summary>
        /// How wide the thing that is about to open is, in metres.
        ///
        /// Measured off the asset rather than authored here: <c>NebulaPrefabBuilder</c> sizes the overlay's grab
        /// sphere to the card width, so the prefab already carries its own width and the two cannot fall out of
        /// step when CS-052 retunes the nebulae. What comes back is the *subject's* width — <see cref="BlackHalo"/>
        /// multiplies it by 1.6 itself to get the disc, so pre-multiplying here would double the margin.
        /// </summary>
        private float Diameter(ExperienceModule module)
        {
            var prefab = module.ContentPrefab;
            if (prefab != null && prefab.TryGetComponent<SphereCollider>(out var sphere))
            {
                var scale = prefab.transform.localScale;
                var largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                var measured = sphere.radius * 2f * largest;
                if (measured > 0.01f)
                {
                    return measured;
                }
            }

            return fallbackDiameterMetres;
        }

        // ---------- selected state

        private void HandleExperienceChanged(ExperienceModule module) => RefreshSelection();

        private void HandleDestinationChanged(ExperienceModule module) => RefreshSelection();

        /// <summary>Outlines the tag that names what is open, and clears the rest.</summary>
        public void RefreshSelection()
        {
            EnsureInit();

            var director = ExperienceDirector.Instance;
            var openDestination = director != null ? director.OpenDestinationModule : null;
            var openExperience = director != null ? director.Current : null;

            foreach (var label in tags)
            {
                if (label == null)
                {
                    continue;
                }

                var module = label.Destination;

                // Both, because the two kinds of tag are open in two different senses: a nebula is a destination
                // over this place, and the Solar System is a place of its own. (The second case only shows in
                // the moment before the map's scene unloads, but leaving it out would make the tag flicker
                // rather than light up as the switch starts.)
                label.SetSelected(module != null && (module == openDestination || module == openExperience));
            }
        }
    }
}
