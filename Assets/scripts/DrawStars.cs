// Copyright Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace GalaxyExplorer
{
    public class DrawStars : MonoBehaviour
    {
        private float originalTransitionAlpha;

        private RenderTargetIdentifier _downRezId, _medRezId, _highRezId;

        private Camera _mainCamera;
        private readonly Dictionary<Camera, CommandBuffer> _cameraToCommandBuffer = new Dictionary<Camera, CommandBuffer>();
        private readonly HashSet<Camera> _attachedCameras = new HashSet<Camera>();

        // Layers must execute in creation order (clouds -> shadow -> stars): the clouds layer clears the
        // downscaled target and the shadow layer's final copy overwrites the camera target.
        private static readonly List<DrawStars> Instances = new List<DrawStars>();

        private ComputeBuffer starsData;
        private bool isFirst;
        private bool _useDownscaledTarget;

        [SerializeField]
        private CameraEvent cameraEvent = CameraEvent.BeforeForwardOpaque;

        public float Age;
        public Material starsMaterial;

        public int starCount;

        public SpiralGalaxy galaxy;

        public Material screenComposeMaterial;

        public bool renderIntoDownscaledTarget;
        public MeshRenderer referenceQuad;

        private static readonly int Stars = Shader.PropertyToID("_Stars");
        private static readonly int LocalCamDir = Shader.PropertyToID("_LocalCamDir");
        private static readonly int WsScale = Shader.PropertyToID("_WSScale");
        private static readonly int PColor = Shader.PropertyToID("_Color");
        private static readonly int EllipseSize = Shader.PropertyToID("_EllipseSize");
        private static readonly int FuzzySideScale = Shader.PropertyToID("_FuzzySideScale");
        private static readonly int CamPos = Shader.PropertyToID("_CamPos");
        private static readonly int CamForward = Shader.PropertyToID("_CamForward");
        private static readonly int PAge = Shader.PropertyToID("_Age");
        private static readonly int TransitionAlpha = Shader.PropertyToID("_TransitionAlpha");

        private void Awake()
        {
            // SpiralGalaxy creates its drawers one frame apart in layer order, so Awake order is layer order.
            Instances.Add(this);
        }

        private IEnumerator Start()
        {
            while (!Camera.main)
            {
                yield return null;
            }

            _mainCamera = Camera.main;

            if (referenceQuad && referenceQuad.sharedMaterial)
            {
                originalTransitionAlpha = referenceQuad.sharedMaterial.GetFloat(TransitionAlpha);
            }

            starsMaterial.SetBuffer(Stars, starsData);
        }

        public void CreateBuffers(StarVertDescriptor[] stars)
        {
            // On a headset the camera renders to stereo eye textures, which the plain 2D downscaled targets
            // can't feed, so every layer draws straight into the eye buffer (also cheaper on a mobile GPU).
            _useDownscaledTarget = renderIntoDownscaledTarget && !XRSettings.isDeviceActive;
            if (_useDownscaledTarget)
            {
                isFirst = RenderTexturesBucket.CreateIfNeeded(galaxy.gameObject);
                _downRezId = new RenderTargetIdentifier(RenderTexturesBucket.Instance.downRez);
                _medRezId = new RenderTargetIdentifier(RenderTexturesBucket.Instance.downRezMed);
                _highRezId = new RenderTargetIdentifier(RenderTexturesBucket.Instance.downRezHigh);
            }

            starsData = new ComputeBuffer(stars.Length, StarVertDescriptor.StructSize);
            starsData.SetData(stars);
            starCount = stars.Length;
        }

        private static void DisposeBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null) return;
            buffer.Dispose();
            buffer = null;
        }

        private void OnDestroy()
        {
            if (referenceQuad && referenceQuad.sharedMaterial)
            {
                referenceQuad.sharedMaterial.SetFloat("_TransitionAlpha", originalTransitionAlpha);
            }

            DisposeBuffer(ref starsData);
            Instances.Remove(this);
        }

        private void OnDisable()
        {
            foreach (var cam in _attachedCameras)
            {
                if (cam)
                {
                    cam.RemoveCommandBuffer(cameraEvent, _cameraToCommandBuffer[cam]);
                }
            }

            // Forget attachment so the buffers are re-added when the galaxy is shown again.
            _attachedCameras.Clear();
        }

        private void OnDrawGizmos()
        {
            UpdateCamera(true);
        }

        private void UpdateCamera(bool isSceneView = false)
        {
            // Camera.current is only valid inside render callbacks (null during Update in Unity 6),
            // so the game view uses the cached main camera and only the Scene view uses Camera.current.
            var cam = isSceneView ? Camera.current : _mainCamera;
            if (cam == null) return;
            if (!_cameraToCommandBuffer.TryGetValue(cam, out var cb))
            {
                cb = new CommandBuffer { name = "Galaxy stars" };
                _cameraToCommandBuffer.Add(cam, cb);
            }
            else
            {
                cb.Clear();
            }
            UpdateCommandBuffer(cb, isSceneView);

            if (!_attachedCameras.Contains(cam))
            {
                AttachAllInOrder(cam);
            }
        }

        private static void AttachAllInOrder(Camera cam)
        {
            foreach (var drawer in Instances)
            {
                if (drawer._attachedCameras.Remove(cam))
                {
                    cam.RemoveCommandBuffer(drawer.cameraEvent, drawer._cameraToCommandBuffer[cam]);
                }
            }

            foreach (var drawer in Instances)
            {
                if (drawer.isActiveAndEnabled && drawer._cameraToCommandBuffer.TryGetValue(cam, out var buffer))
                {
                    cam.AddCommandBuffer(drawer.cameraEvent, buffer);
                    drawer._attachedCameras.Add(cam);
                }
            }
        }

        private void UpdateCommandBuffer(CommandBuffer commandBuffer, bool isSceneView = false)
        {
            if (_useDownscaledTarget)
            {
                commandBuffer.SetRenderTarget(_downRezId);

                if (isFirst)
                {
                    commandBuffer.ClearRenderTarget(true, true, Color.clear);
                }
            }

            // Each star is a quad expanded by the vertex shader: two triangles, six vertices (see StarQuad.cginc).
            commandBuffer.DrawProcedural(galaxy.transform.localToWorldMatrix, starsMaterial, 0, MeshTopology.Triangles, starCount * 6);

            if (!_useDownscaledTarget) return;
            commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

            if (isFirst) return;
            commandBuffer.SetGlobalTexture("_MainTex", _downRezId);
            commandBuffer.Blit(_downRezId, _medRezId, screenComposeMaterial, 0);
            commandBuffer.SetGlobalTexture("_MainTex", _medRezId);
            commandBuffer.Blit(_medRezId, _highRezId, screenComposeMaterial, 0);
            commandBuffer.SetGlobalTexture("_MainTex", _highRezId);
            commandBuffer.Blit(_highRezId, BuiltinRenderTextureType.CameraTarget);
        }

        private void Update()
        {
            if (!enabled || !galaxy.gameObject.activeInHierarchy || !_mainCamera)
            {
                OnDisable();
                return;
            }

            var mainCamTransform = _mainCamera.transform;

            if (renderIntoDownscaledTarget)
            {
                if (referenceQuad)
                {
                    referenceQuad.sharedMaterial.SetFloat(TransitionAlpha, galaxy.TransitionAlpha);
                }
            }

            var wsScale = galaxy.worldSpaceScale * galaxy.transform.lossyScale.x;

            var camDir = galaxy.transform.InverseTransformPoint(mainCamTransform.position).normalized;

            if (!renderIntoDownscaledTarget)
            {
                wsScale *= Mathf.Clamp01(4 * Math.Max(.1f, Mathf.Abs(camDir.y * .1f)));
            }
            else if (galaxy.isShadow)
            {
                var scaleMultiplier = 1 - Mathf.Clamp01(4 * Math.Max(.1f, Mathf.Abs(camDir.y * .1f)));
                wsScale *= scaleMultiplier;
            }

            starsMaterial.SetVector(LocalCamDir, camDir);
            starsMaterial.SetFloat(WsScale, wsScale);

            starsMaterial.SetVector(PColor, galaxy.tint * galaxy.tintMult * Mathf.Lerp(galaxy.verticalTintMultiplier.x, galaxy.verticalTintMultiplier.y, Mathf.Abs(camDir.y)));

            starsMaterial.SetVector(EllipseSize, new Vector4(galaxy.XRadii, galaxy.ZRadii, galaxy.MinEllipseScale, galaxy.MaxEllipseScale));
            starsMaterial.SetVector(FuzzySideScale, galaxy.FuzzySideScale);
            starsMaterial.SetVector(CamPos, mainCamTransform.position);
            starsMaterial.SetVector(CamForward, mainCamTransform.forward);
            starsMaterial.SetFloat(PAge, Age);

            starsMaterial.SetFloat(TransitionAlpha, galaxy.TransitionAlpha);

            UpdateCamera();
        }
    }
}