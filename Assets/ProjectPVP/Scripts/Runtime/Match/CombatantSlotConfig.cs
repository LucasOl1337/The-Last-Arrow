using System;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Match
{
    [Serializable]
    public sealed class CombatantSlotConfig
    {
        public CombatantSlotId slotId = CombatantSlotId.SlotOne;
        public string displayName = string.Empty;
        public CombatantSlotProfile playerProfile;
        public CharacterBootstrapProfile characterProfile;
        public PlayerController controller;
        public CharacterDefinition selectedCharacter;
        public Vector2 fallbackSpawnPoint = Vector2.zero;

        public bool IsAssigned => controller != null;

        public CharacterDefinition ResolveCharacterDefinition()
        {
            if (selectedCharacter != null)
            {
                return selectedCharacter;
            }

            if (characterProfile != null && characterProfile.ResolveCharacterDefinition() != null)
            {
                return characterProfile.ResolveCharacterDefinition();
            }

            return controller != null ? controller.characterDefinition : null;
        }

        public CharacterBootstrapProfile ResolveCharacterProfile()
        {
            return characterProfile;
        }

        public CombatantSlotProfile ResolvePlayerProfile()
        {
            return playerProfile != null
                ? playerProfile
                : CombatantSlotProfile.ResolveRuntimeFallback(slotId);
        }

        public string ResolveDisplayName()
        {
            if (playerProfile != null && !string.IsNullOrWhiteSpace(playerProfile.displayName))
            {
                return playerProfile.ResolveDisplayName(slotId);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return slotId.ToDisplayName();
        }

        public Color ResolveDebugTint()
        {
            return ResolvePlayerProfile().ResolveDebugTint(slotId);
        }

        public void ApplySelectionToController()
        {
            if (controller == null)
            {
                return;
            }

            controller.slotId = Mathf.Max(1, slotId.ToInt());
            controller.AssignSlotProfile(ResolvePlayerProfile());

            if (characterProfile != null)
            {
                characterProfile.ApplyToController(controller);
            }

            if (controller.characterDefinition == null && selectedCharacter != null)
            {
                controller.AssignCharacterDefinition(selectedCharacter);
            }
        }
    }
}
