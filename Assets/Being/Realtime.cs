using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cosmic.Companion
{
    public class Realtime
    {
        public event Action Opened;
        public event Action<string> Received;
        public event Action<string> Failed;

        readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();
        readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        ClientWebSocket socket;
        CancellationTokenSource cancel;

        public bool Open => socket != null && socket.State == WebSocketState.Open;

        public async void Connect(string url, string bearer)
        {
            Close();
            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", "Bearer " + bearer);
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            var token = new CancellationTokenSource();
            socket = ws;
            cancel = token;
            try
            {
                await ws.ConnectAsync(new Uri(url), token.Token);
                inbox.Enqueue(() => Opened?.Invoke());
                _ = Receive(ws, token.Token);
            }
            catch (Exception e) { Fail(e); }
        }

        public async void Send(string json)
        {
            var ws = socket;
            var token = cancel;
            if (ws == null || ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            await sending.WaitAsync();
            try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token.Token); }
            catch (Exception e) { if (!token.IsCancellationRequested) Fail(e); }
            finally { sending.Release(); }
        }

        public void Pump()
        {
            while (inbox.TryDequeue(out var action)) action();
        }

        public void Close()
        {
            cancel?.Cancel();
            socket?.Dispose();
            socket = null;
        }

        void Fail(Exception e) => inbox.Enqueue(() => Failed?.Invoke(e.Message));

        async Task Receive(ClientWebSocket ws, CancellationToken token)
        {
            var chunk = new byte[64 * 1024];
            var whole = new MemoryStream();
            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        var why = result.CloseStatusDescription ?? result.CloseStatus?.ToString() ?? "closed";
                        inbox.Enqueue(() => Failed?.Invoke(why));
                        return;
                    }
                    whole.Write(chunk, 0, result.Count);
                    if (!result.EndOfMessage) continue;
                    var text = Encoding.UTF8.GetString(whole.GetBuffer(), 0, (int)whole.Length);
                    whole.SetLength(0);
                    inbox.Enqueue(() => Received?.Invoke(text));
                }
            }
            catch (Exception e) when (!token.IsCancellationRequested) { Fail(e); }
        }
    }
}
