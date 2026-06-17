using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaFrameExecutorTests
    {
        [Test]
        public void BuildFrame_ConvertsShootRequestIntoAReleaseableHoldCycle()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                arrows = 3,
                shootCooldownLeft = 0f,
                position = Vector2.zero,
            };
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = self.slotId,
                    facing = self.facing,
                    isGrounded = self.isGrounded,
                    arrows = self.arrows,
                    shootCooldownLeft = self.shootCooldownLeft,
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                shootPressed = true,
                shootHeld = true,
                aimX = 1f,
                aimY = 0f,
            };

            AiArenaExecutionState state = default;
            string debugSummary = string.Empty;

            PlayerInputFrame firstFrame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 0,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(firstFrame.shootPressed, Is.True);
            Assert.That(firstFrame.shootHeld, Is.True);

            AiArenaFrameExecutor.Tick(ref state, 0.02f);

            PlayerInputFrame secondFrame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 1,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(secondFrame.shootPressed, Is.False);
            Assert.That(secondFrame.shootHeld, Is.False);
            Assert.That(state.shootHoldLeft, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
