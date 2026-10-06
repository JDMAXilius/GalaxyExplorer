// rig_hand_look.cs - the CS-270/271/274/276 edits to Assets/prefabs/xr/ge_xr_rig.prefab, reproducibly.
//
//   node tools/mcp/umcp.js run tools/mcp/rig_hand_look.cs
//
// What it does, idempotently:
//   1. Adds GalaxyExplorer.XR.HandLook to the rig root and points its handMaterial at xr_hand_material.
//   2. Adds GalaxyExplorer.XR.AppPause to the rig root.
//   3. Strips the retired CS-274 palm probe from the rig root, and points the hand visuals' affordance
//      material helpers at slot 0 (step 4b).
//   4. On every hand SkinnedMeshRenderer under the rig (the four nested XRI "...QuestVisual" /
//      "...AndroidXRVisual" instances), sets sharedMaterials to the single xr_hand_material: the sample's
//      DepthOnly slot 0 goes, so slot 0 is the hand material HandLook filters on.
//   5. Sets the Hand Visualizer's m_HandMeshMaterial to xr_hand_material.
//   6. Saves the prefab.
//
// Fully qualified, no using directives: the runner wraps this file in its own preamble. Serialized private
// fields are set through SerializedObject because the runner forbids System.Reflection.

internal class CommandScript : IRunCommand
{
    private const string RigPath = "Assets/prefabs/xr/ge_xr_rig.prefab";
    private const string MaterialPath = "Assets/materials/xr_hand_material.mat";

    public void Execute(ExecutionResult result)
    {
        if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling)
        {
            result.Log("RIG: refused, the editor is playing or compiling.");
            return;
        }

        var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(MaterialPath);
        if (material == null)
        {
            result.Log("RIG: FAIL " + MaterialPath + " not found.");
            return;
        }

        var root = UnityEditor.PrefabUtility.LoadPrefabContents(RigPath);
        try
        {
            var log = new System.Text.StringBuilder();

            // 1. HandLook
            var look = root.GetComponent<GalaxyExplorer.XR.HandLook>();
            if (look == null)
            {
                look = root.AddComponent<GalaxyExplorer.XR.HandLook>();
                log.AppendLine("RIG: added HandLook");
            }
            else
            {
                log.AppendLine("RIG: HandLook already present");
            }

            var lookSo = new UnityEditor.SerializedObject(look);
            var handMaterialProp = lookSo.FindProperty("handMaterial");
            if (handMaterialProp == null)
            {
                result.Log("RIG: FAIL HandLook has no serialized field 'handMaterial'.");
                return;
            }

            handMaterialProp.objectReferenceValue = material;
            lookSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("RIG: HandLook.handMaterial = " + material.name);

            // 2. AppPause
            if (root.GetComponent<GalaxyExplorer.XR.AppPause>() == null)
            {
                root.AddComponent<GalaxyExplorer.XR.AppPause>();
                log.AppendLine("RIG: added AppPause");
            }
            else
            {
                log.AppendLine("RIG: AppPause already present");
            }

            // 3. The CS-274 palm probe is gone: the palm menu derives its normal from joints, so nothing reads
            //    a palm axis any more. Its component is stripped as a missing script once the file is deleted.
            var stripped = UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            log.AppendLine("RIG: removed " + stripped + " missing script(s) from the rig root");

            // 4. Hand mesh materials. Only the skinned meshes inside the four XRI hand visual instances
            //    ("...QuestVisual" / "...AndroidXRVisual"): the rig also carries skinned pinch/poke pointer
            //    visuals (FresnelHighlight) that must be left alone. Inactive included - the visualizer keeps
            //    untracked hands off and only one platform's pair is ever active.
            var single = new UnityEngine.Material[] { material };
            var hands = 0;
            foreach (var renderer in root.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true))
            {
                var path = PathOf(renderer.gameObject);
                if (IsHandVisual(renderer.transform))
                {
                    hands++;
                    var before = Describe(renderer.sharedMaterials);
                    renderer.sharedMaterials = single;
                    log.AppendLine("RIG: " + path + " materials " + before + " -> " + Describe(renderer.sharedMaterials));
                    continue;
                }

                // Not a hand. Drop any m_Materials override on it (an earlier run of this script, before the
                // filter above, put the hand material on the pinch pointers).
                var rso = new UnityEditor.SerializedObject(renderer);
                var materialsProp = rso.FindProperty("m_Materials");
                if (materialsProp != null && materialsProp.prefabOverride)
                {
                    UnityEditor.PrefabUtility.RevertPropertyOverride(materialsProp, UnityEditor.InteractionMode.AutomatedAction);
                    log.AppendLine("RIG: " + path + " reverted materials override -> " + Describe(renderer.sharedMaterials));
                }
                else
                {
                    log.AppendLine("RIG: " + path + " left alone " + Describe(renderer.sharedMaterials));
                }
            }

            if (hands != 4)
            {
                log.AppendLine("RIG: WARNING expected 4 hand renderers, found " + hands);
            }

            // 4b. The sample's affordance helpers on the hand visuals address material slot 1, which was the
            //     hand material while DepthOnly held slot 0. With one slot left they index past the array and
            //     throw every frame on device (seen 6 Oct: ~220 IndexOutOfRangeException a second).
            foreach (var helper in root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering.MaterialHelperBase>(true))
            {
                if (!IsHandVisual(helper.transform))
                {
                    continue;
                }

                if (helper.materialIndex != 0)
                {
                    var hso = new UnityEditor.SerializedObject(helper);
                    hso.FindProperty("m_MaterialIndex").intValue = 0;
                    hso.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("RIG: " + PathOf(helper.gameObject) + " affordance material index -> 0");
                }

                // These receivers tint the sample hand shader's _EdgeColor, _FingerColor_1 and _ThumbColor,
                // none of which HandOutline has, so each logs an error on first use. Off until the outline
                // shader grows a pinch tint of its own.
                if (helper.gameObject.activeSelf)
                {
                    helper.gameObject.SetActive(false);
                    log.AppendLine("RIG: " + PathOf(helper.gameObject) + " deactivated");
                }
            }

            // 5. Hand Visualizer material
            var visualizers = root.GetComponentsInChildren<UnityEngine.XR.Hands.Samples.VisualizerSample.HandVisualizer>(true);
            foreach (var visualizer in visualizers)
            {
                var vso = new UnityEditor.SerializedObject(visualizer);
                var prop = vso.FindProperty("m_HandMeshMaterial");
                if (prop == null)
                {
                    result.Log("RIG: FAIL HandVisualizer has no serialized field 'm_HandMeshMaterial'.");
                    return;
                }

                prop.objectReferenceValue = material;
                vso.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("RIG: " + PathOf(visualizer.gameObject) + " m_HandMeshMaterial = " + material.name);
            }

            if (visualizers.Length != 1)
            {
                log.AppendLine("RIG: WARNING expected 1 HandVisualizer, found " + visualizers.Length);
            }

            // 6. Save
            bool saved;
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, RigPath, out saved);
            log.AppendLine("RIG: saved=" + saved);
            result.Log(log.ToString());
        }
        finally
        {
            UnityEditor.PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsHandVisual(UnityEngine.Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.EndsWith("QuestVisual") || t.name.EndsWith("AndroidXRVisual"))
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(UnityEngine.Material[] materials)
    {
        var names = new System.Text.StringBuilder("[");
        for (var i = 0; i < materials.Length; i++)
        {
            if (i > 0) names.Append(", ");
            names.Append(materials[i] != null ? materials[i].name : "null");
        }

        return names.Append("]").ToString();
    }

    private static string PathOf(UnityEngine.GameObject go)
    {
        var path = go.name;
        for (var t = go.transform.parent; t != null; t = t.parent)
        {
            path = t.name + "/" + path;
        }

        return path;
    }
}
