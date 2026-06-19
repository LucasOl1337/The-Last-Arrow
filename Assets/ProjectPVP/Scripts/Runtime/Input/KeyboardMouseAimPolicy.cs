using ProjectPVP.Match;

namespace ProjectPVP.Input
{
    internal static class KeyboardMouseAimPolicy
    {
        public static bool ShouldEnableDefaultMouseAim(CombatantControlMode controlMode, int slotId)
        {
            return controlMode == CombatantControlMode.Human && slotId == 1;
        }
    }
}
