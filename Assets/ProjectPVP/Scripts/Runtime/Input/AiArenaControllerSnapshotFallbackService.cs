using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaControllerSnapshotFallbackService
    {
        internal static AiArenaControllerSnapshot BuildFromController(
            MonoBehaviour controller,
            int fallbackSlotId,
            Vector2 fallbackPosition)
        {
            if (controller == null)
            {
                return default;
            }

            Vector2 position = controller.transform.position;
            int resolvedSlotId = AiArenaReflectionReader.ReadIntField(controller, "slotId", fallbackSlotId);
            float horizontalVelocity = AiArenaReflectionReader.ReadFloatProperty(controller, "HorizontalVelocity", 0f);
            float verticalVelocity = AiArenaReflectionReader.ReadFloatProperty(controller, "VerticalVelocity", 0f);

            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = resolvedSlotId,
                botId = AiArenaReflectionReader.ReadStringProperty(controller, "BotId", string.Empty),
                botDisplayName = AiArenaReflectionReader.ReadStringProperty(controller, "BotDisplayName", controller.name),
                characterId = AiArenaReflectionReader.ReadStringField(controller, "characterDefinition", string.Empty),
                displayName = controller.name,
                actionKey = AiArenaReflectionReader.ReadStringProperty(controller, "CurrentVisualActionKey", string.Empty),
                isDead = AiArenaReflectionReader.ReadBoolProperty(controller, "IsDead", false),
                isGrounded = AiArenaReflectionReader.ReadBoolProperty(controller, "IsGrounded", true),
                isTouchingWall = AiArenaReflectionReader.ReadBoolProperty(controller, "IsTouchingWall", false),
                isDashing = AiArenaReflectionReader.ReadBoolProperty(controller, "IsDashing", false),
                isMeleeActive = AiArenaReflectionReader.ReadBoolProperty(controller, "IsMeleeActive", false),
                isShootAnimating = AiArenaReflectionReader.ReadBoolProperty(controller, "IsShootAnimating", false),
                isUltimateActive = AiArenaReflectionReader.ReadBoolProperty(controller, "IsUltimateActive", false),
                isHitStunned = AiArenaReflectionReader.ReadBoolProperty(controller, "IsHitStunned", false),
                canParryProjectile = AiArenaReflectionReader.ReadBoolProperty(controller, "CanParryProjectile", false),
                canBlockProjectiles = AiArenaReflectionReader.ReadBoolProperty(controller, "CanBlockProjectileWithUltimate", false),
                arrows = AiArenaReflectionReader.ReadIntProperty(controller, "CurrentArrows", 0),
                facing = AiArenaReflectionReader.ReadIntProperty(controller, "Facing", position.x >= fallbackPosition.x ? 1 : -1),
                shootCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "ShootCooldownLeft", 0f),
                meleeCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "MeleeCooldownLeft", 0f),
                dashCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "DashCooldownLeft", 0f),
                ultimateCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "UltimateCooldownLeft", 0f),
                hitStunTimeLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "HitStunTimeLeft", 0f),
                position = AiArenaReflectionReader.ReadVector2Property(controller, "RootPosition", position),
                velocity = new Vector2(horizontalVelocity, verticalVelocity),
                meleeHitboxCenter = AiArenaReflectionReader.ReadVector2Property(controller, "MeleeHitboxCenter", position),
                meleeHitboxSize = AiArenaReflectionReader.ReadVector2Property(controller, "MeleeHitboxSize", Vector2.zero),
                ultimateHitboxCenter = AiArenaReflectionReader.ReadVector2Property(controller, "UltimateHitboxCenter", position),
                ultimateHitboxRadius = AiArenaReflectionReader.ReadFloatProperty(controller, "UltimateHitboxRadius", 0f),
            };
        }
    }
}
