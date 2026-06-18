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
            CodexReportedInputFrame reportedInput,
            AiArenaSnapshotEnvelope reportedInputSnapshot = null)
        {
            CodexReportedInputFrame resolvedReportedInput = reportedInput != null ? reportedInput : new CodexReportedInputFrame();
            AiArenaSnapshotEnvelope feedbackSnapshot = ShouldPreferCurrentFeedbackSnapshot(snapshot)
                ? snapshot
                : reportedInputSnapshot ?? snapshot;

            return new CodexExecutorFeedback
            {
                source = lastExecutorSource,
                summary = ResolveCurrentAwareSummary(lastExecutorSummary, snapshot, reportedInputSnapshot),
                botFeedback = ResolveCurrentAwareBotFeedback(snapshot, reportedInputSnapshot, feedbackSnapshot, lastExecutorSummary, resolvedReportedInput),
                intentMode = currentIntent != null ? currentIntent.mode : string.Empty,
                intentReason = currentIntent != null ? currentIntent.reason : string.Empty,
                projectileThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.incomingProjectileThreat,
                targetMeleeThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetUsingMelee,
                targetRangedThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetUsingRanged,
                targetUltimateThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetUsingUltimate,
                selfCornered = snapshot != null && snapshot.semantics != null && snapshot.semantics.selfCornered,
                targetCornered = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetCornered,
                targetVisible = snapshot != null && snapshot.semantics != null && snapshot.semantics.hasTarget,
                roundResetPending = snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending,
                recoverableProjectileAvailable = CountRecoverableProjectiles(snapshot) > 0,
                recoverableProjectileCount = CountRecoverableProjectiles(snapshot),
                nearestRecoverableProjectileDistance = ResolveNearestRecoverableProjectileDistance(snapshot),
                intentAgeMs = intentAgeMs,
                reportedInput = resolvedReportedInput,
            };
        }

        private static string ResolveCurrentAwareBotFeedback(
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSnapshotEnvelope reportedInputSnapshot,
            AiArenaSnapshotEnvelope feedbackSnapshot,
            string lastExecutorSummary,
            CodexReportedInputFrame reportedInput)
        {
            if (IsNewCurrentProjectileThreat(snapshot, reportedInputSnapshot))
            {
                return "projectile threat active now; action pending; improve: defend before attacking.";
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                return "corner pressure active now; action pending; improve: escape toward arena center before attacking.";
            }

            return AiArenaBotFeedbackBuilder.Build(feedbackSnapshot, lastExecutorSummary, reportedInput);
        }

        private static string ResolveCurrentAwareSummary(
            string lastExecutorSummary,
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            if (snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending)
            {
                return "AI | Fallback:round_reset";
            }

            if (snapshot != null && snapshot.semantics != null && !snapshot.semantics.hasTarget)
            {
                return "AI | Fallback:no_target";
            }

            if (IsNewCurrentProjectileThreat(snapshot, reportedInputSnapshot))
            {
                return "AI PROJECTILE THREAT";
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                return "AI CORNER THREAT";
            }

            return lastExecutorSummary;
        }

        private static bool IsNewCurrentProjectileThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.incomingProjectileThreat
                && (reportedInputSnapshot == null
                    || reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.incomingProjectileThreat);
        }

        private static bool IsNewCurrentCornerThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.hasTarget
                && snapshot.semantics.selfCornered
                && (reportedInputSnapshot == null
                    || reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.selfCornered);
        }

        private static bool ShouldPreferCurrentFeedbackSnapshot(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (snapshot.arena != null && snapshot.arena.roundResetPending)
            {
                return true;
            }

            return snapshot.semantics != null && !snapshot.semantics.hasTarget;
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
