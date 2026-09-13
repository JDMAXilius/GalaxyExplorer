using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Cosmic
{
    public class Field : MonoBehaviour
    {
        [Serializable]
        public struct Cutout
        {
            public int plate;
            public Rect uv;
            public float aspect;
            public float floor;
            public float gain;
        }

        class Stream
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv0 = new List<Vector2>();
            public readonly List<Vector4> uv1 = new List<Vector4>(), uv2 = new List<Vector4>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> triangles = new List<int>();
        }

        const int MaxSprites = 4000;
        const float CellInset = 0.06f;
        const float CellFloor = 0.16f;
        const float CellGain = 2.6f;

        static readonly int AgeId = Shader.PropertyToID("_Age");
        static readonly int TransitionAlphaId = Shader.PropertyToID("_TransitionAlpha");

        public Texture2D[] plates = Array.Empty<Texture2D>();
        public Material[] plateMaterials = Array.Empty<Material>();
        public Cutout[] cutouts = Array.Empty<Cutout>();

        public int seed = 20260912;
        public int spriteCount = 420;
        public float shellInnerMetres = 2.2f;
        public float shellOuterMetres = 3f;
        [Range(0f, 1f)] public float directionJitter = 0.32f;
        public float minSpriteMetres = 0.03f;
        public float maxSpriteMetres = 0.25f;
        [Range(1f, 5f)] public float sizeBias = 2.4f;
        [Range(0f, 1f)] public float tintSpread = 0.35f;
        public Vector2 brightnessRange = new Vector2(0.55f, 1.2f);
        public float driftDegreesPerSecond = 1f;
        [Range(0f, 0.6f)] public float driftSpread = 0.18f;
        public bool centreOnViewer = true;
        public float maxViewerOffsetMetres = 3f;
        public Camera target;

        [SerializeField] float alpha = 1f;

        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly List<Mesh> meshes = new List<Mesh>();
        MaterialPropertyBlock block;
        Transform shell;
        Camera cam;
        float age;
        bool built;

        public float Alpha { get => alpha; set => alpha = value; }
        public int SpriteCount { get; private set; }
        public float ShellRadiusMetres => Mathf.Max(shellInnerMetres, shellOuterMetres);

        public void Rebuild()
        {
            Clear();
            Build();
        }

        public void RecentreOnViewer()
        {
            Build();
            if (shell != null) shell.localPosition = ViewerOffset();
        }

        public static Cutout[] Scan(Texture2D plate, int plateIndex, int cols, int rows)
        {
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);
            var cellWidth = 1f / cols;
            var cellHeight = 1f / rows;
            var pixelsWide = plate == null ? 1 : plate.width;
            var pixelsHigh = plate == null ? 1 : plate.height;
            var result = new Cutout[cols * rows];

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < cols; x++)
                {
                    result[y * cols + x] = new Cutout
                    {
                        plate = plateIndex,
                        uv = new Rect((x + CellInset) * cellWidth, (y + CellInset) * cellHeight,
                            cellWidth * (1f - 2f * CellInset), cellHeight * (1f - 2f * CellInset)),
                        aspect = cellWidth * pixelsWide / Mathf.Max(1f, cellHeight * pixelsHigh),
                        floor = CellFloor,
                        gain = CellGain,
                    };
                }
            }

            return result;
        }

        void OnEnable() => Build();

        void OnDisable() => Clear();

        void LateUpdate()
        {
            Build();
            if (renderers.Count == 0) return;

            age = Mathf.Repeat(age + Time.deltaTime * driftDegreesPerSecond * Mathf.Deg2Rad, Mathf.PI * 2f);
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetFloat(AgeId, age);
            block.SetFloat(TransitionAlphaId, alpha);

            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
            }
        }

        void Build()
        {
            if (built) return;
            built = true;

            if (cutouts == null || cutouts.Length == 0 || plateMaterials == null || plateMaterials.Length == 0)
            {
                if (Application.isPlaying) Debug.LogError($"Field on '{name}': no cutouts or plate materials.");
                return;
            }

            var root = new GameObject("shell");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = ViewerOffset();
            shell = root.transform;

            var streams = new Stream[plateMaterials.Length];
            for (var i = 0; i < streams.Length; i++) streams[i] = new Stream();
            Scatter(streams);

            for (var plate = 0; plate < streams.Length; plate++)
            {
                if (plateMaterials[plate] == null || streams[plate].triangles.Count == 0) continue;

                var child = new GameObject("plate_" + plate);
                child.transform.SetParent(shell, false);
                var mesh = MakeMesh(streams[plate], plate);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;

                var meshRenderer = child.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = plateMaterials[plate];
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                meshRenderer.allowOcclusionWhenDynamic = false;

                meshes.Add(mesh);
                renderers.Add(meshRenderer);
            }

            if (renderers.Count > 0) return;
            Discard(root);
            shell = null;
        }

        void Clear()
        {
            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null) Discard(renderers[i].gameObject);
            }

            for (var i = 0; i < meshes.Count; i++) Discard(meshes[i]);
            if (shell != null) Discard(shell.gameObject);

            renderers.Clear();
            meshes.Clear();
            shell = null;
            SpriteCount = 0;
            built = false;
        }

        static void Discard(Object victim)
        {
            if (victim == null) return;
            if (Application.isPlaying) Object.Destroy(victim);
            else Object.DestroyImmediate(victim);
        }

        Vector3 ViewerOffset()
        {
            if (!centreOnViewer) return Vector3.zero;
            if (target == null && cam == null) cam = Camera.main;

            var eye = target != null ? target : cam;
            if (eye == null) return Vector3.zero;

            var local = transform.InverseTransformPoint(eye.transform.position);
            var limit = Mathf.Max(0f, maxViewerOffsetMetres);
            return local.magnitude > limit ? local.normalized * limit : local;
        }

        void Scatter(Stream[] streams)
        {
            var count = Mathf.Clamp(spriteCount, 0, MaxSprites);
            if (count == 0) return;

            var random = new Random(seed);
            var inner = Mathf.Max(0.01f, Mathf.Min(shellInnerMetres, shellOuterMetres));
            var outer = Mathf.Max(inner + 0.01f, shellOuterMetres);
            var smallest = Mathf.Max(0.001f, Mathf.Min(minSpriteMetres, maxSpriteMetres));
            var largest = Mathf.Max(smallest, maxSpriteMetres);
            var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var order = new int[cutouts.Length];
            for (var i = 0; i < order.Length; i++) order[i] = i;

            for (var i = 0; i < count; i++)
            {
                if (i % order.Length == 0) Shuffle(order, random);
                var cutout = cutouts[order[i % order.Length]];

                var y = 1f - (i + 0.5f) * 2f / count;
                var ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var theta = goldenAngle * i;
                var direction = new Vector3(Mathf.Cos(theta) * ring, y, Mathf.Sin(theta) * ring);
                direction = (direction + InsideUnitSphere(random) * directionJitter).normalized;
                if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;

                var radius = Mathf.Lerp(inner, outer, Next01(random));
                var width = Mathf.Lerp(smallest, largest, Mathf.Pow(Next01(random), Mathf.Max(1f, sizeBias)));
                var aspect = cutout.aspect > 0.01f ? cutout.aspect : 1f;
                var warm = (Next01(random) - 0.5f) * 2f * Mathf.Clamp01(tintSpread);
                var roll = Next01(random) * Mathf.PI * 2f;
                var rate = Mathf.Lerp(1f - driftSpread, 1f + driftSpread, Next01(random));
                var gain = cutout.gain > 0.01f ? cutout.gain : 1f;
                var brightness = Mathf.Lerp(brightnessRange.x, brightnessRange.y, Next01(random)) * gain;

                SpriteCount++;
                if (cutout.plate < 0 || cutout.plate >= streams.Length) continue;

                Quad(streams[cutout.plate], cutout, direction * radius,
                    0.5f * width * (aspect >= 1f ? 1f : aspect),
                    0.5f * width * (aspect >= 1f ? 1f / aspect : 1f),
                    roll, new Vector4(Mathf.Clamp01(cutout.floor), rate, brightness, 0f),
                    new Color(1f + 0.20f * warm, 1f + 0.02f * warm, 1f - 0.22f * warm, 1f));
            }
        }

        static void Quad(Stream stream, Cutout cutout, Vector3 centre, float halfX, float halfY, float roll,
            Vector4 extras, Color tint)
        {
            var rect = cutout.uv;
            var sin = Mathf.Sin(roll);
            var cos = Mathf.Cos(roll);
            var start = stream.vertices.Count;

            for (var corner = 0; corner < 4; corner++)
            {
                var cx = (corner & 1) == 0 ? -1f : 1f;
                var cy = (corner & 2) == 0 ? -1f : 1f;
                var ox = cx * halfX;
                var oy = cy * halfY;

                stream.vertices.Add(centre);
                stream.uv0.Add(new Vector2(rect.xMin + (cx * 0.5f + 0.5f) * rect.width,
                    rect.yMin + (cy * 0.5f + 0.5f) * rect.height));
                stream.uv1.Add(new Vector4(ox * cos - oy * sin, ox * sin + oy * cos, cx, cy));
                stream.uv2.Add(extras);
                stream.colors.Add(tint);
            }

            stream.triangles.Add(start);
            stream.triangles.Add(start + 1);
            stream.triangles.Add(start + 2);
            stream.triangles.Add(start + 2);
            stream.triangles.Add(start + 1);
            stream.triangles.Add(start + 3);
        }

        Mesh MakeMesh(Stream stream, int plate)
        {
            var mesh = new Mesh { name = "field_plate_" + plate };
            mesh.SetVertices(stream.vertices);
            mesh.SetUVs(0, stream.uv0);
            mesh.SetUVs(1, stream.uv1);
            mesh.SetUVs(2, stream.uv2);
            mesh.SetColors(stream.colors);
            mesh.SetTriangles(stream.triangles, 0, false);

            // Stated bounds: recalculated ones would miss the shader-expanded quads and the drift, and a plate would vanish at the view's edge.
            var reach = ShellRadiusMetres + Mathf.Max(maxSpriteMetres, minSpriteMetres);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (reach * 2f));
            mesh.UploadMeshData(false);
            return mesh;
        }

        static float Next01(Random random) => (float)random.NextDouble();

        static void Shuffle(int[] values, Random random)
        {
            for (var i = values.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        static Vector3 InsideUnitSphere(Random random)
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var v = new Vector3(Next01(random) * 2f - 1f, Next01(random) * 2f - 1f, Next01(random) * 2f - 1f);
                if (v.sqrMagnitude <= 1f) return v;
            }

            return Vector3.zero;
        }
    }
}
