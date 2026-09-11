using System.Collections.Generic;
using UnityEngine;

public class Moon : MonoBehaviour
{
    private const float KeepVisibleFadeSeconds = 0.3f;

    private Animator _animator;
    private HashSet<MeshRenderer> _renderers = new HashSet<MeshRenderer>();
    private Collider[] _colliders = new Collider[0];
    private float _shownBlend = -1f;
    private float _oldBlend = -1f;
    private bool _isOpaque = true;

    [Tooltip("Holds the moon's renderers and colliders; defaults to this object. The Earth's moon keeps them on its own force-grab root so it can be pulled out of its orbit.")]
    [SerializeField]
    private Transform visualRoot = null;

    [Range(0,1)]
    public float Blend;

    /// <summary>
    /// Keeps the moon fully visible whatever its orbit animation says; set while the moon is pulled out of its orbit.
    /// </summary>
    public bool KeepVisible { get; set; }

    private static readonly int Srcblend = Shader.PropertyToID("_SRCBLEND");
    private static readonly int Dstblend = Shader.PropertyToID("_DSTBLEND");
    private static readonly int TransitionAlpha = Shader.PropertyToID("_TransitionAlpha");

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        var root = visualRoot != null ? visualRoot : transform;
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in renderers)
        {
            // The hover ring animates itself.
            if (renderer.GetComponentInParent<PlanetHighlighter>(true) != null)
            {
                continue;
            }

            var material = renderer.sharedMaterial;
            if (material.HasProperty("_SRCBLEND") &&
                material.HasProperty("_DSTBLEND") &&
                material.HasProperty("_TransitionAlpha"))
            {
                _renderers.Add(renderer);
            }
        }

        // A hidden moon must not catch pointers (including its hover ring's).
        _colliders = root.GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        var target = KeepVisible ? 1f : Blend;
        _shownBlend = _shownBlend < 0f ? target : Mathf.MoveTowards(_shownBlend, target, Time.deltaTime / KeepVisibleFadeSeconds);
        var blend = _shownBlend;

        if (Mathf.Abs(_oldBlend - blend) >= float.Epsilon)
        {
            var canBeOpaque = Mathf.Abs(Mathf.Abs(blend-.5f) -.5f) < float.Epsilon;
            foreach (var renderer in _renderers)
            {
                var material = renderer.material; //create material instance
                if (_isOpaque && !canBeOpaque)
                {
                    material.SetInt(Srcblend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt(Dstblend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else if (!_isOpaque && canBeOpaque)
                {
                    material.SetInt(Srcblend, (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt(Dstblend, (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.renderQueue = (int) UnityEngine.Rendering.RenderQueue.Geometry;
                }
                material.SetFloat(TransitionAlpha, blend);
                renderer.enabled = blend > float.Epsilon;
            }

            foreach (var collider in _colliders)
            {
                collider.enabled = blend > .5f;
            }
            _oldBlend = blend;
            _isOpaque = canBeOpaque;
        }
    }

    public virtual void Show()
    {
        _animator.SetBool("Visible", true);
    }

    public virtual void Hide()
    {
        _animator.SetBool("Visible", false);
    }
}
