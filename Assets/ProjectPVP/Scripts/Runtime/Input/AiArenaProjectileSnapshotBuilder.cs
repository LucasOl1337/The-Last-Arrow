using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaProjectileSnapshotBuilder
    {
        internal static AiArenaProjectileSnapshot Build(MonoBehaviour projectile)
        {
            if (projectile == null)
            {
                return default;
            }

            if (projectile is IAiArenaProjectileSnapshotSource snapshotSource)
            {
                return snapshotSource.BuildAiArenaProjectileSnapshot();
            }

            return AiArenaProjectileSnapshotFallbackService.BuildFromProjectile(projectile);
        }
    }
}
