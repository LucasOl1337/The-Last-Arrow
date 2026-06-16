using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaArenaSnapshotFallbackServiceTests
    {
        [Test]
        public void BuildDefault_ReturnsArenaFallbackDefaults()
        {
            AiArenaArenaSnapshot snapshot = AiArenaArenaSnapshotFallbackService.BuildDefault();

            Assert.That(snapshot.wrapBounds, Is.EqualTo(new Rect(-1280f, -720f, 2560f, 1440f)));
            Assert.That(snapshot.roundResetPending, Is.False);
            Assert.That(snapshot.roundsToChampion, Is.EqualTo(1));
            Assert.That(snapshot.playerOneWins, Is.Zero);
            Assert.That(snapshot.playerTwoWins, Is.Zero);
            Assert.That(snapshot.currentRespawnSeedIndex, Is.Zero);
            Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Fallback"));
            Assert.That(snapshot.pendingRoundWinnerSlot, Is.Zero);
            Assert.That(snapshot.pendingChampionSlot, Is.Zero);
            Assert.That(snapshot.championAnnouncementSlot, Is.Zero);
        }

        [Test]
        public void BuildFromController_ReadsLegacyMatchControllerProperties()
        {
            GameObject root = new GameObject("LegacyArenaSnapshotSource");
            LegacyArenaSnapshotSource source = root.AddComponent<LegacyArenaSnapshotSource>();

            try
            {
                Rect wrapBounds = new Rect(-64f, -32f, 128f, 96f);
                source.ActiveWrapBounds = wrapBounds;
                source.IsRoundResetPending = true;
                source.RoundsToChampion = 4;
                source.PlayerOneWins = 2;
                source.PlayerTwoWins = 3;
                source.CurrentRespawnSeedIndex = 5;
                source.CurrentRespawnSeedLabel = "Crossfire";
                source.PendingRoundWinnerSlot = CombatantSlotId.SlotTwo;
                source.PendingChampionSlot = CombatantSlotId.SlotOne;
                source.ChampionAnnouncementSlot = CombatantSlotId.SlotTwo;

                AiArenaArenaSnapshot snapshot = AiArenaArenaSnapshotFallbackService.BuildFromController(source);

                Assert.That(snapshot.wrapBounds, Is.EqualTo(wrapBounds));
                Assert.That(snapshot.roundResetPending, Is.True);
                Assert.That(snapshot.roundsToChampion, Is.EqualTo(4));
                Assert.That(snapshot.playerOneWins, Is.EqualTo(2));
                Assert.That(snapshot.playerTwoWins, Is.EqualTo(3));
                Assert.That(snapshot.currentRespawnSeedIndex, Is.EqualTo(5));
                Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Crossfire"));
                Assert.That(snapshot.pendingRoundWinnerSlot, Is.EqualTo(2));
                Assert.That(snapshot.pendingChampionSlot, Is.EqualTo(1));
                Assert.That(snapshot.championAnnouncementSlot, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildFromController_UsesDefaultsForMissingController()
        {
            AiArenaArenaSnapshot snapshot = AiArenaArenaSnapshotFallbackService.BuildFromController(null);

            Assert.That(snapshot.wrapBounds, Is.EqualTo(default(Rect)));
            Assert.That(snapshot.roundsToChampion, Is.Zero);
            Assert.That(snapshot.currentRespawnSeedLabel, Is.Null);
        }

        private sealed class LegacyArenaSnapshotSource : MonoBehaviour
        {
            public Rect ActiveWrapBounds { get; set; }
            public bool IsRoundResetPending { get; set; }
            public int RoundsToChampion { get; set; }
            public int PlayerOneWins { get; set; }
            public int PlayerTwoWins { get; set; }
            public int CurrentRespawnSeedIndex { get; set; }
            public string CurrentRespawnSeedLabel { get; set; }
            public CombatantSlotId PendingRoundWinnerSlot { get; set; }
            public CombatantSlotId PendingChampionSlot { get; set; }
            public CombatantSlotId ChampionAnnouncementSlot { get; set; }
        }
    }
}
