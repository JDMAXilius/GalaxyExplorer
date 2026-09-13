using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cosmic
{
    // Order and packing are the shader's, minus the trailing unread `random` float the old baked assets carry.
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct StarVert
    {
        public const int StructSize = sizeof(float) * 10;

        public float yOffset;
        public float curveOffset;
        public float ellipseDistance;
        public float ellipseOffset;
        public Vector3 color;
        public Vector2 uv;
        public float size;
    }

    [CreateAssetMenu(menuName = "Cosmic/Point Cloud", fileName = "point_cloud")]
    public class PointCloud : ScriptableObject
    {
        public StarVert[] points = Array.Empty<StarVert>();
        public float radiusMetres;
        public string sourceAssetPath;

        [TextArea(2, 6)]
        public string provenance;

        public int Count => points == null ? 0 : points.Length;
        public int Stride => StarVert.StructSize;
    }
}
