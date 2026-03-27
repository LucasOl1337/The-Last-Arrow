namespace ProjectPVP.AI
{
    public enum AiArenaControlMode
    {
        Human = 0,
        Heuristic = 1,
        HttpBridge = 2,
        Idle = 3,
    }

    public enum AiActionFallbackMode
    {
        Neutral = 0,
        HoldLastContinuous = 1,
    }
}
