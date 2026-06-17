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

            CombatantSlotConfig slotOne = null;
            CombatantSlotConfig slotTwo = null;
            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                if (slot.slotId == CombatantSlotId.SlotOne)
                {
                    slotOne = MergeDuplicateSlot(slotOne, slot);
                    continue;
                }

                if (slot.slotId == CombatantSlotId.SlotTwo)
                {
                    slotTwo = MergeDuplicateSlot(slotTwo, slot);
                }
            }

            slotOne = EnsureSlot(slotOne, CombatantSlotId.SlotOne, slotOneController);
            slotTwo = EnsureSlot(slotTwo, CombatantSlotId.SlotTwo, slotTwoController);

            slots.Clear();
            slots.Add(slotOne);
            slots.Add(slotTwo);
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

        private static CombatantSlotConfig MergeDuplicateSlot(CombatantSlotConfig target, CombatantSlotConfig source)
        {
            if (source == null)
            {
                return target;
            }

            if (target == null)
            {
                return source;
            }

            if (target.controller == null && source.controller != null)
            {
                target.controller = source.controller;
            }

            if (target.playerProfile == null && source.playerProfile != null)
            {
                target.playerProfile = source.playerProfile;
            }

            if (target.characterProfile == null && source.characterProfile != null)
            {
                target.characterProfile = source.characterProfile;
            }

            if (target.selectedCharacter == null && source.selectedCharacter != null)
            {
                target.selectedCharacter = source.selectedCharacter;
            }

            if (string.IsNullOrWhiteSpace(target.displayName) && !string.IsNullOrWhiteSpace(source.displayName))
            {
                target.displayName = source.displayName;
            }

            if (target.fallbackSpawnPoint == Vector2.zero && source.fallbackSpawnPoint != Vector2.zero)
            {
                target.fallbackSpawnPoint = source.fallbackSpawnPoint;
            }

            return target;
        }

        private static CombatantSlotConfig EnsureSlot(
            CombatantSlotConfig slot,
            CombatantSlotId slotId,
            PlayerController legacyController)
        {
            if (slot == null)
            {
                slot = new CombatantSlotConfig();
            }

            slot.slotId = slotId;

            if (legacyController != null && slot.controller == null)
            {
                slot.controller = legacyController;
            }

            if (string.IsNullOrWhiteSpace(slot.displayName))
            {
                slot.displayName = slotId.ToDisplayName();
            }

            return slot;
        }
    }
}
