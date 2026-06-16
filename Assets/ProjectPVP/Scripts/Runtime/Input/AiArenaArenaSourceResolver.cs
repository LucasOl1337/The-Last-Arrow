using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaArenaSourceResolver
    {
        internal static AiArenaArenaSnapshot ResolveArenaSnapshot()
        {
            if (AiArenaSnapshotSourceRegistry.TryGetArenaSource(out MonoBehaviour registeredArenaSource)
                && registeredArenaSource is IAiArenaArenaSnapshotSource typedArenaSource)
            {
                return typedArenaSource.BuildAiArenaArenaSnapshot();
            }

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            return ResolveSceneArenaSnapshot(behaviours);
        }

        internal static AiArenaArenaSnapshot ResolveSceneArenaSnapshot(IReadOnlyList<MonoBehaviour> behaviours)
        {
            MonoBehaviour fallbackController = null;
            if (behaviours != null)
            {
                for (int index = 0; index < behaviours.Count; index += 1)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null)
                    {
                        continue;
                    }

                    if (behaviour is IAiArenaArenaSnapshotSource arenaSource)
                    {
                        return arenaSource.BuildAiArenaArenaSnapshot();
                    }

                    if (fallbackController == null && behaviour.GetType().Name == "MatchController")
                    {
                        fallbackController = behaviour;
                    }
                }
            }

            if (fallbackController != null)
            {
                return AiArenaArenaSnapshotFallbackService.BuildFromController(fallbackController);
            }

            return AiArenaArenaSnapshotFallbackService.BuildDefault();
        }
    }
}
