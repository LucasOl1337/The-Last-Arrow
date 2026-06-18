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
                summary = ResolveCurrentAwareSummary(lastExecutorSummary, currentIntent, snapshot, reportedInputSnapshot),
                botFeedback = ResolveCurrentAwareBotFeedback(snapshot, reportedInputSnapshot, feedbackSnapshot, currentIntent, lastExecutorSummary, resolvedReportedInput),
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
                horizontalDistance = ResolveHorizontalDistance(snapshot),
                verticalDistance = ResolveVerticalDistance(snapshot),
                targetAbove = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetAbove,
                targetBelow = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetBelow,
                targetInShootRange = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetInShootRange,
                targetInMeleeRange = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetInMeleeRange,
                targetInUltimateRange = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetInUltimateRange,
                targetVulnerable = snapshot != null && snapshot.semantics != null && snapshot.semantics.targetVulnerable,
                shouldAntiAir = snapshot != null && snapshot.semantics != null && snapshot.semantics.shouldAntiAir,
                selfArrows = ResolveSelfArrows(snapshot),
                targetArrows = ResolveTargetArrows(snapshot),
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
            CodexStrategyIntent currentIntent,
            string lastExecutorSummary,
            CodexReportedInputFrame reportedInput)
        {
            if (IsNewCurrentProjectileThreat(snapshot, reportedInputSnapshot))
            {
                return "projectile threat active now; action pending; improve: defend before attacking.";
            }

            if (IsNewCurrentRangedThreat(snapshot, reportedInputSnapshot))
            {
                return "ranged threat active now; action pending; improve: dodge, break line, or interrupt before chasing pickups.";
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                return "corner pressure active now; action pending; improve: escape toward arena center before attacking.";
            }

            if (IsCurrentAntiAirChase(snapshot, currentIntent, lastExecutorSummary))
            {
                return "anti-air chase active now; action pending; improve: climb into range before spending arrows.";
            }

            return AiArenaBotFeedbackBuilder.Build(feedbackSnapshot, lastExecutorSummary, reportedInput);
        }

        private static string ResolveCurrentAwareSummary(
            string lastExecutorSummary,
            CodexStrategyIntent currentIntent,
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

            if (IsNewCurrentRangedThreat(snapshot, reportedInputSnapshot))
            {
                return "AI RANGED THREAT";
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                return "AI CORNER THREAT";
            }

            if (IsCurrentAntiAirChase(snapshot, currentIntent, lastExecutorSummary))
            {
                return "AI ANTI AIR CHASE";
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

        private static bool IsNewCurrentRangedThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.hasTarget
                && snapshot.semantics.targetUsingRanged
                && !snapshot.semantics.incomingProjectileThreat
                && (reportedInputSnapshot == null
                    || reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.targetUsingRanged);
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

        private static bool IsCurrentAntiAirChase(AiArenaSnapshotEnvelope snapshot, CodexStrategyIntent currentIntent, string lastExecutorSummary)
        {
            if (snapshot == null
                || snapshot.semantics == null
                || snapshot.self == null
                || !snapshot.semantics.hasTarget
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingRanged
                || snapshot.semantics.targetUsingUltimate
                || !snapshot.semantics.targetAbove
                || snapshot.semantics.targetInShootRange
                || snapshot.self.arrows <= 0
                || !IsAntiAirIntent(currentIntent))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(lastExecutorSummary)
                || lastExecutorSummary.IndexOf("anti air", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsAntiAirIntent(CodexStrategyIntent intent)
        {
            if (intent == null)
            {
                return false;
            }

            if (intent.antiAir)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(intent.reason)
                && intent.reason.Trim().ToLowerInvariant().Contains("anti_air");
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

        private static float ResolveHorizontalDistance(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null && snapshot.semantics != null ? snapshot.semantics.horizontalDistance : -1f;
        }

        private static float ResolveVerticalDistance(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null && snapshot.semantics != null ? snapshot.semantics.verticalDistance : 0f;
        }

        private static int ResolveSelfArrows(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null && snapshot.self != null ? snapshot.self.arrows : -1;
        }

        private static int ResolveTargetArrows(AiArenaSnapshotEnvelope snapshot)
        {
            if (snapshot == null || snapshot.opponents == null || snapshot.opponents.Count <= 0 || snapshot.opponents[0] == null)
            {
                return -1;
            }

            return snapshot.opponents[0].arrows;
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
