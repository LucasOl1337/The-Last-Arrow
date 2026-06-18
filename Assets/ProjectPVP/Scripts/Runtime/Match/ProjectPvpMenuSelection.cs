using System;
using System.Collections.Generic;
using ProjectPVP.Characters;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Match
{
    internal enum ProjectPvpMenuGameMode
    {
        Versus = 0,
        HumanVsAi = 1,
        AiArena = 2,
    }

    internal sealed class ProjectPvpMenuSlotSelection
    {
        public CombatantSlotId SlotId { get; set; }
        public int CharacterIndex { get; set; }
        public CharacterBootstrapProfile CharacterProfile { get; set; }
        public bool AiEnabled { get; set; }
    }

    internal sealed class ProjectPvpMenuSelection
    {
        private readonly List<ProjectPvpMenuSlotSelection> _slots = new List<ProjectPvpMenuSlotSelection>(2);

        public ProjectPvpMenuGameMode GameMode { get; set; }
        public AiBrainKind AiBrain { get; set; } = AiBrainKind.LocalHeuristic;
        public IReadOnlyList<ProjectPvpMenuSlotSelection> Slots => _slots;

        public void AddSlot(ProjectPvpMenuSlotSelection slot)
        {
            if (slot == null || slot.SlotId == CombatantSlotId.None)
            {
                return;
            }

            ProjectPvpMenuSlotSelection existing = GetSlot(slot.SlotId);
            if (existing != null)
            {
                existing.CharacterIndex = slot.CharacterIndex;
                existing.CharacterProfile = slot.CharacterProfile;
                existing.AiEnabled = slot.AiEnabled;
                return;
            }

            _slots.Add(slot);
        }

        public ProjectPvpMenuSlotSelection GetSlot(CombatantSlotId slotId)
        {
            for (int index = 0; index < _slots.Count; index += 1)
            {
                ProjectPvpMenuSlotSelection slot = _slots[index];
                if (slot != null && slot.SlotId == slotId)
                {
                    return slot;
                }
            }

            return null;
        }
    }

    internal static class ProjectPvpMenuSelectionService
    {
        public static readonly ProjectPvpMenuGameMode[] GameModes =
        {
            ProjectPvpMenuGameMode.Versus,
            ProjectPvpMenuGameMode.HumanVsAi,
            ProjectPvpMenuGameMode.AiArena,
        };

        public static ProjectPvpMenuSelection BuildDefault(
            IReadOnlyList<CombatantSlotConfig> slots,
            IReadOnlyList<CharacterBootstrapProfile> characters,
            ProjectPvpMenuGameMode gameMode)
        {
            ProjectPvpMenuSelection selection = new ProjectPvpMenuSelection
            {
                GameMode = gameMode,
                AiBrain = AiBrainKind.LocalHeuristic,
            };

            AddDefaultSlot(selection, slots, characters, CombatantSlotId.SlotOne);
            AddDefaultSlot(selection, slots, characters, CombatantSlotId.SlotTwo);
            ApplyGameMode(selection, gameMode);
            return selection;
        }

        public static void ApplyGameMode(ProjectPvpMenuSelection selection, ProjectPvpMenuGameMode gameMode)
        {
            if (selection == null)
            {
                return;
            }

            selection.GameMode = gameMode;
            SetSlotAi(selection, CombatantSlotId.SlotOne, gameMode == ProjectPvpMenuGameMode.AiArena);
            SetSlotAi(selection, CombatantSlotId.SlotTwo, gameMode != ProjectPvpMenuGameMode.Versus);
        }

        public static string ToDisplayName(ProjectPvpMenuGameMode gameMode)
        {
            return gameMode switch
            {
                ProjectPvpMenuGameMode.HumanVsAi => "HUMAN VS AI",
                ProjectPvpMenuGameMode.AiArena => "AI ARENA",
                _ => "VERSUS",
            };
        }

        public static int ResolveModeIndex(ProjectPvpMenuGameMode mode)
        {
            for (int index = 0; index < GameModes.Length; index += 1)
            {
                if (GameModes[index] == mode)
                {
                    return index;
                }
            }

            return 0;
        }

        public static ProjectPvpMenuGameMode CycleMode(ProjectPvpMenuGameMode currentMode, int direction)
        {
            int index = ResolveModeIndex(currentMode);
            int count = GameModes.Length;
            if (count == 0)
            {
                return ProjectPvpMenuGameMode.Versus;
            }

            int nextIndex = Mod(index + Math.Sign(direction == 0 ? 1 : direction), count);
            return GameModes[nextIndex];
        }

        public static void CycleCharacter(
            ProjectPvpMenuSlotSelection slot,
            IReadOnlyList<CharacterBootstrapProfile> characters,
            int direction)
        {
            if (slot == null || characters == null || characters.Count == 0)
            {
                return;
            }

            int nextIndex = Mod(slot.CharacterIndex + Math.Sign(direction == 0 ? 1 : direction), characters.Count);
            slot.CharacterIndex = nextIndex;
            slot.CharacterProfile = characters[nextIndex];
        }

        public static CombatantSlotProfile CreateRuntimeControlProfile(
            CombatantSlotProfile sourceProfile,
            CombatantSlotId slotId,
            bool aiEnabled,
            AiBrainKind aiBrain)
        {
            CombatantSlotProfile runtimeProfile = RuntimeBotAssignmentService.CreateRuntimeControlOverrideProfile(
                sourceProfile,
                slotId,
                aiEnabled ? CombatantControlMode.AI : CombatantControlMode.Human,
                aiBrain);
            if (runtimeProfile == null)
            {
                return null;
            }

            string displayName = sourceProfile != null
                ? sourceProfile.ResolveDisplayName(slotId)
                : slotId.ToDisplayName();
            runtimeProfile.displayName = displayName;
            runtimeProfile.botDisplayName = aiEnabled ? displayName + " AI" : string.Empty;
            runtimeProfile.botId = aiEnabled ? "menu_ai_slot_" + slotId.ToInt() : string.Empty;
            return runtimeProfile;
        }

        public static ProjectPvpMenuGameMode ResolveInitialMode(
            IReadOnlyList<CombatantSlotConfig> slots,
            bool slotTwoAutoAiEnabled)
        {
            bool slotOneAi = ResolveSlotAi(slots, CombatantSlotId.SlotOne, fallbackAi: false);
            bool slotTwoAi = ResolveSlotAi(slots, CombatantSlotId.SlotTwo, slotTwoAutoAiEnabled);
            if (slotOneAi && slotTwoAi)
            {
                return ProjectPvpMenuGameMode.AiArena;
            }

            return slotTwoAi ? ProjectPvpMenuGameMode.HumanVsAi : ProjectPvpMenuGameMode.Versus;
        }

        private static void AddDefaultSlot(
            ProjectPvpMenuSelection selection,
            IReadOnlyList<CombatantSlotConfig> slots,
            IReadOnlyList<CharacterBootstrapProfile> characters,
            CombatantSlotId slotId)
        {
            CharacterBootstrapProfile profile = ResolveSelectedProfile(FindSlot(slots, slotId), characters, slotId);
            selection.AddSlot(new ProjectPvpMenuSlotSelection
            {
                SlotId = slotId,
                CharacterIndex = ResolveCharacterIndex(characters, profile),
                CharacterProfile = profile,
                AiEnabled = false,
            });
        }

        private static CharacterBootstrapProfile ResolveSelectedProfile(
            CombatantSlotConfig slot,
            IReadOnlyList<CharacterBootstrapProfile> characters,
            CombatantSlotId slotId)
        {
            if (slot != null)
            {
                if (slot.characterProfile != null)
                {
                    return slot.characterProfile;
                }

                CharacterBootstrapProfile byDefinition = FindByDefinition(characters, slot.selectedCharacter);
                if (byDefinition != null)
                {
                    return byDefinition;
                }

                byDefinition = FindByDefinition(characters, slot.ResolveCharacterDefinition());
                if (byDefinition != null)
                {
                    return byDefinition;
                }
            }

            if (characters == null || characters.Count == 0)
            {
                return null;
            }

            int fallbackIndex = slotId == CombatantSlotId.SlotTwo && characters.Count > 1 ? 1 : 0;
            return characters[Mathf.Clamp(fallbackIndex, 0, characters.Count - 1)];
        }

        private static CharacterBootstrapProfile FindByDefinition(
            IReadOnlyList<CharacterBootstrapProfile> characters,
            ProjectPVP.Data.CharacterDefinition definition)
        {
            if (characters == null || definition == null)
            {
                return null;
            }

            for (int index = 0; index < characters.Count; index += 1)
            {
                CharacterBootstrapProfile candidate = characters[index];
                if (candidate != null && candidate.ResolveCharacterDefinition() == definition)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int ResolveCharacterIndex(
            IReadOnlyList<CharacterBootstrapProfile> characters,
            CharacterBootstrapProfile selectedProfile)
        {
            if (characters == null || characters.Count == 0 || selectedProfile == null)
            {
                return 0;
            }

            for (int index = 0; index < characters.Count; index += 1)
            {
                if (characters[index] == selectedProfile)
                {
                    return index;
                }
            }

            return 0;
        }

        private static void SetSlotAi(ProjectPvpMenuSelection selection, CombatantSlotId slotId, bool aiEnabled)
        {
            ProjectPvpMenuSlotSelection slot = selection.GetSlot(slotId);
            if (slot != null)
            {
                slot.AiEnabled = aiEnabled;
            }
        }

        private static CombatantSlotConfig FindSlot(IReadOnlyList<CombatantSlotConfig> slots, CombatantSlotId slotId)
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

        private static bool ResolveSlotAi(
            IReadOnlyList<CombatantSlotConfig> slots,
            CombatantSlotId slotId,
            bool fallbackAi)
        {
            CombatantSlotConfig slot = FindSlot(slots, slotId);
            return slot != null
                ? slot.ResolveControlMode() == CombatantControlMode.AI
                : fallbackAi;
        }

        private static int Mod(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
