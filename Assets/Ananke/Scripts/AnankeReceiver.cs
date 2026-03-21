using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Ananke
{
    public class AnankeReceiver : MonoBehaviour
    {
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
