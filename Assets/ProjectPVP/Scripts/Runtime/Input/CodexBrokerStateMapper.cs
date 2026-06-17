using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class CodexBrokerStateMapper
    {
        internal static CodexExecutorFeedback BuildExecutorFeedback(
            string lastExecutorSource,
            string lastExecutorSummary,
            CodexStrategyIntent currentIntent,
            AiArenaSnapshotEnvelope snapshot,
            float intentAgeMs,
            CodexReportedInputFrame reportedInput)
        {
            CodexReportedInputFrame resolvedReportedInput = reportedInput != null ? reportedInput : new CodexReportedInputFrame();

            return new CodexExecutorFeedback
            {
                source = lastExecutorSource,
                summary = lastExecutorSummary,
                botFeedback = AiArenaBotFeedbackBuilder.Build(
                    snapshot,
                    BuildFeedbackDecision(lastExecutorSummary, resolvedReportedInput)),
                intentMode = currentIntent != null ? currentIntent.mode : string.Empty,
                intentReason = currentIntent != null ? currentIntent.reason : string.Empty,
                projectileThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.incomingProjectileThreat,
                targetVisible = snapshot != null && snapshot.semantics != null && snapshot.semantics.hasTarget,
                roundResetPending = snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending,
                recoverableProjectileAvailable = CountRecoverableProjectiles(snapshot) > 0,
                recoverableProjectileCount = CountRecoverableProjectiles(snapshot),
                nearestRecoverableProjectileDistance = ResolveNearestRecoverableProjectileDistance(snapshot),
                intentAgeMs = intentAgeMs,
                reportedInput = resolvedReportedInput,
            };
        }

        internal static string ResolveControllerOwner(string envelopeOwner, bool hasExecutableIntent, bool useAgentDrivenMode)
        {
            if (!string.IsNullOrWhiteSpace(envelopeOwner))
            {
                return envelopeOwner;
            }

            if (!hasExecutableIntent)
            {
                return "BrokerDefault";
            }

            return useAgentDrivenMode ? "Codex" : "CodexDirect";
        }

        private static AiArenaDecisionEnvelope BuildFeedbackDecision(string lastExecutorSummary, CodexReportedInputFrame reportedInput)
        {
            return new AiArenaDecisionEnvelope
            {
                debugSummary = lastExecutorSummary,
                moveAxis = reportedInput.axis,
                aimX = reportedInput.aim.x,
                aimY = reportedInput.aim.y,
                jumpPressed = reportedInput.jumpPressed,
                jumpHeld = reportedInput.jumpHeld,
                shootPressed = reportedInput.shootPressed,
                shootHeld = reportedInput.shootHeld,
                meleePressed = reportedInput.meleePressed,
                ultimatePressed = reportedInput.ultimatePressed,
                dashPrimaryPressed = reportedInput.dashPrimaryPressed,
                dashSecondaryPressed = reportedInput.dashSecondaryPressed,
            };
        }

        private static int CountRecoverableProjectiles(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null || snapshot.projectiles == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < snapshot.projectiles.Count; index += 1)
            {
                AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                if (projectile == null || !projectile.isCollectible)
                {
                    continue;
                }

                count += 1;
            }

            return count;
        }

        private static float ResolveNearestRecoverableProjectileDistance(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null || snapshot.self == null || snapshot.projectiles == null)
            {
                return -1f;
            }

            float bestDistance = float.MaxValue;
            for (int index = 0; index < snapshot.projectiles.Count; index += 1)
            {
                AiArenaProjectileObservation projectile = snapshot.projectiles[index];
                if (projectile == null || !projectile.isCollectible)
                {
                    continue;
                }

                float distance = Vector2.Distance(snapshot.self.position, projectile.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
            }

            return bestDistance == float.MaxValue ? -1f : bestDistance;
        }
    }
}
