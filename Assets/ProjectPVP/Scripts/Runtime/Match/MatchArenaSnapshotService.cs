using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Match
{
    internal readonly struct MatchArenaSnapshotState
    {
        internal MatchArenaSnapshotState(
            Rect wrapBounds,
            bool roundResetPending,
            int roundsToChampion,
            int playerOneWins,
            int playerTwoWins,
            int currentRespawnSeedIndex,
            string currentRespawnSeedLabel,
            CombatantSlotId pendingRoundWinnerSlot,
            CombatantSlotId pendingChampionSlot,
            CombatantSlotId championAnnouncementSlot)
        {
            WrapBounds = wrapBounds;
            RoundResetPending = roundResetPending;
            RoundsToChampion = roundsToChampion;
            PlayerOneWins = playerOneWins;
            PlayerTwoWins = playerTwoWins;
            CurrentRespawnSeedIndex = currentRespawnSeedIndex;
            CurrentRespawnSeedLabel = currentRespawnSeedLabel;
            PendingRoundWinnerSlot = pendingRoundWinnerSlot;
            PendingChampionSlot = pendingChampionSlot;
            ChampionAnnouncementSlot = championAnnouncementSlot;
        }

        internal Rect WrapBounds { get; }
        internal bool RoundResetPending { get; }
        internal int RoundsToChampion { get; }
        internal int PlayerOneWins { get; }
        internal int PlayerTwoWins { get; }
        internal int CurrentRespawnSeedIndex { get; }
        internal string CurrentRespawnSeedLabel { get; }
        internal CombatantSlotId PendingRoundWinnerSlot { get; }
        internal CombatantSlotId PendingChampionSlot { get; }
        internal CombatantSlotId ChampionAnnouncementSlot { get; }
    }

    internal static class MatchArenaSnapshotService
    {
        internal static AiArenaArenaSnapshot Build(MatchArenaSnapshotState state)
        {
            return new AiArenaArenaSnapshot
            {
                wrapBounds = state.WrapBounds,
                roundResetPending = state.RoundResetPending,
                roundsToChampion = state.RoundsToChampion,
                playerOneWins = state.PlayerOneWins,
                playerTwoWins = state.PlayerTwoWins,
                currentRespawnSeedIndex = state.CurrentRespawnSeedIndex,
                currentRespawnSeedLabel = state.CurrentRespawnSeedLabel,
                pendingRoundWinnerSlot = (int)state.PendingRoundWinnerSlot,
                pendingChampionSlot = (int)state.PendingChampionSlot,
                championAnnouncementSlot = (int)state.ChampionAnnouncementSlot,
            };
        }
    }
}
