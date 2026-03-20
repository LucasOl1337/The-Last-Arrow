using System;
using System.Collections.Generic;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterAnimationSync
    {
        private const string SharedDirectionKey = "shared";

        private readonly struct ClipTemplate
        {
            public ClipTemplate(float framesPerSecond, bool loop)
            {
                this.framesPerSecond = framesPerSecond;
                this.loop = loop;
            }

            public readonly float framesPerSecond;
            public readonly bool loop;
        }

        [MenuItem("ProjectPVP/Characters/Rebuild Animation Clips From Folders", true)]
        private static bool ValidateRebuildSelectedCharacter()
        {
            return Selection.activeObject is CharacterDefinition;
        }

        [MenuItem("ProjectPVP/Characters/Rebuild Animation Clips From Folders")]
        private static void RebuildSelectedCharacter()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogWarning("ProjectPVP: selecione um CharacterDefinition para reconstruir os clips.");
                return;
            }

            if (RebuildFromFolders(definition, out string summary))
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary);
            }
        }

        [MenuItem("ProjectPVP/Characters/Rebuild All Character Clips From Folders")]
        private static void RebuildAllCharacters()
        {
            RebuildAllCharactersFromMenu();
        }

        [MenuItem("ProjectPVP/Characters/Audit Non Lateral Direction Folders")]
        private static void AuditNonLateralDirectionFolders()
        {
            var invalidFolders = new List<string>();

            foreach (CharacterDefinition definition in ProjectPvpCharacterAssetPaths.EnumerateDefinitions())
            {
                if (!ProjectPvpCharacterAssetPaths.TryGetAnimationsFolder(definition, out string animationsFolderPath))
                {
                    continue;
                }

                string animationsFullPath = ProjectPvpCharacterAssetPaths.ToFullPath(animationsFolderPath);
                if (!Directory.Exists(animationsFullPath))
                {
                    continue;
                }

                string[] directionDirectories = Directory.GetDirectories(animationsFullPath, "*", SearchOption.AllDirectories);
                for (int folderIndex = 0; folderIndex < directionDirectories.Length; folderIndex += 1)
                {
                    string folderName = Path.GetFileName(directionDirectories[folderIndex]);
                    if (TryMapFolderToDirection(folderName, out _))
                    {
                        continue;
                    }

                    if (!LooksLikeDirectionFolder(folderName))
                    {
                        continue;
                    }

                    invalidFolders.Add(directionDirectories[folderIndex].Replace("\\", "/"));
                }
            }

            if (invalidFolders.Count == 0)
            {
                Debug.Log("ProjectPVP: nenhuma pasta de direcao fora do padrao left/right foi encontrada.");
                return;
            }

            invalidFolders.Sort(StringComparer.OrdinalIgnoreCase);
            Debug.Log("ProjectPVP: pastas de direcao fora do padrao left/right encontradas:\n- " + string.Join("\n- ", invalidFolders));
        }

        internal static void RebuildAllCharactersFromMenu()
        {
            int rebuiltCount = 0;
            int failedCount = 0;

            foreach (CharacterDefinition definition in ProjectPvpCharacterAssetPaths.EnumerateDefinitions())
            {
                if (RebuildFromFolders(definition, out string summary))
                {
                    rebuiltCount += 1;
                    Debug.Log(summary);
                }
                else
                {
                    failedCount += 1;
                    Debug.LogWarning(summary);
                }
            }

            Debug.Log("ProjectPVP: rebuild all finalizado. Sucesso: " + rebuiltCount + ". Falhas: " + failedCount + ".");
        }

        public static bool RebuildFromFolders(CharacterDefinition definition, out string summary)
        {
            summary = "ProjectPVP: falha ao reconstruir clips.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            if (!ProjectPvpCharacterAssetPaths.TryGetAnimationsFolder(definition, out string animationsFolderPath))
            {
                summary = "ProjectPVP: nao foi possivel localizar a pasta raiz do personagem.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(animationsFolderPath))
            {
                summary = "ProjectPVP: pasta Animations nao encontrada em " + animationsFolderPath;
                return false;
            }

            string animationsFullPath = ProjectPvpCharacterAssetPaths.ToFullPath(animationsFolderPath);
            if (!Directory.Exists(animationsFullPath))
            {
                summary = "ProjectPVP: pasta fisica de animacoes nao encontrada.";
                return false;
            }

            Dictionary<string, ClipTemplate> existingTemplates = BuildExistingTemplates(definition.GetActionAnimations());
            var rebuiltClips = new List<ActionSpriteAnimation>();
            string[] actionDirectories = Directory.GetDirectories(animationsFullPath);
            Array.Sort(actionDirectories, StringComparer.OrdinalIgnoreCase);

            int ignoredDirectionFolders = 0;
            for (int actionIndex = 0; actionIndex < actionDirectories.Length; actionIndex += 1)
            {
                string actionDirectory = actionDirectories[actionIndex];
                string actionName = Path.GetFileName(actionDirectory);
                if (string.IsNullOrWhiteSpace(actionName))
                {
                    continue;
                }

                var clipsByDirection = new Dictionary<string, List<Sprite>>(StringComparer.OrdinalIgnoreCase);
                var directionPriorityByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                List<Sprite> sharedRootFrames = LoadSpritesFromFolder(actionDirectory, topDirectoryOnly: true);
                if (sharedRootFrames.Count > 0)
                {
                    clipsByDirection[SharedDirectionKey] = sharedRootFrames;
                    directionPriorityByKey[SharedDirectionKey] = 0;
                }

                string[] directionDirectories = Directory.GetDirectories(actionDirectory);
                Array.Sort(directionDirectories, StringComparer.OrdinalIgnoreCase);
                for (int directionIndex = 0; directionIndex < directionDirectories.Length; directionIndex += 1)
                {
                    string directionDirectory = directionDirectories[directionIndex];
                    string folderName = Path.GetFileName(directionDirectory);
                    if (TryMapFolderToDirection(folderName, out string directionKey))
                    {
                        List<Sprite> frames = LoadSpritesFromFolder(directionDirectory, topDirectoryOnly: true);
                        if (frames.Count > 0)
                        {
                            int sourcePriority = ResolveDirectionSourcePriority(folderName);
                            if (!directionPriorityByKey.TryGetValue(directionKey, out int currentPriority) || sourcePriority >= currentPriority)
                            {
                                clipsByDirection[directionKey] = frames;
                                directionPriorityByKey[directionKey] = sourcePriority;
                            }
                        }

                        continue;
                    }

                    if (LooksLikeDirectionFolder(folderName))
                    {
                        List<Sprite> sharedFrames = LoadSpritesFromFolder(directionDirectory, topDirectoryOnly: true);
                        if (sharedFrames.Count > 0)
                        {
                            int sourcePriority = ResolveSharedSourcePriority(folderName);
                            if (!directionPriorityByKey.TryGetValue(SharedDirectionKey, out int currentPriority) || sourcePriority >= currentPriority)
                            {
                                clipsByDirection[SharedDirectionKey] = sharedFrames;
                                directionPriorityByKey[SharedDirectionKey] = sourcePriority;
                            }
                        }

                        continue;
                    }

                    ignoredDirectionFolders += 1;
                }

                AddClipIfPresent(rebuiltClips, existingTemplates, actionName, "left", clipsByDirection);
                AddClipIfPresent(rebuiltClips, existingTemplates, actionName, "right", clipsByDirection);
            }

            Undo.RecordObject(definition, "Rebuild Character Animation Clips");
            definition.actions = BuildActionConfigs(rebuiltClips, existingTemplates, definition.actions);
            EditorUtility.SetDirty(definition);

            if (definition.animationCatalog != null)
            {
                Undo.RecordObject(definition.animationCatalog, "Clear Legacy Animation Catalog");
                definition.animationCatalog.actionSpriteAnimations = new List<ActionSpriteAnimation>();
                EditorUtility.SetDirty(definition.animationCatalog);
            }

            string actionConfigSummary = string.Empty;
            if (ProjectPvpCharacterActionConfigSync.Sync(definition, out string inlineActionSummary))
            {
                actionConfigSummary = inlineActionSummary;
            }

            AssetDatabase.SaveAssets();

            summary = "ProjectPVP: " + definition.displayName + " reconstruido com " + rebuiltClips.Count + " clips. "
                + "Somente left/right foram aceitos; pastas fora desse padrao ignoradas: " + ignoredDirectionFolders + "."
                + (string.IsNullOrWhiteSpace(actionConfigSummary) ? string.Empty : " " + actionConfigSummary);
            return true;
        }

        private static void AddClipIfPresent(
            List<ActionSpriteAnimation> rebuiltClips,
            Dictionary<string, ClipTemplate> existingTemplates,
            string actionName,
            string directionKey,
            Dictionary<string, List<Sprite>> clipsByDirection)
        {
            if (!TryResolveFramesForDirection(directionKey, clipsByDirection, out List<Sprite> frames))
            {
                return;
            }

            ClipTemplate template = ResolveTemplate(existingTemplates, actionName, directionKey);
            rebuiltClips.Add(new ActionSpriteAnimation
            {
                actionName = actionName,
                directionKey = directionKey,
                framesPerSecond = template.framesPerSecond,
                loop = template.loop,
                frames = frames,
            });
        }

        private static bool TryResolveFramesForDirection(
            string directionKey,
            Dictionary<string, List<Sprite>> clipsByDirection,
            out List<Sprite> frames)
        {
            frames = null;
            if (clipsByDirection == null || string.IsNullOrWhiteSpace(directionKey))
            {
                return false;
            }

            if (clipsByDirection.TryGetValue(directionKey, out frames) && frames != null && frames.Count > 0)
            {
                return true;
            }

            return clipsByDirection.TryGetValue(SharedDirectionKey, out frames) && frames != null && frames.Count > 0;
        }

        private static List<CharacterActionConfig> BuildActionConfigs(
            List<ActionSpriteAnimation> rebuiltClips,
            Dictionary<string, ClipTemplate> existingTemplates,
            List<CharacterActionConfig> existingActions)
        {
            var actionsByKey = new Dictionary<string, CharacterActionConfig>(StringComparer.OrdinalIgnoreCase);
            if (existingActions != null)
            {
                for (int index = 0; index < existingActions.Count; index += 1)
                {
                    CharacterActionConfig action = existingActions[index];
                    if (action == null || string.IsNullOrWhiteSpace(action.actionName))
                    {
                        continue;
                    }

                    actionsByKey[action.actionName] = action;
                    action.animations = new List<DirectionalSpriteAnimation>();
                }
            }

            for (int index = 0; index < rebuiltClips.Count; index += 1)
            {
                ActionSpriteAnimation clip = rebuiltClips[index];
                if (clip == null || string.IsNullOrWhiteSpace(clip.actionName))
                {
                    continue;
                }

                if (!actionsByKey.TryGetValue(clip.actionName, out CharacterActionConfig action))
                {
                    action = new CharacterActionConfig
                    {
                        actionName = clip.actionName,
                    };
                    actionsByKey[clip.actionName] = action;
                }

                action.animations.Add(new DirectionalSpriteAnimation
                {
                    directionKey = clip.directionKey,
                    framesPerSecond = clip.framesPerSecond,
                    loop = clip.loop,
                    frames = clip.frames != null ? new List<Sprite>(clip.frames) : new List<Sprite>(),
                });

                if (action.speed <= 0.01f)
                {
                    action.speed = clip.framesPerSecond > 0.01f ? clip.framesPerSecond : 12f;
                }

                if (action.duration <= 0.01f && clip.frames != null && clip.frames.Count > 0)
                {
                    float framesPerSecond = clip.framesPerSecond > 0.01f ? clip.framesPerSecond : 12f;
                    action.duration = Mathf.Max(0.01f, clip.frames.Count / framesPerSecond);
                }

                if (action.colliderOverride != null && string.IsNullOrWhiteSpace(action.colliderOverride.actionName))
                {
                    action.colliderOverride.actionName = action.actionName;
                }
            }

            var rebuiltActions = new List<CharacterActionConfig>(actionsByKey.Values);
            rebuiltActions.Sort((left, right) => string.Compare(left.actionName, right.actionName, StringComparison.OrdinalIgnoreCase));
            return rebuiltActions;
        }

        private static Dictionary<string, ClipTemplate> BuildExistingTemplates(IReadOnlyList<ActionSpriteAnimation> clips)
        {
            var templates = new Dictionary<string, ClipTemplate>(StringComparer.OrdinalIgnoreCase);
            if (clips == null)
            {
                return templates;
            }

            for (int index = 0; index < clips.Count; index += 1)
            {
                ActionSpriteAnimation clip = clips[index];
                if (clip == null || string.IsNullOrWhiteSpace(clip.actionName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(clip.directionKey))
                {
                    continue;
                }

                string directionKey = clip.directionKey.Trim().ToLowerInvariant();
                string key = BuildClipKey(clip.actionName, directionKey);
                templates[key] = new ClipTemplate(
                    clip.framesPerSecond > 0f ? clip.framesPerSecond : 12f,
                    clip.loop);
            }

            return templates;
        }

        private static ClipTemplate ResolveTemplate(Dictionary<string, ClipTemplate> templates, string actionName, string directionKey)
        {
            foreach (string candidateKey in EnumerateCandidateClipKeys(actionName, directionKey))
            {
                if (templates.TryGetValue(candidateKey, out ClipTemplate template))
                {
                    return template;
                }
            }

            return new ClipTemplate(12f, ShouldLoopByDefault(actionName));
        }

        private static IEnumerable<string> EnumerateCandidateClipKeys(string actionName, string directionKey)
        {
            yield return BuildClipKey(actionName, directionKey);
            yield return BuildClipKey(actionName, "right");
            yield return BuildClipKey(actionName, "left");
        }

        private static string BuildClipKey(string actionName, string directionKey)
        {
            return (actionName ?? string.Empty).Trim().ToLowerInvariant() + ":" + (directionKey ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool TryMapFolderToDirection(string folderName, out string directionKey)
        {
            directionKey = string.Empty;
            return ActionCatalog.LoadDefault().TryMapAnimationFolderDirection(folderName, out directionKey, out bool sharedFallback)
                && !sharedFallback;
        }

        private static bool LooksLikeDirectionFolder(string folderName)
        {
            return ActionCatalog.LoadDefault().TryMapAnimationFolderDirection(folderName, out _, out bool sharedFallback)
                && sharedFallback;
        }

        private static int ResolveDirectionSourcePriority(string folderName)
        {
            switch ((folderName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "east":
                case "west":
                    return 2;
                case "left":
                case "right":
                    return 1;
                default:
                    return 0;
            }
        }

        private static int ResolveSharedSourcePriority(string folderName)
        {
            switch ((folderName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "shared":
                case "default":
                    return 2;
                case "north":
                case "south":
                case "up":
                case "down":
                    return 1;
                default:
                    return 0;
            }
        }

        private static List<Sprite> LoadSpritesFromFolder(string folderPath, bool topDirectoryOnly)
        {
            var sprites = new List<Sprite>();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return sprites;
            }

            SearchOption searchOption = topDirectoryOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            string[] files = Directory.GetFiles(folderPath, "*.png", searchOption);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < files.Length; index += 1)
            {
                string assetPath = ToAssetPath(files[index]);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites;
        }

        private static string ToFullPath(string assetPath)
        {
            return ProjectPvpCharacterAssetPaths.ToFullPath(assetPath);
        }

        private static string ToAssetPath(string fullPath)
        {
            string normalizedFullPath = fullPath.Replace("\\", "/");
            string normalizedAssetsPath = Application.dataPath.Replace("\\", "/");
            if (!normalizedFullPath.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "Assets" + normalizedFullPath.Substring(normalizedAssetsPath.Length);
        }

        private static bool ShouldLoopByDefault(string actionName)
        {
            switch ((actionName ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "idle":
                case "walk":
                case "running":
                case "jump_air":
                case "aim":
                case "aiming":
                    return true;
                default:
                    return false;
            }
        }
    }
}
