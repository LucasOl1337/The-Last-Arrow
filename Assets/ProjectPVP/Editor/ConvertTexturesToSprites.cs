#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ProjectPVP.Editor
{
    public class ConvertTexturesToSprites
    {
        [MenuItem("Assets/Converter para Sprite (Pixel Art)", false, 2000)]
        private static void ConvertSelected()
        {
            // Pega todos os arquivos de imagem dentro da pasta/seleção atual (DeepAssets entra em subpastas)
            Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);
            int count = 0;

            foreach (Object obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool changed = false;
                    
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
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

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                Debug.Log($"<color=cyan>The Last Arrow:</color> {count} imagens convertidas para Sprite com sucesso! Elas agora aparecem no seletor padrão do Unity.");
            }
            else
            {
                Debug.Log("Nenhuma imagem precisava ser convertida (todas já eram Sprites configurados).");
            }
        }

        // Validação para o botão só aparecer quando clicar com o botão direito numa pasta ou imagem
        [MenuItem("Assets/Converter para Sprite (Pixel Art)", true)]
        private static bool ConvertSelectedValidation()
        {
            return Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets).Length > 0;
        }
    }
}
#endif
