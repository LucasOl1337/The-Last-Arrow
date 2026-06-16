using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaProjectileSourceResolver
    {
        internal static bool CollectProjectileSources(List<MonoBehaviour> destination)
        {
            if (destination == null)
            {
                return false;
            }

            if (AiArenaSnapshotSourceRegistry.TryGetProjectileSources(destination))
            {
                return true;
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            CollectSceneProjectileSources(behaviours, destination);
            return destination.Count > 0;
        }

        internal static void CollectSceneProjectileSources(
            IReadOnlyList<MonoBehaviour> behaviours,
            List<MonoBehaviour> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (behaviours == null)
            {
                return;
            }

            for (int index = 0; index < behaviours.Count; index += 1)
            {
                MonoBehaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour.GetType().Name != "ProjectileController")
                {
                    continue;
                }

                destination.Add(behaviour);
            }
        }
    }
}
