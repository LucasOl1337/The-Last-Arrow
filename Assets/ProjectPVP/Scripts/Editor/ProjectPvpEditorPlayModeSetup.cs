using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ProjectPVP.Editor
{
    [InitializeOnLoad]
    public static class ProjectPvpEditorPlayModeSetup
    {
        private static int _gameViewRefocusFramesLeft;

        static ProjectPvpEditorPlayModeSetup()
        {
            EditorApplication.delayCall += ConfigurePlayModeScene;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update += TickFocusAssist;
        }

        private static void ConfigurePlayModeScene()
        {
            string scenePath = ProjectPvpEditorSceneUtility.ResolvePrimaryPlayableScenePath();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (bootstrapScene == null)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                if (EditorSceneManager.playModeStartScene != null)
                {
                    EditorSceneManager.playModeStartScene = null;
                }

                return;
            }

            if (EditorSceneManager.playModeStartScene != bootstrapScene)
            {
                EditorSceneManager.playModeStartScene = bootstrapScene;
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _gameViewRefocusFramesLeft = 0;
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            _gameViewRefocusFramesLeft = 180;
            FocusGameView();
        }

        private static void TickFocusAssist()
        {
            if (!EditorApplication.isPlaying || _gameViewRefocusFramesLeft <= 0)
            {
                return;
            }

            _gameViewRefocusFramesLeft -= 1;

            if (_gameViewRefocusFramesLeft % 20 == 0)
            {
                FocusGameView();
            }
        }

        private static void FocusGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null)
            {
                return;
            }

            gameView.Show();
            gameView.Focus();
        }
    }
}
