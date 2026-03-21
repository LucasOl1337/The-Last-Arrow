using System;
using System.IO;
using ProjectPVP.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPVP.Editor
{
    internal static class ProjectPvpEditorSceneUtility
    {
        private const string FallbackScenesRoot = "Assets/ProjectPVP/Scenes";
        private const string BootstrapSceneName = "Bootstrap";

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
                if (!string.IsNullOrWhiteSpace(scenePath) && string.Equals(Path.GetFileNameWithoutExtension(scenePath), BootstrapSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return scenePath;
                }
            }

            return sceneGuids.Length > 0
                ? AssetDatabase.GUIDToAssetPath(sceneGuids[0])
                : string.Empty;
        }

        [MenuItem("ProjectPVP/Open Bootstrap Scene")]
        internal static void OpenPrimaryPlayableScene()
        {
            OpenPrimaryPlayableScene(selectMatchController: true);
        }

        [MenuItem("ProjectPVP/Select MatchController")]
        internal static void SelectMatchControllerInScene()
        {
            if (TrySelectMatchController())
            {
                return;
            }

            OpenPrimaryPlayableScene(selectMatchController: true);
        }

        internal static bool EnsurePrimaryPlayableSceneOpen(bool selectMatchController)
        {
            string scenePath = ResolvePrimaryPlayableScenePath();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                else
                {
                    return false;
                }
            }

            if (selectMatchController)
            {
                TrySelectMatchController();
            }

            return true;
        }

        internal static bool ShouldOpenPrimaryPlayableScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(activeScene.path))
            {
                return false;
            }

            int rootCount = activeScene.rootCount;
            if (rootCount == 0)
            {
                return true;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
            {
                return true;
            }

            if (roots.Length == 2
                && HasRootNamed(roots, "Main Camera")
                && HasRootNamed(roots, "Directional Light"))
            {
                return true;
            }

            return false;
        }

        private static void OpenPrimaryPlayableScene(bool selectMatchController)
        {
            if (!EnsurePrimaryPlayableSceneOpen(selectMatchController))
            {
                Debug.LogWarning("ProjectPVP: nao foi possivel abrir a cena Bootstrap automaticamente.");
            }
        }

        private static bool TrySelectMatchController()
        {
            MatchController matchController = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            if (matchController == null)
            {
                return false;
            }

            Selection.activeGameObject = matchController.gameObject;
            EditorGUIUtility.PingObject(matchController.gameObject);
            return true;
        }

        private static bool HasRootNamed(GameObject[] roots, string expectedName)
        {
            for (int index = 0; index < roots.Length; index += 1)
            {
                if (roots[index] != null && string.Equals(roots[index].name, expectedName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
