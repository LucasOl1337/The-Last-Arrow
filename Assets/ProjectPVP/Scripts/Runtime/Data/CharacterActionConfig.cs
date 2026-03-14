using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Data
{
    [CreateAssetMenu(fileName = "CharacterActionConfig", menuName = "ProjectPVP/Character Action Config")]
    public sealed class CharacterActionConfig : ScriptableObject
    {
        [Serializable]
        public sealed class ActionAudioCue
        {
            public string actionName = string.Empty;
            public AudioClip clip;
            public string resourcesPath = string.Empty;
            public float playbackSpeed = 1f;
            public float volumeDb;
            public float stopAfterSeconds;
        }

        [Header("Identity")]
        public string id = string.Empty;

        [Header("Action Tuning")]
        public List<NamedFloatValue> actionAnimationDurations = new List<NamedFloatValue>();
        public List<NamedBoolValue> actionAnimationCancelable = new List<NamedBoolValue>();
        public List<NamedFloatValue> actionAnimationSpeeds = new List<NamedFloatValue>();
        public List<ActionColliderOverride> actionColliderOverrides = new List<ActionColliderOverride>();
        public List<ActionAudioCue> actionAudioCues = new List<ActionAudioCue>();

        public bool TryResolveActionDuration(string actionName, out float resolvedValue)
        {
            return TryResolveNamedFloat(actionAnimationDurations, actionName, out resolvedValue);
        }

        public bool TryResolveActionCancelable(string actionName, out bool resolvedValue)
        {
            return TryResolveNamedBool(actionAnimationCancelable, actionName, out resolvedValue);
        }

        public bool TryResolveActionSpeed(string actionName, out float resolvedValue)
        {
            return TryResolveNamedFloat(actionAnimationSpeeds, actionName, out resolvedValue);
        }

        public bool TryFindActionColliderOverride(string actionName, out ActionColliderOverride resolvedOverride)
        {
            resolvedOverride = null;
            if (actionColliderOverrides == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < actionColliderOverrides.Count; index += 1)
                {
                    ActionColliderOverride entry = actionColliderOverrides[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.actionName))
                    {
                        continue;
                    }

                    if (!string.Equals(entry.actionName, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    resolvedOverride = entry;
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveActionAudioCue(string actionName, out ActionAudioCue resolvedCue)
        {
            resolvedCue = null;
            if (actionAudioCues == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
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

        private static bool TryResolveNamedFloat(List<NamedFloatValue> values, string actionName, out float resolvedValue)
        {
            resolvedValue = default;
            if (values == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < values.Count; index += 1)
                {
                    NamedFloatValue entry = values[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    if (!string.Equals(entry.key, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    resolvedValue = entry.value;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveNamedBool(List<NamedBoolValue> values, string actionName, out bool resolvedValue)
        {
            resolvedValue = default;
            if (values == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidateKey in EnumerateActionKeys(actionName))
            {
                for (int index = 0; index < values.Count; index += 1)
                {
                    NamedBoolValue entry = values[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    if (!string.Equals(entry.key, candidateKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    resolvedValue = entry.value;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateActionKeys(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            yield return actionName;

            if (string.Equals(actionName, "jump_start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "jump_air", StringComparison.OrdinalIgnoreCase))
            {
                yield return "jump";
            }

            if (string.Equals(actionName, "aim", StringComparison.OrdinalIgnoreCase))
            {
                yield return "aiming";
            }
        }
    }
}
