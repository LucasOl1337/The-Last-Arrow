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
            return new CodexExecutorFeedback
            {
                source = lastExecutorSource,
                summary = lastExecutorSummary,
                intentMode = currentIntent != null ? currentIntent.mode : string.Empty,
                intentReason = currentIntent != null ? currentIntent.reason : string.Empty,
                projectileThreatActive = snapshot != null && snapshot.semantics != null && snapshot.semantics.incomingProjectileThreat,
                targetVisible = snapshot != null && snapshot.semantics != null && snapshot.semantics.hasTarget,
                roundResetPending = snapshot != null && snapshot.arena != null && snapshot.arena.roundResetPending,
                intentAgeMs = intentAgeMs,
                reportedInput = reportedInput != null ? reportedInput : new CodexReportedInputFrame(),
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
                return string.Empty;
            }

            return useAgentDrivenMode ? "Codex" : "CodexDirect";
        }
    }
}
