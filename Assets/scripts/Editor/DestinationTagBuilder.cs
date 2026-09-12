// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using GalaxyExplorer.XR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes the nine destination tags of GDD 4.3 into <c>galaxy_pois_prefab</c>: a
    /// <c>label_button_prefab</c> per destination, its module assigned, its name taken from that module, and a
    /// <see cref="DestinationTags"/> on the node above them that routes every pick.
    ///
    /// <b>Into the existing POI prefab rather than a new one.</b> Three reasons, in order of weight. The prefab
    /// is already referenced from <c>galaxy_view_scene</c>, so nothing has to be added to a scene — and a scene
    /// edit is the one thing in this job that cannot be verified from the repo. The old POI markers' own
    /// positions live in exactly this space, so the tags inherit the placements the original app spent time on
    /// instead of new ones invented in a text editor. And the tags then fade, load and unload with the map they
    /// belong to, which a second prefab dropped into the scene would have to be taught to do.
    ///
    /// <b>Units.</b> Everything in <see cref="Placements"/> is in the local space of the prefab's <c>POIs</c>
    /// root. That space has a deliberately odd local scale — (0.4, 2.5, 0.4) — which the scene cancels exactly:
    /// <c>GrabArea</c> above it is (2.5, 0.4, 2.5), with no rotation or offset between the two, so the product
    /// is the identity and a child of <c>POIs</c> is in undistorted metres of the galaxy's own frame. That is
    /// why the tags go under <c>POIs</c> unscaled and why they are *not* uniform when the prefab is opened on
    /// its own in prefab mode. Do not "fix" the squashed preview by rescaling this node; it would shear the
    /// tags in the only place they are ever seen.
    ///
    /// <b>The placements are not astrometric.</b> Six of them are the original app's, which put the Sun about
    /// 0.66 m from the centre of a 1.6 m disc and everything else on the same plane — an artistic scatter, not
    /// galactic coordinates. Helix and Orion are placed to match: same plane, radii inside the same band, in
    /// the two largest gaps in bearing so no two labels overlap. The galactic centre is the one honest one,
    /// because the centre of the galaxy really is the origin of this space.
    ///
    /// Re-running replaces the whole <c>destination_tags</c> node, so it is idempotent: running it twice leaves
    /// exactly nine tags, and running it after <c>Build UI Prefabs</c> picks up whatever the label prefab has
    /// become.
    /// </summary>
    public static class DestinationTagBuilder
    {
        private const string PoiPrefabPath = "Assets/prefabs/poi_prefabs/galaxy_pois_prefab.prefab";
        private const string LabelPrefabPath = "Assets/prefabs/ui/label_button_prefab.prefab";
        private const string TagRootName = "destination_tags";

        private static readonly string[] ModuleFolders =
        {
            "Assets/data/destinations",
            "Assets/data/experiences",
        };

        /// <summary>Half the pill's height, in canvas units (millimetres). The leader starts at its bottom edge.</summary>
        private const float PillHalfHeightMm = 8f;

        /// <summary>Width of the hairline, in canvas units (millimetres).</summary>
        private const float LeaderWidthMm = 0.6f;

        /// <summary>
        /// How deep the tag's collider is, in canvas units (millimetres). The label prefab draws a flat plate
        /// and ships a 2 mm box, which is enough for a ray but mean for a near pinch; this is the same
        /// widening CS-108 gave the dock's drag bar, and for the same reason.
        /// </summary>
        private const float ColliderDepthMm = 8f;

        /// <summary>One tag: which module, where its point on the disc is, and how far above it the label floats.</summary>
        private readonly struct Placement
        {
            public readonly string Id;
            public readonly Vector3 Anchor;
            public readonly float Lift;

            public Placement(string id, float x, float y, float z, float lift)
            {
                Id = id;
                Anchor = new Vector3(x, y, z);
                Lift = lift;
            }
        }

        // Anchors in POIs-local units, which the scene makes metres (see the class note).
        //
        // Seven of the nine are lifted verbatim off markers that already exist: crab, solar_system, homunculus,
        // pillars, ngc1501 and trumpler14 off the PrefabInstance root transforms in this prefab, and
        // sagittarius_a off the loose `galactic_center` marker that sits directly in galaxy_view_scene — which
        // is at the origin, and the origin really is the centre of the galaxy, so that one is not arbitrary.
        // Helix and Orion are the two the original app never had. Helix fills the gap in bearing between the
        // Sun (80 deg) and Homunculus (165 deg) at a comparable radius; Orion sits between NGC 1501 (-56 deg)
        // and Crab (-120 deg) on a shorter radius, which is at least the right way round — Orion is the
        // nearest of the nine.
        //
        // Lift is straight up, in the same units. 0.12 m clears the disc for a 60 x 16 mm plate; the two on
        // short radii are raised to 0.17 so their labels do not sit on top of the arms, and the galactic centre
        // to 0.22 to clear the bright core it names.
        private static readonly Placement[] Placements =
        {
            new Placement("helix",        -0.371f, 0f,     0.594f, 0.12f),
            new Placement("crab",         -0.409f, 0f,    -0.716f, 0.12f),
            new Placement("solar_system",  0.115f, 0f,     0.648f, 0.12f),
            new Placement("sagittarius_a", 0f,     0f,     0f,     0.22f),
            new Placement("homunculus",   -0.772f, 0f,     0.205f, 0.12f),
            new Placement("orion",        -0.047f, 0f,    -0.538f, 0.17f),
            new Placement("pillars",      -0.669f, 0f,    -0.337f, 0.12f),
            new Placement("ngc1501",       0.364f, 0.06f, -0.547f, 0.12f),
            new Placement("trumpler14",    0.328f, 0f,    -0.176f, 0.17f),
        };

        [MenuItem("Cosmic Simulation/Build Destination Tags")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("DestinationTagBuilder: leave play mode first.");
                return;
            }

            var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPrefabPath);
            if (labelPrefab == null)
            {
                Debug.LogError($"DestinationTagBuilder: {LabelPrefabPath} is missing. " +
                               "Run Cosmic Simulation > Build UI Prefabs first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PoiPrefabPath) == null)
            {
                Debug.LogError($"DestinationTagBuilder: {PoiPrefabPath} is missing.");
                return;
            }

            var modules = LoadModules();

            var contents = PrefabUtility.LoadPrefabContents(PoiPrefabPath);
            try
            {
                var built = new List<string>();
                var missing = new List<string>();

                var tagRoot = ResetTagRoot(contents.transform);
                var set = tagRoot.AddComponent<DestinationTags>();
                var buttons = new List<LabelButton>();

                foreach (var placement in Placements)
                {
                    if (!modules.TryGetValue(placement.Id, out var module))
                    {
                        // Skipped rather than built blank: a tag with no module logs on every pinch, and a
                        // silent one is worse than an absent one.
                        missing.Add(placement.Id);
                        continue;
                    }

                    var button = BuildTag(labelPrefab, tagRoot.transform, placement, module);
                    if (button == null)
                    {
                        missing.Add(placement.Id);
                        continue;
                    }

                    buttons.Add(button);
                    built.Add($"{placement.Id} -> \"{module.DisplayName}\" at " +
                              $"({placement.Anchor.x:0.###}, {placement.Anchor.y:0.###}, {placement.Anchor.z:0.###})" +
                              $" + {placement.Lift:0.##} up, " +
                              (string.IsNullOrEmpty(module.SceneName)
                                  ? (module.ContentPrefab != null ? "overlay" : "NOT BUILT YET")
                                  : $"scene {module.SceneName}"));
                }

                Bind(set, buttons);

                PrefabUtility.SaveAsPrefabAsset(contents, PoiPrefabPath);

                Debug.Log($"DestinationTagBuilder: {built.Count} of {Placements.Length} tags written into " +
                          $"{PoiPrefabPath} under '{TagRootName}'.\n" +
                          $"  built: {string.Join("\n         ", built)}\n" +
                          $"  no module asset: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------- pieces

        /// <summary>
        /// Every <see cref="ExperienceModule"/> in the two data folders, by id. Destinations and dock tiles
        /// together, because the map carries both kinds of tag.
        /// </summary>
        private static Dictionary<string, ExperienceModule> LoadModules()
        {
            var byId = new Dictionary<string, ExperienceModule>();

            foreach (var guid in AssetDatabase.FindAssets("t:ExperienceModule", ModuleFolders))
            {
                var module = AssetDatabase.LoadAssetAtPath<ExperienceModule>(AssetDatabase.GUIDToAssetPath(guid));
                if (module == null || string.IsNullOrEmpty(module.Id))
                {
                    continue;
                }

                byId[module.Id] = module;
            }

            return byId;
        }

        /// <summary>Drops whatever this builder wrote last time and makes a fresh, empty node. Idempotence.</summary>
        private static GameObject ResetTagRoot(Transform poisRoot)
        {
            // A loop, not one Find: a hand edit could have left two nodes of the same name, and leaving one
            // behind would double the tags in exactly the way this method exists to prevent.
            for (var existing = poisRoot.Find(TagRootName); existing != null; existing = poisRoot.Find(TagRootName))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var tagRoot = new GameObject(TagRootName);
            tagRoot.transform.SetParent(poisRoot, false);

            // Unscaled on purpose. The POIs node's (0.4, 2.5, 0.4) is cancelled by GrabArea above it in the
            // scene, so this frame is undistorted metres there; undoing it here would break that, not fix it.
            tagRoot.transform.localPosition = Vector3.zero;
            tagRoot.transform.localRotation = Quaternion.identity;
            tagRoot.transform.localScale = Vector3.one;
            return tagRoot;
        }

        private static LabelButton BuildTag(GameObject labelPrefab, Transform parent, Placement placement,
                                            ExperienceModule module)
        {
            var instance = PrefabUtility.InstantiatePrefab(labelPrefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = "tag_" + placement.Id;

            var t = instance.transform;
            t.localPosition = placement.Anchor + Vector3.up * placement.Lift;
            t.localRotation = Quaternion.identity;

            var button = instance.GetComponent<LabelButton>();
            if (button == null)
            {
                Debug.LogError($"DestinationTagBuilder: {LabelPrefabPath} has no LabelButton on its root.");
                Object.DestroyImmediate(instance);
                return null;
            }

            var so = new SerializedObject(button);
            so.FindProperty("destination").objectReferenceValue = module;

            // The children are read back off the component's own references rather than found by path, so a
            // rename inside UiPrefabBuilder cannot silently leave this builder writing into nothing.
            var text = so.FindProperty("label").objectReferenceValue as TMP_Text;
            var leader = so.FindProperty("leader").objectReferenceValue as Graphic;
            var grow = so.FindProperty("growTarget").objectReferenceValue as Transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetName(text, module.DisplayName);
            SetLeader(leader, placement.Lift);
            WidenCollider(grow);

            // Turned to the player about the world Y only, so the plate stays upright and level however the
            // galaxy is tilted. Billboard writes rotation away from the camera, which is the direction a
            // world-space canvas has to face to be read (the same convention InfoPanel and MoonLabel use).
            var billboard = instance.AddComponent<Billboard>();
            var billboardSo = new SerializedObject(billboard);
            billboardSo.FindProperty("pivotAxis").enumValueIndex = (int)PivotAxis.Y;
            billboardSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            return button;
        }

        private static void SetName(TMP_Text label, string displayName)
        {
            if (label == null)
            {
                return;
            }

            label.text = displayName;
            EditorUtility.SetDirty(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }

        /// <summary>
        /// Draws the hairline from the bottom of the pill down to the point on the disc.
        ///
        /// Straight down in canvas units, because the tag is lifted straight up and only ever turns about Y:
        /// a vertical line in the canvas is a vertical line in the room, whichever way the player is standing.
        /// One canvas unit is a millimetre and the label prefab's root is scaled 0.001, so a lift given in
        /// metres becomes a height in canvas units by multiplying by a thousand and nothing else.
        /// </summary>
        private static void SetLeader(Graphic leader, float liftMetres)
        {
            if (leader == null)
            {
                return;
            }

            var heightMm = liftMetres * 1000f;
            var rect = leader.rectTransform;
            rect.sizeDelta = new Vector2(LeaderWidthMm, heightMm);
            rect.anchoredPosition = new Vector2(0f, -(PillHalfHeightMm + heightMm * 0.5f));

            EditorUtility.SetDirty(leader);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        private static void WidenCollider(Transform growTarget)
        {
            if (growTarget == null || !growTarget.TryGetComponent<BoxCollider>(out var box))
            {
                return;
            }

            var size = box.size;
            box.size = new Vector3(size.x, size.y, ColliderDepthMm);
            EditorUtility.SetDirty(box);
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }

        private static void Bind(DestinationTags set, List<LabelButton> buttons)
        {
            var so = new SerializedObject(set);
            var array = so.FindProperty("tags");
            array.arraySize = buttons.Count;
            for (var i = 0; i < buttons.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
