using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Cosmic.Companion
{
    public class Session
    {
        public event Action Ready, SpeechStarted, SpeechStopped, Done;
        public event Action<byte[]> Audio;
        public event Action<string> Said, Error;
        public event Action<string, string> Acted;
        // The player's words for one utterance (item id, text): Hearing as the transcription streams, Heard once it is final.
        public event Action<string, string> Hearing, Heard;

        const string Url = "wss://api.openai.com/v1/realtime?model=";
        const string Tools =
            @"[{""type"":""function"",""name"":""open_place"",""description"":""Travel to a place in the simulation, including a place listed as opening from the current map. Use the id from the knowledge or the situation line."",""parameters"":{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}}," +
            @"{""type"":""function"",""name"":""pull_body"",""description"":""Bring a body out in front of the player, which also puts it on the info card. Ids the situation line lists as pullable here, or a moon of a planet that is here (its planet comes out first)."",""parameters"":{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}}," +
            @"{""type"":""function"",""name"":""send_home"",""description"":""Send one pulled-out body back to its place."",""parameters"":{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}}," +
            @"{""type"":""function"",""name"":""play_sound"",""description"":""Play the sound of what the info card shows: the selected body, or the place."",""parameters"":{""type"":""object"",""properties"":{}}}," +
            @"{""type"":""function"",""name"":""restore"",""description"":""Put every body back in its arrangement."",""parameters"":{""type"":""object"",""properties"":{}}}]";

        [Serializable] class Event { public string type, delta, transcript, name, call_id, arguments, response_id, item_id; public Fault error; public Reply response; }
        [Serializable] class Reply { public string id; }
        [Serializable] class Fault { public string message, code; }
        [Serializable] class Args { public string id; }

        readonly Realtime link = new Realtime();
        readonly System.Collections.Generic.Dictionary<string, string> hearing = new System.Collections.Generic.Dictionary<string, string>();
        BeingSettings settings;
        string instructions;
        bool toolPending, nearField;
        string current, dropped;

        public bool IsReady { get; private set; }

        public Session()
        {
            link.Opened += Configure;
            link.Received += Handle;
            link.Failed += e => { IsReady = false; Error?.Invoke(e); };
        }

        /// <param name="nearField">The microphone sits at the mouth (a headset's own); otherwise it is a desk or room mic.</param>
        public void Connect(string key, BeingSettings settings, string instructions, bool nearField)
        {
            this.settings = settings;
            this.instructions = instructions;
            this.nearField = nearField;
            link.Connect(Url + Uri.EscapeDataString(settings.model), key);
        }

        public void Pump() => link.Pump();

        public void Close()
        {
            IsReady = false;
            hearing.Clear();
            link.Close();
        }

        public void Append(byte[] pcm) => link.Send("{\"type\":\"input_audio_buffer.append\",\"audio\":\"" + Convert.ToBase64String(pcm) + "\"}");

        public void Clear() => link.Send("{\"type\":\"input_audio_buffer.clear\"}");

        /// <summary>
        /// Ends the player's turn by hand (a tap, or the client's own silence check): what they said is sent and
        /// answered rather than thrown away. If the server's VAD ends the same turn as well, its commit is empty and
        /// its commit_empty error is ignored below.
        /// </summary>
        public void Commit()
        {
            link.Send("{\"type\":\"input_audio_buffer.commit\"}");
            Respond();
        }

        public void Situate(string line) => link.Send(Item(line));

        public void Ask(string text)
        {
            link.Send(Item(text));
            Respond();
        }

        public void Greet() => Ask("[The player has just summoned you. Greet them in one short sentence and invite a question.]");

        // Audio already in flight keeps arriving after a cancel, so everything tagged with the cancelled answer is dropped.
        public void Cancel()
        {
            dropped = current;
            toolPending = false;
            link.Send("{\"type\":\"response.cancel\"}");
        }

        void Respond() => link.Send("{\"type\":\"response.create\"}");

        static string Item(string text) => "{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"message\",\"role\":\"user\",\"content\":[{\"type\":\"input_text\",\"text\":\"" + Escape(text) + "\"}]}}";

        void Configure()
        {
            var s = settings;
            var vad = "{\"type\":\"server_vad\",\"threshold\":" + s.vadThreshold.ToString("0.00", CultureInfo.InvariantCulture) +
                      ",\"prefix_padding_ms\":300,\"silence_duration_ms\":" + s.silenceMs + ",\"create_response\":true,\"interrupt_response\":false}";
            // Noise reduction runs before the VAD and the transcriber: a quiet desk mic's hiss is what the transcriber
            // turned into words nobody said. The prompt tells it what the player is likely to say.
            var noise = "{\"type\":\"" + (nearField ? "near_field" : "far_field") + "\"}";
            var transcription = "{\"model\":\"" + s.transcriptionModel + "\",\"language\":\"" + s.language + "\"" +
                                (string.IsNullOrEmpty(s.transcriptionPrompt) ? "" : ",\"prompt\":\"" + Escape(s.transcriptionPrompt) + "\"") + "}";
            link.Send("{\"type\":\"session.update\",\"session\":{\"type\":\"realtime\",\"output_modalities\":[\"audio\"],\"instructions\":\"" + Escape(instructions) +
                      "\",\"audio\":{\"input\":{\"format\":{\"type\":\"audio/pcm\",\"rate\":24000},\"noise_reduction\":" + noise + ",\"transcription\":" + transcription + ",\"turn_detection\":" + vad +
                      "},\"output\":{\"format\":{\"type\":\"audio/pcm\",\"rate\":24000},\"voice\":\"" + s.voice + "\"}},\"tools\":" + Tools + ",\"tool_choice\":\"auto\"}}");
        }

        void Handle(string json)
        {
            Event e;
            try { e = JsonUtility.FromJson<Event>(json); }
            catch { return; }
            switch (e.type)
            {
                case "session.updated":
                    IsReady = true;
                    Ready?.Invoke();
                    break;
                case "response.created":
                    current = e.response?.id;
                    break;
                case "response.output_audio.delta":
                    if (e.response_id == dropped) break;
                    if (!string.IsNullOrEmpty(e.delta)) Audio?.Invoke(Convert.FromBase64String(e.delta));
                    break;
                case "response.output_audio_transcript.done":
                    if (e.response_id == dropped) break;
                    Said?.Invoke(e.transcript ?? "");
                    break;
                case "conversation.item.input_audio_transcription.delta":
                {
                    var key = e.item_id ?? "";
                    hearing.TryGetValue(key, out var sofar);
                    sofar = (sofar ?? "") + (e.delta ?? "");
                    hearing[key] = sofar;
                    Hearing?.Invoke(key, sofar.Trim());
                    break;
                }
                case "conversation.item.input_audio_transcription.completed":
                    hearing.Remove(e.item_id ?? "");
                    Heard?.Invoke(e.item_id ?? "", (e.transcript ?? "").Trim());
                    break;
                case "input_audio_buffer.speech_started":
                    SpeechStarted?.Invoke();
                    break;
                case "input_audio_buffer.speech_stopped":
                    SpeechStopped?.Invoke();
                    break;
                case "response.function_call_arguments.done":
                    if (e.response_id == dropped) break;
                    toolPending = true;
                    var id = "";
                    try { id = JsonUtility.FromJson<Args>(e.arguments ?? "{}")?.id ?? ""; } catch { }
                    Acted?.Invoke(e.name, id);
                    link.Send("{\"type\":\"conversation.item.create\",\"item\":{\"type\":\"function_call_output\",\"call_id\":\"" + Escape(e.call_id) + "\",\"output\":\"done\"}}");
                    Respond();
                    break;
                case "response.done":
                    if (e.response?.id == dropped) break;
                    if (toolPending) toolPending = false;
                    else Done?.Invoke();
                    break;
                case "error":
                    var code = e.error?.code ?? "";
                    // already_has_active_response: a tap's Commit raced the server's own end of the same turn, whose
                    // answer is already coming; treating it as a failure would cancel that answer.
                    if (code.Contains("cancel") || code.Contains("commit_empty") || code.Contains("already_has_active_response")) break;
                    Error?.Invoke(e.error?.message ?? json);
                    break;
            }
        }

        static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 16);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
