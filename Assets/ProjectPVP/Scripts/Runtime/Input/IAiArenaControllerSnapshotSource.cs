using UnityEngine;

namespace ProjectPVP.Input
{
    public interface IAiArenaControllerSnapshotSource
    {
        AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition);
    }
}
