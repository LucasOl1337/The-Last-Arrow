using NUnit.Framework;
using ProjectPVP.Input;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexBrokerRequestLifecycleTests
    {
        [Test]
        public void Begin_MarksRequestInFlightAndReturnsIncrementedVersion()
        {
            CodexBrokerRequestLifecycleState state = CodexBrokerRequestLifecycleState.Inactive();

            int version = CodexBrokerRequestLifecycle.Begin(ref state, startedTime: 12.5f);

            Assert.That(version, Is.EqualTo(1));
            Assert.That(state.InFlight, Is.True);
            Assert.That(state.StartedTime, Is.EqualTo(12.5f).Within(0.001f));
            Assert.That(state.Version, Is.EqualTo(1));
        }

        [Test]
        public void TryComplete_ClearsOnlyCurrentRequestVersion()
        {
            CodexBrokerRequestLifecycleState state = CodexBrokerRequestLifecycleState.Inactive();
            int currentVersion = CodexBrokerRequestLifecycle.Begin(ref state, startedTime: 3f);

            bool staleCompleted = CodexBrokerRequestLifecycle.TryComplete(ref state, currentVersion - 1);
            bool currentCompleted = CodexBrokerRequestLifecycle.TryComplete(ref state, currentVersion);

            Assert.That(staleCompleted, Is.False);
            Assert.That(currentCompleted, Is.True);
            Assert.That(state.InFlight, Is.False);
            Assert.That(state.StartedTime, Is.EqualTo(CodexBrokerRequestLifecycle.ClearedStartedTime).Within(0.001f));
            Assert.That(state.Version, Is.EqualTo(currentVersion));
        }

        [Test]
        public void Invalidate_ClearsRequestAndPreventsOldCompletion()
        {
            CodexBrokerRequestLifecycleState state = CodexBrokerRequestLifecycleState.Inactive();
            int staleVersion = CodexBrokerRequestLifecycle.Begin(ref state, startedTime: 2f);

            CodexBrokerRequestLifecycle.Invalidate(ref state);
            bool completed = CodexBrokerRequestLifecycle.TryComplete(ref state, staleVersion);

            Assert.That(completed, Is.False);
            Assert.That(state.InFlight, Is.False);
            Assert.That(state.StartedTime, Is.EqualTo(CodexBrokerRequestLifecycle.ClearedStartedTime).Within(0.001f));
            Assert.That(state.Version, Is.EqualTo(staleVersion + 1));
        }

        [Test]
        public void IsStale_RequiresActiveRequestPastWindow()
        {
            CodexBrokerRequestLifecycleState inactive = CodexBrokerRequestLifecycleState.Inactive();
            CodexBrokerRequestLifecycleState active = CodexBrokerRequestLifecycleState.Inactive();
            CodexBrokerRequestLifecycle.Begin(ref active, startedTime: 10f);

            Assert.That(CodexBrokerRequestLifecycle.IsStale(inactive, now: 13f, staleWindowMs: 2000f), Is.False);
            Assert.That(CodexBrokerRequestLifecycle.IsStale(active, now: 11.9f, staleWindowMs: 2000f), Is.False);
            Assert.That(CodexBrokerRequestLifecycle.IsStale(active, now: 12.1f, staleWindowMs: 2000f), Is.True);
        }
    }
}
