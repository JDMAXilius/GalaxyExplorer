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

                    var sky = BuildSky(spec.Id);
                    if (sky != null)
                    {
                        EditorSceneManager.MoveGameObjectToScene(sky, scene);
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

            // Starts at the middle, which is where the player arrives and the only view that matters for
            // judging the look. The nebula fills the sky from here.
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            holder.AddComponent<AudioListener>();
            holder.AddComponent<FreeLook>();
            holder.AddComponent<Bloom>();

            return holder;
        }
    }
}
