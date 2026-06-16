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
                    isValid = true,
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
    }
}
