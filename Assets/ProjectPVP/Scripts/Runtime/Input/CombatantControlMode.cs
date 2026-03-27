namespace ProjectPVP.Match
{
    public enum CombatantControlMode
    {
        Human = 0,
        AI = 1,
        Idle = 2,
    }

    public static class CombatantControlModeUtility
    {
        public static string ToDisplayName(this CombatantControlMode controlMode)
        {
            return controlMode switch
            {
                CombatantControlMode.AI => "AI",
                CombatantControlMode.Idle => "Idle",
                _ => "Human",
            };
        }
    }
}
