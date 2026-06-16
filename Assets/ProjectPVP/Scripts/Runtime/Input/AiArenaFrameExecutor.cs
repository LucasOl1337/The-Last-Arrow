using UnityEngine;

namespace ProjectPVP.Input
{
    internal struct AiArenaExecutionState
    {
        public float shootCooldownLeft;
        public float meleeCooldownLeft;
        public float jumpCooldownLeft;
        public float dashCooldownLeft;
        public float ultimateCooldownLeft;
        public int fallbackCount;
    }

    internal static class AiArenaFrameExecutor
    {
        public static void Tick(ref AiArenaExecutionState state, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            state.shootCooldownLeft = Mathf.Max(0f, state.shootCooldownLeft - deltaTime);
            state.meleeCooldownLeft = Mathf.Max(0f, state.meleeCooldownLeft - deltaTime);
            state.jumpCooldownLeft = Mathf.Max(0f, state.jumpCooldownLeft - deltaTime);
            state.dashCooldownLeft = Mathf.Max(0f, state.dashCooldownLeft - deltaTime);
            state.ultimateCooldownLeft = Mathf.Max(0f, state.ultimateCooldownLeft - deltaTime);
        }

        public static PlayerInputFrame BuildFrame(
            ref AiArenaExecutionState state,
            AiArenaControllerSnapshot self,
            AiArenaSnapshotEnvelope snapshot,
            AiArenaDecisionEnvelope decision,
            int frameIndex,
            float shootInterval,
            float meleeInterval,
            float jumpInterval,
            float dashInterval,
            float ultimateInterval,
            ref string debugSummary)
        {
            if (snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending)
            {
                return BuildFallbackFrame(ref state, self, frameIndex, "AI | Fallback:round_reset", ref debugSummary);
            }

            if (!self.isValid || self.isDead || snapshot == null || snapshot.semantics == null || !snapshot.semantics.hasTarget)
            {
                return BuildFallbackFrame(ref state, self, frameIndex, "AI | Fallback:no_target", ref debugSummary);
            }

            bool canShoot = self.arrows > 0;
            bool shootPressed = canShoot && self.shootCooldownLeft <= 0.01f && state.shootCooldownLeft <= 0f && decision.shootPressed;
            bool meleePressed = self.meleeCooldownLeft <= 0.01f && !self.isMeleeActive && state.meleeCooldownLeft <= 0f && decision.meleePressed;
            bool ultimatePressed = self.ultimateCooldownLeft <= 0.01f && !self.isUltimateActive && state.ultimateCooldownLeft <= 0f && decision.ultimatePressed;
            bool jumpPressed = self.isGrounded && state.jumpCooldownLeft <= 0f && decision.jumpPressed;
            bool dashPressed = self.dashCooldownLeft <= 0.01f && !self.isDashing && state.dashCooldownLeft <= 0f && decision.dashPrimaryPressed;

            if (shootPressed)
            {
                state.shootCooldownLeft = shootInterval;
            }

            if (meleePressed)
            {
                state.meleeCooldownLeft = meleeInterval;
            }

            if (ultimatePressed)
            {
                state.ultimateCooldownLeft = ultimateInterval;
            }

            if (jumpPressed)
            {
                state.jumpCooldownLeft = jumpInterval;
            }

            if (dashPressed)
            {
                state.dashCooldownLeft = dashInterval;
            }

            Vector2 aim = new Vector2(decision.aimX, decision.aimY);
            if (aim.sqrMagnitude <= 0.0001f)
            {
                aim = snapshot.semantics.targetDirection.sqrMagnitude > 0.0001f
                    ? snapshot.semantics.targetDirection
                    : new Vector2(self.facing, 0f);
            }

            if (aim.sqrMagnitude > 1f)
            {
                aim.Normalize();
            }

            float axis = Mathf.Clamp(decision.moveAxis, -1f, 1f);
            debugSummary = string.IsNullOrWhiteSpace(decision.debugSummary)
                ? "AI | OK"
                : decision.debugSummary;

            return new PlayerInputFrame
            {
                frame = frameIndex,
                axis = axis,
                aim = aim,
                left = axis < -0.1f,
                right = axis > 0.1f,
                up = jumpPressed || snapshot.semantics.targetAbove,
                down = snapshot.semantics.targetBelow || snapshot.semantics.shouldDashEvade,
                jumpPressed = jumpPressed,
                jumpHeld = jumpPressed || decision.jumpHeld,
                shootPressed = shootPressed,
                shootHeld = canShoot && decision.shootHeld,
                meleePressed = meleePressed,
                ultimatePressed = ultimatePressed,
                dashPrimaryPressed = dashPressed,
                dashSecondaryPressed = decision.dashSecondaryPressed,
            };
        }

        public static PlayerInputFrame BuildFallbackFrame(
            ref AiArenaExecutionState state,
            AiArenaControllerSnapshot self,
            int frameIndex,
            string reason,
            ref string debugSummary)
        {
            state.fallbackCount += 1;
            debugSummary = string.IsNullOrWhiteSpace(reason)
                ? "AI | Fallback"
                : reason + " | Count:" + state.fallbackCount;

            Vector2 aim = new Vector2(self.facing == 0 ? 1 : self.facing, 0f);
            return new PlayerInputFrame
            {
                frame = frameIndex,
                aim = aim,
            };
        }
    }
}
