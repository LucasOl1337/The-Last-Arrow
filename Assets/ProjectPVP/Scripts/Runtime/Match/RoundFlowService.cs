using System;
using System.Collections.Generic;

namespace ProjectPVP.Match
{
    internal static class RoundFlowService
    {
        private const int MinimumSlotCount = 2;

        internal static int[] EnsureSlotWinsCapacity(int[] slotWins)
        {
            if (slotWins != null && slotWins.Length >= MinimumSlotCount)
            {
                return slotWins;
            }

            int[] resizedWins = new int[MinimumSlotCount];
            if (slotWins != null)
            {
                Array.Copy(slotWins, resizedWins, Math.Min(slotWins.Length, resizedWins.Length));
            }

            return resizedWins;
        }

        internal static int GetWins(int[] slotWins, CombatantSlotId slotId)
        {
            int slotIndex = slotId.ToIndex();
            if (slotIndex < 0 || slotWins == null || slotIndex >= slotWins.Length)
            {
                return 0;
            }

            return slotWins[slotIndex];
        }

        internal static int AddWin(int[] slotWins, CombatantSlotId slotId)
        {
            int slotIndex = slotId.ToIndex();
            if (slotIndex < 0 || slotWins == null || slotIndex >= slotWins.Length)
            {
                return 0;
            }

            slotWins[slotIndex] += 1;
            return slotWins[slotIndex];
        }

        internal static void ResetWins(int[] slotWins)
        {
            if (slotWins == null)
            {
                return;
            }

            for (int index = 0; index < slotWins.Length; index += 1)
            {
                slotWins[index] = 0;
            }
        }

        internal static CombatantSlotId ResolveChampionSlot(
            IReadOnlyList<CombatantSlotConfig> slots,
            int[] slotWins,
            int roundsToChampion)
        {
            if (slots == null)
            {
                return CombatantSlotId.None;
            }

            int winningRoundCount = Math.Max(1, roundsToChampion);
            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot != null && GetWins(slotWins, slot.slotId) >= winningRoundCount)
                {
                    return slot.slotId;
                }
            }

            return CombatantSlotId.None;
        }

        internal static int AdvanceRespawnSeed(int currentRespawnSeedIndex, int respawnSeedCount)
        {
            if (respawnSeedCount <= 0)
            {
                return 0;
            }

            return (NormalizeRespawnSeedIndex(currentRespawnSeedIndex, respawnSeedCount) + 1) % respawnSeedCount;
        }

        internal static int ResetRespawnSeedCycle()
        {
            return 0;
        }

        internal static int NormalizeRespawnSeedIndex(int seedIndex, int respawnSeedCount)
        {
            if (respawnSeedCount <= 0 || seedIndex < 0)
            {
                return 0;
            }

            return seedIndex % respawnSeedCount;
        }
    }
}
