using NUnit.Framework;
using ProjectPVP.Input;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexBrokerFailurePolicyTests
    {
        [Test]
        public void ShouldInvalidateSession_ReturnsTrueWhenSessionIdIsMissing()
        {
            bool shouldInvalidate = CodexBrokerFailurePolicy.ShouldInvalidateSession(
                string.Empty,
                consecutiveBrokerFailures: 1,
                lastBrokerSuccessTime: 10f,
                now: 12f);

            Assert.That(shouldInvalidate, Is.True);
        }

        [Test]
        public void ShouldInvalidateSession_ReturnsFalseBeforeFailureThreshold()
        {
            bool shouldInvalidate = CodexBrokerFailurePolicy.ShouldInvalidateSession(
                "session-1",
                consecutiveBrokerFailures: 5,
                lastBrokerSuccessTime: 1f,
                now: 10f);

            Assert.That(shouldInvalidate, Is.False);
        }

        [Test]
        public void ShouldInvalidateSession_ReturnsFalseWhenSuccessIsStillWithinGraceWindow()
        {
            bool shouldInvalidate = CodexBrokerFailurePolicy.ShouldInvalidateSession(
                "session-1",
                consecutiveBrokerFailures: 6,
                lastBrokerSuccessTime: 8f,
                now: 12f);

            Assert.That(shouldInvalidate, Is.False);
        }

        [Test]
        public void ShouldInvalidateSession_ReturnsTrueWhenThresholdIsExceededAndSuccessIsStale()
        {
            bool shouldInvalidate = CodexBrokerFailurePolicy.ShouldInvalidateSession(
                "session-1",
                consecutiveBrokerFailures: 6,
                lastBrokerSuccessTime: 1f,
                now: 10f);

            Assert.That(shouldInvalidate, Is.True);
        }
    }
}
