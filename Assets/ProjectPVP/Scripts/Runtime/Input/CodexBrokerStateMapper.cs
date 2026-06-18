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
                summary = ResolveCurrentAwareSummary(lastExecutorSummary, currentIntent, snapshot, reportedInputSnapshot, resolvedReportedInput),
                botFeedback = ResolveCurrentAwareBotFeedback(snapshot, reportedInputSnapshot, feedbackSnapshot, currentIntent, lastExecutorSummary, resolvedReportedInput),
                intentMode = ResolveCurrentAwareIntentMode(currentIntent, snapshot),
                intentReason = ResolveCurrentAwareIntentReason(currentIntent, snapshot),
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
                return "projectile threat active now; action " + ResolveReportedAction(reportedInput, null)
                    + "; improve: defend before attacking.";
            }

            if (IsCurrentProjectileThreat(snapshot) && snapshot.semantics != null && !snapshot.semantics.hasTarget)
            {
                return "projectile threat active now; action " + ResolveReportedAction(reportedInput, null)
                    + "; improve: defend before attacking.";
            }

            if (IsNewCurrentRangedThreat(snapshot, reportedInputSnapshot))
            {
                return "ranged threat active now; action " + ResolveReportedAction(reportedInput, null)
                    + "; improve: dodge, break line, or interrupt before chasing pickups.";
            }

            if (IsCurrentRangedThreat(snapshot))
            {
                string builtFeedback = AiArenaBotFeedbackBuilder.Build(feedbackSnapshot, lastExecutorSummary, reportedInput);
                if (builtFeedback.IndexOf("ranged threat active now", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "ranged threat active now; action " + ResolveReportedAction(reportedInput, builtFeedback)
                        + "; improve: dodge, break line, or interrupt before chasing pickups.";
                }

                return builtFeedback;
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                if (IsStalledCornerEscapeInput(snapshot, reportedInput))
                {
                    return "corner escape stalled; action weak or wrong-way movement; improve: move decisively toward arena center before attacking.";
                }

                return "corner pressure active now; action pending; improve: escape toward arena center before attacking.";
            }

            if (IsCurrentResolvedCornerPressure(snapshot, lastExecutorSummary))
            {
                return "corner pressure resolved; action pending; improve: retake center control before committing.";
            }

            if (IsCurrentAntiAirShot(snapshot, lastExecutorSummary))
            {
                string builtFeedback = AiArenaBotFeedbackBuilder.Build(snapshot, lastExecutorSummary, reportedInput);
                return "anti-air shot active now; action " + ResolveReportedAction(reportedInput, builtFeedback)
                    + "; improve: take the vertical shot before repositioning.";
            }

            if (IsCurrentAntiAirChase(snapshot, currentIntent, lastExecutorSummary))
            {
                if (IsStalledAntiAirChaseInput(reportedInput))
                {
                    return "anti-air chase stalled; action grounded advance; improve: hold jump or aim upward while closing vertical distance.";
                }

                return "anti-air chase active now; action " + ResolveReportedAction(reportedInput, null)
                    + "; improve: climb into range before spending arrows.";
            }

            if (IsCurrentLastArrowPressure(snapshot, lastExecutorSummary))
            {
                if (IsStalledLastArrowPressureInput(reportedInput))
                {
                    return "last-arrow pressure stalled; action none; improve: shoot, dash in, or move into a clean shot before the target recovers arrows.";
                }

                return "last-arrow pressure active now; action " + ResolveReportedAction(reportedInput, null)
                    + "; improve: spend the ammo advantage before the target recovers arrows.";
            }

            if (IsCurrentResolvedThreatPressure(snapshot, currentIntent, lastExecutorSummary))
            {
                return "resolved threat pressure active now; action pending; improve: stop retreating and retake the shot window.";
            }

            return AiArenaBotFeedbackBuilder.Build(feedbackSnapshot, lastExecutorSummary, reportedInput);
        }

        private static string ResolveReportedAction(CodexReportedInputFrame reportedInput, string builtFeedback)
        {
            if (reportedInput != null)
            {
                if (reportedInput.ultimatePressed)
                {
                    return "AI ULTIMATE";
                }

                if (reportedInput.meleePressed)
                {
                    return "AI MELEE";
                }

                if (reportedInput.shootPressed || reportedInput.shootHeld)
                {
                    return "AI SHOOT";
                }

                if (reportedInput.dashPrimaryPressed || reportedInput.dashSecondaryPressed)
                {
                    return "AI DASH";
                }

                if (reportedInput.jumpPressed || reportedInput.jumpHeld)
                {
                    return "AI JUMP";
                }

                if (Mathf.Abs(reportedInput.axis) > 0.1f)
                {
                    return "AI MOVE";
                }
            }

            const string marker = "action ";
            if (string.IsNullOrWhiteSpace(builtFeedback))
            {
                return "pending";
            }

            int start = builtFeedback.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return "pending";
            }

            start += marker.Length;
            int end = builtFeedback.IndexOf(';', start);
            if (end < 0)
            {
                end = builtFeedback.Length;
            }

            string action = builtFeedback.Substring(start, end - start).Trim();
            return string.IsNullOrWhiteSpace(action) ? "pending" : action;
        }

        private static string ResolveCurrentAwareSummary(
            string lastExecutorSummary,
            CodexStrategyIntent currentIntent,
            AiArenaSnapshotEnvelope snapshot,
            AiArenaSnapshotEnvelope reportedInputSnapshot,
            CodexReportedInputFrame reportedInput)
        {
            if (snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending && string.IsNullOrWhiteSpace(lastExecutorSummary))
            {
                return "AI | Fallback:round_reset";
            }

            if (IsNewCurrentProjectileThreat(snapshot, reportedInputSnapshot)
                || (IsCurrentProjectileThreat(snapshot) && snapshot.semantics != null && !snapshot.semantics.hasTarget))
            {
                return "AI PROJECTILE THREAT";
            }

            if (snapshot != null && snapshot.semantics != null && !snapshot.semantics.hasTarget)
            {
                return "AI | Fallback:no_target";
            }

            if (IsNewCurrentRangedThreat(snapshot, reportedInputSnapshot))
            {
                return "AI RANGED THREAT";
            }

            if (IsNewCurrentCornerThreat(snapshot, reportedInputSnapshot))
            {
                if (IsStalledCornerEscapeInput(snapshot, reportedInput))
                {
                    return "AI CORNER STALLED";
                }

                return "AI CORNER THREAT";
            }

            if (IsCurrentResolvedCornerPressure(snapshot, lastExecutorSummary))
            {
                return "AI RESOLVED CORNER PRESSURE";
            }

            if (IsCurrentAntiAirShot(snapshot, lastExecutorSummary))
            {
                return "AI ANTI AIR";
            }

            if (IsCurrentAntiAirChase(snapshot, currentIntent, lastExecutorSummary))
            {
                if (IsStalledAntiAirChaseInput(reportedInput))
                {
                    return "AI ANTI AIR STALLED";
                }

                return "AI ANTI AIR CHASE";
            }

            if (IsCurrentLastArrowPressure(snapshot, lastExecutorSummary))
            {
                if (IsStalledLastArrowPressureInput(reportedInput))
                {
                    return "AI LAST ARROW STALLED";
                }

                return "AI LAST ARROW PRESSURE";
            }

            if (IsCurrentResolvedThreatPressure(snapshot, currentIntent, lastExecutorSummary))
            {
                return "AI RESOLVED THREAT PRESSURE";
            }

            if (string.IsNullOrWhiteSpace(lastExecutorSummary))
            {
                return "AI MOVE";
            }

            return lastExecutorSummary;
        }

        private static string ResolveCurrentAwareIntentMode(CodexStrategyIntent currentIntent, AiArenaSnapshotEnvelope snapshot)
        {
            if (IsCurrentProjectileThreat(snapshot) && snapshot.semantics != null && !snapshot.semantics.hasTarget)
            {
                return "retreat";
            }

            if (IsNoTarget(snapshot))
            {
                return "stabilize";
            }

            if (IsCurrentRangedThreat(snapshot))
            {
                return ResolveSelfArrows(snapshot) > 0 ? "pressure" : "retreat";
            }

            return currentIntent != null ? currentIntent.mode : string.Empty;
        }

        private static string ResolveCurrentAwareIntentReason(CodexStrategyIntent currentIntent, AiArenaSnapshotEnvelope snapshot)
        {
            if (IsCurrentProjectileThreat(snapshot) && snapshot.semantics != null && !snapshot.semantics.hasTarget)
            {
                return "projectile_threat_feedback";
            }

            if (IsNoTarget(snapshot))
            {
                return "heuristic_waiting_for_target";
            }

            if (IsCurrentRangedThreat(snapshot))
            {
                return "target_ranged_threat";
            }

            return currentIntent != null ? currentIntent.reason : string.Empty;
        }

        private static bool IsRoundResetOrNoTarget(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null
                && ((snapshot.arena != null && snapshot.arena.roundResetPending)
                    || (snapshot.semantics != null && !snapshot.semantics.hasTarget));
        }

        private static bool IsNoTarget(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && !snapshot.semantics.hasTarget;
        }

        private static bool IsNewCurrentProjectileThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.incomingProjectileThreat
                && reportedInputSnapshot != null
                && (reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.incomingProjectileThreat);
        }

        private static bool IsCurrentProjectileThreat(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.incomingProjectileThreat;
        }

        private static bool IsNewCurrentRangedThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.hasTarget
                && snapshot.semantics.targetUsingRanged
                && !snapshot.semantics.incomingProjectileThreat
                && reportedInputSnapshot != null
                && (reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.targetUsingRanged);
        }

        private static bool IsCurrentRangedThreat(AiArenaSnapshotEnvelope snapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.hasTarget
                && snapshot.semantics.targetUsingRanged
                && !snapshot.semantics.incomingProjectileThreat;
        }

        private static bool IsNewCurrentCornerThreat(AiArenaSnapshotEnvelope snapshot, AiArenaSnapshotEnvelope reportedInputSnapshot)
        {
            return snapshot != null
                && snapshot.semantics != null
                && snapshot.semantics.hasTarget
                && snapshot.semantics.selfCornered
                && reportedInputSnapshot != null
                && (reportedInputSnapshot.semantics == null
                    || !reportedInputSnapshot.semantics.selfCornered);
        }

        private static bool IsCurrentResolvedCornerPressure(AiArenaSnapshotEnvelope snapshot, string lastExecutorSummary)
        {
            if (snapshot == null
                || snapshot.semantics == null
                || !snapshot.semantics.hasTarget
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingRanged
                || snapshot.semantics.targetUsingMelee
                || snapshot.semantics.targetUsingUltimate
                || snapshot.semantics.selfCornered
                || snapshot.semantics.targetCornered
                || string.IsNullOrWhiteSpace(lastExecutorSummary))
            {
                return false;
            }

            return lastExecutorSummary.IndexOf("corner", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
                || (!snapshot.semantics.shouldAntiAir && snapshot.semantics.verticalDistance < 96f)
                || snapshot.self.arrows <= 0
                || !IsAntiAirIntent(currentIntent))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(lastExecutorSummary)
                || lastExecutorSummary.IndexOf("anti air", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsCurrentAntiAirShot(AiArenaSnapshotEnvelope snapshot, string lastExecutorSummary)
        {
            if (snapshot == null
                || snapshot.semantics == null
                || snapshot.self == null
                || !snapshot.semantics.hasTarget
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingRanged
                || snapshot.semantics.targetUsingUltimate
                || !snapshot.semantics.targetAbove
                || !snapshot.semantics.targetInShootRange
                || !snapshot.semantics.shouldAntiAir
                || snapshot.self.arrows <= 0)
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(lastExecutorSummary)
                || lastExecutorSummary.IndexOf("anti air", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsCurrentLastArrowPressure(AiArenaSnapshotEnvelope snapshot, string lastExecutorSummary)
        {
            if (snapshot == null
                || snapshot.semantics == null
                || snapshot.self == null
                || !snapshot.semantics.hasTarget
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingRanged
                || snapshot.semantics.targetUsingMelee
                || snapshot.semantics.targetUsingUltimate
                || snapshot.semantics.selfCornered
                || snapshot.self.arrows <= 0
                || ResolveTargetArrows(snapshot) != 0)
            {
                return false;
            }

            return true;
        }

        private static bool IsStalledLastArrowPressureInput(CodexReportedInputFrame reportedInput)
        {
            return reportedInput != null
                && Mathf.Abs(reportedInput.axis) <= 0.1f
                && !reportedInput.shootPressed
                && !reportedInput.shootHeld
                && !reportedInput.meleePressed
                && !reportedInput.ultimatePressed
                && !reportedInput.dashPrimaryPressed
                && !reportedInput.dashSecondaryPressed;
        }

        private static bool IsStalledAntiAirChaseInput(CodexReportedInputFrame reportedInput)
        {
            return reportedInput == null
                || (!reportedInput.jumpPressed
                    && !reportedInput.jumpHeld
                    && reportedInput.aim.y < 0.55f);
        }

        private static bool IsStalledCornerEscapeInput(AiArenaSnapshotEnvelope snapshot, CodexReportedInputFrame reportedInput)
        {
            if (snapshot == null || snapshot.semantics == null || reportedInput == null)
            {
                return true;
            }

            float escapeAxis = Mathf.Abs(snapshot.semantics.targetDirection.x) > 0.1f
                ? Mathf.Sign(snapshot.semantics.targetDirection.x)
                : 0f;
            if (escapeAxis == 0f)
            {
                return Mathf.Abs(reportedInput.axis) < 0.35f
                    && !reportedInput.dashPrimaryPressed
                    && !reportedInput.dashSecondaryPressed;
            }

            return Mathf.Abs(reportedInput.axis) < 0.35f
                || Mathf.Sign(reportedInput.axis) != escapeAxis;
        }

        private static bool IsCurrentResolvedThreatPressure(AiArenaSnapshotEnvelope snapshot, CodexStrategyIntent currentIntent, string lastExecutorSummary)
        {
            if (snapshot == null
                || snapshot.semantics == null
                || snapshot.self == null
                || !snapshot.semantics.hasTarget
                || snapshot.semantics.incomingProjectileThreat
                || snapshot.semantics.targetUsingRanged
                || snapshot.semantics.targetUsingMelee
                || snapshot.semantics.targetUsingUltimate
                || snapshot.semantics.selfCornered
                || !snapshot.semantics.targetInShootRange
                || snapshot.self.arrows <= 0
                || !IsRangedOrProjectileThreatFeedbackIntent(currentIntent))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(lastExecutorSummary)
                || lastExecutorSummary.IndexOf("resolved threat pressure", System.StringComparison.OrdinalIgnoreCase) < 0;
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

        private static bool IsRangedOrProjectileThreatFeedbackIntent(CodexStrategyIntent intent)
        {
            if (intent == null || string.IsNullOrWhiteSpace(intent.reason))
            {
                return false;
            }

            string normalized = intent.reason.Trim().ToLowerInvariant();
            return normalized.Contains("target_ranged_threat")
                || normalized.Contains("missed_ranged_response")
                || normalized.Contains("projectile_threat_feedback")
                || normalized.Contains("missed_projectile_defense");
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
