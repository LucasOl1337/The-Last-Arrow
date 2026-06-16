using System.Collections.Generic;
using NUnit.Framework;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RoundDeathServiceTests
    {
        [Test]
        public void ResolveDeath_SkipsMissingSlotsAndDeadController()
        {
            GameObject deadObject = new GameObject("RoundDeathDeadPlayer");
            GameObject survivingObject = new GameObject("RoundDeathSurvivingPlayer");

            try
            {
                PlayerController deadPlayer = deadObject.AddComponent<PlayerController>();
                PlayerController survivingPlayer = survivingObject.AddComponent<PlayerController>();
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    null,
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = deadPlayer },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = survivingPlayer },
                };

                RoundDeathResolution resolution = RoundDeathService.ResolveDeath(slots, deadPlayer);

                Assert.That(resolution.HasWinner, Is.True);
                Assert.That(resolution.RoundWinnerSlot, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(resolution.WinningSlots, Is.EqualTo(new[] { CombatantSlotId.SlotTwo }));
            }
            finally
            {
                Object.DestroyImmediate(deadObject);
                Object.DestroyImmediate(survivingObject);
            }
        }

        [Test]
        public void ResolveDeath_PreservesSurvivorOrderAndUsesLastSurvivorAsRoundWinner()
        {
            GameObject deadObject = new GameObject("RoundDeathDeadPlayer");
            GameObject playerOneObject = new GameObject("RoundDeathPlayerOne");
            GameObject playerTwoObject = new GameObject("RoundDeathPlayerTwo");

            try
            {
                PlayerController deadPlayer = deadObject.AddComponent<PlayerController>();
                PlayerController playerOne = playerOneObject.AddComponent<PlayerController>();
                PlayerController playerTwo = playerTwoObject.AddComponent<PlayerController>();
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = deadPlayer },
                };

                RoundDeathResolution resolution = RoundDeathService.ResolveDeath(slots, deadPlayer);

                Assert.That(resolution.HasWinner, Is.True);
                Assert.That(resolution.RoundWinnerSlot, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(resolution.WinningSlots, Is.EqualTo(new[] { CombatantSlotId.SlotOne, CombatantSlotId.SlotTwo }));
            }
            finally
            {
                Object.DestroyImmediate(deadObject);
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
            }
        }

        [Test]
        public void ResolveDeath_ReturnsNoWinnerWhenDeadPlayerOrSurvivorsAreMissing()
        {
            GameObject deadObject = new GameObject("RoundDeathDeadPlayer");

            try
            {
                PlayerController deadPlayer = deadObject.AddComponent<PlayerController>();
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = deadPlayer },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo },
                };

                RoundDeathResolution noDeadPlayer = RoundDeathService.ResolveDeath(slots, null);
                RoundDeathResolution noSurvivors = RoundDeathService.ResolveDeath(slots, deadPlayer);

                Assert.That(noDeadPlayer.HasWinner, Is.False);
                Assert.That(noDeadPlayer.WinningSlots, Is.Empty);
                Assert.That(noSurvivors.HasWinner, Is.False);
                Assert.That(noSurvivors.WinningSlots, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(deadObject);
            }
        }
    }
}
