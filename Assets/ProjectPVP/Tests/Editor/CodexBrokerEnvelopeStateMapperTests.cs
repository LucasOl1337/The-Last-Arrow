using NUnit.Framework;
using ProjectPVP.Input;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexBrokerEnvelopeStateMapperTests
    {
        [Test]
        public void Build_UsesEnvelopeSessionIdAndExecutableIntentWhenPresent()
        {
            var envelope = new CodexBrokerIntentEnvelope
            {
                sessionId = "session-b",
                hasAgentAction = true,
                controllerOwner = "codex-agent",
                intent = new CodexStrategyIntent
                {
                    mode = "pressure",
                    reason = "pressure-window",
                },
            };

            CodexBrokerEnvelopeState state = CodexBrokerEnvelopeStateMapper.Build(
                envelope,
                currentSessionId: "session-a",
                useAgentDrivenMode: true);

            Assert.That(state.sessionId, Is.EqualTo("session-b"));
            Assert.That(state.hasIntent, Is.True);
            Assert.That(state.hasExecutableIntent, Is.True);
            Assert.That(state.controllerOwner, Is.EqualTo("codex-agent"));
            Assert.That(state.intent, Is.SameAs(envelope.intent));
        }

        [Test]
        public void Build_KeepsPreviousSessionIdAndMarksIntentAsNonExecutableWhenMissing()
        {
            var envelope = new CodexBrokerIntentEnvelope
            {
                sessionId = string.Empty,
                hasAgentAction = true,
                controllerOwner = "remote-owner",
                intent = null,
            };

            CodexBrokerEnvelopeState state = CodexBrokerEnvelopeStateMapper.Build(
                envelope,
                currentSessionId: "session-a",
                useAgentDrivenMode: true);

            Assert.That(state.sessionId, Is.EqualTo("session-a"));
            Assert.That(state.hasIntent, Is.False);
            Assert.That(state.hasExecutableIntent, Is.False);
            Assert.That(state.controllerOwner, Is.EqualTo("remote-owner"));
            Assert.That(state.intent, Is.Null);
        }

        [Test]
        public void Build_TreatsDirectModeAsExecutableEvenWhenHasAgentActionIsFalse()
        {
            var envelope = new CodexBrokerIntentEnvelope
            {
                sessionId = "session-b",
                hasAgentAction = false,
                controllerOwner = string.Empty,
                intent = new CodexStrategyIntent
                {
                    mode = "stabilize",
                    reason = "fallback",
                },
            };

            CodexBrokerEnvelopeState state = CodexBrokerEnvelopeStateMapper.Build(
                envelope,
                currentSessionId: "session-a",
                useAgentDrivenMode: false);

            Assert.That(state.sessionId, Is.EqualTo("session-b"));
            Assert.That(state.hasIntent, Is.True);
            Assert.That(state.hasExecutableIntent, Is.True);
            Assert.That(state.controllerOwner, Is.EqualTo("CodexDirect"));
            Assert.That(state.intent, Is.SameAs(envelope.intent));
        }
    }
}
