using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectPVP.Match;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectPVP.Input
{
    [DisallowMultipleComponent]
    public sealed class CodexBrokerCombatantInputSource : MonoBehaviour, ICombatantInputSource
    {
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
        public bool autoStartSession = true;
        public bool keepSessionAliveAcrossRounds = true;
        public bool useAgentDrivenMode = true;

        private readonly AiArenaRuntimeSnapshotCollector _collector = new AiArenaRuntimeSnapshotCollector();
        private readonly Queue<string> _eventMemory = new Queue<string>(8);
        private PlayerInputFrame _currentFrame;
        private AiArenaExecutionState _executionState;
        private int _frameIndex;
        private float _lastStrategyRequestTime = -999f;
        private float _lastIntentReceivedTime = -999f;
        private string _sessionId = string.Empty;
        private bool _sessionStartInFlight;
        private bool _strategyRequestInFlight;
        private string _debugSummary = "AI | Codex pending";
        private CodexStrategyIntent _currentIntent;
        private AiArenaSnapshotEnvelope _previousSnapshot;
        private string _lastExecutorSource = "waiting_for_codex";
        private string _lastExecutorSummary = string.Empty;
        private PlayerInputFrame _lastReportedFrame;
        private bool _hasAgentAction;
        private string _controllerOwner = string.Empty;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => _debugSummary;
        public string SessionId => _sessionId;
        public string LastExecutorSource => _lastExecutorSource;
        public string LastExecutorSummary => _lastExecutorSummary;
        public string CurrentIntentMode => _currentIntent != null ? _currentIntent.mode : string.Empty;
        public string CurrentIntentReason => _currentIntent != null ? _currentIntent.reason : string.Empty;
        public bool HasAgentAction => _hasAgentAction;
        public string ControllerOwner => _controllerOwner;
        public bool IsSessionStarting => _sessionStartInFlight;
        public bool IsStrategyRequestInFlight => _strategyRequestInFlight;
        public bool HasLiveSession => !string.IsNullOrWhiteSpace(_sessionId);
        public float IntentAgeMs => _currentIntent == null || _lastIntentReceivedTime < 0f
            ? -1f
            : (Time.realtimeSinceStartup - _lastIntentReceivedTime) * 1000f;

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
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            StartCoroutine(SendStopRequest(_sessionId));
        }

        public void CaptureFrame()
        {
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

            EnsureSessionStarted(promptState);
            if (useAgentDrivenMode)
            {
                PushAgentStateIfNeeded(snapshot, promptState);
            }
            else
            {
                RequestStrategyIfNeeded(snapshot, promptState);
            }

            AiArenaDecisionEnvelope decision;
            if (HasFreshIntent())
            {
                decision = AiArenaStrategicPolicy.Decide(snapshot, _currentIntent);
                _lastExecutorSource = "codex_live";
            }
            else if (HasReusableIntent())
            {
                decision = AiArenaStrategicPolicy.Decide(snapshot, _currentIntent);
                _lastExecutorSource = "codex_stale";
            }
            else
            {
                decision = new AiArenaDecisionEnvelope
                {
                    debugSummary = "AI | Waiting Codex",
                };
                _lastExecutorSource = "waiting_for_codex";
            }

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
            _lastReportedFrame = _currentFrame;
            _lastExecutorSummary = _debugSummary;
            _previousSnapshot = snapshot;
            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId configuredSlotId)
        {
            slotId = Mathf.Max(1, configuredSlotId.ToInt());
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
            _debugSummary = "AI | Codex pending";
            _collector.ForceRefresh();
            _eventMemory.Clear();
        }

        public void PrewarmSession()
        {
            if (!Application.isPlaying || !autoStartSession)
            {
                return;
            }

            EnsureSessionStarted(BuildPromptState(TryBuildSnapshot()));
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
            return ageMs <= Mathf.Max(maxStrategyAgeMs, _currentIntent.expiresInMs);
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

            float reusableWindowMs = Mathf.Max(maxStrategyAgeMs * 3f, _currentIntent.expiresInMs * 4f);
            return ageMs <= reusableWindowMs;
        }

        private void EnsureSessionStarted(CodexPromptState promptState)
        {
            if (!autoStartSession || _sessionStartInFlight || !string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            _sessionStartInFlight = true;
            var request = new CodexBrokerSessionStartRequest
            {
                slotId = slotId,
                promptState = promptState,
            };
            StartCoroutine(SendJsonRequest(
                useAgentDrivenMode ? "/agent/session/start" : "/session/start",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    _sessionStartInFlight = false;
                    ApplyBrokerEnvelope(responseJson);
                },
                () =>
                {
                    _sessionStartInFlight = false;
                    InvalidateBrokerSession();
                }));
        }

        private void PushAgentStateIfNeeded(AiArenaSnapshotEnvelope snapshot, CodexPromptState promptState)
        {
            if (_sessionStartInFlight || _strategyRequestInFlight || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            bool forceRefresh = ShouldForceRefresh(snapshot);
            float elapsedMs = (Time.realtimeSinceStartup - _lastStrategyRequestTime) * 1000f;
            if (!forceRefresh && elapsedMs < minStrategyIntervalMs)
            {
                return;
            }

            _strategyRequestInFlight = true;
            _lastStrategyRequestTime = Time.realtimeSinceStartup;
            var request = new CodexAgentStateUpdateRequest
            {
                sessionId = _sessionId,
                slotId = slotId,
                frame = _frameIndex,
                forceRefresh = forceRefresh,
                promptState = promptState,
                executorFeedback = BuildExecutorFeedback(snapshot),
            };

            StartCoroutine(SendJsonRequest(
                "/agent/state",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    _strategyRequestInFlight = false;
                    ApplyBrokerEnvelope(responseJson);
                },
                () =>
                {
                    _strategyRequestInFlight = false;
                    InvalidateBrokerSession();
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
            if (_sessionStartInFlight || _strategyRequestInFlight || string.IsNullOrWhiteSpace(_sessionId))
            {
                return;
            }

            bool forceRefresh = ShouldForceRefresh(snapshot);
            float elapsedMs = (Time.realtimeSinceStartup - _lastStrategyRequestTime) * 1000f;
            if (!forceRefresh && elapsedMs < minStrategyIntervalMs)
            {
                return;
            }

            _strategyRequestInFlight = true;
            _lastStrategyRequestTime = Time.realtimeSinceStartup;
            var request = new CodexBrokerStrategyTickRequest
            {
                sessionId = _sessionId,
                slotId = slotId,
                frame = _frameIndex,
                forceRefresh = forceRefresh,
                promptState = promptState,
                executorFeedback = BuildExecutorFeedback(snapshot),
            };

            StartCoroutine(SendJsonRequest(
                "/strategy/tick",
                JsonUtility.ToJson(request),
                responseJson =>
                {
                    _strategyRequestInFlight = false;
                    ApplyBrokerEnvelope(responseJson);
                },
                () =>
                {
                    _strategyRequestInFlight = false;
                    InvalidateBrokerSession();
                }));
        }

        private bool ShouldForceRefresh(AiArenaSnapshotEnvelope snapshot)
        {
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
                || current.hasTarget != previous.hasTarget
                || current.shouldPunish != previous.shouldPunish
                || current.targetVulnerable != previous.targetVulnerable;
        }

        private CodexPromptState BuildPromptState(AiArenaSnapshotEnvelope snapshot)
        {
            var promptState = new CodexPromptState
            {
                frame = snapshot != null ? snapshot.frame : _frameIndex,
                self = BuildPromptCombatant(snapshot != null ? snapshot.self : null),
                target = BuildPromptCombatant(snapshot != null && snapshot.opponents != null && snapshot.opponents.Count > 0 ? snapshot.opponents[0] : null),
                arena = BuildPromptArena(snapshot),
            };

            AppendEvents(snapshot, promptState.events);
            foreach (string memory in _eventMemory)
            {
                promptState.memory.Add(memory);
            }

            if (snapshot != null && snapshot.projectiles != null)
            {
                for (int index = 0; index < snapshot.projectiles.Count; index += 1)
                {
                    AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                    if (projectile == null || projectile.isStuck || projectile.isDisarmed)
                    {
                        continue;
                    }

                    float etaSeconds = EstimateProjectileEta(snapshot.self, projectile);
                    if (etaSeconds < 0f || etaSeconds > 0.5f)
                    {
                        continue;
                    }

                    promptState.dangerousProjectiles.Add(new CodexPromptProjectileThreat
                    {
                        sourceSlotId = projectile.sourceSlotId,
                        etaSeconds = etaSeconds,
                        position = projectile.position,
                        travelDirection = projectile.travelDirection,
                    });
                }
            }

            return promptState;
        }

        private void AppendEvents(AiArenaSnapshotEnvelope snapshot, List<string> eventSink)
        {
            if (snapshot == null || snapshot.semantics == null || snapshot.arena == null)
            {
                return;
            }

            if (_previousSnapshot == null || _previousSnapshot.semantics == null || _previousSnapshot.arena == null)
            {
                AddEvent(eventSink, "round_context_initialized");
                return;
            }

            AiArenaSemanticObservation previous = _previousSnapshot.semantics;
            AiArenaSemanticObservation current = snapshot.semantics;
            if (_previousSnapshot.arena.roundResetPending != snapshot.arena.roundResetPending && snapshot.arena.roundResetPending)
            {
                AddEvent(eventSink, "round_reset_started");
            }

            if (current.incomingProjectileThreat && !previous.incomingProjectileThreat)
            {
                AddEvent(eventSink, "projectile_threat_spiked");
            }

            if (current.targetUsingUltimate && !previous.targetUsingUltimate)
            {
                AddEvent(eventSink, "target_started_ultimate");
            }

            if (current.selfCornered != previous.selfCornered)
            {
                AddEvent(eventSink, current.selfCornered ? "self_cornered" : "self_escaped_corner");
            }

            if (current.targetCornered != previous.targetCornered)
            {
                AddEvent(eventSink, current.targetCornered ? "target_cornered" : "target_left_corner");
            }

            if (current.targetVulnerable && !previous.targetVulnerable)
            {
                AddEvent(eventSink, "target_became_vulnerable");
            }

            if (!current.hasTarget && previous.hasTarget)
            {
                AddEvent(eventSink, "target_lost");
            }
        }

        private void AddEvent(List<string> eventSink, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            eventSink.Add(value);
            _eventMemory.Enqueue(value);
            while (_eventMemory.Count > 5)
            {
                _eventMemory.Dequeue();
            }
        }

        private static CodexPromptCombatant BuildPromptCombatant(AiArenaCombatantObservation source)
        {
            if (source == null)
            {
                return new CodexPromptCombatant();
            }

            return new CodexPromptCombatant
            {
                slotId = source.slotId,
                displayName = source.displayName,
                actionKey = source.actionKey,
                isDead = source.isDead,
                isGrounded = source.isGrounded,
                isDashing = source.isDashing,
                isMeleeActive = source.isMeleeActive,
                isUltimateActive = source.isUltimateActive,
                isHitStunned = source.isHitStunned,
                canParryProjectile = source.canParryProjectile,
                canBlockProjectiles = source.canBlockProjectiles,
                arrows = source.arrows,
                facing = source.facing,
                shootCooldownLeft = source.shootCooldownLeft,
                meleeCooldownLeft = source.meleeCooldownLeft,
                dashCooldownLeft = source.dashCooldownLeft,
                ultimateCooldownLeft = source.ultimateCooldownLeft,
                hitStunTimeLeft = source.hitStunTimeLeft,
                position = source.position,
                velocity = source.velocity,
            };
        }

        private static CodexPromptArena BuildPromptArena(AiArenaSnapshotEnvelope snapshot)
        {
            AiArenaSemanticObservation semantics = snapshot != null ? snapshot.semantics : null;
            AiArenaArenaObservation arena = snapshot != null ? snapshot.arena : null;
            return new CodexPromptArena
            {
                roundResetPending = arena != null && arena.roundResetPending,
                selfCornered = semantics != null && semantics.selfCornered,
                targetCornered = semantics != null && semantics.targetCornered,
                horizontalDistance = semantics != null ? semantics.horizontalDistance : 0f,
                verticalDistance = semantics != null ? semantics.verticalDistance : 0f,
                targetInMeleeRange = semantics != null && semantics.targetInMeleeRange,
                targetInUltimateRange = semantics != null && semantics.targetInUltimateRange,
                targetInShootRange = semantics != null && semantics.targetInShootRange,
                targetAbove = semantics != null && semantics.targetAbove,
                targetBelow = semantics != null && semantics.targetBelow,
            };
        }

        private static float EstimateProjectileEta(AiArenaCombatantObservation self, AiArenaProjectileObservation projectile)
        {
            if (self == null || projectile == null)
            {
                return -1f;
            }

            Vector2 toSelf = self.position - projectile.position;
            float speedSqr = projectile.velocity.sqrMagnitude;
            if (speedSqr <= 1f || Vector2.Dot(toSelf, projectile.velocity) <= 0f)
            {
                return -1f;
            }

            return Mathf.Clamp(Vector2.Dot(toSelf, projectile.velocity) / speedSqr, 0f, 1.5f);
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

            if (!string.IsNullOrWhiteSpace(envelope.sessionId))
            {
                _sessionId = envelope.sessionId;
            }

            _hasAgentAction = envelope.hasAgentAction;
            _controllerOwner = envelope.controllerOwner ?? string.Empty;

            if (envelope.intent == null)
            {
                return;
            }

            _currentIntent = envelope.intent;
            _lastIntentReceivedTime = Time.realtimeSinceStartup;
        }

        private CodexExecutorFeedback BuildExecutorFeedback(AiArenaSnapshotEnvelope snapshot)
        {
            return new CodexExecutorFeedback
            {
                source = _lastExecutorSource,
                summary = _lastExecutorSummary,
                intentMode = _currentIntent != null ? _currentIntent.mode : string.Empty,
                intentReason = _currentIntent != null ? _currentIntent.reason : string.Empty,
                projectileThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.incomingProjectileThreat,
                targetVisible = snapshot != null && snapshot.semantics != null && snapshot.semantics.hasTarget,
                roundResetPending = snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending,
                intentAgeMs = IntentAgeMs,
                reportedInput = BuildReportedInput(_lastReportedFrame),
            };
        }

        private void InvalidateBrokerSession()
        {
            _sessionId = string.Empty;
            _currentIntent = null;
            _hasAgentAction = false;
            _controllerOwner = string.Empty;
            _lastIntentReceivedTime = -999f;
            _lastExecutorSource = "waiting_for_codex";
            _lastExecutorSummary = "AI | Broker disconnected";
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
            var request = new CodexBrokerSessionStopRequest
            {
                sessionId = sessionId,
                slotId = slotId,
            };
            yield return SendJsonRequest(useAgentDrivenMode ? "/agent/session/stop" : "/session/stop", JsonUtility.ToJson(request), null, null);
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

        private IEnumerator SendJsonRequest(string path, string payloadJson, System.Action<string> onSuccess, System.Action onFailure)
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
            while (!operation.isDone)
            {
                if ((Time.realtimeSinceStartup - startedAt) * 1000f > brokerRequestTimeoutMs)
                {
                    request.Abort();
                    break;
                }

                yield return null;
            }

            bool success = request.result == UnityWebRequest.Result.Success && !string.IsNullOrWhiteSpace(request.downloadHandler.text);
            if (success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                onFailure?.Invoke();
            }

            request.Dispose();
        }
    }
}
