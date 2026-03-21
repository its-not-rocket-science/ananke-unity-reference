using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Ananke
{
    public class AnankeReceiver : MonoBehaviour
    {
        [Header("WebSocket")]
        [Tooltip("WebSocket endpoint exposed by the TypeScript sidecar.")]
        public string streamUrl = "ws://127.0.0.1:3001/stream";

        [Tooltip("Delay before retrying after a disconnect.")]
        public float reconnectDelaySeconds = 1f;

        public AnankeFrameEnvelope LatestFrame { get; private set; }
        public bool IsConnected { get; private set; }

        private readonly ConcurrentQueue<string> _pendingPayloads = new();
        private CancellationTokenSource _lifetimeCts;
        private Task _receiveTask;

        private void Start()
        {
            _lifetimeCts = new CancellationTokenSource();
            _receiveTask = RunReceiveLoopAsync(_lifetimeCts.Token);
        }

        private void Update()
        {
            while (_pendingPayloads.TryDequeue(out var payload))
            {
                try
                {
                    LatestFrame = JsonUtility.FromJson<AnankeFrameEnvelope>(payload);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AnankeReceiver] Failed to parse frame: {ex.Message}");
                }
            }
        }

        private async Task RunReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);

                try
                {
                    await socket.ConnectAsync(new Uri(streamUrl), cancellationToken);
                    IsConnected = true;
                    Debug.Log($"[AnankeReceiver] Connected to {streamUrl}");
                    await ReceiveMessagesAsync(socket, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AnankeReceiver] Connection error: {ex.Message}");
                }
                finally
                {
                    IsConnected = false;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReceiveMessagesAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var builder = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                if (builder.Length > 0)
                    _pendingPayloads.Enqueue(builder.ToString());
            }
        }

        private void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _lifetimeCts?.Dispose();
        [Header("Sidecar Connection")]
        public string sidecarUrl = "http://127.0.0.1:7374";
        public string frameEndpoint = "/frame";
        public float pollIntervalSeconds = 0.05f;
        public int timeoutSeconds = 2;
        public bool startPollingOnEnable = true;

        public event Action<AnankeFrameEnvelope> FrameReceived;

        public AnankeFrameEnvelope LatestFrame { get; private set; }

        private bool _isPolling;

        private void OnEnable()
        {
            if (startPollingOnEnable)
            {
                BeginPolling();
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(PollFrame));
            _isPolling = false;
        }

        public void BeginPolling()
        {
            if (_isPolling)
            {
                return;
            }

            _isPolling = true;
            InvokeRepeating(nameof(PollFrame), 0f, pollIntervalSeconds);
        }

        private void PollFrame()
        {
            StartCoroutine(FetchFrame());
        }

        private IEnumerator FetchFrame()
        {
            using var request = UnityWebRequest.Get($"{sidecarUrl}{frameEndpoint}");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AnankeReceiver] Failed to fetch frame: {request.error}");
                yield break;
            }

            AnankeFrameEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<AnankeFrameEnvelope>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnankeReceiver] Could not parse frame JSON: {ex.Message}");
                yield break;
            }

            if (envelope == null || envelope.frames == null)
            {
                Debug.LogWarning("[AnankeReceiver] Received empty frame envelope.");
                yield break;
            }

            LatestFrame = envelope;
            FrameReceived?.Invoke(envelope);
        }
    }
}
