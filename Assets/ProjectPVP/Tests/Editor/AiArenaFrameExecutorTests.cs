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

        [Test]
        public void BuildFrame_DoesNotAddVerticalMovementWhenDecisionHoldsDefenseUnderTarget()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                arrows = 2,
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
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetAbove = true,
                    targetDirection = new Vector2(1f, 1f).normalized,
                    predictedTargetDirection = new Vector2(1f, 1f).normalized,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    shouldDashEvade = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                debugSummary = "AI PARRY HOLD",
                aimX = 1f,
                aimY = 0f,
            };

            AiArenaExecutionState state = default;
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 12,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.up, Is.False);
            Assert.That(frame.down, Is.False);
            Assert.That(frame.Movement.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(frame.jumpPressed, Is.False);
            Assert.That(frame.jumpHeld, Is.False);
        }

        [Test]
        public void BuildFrame_SuppressesOffenseDuringIncomingProjectileThreat()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                arrows = 3,
                shootCooldownLeft = 0f,
                meleeCooldownLeft = 0f,
                ultimateCooldownLeft = 0f,
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
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetInMeleeRange = true,
                    targetInUltimateRange = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.19f,
                    incomingProjectileDirection = Vector2.left,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                shootPressed = true,
                shootHeld = true,
                meleePressed = true,
                ultimatePressed = true,
                aimX = 1f,
                aimY = 0f,
                debugSummary = "AI ARROW LEAD PRESSURE",
            };
            AiArenaExecutionState state = new AiArenaExecutionState
            {
                shootHoldLeft = 0.018f,
            };
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 18,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.shootPressed, Is.False);
            Assert.That(frame.shootHeld, Is.False);
            Assert.That(frame.meleePressed, Is.False);
            Assert.That(frame.ultimatePressed, Is.False);
            Assert.That(state.shootHoldLeft, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void BuildFrame_JumpsAgainstProjectileWhenRequestedDashIsOnExecutorCooldown()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                arrows = 2,
                dashCooldownLeft = 0f,
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
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                dashPrimaryPressed = true,
                moveAxis = 1f,
                aimX = 1f,
                aimY = 0f,
                debugSummary = "AI PROJECTILE DASH",
            };
            AiArenaExecutionState state = new AiArenaExecutionState
            {
                dashCooldownLeft = 0.35f,
            };
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 19,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.dashPrimaryPressed, Is.False);
            Assert.That(frame.jumpPressed, Is.True);
            Assert.That(frame.jumpHeld, Is.True);
            Assert.That(frame.axis, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(debugSummary, Is.EqualTo("AI PROJECTILE JUMP"));
        }

        [Test]
        public void BuildFrame_ReportsProjectileDriftWhenDashAndJumpAreUnavailable()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = false,
                arrows = 2,
                dashCooldownLeft = 0f,
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
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.33f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                dashPrimaryPressed = true,
                moveAxis = 1f,
                aimX = 1f,
                aimY = 0f,
                debugSummary = "AI PROJECTILE DASH",
            };
            AiArenaExecutionState state = new AiArenaExecutionState
            {
                dashCooldownLeft = 0.35f,
            };
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 20,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.dashPrimaryPressed, Is.False);
            Assert.That(frame.jumpPressed, Is.False);
            Assert.That(frame.axis, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(debugSummary, Is.EqualTo("AI PROJECTILE DRIFT"));
        }

        [Test]
        public void BuildFrame_GatesSecondaryDashWithExecutorCooldown()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                dashCooldownLeft = 0f,
                position = Vector2.zero,
            };
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = self.slotId,
                    facing = self.facing,
                    isGrounded = self.isGrounded,
                    dashCooldownLeft = self.dashCooldownLeft,
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                dashSecondaryPressed = true,
                aimX = 1f,
                aimY = 0f,
            };

            AiArenaExecutionState state = new AiArenaExecutionState
            {
                dashCooldownLeft = 0.35f,
            };
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 24,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.dashPrimaryPressed, Is.False);
            Assert.That(frame.dashSecondaryPressed, Is.False);
            Assert.That(state.dashCooldownLeft, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void BuildFrame_StartsExecutorCooldownWhenSecondaryDashIsAccepted()
        {
            var self = new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = 1,
                facing = 1,
                isGrounded = true,
                dashCooldownLeft = 0f,
                position = Vector2.zero,
            };
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = self.slotId,
                    facing = self.facing,
                    isGrounded = self.isGrounded,
                    dashCooldownLeft = self.dashCooldownLeft,
                    position = self.position,
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                },
            };
            var decision = new AiArenaDecisionEnvelope
            {
                dashSecondaryPressed = true,
                aimX = 1f,
                aimY = 0f,
            };

            AiArenaExecutionState state = default;
            string debugSummary = string.Empty;

            PlayerInputFrame frame = AiArenaFrameExecutor.BuildFrame(
                ref state,
                self,
                snapshot,
                decision,
                frameIndex: 25,
                shootInterval: 0.25f,
                meleeInterval: 0.45f,
                jumpInterval: 0.6f,
                dashInterval: 0.85f,
                ultimateInterval: 1.5f,
                ref debugSummary);

            Assert.That(frame.dashPrimaryPressed, Is.False);
            Assert.That(frame.dashSecondaryPressed, Is.True);
            Assert.That(state.dashCooldownLeft, Is.EqualTo(0.85f).Within(0.0001f));
        }
    }
}
