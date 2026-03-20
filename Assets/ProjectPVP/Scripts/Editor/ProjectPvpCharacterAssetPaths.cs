using System.Collections.Generic;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterAssetPaths
    {
        internal const string CharactersRoot = "Assets/ProjectPVP/Characters";

        internal static string[] CharacterSearchRoots => new[] { CharactersRoot };

        internal static IEnumerable<CharacterDefinition> EnumerateDefinitions()
        {
            string[] definitionGuids = AssetDatabase.FindAssets("t:CharacterDefinition", CharacterSearchRoots);
            for (int index = 0; index < definitionGuids.Length; index += 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(definitionGuids[index]);
                CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);
                if (definition != null)
                {
                    yield return definition;
                }
            }
        }

        internal static bool TryGetDefinitionAssetPath(CharacterDefinition definition, out string definitionAssetPath)
        {
            definitionAssetPath = definition != null ? AssetDatabase.GetAssetPath(definition) : string.Empty;
            return !string.IsNullOrWhiteSpace(definitionAssetPath);
        }

        internal static bool TryGetDataFolder(CharacterDefinition definition, out string dataFolderPath)
        {
            dataFolderPath = string.Empty;
            if (!TryGetDefinitionAssetPath(definition, out string definitionPath))
            {
                return false;
            }

            dataFolderPath = Path.GetDirectoryName(definitionPath)?.Replace("\\", "/") ?? string.Empty;
            return !string.IsNullOrWhiteSpace(dataFolderPath);
        }

        internal static bool TryGetCharacterRoot(CharacterDefinition definition, out string characterRootPath)
        {
            characterRootPath = string.Empty;
            if (!TryGetDataFolder(definition, out string dataFolderPath))
            {
                return false;
            }

            characterRootPath = Path.GetDirectoryName(dataFolderPath)?.Replace("\\", "/") ?? string.Empty;
            return !string.IsNullOrWhiteSpace(characterRootPath);
        }

        internal static bool TryGetAnimationsFolder(CharacterDefinition definition, out string animationsFolderPath)
        {
            animationsFolderPath = string.Empty;
            if (!TryGetCharacterRoot(definition, out string characterRootPath))
            {
                return false;
            }

            animationsFolderPath = characterRootPath + "/Animations";
            return true;
        }

        internal static bool TryGetRotationsFolder(CharacterDefinition definition, out string rotationsFolderPath)
        {
            rotationsFolderPath = string.Empty;
            if (!TryGetCharacterRoot(definition, out string characterRootPath))
            {
                return false;
            }

            rotationsFolderPath = characterRootPath + "/Rotations";
            return true;
        }

        internal static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            string relativePath = assetPath.Replace("Assets/", string.Empty).Replace("/", Path.DirectorySeparatorChar.ToString());
            return Path.Combine(projectRoot, "Assets", relativePath);
        }
    }
}
