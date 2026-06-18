using System.Collections.Generic;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Input
{
    [DisallowMultipleComponent]
    public sealed class LocalAiCombatantInputSource : MonoBehaviour, ICombatantInputSource, IBotFeedbackInputSource, IBotCoachStatusInputSource
    {
        [Min(1)] public int slotId = 1;
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
        [Header("Transport")]
        [Min(1)] public int decisionTimeoutMs = 25;
        public bool simulateTransportTimeout;
        public bool simulateTransportDisconnect;
        public bool simulateInvalidResponse;

        private readonly AiArenaRuntimeSnapshotCollector _collector = new AiArenaRuntimeSnapshotCollector();
        private readonly AiArenaLocalTransport _transport = new AiArenaLocalTransport();
        private readonly AiArenaMovementStallEscapeController _movementStallEscape = new AiArenaMovementStallEscapeController();
        private PlayerInputFrame _currentFrame;
        private int _frameIndex;
        private AiArenaExecutionState _executionState;
        private string _debugSummary = "AI";
        private string _botFeedback = string.Empty;

        public PlayerInputFrame CurrentFrame => _currentFrame;
        public int ActiveGamepadSlot => -1;
        public string FaceButtonDebug => _debugSummary;
        public string BotFeedback => _botFeedback;
        public string BotControllerStatus => "local-ai";

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
            string snapshotJson = JsonUtility.ToJson(snapshot);

            _transport.simulateTimeout = simulateTransportTimeout;
            _transport.simulateDisconnect = simulateTransportDisconnect;
            _transport.simulateInvalidResponse = simulateInvalidResponse;

            AiArenaTransportResult result = _transport.RequestDecisionJson(snapshotJson, decisionTimeoutMs);
            _currentFrame = ResolveFrame(result, self, snapshot);
            _currentFrame = ObserveMovementStall(snapshot, _currentFrame);
            _frameIndex += 1;
        }

        public void ConfigureForSlot(CombatantSlotId configuredSlotId)
        {
            slotId = Mathf.Max(1, configuredSlotId.ToInt());
            _frameIndex = 0;
            _executionState = default;
            _botFeedback = string.Empty;
            _movementStallEscape.Reset();
            _collector.ForceRefresh();
        }

        public void ResetInputState()
        {
            _currentFrame = default;
            _frameIndex = 0;
            _executionState = default;
            _debugSummary = "AI";
            _botFeedback = string.Empty;
            _movementStallEscape.Reset();
        }

        private PlayerInputFrame ResolveFrame(AiArenaTransportResult result, AiArenaControllerSnapshot self, AiArenaSnapshotEnvelope snapshot)
        {
            if (!result.IsSuccess)
            {
                _botFeedback = "transport " + result.Status + "; improve: verify local AI transport.";
                return AiArenaFrameExecutor.BuildFallbackFrame(
                    ref _executionState,
                    self,
                    _frameIndex,
                    "AI | Fallback:" + result.Status,
                    ref _debugSummary);
            }

            AiArenaDecisionEnvelope decision = null;
            try
            {
                decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);
            }
            catch
            {
                decision = null;
            }

            if (decision == null || decision.schemaVersion != AiArenaDecisionEnvelope.CurrentSchemaVersion)
            {
                _botFeedback = "invalid decision json; improve: verify local AI policy output.";
                return AiArenaFrameExecutor.BuildFallbackFrame(
                    ref _executionState,
                    self,
                    _frameIndex,
                    "AI | Fallback:invalid_decision_json",
                    ref _debugSummary);
            }

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
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
            _botFeedback = AiArenaBotFeedbackBuilder.Build(snapshot, _debugSummary, frame);
            return frame;
        }

        private PlayerInputFrame ObserveMovementStall(AiArenaSnapshotEnvelope snapshot, PlayerInputFrame frame)
        {
            PlayerInputFrame resolvedFrame = _movementStallEscape.Observe(snapshot, frame);
            if (_movementStallEscape.EscapedThisFrame)
            {
                _debugSummary = "AI | Movement stalled";
                _botFeedback = "movement stalled; action: escape jump/dash; improve: replan path instead of holding one axis.";
            }

            return resolvedFrame;
        }
    }
}
