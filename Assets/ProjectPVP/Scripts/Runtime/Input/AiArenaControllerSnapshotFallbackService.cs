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
            bool isDead = AiArenaReflectionReader.ReadBoolProperty(controller, "IsDead", false);

            return new AiArenaControllerSnapshot
            {
                isValid = true,
                slotId = resolvedSlotId,
                botId = AiArenaReflectionReader.ReadStringProperty(controller, "BotId", string.Empty),
                botDisplayName = AiArenaReflectionReader.ReadStringProperty(controller, "BotDisplayName", controller.name),
                characterId = AiArenaReflectionReader.ReadStringField(controller, "characterDefinition", string.Empty),
                displayName = controller.name,
                actionKey = AiArenaReflectionReader.ReadStringProperty(controller, "CurrentVisualActionKey", string.Empty),
                isDead = isDead,
                isGrounded = AiArenaReflectionReader.ReadBoolProperty(controller, "IsGrounded", true),
                isTouchingWall = AiArenaReflectionReader.ReadBoolProperty(controller, "IsTouchingWall", false),
                isDashing = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "IsDashing", false),
                isMeleeActive = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "IsMeleeActive", false),
                isShootAnimating = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "IsShootAnimating", false),
                isUltimateActive = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "IsUltimateActive", false),
                isHitStunned = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "IsHitStunned", false),
                canParryProjectile = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "CanParryProjectile", false),
                canBlockProjectiles = !isDead && AiArenaReflectionReader.ReadBoolProperty(controller, "CanBlockProjectileWithUltimate", false),
                arrows = AiArenaReflectionReader.ReadIntProperty(controller, "CurrentArrows", 0),
                facing = AiArenaReflectionReader.ReadIntProperty(controller, "Facing", position.x >= fallbackPosition.x ? 1 : -1),
                projectileInheritVelocityFactor = AiArenaReflectionReader.ReadFloatProperty(controller, "ProjectileInheritVelocityFactor", 1f),
                shootCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "ShootCooldownLeft", 0f),
                meleeCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "MeleeCooldownLeft", 0f),
                dashCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "DashCooldownLeft", 0f),
                ultimateCooldownLeft = AiArenaReflectionReader.ReadFloatProperty(controller, "UltimateCooldownLeft", 0f),
                hitStunTimeLeft = isDead ? 0f : AiArenaReflectionReader.ReadFloatProperty(controller, "HitStunTimeLeft", 0f),
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
