using System.IO;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterPortraitRepairTools
    {
        private const string DefaultPortraitFileName = "south.png";

        [MenuItem("ProjectPVP/Characters/Repair Default Portrait Sprites")]
        public static void RepairDefaultPortraitSpritesFromFolders()
        {
            int repairedCount = 0;
            int missingCount = 0;
            int profileRepairCount = 0;

            foreach (CharacterDefinition definition in ProjectPvpCharacterAssetPaths.EnumerateDefinitions())
            {
                if (!TryRepairDefaultPortrait(definition))
                {
                    missingCount += 1;
                    continue;
                }

                repairedCount += 1;
                if (TryRepairBootstrapProfile(definition))
                {
                    profileRepairCount += 1;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "ProjectPVP: default portrait repair finished. Repaired: "
                + repairedCount
                + ". Profiles: "
                + profileRepairCount
                + ". Missing: "
                + missingCount
                + ".");
        }

        private static bool TryRepairDefaultPortrait(CharacterDefinition definition)
        {
            if (definition == null || !ProjectPvpCharacterAssetPaths.TryGetRotationsFolder(definition, out string rotationsFolderPath))
            {
                return false;
            }

            string portraitPath = rotationsFolderPath + "/" + DefaultPortraitFileName;
            if (!File.Exists(ProjectPvpCharacterAssetPaths.ToFullPath(portraitPath)))
            {
                Debug.LogWarning("ProjectPVP: default portrait file missing at " + portraitPath + ".");
                return false;
            }

            OptimizePortraitImport(portraitPath);
            Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
            if (portrait == null)
            {
                Debug.LogWarning("ProjectPVP: could not load portrait sprite at " + portraitPath + ".");
                return false;
            }

            if (definition.defaultSprite == portrait)
            {
                return true;
            }

            Undo.RecordObject(definition, "Repair Default Portrait Sprite");
            definition.defaultSprite = portrait;
            EditorUtility.SetDirty(definition);
            return true;
        }

        private static bool TryRepairBootstrapProfile(CharacterDefinition definition)
        {
            if (definition == null || !ProjectPvpCharacterAssetPaths.TryGetDataFolder(definition, out string dataFolderPath))
            {
                return false;
            }

            string displayName = !string.IsNullOrWhiteSpace(definition.displayName) ? definition.displayName : definition.name;
            string profileName = displayName.Replace(" ", string.Empty) + "BootstrapProfile.asset";
            string profilePath = dataFolderPath + "/" + profileName;
            CharacterBootstrapProfile profile = AssetDatabase.LoadAssetAtPath<CharacterBootstrapProfile>(profilePath);
            if (profile == null)
            {
                Debug.LogWarning("ProjectPVP: bootstrap profile missing at " + profilePath + ".");
                return false;
            }

            Undo.RecordObject(profile, "Repair Character Definition Reference");
            profile.characterDefinition = definition;
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static void OptimizePortraitImport(string portraitPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(portraitPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (Mathf.Abs(importer.spritePixelsPerUnit - 1f) > 0.001f)
            {
                importer.spritePixelsPerUnit = 1f;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                return;
            }

            AssetDatabase.ImportAsset(portraitPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
