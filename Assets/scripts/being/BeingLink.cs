using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CosmicSimulation.Being
{
    [Serializable]
    public class BeingMessage
    {
        public string type;
        public string text;
        public string name;
        public string args;
        public int audioBytes;
        public BeingContext context;
    }

    public class BeingLink : MonoBehaviour
    {
        public event Action<BeingMessage> Received;
        public event Action<byte[]> Audio;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cancel;
        private readonly ConcurrentQueue<Action> _inbox = new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _sending = new SemaphoreSlim(1, 1);

        public bool IsOpen => _socket != null && _socket.State == WebSocketState.Open;

        public async void Connect(string url, BeingContext context)
        {
            Close();
            _socket = new ClientWebSocket();
            _cancel = new CancellationTokenSource();
            try
            {
                await _socket.ConnectAsync(new Uri(url), _cancel.Token);
                Send(new BeingMessage { type = "hello", context = context });
                _ = Receive(_socket, _cancel.Token);
            }
            catch (Exception e)
            {
                _inbox.Enqueue(() => Received?.Invoke(new BeingMessage { type = "error", text = e.Message }));
            }
        }

        public void Send(BeingMessage message) => Post(Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)), WebSocketMessageType.Text);

        public void Send(byte[] pcm) => Post(pcm, WebSocketMessageType.Binary);

        private async void Post(byte[] bytes, WebSocketMessageType kind)
        {
            if (!IsOpen)
            {
                return;
            }

            await _sending.WaitAsync();
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), kind, true, _cancel.Token);
            }
            catch (Exception e)
            {
                _inbox.Enqueue(() => Received?.Invoke(new BeingMessage { type = "error", text = e.Message }));
            }
            finally
            {
                _sending.Release();
            }
        }

        private async Task Receive(ClientWebSocket socket, CancellationToken token)
        {
            var chunk = new byte[64 * 1024];
            var whole = new MemoryStream();
            try
            {
                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(chunk), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    whole.Write(chunk, 0, result.Count);
                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    var bytes = whole.ToArray();
                    whole.SetLength(0);
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _inbox.Enqueue(() => Audio?.Invoke(bytes));
                    }
                    else
                    {
                        var message = JsonUtility.FromJson<BeingMessage>(Encoding.UTF8.GetString(bytes));
                        _inbox.Enqueue(() => Received?.Invoke(message));
                    }
                }
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                _inbox.Enqueue(() => Received?.Invoke(new BeingMessage { type = "error", text = e.Message }));
            }
        }

        private void Update()
        {
            while (_inbox.TryDequeue(out var action))
            {
                action();
            }
        }

        public void Close()
        {
            _cancel?.Cancel();
            _socket?.Dispose();
            _socket = null;
        }

        private void OnDestroy() => Close();
    }
}
