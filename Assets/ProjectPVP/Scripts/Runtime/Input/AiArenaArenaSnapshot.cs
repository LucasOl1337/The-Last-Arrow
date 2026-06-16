using UnityEngine;

namespace ProjectPVP.Input
{
    public struct AiArenaArenaSnapshot
    {
        public Rect wrapBounds;
        public bool roundResetPending;
        public int roundsToChampion;
        public int playerOneWins;
        public int playerTwoWins;
        public int currentRespawnSeedIndex;
        public string currentRespawnSeedLabel;
        public int pendingRoundWinnerSlot;
        public int pendingChampionSlot;
        public int championAnnouncementSlot;
    }
}
