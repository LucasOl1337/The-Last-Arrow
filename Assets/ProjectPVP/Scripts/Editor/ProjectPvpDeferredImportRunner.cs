using System;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [InitializeOnLoad]
    internal static class ProjectPvpDeferredImportRunner
    {
        [Serializable]
        private sealed class ImportRequest
        {
            public string operation = "import_zip";
            public string characterDefinitionAssetPath = string.Empty;
            public string zipPath = string.Empty;
        }

        private static readonly string RequestFolderPath;
        private static double s_nextPollTime;

        static ProjectPvpDeferredImportRunner()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
            RequestFolderPath = Path.Combine(projectRoot, "Temp", "ProjectPvpPixelLabRequests");
            EditorApplication.update += PollForRequests;
        }

        private static void PollForRequests()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < s_nextPollTime)
            {
                return;
            }

            s_nextPollTime = EditorApplication.timeSinceStartup + 1d;

            if (!Directory.Exists(RequestFolderPath))
            {
                return;
            }

            string[] requestFiles = Directory.GetFiles(RequestFolderPath, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(requestFiles, StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < requestFiles.Length; index += 1)
            {
                ProcessRequest(requestFiles[index]);
            }
        }

        private static void ProcessRequest(string requestFilePath)
        {
            if (string.IsNullOrWhiteSpace(requestFilePath) || !File.Exists(requestFilePath))
            {
                return;
            }

            string resultPath = requestFilePath + ".result.txt";

            try
            {
                ImportRequest request = JsonUtility.FromJson<ImportRequest>(File.ReadAllText(requestFilePath));
                if (request == null
                    || string.IsNullOrWhiteSpace(request.characterDefinitionAssetPath))
                {
                    File.WriteAllText(resultPath, "Invalid import request.");
                    File.Delete(requestFilePath);
                    return;
                }

                CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(request.characterDefinitionAssetPath);
                if (definition == null)
                {
                    File.WriteAllText(resultPath, "CharacterDefinition not found: " + request.characterDefinitionAssetPath);
                    File.Delete(requestFilePath);
                    return;
                }

                bool imported = ExecuteRequest(request, definition, out string summary);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(resultPath, (imported ? "SUCCESS: " : "FAILURE: ") + summary);
                File.Delete(requestFilePath);
            }
            catch (Exception exception)
            {
                File.WriteAllText(resultPath, "ERROR: " + exception);
                try
                {
                    File.Delete(requestFilePath);
                }
                catch (IOException)
                {
                }
            }
        }

        private static bool ExecuteRequest(ImportRequest request, CharacterDefinition definition, out string summary)
        {
            string operation = string.IsNullOrWhiteSpace(request.operation)
                ? "import_zip"
                : request.operation.Trim().ToLowerInvariant();

            switch (operation)
            {
                case "optimize_and_rebuild":
                    return OptimizeAndRebuild(definition, out summary);
                case "import_zip":
                default:
                    if (string.IsNullOrWhiteSpace(request.zipPath))
                    {
                        summary = "ZIP path is required for import_zip.";
                        return false;
                    }

                    return ProjectPvpPixelLabImportTools.ImportZipIntoCharacter(definition, request.zipPath, out summary);
            }
        }

        private static bool OptimizeAndRebuild(CharacterDefinition definition, out string summary)
        {
            summary = "Unable to optimize and rebuild character.";
            if (definition == null)
            {
                summary = "CharacterDefinition is null.";
                return false;
            }

            if (!ProjectPvpCharacterAssetPaths.TryGetCharacterRoot(definition, out string characterRoot))
            {
                summary = "Character root not found.";
                return false;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int optimizedCount = ProjectPvpCharacterSpriteImportTools.OptimizeSpriteImportsInFolders(characterRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool rebuilt = ProjectPvpCharacterAnimationSync.RebuildFromFolders(definition, out string rebuildSummary);

            summary = "ProjectPVP: optimize_and_rebuild concluido para "
                + definition.displayName
                + ". Imports atualizados: "
                + optimizedCount
                + ". "
                + rebuildSummary;
            return rebuilt;
        }
    }
}
