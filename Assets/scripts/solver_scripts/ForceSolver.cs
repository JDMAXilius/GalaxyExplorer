using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicSimulation;
using GalaxyExplorer;
using GalaxyExplorer.XR;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

[Serializable]
public class UnityForceSolverEvent : UnityEvent<ForceSolver>
{
}

/// <summary>
/// Force-pull: a far ray dwelling on an object attracts it to the hand, a select attracts it at once, and a desktop
/// click pulls it in front of the camera. Root, Dwell, Attraction, Free, Manipulation.
/// </summary>
public class ForceSolver : Solver, IGEFocusChangedHandler, IGEFocusHandler, IGEPointerHandler
{
    public enum State
    {
        None = 0,
        Root,
        Free,
        Dwell,
        Attraction,
        Manipulation,
    }

    private static int _dwellCounter;
    private static AudioSource _tractionBeamAudioSource;

    private ManipulationHandler _manipulationHandler;
    private Collider _attractionCollider;
    private Quaternion _rotationOffset, _previousRotation;
    private float _baseScale;
    private IAudioService _audioService;
    private AudioSource _activeAudioSource;
    private bool _forcePullToFrontOfCamera;
    private bool ForcePullToHandController => !_forcePullToFrontOfCamera;
    private Camera _mainCamera;
    private Coroutine _attractionDwellRoutine;
    private float _dwellTimer, _dwellForgivenessTimer;
    private readonly HashSet<GEPointer> _focusers = new HashSet<GEPointer>();
    private readonly HashSet<ForceTractorBeam> _activeTractorBeams = new HashSet<ForceTractorBeam>();
    private PlanetPreviewController planetController;
    private UiPreviewTarget previewTarget;

    // Overwritten in Awake from GalaxyExplorerManager.ForcePullToCamFixedDistance; set it there.
    private float _offsetOnPullToCamera = 1f;

    public State ForceState { get; private set; }
    public State PreviousForceState { get; private set; }

    /// <summary>Raised on every state change of any body, after the state is set.</summary>
    public static event System.Action<ForceSolver> StateChanged;

    private void SetState(State next)
    {
        PreviousForceState = ForceState;
        ForceState = next;
        StateChanged?.Invoke(this);
    }
    
    public bool EnableForce = true;
    [Range(0,10)]
    public float AttractionDwellDuration = 1f;

    public bool AvergeSides = true;
    public GameObject TractionBeamPrefab;
    public float AttractionDwellForgiveness = .5f;
    public Transform RootTransform;
    public ControllerTransformTracker ControllerTracker;
    public bool OffsetToObjectBoundsFromController = true;
    public ManipulationHandler ManipulationHandler;
    public Collider AttractionCollider;
    public float CurrentRelativeDwell => _dwellTimer;

    public UnityForceSolverEvent SetToRoot, SetToDwell, DwellCanceled, SetToAttract, SetToManipulate, SetToFree;

    [Header("Tap to return (CS-174)")]
    [Tooltip("A press and release on a body that is out of its arrangement, shorter than this and without " +
             "moving it, sends it home: a pulled planet back to the row, a pulled moon back into orbit round " +
             "its planet wherever the planet is. A longer or moving press is a grab, as before.")]
    public float TapSeconds = 0.4f;

    [Tooltip("How far a body may drift during the press and still count as tapped rather than dragged, in metres.")]
    public float TapMoveMetres = 0.03f;

    [Tooltip("Let go of a body with one hand moving faster than this, in metres per second, and it returns to " +
             "its orbit or layout instead of staying where it was released (CS-290). Zero turns it off. The " +
             "mouse never flings.")]
    public float FlingSpeed = 1.5f;

    [Tooltip("Width kept free beside a pulled body for its info card, in metres at text scale 1.")]
    public float CardRoomMetres = 0.2f;

    // Where a newer pull to the camera asked this body to step aside; null when it stays put.
    private Vector3? _aside;

    private bool _tapArmed;
    private GEPointer _tapPointer;
    private float _tapStarted;
    private Vector3 _tapStartPosition;

    protected override void Awake()
    {
        base.Awake();
        _mainCamera = Camera.main;

        _offsetOnPullToCamera = GalaxyExplorerManager.ForcePullToCamFixedDistance;

        _manipulationHandler = ManipulationHandler ? ManipulationHandler : GetComponentInChildren<ManipulationHandler>();
        Debug.Assert(_manipulationHandler != null, "Force Solver failed to find a manipulation handler");
        _attractionCollider = AttractionCollider ? AttractionCollider : GetComponentInChildren<Collider>();
        Debug.Assert(_attractionCollider != null, "Force Solver failed to find a attraction collider");
        Registry<ForceSolver>.Add(this);

        _manipulationHandler.OnManipulationEnded.AddListener(OnManipulationEnd);

        // Only when the handler scales this very transform: the limit below is in this transform's scale units.
        if (_manipulationHandler.HostTransform == transform)
        {
            _manipulationHandler.FallbackScaleClamp = ClampHeldScale;
        }

        if (ControllerTracker == null)
        {
            Debug.LogWarning(gameObject.name + " ForceSolver no controller tracker transform supplied will instantiate locally");
            var controllerTrackerObject = new GameObject(gameObject.name + " ForceSolver autogenerated controller tracker");
            ControllerTracker = controllerTrackerObject.AddComponent<ControllerTransformTracker>();
        }

        ControllerTracker.AllTrackingLost += OnControllersLost;

        // Added here, not per prefab, so every body and moon answers a touch; one already on a prefab keeps its tuning.
        if (GetComponent<CosmicSimulation.TouchNudge>() == null)
        {
            gameObject.AddComponent<CosmicSimulation.TouchNudge>();
        }
    }

    private void Start()
    {
        _audioService = AudioService.Instance;
        StartRoot();
    }

    private void OnDestroy()
    {
        Registry<ForceSolver>.Remove(this);
        ControllerTracker.AllTrackingLost -= OnControllersLost;
    }

    private void UpdateGoalsAttraction()
    {
        if (ForcePullToHandController && _focusers.Count == 0)
        {
            StartFree();
            return;
        }
        
        if (ForcePullToHandController)
        {
            GoalRotation = SolverHandler.TransformTarget.rotation * _rotationOffset;
            UpdateWorkingRotationToGoal();
            GoalScale = ControllerTracker.ResolvedTransform.localScale;
            GoalPosition = AverageFocuserPosition();
        }
        else
        {
            var mainCameraTransform = _mainCamera.transform;
            GoalPosition = mainCameraTransform.position + mainCameraTransform.forward * _offsetOnPullToCamera;
        }
        
        if (ForcePullToHandController && OffsetToObjectBoundsFromController)
        {
            GoalPosition += GetOffsetPositionFromController();
        }
        UpdateWorkingPositionToGoal();

        if (IsAttractionComplete())
        {
            StartFree();
        }
    }

    private void UpdateTractionBeams()
    {
        switch (ForceState)
        {
            case State.Dwell:
                foreach (var tractorBeam in _activeTractorBeams)
                {
                    tractorBeam.Coverage = _dwellTimer / AttractionDwellDuration;
                }
                break;
            
            case State.Attraction:
                foreach (var tractorBeam in _activeTractorBeams)
                {
                    tractorBeam.Coverage = 1f;
                }
                break;
            
            case State.None:
            case State.Root:
            case State.Free:
            case State.Manipulation:
                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SetActivePointersFocusLocked(bool locked)
    {
        foreach (var pointer in _focusers)
        {
            pointer.IsFocusLocked = locked;
        }
    }

    protected virtual bool IsAttractionComplete()
    {
        if (ForcePullToHandController)
        {
            return Vector3.Distance(GoalPosition, WorkingPosition) <= .05f;
        }
        else if (_forcePullToFrontOfCamera)
        {
            return Vector3.Distance(WorkingPosition, GoalPosition) <= 0.1f;
        }

        return false;
    }

    // Loops rather than LINQ: these run every frame of a pull, and Select/Average allocate.
    private Vector3 AverageFocuserPosition()
    {
        var sum = Vector3.zero;
        foreach (var p in _focusers)
        {
            sum += p.Handedness == Handedness.Left ? ControllerTracker.LeftSidePosition : ControllerTracker.RightSidePosition;
        }

        return _focusers.Count > 0 ? sum / _focusers.Count : sum;
    }

    private Vector3 AverageFocuserForward()
    {
        var sum = Vector3.zero;
        foreach (var p in _focusers)
        {
            sum += (p.Handedness == Handedness.Left ? ControllerTracker.LeftSideRotation : ControllerTracker.RightSideRotation) * Vector3.forward;
        }

        return _focusers.Count > 0 ? sum / _focusers.Count : sum;
    }

    protected virtual Vector3 GetOffsetPositionFromController()
    {
        var controllerFwd = AverageFocuserForward().normalized;
        
        var position = transform.position;
        var ray = new Ray(position - controllerFwd * 100, controllerFwd);
        var hit = _attractionCollider.Raycast(ray, out var hitInfo, 150);
        Debug.Assert(hit);
        return position - hitInfo.point;
    }

    private void StartRoot()
    {
        if (ForceState == State.Root)
        {
            return;
        }

        _aside = null;
        SetState(State.Root);
        _manipulationHandler.enabled = false;
        SolverHandler.TransformTarget = RootTransform;
        OnStartRoot();
        SetToRoot?.Invoke(this);
    }

    protected virtual void OnStartRoot()
    {
    }

    private void StartDwell()
    {
        if (ForceState == State.Dwell)
        {
            return;
        }

        SetState(State.Dwell);
        _manipulationHandler.enabled = false;
        SolverHandler.TransformTarget = ControllerTracker.ResolvedTransform;
        Debug.Assert(_attractionDwellRoutine == null);
        _attractionDwellRoutine = StartCoroutine(DwellCoroutine());
        OnStartDwell();
        SetToDwell?.Invoke(this);
    }

    protected virtual void OnStartDwell()
    {
    }

    public bool ForceSetDwellTimer(float time)
    {
        if (ForceState != State.Dwell)
        {
            return false;
        }
        _dwellTimer = time;
        return true;
    }

    private IEnumerator DwellCoroutine()
    {
        ++_dwellCounter;
        if (_dwellCounter == 1)
        {
            _audioService.PlayClip(AudioId.ForceDwell, out _tractionBeamAudioSource);
        }
        _dwellTimer = _activeTractorBeams.Count > 0 ? _activeTractorBeams.Max(tb => tb.Coverage) : 0f;
        _dwellForgivenessTimer = 0f;
        while (ForceState == State.Dwell &&
               _dwellTimer < AttractionDwellDuration &&
               EnableForce &&
               _dwellForgivenessTimer < AttractionDwellForgiveness)
        {
            _dwellTimer += Time.deltaTime;
            if (_focusers.Count == 0)
            {
                _dwellForgivenessTimer += Time.deltaTime;
            }
            else
            {
                _dwellForgivenessTimer = 0f;
            }
            yield return null;
        }

        --_dwellCounter;
        Debug.Assert(_dwellCounter >= 0);
        if (ForceState == State.Dwell &&
            _focusers.Count > 0 &&
            EnableForce &&
            _dwellForgivenessTimer < AttractionDwellForgiveness)
        {
            StartAttraction();
        }
        else
        {
            OnDwellCanceled();
        }
    }

    protected virtual void OnDwellCanceled()
    {
        if (_dwellCounter == 0)
        {
            _tractionBeamAudioSource.Stop();
            ReleaseAllTractorBeams();
        }
        _attractionDwellRoutine = null;
        DwellCanceled?.Invoke(this);
        if (ForceState == State.Dwell)
        {
            switch (PreviousForceState)
            {
                
                case State.Root:
                    StartRoot();
                    break;
                
                case State.Free:
                    StartFree();
                    break;
                
                case State.None:
                case State.Manipulation:
                case State.Attraction:
                case State.Dwell:
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void StartAttraction(bool forcePullToFrontOfCamera = false)
    {
        if (ForceState == State.Dwell)
        {
            _attractionDwellRoutine = null;
            SetActivePointersFocusLocked(true);
        }
        if (ForceState == State.Attraction)
        {
            return;
        }

        SetState(State.Attraction);
        if (forcePullToFrontOfCamera)
        {
            var camera = _mainCamera.transform;
            MakeRoom(camera.position + camera.forward * _offsetOnPullToCamera, camera.right, camera.forward, this);
            _forcePullToFrontOfCamera = true;
            SolverHandler.TransformTarget = _mainCamera.transform;
        }
        else
        {
            _forcePullToFrontOfCamera = false;
            SolverHandler.TransformTarget = ControllerTracker.transform;
            SolverHandler.AdditionalOffset = Vector3.zero;
            var worldToPalmRotation = Quaternion.Inverse(SolverHandler.TransformTarget.rotation);
            _rotationOffset = worldToPalmRotation * transform.rotation;
        }
        _audioService.PlayClip(AudioId.ForcePull, out _activeAudioSource);
        OnStartAttraction();
        SetToAttract?.Invoke(this);
    }

    protected virtual void OnStartAttraction()
    {
    }

    /// <summary>
    /// Pulls to the camera all land on one spot, so a free body there steps a body-and-card left and back, recursively,
    /// and they line up instead of stacking. Held bodies and ones the player carried elsewhere stay put.
    /// </summary>
    private static void MakeRoom(Vector3 spot, Vector3 right, Vector3 forward, ForceSolver arriving)
    {
        foreach (var other in Registry<ForceSolver>.Active)
        {
            if (other == arriving || other == null || other.ForceState != State.Free)
            {
                continue;
            }

            var at = other._aside ?? other.WorkingPosition;
            var room = other.Radius() * 2f + other.CardRoomMetres * CosmicSimulation.InfoPanel.TextScale;
            if (Vector3.Distance(at, spot) >= room)
            {
                continue;
            }

            // Back as well as across, so a narrow desktop view still holds the body that stepped aside.
            var aside = spot - right * room + forward * room * 0.5f;
            MakeRoom(aside, right, forward, other);
            other._aside = aside;
        }
    }

    private float Radius() => _attractionCollider != null ? _attractionCollider.bounds.extents.x : 0.1f;

    private void StartManipulation()
    {
        if (ForceState == State.Manipulation)
        {
            return;
        }

        ReleaseAllTractorBeams();
        _aside = null;
        SetState(State.Manipulation);
        SolverHandler.TransformTarget = ControllerTracker.transform;
        _manipulationHandler.enabled = true;
        _audioService.PlayClip(AudioId.ManipulationStart, out _activeAudioSource);
        OnStartManipulation();
        SetToManipulate?.Invoke(this);
    }

    protected virtual void OnStartManipulation()
    {
    }

    private void StartFree()
    {
        SetState(State.Free);
        SolverHandler.TransformTarget = ControllerTracker.transform;
        _manipulationHandler.enabled = false;
        if (_activeAudioSource != null)
        {
            _activeAudioSource.Stop();
        }
        _audioService.PlayClip(AudioId.ManipulationEnd, out _activeAudioSource, transform);
        SetActivePointersFocusLocked(false);
        OnStartFree();
        SetToFree?.Invoke(this);
    }

    protected virtual void OnStartFree()
    {
    }

    private void OnManipulationEnd(ManipulationEventData _)
    {
        StartFree();

        // Thrown rather than put down: the body goes home. Not the intro's orb, which has no arrangement to go back to.
        if (FlingSpeed > 0f && !(this is PlacementForceSolver) && _manipulationHandler.ReleaseSpeed >= FlingSpeed)
        {
            ReturnHome();
        }
    }

    private void OnControllersLost()
    {
        _focusers.Clear();
        switch (ForceState)
        {
            case State.Attraction:
                if (ForcePullToHandController)
                {
                    StartFree();
                }
                break;
            
            case State.Manipulation:
                StartFree();
                break;
            
            case State.None:
            case State.Root:
            case State.Free:
            case State.Dwell:
                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // Force-pull is driven by far rays from a hand or controller; pokes and gaze don't count.
    private static bool IsForcePointer(GEPointer pointer)
    {
        return pointer != null && pointer.CanCastFar && pointer.Handedness != Handedness.None;
    }

    private ForceTractorBeam AttachTractorBeamToPointer(GEPointer pointer)
    {
        return ForceTractorBeam.AttachToPointer(pointer, TractionBeamPrefab);
    }

    private void ReleaseAllTractorBeams()
    {
        foreach (var tractorBeam in _activeTractorBeams)
        {
            tractorBeam.Dissipate();
        }
        _activeTractorBeams.Clear();
    }

    public override void SolverUpdate()
    {
        switch (ForceState)
        {
            case State.Root:
                SnapTo(RootTransform.position, RootTransform.rotation);
                break;
            
            case State.Dwell:
                if (PreviousForceState == State.Root)
                {
                    SnapTo(RootTransform.position, RootTransform.rotation);
                }
                UpdateTractionBeams();
                break;

            case State.Free:
                // Stays where it was left, unless a newer pull asked it to step aside.
                if (_aside.HasValue)
                {
                    GoalPosition = _aside.Value;
                    UpdateWorkingPositionToGoal();
                    if (Vector3.Distance(WorkingPosition, _aside.Value) <= 0.01f)
                    {
                        _aside = null;
                    }
                }
                break;

            case State.Attraction:
                UpdateGoalsAttraction();
                UpdateTractionBeams();
                break;

            case State.Manipulation:
            case State.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void OnBeforeFocusChange(GEFocusEventData eventData)
    {
        var pointer = eventData.Pointer;
        if (!EnableForce || !IsForcePointer(pointer))
        {
            return;
        }

        // A hand/controller that is already touching the object grabs it instead of force-pulling it.
        if (eventData.NewFocusedObject != null && eventData.NewFocusedObject.transform.IsChildOf(transform) && !eventData.IsNear)
        {
            if (!_focusers.Add(pointer))
            {
                return;
            }
            pointer.FocusTarget = this;

            var tractorBeam = AttachTractorBeamToPointer(pointer);

            switch (ForceState)
            {
                case State.Root:
                case State.Free:
                    _activeTractorBeams.Add(tractorBeam);
                    StartDwell();
                    break;

                case State.Dwell:
                    _activeTractorBeams.Add(tractorBeam);
                    break;

                case State.Manipulation:
                case State.Attraction:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else if (eventData.NewFocusedObject == null || !eventData.NewFocusedObject.transform.IsChildOf(transform))
        {
            _focusers.Remove(pointer);
            if (ReferenceEquals(pointer.FocusTarget, this))
            {
                pointer.FocusTarget = null;
            }
            var tractorBeam = ForceTractorBeam.GetTractorBeamFromPointer(pointer);
            if (tractorBeam != null)
            {
                _activeTractorBeams.Remove(tractorBeam);
            }
        }
    }

    public void OnFocusChanged(GEFocusEventData eventData)
    {
    }

    public virtual void OnFocusEnter(GEFocusEventData eventData)
    {
    }

    public virtual void OnFocusExit(GEFocusEventData eventData)
    {
    }

    public void OnPointerUp(GEPointerEventData eventData)
    {
        switch (ForceState)
        {
            // The handler is not a pointer handler, so its pointer up has to come through here.
            case State.Manipulation:
                _manipulationHandler.OnPointerUp(eventData);
                break;
            
            case State.None:
            case State.Root:
            case State.Free:
            case State.Dwell:
            case State.Attraction:
                break;
                
            default:
                throw new ArgumentOutOfRangeException();
        }

        // A quick, still tap on a body already out sends it home. Checked after the release above, so the body is
        // Free again and walks home from where it stands.
        if (_tapArmed)
        {
            _tapArmed = false;
            var samePointer = _tapPointer == null || eventData == null || eventData.Pointer == _tapPointer;
            var quick = Time.time - _tapStarted <= TapSeconds;
            var still = (transform.position - _tapStartPosition).sqrMagnitude <= TapMoveMetres * TapMoveMetres;
            if (samePointer && quick && still && ForceState == State.Free)
            {
                ReturnHome();
            }
        }
    }

    /// <summary>
    /// Sends the body home: walked there by its <c>FreePlacementSolver</c> if it has one, else straight to Root. A
    /// moon's home is its orbit anchor, so it follows wherever its planet is now.
    /// </summary>
    public void ReturnHome()
    {
        _tapArmed = false;

        var placement = GetComponent<CosmicSimulation.FreePlacementSolver>();
        if (placement != null && placement.isActiveAndEnabled)
        {
            placement.RestoreLayout();
            return;
        }

        ResetToRoot();
        EnableForce = true;
    }

    // Desktop/mouse and UI entry point: pulls the object in front of the camera.
    public void OnPointerDown()
    {
        OnPointerDown(new GEPointerEventData(null));
    }

    public void OnPointerDown(GEPointerEventData eventData)
    {
        eventData = eventData ?? new GEPointerEventData(null);
        if (planetController == null)
        {
            planetController = FindFirstObjectByType<PlanetPreviewController>();
        }
        if (ForceState != State.Free)
        {
            _tapArmed = false;
        }

        switch (ForceState)
        {
            case State.Root:
                if (eventData.Pointer != null && eventData.IsNear)
                {
                    StartManipulation();
                    _manipulationHandler.OnPointerDown(eventData);
                }
                else if (eventData.Pointer == null || eventData.Pointer.IsMouse)
                {
                    // Desktop: no hand to pull to, so bring the object in front of the camera.
                    if (planetController != null)
                    {
                        if (previewTarget == null)
                        {
                            previewTarget = GetComponentInChildren<UiPreviewTarget>();
                        }

                        if (previewTarget != null)
                        {
                            planetController.OnButtonSelected(previewTarget.slotId);
                        }
                    }
                    StartAttraction(true);
                }
                else
                {
                    // Selecting from afar pulls the object to the selecting hand right away.
                    _focusers.Add(eventData.Pointer);
                    StartAttraction();
                }
                break;
            
            case State.Dwell:
                    StartManipulation();
                    _manipulationHandler.OnPointerDown(eventData);
                break;

            case State.Attraction:
            case State.Free:
                // Arm the tap-to-return test in OnPointerUp. Only from Free: a body still flying to the hand is not out yet.
                if (ForceState == State.Free)
                {
                    _tapArmed = true;
                    _tapPointer = eventData.Pointer;
                    _tapStarted = Time.time;
                    _tapStartPosition = transform.position;
                }
                else
                {
                    _tapArmed = false;
                }

                StartManipulation();
                if (eventData.Pointer != null)
                {
                    _manipulationHandler.OnPointerDown(eventData);
                }
                break;

            case State.Manipulation:
                // A second hand on a held body. The handler is not a pointer handler (see ManipulationPointerRouter),
                // so without this two hands could never scale a body. It ignores a pointer it already holds.
                if (eventData.Pointer != null)
                {
                    _manipulationHandler.OnPointerDown(eventData);
                }
                break;

            case State.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Scale limit when the handler has no constraint of its own (row bodies carry <c>ScaleLimits</c>; the
    /// orbit-model <c>poi_*</c> bodies do not).
    /// </summary>
    protected virtual Vector3 ClampHeldScale(Vector3 desiredLocalScale) => desiredLocalScale;

    public void OnPointerClicked(GEPointerEventData eventData)
    {
    }

    public void ResetToRoot()
    {
        StartRoot();
    }
}