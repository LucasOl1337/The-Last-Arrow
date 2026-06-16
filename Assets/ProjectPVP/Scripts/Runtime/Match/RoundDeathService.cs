using System;
using System.Collections.Generic;
using ProjectPVP.Gameplay;

namespace ProjectPVP.Match
{
    internal readonly struct RoundDeathResolution
    {
        internal RoundDeathResolution(IReadOnlyList<CombatantSlotId> winningSlots, CombatantSlotId roundWinnerSlot)
        {
            WinningSlots = winningSlots ?? Array.Empty<CombatantSlotId>();
            RoundWinnerSlot = roundWinnerSlot;
        }

        internal static RoundDeathResolution None { get; } =
            new RoundDeathResolution(Array.Empty<CombatantSlotId>(), CombatantSlotId.None);

        internal IReadOnlyList<CombatantSlotId> WinningSlots { get; }
        internal CombatantSlotId RoundWinnerSlot { get; }
        internal bool HasWinner => RoundWinnerSlot != CombatantSlotId.None;
    }

    internal static class RoundDeathService
    {
        internal static RoundDeathResolution ResolveDeath(
            IReadOnlyList<CombatantSlotConfig> slots,
            PlayerController deadPlayer)
        {
            if (slots == null || deadPlayer == null)
            {
                return RoundDeathResolution.None;
            }

            List<CombatantSlotId> winningSlots = new List<CombatantSlotId>();
            CombatantSlotId roundWinner = CombatantSlotId.None;
            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot?.controller == null || slot.controller == deadPlayer)
                {
                    continue;
                }

                winningSlots.Add(slot.slotId);
                roundWinner = slot.slotId;
            }

            return roundWinner == CombatantSlotId.None
                ? RoundDeathResolution.None
                : new RoundDeathResolution(winningSlots, roundWinner);
        }
    }
}
