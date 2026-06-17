using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexBrokerStateMapperTests
    {
        [Test]
        public void BuildExecutorFeedback_MapsSnapshotIntentAndReportedInput()
        {
            var intent = new CodexStrategyIntent
            {
                mode = "pressure",
                reason = "punish_jump",
            };
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    position = Vector2.zero,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    shouldCollectProjectile = true,
                },
                arena = new AiArenaArenaObservation
                {
                    roundResetPending = true,
                },
                projectiles = new System.Collections.Generic.List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isCollectible = true,
                        isStuck = true,
                        sourceSlotId = 2,
                        position = new Vector2(48f, 0f),
                    },
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 77,
                axis = 0.75f,
                aim = new Vector2(3f, -2f),
                jumpPressed = true,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI | Active",
                intent,
                snapshot,
                123.4f,
                reportedInput);

            Assert.That(feedback.source, Is.EqualTo("codex"));
            Assert.That(feedback.summary, Is.EqualTo("AI | Active"));
            Assert.That(feedback.intentMode, Is.EqualTo("pressure"));
            Assert.That(feedback.intentReason, Is.EqualTo("punish_jump"));
            Assert.That(feedback.projectileThreatActive, Is.True);
            Assert.That(feedback.targetVisible, Is.True);
            Assert.That(feedback.roundResetPending, Is.True);
            Assert.That(feedback.recoverableProjectileAvailable, Is.True);
            Assert.That(feedback.recoverableProjectileCount, Is.EqualTo(1));
            Assert.That(feedback.nearestRecoverableProjectileDistance, Is.EqualTo(48f).Within(0.001f));
            Assert.That(feedback.intentAgeMs, Is.EqualTo(123.4f).Within(0.001f));
            Assert.That(feedback.reportedInput, Is.SameAs(reportedInput));
            Assert.That(feedback.botFeedback, Does.Contain("projectile threat"));
            Assert.That(feedback.botFeedback, Does.Contain("AI | Active"));
        }

        [Test]
        public void BuildExecutorFeedback_UsesSafeDefaultsWhenInputsAreMissing()
        {
            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "waiting_for_codex",
                "AI | Broker disconnected",
                currentIntent: null,
                snapshot: null,
                intentAgeMs: -1f,
                reportedInput: null);

            Assert.That(feedback.intentMode, Is.EqualTo(string.Empty));
            Assert.That(feedback.intentReason, Is.EqualTo(string.Empty));
            Assert.That(feedback.projectileThreatActive, Is.False);
            Assert.That(feedback.targetVisible, Is.False);
            Assert.That(feedback.roundResetPending, Is.False);
            Assert.That(feedback.recoverableProjectileAvailable, Is.False);
            Assert.That(feedback.recoverableProjectileCount, Is.EqualTo(0));
            Assert.That(feedback.nearestRecoverableProjectileDistance, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.intentAgeMs, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.reportedInput, Is.Not.Null);
        }

        [Test]
        public void BuildExecutorFeedback_MarksRecoverableProjectileAvailableEvenWhenCollectionIsNotRecommended()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    position = Vector2.zero,
                    arrows = 3,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfHasArrows = true,
                    shouldCollectProjectile = false,
                },
                projectiles = new System.Collections.Generic.List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isCollectible = true,
                        sourceSlotId = 2,
                        position = new Vector2(64f, 0f),
                    },
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI | Active",
                currentIntent: null,
                snapshot,
                50f,
                reportedInput: null);

            Assert.That(feedback.recoverableProjectileAvailable, Is.True);
            Assert.That(feedback.recoverableProjectileCount, Is.EqualTo(1));
            Assert.That(feedback.nearestRecoverableProjectileDistance, Is.EqualTo(64f).Within(0.001f));
        }

        [Test]
        public void BuildExecutorFeedback_UsesReportedAttackInputForPunishFeedback()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 1,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    targetInShootRange = true,
                    horizontalDistance = 220f,
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 12,
                shootPressed = true,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI | Active",
                currentIntent: null,
                snapshot,
                75f,
                reportedInput);

            Assert.That(feedback.reportedInput, Is.SameAs(reportedInput));
            Assert.That(feedback.botFeedback, Does.Contain("punish window available"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed punish"));
        }

        [Test]
        public void ResolveControllerOwner_ReturnsEnvelopeOwnerWhenPresent()
        {
            string owner = CodexBrokerStateMapper.ResolveControllerOwner(
                "remote_owner",
                hasExecutableIntent: false,
                useAgentDrivenMode: true);

            Assert.That(owner, Is.EqualTo("remote_owner"));
        }

        [Test]
        public void ResolveControllerOwner_ReturnsBrokerDefaultWhenNoExecutableIntentExists()
        {
            string agentDrivenOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: false,
                useAgentDrivenMode: true);
            string directOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: false,
                useAgentDrivenMode: false);

            Assert.That(agentDrivenOwner, Is.EqualTo("BrokerDefault"));
            Assert.That(directOwner, Is.EqualTo("BrokerDefault"));
        }

        [Test]
        public void ResolveControllerOwner_UsesModeSpecificFallbackWhenExecutableIntentExists()
        {
            string agentDrivenOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: true,
                useAgentDrivenMode: true);
            string directOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: true,
                useAgentDrivenMode: false);

            Assert.That(agentDrivenOwner, Is.EqualTo("Codex"));
            Assert.That(directOwner, Is.EqualTo("CodexDirect"));
        }

        [Test]
        public void BotFeedbackBuilder_PrioritizesProjectileThreatAdvice()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 0,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    shouldCollectProjectile = true,
                    collectibleProjectileDistance = 48f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PARRY HOLD",
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("projectile threat 0.12s"));
            Assert.That(feedback, Does.Contain("AI PARRY HOLD"));
            Assert.That(feedback, Does.Contain("defend before attacking"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedProjectileDefenseWhenDecisionIgnoresThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.16f,
                    shouldDashEvade = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI DRIFT",
                moveAxis = 0.35f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed projectile defense"));
            Assert.That(feedback, Does.Contain("dash, jump, parry, or block"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsArrowRecoveryAdvice()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 0,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    shouldCollectProjectile = true,
                    collectibleProjectileDistance = 64f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI COLLECT ARROW",
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("recover arrow at 64u"));
            Assert.That(feedback, Does.Contain("recover ammo before forcing trades"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedArrowRecoveryWhenDecisionMovesAwayFromCollectible()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 0,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    shouldCollectProjectile = true,
                    collectibleProjectileDistance = 72f,
                    collectibleProjectileDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI DRIFT",
                moveAxis = -0.65f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed arrow recovery"));
            Assert.That(feedback, Does.Contain("move toward pickup"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedPunishWhenDecisionDoesNotAttack()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    horizontalDistance = 180f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI DRIFT",
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed punish window"));
            Assert.That(feedback, Does.Contain("fire, melee, or ultimate"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedAntiAirWhenDecisionDoesNotChallengeVerticalTarget()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    shouldAntiAir = true,
                    targetAbove = true,
                    targetInShootRange = true,
                    horizontalDistance = 320f,
                    verticalDistance = 160f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI DRIFT",
                moveAxis = 0.35f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed anti-air"));
            Assert.That(feedback, Does.Contain("shoot, jump, or aim upward"));
        }

        [Test]
        public void BotFeedbackBuilder_UsesSafeFallbackWhenSnapshotIsMissing()
        {
            string feedback = AiArenaBotFeedbackBuilder.Build(null, null);

            Assert.That(feedback, Does.Contain("waiting for arena snapshot"));
            Assert.That(feedback, Does.Contain("verify bot observation setup"));
        }
    }
}
