// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

//using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace GalaxyExplorer
{
    public class OrbitalTrail : MonoBehaviour
    {
        public class OrbitsRenderer : SingleInstance<OrbitsRenderer>
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct OrbitDataPoint
            {
                public const int size =
                    sizeof(float) * 3 +
                    sizeof(float) * 3 +
                    sizeof(uint) +

                    sizeof(uint) +
                    sizeof(uint) +
                    sizeof(uint);

                public Vector3 realPos;
                public Vector3 schematicPos;
                public uint globalIndex;

                public uint orbitStartIndex;
                public uint orbitEntryCount;
                public uint orbitIndex;
            }
            
            static bool isInitialized = false;
            public static OrbitsRenderer GetOrCreate(Transform world)
            {
                if (!isInitialized)
                {
                    var go = new GameObject("Orbits Renderer Hook");
                    go.transform.SetParent(world, worldPositionStays: false);
                    go.AddComponent<NoAutomaticFade>();

                    var renderer = go.AddComponent<OrbitsRenderer>();
                    renderer.orbitsWorld = world;

                    isInitialized = true;
                    return renderer;
                }
                else
                {
                    return OrbitsRenderer.Instance;
                }
            }

            public Material orbitsMaterial;
            private Transform orbitsWorld;

            /// <summary>
            /// There is only one solar system at anytime, so we kill ourselves
            /// when any of our trails dies
            /// </summary>
            private List<OrbitalTrail> registeredTrails = new List<OrbitalTrail>();
            private uint currentStartIndex = 0;

            private List<OrbitDataPoint> orbitsData = new List<OrbitDataPoint>();

            private ComputeBuffer orbitsBuffer;
            private bool dataInvalidated = true;

            // Orbits are drawn by a command buffer on the main camera after transparent objects, which renders
            // correctly into both eyes on headsets (the old OnPostRender immediate-mode draw did not).
            private CommandBuffer commandBuffer;
            private Camera commandBufferCamera;
            private const CameraEvent OrbitsCameraEvent = CameraEvent.AfterForwardAlpha;

            private float previousTruthfulness = -1;

            public void AddOrbit(OrbitalTrail origin, List<Vector3> realPositions, List<Vector3> schematicPositions)
            {
                if (realPositions.Count != schematicPositions.Count)
                {
                    throw new InvalidOperationException("Real and schematic positions must have the same length");
                }

                registeredTrails.Add(origin);

                for (int i = 0; i < realPositions.Count; i++)
                {
                    var stepData = new OrbitDataPoint()
                    {
                        globalIndex = (uint)i,
                        realPos = realPositions[i],
                        schematicPos = schematicPositions[i],
                        orbitStartIndex = currentStartIndex,
                        orbitEntryCount = (uint)schematicPositions.Count,
                        orbitIndex = (uint)(registeredTrails.Count - 1)
                    };

                    orbitsData.Add(stepData);
                }

                currentStartIndex += (uint)realPositions.Count;

                dataInvalidated = true;
            }

            private void ReCreateData()
            {
                DestroyBuffers();

                if (registeredTrails.Count > 0)
                {
                    orbitsBuffer = new ComputeBuffer(orbitsData.Count, OrbitDataPoint.size);
                    orbitsBuffer.SetData(orbitsData.ToArray());
                }
            }

            protected override void OnDestroy()
            {
                RemoveCommandBuffer();
                DestroyBuffers();
                isInitialized = false;
                base.OnDestroy();
            }

            private void EnsureCommandBuffer()
            {
                var camera = Camera.main;
                if (camera == commandBufferCamera && commandBuffer != null)
                {
                    return;
                }

                RemoveCommandBuffer();
                if (camera != null)
                {
                    commandBuffer = new CommandBuffer { name = "Orbital trails" };
                    camera.AddCommandBuffer(OrbitsCameraEvent, commandBuffer);
                    commandBufferCamera = camera;
                }
            }

            private void RemoveCommandBuffer()
            {
                if (commandBuffer != null && commandBufferCamera != null)
                {
                    commandBufferCamera.RemoveCommandBuffer(OrbitsCameraEvent, commandBuffer);
                }

                commandBuffer?.Release();
                commandBuffer = null;
                commandBufferCamera = null;
            }

            private void DestroyBuffers()
            {
                if (orbitsBuffer != null)
                {
                    orbitsBuffer.Dispose();
                    orbitsBuffer = null;
                }
            }

            private void Update()
            {
                if (TrueScaleSetting.Instance && orbitsMaterial)
                {
                    if (previousTruthfulness != TrueScaleSetting.Instance.CurrentRealismScale)
                    {
                        previousTruthfulness = TrueScaleSetting.Instance.CurrentRealismScale;

                        if (previousTruthfulness == 1)
                        {
                            orbitsMaterial.DisableKeyword("IN_TRANSITION");
                            orbitsMaterial.DisableKeyword("SCHEMATIC");
                            orbitsMaterial.EnableKeyword("REALSCALE");
                        }
                        else if (previousTruthfulness == 0)
                        {
                            orbitsMaterial.DisableKeyword("IN_TRANSITION");
                            orbitsMaterial.DisableKeyword("REALSCALE");
                            orbitsMaterial.EnableKeyword("SCHEMATIC");
                        }
                        else
                        {
                            orbitsMaterial.DisableKeyword("SCHEMATIC");
                            orbitsMaterial.DisableKeyword("REALSCALE");
                            orbitsMaterial.EnableKeyword("IN_TRANSITION");
                        }
                    }
                }

                for (int i = 0; i < registeredTrails.Count; i++)
                {
                    var item = registeredTrails[i];

                    if (!item)
                    {
                        Destroy(gameObject);
                    }
                    else if (orbitsMaterial)
                    {
                        var planetPos = item.Planet.transform.position;
                        orbitsMaterial.SetVector("planetPositionsAndRadius" + i.ToString(), new Vector4(planetPos.x, planetPos.y, planetPos.z, 0));
                    }
                }

                if (dataInvalidated)
                {
                    dataInvalidated = false;
                    ReCreateData();
                }

                EnsureCommandBuffer();
                RenderOrbits();
            }

            private void RenderOrbits()
            {
                if (commandBuffer == null)
                {
                    return;
                }

                commandBuffer.Clear();
                if (orbitsMaterial && orbitsBuffer != null && orbitsWorld && orbitsWorld.gameObject.activeInHierarchy)
                {
                    orbitsMaterial.SetBuffer("_OrbitsData", orbitsBuffer);
                    orbitsMaterial.SetMatrix("_Orbits2World", orbitsWorld.transform.localToWorldMatrix);
                    orbitsMaterial.SetFloat("_GlobalScale", orbitsWorld.lossyScale.x);

                    // Each orbit point draws one segment quad: two triangles, six vertices (see the shader).
                    commandBuffer.DrawProcedural(Matrix4x4.identity, orbitsMaterial, 0, MeshTopology.Triangles, orbitsData.Count * 6);
                }
            }
        }

        public Material orbitMaterial;

        [SerializeField]
        private OrbitUpdater Planet;

        private float originalGlobalScale;
        private float originalWidth;

        public int orbitStepCount = 50;

        public PointOfInterest pointOfInterestMarker;
        public float pointOfInterestAngle;

        public bool snapPointOfInterestToPosition;

        public GameObject pointOfInterestTarget;
        public GameObject container;

        private void Awake()
        {
            // We put the trail object under the planet, but we want it to be the parented to the parent of the planet ...
            // which will be the solar system
            if (Planet == null)
            {
                Planet = GetComponentInParent<OrbitUpdater>();
            }

            if (container == null)
            {
                container = new GameObject("Orbit Container " + Planet.name);
                container.transform.SetParent(Planet.transform.parent, worldPositionStays: false);
            }

            container.AddComponent<NoAutomaticFade>();

            StartCoroutine(AwakeCoroutine());
        }

        // Following functionality is very expensive and doesnt really need to happen in awake so delay it a bit to reduce the transition overhead
        private IEnumerator AwakeCoroutine()
        {
            yield return new WaitForEndOfFrame(); 
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            List<Vector3> realPositions, schematicPositions;

            GeneratePositionsForOrbit(out realPositions, out schematicPositions);

            var orbitsRenderer = OrbitsRenderer.GetOrCreate(Planet.transform.parent);
            orbitsRenderer.AddOrbit(this, realPositions, schematicPositions);
            originalGlobalScale = orbitMaterial.GetFloat("_GlobalScale");
            originalWidth = orbitMaterial.GetFloat("_Width");
            if (GalaxyExplorerManager.IsImmersiveHMD || GalaxyExplorerManager.IsQuest3)
            {
                orbitMaterial.SetFloat("_Width", GalaxyExplorerManager.OrbitalTrailFixedWidth);
            }

            orbitsRenderer.orbitsMaterial = orbitMaterial;
        }

        private void GeneratePositionsForOrbit(out List<Vector3> realPositions, out List<Vector3> schematicPositions)
        {
            var orbitStep = Planet.CurrentPeriod / orbitStepCount;

            var orbitStepTime = TimeSpan.FromDays(orbitStep);
            var currentDate = DateTime.MinValue;

            realPositions = new List<Vector3>();
            schematicPositions = new List<Vector3>();

            for (float currentStep = 0; currentStep < Planet.CurrentPeriod; currentStep += orbitStep)
            {
                Planet.Reality = 1;
                var realStepPosition = Planet.CalculatePosition(currentDate);

                Planet.Reality = 0;
                var schematicPosition = Planet.CalculatePosition(currentDate);

                realPositions.Add(realStepPosition);
                schematicPositions.Add(schematicPosition);

                currentDate += orbitStepTime;
            }

            realPositions.RemoveAt(realPositions.Count - 1);
            schematicPositions.RemoveAt(schematicPositions.Count - 1);
        }

        private void Start()
        {
            // create a point of interest target position
            if (pointOfInterestTarget == null && pointOfInterestMarker != null && Planet != null && container != null)
            {
                pointOfInterestTarget = new GameObject("POI_OrbitTarget");
                pointOfInterestTarget.transform.localPosition = Planet.CalculatePosition(pointOfInterestAngle * Mathf.Deg2Rad);
                pointOfInterestTarget.transform.localRotation = Quaternion.identity;
                pointOfInterestTarget.transform.localScale = Vector3.one;
                pointOfInterestTarget.transform.SetParent(container.transform, false);

                if (pointOfInterestMarker.GetIndicatorLine != null)
                {
                    pointOfInterestMarker.GetIndicatorLine.points[0] = pointOfInterestTarget.transform;
                }
            }
            else if (pointOfInterestTarget != null)
            {
                pointOfInterestTarget.transform.localPosition = Planet.CalculatePosition(pointOfInterestAngle * Mathf.Deg2Rad);
                pointOfInterestTarget.transform.localRotation = Quaternion.identity;
                pointOfInterestTarget.transform.localScale = Vector3.one;

                if (pointOfInterestMarker != null && pointOfInterestMarker.GetIndicatorLine != null)
                {
                    if (pointOfInterestMarker.GetIndicatorLine.points.Length > 0)
                    {
                        pointOfInterestMarker.GetIndicatorLine.points[0] = pointOfInterestTarget.transform;
                    }
                }
            }
        }

        private void Update()
        {
            if (pointOfInterestTarget != null && Planet != null)
            {
                if (snapPointOfInterestToPosition)
                {
                    pointOfInterestTarget.transform.position = Planet.transform.position;
                }
                else
                {
                    pointOfInterestTarget.transform.localPosition = Planet.CalculatePosition(pointOfInterestAngle * Mathf.Deg2Rad);
                }
            }
        }

        private void OnDestroy()
        {
            orbitMaterial.SetFloat("_GlobalScale", originalGlobalScale);
            orbitMaterial.SetFloat("_Width", originalWidth);
        }
    }
}