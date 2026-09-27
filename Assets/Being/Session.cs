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
        public event Action<string> Said, Heard, Error;
        public event Action<string, string> Acted;

        const string Url = "wss://api.openai.com/v1/realtime?model=";
        const string Tools =
            @"[{""type"":""function"",""name"":""open_place"",""description"":""Travel to a place in the simulation. Use the id from the knowledge list."",""parameters"":{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}}," +
            @"{""type"":""function"",""name"":""pull_body"",""description"":""Pull a planet, the Sun or a moon out of the row in front of the player. Only in the Planets place."",""parameters"":{""type"":""object"",""properties"":{""id"":{""type"":""string""}},""required"":[""id""]}}," +
            @"{""type"":""function"",""name"":""restore"",""description"":""Put every body back in its arrangement."",""parameters"":{""type"":""object"",""properties"":{}}}]";

        [Serializable] class Event { public string type, delta, transcript, name, call_id, arguments, response_id; public Fault error; public Reply response; }
        [Serializable] class Reply { public string id; }
        [Serializable] class Fault { public string message, code; }
        [Serializable] class Args { public string id; }

        readonly Realtime link = new Realtime();
        BeingSettings settings;
        string instructions;
        bool toolPending;
        string current, dropped;

        public bool IsReady { get; private set; }

        public Session()
        {
            link.Opened += Configure;
            link.Received += Handle;
            link.Failed += e => { IsReady = false; Error?.Invoke(e); };
        }

        public void Connect(string key, BeingSettings settings, string instructions)
        {
            this.settings = settings;
            this.instructions = instructions;
            link.Connect(Url + Uri.EscapeDataString(settings.model), key);
        }

        public void Pump() => link.Pump();

        public void Close()
        {
            IsReady = false;
            link.Close();
        }

        public void Append(byte[] pcm) => link.Send("{\"type\":\"input_audio_buffer.append\",\"audio\":\"" + Convert.ToBase64String(pcm) + "\"}");

        public void Clear() => link.Send("{\"type\":\"input_audio_buffer.clear\"}");

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
            link.Send("{\"type\":\"session.update\",\"session\":{\"type\":\"realtime\",\"output_modalities\":[\"audio\"],\"instructions\":\"" + Escape(instructions) +
                      "\",\"audio\":{\"input\":{\"format\":{\"type\":\"audio/pcm\",\"rate\":24000},\"transcription\":{\"model\":\"" + s.transcriptionModel + "\",\"language\":\"" + s.language + "\"},\"turn_detection\":" + vad +
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
                case "conversation.item.input_audio_transcription.completed":
                    Heard?.Invoke((e.transcript ?? "").Trim());
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
                    if (code.Contains("cancel") || code.Contains("commit_empty")) break;
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
