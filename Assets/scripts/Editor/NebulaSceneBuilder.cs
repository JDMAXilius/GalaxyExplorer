// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicSimulation.EditorTools
{
    /// <summary>
    /// Writes one scene per nebula, so each of the seven can be opened and judged on its own.
    ///
    /// <para><b>Why a builder and not seven hand-made scenes.</b> A scene saved by hand records whatever the
    /// project happened to contain the day it was saved - a camera with the wrong clear colour, a prefab
    /// instance with a stale override, a missing component nobody noticed. Seven of those drift apart from
    /// each other within a week and then nobody can tell whether a nebula looks different because the object
    /// is different or because its scene is. Generated from one function, they differ only where the table
    /// says they differ.</para>
    ///
    /// <para>These are places to look at one nebula, not the app: the camera is the desktop <see cref="FreeLook"/>
    /// rig rather than the XR one, so a scene opens, plays, and can be flown around with a mouse. What ships is
    /// still the destination prefab, which is the same asset these scenes instantiate.</para>
    /// </summary>
    public static class NebulaSceneBuilder
    {
        private const string Folder = "Assets/scenes/nebula_scenes";

        [MenuItem("Cosmic Simulation/Build Nebula Scenes")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("NebulaSceneBuilder: refused, the editor is in play mode.");
                return;
            }

            Directory.CreateDirectory(Folder);

            var report = new System.Text.StringBuilder();
            var built = 0;
            var destinations = NebulaVolumeBuilder.Destinations();

            foreach (var spec in NebulaVolumeBuilder.AllSpecs)
            {
                if (!destinations.TryGetValue(spec.Id, out var module) || module == null ||
                    module.ContentPrefab == null)
                {
                    report.AppendLine($"  {spec.Id}: no destination prefab, skipped.");
                    continue;
                }

                // Additive, never Single: a Single open or a new Single scene throws a modal save prompt when
                // anything is dirty, and a modal dialog deadlocks the editor relay this is usually driven from.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

                try
                {
                    var camera = BuildCamera(spec);
                    EditorSceneManager.MoveGameObjectToScene(camera, scene);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(module.ContentPrefab, scene);
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                    // CentreOnViewer off, as a scene override only.
                    //
                    // It is right in the app: the player travels to a destination and should arrive standing
                    // in it. In a scene authored to frame the object from outside it is the opposite of what
                    // is wanted - the moment play begins it drags the nebula onto the camera and the framing
                    // this scene exists for is gone. Pressing play used to leave a starfield and nothing else.
                    // Overridden on the instance, so the prefab the app ships is untouched.
                    foreach (var centring in instance.GetComponentsInChildren<CentreOnViewer>(true))
                    {
                        centring.enabled = false;
                    }

                    var sky = BuildSky(spec.Id);
                    if (sky != null)
                    {
                        EditorSceneManager.MoveGameObjectToScene(sky, scene);
                    }

                    var panel = BuildPanel(spec, module, camera.transform, instance.transform);
                    if (panel != null)
                    {
                        EditorSceneManager.MoveGameObjectToScene(panel, scene);
                    }

                    var path = $"{Folder}/{spec.Id}_nebula.unity";
                    EditorSceneManager.SaveScene(scene, path);

                    built++;
                    report.AppendLine($"  {spec.Id}: {path}");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"NebulaSceneBuilder: {built} scene(s) written.\n{report}");
        }

        /// <summary>
        /// The nebula's own sky, as a real object in the scene rather than one PlaceShell builds at run time.
        ///
        /// <para>It reuses the same material the shipping place shell uses, so the two cannot drift: change the
        /// panorama once and both the app and the scene you judge it in show the same sky.</para>
        /// </summary>
        private static GameObject BuildSky(string id)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                $"Assets/materials/place_shells/place_shell_{id}_sky.mat");

            if (material == null)
            {
                Debug.LogWarning($"NebulaSceneBuilder: no sky material for '{id}', so its scene has no sky.");
                return null;
            }

            var holder = new GameObject("nebula_sky");
            holder.AddComponent<NebulaSky>().Configure(material, 90f);
            return holder;
        }

        /// <summary>
        /// The place's own panel - the same prefab the app puts up when a destination opens, bound to the same
        /// module, so the text read here is the text the player will get.
        /// </summary>
        private static GameObject BuildPanel(NebulaVolumeBuilder.Spec spec, ExperienceModule module,
            Transform camera, Transform subject)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/ui/info_panel_prefab.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("NebulaSceneBuilder: no info_panel_prefab, so the scenes get no panel. " +
                                 "Run Cosmic Simulation/Build UI Prefabs first.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "nebula_panel";

            var panel = instance.GetComponent<InfoPanel>();
            if (panel == null)
            {
                Object.DestroyImmediate(instance);
                Debug.LogWarning("NebulaSceneBuilder: info_panel_prefab has no InfoPanel component.");
                return null;
            }

            // The scene variant: a title, prose, and an instruction, with no stats grid. A nebula has no mass
            // or orbital period to put in a two-by-two, which is exactly the case that variant exists for.
            var properties = new SerializedObject(panel);
            var variant = properties.FindProperty("variant");
            if (variant != null)
            {
                variant.enumValueIndex = (int)InfoPanel.Variant.Scene;
                properties.ApplyModifiedPropertiesWithoutUndo();
            }

            // Off to one side and slightly above the middle, far enough out that it is never inside the gas.
            // The camera sits back along -Z, so +X puts it to the reader's right.
            instance.transform.position = new Vector3(
                spec.RadiusMetres * 1.05f, spec.RadiusMetres * 0.30f, -spec.RadiusMetres * 0.55f);

            instance.AddComponent<NebulaScenePanel>().Configure(panel, module, camera, subject);
            return instance;
        }

        private static GameObject BuildCamera(NebulaVolumeBuilder.Spec spec)
        {
            var holder = new GameObject("Main Camera");
            holder.tag = "MainCamera";

            var camera = holder.AddComponent<Camera>();

            // Black, not skybox. The default editor skybox is a blue-brown gradient, and a nebula judged
            // against it is being judged against a sunset - every reading of "is there enough black here" is
            // wrong before it starts.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            // HDR or there is nothing above 1.0 for the bloom pass to find, and the central star - which is
            // written at many times 1.0 on purpose - is just a white dot.
            camera.allowHDR = true;

            // Near plane short enough to stand inside gas without it clipping through the face, far plane past
            // the place shell's sky sphere at 60 m.
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 60f;

            // Starts OUTSIDE, looking at the object.
            //
            // This was at the origin, on the grounds that the middle is where the player arrives. That is
            // true of the app and it is the wrong place to open a scene you are judging against a
            // photograph, because every photograph of a nebula is taken from outside it: from the centre
            // you cannot see the object's shape at all, only gas in every direction, so nothing rendered
            // from there can ever match the reference no matter how good it is. Flying in is one scroll of
            // the wheel away - FreeLook is on this camera - so this costs nothing and fixes the comparison.
            var back = spec.RadiusMetres * 2.7f;
            holder.transform.position = new Vector3(0f, spec.RadiusMetres * 0.18f, -back);
            holder.transform.LookAt(Vector3.zero);

            holder.AddComponent<AudioListener>();
            holder.AddComponent<FreeLook>();
            holder.AddComponent<Bloom>();

            return holder;
        }
    }
}
