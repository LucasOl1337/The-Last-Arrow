using System.Collections.Generic;
using NUnit.Framework;
using ProjectPVP.Match;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RoundFlowServiceTests
    {
        [Test]
        public void EnsureSlotWinsCapacity_PreservesExistingWins()
        {
            int[] wins = { 2 };

            int[] resizedWins = RoundFlowService.EnsureSlotWinsCapacity(wins);

            Assert.That(resizedWins, Has.Length.EqualTo(2));
            Assert.That(resizedWins[0], Is.EqualTo(2));
            Assert.That(resizedWins[1], Is.Zero);
        }

        [Test]
        public void AddWinAndResolveChampion_UseConfiguredSlotOrder()
        {
            int[] wins = RoundFlowService.EnsureSlotWinsCapacity(null);
            List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
            {
                new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne },
                new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo },
            };

            RoundFlowService.AddWin(wins, CombatantSlotId.SlotTwo);
            RoundFlowService.AddWin(wins, CombatantSlotId.SlotTwo);

            Assert.That(RoundFlowService.GetWins(wins, CombatantSlotId.SlotOne), Is.Zero);
            Assert.That(RoundFlowService.GetWins(wins, CombatantSlotId.SlotTwo), Is.EqualTo(2));
            Assert.That(RoundFlowService.ResolveChampionSlot(slots, wins, roundsToChampion: 2), Is.EqualTo(CombatantSlotId.SlotTwo));
        }

        [Test]
        public void ResetWinsAndSeedCycle_ClearSeriesState()
        {
            int[] wins = { 3, 1 };

            RoundFlowService.ResetWins(wins);
            int resetSeedIndex = RoundFlowService.ResetRespawnSeedCycle();

            Assert.That(wins[0], Is.Zero);
            Assert.That(wins[1], Is.Zero);
            Assert.That(resetSeedIndex, Is.Zero);
        }

        [Test]
        public void RespawnSeedIndex_NormalizesAndAdvancesThroughCycle()
        {
            Assert.That(RoundFlowService.NormalizeRespawnSeedIndex(-1, respawnSeedCount: 3), Is.Zero);
            Assert.That(RoundFlowService.NormalizeRespawnSeedIndex(4, respawnSeedCount: 3), Is.EqualTo(1));
            Assert.That(RoundFlowService.NormalizeRespawnSeedIndex(4, respawnSeedCount: 0), Is.Zero);
            Assert.That(RoundFlowService.AdvanceRespawnSeed(2, respawnSeedCount: 3), Is.Zero);
            Assert.That(RoundFlowService.AdvanceRespawnSeed(0, respawnSeedCount: 0), Is.Zero);
        }
    }
}
