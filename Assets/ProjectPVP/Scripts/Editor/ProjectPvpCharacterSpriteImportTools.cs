using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpCharacterSpriteImportTools
    {
        [MenuItem("ProjectPVP/Characters/Optimize Character Sprite Imports")]
        private static void OptimizeCharacterSpriteImports()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ProjectPVP/Characters" });
            int updatedCount = 0;

            for (int index = 0; index < guids.Length; index += 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                bool changed = false;

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

            Debug.Log("ProjectPVP: imports de sprite otimizados. Assets atualizados: " + updatedCount + ".");
        }
    }
}
