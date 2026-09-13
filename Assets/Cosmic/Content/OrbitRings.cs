using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cosmic
{
    public class OrbitRings
    {
        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct OrbitPoint
        {
            public Vector3 schematic;
            public Vector3 real;
        }

        // The planet position rides in the per-orbit record so one small buffer carries both, replacing nine uniforms.
        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Span
        {
            public int start;
            public int count;
            public int index;
            public int padding;
            public Vector4 planetPositionAndRadius;
        }

        const CameraEvent DrawEvent = CameraEvent.AfterForwardAlpha;

        static readonly int OrbitsDataId = Shader.PropertyToID("_OrbitsData");
        static readonly int OrbitSpansId = Shader.PropertyToID("_OrbitSpans");
        static readonly int OrbitCountId = Shader.PropertyToID("_OrbitCount");
        static readonly int OrbitsToWorldId = Shader.PropertyToID("_Orbits2World");
        static readonly int TruthfulnessId = Shader.PropertyToID("_Truthfulness");
        static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");
        static readonly int GlobalScaleId = Shader.PropertyToID("_GlobalScale");

        readonly Material material;
        MaterialPropertyBlock properties;
        ComputeBuffer pointBuffer;
        ComputeBuffer spanBuffer;
        Span[] spans;
        int vertexCount;
        CommandBuffer commands;
        Camera commandCamera;

        public OrbitRings(Material material) => this.material = material;

        public int Count => spans == null ? 0 : spans.Length;

        public void Build(IList<OrbitPoint> points, Span[] orbits)
        {
            ReleaseBuffers();
            spans = orbits;
            vertexCount = 0;
            if (points == null || points.Count == 0 || orbits == null || orbits.Length == 0)
                return;
            var data = new OrbitPoint[points.Count];
            points.CopyTo(data, 0);
            pointBuffer = new ComputeBuffer(data.Length, Marshal.SizeOf<OrbitPoint>());
            pointBuffer.SetData(data);
            spanBuffer = new ComputeBuffer(orbits.Length, Marshal.SizeOf<Span>());
            vertexCount = data.Length * 6;
        }

        public void SetPlanet(int index, Vector4 positionAndRadius)
        {
            if (spans == null || index < 0 || index >= spans.Length)
                return;
            spans[index].planetPositionAndRadius = positionAndRadius;
        }

        public void Draw(Camera camera, Matrix4x4 orbitsToWorld, float truthfulness, float alpha, float globalScale)
        {
            if (material == null || pointBuffer == null || spanBuffer == null || vertexCount == 0)
                return;
            if (camera == null)
            {
                Detach();
                return;
            }
            if (commands == null || camera != commandCamera)
            {
                Detach();
                commands = new CommandBuffer { name = "Cosmic orbits" };
                camera.AddCommandBuffer(DrawEvent, commands);
                commandCamera = camera;
            }

            spanBuffer.SetData(spans);
            if (properties == null)
                properties = new MaterialPropertyBlock();
            properties.SetBuffer(OrbitsDataId, pointBuffer);
            properties.SetBuffer(OrbitSpansId, spanBuffer);
            properties.SetFloat(OrbitCountId, spans.Length);
            properties.SetMatrix(OrbitsToWorldId, orbitsToWorld);
            properties.SetFloat(TruthfulnessId, truthfulness);
            properties.SetFloat(TransitionAlphaId, alpha);
            properties.SetFloat(GlobalScaleId, globalScale);

            commands.Clear();
            commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, vertexCount, 1, properties);
        }

        public void Detach()
        {
            if (commands != null && commandCamera != null)
                commandCamera.RemoveCommandBuffer(DrawEvent, commands);
            commands?.Release();
            commands = null;
            commandCamera = null;
        }

        public void Release()
        {
            Detach();
            ReleaseBuffers();
            spans = null;
            vertexCount = 0;
        }

        void ReleaseBuffers()
        {
            pointBuffer?.Release();
            pointBuffer = null;
            spanBuffer?.Release();
            spanBuffer = null;
        }
    }
}
