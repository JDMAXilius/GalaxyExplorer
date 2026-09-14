using System.Collections;
using GalaxyExplorer.XR;
using UnityEngine;

namespace CosmicSimulation.Being
{
    [RequireComponent(typeof(BeingLink), typeof(BeingMic), typeof(BeingSpeaker))]
    public class CosmicBeing : MonoBehaviour, IGEPointerHandler
    {
        public enum Phase { Idle, Listening, Thinking, Speaking }

        public static CosmicBeing Instance { get; private set; }

        [SerializeField] private BeingSettings settings;
        [SerializeField] private BeingVisual visual;

        /// <summary>A press and release closer together than this, and barely moved, is a tap. Matches ForceSolver.</summary>
        private const float TapSeconds = 0.4f;
        private const float TapMoveMetres = 0.03f;

        public Phase Current { get; private set; }

        private BeingLink _link;
        private BeingMic _mic;
        private BeingSpeaker _speaker;
        private ManipulationHandler _hands;
        private bool _answerDone;

        private bool _tapArmed;
        private GEPointer _tapPointer;
        private float _tapStarted;
        private Vector3 _tapStartPosition;

        public static void Toggle(CosmicBeing prefab)
        {
            if (Instance != null)
            {
                Instance.Dismiss();
            }
            else if (prefab != null)
            {
                Instantiate(prefab);
            }
        }

        private void Awake()
        {
            Instance = this;
            _link = GetComponent<BeingLink>();
            _mic = GetComponent<BeingMic>();
            _speaker = GetComponent<BeingSpeaker>();
            _hands = GetComponent<ManipulationHandler>();
            _link.Received += OnMessage;
            _link.Audio += _speaker.Enqueue;
            _speaker.Started += () => Set(Phase.Speaking);
            _speaker.Drained += OnDrained;
        }

        private void Start()
        {
            StartCoroutine(visual.Reveal(true));
            _link.Connect(settings.RelayUrl, BeingContext.Capture());
        }

        private void Update() => visual.Apply(Current, _speaker.Loudness, _speaker.Bands);

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Handled on press and release rather than here. A click event cannot tell a tap from a carry, and the
        /// being can be carried: <see cref="OnPointerUp"/> runs the same quick-and-still test the bodies use
        /// for "put it back" (CS-174), so dragging it across the room never starts a conversation.
        /// </summary>
        public void OnPointerClicked(GEPointerEventData eventData) { }

        public void OnPointerDown(GEPointerEventData eventData)
        {
            _tapArmed = true;
            _tapPointer = eventData?.Pointer;
            _tapStarted = Time.time;
            _tapStartPosition = transform.position;

            // The press is answered whatever it turns out to be. A carry that begins with a pop reads as the
            // being noticing the hand, which is true - it is only the talking that waits to see if this was a tap.
            var attach = _tapPointer != null ? _tapPointer.AttachTransform : null;
            visual.Press(attach != null ? attach.position : transform.position);

            // The hands are told too, the way a body's ForceSolver tells its handler: ManipulationHandler is
            // deliberately not a pointer handler, so without this a drag would never carry the being.
            if (_hands != null && eventData?.Pointer != null)
            {
                _hands.OnPointerDown(eventData);
            }
        }

        public void OnPointerUp(GEPointerEventData eventData)
        {
            if (_hands != null && eventData?.Pointer != null)
            {
                _hands.OnPointerUp(eventData);
            }

            if (!_tapArmed)
            {
                return;
            }

            _tapArmed = false;
            var samePointer = _tapPointer == null || eventData == null || eventData.Pointer == _tapPointer;
            var quick = Time.time - _tapStarted <= TapSeconds;
            var still = (transform.position - _tapStartPosition).sqrMagnitude <= TapMoveMetres * TapMoveMetres;
            if (samePointer && quick && still)
            {
                Tap();
            }
        }

        public void Tap()
        {
            switch (Current)
            {
                case Phase.Idle:
                    Set(Phase.Listening);
                    _mic.Record(settings, OnUtterance);
                    break;
                case Phase.Listening:
                    _mic.Stop();
                    break;
                default:
                    Interrupt();
                    break;
            }
        }

        public void Ask(string text)
        {
            Interrupt();
            Set(Phase.Thinking);
            _link.Send(new BeingMessage { type = "turn", text = text, context = BeingContext.Capture() });
        }

        public void Dismiss() => StartCoroutine(DismissRoutine());

        private void OnUtterance(byte[] pcm)
        {
            if (pcm == null || !_link.IsOpen)
            {
                Set(Phase.Idle);
                return;
            }

            Set(Phase.Thinking);
            _link.Send(new BeingMessage { type = "turn", audioBytes = pcm.Length, context = BeingContext.Capture() });
            _link.Send(pcm);
        }

        private void OnMessage(BeingMessage message)
        {
            switch (message.type)
            {
                case "action":
                    BeingActions.Run(message.name, message.args);
                    break;
                case "done":
                    _answerDone = true;
                    _speaker.Finish();
                    break;
                case "error":
                    SwitchNotice.Instance?.Show(message.text);
                    Interrupt();
                    break;
            }
        }

        private void OnDrained()
        {
            if (_answerDone)
            {
                Set(Phase.Idle);
            }
        }

        private void Interrupt()
        {
            if (Current == Phase.Listening)
            {
                _mic.Stop();
            }
            if (Current == Phase.Thinking || Current == Phase.Speaking)
            {
                _link.Send(new BeingMessage { type = "interrupt" });
            }
            _speaker.Clear();
            Set(Phase.Idle);
        }

        private void Set(Phase phase)
        {
            if (phase != Phase.Idle && phase != Phase.Speaking)
            {
                _answerDone = false;
            }
            Current = phase;
        }

        private IEnumerator DismissRoutine()
        {
            Interrupt();
            _link.Close();
            yield return visual.Reveal(false);
            Destroy(gameObject);
        }
    }
}
