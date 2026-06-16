using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class CodexBrokerStateMapperTests
    {
        [Test]
        public void BuildExecutorFeedback_MapsSnapshotIntentAndReportedInput()
        {
            var intent = new CodexStrategyIntent
            {
                mode = "pressure",
                reason = "punish_jump",
            };
            var snapshot = new AiArenaSnapshotEnvelope
            {
                semantics = new AiArenaSemanticObservation
                {
                    incomingProjectileThreat = true,
                    hasTarget = true,
                },
                arena = new AiArenaArenaObservation
                {
                    roundResetPending = true,
                },
            };
            var reportedInput = new CodexReportedInputFrame
            {
                frame = 77,
                axis = 0.75f,
                aim = new Vector2(3f, -2f),
                jumpPressed = true,
            };

            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "codex",
                "AI | Active",
                intent,
                snapshot,
                123.4f,
                reportedInput);

            Assert.That(feedback.source, Is.EqualTo("codex"));
            Assert.That(feedback.summary, Is.EqualTo("AI | Active"));
            Assert.That(feedback.intentMode, Is.EqualTo("pressure"));
            Assert.That(feedback.intentReason, Is.EqualTo("punish_jump"));
            Assert.That(feedback.projectileThreatActive, Is.True);
            Assert.That(feedback.targetVisible, Is.True);
            Assert.That(feedback.roundResetPending, Is.True);
            Assert.That(feedback.intentAgeMs, Is.EqualTo(123.4f).Within(0.001f));
            Assert.That(feedback.reportedInput, Is.SameAs(reportedInput));
        }

        [Test]
        public void BuildExecutorFeedback_UsesSafeDefaultsWhenInputsAreMissing()
        {
            CodexExecutorFeedback feedback = CodexBrokerStateMapper.BuildExecutorFeedback(
                "waiting_for_codex",
                "AI | Broker disconnected",
                currentIntent: null,
                snapshot: null,
                intentAgeMs: -1f,
                reportedInput: null);

            Assert.That(feedback.intentMode, Is.EqualTo(string.Empty));
            Assert.That(feedback.intentReason, Is.EqualTo(string.Empty));
            Assert.That(feedback.projectileThreatActive, Is.False);
            Assert.That(feedback.targetVisible, Is.False);
            Assert.That(feedback.roundResetPending, Is.False);
            Assert.That(feedback.intentAgeMs, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(feedback.reportedInput, Is.Not.Null);
        }

        [Test]
        public void ResolveControllerOwner_ReturnsEnvelopeOwnerWhenPresent()
        {
            string owner = CodexBrokerStateMapper.ResolveControllerOwner(
                "remote_owner",
                hasExecutableIntent: false,
                useAgentDrivenMode: true);

            Assert.That(owner, Is.EqualTo("remote_owner"));
        }

        [Test]
        public void ResolveControllerOwner_ReturnsEmptyWhenNoExecutableIntentExists()
        {
            string owner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: false,
                useAgentDrivenMode: true);

            Assert.That(owner, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ResolveControllerOwner_UsesModeSpecificFallbackWhenExecutableIntentExists()
        {
            string agentDrivenOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: true,
                useAgentDrivenMode: true);
            string directOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                string.Empty,
                hasExecutableIntent: true,
                useAgentDrivenMode: false);

            Assert.That(agentDrivenOwner, Is.EqualTo("Codex"));
            Assert.That(directOwner, Is.EqualTo("CodexDirect"));
        }
    }
}
