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
            HashSet<CombatantSlotId> seenWinningSlots = new HashSet<CombatantSlotId>();
            CombatantSlotId roundWinner = CombatantSlotId.None;
            bool deadPlayerBelongsToRoster = false;
            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot?.controller == null)
                {
                    continue;
                }

                if (slot.controller == deadPlayer)
                {
                    deadPlayerBelongsToRoster = true;
                    continue;
                }

                if (slot.controller.IsDead)
                {
                    continue;
                }

                if (!seenWinningSlots.Add(slot.slotId))
                {
                    continue;
                }

                winningSlots.Add(slot.slotId);
                roundWinner = slot.slotId;
            }

            return !deadPlayerBelongsToRoster || roundWinner == CombatantSlotId.None
                ? RoundDeathResolution.None
                : new RoundDeathResolution(winningSlots, roundWinner);
        }
    }
}
