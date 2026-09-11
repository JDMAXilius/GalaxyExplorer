using System;
using System.Collections.Generic;
using GalaxyExplorer.XR;
using UnityEngine;

/// <summary>
/// The beam drawn from a hand/controller ray to a planet while the user dwells on it to force-pull it.
/// One beam per pointer, created on demand; <see cref="Coverage"/> (0-1) shows the dwell progress.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ForceTractorBeam : MonoBehaviour
{
    private const int LinePointCount = 16;

    public event Action<ForceTractorBeam> Destroyed;

    private bool _wasActive;

    private LineRenderer _lineRenderer;
    private MaterialPropertyBlock _tractorBeamMaterialPropertyBlock;
    private GEPointer _pointer;
    private float _lineLength;

    private static readonly Dictionary<GEPointer, ForceTractorBeam> _staticPointersToTractorBeams =
        new Dictionary<GEPointer, ForceTractorBeam>();

    [Range(0,1)]
    public float Coverage;
    public float TractorBeamWidth = .02f;

    private static readonly int LineLength = Shader.PropertyToID("_LineLength");
    private static readonly int LineWidth = Shader.PropertyToID("_LineWidth");
    private static readonly int Active = Shader.PropertyToID("_Active");
    private static readonly int CoverageProperty = Shader.PropertyToID("_Coverage");

    public static ForceTractorBeam AttachToPointer(GEPointer pointer, GameObject tractorBeamPrefab)
    {
        if (!_staticPointersToTractorBeams.TryGetValue(pointer, out var tractorBeam) || tractorBeam == null)
        {
            tractorBeam = Instantiate(tractorBeamPrefab, pointer.RayOrigin).GetComponent<ForceTractorBeam>();
            tractorBeam._pointer = pointer;
            _staticPointersToTractorBeams[pointer] = tractorBeam;
        }
        return tractorBeam;
    }

    public static ForceTractorBeam GetTractorBeamFromPointer(GEPointer pointer)
    {
        return _staticPointersToTractorBeams.TryGetValue(pointer, out var tb) ? tb : null;
    }

    private void Awake()
    {
        _tractorBeamMaterialPropertyBlock = new MaterialPropertyBlock();
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = LinePointCount;
        Dissipate();
    }

    private void OnDestroy()
    {
        if (_pointer != null)
        {
            _staticPointersToTractorBeams.Remove(_pointer);
        }
        Destroyed?.Invoke(this);
    }

    private void Update()
    {
        if (_pointer == null)
        {
            return;
        }

        var target = _pointer.FocusTarget as ForceSolver;
        if (!_wasActive && !_pointer.IsActive)
        {
            return;
        }
        if (!_pointer.IsActive || target == null || Math.Abs(Coverage) < float.Epsilon)
        {
            Dissipate();
        }
        else
        {
            _lineRenderer.enabled = true;
            UpdateLine(target.transform.position);
            _wasActive = true;
        }
        _tractorBeamMaterialPropertyBlock.SetFloat(CoverageProperty, Coverage);
        _lineRenderer.SetPropertyBlock(_tractorBeamMaterialPropertyBlock);
    }

    private void UpdateLine(Vector3 targetPosition)
    {
        var origin = _pointer.RayOrigin.position;
        for (var i = 0; i < LinePointCount; i++)
        {
            _lineRenderer.SetPosition(i, Vector3.Lerp(origin, targetPosition, i / (LinePointCount - 1f)));
        }

        _lineLength = Vector3.Distance(origin, targetPosition);
        _lineRenderer.widthMultiplier = TractorBeamWidth;
        _tractorBeamMaterialPropertyBlock.SetFloat(Active, 1f);
        _tractorBeamMaterialPropertyBlock.SetFloat(LineLength, _lineLength);
        _tractorBeamMaterialPropertyBlock.SetFloat(LineWidth, TractorBeamWidth);
    }

    public void Dissipate()
    {
        Coverage = 0f;
        _tractorBeamMaterialPropertyBlock.SetFloat(Active, 0f);

        // the beam may already be destroyed with its pointer
        if (this != null && _lineRenderer != null)
        {
            _lineRenderer.enabled = false;
        }

        _wasActive = false;
    }
}
