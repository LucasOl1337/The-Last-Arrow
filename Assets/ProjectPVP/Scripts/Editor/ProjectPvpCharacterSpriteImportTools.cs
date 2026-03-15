using System.Collections.Generic;
using System.IO;
using ProjectPVP.Data;
using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterSpriteImportTools
    {
        private static readonly string[] CharacterSearchRoots =
        {
            "Assets/ProjectPVP/Characters",
        };

        [MenuItem("ProjectPVP/Characters/Optimize Character Sprite Imports")]
        private static void OptimizeCharacterSpriteImports()
        {
            int updatedCount = OptimizeSpriteImportsInFolders(CharacterSearchRoots);
            Debug.Log("ProjectPVP: imports de sprite otimizados. Assets atualizados: " + updatedCount + ".");
        }

        [MenuItem("ProjectPVP/Characters/Bake Selected Character Sprites To Native Scale", true)]
        private static bool ValidateBakeSelectedCharacterSpritesToNativeScale()
        {
            return Selection.activeObject is CharacterDefinition;
        }

        [MenuItem("ProjectPVP/Characters/Bake Selected Character Sprites To Native Scale")]
        private static void BakeSelectedCharacterSpritesToNativeScale()
        {
            if (Selection.activeObject is not CharacterDefinition definition)
            {
                Debug.LogWarning("ProjectPVP: selecione um CharacterDefinition para bakear os sprites.");
                return;
            }

            if (BakeCharacterSpritesToNativeScale(definition, out string summary))
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary);
            }
        }

        internal static bool BakeCharacterSpritesToNativeScale(CharacterDefinition definition, out string summary)
        {
            summary = "ProjectPVP: nao foi possivel bakear os sprites do personagem.";
            if (definition == null)
            {
                summary = "ProjectPVP: CharacterDefinition nulo.";
                return false;
            }

            int upscaleFactor = Mathf.Max(1, definition.nativeSpriteBakeScale);
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

            int bakedCount = 0;
            if (upscaleFactor > 1)
            {
                string[] pngFiles = Directory.GetFiles(characterRootFullPath, "*.png", SearchOption.AllDirectories);
                for (int index = 0; index < pngFiles.Length; index += 1)
                {
                    if (TryBakePngToNativeScale(pngFiles[index], upscaleFactor))
                    {
                        bakedCount += 1;
                    }
                }
            }

            Undo.RecordObject(definition, "Bake Character Sprites To Native Scale");
            definition.spriteScale = Vector2.one;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int optimizedCount = OptimizeSpriteImportsInFolders(characterRoot);

            summary = "ProjectPVP: bake nativo concluido para "
                + definition.displayName
                + ". Fator: x"
                + upscaleFactor
                + ". PNGs processados: "
                + bakedCount
                + ". Imports otimizados: "
                + optimizedCount
                + ". spriteScale definido para 1,1.";
            return true;
        }

        internal static int OptimizeSpriteImportsInFolders(params string[] searchRoots)
        {
            if (searchRoots == null || searchRoots.Length == 0)
            {
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchRoots);
            return OptimizeSpriteImports(guids);
        }

        internal static int BakePngFilesToNativeScale(IEnumerable<string> filePaths, int upscaleFactor)
        {
            if (filePaths == null || upscaleFactor <= 1)
            {
                return 0;
            }

            var uniquePaths = new HashSet<string>();
            int bakedCount = 0;

            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !uniquePaths.Add(filePath))
                {
                    continue;
                }

                if (TryBakePngToNativeScale(filePath, upscaleFactor))
                {
                    bakedCount += 1;
                }
            }

            return bakedCount;
        }

        private static int OptimizeSpriteImports(IEnumerable<string> guids)
        {
            int updatedCount = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
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

                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }

                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }

                if (importer.npotScale != TextureImporterNPOTScale.None)
                {
                    importer.npotScale = TextureImporterNPOTScale.None;
                    changed = true;
                }

                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                importer.SaveAndReimport();
                updatedCount += 1;
            }

            return updatedCount;
        }

        private static bool TryBakePngToNativeScale(string filePath, int upscaleFactor)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || upscaleFactor <= 1)
            {
                return false;
            }

            byte[] pngBytes = File.ReadAllBytes(filePath);
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!sourceTexture.LoadImage(pngBytes))
                {
                    return false;
                }

                int targetWidth = sourceTexture.width * upscaleFactor;
                int targetHeight = sourceTexture.height * upscaleFactor;
                var bakedTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

                try
                {
                    bakedTexture.filterMode = FilterMode.Point;
                    Color32[] sourcePixels = sourceTexture.GetPixels32();
                    var bakedPixels = new Color32[targetWidth * targetHeight];

                    for (int y = 0; y < sourceTexture.height; y += 1)
                    {
                        for (int x = 0; x < sourceTexture.width; x += 1)
                        {
                            Color32 color = sourcePixels[(y * sourceTexture.width) + x];
                            int bakedStartX = x * upscaleFactor;
                            int bakedStartY = y * upscaleFactor;

                            for (int offsetY = 0; offsetY < upscaleFactor; offsetY += 1)
                            {
                                int rowStart = (bakedStartY + offsetY) * targetWidth;
                                for (int offsetX = 0; offsetX < upscaleFactor; offsetX += 1)
                                {
                                    bakedPixels[rowStart + bakedStartX + offsetX] = color;
                                }
                            }
                        }
                    }

                    bakedTexture.SetPixels32(bakedPixels);
                    bakedTexture.Apply(false, false);
                    File.WriteAllBytes(filePath, bakedTexture.EncodeToPNG());
                    return true;
                }
                finally
                {
                    Object.DestroyImmediate(bakedTexture);
                }
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
            }
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
    }
}
