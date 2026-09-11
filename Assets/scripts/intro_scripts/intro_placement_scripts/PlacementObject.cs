using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlacementObject : MonoBehaviour
{
    private readonly List<Vector4> _randoms = new List<Vector4>();
    [SerializeField, HideInInspector]
    private Mesh _targetMesh;
    private float _time;
    private Material _material;

    private const float s_divider = (float) 1.0d / int.MaxValue;

    public Mesh SourceMesh;
    public int Seed;
    public float Speed = 5f;
    
    
    private static readonly int SelfTime = Shader.PropertyToID("_SelfTime");
    private static readonly int Active = Shader.PropertyToID("_Active");

    private void Awake()
    {
        _material = GetComponent<MeshRenderer>().material; //instanced!! (not saved, not shared)
    }

    private void Start()
    {
        GenerateData();
    }

    private void Update()
    {
        _time += Time.deltaTime * Speed;
        _material.SetFloat(SelfTime, _time);
    }

    private static float GetRandFloat01(Random random)
    {
        return random.Next() * s_divider;
    }

    public void SetActive(bool active)
    {
        _material.SetFloat(Active, active?1f:0f);
    }

    public void GenerateData()
    {
        if(SourceMesh == null) return;

        var random = new Random(Seed);

        var vertices = SourceMesh.vertices;

        // Every source vertex becomes a camera-facing sparkle: a quad of four vertices sharing its position and
        // random values, with the corner index (0-3) in UV1. The shader expands the corners in its vertex stage.
        var quadVertices = new Vector3[vertices.Length * 4];
        var corners = new List<Vector2>(vertices.Length * 4);
        var indices = new int[vertices.Length * 6];
        _randoms.Clear();

        for (var i = 0; i < vertices.Length; i++)
        {
            var randoms = new Vector4(
                GetRandFloat01(random),
                GetRandFloat01(random),
                GetRandFloat01(random),
                GetRandFloat01(random)
            );

            for (var corner = 0; corner < 4; corner++)
            {
                quadVertices[i * 4 + corner] = vertices[i];
                _randoms.Add(randoms);
                corners.Add(new Vector2(corner, 0));
            }

            // corners: 0 bottom, 1 right, 2 left, 3 top
            indices[i * 6 + 0] = i * 4 + 0;
            indices[i * 6 + 1] = i * 4 + 1;
            indices[i * 6 + 2] = i * 4 + 2;
            indices[i * 6 + 3] = i * 4 + 2;
            indices[i * 6 + 4] = i * 4 + 1;
            indices[i * 6 + 5] = i * 4 + 3;
        }

        _targetMesh = new Mesh
        {
            indexFormat = quadVertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            vertices = quadVertices
        };
        _targetMesh.SetUVs(0, _randoms);
        _targetMesh.SetUVs(1, corners);
        _targetMesh.SetTriangles(indices, 0);
        _targetMesh.RecalculateBounds();
        _targetMesh.UploadMeshData(false);
        GetComponent<MeshFilter>().mesh = _targetMesh;
    }
}
