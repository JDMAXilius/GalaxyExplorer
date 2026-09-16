using System.Collections;
using UnityEngine;

namespace Cosmic
{
    [RequireComponent(typeof(BeingLink), typeof(BeingMic), typeof(BeingVoice))]
    public class Being : MonoBehaviour
    {
        public enum Phase { Idle, Listening, Thinking, Speaking }

        [SerializeField] BeingSettings settings;
        [SerializeField] BeingLook look;
        [SerializeField] Grabbable grab;
        [SerializeField] bool connectOnStart = true;

        BeingLink link;
        BeingMic mic;
        BeingVoice voice;
        Vector3 pressedAt;
        float pressedTime;
        bool answerDone, greeting, leaving, ready;

        public Phase Current { get; private set; }
        public bool Greeting => greeting;
        public bool Listening => Current == Phase.Listening;
        public string Heard { get; private set; } = "";

        public void Tap()
        {
            Init();
            if (Grabbable.Bus != null) Grabbable.Bus.Play(Sfx.Select, transform);
            if (Current == Phase.Listening) { mic.Stop(); return; }
            if (Current != Phase.Idle || greeting) { Interrupt(); return; }
            if (settings.greetOnTap && settings.greeting != null && voice.Say(settings.greeting)) greeting = true;
            else Listen();
        }

        public void Ask(string text)
        {
            Init();
            Interrupt();
            Set(Phase.Thinking);
            link.Send(new BeingMessage { type = "turn", text = text, context = BeingContext.Capture() });
        }

        public void Dismiss()
        {
            if (leaving) return;
            leaving = true;
            StartCoroutine(Leave());
        }

        void OnEnable()
        {
            Init();
            link.Received += OnMessage;
            link.Audio += voice.Enqueue;
            voice.Started += OnStarted;
            voice.Drained += OnDrained;
            if (grab != null) { grab.Grabbed += OnGrabbed; grab.Released += OnReleased; }
        }

        void OnDisable()
        {
            link.Received -= OnMessage;
            link.Audio -= voice.Enqueue;
            voice.Started -= OnStarted;
            voice.Drained -= OnDrained;
            if (grab != null) { grab.Grabbed -= OnGrabbed; grab.Released -= OnReleased; }
        }

        void Start()
        {
            StartCoroutine(look.Reveal(true));
            if (connectOnStart) link.Connect(settings.relayUrl, BeingContext.Capture());
        }

        void Update() => look.Apply(Current, voice.Loudness, voice.Bands);

        void OnGrabbed(Grabbable g)
        {
            pressedTime = Time.unscaledTime;
            pressedAt = transform.position;
            var hand = g.firstInteractorSelecting;
            look.Press(hand != null ? hand.GetAttachTransform(g).position : transform.position);
        }

        void OnReleased(Grabbable g)
        {
            var quick = Time.unscaledTime - pressedTime <= settings.tapSeconds;
            var still = (transform.position - pressedAt).sqrMagnitude <= settings.tapMoveMetres * settings.tapMoveMetres;
            if (quick && still && !g.isSelected) Tap();
        }

        void Listen()
        {
            Set(Phase.Listening);
            mic.Record(settings, OnUtterance);
        }

        void OnUtterance(byte[] pcm)
        {
            if (pcm == null || !link.Open)
            {
                Heard = pcm == null ? $"nothing on {mic.DeviceLabel}" : $"{pcm.Length / 2f / BeingMic.Rate:0.0} s on {mic.DeviceLabel}, relay offline";
                Debug.Log($"Being: heard {Heard}.", this);
                Set(Phase.Idle);
                return;
            }
            Heard = $"{pcm.Length / 2f / BeingMic.Rate:0.0} s on {mic.DeviceLabel}";
            Set(Phase.Thinking);
            link.Send(new BeingMessage { type = "turn", audioBytes = pcm.Length, context = BeingContext.Capture() });
            link.Send(pcm);
        }

        void OnMessage(BeingMessage message)
        {
            switch (message.type)
            {
                case "action": BeingContext.Act(message.name, message.args); break;
                case "done": answerDone = true; voice.Finish(); break;
                case "error":
                    Debug.Log($"Being: relay says {message.text}", this);
                    if (!greeting) Interrupt();
                    break;
            }
        }

        void OnStarted() => Set(Phase.Speaking);

        void OnDrained()
        {
            if (greeting) { greeting = false; Listen(); }
            else if (answerDone) Set(Phase.Idle);
        }

        void Interrupt()
        {
            if (Current == Phase.Listening) mic.Stop();
            if (!greeting && (Current == Phase.Thinking || Current == Phase.Speaking)) link.Send(new BeingMessage { type = "interrupt" });
            greeting = false;
            voice.Clear();
            Set(Phase.Idle);
        }

        void Set(Phase phase)
        {
            if (phase != Phase.Idle && phase != Phase.Speaking) answerDone = false;
            Current = phase;
        }

        IEnumerator Leave()
        {
            Interrupt();
            link.Close();
            yield return look.Reveal(false);
            Destroy(gameObject);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            link = GetComponent<BeingLink>();
            mic = GetComponent<BeingMic>();
            voice = GetComponent<BeingVoice>();
        }
    }
}
