using UnityEngine;

namespace ProjectPVP.Input
{
    internal static class AiArenaArenaSnapshotFallbackService
    {
        private static readonly Rect DefaultWrapBounds = new Rect(-1280f, -720f, 2560f, 1440f);

        internal static AiArenaArenaSnapshot BuildDefault()
        {
            return new AiArenaArenaSnapshot
            {
                wrapBounds = DefaultWrapBounds,
                roundResetPending = false,
                roundsToChampion = 1,
                playerOneWins = 0,
                playerTwoWins = 0,
                currentRespawnSeedIndex = 0,
                currentRespawnSeedLabel = "Fallback",
                pendingRoundWinnerSlot = 0,
                pendingChampionSlot = 0,
                championAnnouncementSlot = 0,
            };
        }

        internal static AiArenaArenaSnapshot BuildFromController(MonoBehaviour controller)
        {
            if (controller == null)
            {
                return default;
            }

            return new AiArenaArenaSnapshot
            {
                wrapBounds = AiArenaReflectionReader.ReadRectProperty(controller, "ActiveWrapBounds", DefaultWrapBounds),
                roundResetPending = AiArenaReflectionReader.ReadBoolProperty(controller, "IsRoundResetPending", false),
                roundsToChampion = AiArenaReflectionReader.ReadIntProperty(controller, "RoundsToChampion", 1),
                playerOneWins = AiArenaReflectionReader.ReadIntProperty(controller, "PlayerOneWins", 0),
                playerTwoWins = AiArenaReflectionReader.ReadIntProperty(controller, "PlayerTwoWins", 0),
                currentRespawnSeedIndex = AiArenaReflectionReader.ReadIntProperty(controller, "CurrentRespawnSeedIndex", 0),
                currentRespawnSeedLabel = AiArenaReflectionReader.ReadStringProperty(controller, "CurrentRespawnSeedLabel", "Fallback"),
                pendingRoundWinnerSlot = AiArenaReflectionReader.ReadEnumAsIntProperty(controller, "PendingRoundWinnerSlot", 0),
                pendingChampionSlot = AiArenaReflectionReader.ReadEnumAsIntProperty(controller, "PendingChampionSlot", 0),
                championAnnouncementSlot = AiArenaReflectionReader.ReadEnumAsIntProperty(controller, "ChampionAnnouncementSlot", 0),
            };
        }
    }
}
