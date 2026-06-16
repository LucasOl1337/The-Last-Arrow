namespace ProjectPVP.Input
{
    internal sealed class CodexBrokerEnvelopeState
    {
        public string sessionId = string.Empty;
        public bool hasExecutableIntent;
        public string controllerOwner = string.Empty;
        public bool hasIntent;
        public CodexStrategyIntent intent;
    }

    internal static class CodexBrokerEnvelopeStateMapper
    {
        internal static CodexBrokerEnvelopeState Build(
            CodexBrokerIntentEnvelope envelope,
            string currentSessionId,
            bool useAgentDrivenMode)
        {
            var state = new CodexBrokerEnvelopeState
            {
                sessionId = currentSessionId,
            };

            if (envelope == null)
            {
                return state;
            }

            if (!string.IsNullOrWhiteSpace(envelope.sessionId))
            {
                state.sessionId = envelope.sessionId;
            }

            state.hasIntent = envelope.intent != null;
            state.intent = envelope.intent;
            state.hasExecutableIntent = state.hasIntent && (envelope.hasAgentAction || !useAgentDrivenMode);
            state.controllerOwner = CodexBrokerStateMapper.ResolveControllerOwner(
                envelope.controllerOwner,
                state.hasExecutableIntent,
                useAgentDrivenMode);

            return state;
        }
    }
}
