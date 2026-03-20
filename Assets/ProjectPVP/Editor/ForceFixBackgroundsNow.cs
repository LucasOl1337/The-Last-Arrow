using UnityEditor;
using UnityEngine;

namespace ProjectPVP.Editor
{
    [InitializeOnLoad]
    public class ForceFixBackgroundsNow
    {
        static ForceFixBackgroundsNow()
        {
            // Roda IMEDIATAMENTE e UMA VEZ na proxima compilacao
            string folderPath = "Assets/ProjectPVP/Environment/Backgrounds/Maps";
            if (!AssetDatabase.IsValidFolder(folderPath)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folderPath });
            int converted = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
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
                        converted++;
                    }
                }
            }

            if (converted > 0)
            {
                Debug.Log($"<color=green>🔥 RESOLVIDO:</color> {converted} backgrounds convertidos FORÇADAMENTE para Sprite agora mesmo!");
            }
        }
    }
}
