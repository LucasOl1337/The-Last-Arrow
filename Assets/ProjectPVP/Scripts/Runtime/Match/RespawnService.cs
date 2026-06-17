using System;
using System.Collections.Generic;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Match
{
    internal readonly struct RespawnSlotCommand
    {
        internal RespawnSlotCommand(
            CombatantSlotConfig slot,
            PlayerController controller,
            Vector2 spawnPoint,
            bool controlLocked)
        {
            Slot = slot;
            Controller = controller;
            SpawnPoint = spawnPoint;
            ControlLocked = controlLocked;
        }

        internal CombatantSlotConfig Slot { get; }
        internal PlayerController Controller { get; }
        internal Vector2 SpawnPoint { get; }
        internal bool ControlLocked { get; }
        internal CombatantSlotId SlotId => Slot != null ? Slot.slotId : CombatantSlotId.None;
        internal bool IsValid => Slot != null && Controller != null;
    }

    internal static class RespawnService
    {
        internal static List<RespawnSlotCommand> BuildRespawnCommands(
            IReadOnlyList<CombatantSlotConfig> slots,
            Func<CombatantSlotId, Vector2> resolveSpawnPoint,
            bool applyFreeze)
        {
            List<RespawnSlotCommand> commands = new List<RespawnSlotCommand>();
            HashSet<CombatantSlotId> seenSlots = new HashSet<CombatantSlotId>();
            if (slots == null || resolveSpawnPoint == null)
            {
                return commands;
            }

            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot?.controller == null)
                {
                    continue;
                }

                if (!seenSlots.Add(slot.slotId))
                {
                    continue;
                }

                commands.Add(new RespawnSlotCommand(
                    slot,
                    slot.controller,
                    resolveSpawnPoint(slot.slotId),
                    applyFreeze));
            }

            return commands;
        }

        internal static int ApplyRespawnCommands(
            IReadOnlyList<RespawnSlotCommand> commands,
            Action<RespawnSlotCommand> onRespawnApplied = null)
        {
            if (commands == null)
            {
                return 0;
            }

            int appliedCount = 0;
            for (int index = 0; index < commands.Count; index += 1)
            {
                RespawnSlotCommand command = commands[index];
                if (!command.IsValid)
                {
                    continue;
                }

                command.Slot.ApplySelectionToController();
                command.Controller.SetSpawnPosition(command.SpawnPoint);
                command.Controller.SetExternalControlLock(command.ControlLocked);
                onRespawnApplied?.Invoke(command);
                appliedCount += 1;
            }

            return appliedCount;
        }
    }
}
