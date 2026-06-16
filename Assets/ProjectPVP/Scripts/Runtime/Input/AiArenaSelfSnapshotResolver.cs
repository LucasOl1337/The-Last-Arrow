using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaSelfSnapshotResolver
    {
        internal static AiArenaControllerSnapshot Resolve(
            IReadOnlyList<MonoBehaviour> controllerSources,
            GameObject owner,
            int fallbackSlotId)
        {
            MonoBehaviour selfController = FindByOwner(controllerSources, owner);
            Vector2 fallbackPosition = owner != null ? (Vector2)owner.transform.position : Vector2.zero;
            return AiArenaControllerSnapshotBuilder.Build(selfController, fallbackSlotId, fallbackPosition);
        }

        private static MonoBehaviour FindByOwner(IReadOnlyList<MonoBehaviour> controllerSources, GameObject owner)
        {
            if (controllerSources == null || owner == null)
            {
                return null;
            }

            for (int index = 0; index < controllerSources.Count; index += 1)
            {
                MonoBehaviour source = controllerSources[index];
                if (source == null || source.gameObject != owner)
                {
                    continue;
                }

                return source;
            }

            return null;
        }
    }
}
