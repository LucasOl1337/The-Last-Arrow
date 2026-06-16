using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaProjectileSnapshotFallbackService
    {
        internal static AiArenaProjectileSnapshot BuildFromProjectile(MonoBehaviour projectile)
        {
            if (projectile == null)
            {
                return default;
            }

            GameObject sourceObject = AiArenaReflectionReader.ReadGameObjectProperty(projectile, "SourceObject", null);
            int sourceSlotId = ResolveSourceSlotId(sourceObject);
            Vector2 position = projectile.transform.position;
            Vector2 velocity = AiArenaReflectionReader.ReadVector2Property(projectile, "CurrentVelocity", Vector2.zero);
            Vector2 fallbackTravelDirection = velocity.sqrMagnitude > 0.001f
                ? velocity.normalized
                : Vector2.right;

            return new AiArenaProjectileSnapshot
            {
                isValid = true,
                sourceSlotId = sourceSlotId,
                isStuck = AiArenaReflectionReader.ReadBoolProperty(projectile, "IsStuck", false),
                isDisarmed = AiArenaReflectionReader.ReadBoolProperty(projectile, "IsDisarmed", false),
                position = position,
                velocity = velocity,
                travelDirection = AiArenaReflectionReader.ReadVector2Property(projectile, "TravelDirection", fallbackTravelDirection),
            };
        }

        private static int ResolveSourceSlotId(GameObject sourceObject)
        {
            if (sourceObject == null)
            {
                return 0;
            }

            MonoBehaviour[] sourceBehaviours = sourceObject.GetComponents<MonoBehaviour>();
            for (int index = 0; index < sourceBehaviours.Length; index += 1)
            {
                MonoBehaviour behaviour = sourceBehaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "PlayerController")
                {
                    continue;
                }

                return AiArenaReflectionReader.ReadIntField(behaviour, "slotId", 0);
            }

            return 0;
        }
    }
}
