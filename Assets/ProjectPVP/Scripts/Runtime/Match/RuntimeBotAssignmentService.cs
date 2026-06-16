using System;
using System.Collections.Generic;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Match
{
    [Serializable]
    internal sealed class RuntimeBotMenuSlotAssignment
    {
        public int slotId;
        public bool enabled;
        public string botId = string.Empty;
        public string displayName = string.Empty;
        public string provider = string.Empty;
        public string model = string.Empty;
    }

    [Serializable]
    internal sealed class RuntimeBotMenuAssignmentsFile
    {
        public string updatedAt = string.Empty;
        public List<RuntimeBotMenuSlotAssignment> slots = new List<RuntimeBotMenuSlotAssignment>();
    }

    internal sealed class RuntimeBotAssignmentService
    {
        private readonly Dictionary<CombatantSlotId, CombatantSlotProfile> _runtimeOriginalProfiles = new Dictionary<CombatantSlotId, CombatantSlotProfile>();
        private readonly Dictionary<CombatantSlotId, CombatantSlotProfile> _runtimeOverrideProfiles = new Dictionary<CombatantSlotId, CombatantSlotProfile>();

        internal bool ApplyAssignments(
            IReadOnlyList<CombatantSlotConfig> slots,
            RuntimeBotMenuAssignmentsFile runtimeAssignments,
            out bool anyEnabled,
            ICollection<CombatantSlotConfig> changedSlots = null)
        {
            anyEnabled = false;
            if (runtimeAssignments == null || runtimeAssignments.slots == null || runtimeAssignments.slots.Count == 0)
            {
                return false;
            }

            if (slots == null)
            {
                return true;
            }

            for (int index = 0; index < slots.Count; index += 1)
            {
                CombatantSlotConfig slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                RuntimeBotMenuSlotAssignment assignment = FindRuntimeAssignment(runtimeAssignments, slot.slotId);
                if (assignment != null && assignment.enabled)
                {
                    if (ApplyRuntimeBotAssignment(slot, assignment))
                    {
                        changedSlots?.Add(slot);
                    }

                    anyEnabled = true;
                    continue;
                }

                if (RestoreRuntimeBotAssignment(slot))
                {
                    changedSlots?.Add(slot);
                }
            }

            return true;
        }

        internal bool ApplyRuntimeBotAssignment(CombatantSlotConfig slot, RuntimeBotMenuSlotAssignment assignment)
        {
            if (slot == null || assignment == null)
            {
                return false;
            }

            if (!_runtimeOriginalProfiles.ContainsKey(slot.slotId))
            {
                _runtimeOriginalProfiles[slot.slotId] = slot.playerProfile;
            }

            CombatantSlotProfile overrideProfile = CreateRuntimeControlOverrideProfile(
                slot.ResolvePlayerProfile(),
                slot.slotId,
                CombatantControlMode.AI,
                AiBrainKind.CodexBroker);
            if (overrideProfile == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(assignment.botId))
            {
                overrideProfile.botId = assignment.botId.Trim();
            }

            string resolvedName = !string.IsNullOrWhiteSpace(assignment.displayName)
                ? assignment.displayName.Trim()
                : slot.ResolveDisplayName();
            overrideProfile.botDisplayName = resolvedName;
            overrideProfile.displayName = resolvedName;
            slot.playerProfile = overrideProfile;
            _runtimeOverrideProfiles[slot.slotId] = overrideProfile;
            slot.ApplySelectionToController();
            Debug.Log($"[CodexBot] Runtime assignment applied slot={slot.slotId} botId={overrideProfile.botId} display={resolvedName} provider={assignment.provider} model={assignment.model}");
            return true;
        }

        internal bool RestoreRuntimeBotAssignment(CombatantSlotConfig slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (!_runtimeOriginalProfiles.TryGetValue(slot.slotId, out CombatantSlotProfile originalProfile))
            {
                return false;
            }

            slot.playerProfile = originalProfile;
            slot.ApplySelectionToController();
            _runtimeOriginalProfiles.Remove(slot.slotId);
            _runtimeOverrideProfiles.Remove(slot.slotId);
            return true;
        }

        internal static RuntimeBotMenuSlotAssignment FindRuntimeAssignment(RuntimeBotMenuAssignmentsFile runtimeAssignments, CombatantSlotId slotId)
        {
            if (runtimeAssignments == null || runtimeAssignments.slots == null)
            {
                return null;
            }

            int slotInt = slotId.ToInt();
            for (int index = 0; index < runtimeAssignments.slots.Count; index += 1)
            {
                RuntimeBotMenuSlotAssignment assignment = runtimeAssignments.slots[index];
                if (assignment != null && assignment.slotId == slotInt)
                {
                    return assignment;
                }
            }

            return null;
        }

        internal static CombatantSlotProfile CreateRuntimeControlOverrideProfile(
            CombatantSlotProfile sourceProfile,
            CombatantSlotId slotId,
            CombatantControlMode controlMode,
            AiBrainKind aiBrain)
        {
            CombatantSlotProfile templateProfile = sourceProfile != null
                ? sourceProfile
                : CombatantSlotProfile.ResolveRuntimeFallback(slotId);
            CombatantSlotProfile runtimeProfile = templateProfile != null
                ? UnityEngine.Object.Instantiate(templateProfile)
                : null;

            if (runtimeProfile == null)
            {
                return null;
            }

            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            runtimeProfile.controlMode = controlMode;
            runtimeProfile.aiBrain = aiBrain;
            return runtimeProfile;
        }
    }
}
