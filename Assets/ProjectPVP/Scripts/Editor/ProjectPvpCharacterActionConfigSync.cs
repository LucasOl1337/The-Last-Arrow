using System;
using System.Collections.Generic;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterActionConfigSync
    {
        internal static bool Sync(CharacterDefinition definition, out string summary)
        {
            summary = "ProjectPVP: Action Data nao sincronizado.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            IReadOnlyList<ActionSpriteAnimation> animations = definition.GetActionAnimations();
            List<string> actionKeys = CollectActionKeys(animations);
            CollectMechanicsModuleActionKeys(definition, actionKeys);
            if (actionKeys.Count == 0)
            {
                summary = "ProjectPVP: nenhuma action encontrada para sincronizar em " + definition.displayName + ".";
                return false;
            }

            Dictionary<string, float> durationByAction = BuildDurationLookup(animations);
            Dictionary<string, float> speedByAction = BuildSpeedLookup(animations);

            Undo.RecordObject(definition, "Sync Character Action Data");

            int addedEntries = EnsureActionEntries(definition, actionKeys, durationByAction, speedByAction);
            CharacterAudioDefinition audioDefinition = EnsureAudioDefinition(definition);
            int addedAudioEntries = EnsureAudioCues(audioDefinition, actionKeys, durationByAction);

            if (audioDefinition != null)
            {
                EditorUtility.SetDirty(audioDefinition);
            }

            if (addedEntries <= 0 && addedAudioEntries <= 0)
            {
                summary = "ProjectPVP: Action Data de " + definition.displayName + " ja estava atualizado.";
                return true;
            }

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            summary = "ProjectPVP: Action Data de " + definition.displayName + " sincronizado. Actions novas: "
                + addedEntries + ". Audios novos: " + addedAudioEntries + ".";
            return true;
        }

        private static List<string> CollectActionKeys(IReadOnlyList<ActionSpriteAnimation> clips)
        {
            var actionKeys = new List<string>();
            if (clips == null)
            {
                return actionKeys;
            }

            for (int index = 0; index < clips.Count; index += 1)
            {
                ActionSpriteAnimation clip = clips[index];
                if (clip == null || string.IsNullOrWhiteSpace(clip.actionName))
                {
                    continue;
                }

                AddUniqueActionKey(actionKeys, clip.actionName);
            }

            return actionKeys;
        }

        private static void CollectMechanicsModuleActionKeys(CharacterDefinition definition, List<string> actionKeys)
        {
            if (definition == null || definition.mechanicsModule == null || actionKeys == null)
            {
                return;
            }

            IEnumerable<string> additionalKeys = definition.mechanicsModule.GetAdditionalActionKeys(definition);
            if (additionalKeys == null)
            {
                return;
            }

            foreach (string actionKey in additionalKeys)
            {
                AddUniqueActionKey(actionKeys, actionKey);
            }
        }

        private static void AddUniqueActionKey(List<string> actionKeys, string actionKey)
        {
            if (actionKeys == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return;
            }

            for (int index = 0; index < actionKeys.Count; index += 1)
            {
                if (string.Equals(actionKeys[index], actionKey, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            actionKeys.Add(actionKey.Trim());
        }

        private static Dictionary<string, float> BuildDurationLookup(IReadOnlyList<ActionSpriteAnimation> clips)
        {
            var durations = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (clips == null)
            {
                return durations;
            }

            for (int index = 0; index < clips.Count; index += 1)
            {
                if (!TryGetActionMetrics(clips[index], out string actionName, out float framesPerSecond, out int frameCount))
                {
                    continue;
                }

                if (!durations.ContainsKey(actionName))
                {
                    durations[actionName] = Mathf.Max(0.01f, frameCount / framesPerSecond);
                }
            }

            return durations;
        }

        private static Dictionary<string, float> BuildSpeedLookup(IReadOnlyList<ActionSpriteAnimation> clips)
        {
            var speeds = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (clips == null)
            {
                return speeds;
            }

            for (int index = 0; index < clips.Count; index += 1)
            {
                if (!TryGetActionMetrics(clips[index], out string actionName, out float framesPerSecond, out _))
                {
                    continue;
                }

                if (!speeds.ContainsKey(actionName))
                {
                    speeds[actionName] = framesPerSecond;
                }
            }

            return speeds;
        }

        private static bool TryGetActionMetrics(ActionSpriteAnimation clip, out string actionName, out float framesPerSecond, out int frameCount)
        {
            actionName = string.Empty;
            framesPerSecond = 12f;
            frameCount = 0;

            if (clip == null || string.IsNullOrWhiteSpace(clip.actionName))
            {
                return false;
            }

            actionName = clip.actionName.Trim();
            framesPerSecond = clip.framesPerSecond > 0.01f ? clip.framesPerSecond : 12f;
            frameCount = clip.frames != null ? clip.frames.Count : 0;
            return frameCount > 0;
        }

        private static int EnsureActionEntries(
            CharacterDefinition definition,
            List<string> actionKeys,
            Dictionary<string, float> durationByAction,
            Dictionary<string, float> speedByAction)
        {
            if (definition.actions == null)
            {
                definition.actions = new List<CharacterActionConfig>();
            }

            int added = 0;
            for (int index = 0; index < actionKeys.Count; index += 1)
            {
                string actionKey = actionKeys[index];
                if (FindAction(definition.actions, actionKey) != null)
                {
                    continue;
                }

                definition.actions.Add(new CharacterActionConfig
                {
                    actionName = actionKey,
                    duration = durationByAction != null && durationByAction.TryGetValue(actionKey, out float resolvedDuration)
                        ? resolvedDuration
                        : 0.12f,
                    speed = speedByAction != null && speedByAction.TryGetValue(actionKey, out float resolvedSpeed)
                        ? resolvedSpeed
                        : 12f,
                    colliderOverride = new ActionColliderOverride
                    {
                        actionName = actionKey,
                        size = definition.colliderSize,
                        offset = definition.colliderOffset,
                    },
                });
                added += 1;
            }

            return added;
        }

        private static CharacterActionConfig FindAction(List<CharacterActionConfig> actions, string actionKey)
        {
            if (actions == null)
            {
                return null;
            }

            for (int index = 0; index < actions.Count; index += 1)
            {
                CharacterActionConfig action = actions[index];
                if (action != null && string.Equals(action.actionName, actionKey, StringComparison.OrdinalIgnoreCase))
                {
                    return action;
                }
            }

            return null;
        }

        private static CharacterAudioDefinition EnsureAudioDefinition(CharacterDefinition definition)
        {
            if (definition.audioDefinition != null)
            {
                return definition.audioDefinition;
            }

            if (!ProjectPvpCharacterAssetPaths.TryGetDataFolder(definition, out string dataFolderPath))
            {
                return null;
            }

            string assetName = BuildAudioAssetName(definition);
            string assetPath = dataFolderPath + "/" + assetName + ".asset";
            CharacterAudioDefinition audioDefinition = AssetDatabase.LoadAssetAtPath<CharacterAudioDefinition>(assetPath);
            if (audioDefinition == null)
            {
                audioDefinition = ScriptableObject.CreateInstance<CharacterAudioDefinition>();
                audioDefinition.characterId = definition.id;
                AssetDatabase.CreateAsset(audioDefinition, assetPath);
            }

            definition.audioDefinition = audioDefinition;
            return audioDefinition;
        }

        private static string BuildAudioAssetName(CharacterDefinition definition)
        {
            string assetName = !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName.Replace(" ", string.Empty)
                : "Character";
            return assetName + "Audio";
        }

        private static int EnsureAudioCues(
            CharacterAudioDefinition audioDefinition,
            List<string> actionKeys,
            Dictionary<string, float> durations)
        {
            if (audioDefinition == null)
            {
                return 0;
            }

            if (audioDefinition.actionAudioCues == null)
            {
                audioDefinition.actionAudioCues = new List<ActionAudioCue>();
            }

            int added = 0;
            for (int index = 0; index < actionKeys.Count; index += 1)
            {
                string actionKey = actionKeys[index];
                if (ContainsAudioCue(audioDefinition.actionAudioCues, actionKey))
                {
                    continue;
                }

                audioDefinition.actionAudioCues.Add(new ActionAudioCue
                {
                    actionName = actionKey,
                    playbackSpeed = 1f,
                    stopAfterSeconds = durations != null && durations.TryGetValue(actionKey, out float duration)
                        ? duration
                        : 0f,
                });
                added += 1;
            }

            return added;
        }

        private static bool ContainsAudioCue(List<ActionAudioCue> values, string actionKey)
        {
            for (int index = 0; index < values.Count; index += 1)
            {
                ActionAudioCue entry = values[index];
                if (entry != null && string.Equals(entry.actionName, actionKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
