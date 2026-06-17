using System.Collections.Generic;
using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexPromptStateBuilderTests
    {
        [Test]
        public void Build_UsesDefaultPromptPartsWhenSnapshotFieldsAreMissing()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 17,
                self = null,
                opponents = null,
                arena = null,
                projectiles = null,
                semantics = null,
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 99,
                memoryHistory: new List<string> { "prior-a", "prior-b" });

            Assert.That(prompt.frame, Is.EqualTo(17));
            Assert.That(prompt.botId, Is.EqualTo(string.Empty));
            Assert.That(prompt.botDisplayName, Is.EqualTo(string.Empty));
            Assert.That(prompt.self, Is.Not.Null);
            Assert.That(prompt.self.slotId, Is.EqualTo(0));
            Assert.That(prompt.target, Is.Not.Null);
            Assert.That(prompt.target.slotId, Is.EqualTo(0));
            Assert.That(prompt.arena, Is.Not.Null);
            Assert.That(prompt.arena.roundResetPending, Is.False);
            Assert.That(prompt.arena.currentRespawnSeedLabel, Is.EqualTo(string.Empty));
            Assert.That(prompt.dangerousProjectiles, Is.Empty);
            Assert.That(prompt.recoverableProjectiles, Is.Empty);
            Assert.That(prompt.events, Is.Empty);
            Assert.That(prompt.memory, Has.Count.EqualTo(2));
            Assert.That(prompt.memory[0], Is.EqualTo("prior-a"));
            Assert.That(prompt.memory[1], Is.EqualTo("prior-b"));
        }

        [Test]
        public void Build_AddsRoundContextInitializedEventWhenPreviousSnapshotIsMissing()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 5,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    botId = "bot-alpha",
                    botDisplayName = "Alpha",
                    projectileInheritVelocityFactor = 0.45f,
                    projectileBaseSpeed = 1234f,
                    projectileGravity = 987f,
                    position = new Vector2(32f, 16f),
                },
                arena = new AiArenaArenaObservation
                {
                    roundResetPending = true,
                    roundsToChampion = 3,
                    currentRespawnSeedLabel = "seed_a",
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = false,
                },
                projectiles = new List<AiArenaProjectileObservation>(),
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 4,
                memoryHistory: new List<string> { "memory-1" });

            Assert.That(prompt.botId, Is.EqualTo("bot-alpha"));
            Assert.That(prompt.botDisplayName, Is.EqualTo("Alpha"));
            Assert.That(prompt.self.projectileInheritVelocityFactor, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(prompt.self.projectileBaseSpeed, Is.EqualTo(1234f).Within(0.001f));
            Assert.That(prompt.self.projectileGravity, Is.EqualTo(987f).Within(0.001f));
            Assert.That(prompt.events, Has.Count.EqualTo(1));
            Assert.That(prompt.events[0], Is.EqualTo("round_context_initialized"));
            Assert.That(prompt.memory, Has.Count.EqualTo(1));
            Assert.That(prompt.memory[0], Is.EqualTo("memory-1"));
        }

        [Test]
        public void Build_AddsProjectileThreatTransitionEvents()
        {
            var previousSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 26,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = false,
                },
            };
            var currentThreatSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 27,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                },
            };
            var previousThreatSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 28,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = true,
                },
            };
            var currentSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 29,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    incomingProjectileThreat = false,
                },
            };

            CodexPromptState threatStartedPrompt = CodexPromptStateBuilder.Build(
                currentThreatSnapshot,
                previousSafeSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);
            CodexPromptState threatClearedPrompt = CodexPromptStateBuilder.Build(
                currentSafeSnapshot,
                previousThreatSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(threatStartedPrompt.events, Does.Contain("projectile_threat_spiked"));
            Assert.That(threatClearedPrompt.events, Does.Contain("projectile_threat_cleared"));
        }

        [Test]
        public void Build_AddsTargetMeleeTransitionEvents()
        {
            var previousSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 30,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = false,
                },
            };
            var currentMeleeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 31,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = true,
                },
            };
            var previousMeleeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 32,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = true,
                },
            };
            var currentSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 33,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingMelee = false,
                },
            };

            CodexPromptState meleeStartedPrompt = CodexPromptStateBuilder.Build(
                currentMeleeSnapshot,
                previousSafeSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);
            CodexPromptState meleeEndedPrompt = CodexPromptStateBuilder.Build(
                currentSafeSnapshot,
                previousMeleeSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(meleeStartedPrompt.events, Does.Contain("target_started_melee"));
            Assert.That(meleeEndedPrompt.events, Does.Contain("target_stopped_melee"));
        }

        [Test]
        public void Build_AddsTargetRangedTransitionEvents()
        {
            var previousSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 34,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = false,
                    targetVulnerable = true,
                },
            };
            var currentRangedSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 35,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    targetVulnerable = true,
                },
            };
            var previousRangedSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 36,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = true,
                    targetVulnerable = true,
                },
            };
            var currentSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 37,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingRanged = false,
                    targetVulnerable = true,
                },
            };

            CodexPromptState rangedStartedPrompt = CodexPromptStateBuilder.Build(
                currentRangedSnapshot,
                previousSafeSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);
            CodexPromptState rangedEndedPrompt = CodexPromptStateBuilder.Build(
                currentSafeSnapshot,
                previousRangedSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(rangedStartedPrompt.events, Does.Contain("target_started_ranged"));
            Assert.That(rangedEndedPrompt.events, Does.Contain("target_stopped_ranged"));
        }

        [Test]
        public void Build_AddsTargetUltimateTransitionEvents()
        {
            var previousSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 38,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = false,
                },
            };
            var currentUltimateSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 39,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = true,
                },
            };
            var previousUltimateSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 40,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = true,
                },
            };
            var currentSafeSnapshot = new AiArenaSnapshotEnvelope
            {
                frame = 41,
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetUsingUltimate = false,
                },
            };

            CodexPromptState ultimateStartedPrompt = CodexPromptStateBuilder.Build(
                currentUltimateSnapshot,
                previousSafeSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);
            CodexPromptState ultimateEndedPrompt = CodexPromptStateBuilder.Build(
                currentSafeSnapshot,
                previousUltimateSnapshot,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(ultimateStartedPrompt.events, Does.Contain("target_started_ultimate"));
            Assert.That(ultimateEndedPrompt.events, Does.Contain("target_stopped_ultimate"));
        }

        [Test]
        public void Build_FiltersProjectileThreatsByEtaAndLateralDistance()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 12,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    isGrounded = true,
                    position = Vector2.zero,
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        sourceSlotId = 2,
                        position = new Vector2(-10f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        sourceSlotId = 3,
                        position = new Vector2(-60f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        sourceSlotId = 4,
                        position = new Vector2(-12f, 200f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        sourceSlotId = 5,
                        position = new Vector2(200f, -20f),
                        velocity = new Vector2(0f, 120f),
                        travelDirection = Vector2.up,
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.dangerousProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.dangerousProjectiles[0].sourceSlotId, Is.EqualTo(2));
            Assert.That(prompt.dangerousProjectiles[0].etaSeconds, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(prompt.dangerousProjectiles[0].position, Is.EqualTo(new Vector2(-10f, 0f)));
            Assert.That(prompt.dangerousProjectiles[0].travelDirection, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void Build_SeparatesRecoverableProjectilesFromImmediateThreats()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 24,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isStuck = true,
                        isDisarmed = false,
                        isCollectible = true,
                        sourceSlotId = 1,
                        position = new Vector2(32f, 0f),
                    },
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        isCollectible = false,
                        sourceSlotId = 2,
                        position = new Vector2(-10f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.recoverableProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.recoverableProjectiles[0].sourceSlotId, Is.EqualTo(1));
            Assert.That(prompt.recoverableProjectiles[0].distanceToSelf, Is.EqualTo(32f).Within(0.001f));
            Assert.That(prompt.recoverableProjectiles[0].position, Is.EqualTo(new Vector2(32f, 0f)));
            Assert.That(prompt.dangerousProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.dangerousProjectiles[0].sourceSlotId, Is.EqualTo(2));
        }

        [Test]
        public void Build_IgnoresOwnFlyingProjectilesAsImmediateThreats()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 26,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        isCollectible = false,
                        sourceSlotId = 1,
                        position = new Vector2(-10f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.dangerousProjectiles, Is.Empty);
        }

        [Test]
        public void Build_TreatsCollectibleFlyingProjectileAsRecoveryInsteadOfImmediateThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 27,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        isCollectible = true,
                        sourceSlotId = 2,
                        position = new Vector2(-10f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.recoverableProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.recoverableProjectiles[0].sourceSlotId, Is.EqualTo(2));
            Assert.That(prompt.recoverableProjectiles[0].distanceToSelf, Is.EqualTo(10f).Within(0.001f));
            Assert.That(prompt.recoverableProjectiles[0].position, Is.EqualTo(new Vector2(-10f, 0f)));
            Assert.That(prompt.dangerousProjectiles, Is.Empty);
        }

        [Test]
        public void Build_IgnoresOutOfBoundsRecoverableProjectiles()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 28,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation
                {
                    wrapXMin = -400f,
                    wrapXMax = 400f,
                    wrapYMin = -200f,
                    wrapYMax = 200f,
                },
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isCollectible = true,
                        sourceSlotId = 2,
                        position = new Vector2(-460f, 0f),
                    },
                    new AiArenaProjectileObservation
                    {
                        isCollectible = true,
                        sourceSlotId = 2,
                        position = new Vector2(180f, 0f),
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.recoverableProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.recoverableProjectiles[0].distanceToSelf, Is.EqualTo(180f).Within(0.001f));
            Assert.That(prompt.recoverableProjectiles[0].position, Is.EqualTo(new Vector2(180f, 0f)));
        }

        [Test]
        public void Build_AllowsRecoverableProjectileOnArenaEdge()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                frame = 29,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = new Vector2(320f, 0f),
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation
                {
                    wrapXMin = -400f,
                    wrapXMax = 400f,
                    wrapYMin = -200f,
                    wrapYMax = 200f,
                },
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isCollectible = true,
                        sourceSlotId = 2,
                        position = new Vector2(400f, 0f),
                    },
                },
            };

            CodexPromptState prompt = CodexPromptStateBuilder.Build(
                snapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(prompt.recoverableProjectiles, Has.Count.EqualTo(1));
            Assert.That(prompt.recoverableProjectiles[0].distanceToSelf, Is.EqualTo(80f).Within(0.001f));
            Assert.That(prompt.recoverableProjectiles[0].position, Is.EqualTo(new Vector2(400f, 0f)));
        }

        [Test]
        public void Build_UsesRelativeVelocityForProjectileEta()
        {
            var stationarySnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = Vector2.zero,
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = new List<AiArenaProjectileObservation>
                {
                    new AiArenaProjectileObservation
                    {
                        isStuck = false,
                        isDisarmed = false,
                        sourceSlotId = 2,
                        position = new Vector2(-20f, 0f),
                        velocity = new Vector2(100f, 0f),
                        travelDirection = Vector2.right,
                    },
                },
            };

            var movingSnapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    position = Vector2.zero,
                    velocity = new Vector2(-50f, 0f),
                },
                arena = new AiArenaArenaObservation(),
                semantics = new AiArenaSemanticObservation(),
                projectiles = stationarySnapshot.projectiles,
            };

            CodexPromptState stationaryPrompt = CodexPromptStateBuilder.Build(
                stationarySnapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            CodexPromptState movingPrompt = CodexPromptStateBuilder.Build(
                movingSnapshot,
                previousSnapshot: null,
                fallbackFrame: 0,
                memoryHistory: null);

            Assert.That(stationaryPrompt.dangerousProjectiles, Has.Count.EqualTo(1));
            Assert.That(movingPrompt.dangerousProjectiles, Has.Count.EqualTo(1));
            Assert.That(movingPrompt.dangerousProjectiles[0].etaSeconds, Is.LessThan(stationaryPrompt.dangerousProjectiles[0].etaSeconds));
        }
    }
}
