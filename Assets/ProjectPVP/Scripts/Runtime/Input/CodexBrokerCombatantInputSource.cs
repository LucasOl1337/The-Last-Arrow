using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectPVP.Match;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectPVP.Input
{
    [DisallowMultipleComponent]
    public sealed class CodexBrokerCombatantInputSource : MonoBehaviour, ICombatantInputSource, IBotFeedbackInputSource, IBotCoachStatusInputSource
    {
        private const int MinimumSessionStartTimeoutMs = 3000;

        [Min(1)] public int slotId = 2;
        [Header("Combat Ranges")]
        public float desiredCombatDistance = 360f;
        public float closeRetreatDistance = 140f;
        public float meleeRange = 120f;
        public float ultimateRange = 180f;
        public float shootRange = 960f;
        public float verticalTolerance = 240f;

        [Header("Decision Timers")]
        public float shootInterval = 0.25f;
        public float meleeInterval = 0.45f;
        public float jumpInterval = 0.6f;
        public float dashInterval = 0.85f;
        public float ultimateInterval = 1.5f;

        [Header("Broker")]
        public string brokerBaseUrl = "http://127.0.0.1:8765";
        [Min(50)] public int minStrategyIntervalMs = 300;
        [Min(100)] public int maxStrategyAgeMs = 1500;
        [Min(50)] public int brokerRequestTimeoutMs = 150;
        [Min(600)] public int brokerSessionStartTimeoutMs = 3000;
        public bool autoStartSession = true;
        public bool keepSessionAliveAcrossRounds = true;
        public bool useAgentDrivenMode = true;

        private readonly AiArenaRuntimeSnapshotCollector _collector = new AiArenaRuntimeSnapshotCollector();
        private readonly Queue<string> _eventMemory = new Queue<string>(8);
        private readonly AiArenaMovementStallEscapeController _movementStallEscape = new AiArenaMovementStallEscapeController();
        private PlayerInputFrame _currentFrame;
        private AiArenaExecutionState _executionState;
        private int _frameIndex;
        private float _lastStrategyRequestTime = -999f;
        private float _lastIntentReceivedTime = -999f;
        private string _sessionId = string.Empty;
        private string _debugSummary = "AI | Codex pending";
        private CodexStrategyIntent _currentIntent;
        private AiArenaSnapshotEnvelope _previousSnapshot;
        private string _lastExecutorSource = "waiting_for_codex";
        private string _lastExecutorSummary = string.Empty;
        private string _botFeedback = string.Empty;
        private PlayerInputFrame _lastReportedFrame;
        private bool _hasAgentAction;
        private string _controllerOwner = string.Empty;
        private int _consecutiveBrokerFailures;
        private float _lastBrokerSuccessTime = -999f;
        private bool _manualForceRefresh;
        private CodexBrokerRequestLifecycleState _sessionStartRequest = CodexBrokerRequestLifecycleState.Inactive();
        private CodexBrokerRequestLifecycleState _strategyRequest = CodexBrokerRequestLifecycleState.Inactive();

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => _debugSummary;
        public string SessionId => _sessionId;
        public string LastExecutorSource => _lastExecutorSource;
        public string LastExecutorSummary => _lastExecutorSummary;
        public string BotFeedback => _botFeedback;
        public string BotControllerStatus => BuildBotControllerStatus();
        public string CurrentIntentMode => _currentIntent != null ? _currentIntent.mode : string.Empty;
        public string CurrentIntentReason => _currentIntent != null ? _currentIntent.reason : string.Empty;
        public bool HasAgentAction => _hasAgentAction;
        public string ControllerOwner => _controllerOwner;
        public bool IsSessionStarting => _sessionStartRequest.InFlight;
        public bool IsStrategyRequestInFlight => _strategyRequest.InFlight;
        public bool HasLiveSession => !string.IsNullOrWhiteSpace(_sessionId);
        public bool ManualForceRefreshPending => _manualForceRefresh;
        public float IntentAgeMs => _currentIntent == null || _lastIntentReceivedTime < 0f
            ? -1f
            : (Time.realtimeSinceStartup - _lastIntentReceivedTime) * 1000f;

        private string BuildBotControllerStatus()
        {
            string owner = string.IsNullOrWhiteSpace(_controllerOwner)
                ? _lastExecutorSource
                : _controllerOwner;
            string mode = _currentIntent != null && !string.IsNullOrWhiteSpace(_currentIntent.mode)
                ? _currentIntent.mode.Trim()
                : (_hasAgentAction ? "agent" : "fallback");
            string session = string.IsNullOrWhiteSpace(_sessionId)
                ? "no-session"
                : (_sessionStartRequest.InFlight || _strategyRequest.InFlight ? "requesting" : "live");
            return CompactBotStatusToken(owner) + "/" + CompactBotStatusToken(mode) + "/" + session;
        }

        private static string CompactBotStatusToken(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "unknown"
                : value.Trim().Replace(' ', '_');
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PrewarmSession();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(_sessionId) || keepSessionAliveAcrossRounds)
            {
                return;
            }

            StartCoroutine(SendStopRequest(_sessionId));
        }

        public void CaptureFrame()
        {
            RecoverStaleBrokerRequests();
            float deltaTime = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : Time.deltaTime;
            _collector.Tick(deltaTime);
            AiArenaFrameExecutor.Tick(ref _executionState, deltaTime);
            _collector.RefreshControllersIfNeeded();

            AiArenaControllerSnapshot self = _collector.ResolveSelfSnapshot(gameObject, slotId);
            AiArenaControllerSnapshot opponent = _collector.ResolveClosestOpponentSnapshot(self);
            AiArenaArenaSnapshot arena = _collector.ResolveArenaSnapshot();
            List<AiArenaProjectileSnapshot> projectiles = _collector.ResolveProjectileSnapshots(self);
            AiArenaSnapshotEnvelope snapshot = AiArenaSnapshotBuilder.Build(
                self,
                opponent,
                projectiles,
                arena,
                _frameIndex,
                desiredCombatDistance,
                closeRetreatDistance,
                meleeRange,
                ultimateRange,
                shootRange,
                verticalTolerance);

            CodexPromptState promptState = BuildPromptState(snapshot);
            if (keepSessionAliveAcrossRounds
                && !string.IsNullOrWhiteSpace(_sessionId)
                && snapshot != null
                && snapshot.arena != null
                && snapshot.arena.roundResetPending
                && (_previousSnapshot == null || _previousSnapshot.arena == null || !_previousSnapshot.arena.roundResetPending))
            {
                StartCoroutine(SendResetRequest(_sessionId, "round_reset"));
            }

            EnsureSessionStarted(snapshot, promptState);
            if (useAgentDrivenMode)
            {
                PushAgentStateIfNeeded(snapshot, promptState);
            }
            else
            {
                RequestStrategyIfNeeded(snapshot, promptState);
            }

            AiArenaDecisionEnvelope decision = ResolveDecision(snapshot);

            _currentFrame = AiArenaFrameExecutor.BuildFrame(
                ref _executionState,
                self,
                snapshot,
                decision,
                _frameIndex,
                shootInterval,
                meleeInterval,
                jumpInterval,
                dashInterval,
                ultimateInterval,
                ref _debugSummary);
            _botFeedback = DecorateBotFeedbackForExecutorSource(
                _lastExecutorSource,
                AiArenaBotFeedbackBuilder.Build(snapshot, _debugSummary, _currentFrame));
            _lastExecutorSummary = _debugSummary;
            _currentFrame = ObserveMovementStall(snapshot, _currentFrame);
            _currentFrame = ApplyRecoverArrowFeedbackFailsafe(snapshot, _currentFrame);
            _currentFrame = ApplyLastArrowCommitFailsafe(snapshot, _currentFrame);
            _currentFrame = ApplyAntiAirShotFailsafe(snapshot, _currentFrame);
            _lastReportedFrame = _currentFrame;
            _previousSnapshot = snapshot;
            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId configuredSlotId)
        {
            int previousSlotId = slotId;
            slotId = Mathf.Max(1, configuredSlotId.ToInt());
            if (slotId != previousSlotId)
            {
                _sessionId = string.Empty;
            }

            _frameIndex = 0;
            _executionState = default;
            _previousSnapshot = null;
            _lastStrategyRequestTime = -999f;
            _lastIntentReceivedTime = -999f;
            _currentIntent = null;
            _lastReportedFrame = default;
            _hasAgentAction = false;
            _controllerOwner = string.Empty;
            _lastExecutorSource = "waiting_for_codex";
            _lastExecutorSummary = string.Empty;
            _botFeedback = string.Empty;
            _debugSummary = "AI | Codex pending";
            _consecutiveBrokerFailures = 0;
            _lastBrokerSuccessTime = -999f;
            _manualForceRefresh = false;
            ResetMovementStall();
            CodexBrokerRequestLifecycle.Invalidate(ref _sessionStartRequest);
            CodexBrokerRequestLifecycle.Invalidate(ref _strategyRequest);
            _collector.ForceRefresh();
            _eventMemory.Clear();
        }

        public void ResetInputState()
        {
            _currentFrame = default;
            _lastReportedFrame = default;
            _executionState = default;
            _frameIndex = 0;
            _hasAgentAction = false;
            _currentIntent = null;
            _lastExecutorSource = "waiting_for_codex";
            _lastExecutorSummary = string.Empty;
            _botFeedback = string.Empty;
            _controllerOwner = string.Empty;
            _debugSummary = "AI | Codex pending";
            _manualForceRefresh = false;
            ResetMovementStall();
        }

        public void RequestImmediateReplan(string reason = "debug_hud")
        {
            string normalizedReason = NormalizeDebugReason(reason);
            _manualForceRefresh = true;
            _lastStrategyRequestTime = -999f;
            _debugSummary = "AI | Manual replan:" + normalizedReason;
            _lastExecutorSummary = _debugSummary;
            _botFeedback = "manual replan requested; improve: reassess current fight state.";
        }

        public void RestartBrokerSession(string reason = "debug_hud")
        {
            RestartBrokerSession(reason, useAgentDrivenMode);
        }

        private void RestartBrokerSession(string reason, bool stopUsingAgentDrivenMode)
        {
            string sessionToStop = _sessionId;
            string normalizedReason = NormalizeDebugReason(reason);
            if (Application.isPlaying && !string.IsNullOrWhiteSpace(sessionToStop))
            {
                StartCoroutine(SendStopRequest(sessionToStop, stopUsingAgentDrivenMode));
            }

            InvalidateBrokerSession();
            _manualForceRefresh = true;
            _lastStrategyRequestTime = -999f;
            _debugSummary = "AI | Broker restart:" + normalizedReason;
            _lastExecutorSummary = _debugSummary;
            _botFeedback = "broker session restarted; improve: rebuild live context before next attack.";
        }

        public void SetAgentDrivenMode(bool enabled)
        {
            if (useAgentDrivenMode == enabled)
            {
                return;
            }

            bool previousAgentDrivenMode = useAgentDrivenMode;
            useAgentDrivenMode = enabled;
            RestartBrokerSession("agent mode changed", previousAgentDrivenMode);
            _debugSummary = "AI | Agent mode " + (enabled ? "on" : "off");
            _lastExecutorSummary = _debugSummary;
            _botFeedback = "agent mode changed; improve: rebuild broker session for the selected control path.";
        }

        public void PrewarmSession()
        {
            if (!Application.isPlaying || !autoStartSession)
            {
                return;
            }

            Debug.Log($"[CodexBot] CodexBrokerCombatantInputSource.PrewarmSession slot {slotId}.");
            AiArenaSnapshotEnvelope snapshot = TryBuildSnapshot();
            EnsureSessionStarted(snapshot, BuildPromptState(snapshot));
        }

        private bool HasFreshIntent()
        {
            if (_currentIntent == null)
            {
                return false;
            }

            if (!_hasAgentAction)
            {
                return false;
            }

            float ageMs = (Time.realtimeSinceStartup - _lastIntentReceivedTime) * 1000f;
            return ageMs <= ResolveFreshIntentWindowMs();
        }

        private bool HasReusableIntent()
        {
            if (_currentIntent == null)
            {
                return false;
            }

            if (!_hasAgentAction)
            {
                return false;
            }

            float ageMs = IntentAgeMs;
            if (ageMs < 0f)
            {
                return false;
            }

            float reusableWindowMs = Mathf.Min(maxStrategyAgeMs, ResolveFreshIntentWindowMs() * 1.5f);
            return ageMs <= reusableWindowMs;
        }

        private float ResolveFreshIntentWindowMs()
        {
            if (_currentIntent == null)
            {
                return 0f;
            }

            return Mathf.Min(maxStrategyAgeMs, Mathf.Max(0f, _currentIntent.expiresInMs));
        }

        private AiArenaDecisionEnvelope ResolveDecision(AiArenaSnapshotEnvelope snapshot)
        {
            if (HasFreshIntent())
            {
                _lastExecutorSource = "codex_live";
                return AiArenaStrategicPolicy.Decide(snapshot, _currentIntent);
            }

            if (HasReusableIntent())
            {
                _lastExecutorSource = "codex_stale";
                return AiArenaStrategicPolicy.Decide(snapshot, _currentIntent);
            }

            _lastExecutorSource = "heuristic_fallback";
            return AiArenaHeuristicPolicy.Decide(snapshot);
        }

        private void EnsureSessionStarted(AiArenaSnapshotEnvelope snapshot, CodexPromptState promptState)
        {
            if (!autoStartSession || _sessionStartRequest.InFlight || !string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            int requestVersion = BeginSessionStartRequest();
            if (slotId == 2)
            {
                Debug.Log($"[CodexBot] Starting broker session for slot {slotId} at frame {_frameIndex}.");
            }

            var request = new CodexBrokerSessionStartRequest
            {
                slotId = slotId,
                promptState = promptState,
                executorFeedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                    _lastExecutorSource,
                    _lastExecutorSummary,
                    _currentIntent,
                    snapshot,
                    IntentAgeMs,
                    BuildReportedInput(_lastReportedFrame)),
            };
            StartCoroutine(SendJsonRequest(
                useAgentDrivenMode ? "/agent/session/start" : "/session/start",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    if (!TryCompleteSessionStartRequest(requestVersion))
                    {
                        return;
                    }

                    if (slotId == 2)
                    {
                        Debug.Log($"[CodexBot] Broker session start response received for slot {slotId}.");
                    }

                    ApplyBrokerEnvelope(responseJson);
                },
                (responseCode, responseBody) =>
                {
                    if (!TryCompleteSessionStartRequest(requestVersion))
                    {
                        return;
                    }

                    _lastExecutorSummary = "AI | Broker session start failed";
                    if (slotId == 2)
                    {
                        Debug.LogWarning($"[CodexBot] Broker session start failed for slot {slotId}.");
                    }
                }));
        }

        private void PushAgentStateIfNeeded(AiArenaSnapshotEnvelope snapshot, CodexPromptState promptState)
        {
            if (_sessionStartRequest.InFlight || _strategyRequest.InFlight || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            bool forceRefresh = ShouldForceRefresh(snapshot);
            float elapsedMs = (Time.realtimeSinceStartup - _lastStrategyRequestTime) * 1000f;
            if (!forceRefresh && elapsedMs < minStrategyIntervalMs)
            {
                return;
            }

            int requestVersion = BeginStrategyRequest();
            _manualForceRefresh = false;
            var request = new CodexAgentStateUpdateRequest
            {
                sessionId = _sessionId,
                slotId = slotId,
                frame = _frameIndex,
                forceRefresh = forceRefresh,
                promptState = promptState,
                executorFeedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                    _lastExecutorSource,
                    _lastExecutorSummary,
                    _currentIntent,
                    snapshot,
                    IntentAgeMs,
                    BuildReportedInput(_lastReportedFrame),
                    _previousSnapshot),
            };

            StartCoroutine(SendJsonRequest(
                "/agent/state",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    if (!TryCompleteStrategyRequest(requestVersion))
                    {
                        return;
                    }

                    if (slotId == 2)
                    {
                        Debug.Log($"[CodexBot] Broker strategy response received for slot {slotId}.");
                    }

                    ApplyBrokerEnvelope(responseJson);
                },
                (responseCode, responseBody) =>
                {
                    if (!TryCompleteStrategyRequest(requestVersion))
                    {
                        return;
                    }

                    HandleBrokerRequestFailure(responseCode, responseBody);
                    if (slotId == 2)
                    {
                        Debug.LogWarning($"[CodexBot] Broker strategy request failed for slot {slotId}.");
                    }
                }));
        }

        private AiArenaSnapshotEnvelope TryBuildSnapshot()
        {
            _collector.RefreshControllersIfNeeded();
            AiArenaControllerSnapshot self = _collector.ResolveSelfSnapshot(gameObject, slotId);
            AiArenaControllerSnapshot opponent = _collector.ResolveClosestOpponentSnapshot(self);
            AiArenaArenaSnapshot arena = _collector.ResolveArenaSnapshot();
            List<AiArenaProjectileSnapshot> projectiles = _collector.ResolveProjectileSnapshots(self);
            return AiArenaSnapshotBuilder.Build(
                self,
                opponent,
                projectiles,
                arena,
                _frameIndex,
                desiredCombatDistance,
                closeRetreatDistance,
                meleeRange,
                ultimateRange,
                shootRange,
                verticalTolerance);
        }

        private void RequestStrategyIfNeeded(AiArenaSnapshotEnvelope snapshot, CodexPromptState promptState)
        {
            if (_sessionStartRequest.InFlight || _strategyRequest.InFlight || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            bool forceRefresh = ShouldForceRefresh(snapshot);
            float elapsedMs = (Time.realtimeSinceStartup - _lastStrategyRequestTime) * 1000f;
            if (!forceRefresh && elapsedMs < minStrategyIntervalMs)
            {
                return;
            }

            int requestVersion = BeginStrategyRequest();
            _manualForceRefresh = false;
            var request = new CodexBrokerStrategyTickRequest
            {
                sessionId = _sessionId,
                slotId = slotId,
                frame = _frameIndex,
                forceRefresh = forceRefresh,
                promptState = promptState,
                executorFeedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                    _lastExecutorSource,
                    _lastExecutorSummary,
                    _currentIntent,
                    snapshot,
                    IntentAgeMs,
                    BuildReportedInput(_lastReportedFrame),
                    _previousSnapshot),
            };

            StartCoroutine(SendJsonRequest(
                "/strategy/tick",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    if (!TryCompleteStrategyRequest(requestVersion))
                    {
                        return;
                    }

                    ApplyBrokerEnvelope(responseJson);
                },
                (responseCode, responseBody) =>
                {
                    if (!TryCompleteStrategyRequest(requestVersion))
                    {
                        return;
                    }

                    HandleBrokerRequestFailure(responseCode, responseBody);
                }));
        }

        private void RecoverStaleBrokerRequests()
        {
            float now = Time.realtimeSinceStartup;
            float sessionStartStaleWindowMs = Mathf.Max(brokerSessionStartTimeoutMs * 2f, 4000f);
            float strategyStaleWindowMs = Mathf.Max(brokerRequestTimeoutMs * 4f, 2000f);

            if (CodexBrokerRequestLifecycle.IsStale(_sessionStartRequest, now, sessionStartStaleWindowMs))
            {
                InvalidateSessionStartRequest("AI | Session start watchdog recovered");
            }

            if (CodexBrokerRequestLifecycle.IsStale(_strategyRequest, now, strategyStaleWindowMs))
            {
                InvalidateStrategyRequest();
                HandleBrokerRequestFailure();
            }
        }

        private int BeginSessionStartRequest()
        {
            return CodexBrokerRequestLifecycle.Begin(ref _sessionStartRequest, Time.realtimeSinceStartup);
        }

        private bool TryCompleteSessionStartRequest(int requestVersion)
        {
            return CodexBrokerRequestLifecycle.TryComplete(ref _sessionStartRequest, requestVersion);
        }

        private void InvalidateSessionStartRequest(string executorSummary)
        {
            CodexBrokerRequestLifecycle.Invalidate(ref _sessionStartRequest);
            _lastExecutorSummary = executorSummary;
        }

        private int BeginStrategyRequest()
        {
            _lastStrategyRequestTime = Time.realtimeSinceStartup;
            return CodexBrokerRequestLifecycle.Begin(ref _strategyRequest, _lastStrategyRequestTime);
        }

        private bool TryCompleteStrategyRequest(int requestVersion)
        {
            return CodexBrokerRequestLifecycle.TryComplete(ref _strategyRequest, requestVersion);
        }

        private static bool IsCurrentRequestVersion(int requestVersion, int currentVersion)
        {
            return CodexBrokerRequestLifecycle.IsCurrentVersion(requestVersion, currentVersion);
        }

        private void InvalidateStrategyRequest()
        {
            CodexBrokerRequestLifecycle.Invalidate(ref _strategyRequest);
        }

        private bool ShouldForceRefresh(AiArenaSnapshotEnvelope snapshot)
        {
            if (_manualForceRefresh)
            {
                return true;
            }

            if (snapshot == null || snapshot.semantics == null)
            {
                return false;
            }

            if (_previousSnapshot == null || _previousSnapshot.semantics == null || _previousSnapshot.arena == null)
            {
                return true;
            }

            AiArenaSemanticObservation previous = _previousSnapshot.semantics;
            AiArenaSemanticObservation current = snapshot.semantics;
            if (_previousSnapshot.arena.roundResetPending != snapshot.arena.roundResetPending)
            {
                return true;
            }

            return current.incomingProjectileThreat != previous.incomingProjectileThreat
                || current.selfCornered != previous.selfCornered
                || current.targetCornered != previous.targetCornered
                || current.targetUsingUltimate != previous.targetUsingUltimate
                || current.targetUsingMelee != previous.targetUsingMelee
                || current.targetUsingRanged != previous.targetUsingRanged
                || current.hasTarget != previous.hasTarget
                || current.shouldPunish != previous.shouldPunish
                || current.targetVulnerable != previous.targetVulnerable
                || CountRecoverableProjectiles(snapshot) != CountRecoverableProjectiles(_previousSnapshot)
                || HasMeaningfulRecoverableDistanceChange(snapshot, _previousSnapshot);
        }

        private CodexPromptState BuildPromptState(AiArenaSnapshotEnvelope snapshot)
        {
            CodexPromptState promptState = CodexPromptStateBuilder.Build(
                snapshot,
                _previousSnapshot,
                snapshot != null ? snapshot.frame : _frameIndex,
                _eventMemory);
            RecordPromptEvents(promptState.events);
            return promptState;
        }

        private static int CountRecoverableProjectiles(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null || snapshot.projectiles == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < snapshot.projectiles.Count; index += 1)
            {
                AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                if (projectile == null || !projectile.isCollectible)
                {
                    continue;
                }

                count += 1;
            }

            return count;
        }

        private static bool HasMeaningfulRecoverableDistanceChange(AiArenaSnapshotEnvelope current, AiArenaSnapshotEnvelope previous)
        {
            float currentDistance = ResolveNearestRecoverableProjectileDistance(current);
            float previousDistance = ResolveNearestRecoverableProjectileDistance(previous);

            if (currentDistance < 0f || previousDistance < 0f)
            {
                return currentDistance != previousDistance;
            }

            return Mathf.Abs(currentDistance - previousDistance) >= 24f;
        }

        private static float ResolveNearestRecoverableProjectileDistance(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null || snapshot.self == null || snapshot.projectiles == null)
            {
                return -1f;
            }

            float bestDistance = float.MaxValue;
            for (int index = 0; index < snapshot.projectiles.Count; index += 1)
            {
                AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                if (projectile == null || !projectile.isCollectible)
                {
                    continue;
                }

                float distance = Vector2.Distance(snapshot.self.position, projectile.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
            }

            return bestDistance == float.MaxValue ? -1f : bestDistance;
        }

        private void RecordPromptEvents(List<string> events)
        {
            if (events == null || events.Count == 0)
            {
                return;
            }

            for (int index = 0; index < events.Count; index += 1)
            {
                string value = events[index];
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                _eventMemory.Enqueue(value);
                while (_eventMemory.Count > 5)
                {
                    _eventMemory.Dequeue();
                }
            }
        }

        private PlayerInputFrame ObserveMovementStall(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            PlayerInputFrame resolvedFrame = _movementStallEscape.Observe(snapshot, frame);
            if (_movementStallEscape.EscapedThisFrame)
            {
                _debugSummary = "AI | Movement stalled";
                _lastExecutorSummary = _debugSummary;
                _botFeedback = "movement stalled; action: escape jump/dash; improve: replan path instead of holding one axis.";
            }

            if (_movementStallEscape.TriggeredThisFrame)
            {
                _manualForceRefresh = true;
                _lastStrategyRequestTime = -999f;
                RecordPromptEvents(new List<string> { "movement_stalled" });
            }

            return resolvedFrame;
        }

        private PlayerInputFrame ApplyRecoverArrowFeedbackFailsafe(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            if (!IsRecoverArrowFeedbackIntent(_currentIntent)
                || snapshot == null
                || snapshot.self == null
                || snapshot.semantics == null
                || !snapshot.semantics.shouldCollectProjectile
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingUltimate)
            {
                return frame;
            }

            bool stalledRecoveryCommit = IsRecoverArrowStalledCommitIntent(_currentIntent);
            bool alreadyMoving = Mathf.Abs(frame.axis) > 0.1f
                || frame.jumpPressed
                || frame.jumpHeld
                || frame.dashPrimaryPressed
                || frame.dashSecondaryPressed;
            if (alreadyMoving && !stalledRecoveryCommit)
            {
                return frame;
            }

            bool canDash = snapshot.self.dashCooldownLeft <= 0.01f && !snapshot.self.isDashing;
            float axis = AiArenaHeuristicPolicy.ResolveCollectionMoveAxis(
                snapshot.semantics.collectibleProjectileDirection,
                snapshot.self);
            bool jump = AiArenaHeuristicPolicy.ShouldJumpForCollectible(
                snapshot.semantics.collectibleProjectileDirection,
                snapshot.self);
            bool dash = AiArenaHeuristicPolicy.ShouldDashForAirborneCollectible(
                snapshot.semantics.collectibleProjectileDirection,
                snapshot.self,
                canDash);

            if (Mathf.Abs(axis) <= 0.1f && !jump && !dash)
            {
                return frame;
            }

            frame.axis = Mathf.Clamp(axis, -1f, 1f);
            frame.left = frame.axis < -0.1f;
            frame.right = frame.axis > 0.1f;
            frame.up = jump;
            frame.down = false;
            frame.jumpPressed = jump;
            frame.jumpHeld = jump;
            frame.dashPrimaryPressed = dash;
            frame.dashSecondaryPressed = false;
            frame.shootPressed = false;
            frame.shootHeld = false;
            frame.meleePressed = false;
            frame.ultimatePressed = false;
            _debugSummary = "AI COLLECT ARROW";
            _lastExecutorSummary = _debugSummary;
            _botFeedback = DecorateBotFeedbackForExecutorSource(
                _lastExecutorSource,
                AiArenaBotFeedbackBuilder.Build(snapshot, _debugSummary, frame));
            return frame;
        }

        private PlayerInputFrame ApplyLastArrowCommitFailsafe(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            if (!IsLastArrowCommitIntent(_currentIntent)
                || snapshot == null
                || snapshot.self == null
                || snapshot.semantics == null
                || snapshot.opponents == null
                || snapshot.opponents.Count == 0
                || snapshot.opponents[0] == null
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingUltimate)
            {
                return frame;
            }

            AiArenaCombatantObservation target = snapshot.opponents[0];
            if (snapshot.self.arrows <= 0 || target.arrows > 0)
            {
                return frame;
            }

            bool alreadyCommitted = frame.shootPressed
                || frame.shootHeld
                || frame.meleePressed
                || frame.ultimatePressed
                || frame.dashPrimaryPressed
                || frame.dashSecondaryPressed;
            if (alreadyCommitted)
            {
                return frame;
            }

            bool targetCanStopProjectile = !target.isDead && (target.canParryProjectile || target.canBlockProjectiles);
            bool canShoot = snapshot.self.shootCooldownLeft <= 0.01f && !targetCanStopProjectile;
            bool canDash = snapshot.self.dashCooldownLeft <= 0.01f && !snapshot.self.isDashing;
            float towardTarget = snapshot.semantics.targetDirection.x >= 0f ? 1f : -1f;

            frame.axis = towardTarget;
            frame.left = frame.axis < -0.1f;
            frame.right = frame.axis > 0.1f;
            frame.down = false;
            if (canShoot && snapshot.semantics.targetInShootRange)
            {
                frame.shootPressed = true;
                frame.shootHeld = true;
                frame.axis = Mathf.Clamp(towardTarget * 0.25f, -1f, 1f);
                frame.left = frame.axis < -0.1f;
                frame.right = frame.axis > 0.1f;
            }
            else if (canDash && snapshot.semantics.horizontalDistance > 120f)
            {
                frame.dashPrimaryPressed = true;
            }

            if (snapshot.self.isGrounded && snapshot.semantics.targetAbove)
            {
                frame.up = true;
                frame.jumpPressed = true;
                frame.jumpHeld = true;
            }

            _debugSummary = "AI LAST ARROW COMMIT";
            _lastExecutorSummary = _debugSummary;
            _botFeedback = DecorateBotFeedbackForExecutorSource(
                _lastExecutorSource,
                AiArenaBotFeedbackBuilder.Build(snapshot, _debugSummary, frame));
            return frame;
        }

        private PlayerInputFrame ApplyAntiAirShotFailsafe(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            if (!IsAntiAirShotIntent(_currentIntent)
                || snapshot == null
                || snapshot.self == null
                || snapshot.semantics == null
                || snapshot.opponents == null
                || snapshot.opponents.Count == 0
                || snapshot.opponents[0] == null
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingUltimate
                || !snapshot.semantics.hasTarget
                || !snapshot.semantics.targetAbove
                || !snapshot.semantics.targetInShootRange)
            {
                return frame;
            }

            AiArenaCombatantObservation target = snapshot.opponents[0];
            bool targetCanStopProjectile = !target.isDead && (target.canParryProjectile || target.canBlockProjectiles);
            bool canShoot = snapshot.self.arrows > 0
                && snapshot.self.shootCooldownLeft <= 0.01f
                && !targetCanStopProjectile;
            if (!canShoot || HasCommittedAction(frame))
            {
                return frame;
            }

            float towardTarget = snapshot.semantics.targetDirection.x >= 0f ? 1f : -1f;
            Vector2 aim = frame.aim.sqrMagnitude > 0.001f
                ? frame.aim.normalized
                : snapshot.semantics.targetDirection.normalized;
            if (aim.sqrMagnitude <= 0.001f)
            {
                aim = new Vector2(towardTarget, 0.45f).normalized;
            }
            else if (aim.y < 0.28f)
            {
                aim = new Vector2(Mathf.Sign(aim.x == 0f ? towardTarget : aim.x), 0.45f).normalized;
            }

            frame.aim = aim;
            frame.axis = Mathf.Clamp(towardTarget * 0.25f, -1f, 1f);
            frame.left = frame.axis < -0.1f;
            frame.right = frame.axis > 0.1f;
            frame.down = false;
            frame.shootPressed = true;
            frame.shootHeld = true;
            frame.meleePressed = false;
            frame.ultimatePressed = false;
            frame.dashPrimaryPressed = false;
            frame.dashSecondaryPressed = false;
            _debugSummary = "AI ANTI AIR";
            _lastExecutorSummary = _debugSummary;
            _botFeedback = DecorateBotFeedbackForExecutorSource(
                _lastExecutorSource,
                AiArenaBotFeedbackBuilder.Build(snapshot, _debugSummary, frame));
            return frame;
        }

        private void ResetMovementStall()
        {
            _movementStallEscape.Reset();
        }

        private static bool HasCommittedAction(PlayerInputFrame frame)
        {
            return frame.shootPressed
                || frame.shootHeld
                || frame.meleePressed
                || frame.ultimatePressed
                || frame.dashPrimaryPressed
                || frame.dashSecondaryPressed;
        }

        private static bool IsRecoverArrowFeedbackIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            string normalized = intent.reason.Trim().ToLowerInvariant();
            return normalized.Contains("recover_arrow_feedback")
                || normalized.Contains("recover_missed_arrow")
                || normalized.Contains("recover_arrow_after_empty_shot");
        }

        private static bool IsRecoverArrowStalledCommitIntent(CodexStrategyIntent intent)
        {
            return intent != null
                && !string.IsNullOrWhiteSpace(intent.reason)
                && intent.reason.Trim().ToLowerInvariant().Contains("recover_arrow_feedback_stalled_commit");
        }

        private static bool IsLastArrowCommitIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            return intent.reason.Trim().ToLowerInvariant().Contains("last_arrow_stalled_commit");
        }

        private static bool IsAntiAirShotIntent(CodexStrategyIntent intent)
        {
            if (intent == null)
            {
                return false;
            }

            if (intent.antiAir)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            string normalized = intent.reason.Trim().ToLowerInvariant();
            return normalized.Contains("anti_air_shot_active")
                || normalized.Contains("anti_air_opportunity")
                || normalized.Contains("missed_anti_air")
                || normalized.Contains("last_arrow_pressure")
                || normalized.Contains("last_arrow_stalled_commit");
        }

        private void ApplyBrokerEnvelope(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return;
            }

            CodexBrokerIntentEnvelope envelope = null;
            try
            {
                envelope = JsonUtility.FromJson<CodexBrokerIntentEnvelope>(responseJson);
            }
            catch
            {
                envelope = null;
            }

            if (envelope == null)
            {
                return;
            }

            CodexBrokerEnvelopeState envelopeState = CodexBrokerEnvelopeStateMapper.Build(
                envelope,
                _sessionId,
                useAgentDrivenMode);

            _sessionId = envelopeState.sessionId;
            _consecutiveBrokerFailures = 0;
            _lastBrokerSuccessTime = Time.realtimeSinceStartup;
            _hasAgentAction = envelopeState.hasExecutableIntent;
            _controllerOwner = envelopeState.controllerOwner;

            if (!envelopeState.hasIntent)
            {
                return;
            }

            _currentIntent = envelopeState.intent;
            _lastIntentReceivedTime = Time.realtimeSinceStartup;
        }

        private void HandleBrokerRequestFailure()
        {
            HandleBrokerRequestFailure(0L, string.Empty);
        }

        private void HandleBrokerRequestFailure(long responseCode, string responseBody)
        {
            _consecutiveBrokerFailures += 1;
            if (IsUnknownBrokerSessionFailure(responseCode, responseBody))
            {
                InvalidateBrokerSession();
                _lastExecutorSource = "unknown_broker_session";
                _lastExecutorSummary = "AI | Broker session expired";
                _botFeedback = "broker session expired; improve: start a fresh Codex broker session before reusing intent.";
                return;
            }

            _lastExecutorSource = "broker_retrying";
            _lastExecutorSummary = "AI | Broker retrying";
            _botFeedback = DecorateBotFeedbackForExecutorSource(
                _lastExecutorSource,
                "waiting for broker response; improve: keep local defense active while reconnecting.");

            if (!CodexBrokerFailurePolicy.ShouldInvalidateSession(
                _sessionId,
                _consecutiveBrokerFailures,
                _lastBrokerSuccessTime,
                Time.realtimeSinceStartup))
            {
                return;
            }

            InvalidateBrokerSession();
        }

        private void InvalidateBrokerSession()
        {
            CodexBrokerRequestLifecycle.Invalidate(ref _sessionStartRequest);
            CodexBrokerRequestLifecycle.Invalidate(ref _strategyRequest);
            _sessionId = string.Empty;
            _currentIntent = null;
            _hasAgentAction = false;
            _controllerOwner = string.Empty;
            _lastIntentReceivedTime = -999f;
            _lastExecutorSource = "waiting_for_codex";
            _lastExecutorSummary = "AI | Broker disconnected";
            _botFeedback = "broker disconnected; improve: verify broker process and network path.";
            _consecutiveBrokerFailures = 0;
            _lastBrokerSuccessTime = -999f;
            _manualForceRefresh = false;
        }

        private static bool IsUnknownBrokerSessionFailure(long responseCode, string responseBody)
        {
            if (responseCode != 404L)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return false;
            }

            return responseBody.Contains("unknown_agent_session") || responseBody.Contains("unknown_session");
        }

        private static string NormalizeDebugReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim();
        }

        private static string DecorateBotFeedbackForExecutorSource(string executorSource, string feedback)
        {
            string normalizedFeedback = string.IsNullOrWhiteSpace(feedback)
                ? "waiting for bot feedback."
                : feedback.Trim();
            string normalizedSource = string.IsNullOrWhiteSpace(executorSource)
                ? string.Empty
                : executorSource.Trim();

            return normalizedSource switch
            {
                "broker_retrying" => "broker retrying; control: keep local fallback active while broker recovers; " + normalizedFeedback,
                "codex_stale" => "codex stale; control: force replan if this repeats; " + normalizedFeedback,
                "heuristic_fallback" => "heuristic fallback; control: restore broker or wait for agent intent; " + normalizedFeedback,
                _ => normalizedFeedback,
            };
        }

        private static CodexReportedInputFrame BuildReportedInput(PlayerInputFrame frame)
        {
            return new CodexReportedInputFrame
            {
                frame = frame.frame,
                axis = frame.axis,
                aim = frame.aim,
                jumpPressed = frame.jumpPressed,
                jumpHeld = frame.jumpHeld,
                shootPressed = frame.shootPressed,
                shootHeld = frame.shootHeld,
                meleePressed = frame.meleePressed,
                ultimatePressed = frame.ultimatePressed,
                dashPrimaryPressed = frame.dashPrimaryPressed,
                dashSecondaryPressed = frame.dashSecondaryPressed,
            };
        }

        private IEnumerator SendStopRequest(string sessionId)
        {
            return SendStopRequest(sessionId, useAgentDrivenMode);
        }

        private IEnumerator SendStopRequest(string sessionId, bool agentDrivenMode)
        {
            var request = new CodexBrokerSessionStopRequest
            {
                sessionId = sessionId,
                slotId = slotId,
            };
            yield return SendJsonRequest(agentDrivenMode ? "/agent/session/stop" : "/session/stop", JsonUtility.ToJson(request), null, null);
        }

        private IEnumerator SendResetRequest(string sessionId, string reason)
        {
            var request = new CodexBrokerSessionResetRequest
            {
                sessionId = sessionId,
                slotId = slotId,
                reason = reason,
            };
            yield return SendJsonRequest(useAgentDrivenMode ? "/agent/session/reset" : "/session/reset", JsonUtility.ToJson(request), null, null);
        }

        private IEnumerator SendJsonRequest(string path, string payloadJson, System.Action<string> onSuccess, System.Action<long, string> onFailure)
        {
            string url = brokerBaseUrl.TrimEnd('/') + path;
            byte[] body = Encoding.UTF8.GetBytes(payloadJson ?? "{}");
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            float startedAt = Time.realtimeSinceStartup;
            int effectiveTimeoutMs = ResolveBrokerRequestTimeoutMs(path, brokerRequestTimeoutMs, brokerSessionStartTimeoutMs);
            while (!operation.isDone)
            {
                if ((Time.realtimeSinceStartup - startedAt) * 1000f > effectiveTimeoutMs)
                {
                    if (slotId == 2)
                    {
                        Debug.LogWarning($"[CodexBot] Broker request timeout path={path} elapsedMs={(Time.realtimeSinceStartup - startedAt) * 1000f:F0} timeoutMs={effectiveTimeoutMs}");
                    }
                    request.Abort();
                    break;
                }

                yield return null;
            }

            bool success = request.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(request.downloadHandler.text);
            if (success)
            {
                if (slotId == 2)
                {
                    Debug.Log($"[CodexBot] Broker request success path={path} code={request.responseCode} bytes={(request.downloadHandler.text != null ? request.downloadHandler.text.Length : 0)}");
                }
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                if (slotId == 2)
                {
                    Debug.LogWarning($"[CodexBot] Broker request failed path={path} result={request.result} code={request.responseCode} error={request.error} bytes={(request.downloadHandler.text != null ? request.downloadHandler.text.Length : 0)}");
                }
                onFailure?.Invoke(request.responseCode, request.downloadHandler.text);
            }

            request.Dispose();
        }

        private static int ResolveBrokerRequestTimeoutMs(string path, int requestTimeoutMs, int sessionStartTimeoutMs)
        {
            int minimumRequestTimeoutMs = Mathf.Max(requestTimeoutMs, 600);
            if (path == "/agent/session/start" || path == "/session/start")
            {
                return Mathf.Max(sessionStartTimeoutMs, MinimumSessionStartTimeoutMs, minimumRequestTimeoutMs);
            }

            return minimumRequestTimeoutMs;
        }
    }
}
