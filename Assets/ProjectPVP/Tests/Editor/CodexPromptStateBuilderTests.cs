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
            Assert.That(prompt.events, Has.Count.EqualTo(1));
            Assert.That(prompt.events[0], Is.EqualTo("round_context_initialized"));
            Assert.That(prompt.memory, Has.Count.EqualTo(1));
            Assert.That(prompt.memory[0], Is.EqualTo("memory-1"));
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
