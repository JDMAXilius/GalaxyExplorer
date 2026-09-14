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

        public Phase Current { get; private set; }

        private BeingLink _link;
        private BeingMic _mic;
        private BeingSpeaker _speaker;
        private bool _answerDone;

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

        private void Update() => visual.Apply(Current, _speaker.Loudness);

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void OnPointerClicked(GEPointerEventData eventData) => Tap();

        public void OnPointerDown(GEPointerEventData eventData) { }

        public void OnPointerUp(GEPointerEventData eventData) { }

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
