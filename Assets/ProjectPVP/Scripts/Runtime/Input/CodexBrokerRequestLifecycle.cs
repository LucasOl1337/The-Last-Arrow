namespace ProjectPVP.Input
{
    internal struct CodexBrokerRequestLifecycleState
    {
        private bool _inFlight;
        private float _startedTime;
        private int _version;

        internal bool InFlight => _inFlight;
        internal float StartedTime => _startedTime;
        internal int Version => _version;

        internal static CodexBrokerRequestLifecycleState Inactive()
        {
            return new CodexBrokerRequestLifecycleState
            {
                _startedTime = CodexBrokerRequestLifecycle.ClearedStartedTime,
            };
        }

        internal void Begin(float startedTime)
        {
            _inFlight = true;
            _startedTime = startedTime;
            _version += 1;
        }

        internal void Clear()
        {
            _inFlight = false;
            _startedTime = CodexBrokerRequestLifecycle.ClearedStartedTime;
        }

        internal void Invalidate()
        {
            _version += 1;
            Clear();
        }
    }

    internal static class CodexBrokerRequestLifecycle
    {
        internal const float ClearedStartedTime = -999f;

        internal static int Begin(ref CodexBrokerRequestLifecycleState state, float startedTime)
        {
            state.Begin(startedTime);
            return state.Version;
        }

        internal static bool TryComplete(ref CodexBrokerRequestLifecycleState state, int requestVersion)
        {
            if (!IsCurrentVersion(requestVersion, state.Version))
            {
                return false;
            }

            state.Clear();
            return true;
        }

        internal static void Invalidate(ref CodexBrokerRequestLifecycleState state)
        {
            state.Invalidate();
        }

        internal static bool IsStale(CodexBrokerRequestLifecycleState state, float now, float staleWindowMs)
        {
            return state.InFlight
                && state.StartedTime >= 0f
                && (now - state.StartedTime) * 1000f > staleWindowMs;
        }

        internal static bool IsCurrentVersion(int requestVersion, int currentVersion)
        {
            return requestVersion > 0 && requestVersion == currentVersion;
        }
    }
}
