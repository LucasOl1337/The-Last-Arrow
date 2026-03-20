using System;
using System.IO;
using UnityEditor;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpEditorSceneUtility
    {
        private const string FallbackScenesRoot = "Assets/ProjectPVP/Scenes";

        internal static string ResolvePrimaryPlayableScenePath()
        {
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int index = 0; index < buildScenes.Length; index += 1)
            {
                EditorBuildSettingsScene buildScene = buildScenes[index];
                if (buildScene != null && buildScene.enabled && !string.IsNullOrWhiteSpace(buildScene.path))
                {
                    return buildScene.path;
                }
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { FallbackScenesRoot });
            Array.Sort(sceneGuids, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < sceneGuids.Length; index += 1)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                if (!string.IsNullOrWhiteSpace(scenePath) && string.Equals(Path.GetFileNameWithoutExtension(scenePath), "Bootstrap", StringComparison.OrdinalIgnoreCase))
                {
                    return scenePath;
                }
            }

            return sceneGuids.Length > 0
                ? AssetDatabase.GUIDToAssetPath(sceneGuids[0])
                : string.Empty;
        }
    }
}
