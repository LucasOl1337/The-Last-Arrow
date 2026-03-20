using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    /// <summary>
    /// Esse script roda invisivel e automaticamente no Unity.
    /// QUALQUER imagem arrastada para as pastas de background se torna Sprite imediatamente.
    /// </summary>
    public class BackgroundAutoImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            // Verifica se a imagem está caindo nas pastas de mapas ou background
            if (assetPath.Contains("Environment/Backgrounds") || assetPath.Contains("Maps"))
            {
                TextureImporter importer = (TextureImporter)assetImporter;

                // Se Unity tentar importar como "Default", nós rasgamos a regra e forçamos para Sprite
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    
                    // Configurações exatas para a arte HD-2D e Pixel Art não borrarem
                    importer.filterMode = FilterMode.Point; 
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    
                    Debug.Log($"<color=yellow>The Last Arrow:</color> Interceptamos '{assetPath}' e forçamos a virar Sprite automaticamente!");
                }
            }
        }
    }
}
