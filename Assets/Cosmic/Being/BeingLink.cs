using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Cosmic
{
    [Serializable]
    public class BeingMessage
    {
        public string type, text, name, args;
        public int audioBytes;
        public BeingContext context;
    }

    [Serializable]
    public class BeingContext
    {
        public string place, placeName, layout, platform;
        public string[] pulled;

        public static BeingContext Capture()
        {
            var director = Director.Instance;
            var current = director != null ? director.Current : null;
            var pulled = new List<string>();
            if (director != null && director.Content != null)
                foreach (var grab in director.Content.GetComponentsInChildren<Grabbable>())
                    if (grab.Placed || grab.isSelected) pulled.Add(grab.name);
            return new BeingContext
            {
                place = current != null ? current.id : "",
                placeName = current != null ? current.title : "",
                layout = director != null && director.CurrentLayout != null ? director.CurrentLayout.id : "",
                pulled = pulled.ToArray(),
                platform = UnityEngine.XR.XRSettings.isDeviceActive ? "headset" : "desktop",
            };
        }

        public static void Act(string name, string args)
        {
            var director = Director.Instance;
            if (director == null) return;
            switch (name)
            {
                case "open_place":
                    var place = App.Instance != null ? App.Instance.Find(args) : null;
                    if (place != null) director.Open(place);
                    break;
                case "pull_body":
                    var rig = director.CurrentRig;
                    for (var i = 0; rig != null && i < rig.Bodies.Count; i++)
                        if (rig.Bodies[i] != null && rig.Bodies[i].id == args) director.PullBody(i);
                    break;
                case "restore":
                    director.Restore();
                    break;
            }
        }
    }

    public class BeingLink : MonoBehaviour
    {
        public event Action<BeingMessage> Received;
        public event Action<byte[]> Audio;

        readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();
        readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        ClientWebSocket socket;
        CancellationTokenSource cancel;

        public bool Open => socket != null && socket.State == WebSocketState.Open;

        public async void Connect(string url, BeingContext context)
        {
            Close();
            socket = new ClientWebSocket();
            cancel = new CancellationTokenSource();
            try
            {
                await socket.ConnectAsync(new Uri(url), cancel.Token);
                Send(new BeingMessage { type = "hello", context = context });
                _ = Receive(socket, cancel.Token);
            }
            catch (Exception e) { Fail(e); }
        }

        public void Send(BeingMessage message) => Post(Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)), WebSocketMessageType.Text);

        public void Send(byte[] pcm) => Post(pcm, WebSocketMessageType.Binary);

        public void Close()
        {
            cancel?.Cancel();
            socket?.Dispose();
            socket = null;
        }

        void Update()
        {
            while (inbox.TryDequeue(out var action)) action();
        }

        void OnDestroy() => Close();

        void Fail(Exception e) => inbox.Enqueue(() => Received?.Invoke(new BeingMessage { type = "error", text = e.Message }));

        async void Post(byte[] bytes, WebSocketMessageType kind)
        {
            if (!Open) return;
            await sending.WaitAsync();
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), kind, true, cancel.Token); }
            catch (Exception e) { Fail(e); }
            finally { sending.Release(); }
        }

        async Task Receive(ClientWebSocket from, CancellationToken token)
        {
            var chunk = new byte[64 * 1024];
            var whole = new MemoryStream();
            try
            {
                while (from.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await from.ReceiveAsync(new ArraySegment<byte>(chunk), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    whole.Write(chunk, 0, result.Count);
                    if (!result.EndOfMessage) continue;
                    var bytes = whole.ToArray();
                    whole.SetLength(0);
                    if (result.MessageType == WebSocketMessageType.Binary) inbox.Enqueue(() => Audio?.Invoke(bytes));
                    else
                    {
                        var message = JsonUtility.FromJson<BeingMessage>(Encoding.UTF8.GetString(bytes));
                        inbox.Enqueue(() => Received?.Invoke(message));
                    }
                }
            }
            catch (Exception e) when (!token.IsCancellationRequested) { Fail(e); }
        }
    }
}
