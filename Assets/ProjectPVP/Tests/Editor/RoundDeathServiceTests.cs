using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RoundDeathServiceTests
    {
        private static readonly MethodInfo PlayerAwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

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
        public void ResolveDeath_SkipsDeadSurvivors()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject deadObject = new GameObject("RoundDeathDeadPlayer");
            GameObject livingObject = new GameObject("RoundDeathLivingPlayer");
            GameObject deadSurvivorObject = new GameObject("RoundDeathDeadSurvivorPlayer");

            try
            {
                PlayerController deadPlayer = CreatePlayer(deadObject);
                PlayerController livingPlayer = CreatePlayer(livingObject);
                PlayerController deadSurvivor = CreatePlayer(deadSurvivorObject);

                PlayerAwakeMethod.Invoke(deadPlayer, null);
                PlayerAwakeMethod.Invoke(livingPlayer, null);
                PlayerAwakeMethod.Invoke(deadSurvivor, null);

                Assert.That(deadSurvivor.TryKill(), Is.True);
                Assert.That(deadSurvivor.IsDead, Is.True);

                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = livingPlayer },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = deadSurvivor },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = deadPlayer },
                };

                RoundDeathResolution resolution = RoundDeathService.ResolveDeath(slots, deadPlayer);

                Assert.That(resolution.HasWinner, Is.True);
                Assert.That(resolution.RoundWinnerSlot, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(resolution.WinningSlots, Is.EqualTo(new[] { CombatantSlotId.SlotOne }));
            }
            finally
            {
                Object.DestroyImmediate(deadObject);
                Object.DestroyImmediate(livingObject);
                Object.DestroyImmediate(deadSurvivorObject);
            }
        }

        [Test]
        public void ResolveDeath_DeduplicatesWinningSlotsInRawInput()
        {
            GameObject deadObject = new GameObject("RoundDeathDedupDeadPlayer");
            GameObject firstWinnerObject = new GameObject("RoundDeathDedupWinnerOne");
            GameObject secondWinnerObject = new GameObject("RoundDeathDedupWinnerTwo");

            try
            {
                PlayerController deadPlayer = deadObject.AddComponent<PlayerController>();
                PlayerController firstWinner = firstWinnerObject.AddComponent<PlayerController>();
                PlayerController secondWinner = secondWinnerObject.AddComponent<PlayerController>();
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = firstWinner },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = firstWinner },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = secondWinner },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = secondWinner },
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
                Object.DestroyImmediate(firstWinnerObject);
                Object.DestroyImmediate(secondWinnerObject);
            }
        }

        [Test]
        public void ResolveDeath_ReturnsNoWinnerWhenDeadPlayerIsNotInRoster()
        {
            GameObject strayDeadObject = new GameObject("RoundDeathStrayDeadPlayer");
            GameObject playerOneObject = new GameObject("RoundDeathRosterPlayerOne");
            GameObject playerTwoObject = new GameObject("RoundDeathRosterPlayerTwo");

            try
            {
                PlayerController strayDeadPlayer = strayDeadObject.AddComponent<PlayerController>();
                PlayerController playerOne = playerOneObject.AddComponent<PlayerController>();
                PlayerController playerTwo = playerTwoObject.AddComponent<PlayerController>();
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                };

                RoundDeathResolution resolution = RoundDeathService.ResolveDeath(slots, strayDeadPlayer);

                Assert.That(resolution.HasWinner, Is.False);
                Assert.That(resolution.RoundWinnerSlot, Is.EqualTo(CombatantSlotId.None));
                Assert.That(resolution.WinningSlots, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(strayDeadObject);
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
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

        private static PlayerController CreatePlayer(GameObject root)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            PlayerController controller = root.AddComponent<PlayerController>();
            controller.body = body;
            controller.bodyCollider = collider;
            return controller;
        }
    }
}
