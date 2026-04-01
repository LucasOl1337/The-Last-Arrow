using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaInputSourceTests
    {
        [Test]
        public void CombatantSlotProfile_DefaultControlMode_IsHuman()
        {
            CombatantSlotProfile profile = ScriptableObject.CreateInstance<CombatantSlotProfile>();

            try
            {
                Assert.That(profile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CombatantSlotProfile_DefaultAiBrain_IsLocalHeuristic()
        {
            CombatantSlotProfile profile = ScriptableObject.CreateInstance<CombatantSlotProfile>();

            try
            {
                Assert.That(profile.ResolveAiBrain(), Is.EqualTo(AiBrainKind.LocalHeuristic));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IdleCombatantInputSource_ProducesEmptyFrame()
        {
            GameObject root = new GameObject("IdleInput");
            IdleCombatantInputSource input = root.AddComponent<IdleCombatantInputSource>();

            try
            {
                input.ConfigureForSlot(CombatantSlotId.SlotTwo);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.CurrentFrame.jumpPressed, Is.False);
                Assert.That(input.CurrentFrame.shootPressed, Is.False);
                Assert.That(input.FaceButtonDebug, Is.EqualTo("Idle"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_MovesTowardClosestOpponent()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                self.currentArrows = 5;
                opponent.currentArrows = 5;
                selfRoot.transform.position = new Vector3(-200f, 0f, 0f);
                opponentRoot.transform.position = new Vector3(200f, 0f, 0f);

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.GreaterThan(0f));
                Assert.That(input.CurrentFrame.right, Is.True);
                Assert.That(input.CurrentFrame.aim.x, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_FallsBackSafely_OnInvalidResponse()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(128f, 0f, 0f);
                input.simulateInvalidResponse = true;

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.CurrentFrame.shootPressed, Is.False);
                Assert.That(input.FaceButtonDebug, Does.Contain("Fallback"));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_FallsBackSafely_OnTimeout()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(512f, 0f, 0f);
                input.simulateTransportTimeout = true;

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.FaceButtonDebug, Does.Contain("Timeout").IgnoreCase);
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_HandlesOpponentResetWithoutException()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(400f, 0f, 0f);

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();
                Object.DestroyImmediate(opponentRoot);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.FaceButtonDebug, Does.Contain("Fallback").Or.Contain("NoTarget"));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
            }
        }

        [Test]
        public void LocalTransport_UsesDashAgainstImminentProjectileThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(300f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    shouldDashEvade = true,
                    incomingProjectileTime = 0.12f,
                    targetInShootRange = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.dashPrimaryPressed, Is.True);
        }

        [Test]
        public void LocalTransport_PunishesVulnerableTargetInMeleeRange()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 1,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        isShootAnimating = true,
                        position = new Vector2(80f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    horizontalDistance = 80f,
                    verticalDistance = 0f,
                    targetInMeleeRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.meleePressed, Is.True);
        }

        [Test]
        public void StrategicPolicy_UsesCodexZoneIntentToPreferShooting()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(420f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 420f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.8f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = true,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "keep distance",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.True);
            Assert.That(decision.meleePressed, Is.False);
        }

        private sealed class PlayerController : MonoBehaviour
        {
            public int slotId = 1;
            public int currentArrows = 5;
            public bool dead;
            public bool grounded = true;

            public bool IsDead => dead;
            public bool IsGrounded => grounded;
            public int CurrentArrows => currentArrows;
            public int Facing => 1;
            public float HorizontalVelocity => 0f;
            public float VerticalVelocity => 0f;
            public Vector2 RootPosition => transform.position;
        }
    }
}
