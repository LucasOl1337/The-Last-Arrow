using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class MatchArenaSnapshotServiceTests
    {
        [Test]
        public void Build_MapsMatchStateToAiArenaSnapshot()
        {
            Rect wrapBounds = new Rect(-12f, -24f, 120f, 240f);
            MatchArenaSnapshotState state = new MatchArenaSnapshotState(
                wrapBounds: wrapBounds,
                roundResetPending: true,
                roundsToChampion: 3,
                playerOneWins: 1,
                playerTwoWins: 2,
                currentRespawnSeedIndex: 4,
                currentRespawnSeedLabel: "Mirror",
                pendingRoundWinnerSlot: CombatantSlotId.SlotTwo,
                pendingChampionSlot: CombatantSlotId.SlotOne,
                championAnnouncementSlot: CombatantSlotId.SlotTwo);

            AiArenaArenaSnapshot snapshot = MatchArenaSnapshotService.Build(state);

            Assert.That(snapshot.wrapBounds, Is.EqualTo(wrapBounds));
            Assert.That(snapshot.roundResetPending, Is.True);
            Assert.That(snapshot.roundsToChampion, Is.EqualTo(3));
            Assert.That(snapshot.playerOneWins, Is.EqualTo(1));
            Assert.That(snapshot.playerTwoWins, Is.EqualTo(2));
            Assert.That(snapshot.currentRespawnSeedIndex, Is.EqualTo(4));
            Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Mirror"));
            Assert.That(snapshot.pendingRoundWinnerSlot, Is.EqualTo(2));
            Assert.That(snapshot.pendingChampionSlot, Is.EqualTo(1));
            Assert.That(snapshot.championAnnouncementSlot, Is.EqualTo(2));
        }

        [Test]
        public void Build_PreservesNoneSlotsAndLabelValue()
        {
            MatchArenaSnapshotState state = new MatchArenaSnapshotState(
                wrapBounds: default,
                roundResetPending: false,
                roundsToChampion: 1,
                playerOneWins: 0,
                playerTwoWins: 0,
                currentRespawnSeedIndex: 0,
                currentRespawnSeedLabel: string.Empty,
                pendingRoundWinnerSlot: CombatantSlotId.None,
                pendingChampionSlot: CombatantSlotId.None,
                championAnnouncementSlot: CombatantSlotId.None);

            AiArenaArenaSnapshot snapshot = MatchArenaSnapshotService.Build(state);

            Assert.That(snapshot.roundResetPending, Is.False);
            Assert.That(snapshot.currentRespawnSeedLabel, Is.Empty);
            Assert.That(snapshot.pendingRoundWinnerSlot, Is.Zero);
            Assert.That(snapshot.pendingChampionSlot, Is.Zero);
            Assert.That(snapshot.championAnnouncementSlot, Is.Zero);
        }
    }
}
