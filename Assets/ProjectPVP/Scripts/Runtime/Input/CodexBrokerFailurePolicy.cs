namespace ProjectPVP.Input
{
    internal static class CodexBrokerFailurePolicy
    {
        private const int FailureThreshold = 6;
        private const float SuccessGraceWindowMs = 5000f;

        internal static bool ShouldInvalidateSession(
            string sessionId,
            int consecutiveBrokerFailures,
            float lastBrokerSuccessTime,
            float now)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return true;
            }

            if (consecutiveBrokerFailures < FailureThreshold)
            {
                return false;
            }

            float elapsedSinceSuccessMs = lastBrokerSuccessTime < 0f
                ? float.MaxValue
                : (now - lastBrokerSuccessTime) * 1000f;
            return elapsedSinceSuccessMs >= SuccessGraceWindowMs;
        }
    }
}
