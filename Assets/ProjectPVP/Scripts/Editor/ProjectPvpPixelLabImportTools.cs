using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    public static class ProjectPvpPixelLabImportTools
    {
        private sealed class PixelLabActionCandidate
        {
            public string sourceActionName = string.Empty;
            public string sourceFolderPath = string.Empty;
            public string targetActionName = string.Empty;
            public string matchReason = string.Empty;
            public int priority;
            public int supportedDirectionCount;
            public int supportedFrameCount;
        }

        private const string PixelLabAuthHeaderEnvironmentVariable = "PIXELLAB_AUTH_HEADER";
        private const string PixelLabZipUrlTemplate = "https://api.pixellab.ai/v2/characters/{0}/zip";

        private static readonly HashSet<string> SupportedActionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "idle",
            "aim",
            "walk",
            "running",
            "shoot",
            "dash",
            "jump",
            "jump_start",
            "jump_air",
            "melee",
            "ult",
            "death",
        };

        [MenuItem("ProjectPVP/Characters/Import PixelLab ZIP To Selected Character", true)]
        private static bool ValidateImportPixelLabZipToSelectedCharacter()
        {
            return Selection.activeObject is CharacterDefinition;
        }

        [MenuItem("ProjectPVP/Characters/Import PixelLab ZIP To Selected Character")]
        private static void ImportPixelLabZipToSelectedCharacter()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogWarning("ProjectPVP: selecione um CharacterDefinition para importar um ZIP do PixelLab.");
                return;
            }

            string zipPath = EditorUtility.OpenFilePanel("Selecione o ZIP exportado pelo PixelLab", string.Empty, "zip");
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                return;
            }

            if (ImportZipIntoCharacter(definition, zipPath, out string summary))
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        [MenuItem("ProjectPVP/Characters/Sync Selected Character From PixelLab", true)]
        private static bool ValidateSyncSelectedCharacterFromPixelLab()
        {
            return Selection.activeObject is CharacterDefinition;
        }

        [MenuItem("ProjectPVP/Characters/Sync Selected Character From PixelLab")]
        private static void SyncSelectedCharacterFromPixelLab()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogWarning("ProjectPVP: selecione um CharacterDefinition para sincronizar com o PixelLab.");
                return;
            }

            if (SyncFromPixelLab(definition, out string summary))
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary);
            }
        }

        [MenuItem("ProjectPVP/Characters/Sync All Configured Characters From PixelLab")]
        internal static void SyncAllConfiguredCharactersFromPixelLab()
        {
            string[] definitionGuids = AssetDatabase.FindAssets("t:CharacterDefinition", new[] { "Assets/ProjectPVP/Characters" });
            int syncedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            for (int index = 0; index < definitionGuids.Length; index += 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(definitionGuids[index]);
                CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);
                if (definition == null)
                {
                    failedCount += 1;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.pixelLabCharacterId))
                {
                    skippedCount += 1;
                    continue;
                }

                if (SyncFromPixelLab(definition, out string summary))
                {
                    syncedCount += 1;
                    Debug.Log(summary);
                }
                else
                {
                    failedCount += 1;
                    Debug.LogWarning(summary);
                }
            }

            Debug.Log("ProjectPVP: sync PixelLab finalizado. Sincronizados: " + syncedCount + ". Ignorados sem id: " + skippedCount + ". Falhas: " + failedCount + ".");
        }

        public static void BatchSyncConfiguredCharactersFromPixelLab()
        {
            SyncAllConfiguredCharactersFromPixelLab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        internal static bool SyncFromPixelLab(CharacterDefinition definition, out string summary)
        {
            summary = "ProjectPVP: falha ao sincronizar com o PixelLab.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.pixelLabCharacterId))
            {
                summary = "ProjectPVP: o personagem " + definition.displayName + " nao possui pixelLabCharacterId configurado.";
                return false;
            }

            if (!TryGetPixelLabAuthHeader(out string authHeader, out string authSummary))
            {
                summary = authSummary;
                return false;
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "ProjectPVP", "PixelLab");
            Directory.CreateDirectory(tempFolder);
            string zipPath = Path.Combine(tempFolder, definition.pixelLabCharacterId + ".zip");

            try
            {
                if (!TryDownloadPixelLabCharacterZip(definition.pixelLabCharacterId, authHeader, zipPath, out string downloadSummary))
                {
                    summary = downloadSummary;
                    return false;
                }

                if (ImportZipIntoCharacter(definition, zipPath, out string importSummary))
                {
                    summary = "ProjectPVP: sync PixelLab concluido para " + definition.displayName + ". " + importSummary;
                    return true;
                }

                summary = importSummary;
                return false;
            }
            finally
            {
                TryDeleteFile(zipPath);
            }
        }

        internal static bool ImportZipIntoCharacter(CharacterDefinition definition, string zipPath, out string summary)
        {
            summary = "ProjectPVP: falha ao importar o ZIP do PixelLab.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                summary = "ProjectPVP: ZIP do PixelLab nao encontrado em " + zipPath;
                return false;
            }

            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrWhiteSpace(definitionPath))
            {
                summary = "ProjectPVP: nao foi possivel localizar o asset do personagem selecionado.";
                return false;
            }

            string characterRoot = Path.GetDirectoryName(Path.GetDirectoryName(definitionPath) ?? string.Empty)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(characterRoot))
            {
                summary = "ProjectPVP: nao foi possivel localizar a pasta raiz do personagem.";
                return false;
            }

            string characterRootFullPath = ToFullPath(characterRoot);
            if (string.IsNullOrWhiteSpace(characterRootFullPath) || !Directory.Exists(characterRootFullPath))
            {
                summary = "ProjectPVP: pasta fisica do personagem nao encontrada.";
                return false;
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "ProjectPVP", "PixelLab", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                ExtractZipToDirectory(zipPath, tempFolder);

                var copiedFilePaths = new List<string>();
                int copiedRotations = CopyRotations(tempFolder, Path.Combine(characterRootFullPath, "Rotations"), copiedFilePaths);
                int copiedFrames = 0;
                int copiedActionFolders = 0;
                int skippedDirections = 0;
                int bakedCount = 0;
                var importedMappings = new List<string>();
                var skippedActions = new List<string>();

                string animationsSourceFolder = Path.Combine(tempFolder, "animations");
                if (Directory.Exists(animationsSourceFolder))
                {
                    string animationsTargetFolder = Path.Combine(characterRootFullPath, "Animations");
                    Directory.CreateDirectory(animationsTargetFolder);

                    Dictionary<string, PixelLabActionCandidate> bestCandidates = BuildBestActionCandidates(
                        definition,
                        animationsSourceFolder,
                        skippedActions,
                        ref skippedDirections);

                    foreach (KeyValuePair<string, PixelLabActionCandidate> pair in bestCandidates)
                    {
                        PixelLabActionCandidate candidate = pair.Value;
                        if (candidate == null)
                        {
                            continue;
                        }

                        bool importedAnyDirection = false;
                        string[] directionFolders = Directory.GetDirectories(candidate.sourceFolderPath);
                        Array.Sort(directionFolders, StringComparer.OrdinalIgnoreCase);

                        for (int directionIndex = 0; directionIndex < directionFolders.Length; directionIndex += 1)
                        {
                            string directionFolder = directionFolders[directionIndex];
                            string directionName = Path.GetFileName(directionFolder);
                            if (!TryMapSupportedDirection(directionName, out string targetDirectionFolderName))
                            {
                                continue;
                            }

                            string targetDirectionFolder = Path.Combine(animationsTargetFolder, candidate.targetActionName, targetDirectionFolderName);
                            copiedFrames += CopyPngFiles(directionFolder, targetDirectionFolder, copiedFilePaths);
                            importedAnyDirection = true;
                        }

                        if (importedAnyDirection)
                        {
                            copiedActionFolders += 1;
                            importedMappings.Add(candidate.sourceActionName + " -> " + candidate.targetActionName + " (" + candidate.matchReason + ")");
                        }
                    }
                }

                int nativeBakeScale = Mathf.Max(1, definition.nativeSpriteBakeScale);
                bakedCount = ProjectPvpCharacterSpriteImportTools.BakePngFilesToNativeScale(copiedFilePaths, nativeBakeScale);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                int optimizedCount = ProjectPvpCharacterSpriteImportTools.OptimizeSpriteImportsInFolders(characterRoot);
                bool rebuilt = ProjectPvpCharacterAnimationSync.RebuildFromFolders(definition, out string rebuildSummary);
                string importedMappingsSummary = BuildImportedMappingsSummary(importedMappings);
                string skippedActionSummary = BuildSkippedActionSummary(skippedActions);

                summary = "ProjectPVP: import PixelLab concluido para " + definition.displayName + ". "
                    + "Rotations copiadas: " + copiedRotations + ". "
                    + "Acoes sincronizadas: " + copiedActionFolders + ". "
                    + "Frames copiados: " + copiedFrames + ". "
                    + "PNGs bakeados para escala nativa: " + bakedCount + " (x" + nativeBakeScale + "). "
                    + "Direcoes ignoradas (fora de east/west/left/right): " + skippedDirections + ". "
                    + "Sprites reimportados/otimizados: " + optimizedCount + ". "
                    + (rebuilt ? rebuildSummary + " " : "Rebuild de animacoes falhou. " + rebuildSummary + " ")
                    + importedMappingsSummary + " "
                    + skippedActionSummary;
                return rebuilt;
            }
            catch (InvalidDataException exception)
            {
                summary = "ProjectPVP: o arquivo selecionado nao parece ser um ZIP valido do PixelLab. " + exception.Message;
                return false;
            }
            catch (IOException exception)
            {
                summary = "ProjectPVP: nao foi possivel importar o ZIP do PixelLab. " + exception.Message;
                return false;
            }
            finally
            {
                TryDeleteDirectory(tempFolder);
            }
        }

        private static Dictionary<string, PixelLabActionCandidate> BuildBestActionCandidates(
            CharacterDefinition definition,
            string animationsSourceFolder,
            List<string> skippedActions,
            ref int skippedDirections)
        {
            var bestCandidates = new Dictionary<string, PixelLabActionCandidate>(StringComparer.OrdinalIgnoreCase);
            string[] actionFolders = Directory.GetDirectories(animationsSourceFolder);
            Array.Sort(actionFolders, StringComparer.OrdinalIgnoreCase);

            for (int actionIndex = 0; actionIndex < actionFolders.Length; actionIndex += 1)
            {
                string actionFolder = actionFolders[actionIndex];
                string sourceActionName = Path.GetFileName(actionFolder);
                if (string.IsNullOrWhiteSpace(sourceActionName))
                {
                    continue;
                }

                PixelLabActionCandidate candidate = BuildActionCandidate(definition, sourceActionName, actionFolder, ref skippedDirections);
                if (candidate == null)
                {
                    skippedActions.Add(sourceActionName);
                    continue;
                }

                if (!bestCandidates.TryGetValue(candidate.targetActionName, out PixelLabActionCandidate currentBest)
                    || IsCandidateBetter(candidate, currentBest))
                {
                    bestCandidates[candidate.targetActionName] = candidate;
                }
            }

            return bestCandidates;
        }

        private static PixelLabActionCandidate BuildActionCandidate(
            CharacterDefinition definition,
            string sourceActionName,
            string actionFolder,
            ref int skippedDirections)
        {
            if (!TryResolveTargetActionName(definition, sourceActionName, out string targetActionName, out int priority, out string matchReason))
            {
                return null;
            }

            GetSupportedDirectionStats(actionFolder, out int supportedDirectionCount, out int supportedFrameCount, ref skippedDirections);
            if (supportedDirectionCount <= 0 || supportedFrameCount <= 0)
            {
                return null;
            }

            return new PixelLabActionCandidate
            {
                sourceActionName = sourceActionName,
                sourceFolderPath = actionFolder,
                targetActionName = targetActionName,
                matchReason = matchReason,
                priority = priority,
                supportedDirectionCount = supportedDirectionCount,
                supportedFrameCount = supportedFrameCount,
            };
        }

        private static bool TryResolveTargetActionName(
            CharacterDefinition definition,
            string sourceActionName,
            out string targetActionName,
            out int priority,
            out string matchReason)
        {
            targetActionName = string.Empty;
            priority = 0;
            matchReason = string.Empty;

            string normalized = NormalizeActionName(sourceActionName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (SupportedActionNames.Contains(normalized))
            {
                targetActionName = normalized;
                priority = 1000;
                matchReason = "exact";
                return true;
            }

            if (string.Equals(normalized, "aiming", StringComparison.OrdinalIgnoreCase))
            {
                targetActionName = "aim";
                priority = 980;
                matchReason = "runtime alias";
                return true;
            }

            if (TryResolveActionAlias(definition, normalized, out targetActionName))
            {
                priority = 950;
                matchReason = "character alias";
                return true;
            }

            if (ContainsToken(normalized, "death")
                || ContainsToken(normalized, "dying")
                || ContainsToken(normalized, "dead")
                || ContainsToken(normalized, "die")
                || ContainsToken(normalized, "ko")
                || ContainsToken(normalized, "knockout"))
            {
                targetActionName = "death";
                priority = 920;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "ultimate") || StartsWithToken(normalized, "ult") || ContainsToken(normalized, "special"))
            {
                targetActionName = "ult";
                priority = 900;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "dash"))
            {
                targetActionName = "dash";
                priority = 880;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "aim") || ContainsToken(normalized, "aiming"))
            {
                targetActionName = "aim";
                priority = 860;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "running") || ContainsToken(normalized, "run"))
            {
                targetActionName = "running";
                priority = 840;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "walk") || ContainsToken(normalized, "walking"))
            {
                targetActionName = "walk";
                priority = 820;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "idle") || ContainsToken(normalized, "breathing") || ContainsToken(normalized, "stance"))
            {
                targetActionName = "idle";
                priority = 800;
                matchReason = "keyword";
                return true;
            }

            if (ContainsPhrase(normalized, "jump start") || ContainsToken(normalized, "takeoff"))
            {
                targetActionName = "jump_start";
                priority = 780;
                matchReason = "keyword";
                return true;
            }

            if (ContainsPhrase(normalized, "jump air") || ContainsToken(normalized, "airborne"))
            {
                targetActionName = "jump_air";
                priority = 760;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "jump"))
            {
                targetActionName = "jump";
                priority = 740;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "shoot") || ContainsToken(normalized, "shooting") || ContainsToken(normalized, "firing") || ContainsToken(normalized, "fireball"))
            {
                targetActionName = "shoot";
                priority = 720;
                matchReason = "keyword";
                return true;
            }

            if (ContainsToken(normalized, "melee")
                || ContainsToken(normalized, "melle")
                || ContainsToken(normalized, "swing")
                || ContainsToken(normalized, "slash")
                || ContainsToken(normalized, "sword")
                || ContainsToken(normalized, "katana")
                || ContainsToken(normalized, "punch")
                || ContainsToken(normalized, "kick")
                || ContainsToken(normalized, "attack"))
            {
                targetActionName = "melee";
                priority = 700;
                matchReason = "keyword";
                return true;
            }

            return false;
        }

        private static bool TryResolveActionAlias(CharacterDefinition definition, string normalizedSourceActionName, out string targetActionName)
        {
            targetActionName = string.Empty;
            if (definition == null || definition.pixelLabActionAliases == null)
            {
                return false;
            }

            for (int index = 0; index < definition.pixelLabActionAliases.Count; index += 1)
            {
                PixelLabActionAlias alias = definition.pixelLabActionAliases[index];
                if (alias == null || string.IsNullOrWhiteSpace(alias.pattern) || string.IsNullOrWhiteSpace(alias.actionName))
                {
                    continue;
                }

                string normalizedPattern = NormalizeActionName(alias.pattern);
                string normalizedTargetActionName = alias.actionName.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedPattern) || !SupportedActionNames.Contains(normalizedTargetActionName))
                {
                    continue;
                }

                if (!ContainsPhrase(normalizedSourceActionName, normalizedPattern))
                {
                    continue;
                }

                targetActionName = normalizedTargetActionName;
                return true;
            }

            return false;
        }

        private static bool IsCandidateBetter(PixelLabActionCandidate candidate, PixelLabActionCandidate currentBest)
        {
            if (candidate.priority != currentBest.priority)
            {
                return candidate.priority > currentBest.priority;
            }

            if (candidate.supportedDirectionCount != currentBest.supportedDirectionCount)
            {
                return candidate.supportedDirectionCount > currentBest.supportedDirectionCount;
            }

            if (candidate.supportedFrameCount != currentBest.supportedFrameCount)
            {
                return candidate.supportedFrameCount > currentBest.supportedFrameCount;
            }

            return string.Compare(candidate.sourceActionName, currentBest.sourceActionName, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static void GetSupportedDirectionStats(
            string actionFolder,
            out int supportedDirectionCount,
            out int supportedFrameCount,
            ref int skippedDirections)
        {
            supportedDirectionCount = 0;
            supportedFrameCount = 0;

            string[] directionFolders = Directory.GetDirectories(actionFolder);
            Array.Sort(directionFolders, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < directionFolders.Length; index += 1)
            {
                string directionFolder = directionFolders[index];
                string directionName = Path.GetFileName(directionFolder);
                if (!TryMapSupportedDirection(directionName, out _))
                {
                    skippedDirections += 1;
                    continue;
                }

                string[] pngFiles = Directory.GetFiles(directionFolder, "*.png", SearchOption.TopDirectoryOnly);
                if (pngFiles.Length <= 0)
                {
                    continue;
                }

                supportedDirectionCount += 1;
                supportedFrameCount += pngFiles.Length;
            }
        }

        private static int CopyRotations(string extractedZipFolder, string rotationsTargetFolder, List<string> copiedFilePaths)
        {
            string rotationsSourceFolder = Path.Combine(extractedZipFolder, "rotations");
            if (!Directory.Exists(rotationsSourceFolder))
            {
                return 0;
            }

            return CopyPngFiles(rotationsSourceFolder, rotationsTargetFolder, copiedFilePaths);
        }

        private static void ExtractZipToDirectory(string zipPath, string destinationFolder)
        {
            string normalizedDestinationRoot = Path.GetFullPath(destinationFolder);
            if (!normalizedDestinationRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                normalizedDestinationRoot += Path.DirectorySeparatorChar;
            }
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                for (int index = 0; index < archive.Entries.Count; index += 1)
                {
                    ZipArchiveEntry entry = archive.Entries[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    string entryPath = BuildSafeZipEntryPath(entry.FullName);
                    if (string.IsNullOrWhiteSpace(entryPath))
                    {
                        continue;
                    }

                    string destinationPath = Path.GetFullPath(Path.Combine(destinationFolder, entryPath));
                    if (!destinationPath.StartsWith(normalizedDestinationRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    string parentDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrWhiteSpace(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    using (Stream sourceStream = entry.Open())
                    using (FileStream destinationStream = File.Create(destinationPath))
                    {
                        sourceStream.CopyTo(destinationStream);
                    }
                }
            }
        }

        private static string BuildSafeZipEntryPath(string entryFullName)
        {
            if (string.IsNullOrWhiteSpace(entryFullName))
            {
                return string.Empty;
            }

            string[] rawSegments = entryFullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawSegments.Length == 0)
            {
                return string.Empty;
            }

            var safeSegments = new List<string>(rawSegments.Length);
            for (int index = 0; index < rawSegments.Length; index += 1)
            {
                string safeSegment = SanitizeZipEntrySegment(rawSegments[index]);
                if (string.IsNullOrWhiteSpace(safeSegment))
                {
                    continue;
                }

                safeSegments.Add(safeSegment);
            }

            return safeSegments.Count > 0
                ? Path.Combine(safeSegments.ToArray())
                : string.Empty;
        }

        private static string SanitizeZipEntrySegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            string trimmed = segment.Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(trimmed.Length);
            for (int index = 0; index < trimmed.Length; index += 1)
            {
                char character = trimmed[index];
                if (character < 32 || character == ':' || character == '*' || character == '?' || character == '"' || character == '<' || character == '>' || character == '|')
                {
                    builder.Append('_');
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim().TrimEnd('.');
        }

        private static int CopyPngFiles(string sourceFolder, string targetFolder, List<string> copiedFilePaths)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                return 0;
            }

            Directory.CreateDirectory(targetFolder);
            string[] pngFiles = Directory.GetFiles(sourceFolder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(pngFiles, StringComparer.OrdinalIgnoreCase);

            int copiedCount = 0;
            for (int index = 0; index < pngFiles.Length; index += 1)
            {
                string sourceFile = pngFiles[index];
                string targetFile = Path.Combine(targetFolder, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, targetFile, overwrite: true);
                copiedFilePaths?.Add(targetFile);
                copiedCount += 1;
            }

            return copiedCount;
        }

        private static bool TryMapSupportedDirection(string directionName, out string targetDirectionFolderName)
        {
            targetDirectionFolderName = string.Empty;
            if (string.IsNullOrWhiteSpace(directionName))
            {
                return false;
            }

            switch (directionName.Trim().ToLowerInvariant())
            {
                case "east":
                case "right":
                    targetDirectionFolderName = "east";
                    return true;
                case "west":
                case "left":
                    targetDirectionFolderName = "west";
                    return true;
                default:
                    return false;
            }
        }

        private static string BuildImportedMappingsSummary(List<string> importedMappings)
        {
            if (importedMappings == null || importedMappings.Count == 0)
            {
                return "Nenhuma acao de runtime foi sincronizada a partir do ZIP.";
            }

            importedMappings.Sort(StringComparer.OrdinalIgnoreCase);
            return "Mapeamentos aplicados: " + string.Join("; ", importedMappings) + ".";
        }

        private static string BuildSkippedActionSummary(List<string> skippedActions)
        {
            if (skippedActions == null || skippedActions.Count == 0)
            {
                return "Nenhuma acao foi ignorada por incompatibilidade de nome.";
            }

            skippedActions.Sort(StringComparer.OrdinalIgnoreCase);
            return "Acoes ignoradas por nao terem match seguro com o runtime: " + string.Join(", ", skippedActions) + ".";
        }

        private static bool TryGetPixelLabAuthHeader(out string authHeader, out string summary)
        {
            authHeader = Environment.GetEnvironmentVariable(PixelLabAuthHeaderEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                authHeader = Environment.GetEnvironmentVariable(PixelLabAuthHeaderEnvironmentVariable, EnvironmentVariableTarget.User);
            }

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                authHeader = Environment.GetEnvironmentVariable(PixelLabAuthHeaderEnvironmentVariable, EnvironmentVariableTarget.Machine);
            }

            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                summary = string.Empty;
                return true;
            }

            summary = "ProjectPVP: cabecalho de autenticacao do PixelLab nao encontrado em " + PixelLabAuthHeaderEnvironmentVariable + ".";
            return false;
        }

        private static bool TryDownloadPixelLabCharacterZip(string characterId, string authHeader, string zipPath, out string summary)
        {
            summary = "ProjectPVP: falha ao baixar o ZIP do PixelLab.";
            if (string.IsNullOrWhiteSpace(characterId))
            {
                summary = "ProjectPVP: pixelLabCharacterId vazio.";
                return false;
            }

            try
            {
                string requestUrl = string.Format(PixelLabZipUrlTemplate, characterId);
                var request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Timeout = 120000;
                request.ReadWriteTimeout = 120000;
                request.Headers[HttpRequestHeader.Authorization] = authHeader;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (var fileStream = File.Create(zipPath))
                {
                    if (responseStream == null)
                    {
                        summary = "ProjectPVP: resposta sem conteudo ao baixar o ZIP do PixelLab.";
                        return false;
                    }

                    responseStream.CopyTo(fileStream);
                }

                summary = "ProjectPVP: ZIP do PixelLab baixado com sucesso.";
                return true;
            }
            catch (WebException exception)
            {
                if (exception.Response is HttpWebResponse response)
                {
                    int statusCode = (int)response.StatusCode;
                    if (statusCode == 423)
                    {
                        summary = "ProjectPVP: o personagem do PixelLab ainda esta sendo gerado; o ZIP ainda nao esta pronto.";
                        return false;
                    }

                    summary = "ProjectPVP: falha ao baixar ZIP do PixelLab. HTTP " + statusCode + " (" + response.StatusDescription + ").";
                    return false;
                }

                summary = "ProjectPVP: erro de rede ao baixar ZIP do PixelLab. " + exception.Message;
                return false;
            }
            catch (IOException exception)
            {
                summary = "ProjectPVP: nao foi possivel salvar o ZIP do PixelLab. " + exception.Message;
                return false;
            }
        }

        private static string NormalizeActionName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool lastWasSeparator = true;

            for (int index = 0; index < value.Length; index += 1)
            {
                char character = char.ToLowerInvariant(value[index]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    lastWasSeparator = false;
                    continue;
                }

                if (!lastWasSeparator)
                {
                    builder.Append(' ');
                    lastWasSeparator = true;
                }
            }

            string normalized = builder.ToString().Trim();
            if (normalized.StartsWith("custom ", StringComparison.Ordinal))
            {
                normalized = normalized.Substring("custom ".Length).Trim();
            }

            return normalized;
        }

        private static bool StartsWithToken(string normalizedValue, string token)
        {
            if (string.IsNullOrWhiteSpace(normalizedValue) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedToken = NormalizeActionName(token);
            return normalizedValue.StartsWith(normalizedToken + " ", StringComparison.Ordinal)
                || string.Equals(normalizedValue, normalizedToken, StringComparison.Ordinal);
        }

        private static bool ContainsToken(string normalizedValue, string token)
        {
            if (string.IsNullOrWhiteSpace(normalizedValue) || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalizedToken = NormalizeActionName(token);
            string paddedValue = " " + normalizedValue + " ";
            return paddedValue.Contains(" " + normalizedToken + " ");
        }

        private static bool ContainsPhrase(string normalizedValue, string phrase)
        {
            if (string.IsNullOrWhiteSpace(normalizedValue) || string.IsNullOrWhiteSpace(phrase))
            {
                return false;
            }

            return normalizedValue.IndexOf(NormalizeActionName(phrase), StringComparison.Ordinal) >= 0;
        }

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            string relativePath = assetPath.Replace("Assets/", string.Empty).Replace("/", Path.DirectorySeparatorChar.ToString());
            return Path.Combine(projectRoot, "Assets", relativePath);
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
