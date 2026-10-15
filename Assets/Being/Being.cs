using System.Collections;
using UnityEngine;

namespace Cosmic.Companion
{
    [RequireComponent(typeof(Voice), typeof(Look), typeof(Follow))]
    public class Being : MonoBehaviour
    {
        public enum Phase { Idle, Listening, Thinking, Speaking }

        // Mic RMS that counts as someone talking, how long it keeps the listen window open after, and the most a noisy
        // room can stretch the window from when it opened.
        const float SpeechFloor = 0.02f;
        const float SpeechHoldSeconds = 1f;
        const float MaxHoldSeconds = 10f;

        [SerializeField] BeingSettings settings;
        [SerializeField] BeingKeys keys;
        [SerializeField] Look look;

        readonly Session session = new Session();
        readonly Mic mic = new Mic();
        Voice voice;
        Vector3 pressedAt;
        float pressedTime, window, micPeak, openAt;
        bool greeting, answerDone, leaving, ready, speechHeard, rearm, relisten, gone;
        string situated;

        public Phase Current { get; private set; }
        public IHost Host { get; private set; }
        public bool Online => session.IsReady;
        public bool Greeting => greeting;
        public bool AlwaysListening => settings.alwaysListening;
        public string Heard { get; private set; } = "";
        public string Said { get; private set; } = "";
        public float MicLevel => Current == Phase.Listening ? mic.Level : 0f;
        public int StartLatencyMs => voice.StartLatencyMs;
        public int ShortReads => voice.ShortReads;

        public void Attach(IHost host) => Host = host;

        public void Press(Vector3 at)
        {
            Init();
            pressedTime = Time.unscaledTime;
            pressedAt = transform.position;
            look.Press(at);
        }

        public void Release()
        {
            var quick = Time.unscaledTime - pressedTime <= settings.tapSeconds;
            var still = (transform.position - pressedAt).sqrMagnitude <= settings.tapMoveMetres * settings.tapMoveMetres;
            if (quick && still && !(Host != null && Host.Held)) Tap();
        }

        public void Tap()
        {
            Init();
            Host?.Click();
            switch (Current)
            {
                case Phase.Idle: Listen(true, 0f); break;
                case Phase.Listening: Stop(); break;
                default: Interrupt(); Listen(true, settings.echoTailSeconds); break;
            }
        }

        public void Ask(string text)
        {
            Init();
            if (!session.IsReady) { Debug.Log("Being: offline, cannot ask.", this); return; }
            Interrupt();
            Situate();
            session.Ask(text);
            Set(Phase.Thinking);
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
            session.Audio += voice.Enqueue;
            session.Said += OnSaid;
            session.Heard += OnHeard;
            session.SpeechStarted += OnSpeechStarted;
            session.SpeechStopped += OnSpeechStopped;
            session.Done += OnDone;
            session.Acted += OnActed;
            session.Error += OnError;
            mic.Frame += session.Append;
            voice.Started += OnStarted;
            voice.Drained += OnDrained;
        }

        void OnDisable()
        {
            session.Audio -= voice.Enqueue;
            session.Said -= OnSaid;
            session.Heard -= OnHeard;
            session.SpeechStarted -= OnSpeechStarted;
            session.SpeechStopped -= OnSpeechStopped;
            session.Done -= OnDone;
            session.Acted -= OnActed;
            session.Error -= OnError;
            mic.Frame -= session.Append;
            voice.Started -= OnStarted;
            voice.Drained -= OnDrained;
        }

        void Start()
        {
            voice.Configure(settings);
            StartCoroutine(look.Reveal(true));
            StartCoroutine(Boot());
        }

        void Update()
        {
            session.Pump();
            mic.Poll();
            look.Apply(Current, voice.Loudness, voice.Bands, MicLevel);
            if (Current != Phase.Listening) return;
            if (!mic.Listening)
            {
                if (Time.unscaledTime < openAt) return;
                mic.Start();
            }
            micPeak = Mathf.Max(micPeak, mic.Level);
            if (speechHeard || settings.alwaysListening) return;
            // Someone is talking but the server has not called it speech yet (a quieter headset mic): hold the window (CS-371).
            if (mic.Level > SpeechFloor && Time.unscaledTime - openAt < MaxHoldSeconds) window = Mathf.Max(window, SpeechHoldSeconds);
            window -= Time.unscaledDeltaTime;
            if (window <= 0f) Stop();
        }

        // Android stops a paused app's capture and may not give it back, and under the Meta system menu (a focus loss,
        // not a pause) the room should not stream to the guide: release the mic, and take it again on return (CS-371).
        void OnApplicationPause(bool paused) => Away(paused);

        void OnApplicationFocus(bool focused)
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive) Away(!focused);
        }

        void Away(bool away)
        {
            if (away == gone) return;
            gone = away;
            if (away)
            {
                rearm = mic.Armed;
                relisten = mic.Listening;
                mic.Close();
            }
            else if (rearm && mic.Arm() && relisten)
            {
                mic.Start();
            }
        }

        void OnDestroy()
        {
            mic.Close();
            session.Close();
            Host?.Duck(false);
        }

        IEnumerator Boot()
        {
            Set(Phase.Thinking);
            // Asked once a session on summon; after that only the guide's Enable microphone asks, so a player who
            // summons the being twice and says no twice is not locked out by Android's "don't ask again" (CS-373).
            if (!Mic.Asked) yield return Mic.Request();
            mic.Arm();
            var key = keys != null ? keys.OpenAi : "";
            if (key.Length > 0) session.Connect(key, settings, Instructions());
            else Debug.Log("Being: no OpenAI key; offline.", this);
            for (var t = 0f; t < settings.connectTimeoutSeconds && !session.IsReady && key.Length > 0; t += Time.unscaledDeltaTime) yield return null;
            greeting = true;
            if (session.IsReady) { Situate(); session.Greet(); }
            else if (!voice.Say(settings.greeting)) { greeting = false; Listen(true, 0f); }
        }

        // The microphone opens only once the being's own voice and the chime have left the speakers, or it answers itself.
        void Listen(bool chime, float quietSeconds)
        {
            if (!mic.Armed && !mic.Arm()) { Debug.Log("Being: " + mic.Label, this); Set(Phase.Idle); return; }
            speechHeard = false;
            answerDone = false;
            micPeak = 0f;
            window = settings.listenWindowSeconds;
            Situate();
            var cue = chime && settings.listenChime != null ? settings.listenChime.length + 0.05f : 0f;
            if (chime) voice.Cue(settings.listenChime);
            openAt = Time.unscaledTime + quietSeconds + cue;
            Set(Phase.Listening);
        }

        void Stop()
        {
            mic.Stop();
            session.Clear();
            voice.Cue(settings.closeTick);
            if (!session.IsReady) Debug.Log($"Being: offline, mic peak {micPeak:0.000} on {mic.Label}.", this);
            else if (!speechHeard) Debug.Log($"Being: window closed with no speech heard, mic peak {micPeak:0.000} on {mic.Label} (VAD {settings.vadThreshold:0.00}).", this);
            Set(Phase.Idle);
        }

        void Interrupt()
        {
            if (Current == Phase.Listening) { mic.Stop(); session.Clear(); }
            if (Current == Phase.Thinking || Current == Phase.Speaking) session.Cancel();
            greeting = false;
            answerDone = false;
            voice.Clear();
            Set(Phase.Idle);
        }

        void OnSpeechStarted()
        {
            speechHeard = true;
            // Before the turn this speech becomes: it may have changed since the window opened (always-listening keeps one open).
            Situate();
        }

        void OnSpeechStopped()
        {
            if (Current != Phase.Listening) return;
            mic.Stop();
            Set(Phase.Thinking);
        }

        void OnStarted()
        {
            Set(Phase.Speaking);
            Debug.Log($"Being: speaking after {voice.StartLatencyMs} ms.", this);
        }

        void OnDone()
        {
            if (Current != Phase.Thinking && Current != Phase.Speaking) return;
            answerDone = true;
            voice.Finish();
        }

        void OnDrained()
        {
            if (greeting) { greeting = false; Listen(true, settings.echoTailSeconds); }
            else if (answerDone) Listen(false, settings.echoTailSeconds);
        }

        void OnHeard(string text)
        {
            Heard = text;
            Debug.Log("Being heard: " + text, this);
        }

        void OnSaid(string text) => Said = text;

        void OnActed(string name, string id) => Host?.Act(name, id);

        void OnError(string text)
        {
            Debug.LogWarning("Being: " + text, this);
            if (Current == Phase.Thinking || Current == Phase.Speaking) Interrupt();
        }

        void Set(Phase phase)
        {
            Current = phase;
            Host?.Duck(phase != Phase.Idle);
        }

        // Told again only when it changed: the line is a conversation item, and a repeat of it is noise.
        void Situate()
        {
            if (!session.IsReady) return;
            var line = Situation();
            if (line == situated) return;
            situated = line;
            session.Situate(line);
        }

        string Situation()
        {
            var s = Host != null ? Host.Situation() : default;
            var platform = UnityEngine.XR.XRSettings.isDeviceActive ? "headset" : "desktop";
            var line = string.IsNullOrEmpty(s.place)
                ? "[Situation: the player is in the intro"
                : $"[Situation: the player is in {s.place} (id {s.placeId})";
            line += ", on " + platform + ".";
            if (!string.IsNullOrEmpty(s.layout)) line += " Layout: " + s.layout + ".";
            if (!string.IsNullOrEmpty(s.selected)) line += " The info card shows " + s.selected + ".";
            if (!string.IsNullOrEmpty(s.previewing)) line += " The card is previewing " + s.previewing + ", not yet entered.";
            if (s.held != null && s.held.Length > 0) line += " Pulled out now: " + string.Join(", ", s.held) + ".";
            if (s.bodies != null && s.bodies.Length > 0) line += " Bodies pull_body can bring out here: " + string.Join(", ", s.bodies) + ".";
            if (s.destinations != null && s.destinations.Length > 0) line += " Places that open from this map: " + string.Join(", ", s.destinations) + ".";
            return line + "]";
        }

        string Instructions() => (settings.prompt != null ? settings.prompt.text : "") + "\n\n" + (settings.knowledge != null ? settings.knowledge.text : "");

        IEnumerator Leave()
        {
            Interrupt();
            session.Close();
            mic.Close();
            yield return look.Reveal(false);
            Destroy(gameObject);
        }

        void Init()
        {
            if (ready) return;
            ready = true;
            voice = GetComponent<Voice>();
            if (look == null) look = GetComponent<Look>();
        }
    }
}
