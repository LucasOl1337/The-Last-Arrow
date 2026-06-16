using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaProjectileSnapshotResolver
    {
        internal static List<AiArenaProjectileSnapshot> Resolve(
            IReadOnlyList<MonoBehaviour> projectileSources,
            AiArenaControllerSnapshot self)
        {
            var projectiles = new List<AiArenaProjectileSnapshot>(8);
            if (projectileSources == null)
            {
                return projectiles;
            }

            for (int index = 0; index < projectileSources.Count; index += 1)
            {
                AiArenaProjectileSnapshot projectile = AiArenaProjectileSnapshotBuilder.Build(projectileSources[index]);
                if (!projectile.isValid || projectile.sourceSlotId == self.slotId)
                {
                    continue;
                }

                projectiles.Add(projectile);
            }

            return projectiles;
        }
    }
}
