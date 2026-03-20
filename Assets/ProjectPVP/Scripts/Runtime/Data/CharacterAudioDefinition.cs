using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [CreateAssetMenu(fileName = "CharacterAudio", menuName = "ProjectPVP/Character Audio Definition")]
    public sealed class CharacterAudioDefinition : ScriptableObject
    {
        public string characterId = string.Empty;
        public List<ActionAudioCue> actionAudioCues = new List<ActionAudioCue>();

        public bool TryResolveActionAudioCue(ActionCatalog actionCatalog, string actionName, out ActionAudioCue resolvedCue)
        {
            resolvedCue = null;
            if (actionAudioCues == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            ActionCatalog resolvedCatalog = actionCatalog != null ? actionCatalog : ActionCatalog.LoadDefault();
            foreach (string candidateKey in resolvedCatalog.EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < actionAudioCues.Count; index += 1)
                {
                    ActionAudioCue entry = actionAudioCues[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.actionName))
                    {
                        continue;
                    }

                    if (!string.Equals(entry.actionName, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    resolvedCue = entry;
                    return true;
                }
            }

            return false;
        }
    }
}
