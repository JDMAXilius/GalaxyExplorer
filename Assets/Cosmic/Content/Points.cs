using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cosmic
{
    public class Points : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public PointCloud cloud;
            public Material material;
            public int drawIndex;
            public bool afterOpaque;
            public float worldSpaceScale;
            public Color tint;
            public float tintMultiplier;
            public Vector2 ellipseRadii;
        }

        static readonly int StarsId = Shader.PropertyToID("_Stars");
        static readonly int WsScaleId = Shader.PropertyToID("_WSScale");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");
        static readonly int AgeId = Shader.PropertyToID("_Age");
        static readonly int EllipseSizeId = Shader.PropertyToID("_EllipseSize");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int LocalCamDirId = Shader.PropertyToID("_LocalCamDir");

        public Layer[] layers = Array.Empty<Layer>();
        public float velocityRadiansPerSecond = 0.05f;

        public Camera target;
        public float sizeFactor = 0.4f;

        [SerializeField]
        Vector2 ellipseRadii = new Vector2(0.8f, 1f);

        [SerializeField]
        float alpha = 1f;

        [SerializeField]
        bool spinning = true;

        readonly List<int> order = new List<int>();
        ComputeBuffer[] buffers;
        Material[] instances;
        CommandBuffer beforeOpaque;
        CommandBuffer beforeAlpha;
        Camera attached;
        Camera cached;
        float age;
        bool built;

        public float Alpha { get => alpha; set => alpha = value; }

        public bool Spinning { get => spinning; set => spinning = value; }

        public Vector2 EllipseRadii { get => ellipseRadii; set => ellipseRadii = value; }

        public Vector3 LocalCamDir { get; private set; }

        public Material Instance(int layer) => instances != null && layer >= 0 && layer < instances.Length ? instances[layer] : null;

        public void Rebuild()
        {
            Release();
            Build();
        }

        void OnEnable() => Build();

        void OnDisable() => Release();

        void OnDestroy() => Release();

        void LateUpdate()
        {
            Build();
            if (order.Count == 0)
            {
                return;
            }

            var camera = Resolve();
            if (camera == null)
            {
                Detach();
                return;
            }

            if (camera != attached)
            {
                Detach();
                Attach(camera);
            }

            if (spinning)
            {
                age = Mathf.Repeat(age + Time.deltaTime * velocityRadiansPerSecond, Mathf.PI * 2f);
            }

            Push(camera);
            Record();
        }

        void Build()
        {
            if (built)
            {
                return;
            }

            built = true;
            var count = layers == null ? 0 : layers.Length;
            buffers = new ComputeBuffer[count];
            instances = new Material[count];
            order.Clear();

            for (var i = 0; i < count; i++)
            {
                var layer = layers[i];
                if (layer.material == null || layer.cloud == null || layer.cloud.Count == 0)
                {
                    continue;
                }

                var buffer = new ComputeBuffer(layer.cloud.Count, StarVert.StructSize);
                buffer.SetData(layer.cloud.points);
                var instance = new Material(layer.material) { enableInstancing = false };
                instance.SetBuffer(StarsId, buffer);
                buffers[i] = buffer;
                instances[i] = instance;
                order.Add(i);
            }

            order.Sort((a, b) => layers[a].drawIndex.CompareTo(layers[b].drawIndex));
        }

        void Push(Camera camera)
        {
            var scale = transform.lossyScale.x;
            LocalCamDir = transform.InverseTransformPoint(camera.transform.position).normalized;
            var ellipse = new Vector4(ellipseRadii.x, ellipseRadii.y, 0f, 0f);

            for (var i = 0; i < order.Count; i++)
            {
                var layer = layers[order[i]];
                var instance = instances[order[i]];
                instance.SetFloat(WsScaleId, layer.worldSpaceScale * scale * sizeFactor);
                instance.SetVector(ColorId, layer.tint * layer.tintMultiplier);
                instance.SetFloat(TransitionAlphaId, alpha);
                instance.SetFloat(AgeId, age);
                instance.SetVector(EllipseSizeId, layer.ellipseRadii.x > 0f ? new Vector4(layer.ellipseRadii.x, layer.ellipseRadii.y, 0f, 0f) : ellipse);
                instance.SetFloat(RadiusId, layer.cloud != null && layer.cloud.radiusMetres > 0f ? layer.cloud.radiusMetres : 1f);
                instance.SetVector(LocalCamDirId, LocalCamDir);
            }
        }

        // Re-recorded every frame because the matrix is baked into the draw and the player moves these by hand.
        void Record()
        {
            beforeOpaque.Clear();
            beforeAlpha.Clear();
            var matrix = transform.localToWorldMatrix;

            for (var i = 0; i < order.Count; i++)
            {
                var layer = layers[order[i]];
                var commands = layer.afterOpaque ? beforeAlpha : beforeOpaque;
                commands.DrawProcedural(matrix, instances[order[i]], 0, MeshTopology.Triangles, layer.cloud.Count * 6);
            }
        }

        void Attach(Camera camera)
        {
            beforeOpaque = new CommandBuffer { name = "Cosmic points (opaque)" };
            beforeAlpha = new CommandBuffer { name = "Cosmic points (alpha)" };
            camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, beforeOpaque);
            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, beforeAlpha);
            attached = camera;
        }

        void Detach()
        {
            if (attached != null)
            {
                if (beforeOpaque != null)
                {
                    attached.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, beforeOpaque);
                }

                if (beforeAlpha != null)
                {
                    attached.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, beforeAlpha);
                }
            }

            beforeOpaque?.Release();
            beforeAlpha?.Release();
            beforeOpaque = null;
            beforeAlpha = null;
            attached = null;
        }

        void Release()
        {
            Detach();
            if (buffers != null)
            {
                for (var i = 0; i < buffers.Length; i++)
                {
                    buffers[i]?.Release();
                }
            }

            if (instances != null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    if (instances[i] == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Destroy(instances[i]);
                    }
                    else
                    {
                        DestroyImmediate(instances[i]);
                    }
                }
            }

            buffers = null;
            instances = null;
            order.Clear();
            built = false;
        }

        Camera Resolve()
        {
            if (target != null)
            {
                return target;
            }

            if (cached == null)
            {
                cached = Camera.main;
            }

            return cached;
        }
    }
}
