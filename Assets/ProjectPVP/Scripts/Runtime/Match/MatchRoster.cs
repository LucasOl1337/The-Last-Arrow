using System;
using System.Collections.Generic;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Match
{
    [Serializable]
    public sealed class MatchRoster
    {
        [SerializeField] private List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>(2);

        public IReadOnlyList<CombatantSlotConfig> Slots => slots;

        public void EnsureDefaults(PlayerController slotOneController = null, PlayerController slotTwoController = null)
        {
            if (slots == null)
            {
                slots = new List<CombatantSlotConfig>(2);
            }

            EnsureSlot(CombatantSlotId.SlotOne, slotOneController);
            EnsureSlot(CombatantSlotId.SlotTwo, slotTwoController);
        }

        public CombatantSlotConfig GetSlot(CombatantSlotId slotId)
        {
            if (slots == null)
            {
                return null;
            }

            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot != null && slot.slotId == slotId)
                {
                    return slot;
                }
            }

            return null;
        }

        public CombatantSlotConfig GetSlotByIndex(int index)
        {
            if (slots == null || index < 0 || index >= slots.Count)
            {
                return null;
            }

            return slots[index];
        }

        public IEnumerable<PlayerController> EnumerateControllers()
        {
            if (slots == null)
            {
                yield break;
            }

            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot?.controller != null)
                {
                    yield return slot.controller;
                }
            }
        }

        private void EnsureSlot(CombatantSlotId slotId, PlayerController legacyController)
        {
            CombatantSlotConfig slot = GetSlot(slotId);
            if (slot == null)
            {
                slot = new CombatantSlotConfig
                {
                    slotId = slotId,
                    displayName = slotId.ToDisplayName(),
                    controller = legacyController,
                };
                slots.Add(slot);
            }

            if (legacyController != null && slot.controller == null)
            {
                slot.controller = legacyController;
            }

            if (string.IsNullOrWhiteSpace(slot.displayName))
            {
                slot.displayName = slotId.ToDisplayName();
            }
        }
    }
}
