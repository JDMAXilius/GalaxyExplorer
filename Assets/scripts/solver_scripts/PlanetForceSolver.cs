using System;
using System.Collections.Generic;
using System.Linq;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEngine;

public class PlanetForceSolver : ForceSolver
{
    private PlanetOffsetScaleController _scaleController;
    protected Vector3 _editScaleTarget = Vector3.one;
    private float _oldBlend;
    private PlanetHighlighter _planetHighlighter;
    private IAudioService _audioService;
    private AudioSource _voAudioSource, _ambientAudioSource;
    private List<Moon> _moons = new List<Moon>();

    [SerializeField]
    private AudioClip planetAudioClip;
    [SerializeField]
    private AudioClip planetAmbiantClip;

    protected override void Awake()
    {
        base.Awake();
        
        _scaleController = GetComponentInChildren<PlanetOffsetScaleController>();
        if (_scaleController != null)
        {
            _editScaleTarget = Vector3.one *
                               (PlanetOffsetScaleController.TargetEditScaleCm /
                                _scaleController.transform.localScale.x);

        }

        _planetHighlighter = GetComponentInChildren<PlanetHighlighter>();
        _audioService = AudioService.Instance;

        _moons = GetComponentsInChildren<Moon>().ToList();
    }

    private void StopAudio()
    {
        if (_voAudioSource != null)
        {
            _voAudioSource.Stop();
        }

        if (_ambientAudioSource != null)
        {
            _ambientAudioSource.Stop();
        }
    }

    private void StartAudio()
    {
        if (planetAudioClip != null)
        {
            // Narrate every time the body is pulled, not only the first time in a session.
            GalaxyExplorerManager.Instance.VoManager.Stop(true);
            GalaxyExplorerManager.Instance.VoManager.PlayClip(planetAudioClip, allowReplay: true, replaceQueue: true);
        }

        if (planetAmbiantClip != null)
        {
            _audioService.PlayClip(planetAmbiantClip, out _ambientAudioSource, transform, playOptions:PlayOptions.Loop);
        }
    }

    private void HideMoons()
    {
        foreach (var moon in _moons)
        {
            moon.Hide();
        }
    }

    private void ShowMoons()
    {
        foreach (var moon in _moons)
        {
            moon.Show();
        }
    }

    protected override void OnStartRoot()
    {
        base.OnStartRoot();
        if (_planetHighlighter != null) _planetHighlighter.gameObject.SetActive(true);
        StopAudio();
        HideMoons();
    }

    protected override void OnStartAttraction()
    {
        base.OnStartAttraction();
        if (_planetHighlighter != null) _planetHighlighter.gameObject.SetActive(false);
        StartAudio();
        HideMoons();
    }


    protected override void OnStartManipulation()
    {
        base.OnStartManipulation();
        ShowMoons();
    }

    protected override void OnStartFree()
    {
        base.OnStartFree();
        ShowMoons();
    }

    // The pull ends once the body has arrived and also finished growing, so it always ends at its pulled size.
    protected override bool IsAttractionComplete()
    {
        return base.IsAttractionComplete() &&
               (_scaleController == null ||
                Mathf.Abs(transform.localScale.x - _editScaleTarget.x) <= _editScaleTarget.x * 0.02f);
    }

    /// <summary>Local scale this body grows to when pulled out of its orbit.</summary>
    public float EditScale => _editScaleTarget.x;

    // Two hands may take a held body from a tenth of its pulled size to three times it: the same multiples the
    // desktop wheel uses (DesktopMouseInput minPlanetScale / maxPlanetScale), so both inputs stop in one place.
    private const float MinHeldScale = 0.1f, MaxHeldScale = 3f;

    protected override Vector3 ClampHeldScale(Vector3 desiredLocalScale)
    {
        var wanted = desiredLocalScale.x;
        if (wanted <= 0f || EditScale <= 0f)
        {
            return desiredLocalScale;
        }

        var clamped = Mathf.Clamp(wanted, MinHeldScale * EditScale, MaxHeldScale * EditScale);
        return desiredLocalScale * (clamped / wanted);
    }

    public override void SolverUpdate()
    {
        // Use the state from before the base update: a long frame can finish the attraction (and leave the state)
        // in one step, which would otherwise skip the scale-up and leave the body at its orbit size.
        var state = ForceState;
        base.SolverUpdate();
        switch (state)
        {
            case State.Root:
                GoalScale = Vector3.one;
                UpdateWorkingScaleToGoal();
                break;
                
            case State.Attraction:
                if (_scaleController == null) break;
                GoalScale = _editScaleTarget;
                UpdateWorkingScaleToGoal();
                break;
            
            case State.Dwell:
            case State.Free:
            case State.Manipulation:
            case State.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void OnFocusExit(GEFocusEventData eventData)
    {
        base.OnFocusExit(eventData);
        if (_planetHighlighter != null) _planetHighlighter.SetFocused(false);
    }

    public override void OnFocusEnter(GEFocusEventData eventData)
    {
        base.OnFocusEnter(eventData);
        if (_planetHighlighter != null) _planetHighlighter.SetFocused(true);
    }
}
