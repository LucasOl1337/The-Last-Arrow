using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.AI
{
    public sealed class AiHttpPollingInputSource : MonoBehaviour, ICombatantInputSource
    {
        private static readonly HttpClient SharedHttpClient = new HttpClient();

        public MatchController matchController;
        public string endpoint = "http://127.0.0.1:8765/arena/act";
        [Min(10)] public int pollingIntervalMs = 33;
        [Min(10)] public int requestTimeoutMs = 60;
        [Min(20)] public int staleActionTimeoutMs = 120;
        public AiActionFallbackMode fallbackMode = AiActionFallbackMode.HoldLastContinuous;
        public bool logFailures;

        private readonly object _stateLock = new object();
        private PlayerController _player;
        private PlayerInputFrame _currentFrame;
        private PlayerInputFrame _lastAppliedFrame;
        private AiFrameAction _latestAction;
        private string _pendingResponseJson = string.Empty;
        private string _debugState = "idle";
        private string _lastError = string.Empty;
        private string _lastLoggedError = string.Empty;
        private long _lastActionReceivedAtMs;
        private long _nextPollAtMs;
        private int _frameIndex;
        private int _requestInFlight;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => "AI/HTTP " + _debugState + (string.IsNullOrWhiteSpace(_lastError) ? string.Empty : " err=" + _lastError);

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
        }

        public void CaptureFrame()
        {
            CacheReferences();
            long nowMs = NowMs();
            TryConsumePendingResponseJson(nowMs);

            if (ShouldDispatchPoll(nowMs))
            {
                DispatchSnapshot(nowMs, _frameIndex);
            }

            AiFrameAction latestAction = null;
            long receivedAtMs = 0L;
            lock (_stateLock)
            {
                latestAction = _latestAction;
                receivedAtMs = _lastActionReceivedAtMs;
            }

            bool hasFreshAction = latestAction != null && nowMs - receivedAtMs <= staleActionTimeoutMs;
            if (hasFreshAction)
            {
                _currentFrame = AiActionSanitizer.ToPlayerInputFrame(latestAction, _frameIndex);
                _lastAppliedFrame = _currentFrame;
                _debugState = "fresh";
            }
            else
            {
                switch (fallbackMode)
                {
                    case AiActionFallbackMode.HoldLastContinuous:
                        _currentFrame = AiActionSanitizer.ToContinuousFallback(_lastAppliedFrame, _frameIndex);
                        _debugState = "fallback-last";
                        break;
                    default:
                        _currentFrame = default;
                        _currentFrame.frame = _frameIndex;
                        _debugState = "fallback-neutral";
                        break;
                }
            }

            if (logFailures && !string.IsNullOrWhiteSpace(_lastError) && !string.Equals(_lastLoggedError, _lastError, StringComparison.Ordinal))
            {
                _lastLoggedError = _lastError;
                Debug.LogWarning("AiHttpPollingInputSource: " + _lastError, this);
            }

            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId slotId)
        {
            _frameIndex = 0;
            _lastAppliedFrame = default;
            _currentFrame = default;
            _latestAction = null;
            _lastActionReceivedAtMs = 0L;
            _nextPollAtMs = 0L;
            _debugState = slotId.ToDisplayName();
            _lastError = string.Empty;
            _lastLoggedError = string.Empty;
        }

        private void CacheReferences()
        {
            if (_player == null)
            {
                _player = GetComponent<PlayerController>();
            }

            if (matchController == null)
            {
                matchController = FindFirstObjectByType<MatchController>();
            }
        }

        private bool ShouldDispatchPoll(long nowMs)
        {
            if (_player == null || string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _requestInFlight, 0, 0) != 0)
            {
                return false;
            }

            return nowMs >= _nextPollAtMs;
        }

        private void DispatchSnapshot(long nowMs, int frameIndex)
        {
            AiCombatSnapshot snapshot = AiCombatSnapshotBuilder.Build(matchController, _player, frameIndex);
            AiAgentRequestEnvelope envelope = new AiAgentRequestEnvelope
            {
                sentAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                snapshot = snapshot,
            };

            string json = JsonUtility.ToJson(envelope);
            _nextPollAtMs = nowMs + Mathf.Max(10, pollingIntervalMs);
            Interlocked.Exchange(ref _requestInFlight, 1);

            Task.Run(async () =>
            {
                try
                {
                    using (CancellationTokenSource cancellation = new CancellationTokenSource(Mathf.Max(10, requestTimeoutMs)))
                    using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (HttpResponseMessage response = await SharedHttpClient.PostAsync(endpoint, content, cancellation.Token).ConfigureAwait(false))
                    {
                        string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            SetError("http_" + (int)response.StatusCode);
                            return;
                        }

                        lock (_stateLock)
                        {
                            _pendingResponseJson = responseJson;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    SetError("timeout");
                }
                catch (Exception exception)
                {
                    SetError(exception.GetType().Name);
                }
                finally
                {
                    Interlocked.Exchange(ref _requestInFlight, 0);
                }
            });
        }

        private void SetError(string error)
        {
            lock (_stateLock)
            {
                _lastError = error;
            }
        }

        private void TryConsumePendingResponseJson(long nowMs)
        {
            string responseJson;
            lock (_stateLock)
            {
                responseJson = _pendingResponseJson;
                _pendingResponseJson = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return;
            }

            AiAgentResponseEnvelope parsed = JsonUtility.FromJson<AiAgentResponseEnvelope>(responseJson);
            if (parsed == null || parsed.action == null)
            {
                SetError("parse_null");
                return;
            }

            lock (_stateLock)
            {
                _latestAction = parsed.action;
                _lastActionReceivedAtMs = nowMs;
                _lastError = string.Empty;
                _lastLoggedError = string.Empty;
            }
        }

        private static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
