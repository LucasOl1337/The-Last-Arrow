using System.Collections.Generic;
using System.Linq;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using ProjectPVP.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPVP.Editor
{
    public static class ProjectPvpPlayableValidator
    {
        private const string ScenePath = "Assets/ProjectPVP/Scenes/Bootstrap.unity";
        private const string ArenaAssetPath = "Assets/ProjectPVP/Environment/Arenas/DefaultArenaDefinition.asset";
        private const string PlayerOneAssetPath = "Assets/ProjectPVP/Characters/Mizu/Data/MizuDefinition.asset";
        private const string PlayerTwoAssetPath = "Assets/ProjectPVP/Characters/StormDragon/Data/StormDragonDefinition.asset";
        private const string ProjectilePrefabPath = "Assets/ProjectPVP/Characters/Shared/Projectiles/Prefabs/SimpleProjectile.prefab";

        [MenuItem("ProjectPVP/Validate Project Setup")]
        public static void ValidatePlayableSlice()
        {
            List<string> issues = CollectIssues();

            if (issues.Count == 0)
            {
                Debug.Log("ProjectPVP: playable slice validado com sucesso.");
                return;
            }

            throw new System.InvalidOperationException("ProjectPVP: validacao falhou.\n- " + string.Join("\n- ", issues));
        }

        private static List<string> CollectIssues()
        {
            var issues = new List<string>();

            if (AssetDatabase.LoadAssetAtPath<Object>(ArenaAssetPath) == null)
            {
                issues.Add("ArenaDefinition gerada ausente.");
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(PlayerOneAssetPath) == null)
            {
                issues.Add("CharacterDefinition do Mizu ausente.");
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(PlayerTwoAssetPath) == null)
            {
                issues.Add("CharacterDefinition do Storm Dragon ausente.");
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(ProjectilePrefabPath) == null)
            {
                issues.Add("Prefab de projectile ausente.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                issues.Add("Cena Bootstrap nao pode ser aberta.");
                return issues;
            }

            MatchController matchController = Object.FindFirstObjectByType<MatchController>();
            if (matchController == null)
            {
                issues.Add("MatchController ausente na cena.");
                return issues;
            }

            if (matchController.arenaDefinition == null)
            {
                issues.Add("MatchController sem arenaDefinition.");
            }

            ValidatePlayer(matchController.playerOne, "PlayerOne", issues);
            ValidatePlayer(matchController.playerTwo, "PlayerTwo", issues);

            if (Object.FindFirstObjectByType<ProjectPvpDebugHud>() == null)
            {
                issues.Add("HUD de debug ausente na cena.");
            }

            if (Object.FindFirstObjectByType<ProjectPvpArenaGizmos>() == null)
            {
                issues.Add("Componente de gizmos da arena ausente na cena.");
            }

            if (!HasWorldGeometry())
            {
                issues.Add("Nenhum collider de arena foi gerado em Environment/WorldGeometry.");
            }

            if (Camera.main == null)
            {
                issues.Add("Main Camera ausente.");
            }

            if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                issues.Add("AudioListener ausente na cena.");
            }

            return issues;
        }

        private static void ValidatePlayer(PlayerController player, string label, List<string> issues)
        {
            if (player == null)
            {
                issues.Add(label + " ausente.");
                return;
            }

            if (player.characterDefinition == null)
            {
                issues.Add(label + " sem CharacterDefinition.");
            }

            if (player.projectilePrefab == null)
            {
                issues.Add(label + " sem prefab de projectile.");
            }

            if (player.inputSource == null)
            {
                issues.Add(label + " sem KeyboardPlayerInputSource.");
            }
        }

        private static bool HasWorldGeometry()
        {
            GameObject[] roots = sceneRoots();
            foreach (GameObject root in roots)
            {
                Transform worldGeometry = root.transform.Find("Environment/WorldGeometry");
                if (worldGeometry == null)
                {
                    continue;
                }

                return worldGeometry.GetComponentsInChildren<Collider2D>(true).Any();
            }

            return false;
        }

        private static GameObject[] sceneRoots()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.GetRootGameObjects() : new GameObject[0];
        }
    }
}
