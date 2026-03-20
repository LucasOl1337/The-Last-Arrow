using System;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Characters
{
    public enum CharacterBootstrapColliderShape
    {
        None = 0,
        Box = 1,
        Circle = 2,
        Capsule = 3,
    }

    [Serializable]
    public sealed class CharacterBootstrapColliderConfig
    {
        public CharacterBootstrapColliderShape shapeKind = CharacterBootstrapColliderShape.Box;
        public Vector2 offset = Vector2.zero;
        public Vector2 size = new Vector2(96f, 72f);
        [Min(0f)] public float radius = 96f;
        public CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Horizontal;
    }

    [Serializable]
    public sealed class CharacterBootstrapAnchorConfig
    {
        public string childName = "Anchor";
        public PlayerCombatAnchorKind anchorKind = PlayerCombatAnchorKind.MeleeHitbox;
        public bool mirrorX = true;
        public Vector2 localPosition = Vector2.zero;
        public Vector3 localEulerAngles = Vector3.zero;
        public CharacterBootstrapColliderConfig collider = new CharacterBootstrapColliderConfig();

        public static CharacterBootstrapAnchorConfig CreateSpawnAnchor()
        {
            return new CharacterBootstrapAnchorConfig
            {
                childName = "SpawnAnchor",
                anchorKind = PlayerCombatAnchorKind.Spawn,
                mirrorX = false,
                collider = new CharacterBootstrapColliderConfig
                {
                    shapeKind = CharacterBootstrapColliderShape.None,
                },
            };
        }

        public static CharacterBootstrapAnchorConfig CreateMeleeAnchor()
        {
            return new CharacterBootstrapAnchorConfig
            {
                childName = "MeleeHitbox",
                anchorKind = PlayerCombatAnchorKind.MeleeHitbox,
                mirrorX = true,
            };
        }

        public static CharacterBootstrapAnchorConfig CreateUltimateAnchor()
        {
            return new CharacterBootstrapAnchorConfig
            {
                childName = "UltimateHitbox",
                anchorKind = PlayerCombatAnchorKind.UltimateHitbox,
                mirrorX = true,
            };
        }
    }

    [CreateAssetMenu(fileName = "CharacterBootstrapProfile", menuName = "ProjectPVP/Characters/Character Bootstrap Profile")]
    public sealed class CharacterBootstrapProfile : ScriptableObject
    {
        [Header("Identity")]
        public string id = string.Empty;
        public string displayName = string.Empty;

        [Header("Runtime")]
        public CharacterDefinition characterDefinition;
        public ProjectileController projectilePrefab;

        [Header("Scene Graph")]
        public CharacterBootstrapAnchorConfig spawnAnchor = CharacterBootstrapAnchorConfig.CreateSpawnAnchor();
        public CharacterBootstrapAnchorConfig meleeHitbox = CharacterBootstrapAnchorConfig.CreateMeleeAnchor();
        public CharacterBootstrapAnchorConfig ultimateHitbox = CharacterBootstrapAnchorConfig.CreateUltimateAnchor();

        public CharacterDefinition ResolveCharacterDefinition()
        {
            return characterDefinition;
        }

        public string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            if (characterDefinition != null && !string.IsNullOrWhiteSpace(characterDefinition.displayName))
            {
                return characterDefinition.displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                return id.Trim();
            }

            return "Character";
        }

        public void ApplyToController(PlayerController controller)
        {
            if (controller == null)
            {
                return;
            }

            CharacterDefinition resolvedDefinition = ResolveCharacterDefinition();
            if (resolvedDefinition != null)
            {
                controller.AssignCharacterDefinition(resolvedDefinition);
            }

            if (projectilePrefab != null)
            {
                controller.projectilePrefab = projectilePrefab;
            }
        }
    }
}
