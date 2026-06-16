using System.Collections.Generic;
using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaOpponentSnapshotSelector
    {
        internal static AiArenaControllerSnapshot SelectClosest(
            IReadOnlyList<MonoBehaviour> controllerSources,
            AiArenaControllerSnapshot self)
        {
            AiArenaControllerSnapshot resolved = default;
            float bestDistance = float.MaxValue;

            if (controllerSources == null)
            {
                return resolved;
            }

            for (int index = 0; index < controllerSources.Count; index += 1)
            {
                AiArenaControllerSnapshot candidate = AiArenaControllerSnapshotBuilder.Build(
                    controllerSources[index],
                    self.slotId,
                    self.position);
                if (!candidate.isValid || candidate.slotId == self.slotId || candidate.isDead)
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(candidate.position - self.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                resolved = candidate;
            }

            return resolved;
        }
    }
}
