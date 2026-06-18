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
                    arrows = 2,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        arrows = 4,
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    horizontalDistance = 312f,
                    verticalDistance = 96f,
                    targetAbove = true,
                    targetInShootRange = true,
                    targetInMeleeRange = false,
                    targetInUltimateRange = true,
                    targetVulnerable = true,
                    shouldAntiAir = true,
                    incomingProjectileThreat = true,
                    shouldCollectProjectile = true,
                    targetUsingMelee = true,
                    targetUsingRanged = true,
                    targetUsingUltimate = true,
                    selfCornered = true,
                    targetCornered = true,
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
            Assert.That(feedback.targetMeleeThreatActive, Is.True);
            Assert.That(feedback.targetRangedThreatActive, Is.True);
            Assert.That(feedback.targetUltimateThreatActive, Is.True);
            Assert.That(feedback.selfCornered, Is.True);
            Assert.That(feedback.targetCornered, Is.True);
            Assert.That(feedback.targetVisible, Is.True);
            Assert.That(feedback.roundResetPending, Is.True);
            Assert.That(feedback.horizontalDistance, Is.EqualTo(312f).Within(0.001f));
            Assert.That(feedback.verticalDistance, Is.EqualTo(96f).Within(0.001f));
            Assert.That(feedback.targetAbove, Is.True);
            Assert.That(feedback.targetInShootRange, Is.True);
            Assert.That(feedback.targetInMeleeRange, Is.False);
            Assert.That(feedback.targetInUltimateRange, Is.True);
            Assert.That(feedback.targetVulnerable, Is.True);
            Assert.That(feedback.shouldAntiAir, Is.True);
            Assert.That(feedback.selfArrows, Is.EqualTo(2));
            Assert.That(feedback.targetArrows, Is.EqualTo(4));
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
            Assert.That(feedback.targetMeleeThreatActive, Is.False);
            Assert.That(feedback.targetRangedThreatActive, Is.False);
            Assert.That(feedback.targetUltimateThreatActive, Is.False);
            Assert.That(feedback.selfCornered, Is.False);
            Assert.That(feedback.targetCornered, Is.False);
            Assert.That(feedback.targetVisible, Is.False);
            Assert.That(feedback.roundResetPending, Is.False);
            Assert.That(feedback.horizontalDistance, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.verticalDistance, Is.EqualTo(0f).Within(0.001f));
            Assert.That(feedback.targetAbove, Is.False);
            Assert.That(feedback.targetBelow, Is.False);
            Assert.That(feedback.targetInShootRange, Is.False);
            Assert.That(feedback.targetInMeleeRange, Is.False);
            Assert.That(feedback.targetInUltimateRange, Is.False);
            Assert.That(feedback.targetVulnerable, Is.False);
            Assert.That(feedback.shouldAntiAir, Is.False);
            Assert.That(feedback.selfArrows, Is.EqualTo(-1));
            Assert.That(feedback.targetArrows, Is.EqualTo(-1));
            Assert.That(feedback.recoverableProjectileAvailable, Is.False);
            Assert.That(feedback.recoverableProjectileCount, Is.EqualTo(0));
            Assert.That(feedback.nearestRecoverableProjectileDistance, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.intentAgeMs, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.reportedInput, Is.Not.Null);
        }

        [Test]
        public void BuildExecutorFeedback_UsesMoveSummaryWhenCurrentSummaryIsEmpty()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 3,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    horizontalDistance = 980f,
                    verticalDistance = 24f,
                    targetDirection = Vector2.right,
                    incomingProjectileThreat = false,
                    targetUsingRanged = false,
                    targetUsingUltimate = false,
                    selfCornered = false,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                string.Empty,
                currentIntent: null,
                snapshot: snapshot,
                intentAgeMs: 80f,
                reportedInput: new CodexReportedInputFrame { frame = 0, axis = 0f, aim = Vector2.zero });

            Assert.That(feedback.summary, Is.EqualTo("AI MOVE"));
            Assert.That(feedback.botFeedback, Does.Contain("spacing stable"));
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
        public void BuildExecutorFeedback_UsesReportedInputInsteadOfSummaryForProjectileDefense()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                    dashCooldownLeft = 0.4f,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.14f,
                    shouldDashEvade = true,
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 18,
                aim = Vector2.right,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI PARRY DASH",
                currentIntent: null,
                snapshot,
                90f,
                reportedInput);

            Assert.That(feedback.botFeedback, Does.Contain("missed projectile defense"));
            Assert.That(feedback.botFeedback, Does.Contain("AI PARRY DASH"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("projectile threat 0.14s; action AI PARRY DASH; improve: defend before attacking"));
        }

        [Test]
        public void BuildExecutorFeedback_AlignsBotFeedbackWithReportedInputSnapshot()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.14f,
                    shouldDashEvade = true,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetInShootRange = true,
                    horizontalDistance = 360f,
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 34,
                shootPressed = true,
                shootHeld = true,
                aim = Vector2.right,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI ZONE SHOT",
                currentIntent: null,
                currentSnapshot,
                90f,
                reportedInput,
                reportedInputSnapshot);

            Assert.That(feedback.projectileThreatActive, Is.True);
            Assert.That(feedback.reportedInput, Is.SameAs(reportedInput));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed projectile defense"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("projectile threat 0.14s"));
        }

        [Test]
        public void BuildExecutorFeedback_UsesCurrentNoTargetStateOverReportedInputSnapshot()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = false,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 150,
                axis = -0.25f,
                aim = Vector2.right,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent: null,
                currentSnapshot,
                120f,
                reportedInput,
                reportedInputSnapshot);

            Assert.That(feedback.targetVisible, Is.False);
            Assert.That(feedback.summary, Is.EqualTo("AI | Fallback:no_target"));
            Assert.That(feedback.botFeedback, Does.Contain("no target visible"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed corner escape"));
        }

        [Test]
        public void BuildExecutorFeedback_NormalizesStaleIntentForCurrentNoTargetState()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                arena = new AiArenaArenaObservation
                {
                    roundResetPending = false,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = false,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI ANTI AIR CHASE",
                new CodexStrategyIntent { mode = "pressure", reason = "heuristic_anti_air" },
                currentSnapshot,
                180f,
                new CodexReportedInputFrame { frame = 210, axis = 0f, aim = Vector2.left });

            Assert.That(feedback.summary, Is.EqualTo("AI | Fallback:no_target"));
            Assert.That(feedback.intentMode, Is.EqualTo("stabilize"));
            Assert.That(feedback.intentReason, Is.EqualTo("heuristic_waiting_for_target"));
            Assert.That(feedback.botFeedback, Does.Contain("no target visible"));
        }

        [Test]
        public void BuildExecutorFeedback_PrioritizesProjectileThreatWhenTargetIsMissing()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                arena = new AiArenaArenaObservation
                {
                    roundResetPending = false,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = false,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                new CodexStrategyIntent { mode = "pressure", reason = "heuristic_waiting_for_target" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 211, axis = -1f, aim = Vector2.left });

            Assert.That(feedback.targetVisible, Is.False);
            Assert.That(feedback.projectileThreatActive, Is.True);
            Assert.That(feedback.summary, Is.EqualTo("AI PROJECTILE THREAT"));
            Assert.That(feedback.intentMode, Is.EqualTo("retreat"));
            Assert.That(feedback.intentReason, Is.EqualTo("projectile_threat_feedback"));
            Assert.That(feedback.botFeedback, Does.Contain("projectile threat active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("no target visible"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentProjectileThreatSummaryWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.18f,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = false,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent: null,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = 0.75f, aim = Vector2.right },
                reportedInputSnapshot);

            Assert.That(feedback.projectileThreatActive, Is.True);
            Assert.That(feedback.summary, Is.EqualTo("AI PROJECTILE THREAT"));
            Assert.That(feedback.botFeedback, Does.Contain("projectile threat active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed projectile defense"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentCornerThreatSummaryWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = false,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent: null,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = 0.75f, aim = Vector2.right },
                reportedInputSnapshot);

            Assert.That(feedback.selfCornered, Is.True);
            Assert.That(feedback.summary, Is.EqualTo("AI CORNER THREAT"));
            Assert.That(feedback.botFeedback, Does.Contain("corner pressure active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed corner escape"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsStalledCornerThreatWhenInputDoesNotEscape()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = false,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent: null,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = -0.21f, aim = Vector2.right },
                reportedInputSnapshot);

            Assert.That(feedback.selfCornered, Is.True);
            Assert.That(feedback.summary, Is.EqualTo("AI CORNER STALLED"));
            Assert.That(feedback.botFeedback, Does.Contain("corner escape stalled"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("corner pressure active now"));
        }

        [Test]
        public void BuildExecutorFeedback_ClearsResolvedCornerEscape()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = false,
                    targetCornered = false,
                    targetDirection = Vector2.right,
                    horizontalDistance = 940f,
                    incomingProjectileThreat = false,
                    targetUsingRanged = false,
                    targetUsingMelee = false,
                    targetUsingUltimate = false,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI CORNER ESCAPE",
                new CodexStrategyIntent { mode = "retreat", reason = "missed_corner_escape" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 72, axis = 0.65f, aim = Vector2.right },
                reportedInputSnapshot);

            Assert.That(feedback.selfCornered, Is.False);
            Assert.That(feedback.summary, Is.EqualTo("AI RESOLVED CORNER PRESSURE"));
            Assert.That(feedback.botFeedback, Does.Contain("corner pressure resolved"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("corner pressure detected"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentRangedThreatSummaryWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    targetDirection = Vector2.right,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = false,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI COLLECT ARROW",
                currentIntent: null,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = -1f, aim = Vector2.left, jumpPressed = true },
                reportedInputSnapshot);

            Assert.That(feedback.targetRangedThreatActive, Is.True);
            Assert.That(feedback.summary, Is.EqualTo("AI RANGED THREAT"));
            Assert.That(feedback.botFeedback, Does.Contain("ranged threat active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("missed ranged response"));
        }

        [Test]
        public void BuildExecutorFeedback_ReplacesStaleProjectileFeedbackDuringCurrentRangedThreat()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    incomingProjectileThreat = false,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    horizontalDistance = 626f,
                    verticalDistance = 112f,
                    targetAbove = true,
                    targetDirection = Vector2.left,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.27f,
                    targetDirection = Vector2.left,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI RANGED THREAT",
                new CodexStrategyIntent { mode = "retreat", reason = "projectile_threat_feedback" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 63, axis = -1f, aim = Vector2.left },
                reportedInputSnapshot);

            Assert.That(feedback.summary, Is.EqualTo("AI RANGED THREAT"));
            Assert.That(feedback.intentMode, Is.EqualTo("pressure"));
            Assert.That(feedback.intentReason, Is.EqualTo("target_ranged_threat"));
            Assert.That(feedback.botFeedback, Does.Contain("ranged threat active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("projectile threat"));
        }

        [Test]
        public void BuildExecutorFeedback_ClearsResolvedRangedThreatRetreat()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 3,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        arrows = 1,
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetInShootRange = true,
                    horizontalDistance = 340f,
                    verticalDistance = 72f,
                    targetDirection = Vector2.right,
                    incomingProjectileThreat = false,
                    targetUsingRanged = false,
                    targetUsingMelee = false,
                    targetUsingUltimate = false,
                    selfCornered = false,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI DEFENSIVE RETREAT",
                new CodexStrategyIntent { mode = "retreat", reason = "target_ranged_threat" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 41, axis = -0.79f, aim = Vector2.right });

            Assert.That(feedback.summary, Is.EqualTo("AI RESOLVED THREAT PRESSURE"));
            Assert.That(feedback.botFeedback, Does.Contain("resolved threat pressure active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("enemy ranged active"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentAntiAirChaseWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetAbove = true,
                    targetInShootRange = false,
                    horizontalDistance = 1120f,
                    verticalDistance = 340f,
                    targetDirection = Vector2.left,
                },
            };
            var reportedInputSnapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetAbove = false,
                    targetInShootRange = false,
                },
            };
            var currentIntent = new CodexStrategyIntent
            {
                mode = "pressure",
                reason = "heuristic_anti_air",
                antiAir = false,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = -1f, aim = Vector2.left, jumpHeld = true },
                reportedInputSnapshot);

            Assert.That(feedback.summary, Is.EqualTo("AI ANTI AIR CHASE"));
            Assert.That(feedback.botFeedback, Does.Contain("anti-air chase active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("spacing stable"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsStalledAntiAirChaseWhenInputDoesNotClimb()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 3,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetAbove = true,
                    targetInShootRange = false,
                    shouldAntiAir = false,
                    horizontalDistance = 1460f,
                    verticalDistance = 340f,
                    targetDirection = Vector2.left,
                },
            };
            var currentIntent = new CodexStrategyIntent
            {
                mode = "pressure",
                reason = "heuristic_anti_air",
                antiAir = false,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = -1f, aim = Vector2.left });

            Assert.That(feedback.summary, Is.EqualTo("AI ANTI AIR STALLED"));
            Assert.That(feedback.botFeedback, Does.Contain("anti-air chase stalled"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("anti-air chase active now"));
        }

        [Test]
        public void BuildExecutorFeedback_DoesNotReportAntiAirChaseForSlightVerticalOffset()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 3,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetAbove = true,
                    targetInShootRange = false,
                    shouldAntiAir = false,
                    horizontalDistance = 1460f,
                    verticalDistance = 44f,
                    targetDirection = Vector2.right,
                },
            };
            var currentIntent = new CodexStrategyIntent
            {
                mode = "pressure",
                reason = "heuristic_anti_air",
                antiAir = false,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI MOVE",
                currentIntent,
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = 1f, aim = Vector2.right });

            Assert.That(feedback.summary, Is.EqualTo("AI MOVE"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("anti-air chase active now"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentAntiAirShotWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetAbove = true,
                    targetInShootRange = true,
                    shouldAntiAir = true,
                    horizontalDistance = 520f,
                    verticalDistance = 140f,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI DEFENSIVE RETREAT",
                new CodexStrategyIntent { mode = "pressure", reason = "target_ranged_threat" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 42, axis = -0.8f, aim = Vector2.right });

            Assert.That(feedback.summary, Is.EqualTo("AI ANTI AIR"));
            Assert.That(feedback.botFeedback, Does.Contain("anti-air shot active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("enemy ranged active"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsCurrentLastArrowPressureWhenInputSnapshotIsOlder()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 3,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        arrows = 0,
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetInShootRange = true,
                    horizontalDistance = 850f,
                    verticalDistance = 120f,
                    targetDirection = Vector2.right,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI DEFENSIVE RETREAT",
                new CodexStrategyIntent { mode = "retreat", reason = "target_ranged_threat" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 109, axis = 0.94f, aim = Vector2.left });

            Assert.That(feedback.summary, Is.EqualTo("AI LAST ARROW PRESSURE"));
            Assert.That(feedback.botFeedback, Does.Contain("last-arrow pressure active now"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("enemy ranged active"));
        }

        [Test]
        public void BuildExecutorFeedback_ReportsStalledLastArrowPressureWhenInputHasNoAction()
        {
            var currentSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 1,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        arrows = 0,
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetInShootRange = true,
                    horizontalDistance = 564f,
                    verticalDistance = 206f,
                    targetDirection = Vector2.left,
                },
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI LAST ARROW PRESSURE",
                new CodexStrategyIntent { mode = "pressure", reason = "last_arrow_pressure" },
                currentSnapshot,
                80f,
                new CodexReportedInputFrame { frame = 8834, axis = 0f, aim = Vector2.left });

            Assert.That(feedback.summary, Is.EqualTo("AI LAST ARROW STALLED"));
            Assert.That(feedback.botFeedback, Does.Contain("last-arrow pressure stalled"));
            Assert.That(feedback.botFeedback, Does.Not.Contain("active now"));
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
        public void BotFeedbackBuilder_TreatsProjectileDriftAsThreatResponse()
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
                    incomingProjectileTime = 0.12f,
                    shouldDashEvade = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PROJECTILE DRIFT",
                moveAxis = 0.35f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("projectile threat 0.12s"));
            Assert.That(feedback, Does.Contain("AI PROJECTILE DRIFT"));
            Assert.That(feedback, Does.Not.Contain("missed projectile defense"));
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
        public void BotFeedbackBuilder_UsesExecutedFrameInsteadOfActionSummaryForProjectileDefense()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    arrows = 2,
                    dashCooldownLeft = 0.4f,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.16f,
                    shouldDashEvade = true,
                },
            };
            var frame = new PlayerInputFrame
            {
                frame = 21,
                aim = Vector2.right,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, "AI PARRY DASH", frame);

            Assert.That(feedback, Does.Contain("missed projectile defense"));
            Assert.That(feedback, Does.Contain("AI PARRY DASH"));
            Assert.That(feedback, Does.Not.Contain("projectile threat 0.16s; action AI PARRY DASH; improve: defend before attacking"));
        }

        [Test]
        public void BotFeedbackBuilder_TreatsExecutedProjectileDriftAsThreatResponse()
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
                    incomingProjectileTime = 0.12f,
                    shouldDashEvade = true,
                },
            };
            var frame = new PlayerInputFrame
            {
                frame = 22,
                axis = 0.35f,
                right = true,
                aim = Vector2.right,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, "AI PROJECTILE DRIFT", frame);

            Assert.That(feedback, Does.Contain("projectile threat 0.12s"));
            Assert.That(feedback, Does.Contain("AI PROJECTILE DRIFT"));
            Assert.That(feedback, Does.Not.Contain("missed projectile defense"));
        }

        [Test]
        public void BotFeedbackBuilder_TreatsUltimateEvadeAsDangerResponse()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI EVADE ULT",
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("enemy ultimate active"));
            Assert.That(feedback, Does.Contain("AI EVADE ULT"));
            Assert.That(feedback, Does.Not.Contain("missed ultimate escape"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedUltimateEscapeWhenDecisionAttacks()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PUNISH MELEE",
                meleePressed = true,
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed ultimate escape"));
            Assert.That(feedback, Does.Contain("dash or move away"));
        }

        [Test]
        public void BotFeedbackBuilder_TreatsMeleeEvadeAsThreatResponse()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI EVADE MELEE",
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("enemy melee active"));
            Assert.That(feedback, Does.Contain("AI EVADE MELEE"));
            Assert.That(feedback, Does.Not.Contain("missed melee escape"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedMeleeEscapeWhenDecisionAttacks()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PUNISH MELEE",
                meleePressed = true,
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed melee escape"));
            Assert.That(feedback, Does.Contain("dash or move away"));
        }

        [Test]
        public void BotFeedbackBuilder_TreatsRangedPressureAsThreatResponse()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    targetDirection = Vector2.right,
                    targetInShootRange = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PRESSURE SHOT",
                shootPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("enemy ranged active"));
            Assert.That(feedback, Does.Contain("AI PRESSURE SHOT"));
            Assert.That(feedback, Does.Not.Contain("missed ranged response"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedRangedResponseWhenDecisionIgnoresThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    targetDirection = Vector2.right,
                    targetInShootRange = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI COLLECT ARROW",
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed ranged response"));
            Assert.That(feedback, Does.Contain("dodge, break line, or interrupt"));
        }

        [Test]
        public void BotFeedbackBuilder_TreatsCornerEscapeAsPressureResponse()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI CORNER ESCAPE",
                moveAxis = 1f,
                dashPrimaryPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("corner pressure detected"));
            Assert.That(feedback, Does.Contain("AI CORNER ESCAPE"));
            Assert.That(feedback, Does.Not.Contain("missed corner escape"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedCornerEscapeWhenDecisionMovesIntoWall()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI RETREAT",
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed corner escape"));
            Assert.That(feedback, Does.Contain("move toward center"));
        }

        [Test]
        public void BotFeedbackBuilder_PrioritizesCornerEscapeOverWallSideArrowRecovery()
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
                    selfCornered = true,
                    targetDirection = Vector2.right,
                    shouldCollectProjectile = true,
                    collectibleProjectileDistance = 72f,
                    collectibleProjectileDirection = Vector2.left,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI CORNER ESCAPE",
                moveAxis = 1f,
                dashPrimaryPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("corner pressure detected"));
            Assert.That(feedback, Does.Contain("AI CORNER ESCAPE"));
            Assert.That(feedback, Does.Not.Contain("missed arrow recovery"));
        }

        [Test]
        public void BotFeedbackBuilder_PrioritizesCornerEscapeWhenOutOfArrowsWithoutKnownRecovery()
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
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI CORNER ESCAPE",
                moveAxis = 1f,
                dashPrimaryPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("corner pressure detected"));
            Assert.That(feedback, Does.Contain("AI CORNER ESCAPE"));
            Assert.That(feedback, Does.Not.Contain("recover arrow"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsMissedCornerEscapeWhenOutOfArrowsAndMovingIntoWall()
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
                    selfCornered = true,
                    targetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI ARROW DISADVANTAGE",
                moveAxis = -1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("missed corner escape"));
            Assert.That(feedback, Does.Contain("move toward center"));
            Assert.That(feedback, Does.Not.Contain("recover arrow"));
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
        public void BotFeedbackBuilder_ReportsSearchWhenOutOfArrowsWithoutVisibleRecovery()
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
                    shouldCollectProjectile = false,
                    collectibleProjectileDistance = -1f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI MOVE",
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("out of arrows with no recovery visible"));
            Assert.That(feedback, Does.Contain("reposition and search"));
            Assert.That(feedback, Does.Not.Contain("recover arrow at unknown distance"));
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
        public void BotFeedbackBuilder_ReportsShotAttemptWithoutArrows()
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
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI SHOOT",
                shootPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("shot attempted without arrows"));
            Assert.That(feedback, Does.Contain("recover arrow before shooting"));
            Assert.That(feedback, Does.Not.Contain("recover arrow at"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsShotAttemptOutsideShootRange()
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
                    horizontalDistance = 1180f,
                    targetInShootRange = false,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI ZONE SHOT",
                shootPressed = true,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("shot attempted out of range at 1180u"));
            Assert.That(feedback, Does.Contain("close distance, aim a valid line, or hold fire"));
            Assert.That(feedback, Does.Not.Contain("spacing stable"));
        }

        [Test]
        public void BotFeedbackBuilder_ReportsVulnerableTargetOutOfPunishRange()
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
                    shouldPunish = false,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    targetInShootRange = false,
                    horizontalDistance = 1180f,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI ADVANCE",
                moveAxis = 1f,
            };

            string feedback = AiArenaBotFeedbackBuilder.Build(snapshot, decision);

            Assert.That(feedback, Does.Contain("vulnerable target out of range at 1180u"));
            Assert.That(feedback, Does.Contain("close distance before spending attacks"));
            Assert.That(feedback, Does.Not.Contain("missed punish window"));
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
