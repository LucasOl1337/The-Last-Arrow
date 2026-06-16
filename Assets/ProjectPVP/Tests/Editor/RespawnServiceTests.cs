using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RespawnServiceTests
    {
        private static readonly MethodInfo PlayerAwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void BuildRespawnCommands_SkipsMissingSlotsAndResolvesSpawnPerSlot()
        {
            GameObject playerOneObject = new GameObject("RespawnServicePlayerOne");
            GameObject playerTwoObject = new GameObject("RespawnServicePlayerTwo");

            try
            {
                PlayerController playerOne = CreatePlayer(playerOneObject);
                PlayerController playerTwo = CreatePlayer(playerTwoObject);
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    null,
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                };

                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    slots,
                    slotId => slotId == CombatantSlotId.SlotTwo ? new Vector2(20f, 30f) : new Vector2(-10f, 5f),
                    applyFreeze: true);

                Assert.That(commands, Has.Count.EqualTo(2));
                Assert.That(commands[0].SlotId, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(commands[0].SpawnPoint, Is.EqualTo(new Vector2(-10f, 5f)));
                Assert.That(commands[0].ControlLocked, Is.True);
                Assert.That(commands[1].SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(commands[1].SpawnPoint, Is.EqualTo(new Vector2(20f, 30f)));
                Assert.That(commands[1].ControlLocked, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
            }
        }

        [Test]
        public void ApplyRespawnCommands_AppliesSlotSelectionLockAndCallback()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject playerObject = new GameObject("RespawnServiceApplyPlayer");

            try
            {
                PlayerController player = CreatePlayer(playerObject);
                PlayerAwakeMethod.Invoke(player, null);
                CombatantSlotConfig slot = new CombatantSlotConfig
                {
                    slotId = CombatantSlotId.SlotTwo,
                    controller = player,
                };
                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    new[] { slot },
                    _ => new Vector2(48f, 96f),
                    applyFreeze: true);

                int callbackCount = 0;
                int appliedCount = RespawnService.ApplyRespawnCommands(
                    commands,
                    command =>
                    {
                        Assert.That(command.SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                        callbackCount += 1;
                    });

                Assert.That(appliedCount, Is.EqualTo(1));
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(player.slotId, Is.EqualTo(2));
                Assert.That(player.IsExternallyControlLocked, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
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
