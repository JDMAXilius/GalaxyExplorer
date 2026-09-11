using System;
using System.Collections.Generic;
using System.Linq;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEngine;

public class PlanetForceSolver : ForceSolver
{
    private PlanetOffsetScaleController _scaleController;
    private Vector3 _editScaleTarget = Vector3.one;
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
            GalaxyExplorerManager.Instance.VoManager.Stop(true);
            GalaxyExplorerManager.Instance.VoManager.PlayClip(planetAudioClip);
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
        _planetHighlighter.gameObject.SetActive(true);
        StopAudio();
        HideMoons();
    }

    protected override void OnStartAttraction()
    {
        base.OnStartAttraction();
        _planetHighlighter.gameObject.SetActive(false);
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

    /// <summary>Local scale this body grows to when pulled out of its orbit.</summary>
    public float EditScale => _editScaleTarget.x;

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
        _planetHighlighter.SetFocused(false);
    }

    public override void OnFocusEnter(GEFocusEventData eventData)
    {
        base.OnFocusEnter(eventData);
        _planetHighlighter.SetFocused(true);
    }
}
