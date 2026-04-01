namespace ProjectPVP.Match
{
    public enum CombatantSlotId
    {
        None = 0,
        SlotOne = 1,
        SlotTwo = 2,
    }

    public static class CombatantSlotIdUtility
    {
        public static CombatantSlotId FromInt(int value)
        {
            return value switch
            {
                1 => CombatantSlotId.SlotOne,
                2 => CombatantSlotId.SlotTwo,
                _ => CombatantSlotId.None,
            };
        }

        public static int ToInt(this CombatantSlotId slotId)
        {
            return slotId switch
            {
                CombatantSlotId.SlotOne => 1,
                CombatantSlotId.SlotTwo => 2,
                _ => 0,
            };
        }

        public static int ToIndex(this CombatantSlotId slotId)
        {
            int intValue = slotId.ToInt();
            return intValue > 0 ? intValue - 1 : -1;
        }

        public static string ToDisplayName(this CombatantSlotId slotId)
        {
            return slotId switch
            {
                CombatantSlotId.SlotOne => "Slot 1",
                CombatantSlotId.SlotTwo => "Slot 2",
                _ => "Unassigned",
            };
        }
    }
}
