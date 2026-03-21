using System.Collections.Generic;
using System.Linq;
using ProjectPVP.Characters;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using ProjectPVP.Match;
using ProjectPVP.Presentation;
using ProjectPVP.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPVP.Editor
{
    public static class ProjectPvpPlayableValidator
    {
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

            string scenePath = ProjectPvpEditorSceneUtility.ResolvePrimaryPlayableScenePath();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                issues.Add("Nenhuma cena jogavel foi encontrada em Build Settings ou Assets/ProjectPVP/Scenes.");
                return issues;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                issues.Add("Cena jogavel nao pode ser aberta: " + scenePath);
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

            if (matchController.characterCatalog == null)
            {
                issues.Add("MatchController sem CharacterCatalog.");
            }
            else if (matchController.AvailableCharacters.Count == 0)
            {
                issues.Add("CharacterCatalog vazio no MatchController.");
            }

            if (matchController.Slots.Count < 2)
            {
                issues.Add("MatchController precisa de 2 slots configurados no roster.");
            }

            ValidateRoster(matchController, issues);

            if (Object.FindFirstObjectByType<ProjectPvpDebugHud>() == null)
            {
                issues.Add("HUD de debug ausente na cena.");
            }

            if (Object.FindFirstObjectByType<ProjectPvpArenaGizmos>() == null)
            {
                issues.Add("Componente de gizmos da arena ausente na cena.");
            }

            if (!HasArenaGeometry())
            {
                issues.Add("Nenhum collider de arena foi encontrado fora dos combatentes.");
            }

            if (Camera.main == null)
            {
                issues.Add("Main Camera ausente.");
            }

            if (!HasAudioListenerCoverage())
            {
                issues.Add("AudioListener ausente na cena.");
            }

            return issues;
        }

        private static void ValidateRoster(MatchController matchController, List<string> issues)
        {
            var seenSlots = new HashSet<CombatantSlotId>();
            int configuredPlayers = 0;

            for (int index = 0; index < matchController.Slots.Count; index += 1)
            {
                CombatantSlotConfig slot = matchController.Slots[index];
                if (slot == null)
                {
                    issues.Add("Roster possui um slot nulo na posicao " + index + ".");
                    continue;
                }

                if (!seenSlots.Add(slot.slotId))
                {
                    issues.Add("Roster possui slot duplicado para " + slot.slotId.ToDisplayName() + ".");
                }

                ValidatePlayer(matchController, slot, issues);
                if (slot.controller != null || slot.characterProfile != null)
                {
                    configuredPlayers += 1;
                }
            }

            if (configuredPlayers < 2)
            {
                issues.Add("A cena precisa de 2 combatentes configurados para o slice 1v1.");
            }
        }

        private static void ValidatePlayer(MatchController matchController, CombatantSlotConfig slot, List<string> issues)
        {
            string label = slot.ResolveDisplayName();
            PlayerController player = slot.controller;
            CharacterBootstrapProfile characterProfile = slot.ResolveCharacterProfile();
            if (player == null && characterProfile == null)
            {
                issues.Add(label + " sem PlayerController nem CharacterBootstrapProfile.");
                return;
            }

            if (player != null && player.SlotId != slot.slotId)
            {
                issues.Add(label + " com slotId divergente do roster.");
            }

            if (characterProfile != null
                && matchController.characterCatalog != null
                && !matchController.AvailableCharacters.Contains(characterProfile))
            {
                issues.Add(label + " referencia um CharacterBootstrapProfile fora do CharacterCatalog.");
            }

            CharacterDefinition assignedCharacter = slot.ResolveCharacterDefinition();
            if (assignedCharacter == null)
            {
                issues.Add(label + " sem CharacterDefinition selecionado.");
            }

            if (slot.playerProfile == null)
            {
                issues.Add(label + " sem CombatantSlotProfile explicito; usando fallback legado.");
            }

            ProjectileController projectilePrefab = player != null
                ? player.projectilePrefab
                : characterProfile != null ? characterProfile.projectilePrefab : null;
            if (projectilePrefab == null)
            {
                issues.Add(label + " sem prefab de projectile no personagem.");
            }

            if (player != null && player.InputSource == null)
            {
                issues.Add(label + " sem ICombatantInputSource configurado.");
            }

            if (player != null && player.anchorRig == null)
            {
                issues.Add(label + " sem CombatantAnchorRig.");
            }
        }

        private static bool HasArenaGeometry()
        {
            Collider2D[] colliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            return colliders.Any(collider =>
                collider != null
                && collider.enabled
                && collider.GetComponentInParent<PlayerController>() == null
                && collider.GetComponentInParent<ProjectileController>() == null);
        }

        private static bool HasAudioListenerCoverage()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null)
            {
                return true;
            }

            return Camera.main != null
                && Object.FindFirstObjectByType<ProjectPvpRuntimeBootstrap>() != null;
        }
    }
}
