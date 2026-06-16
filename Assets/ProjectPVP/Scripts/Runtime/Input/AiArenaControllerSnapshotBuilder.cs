using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaControllerSnapshotBuilder
    {
        internal static AiArenaControllerSnapshot Build(
            MonoBehaviour controller,
            int fallbackSlotId,
            Vector2 fallbackPosition)
        {
            if (controller == null)
            {
                return default;
            }

            if (controller is IAiArenaControllerSnapshotSource snapshotSource)
            {
                return snapshotSource.BuildAiArenaControllerSnapshot(fallbackSlotId, fallbackPosition);
            }

            return AiArenaControllerSnapshotFallbackService.BuildFromController(
                controller,
                fallbackSlotId,
                fallbackPosition);
        }
    }
}
