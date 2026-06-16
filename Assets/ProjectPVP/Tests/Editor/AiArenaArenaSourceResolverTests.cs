using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaArenaSourceResolverTests
    {
        [Test]
        public void ResolveArenaSnapshot_UsesRegisteredSourceBeforeSceneFallback()
        {
            ClearSnapshotRegistry();
            GameObject registeredRoot = new GameObject("RegisteredArenaSource");
            GameObject sceneRoot = new GameObject("SceneArenaSource");
            SnapshotSourceArena registered = registeredRoot.AddComponent<SnapshotSourceArena>();
            MatchController sceneFallback = sceneRoot.AddComponent<MatchController>();

            try
            {
                registered.roundsToChampion = 7;
                sceneFallback.RoundsToChampion = 2;
                AiArenaSnapshotSourceRegistry.Register(registered);

                AiArenaArenaSnapshot snapshot = AiArenaArenaSourceResolver.ResolveArenaSnapshot();

                Assert.That(snapshot.roundsToChampion, Is.EqualTo(7));
                Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Registered"));
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(registeredRoot);
                Object.DestroyImmediate(sceneRoot);
            }
        }

        [Test]
        public void ResolveSceneArenaSnapshot_UsesTypedSourceBeforeLegacyMatchController()
        {
            GameObject typedRoot = new GameObject("TypedArenaSource");
            GameObject fallbackRoot = new GameObject("LegacyArenaSource");
            SnapshotSourceArena typed = typedRoot.AddComponent<SnapshotSourceArena>();
            MatchController fallback = fallbackRoot.AddComponent<MatchController>();

            try
            {
                typed.roundsToChampion = 5;
                fallback.RoundsToChampion = 2;

                AiArenaArenaSnapshot snapshot = AiArenaArenaSourceResolver.ResolveSceneArenaSnapshot(
                    new MonoBehaviour[] { fallback, null, typed });

                Assert.That(snapshot.roundsToChampion, Is.EqualTo(5));
                Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Registered"));
            }
            finally
            {
                Object.DestroyImmediate(typedRoot);
                Object.DestroyImmediate(fallbackRoot);
            }
        }

        [Test]
        public void ResolveSceneArenaSnapshot_FallsBackToLegacyMatchControllerName()
        {
            GameObject fallbackRoot = new GameObject("LegacyArenaSource");
            GameObject otherRoot = new GameObject("OtherArenaSource");
            MatchController fallback = fallbackRoot.AddComponent<MatchController>();
            OtherArenaSource other = otherRoot.AddComponent<OtherArenaSource>();

            try
            {
                Rect wrapBounds = new Rect(-32f, -16f, 64f, 48f);
                fallback.ActiveWrapBounds = wrapBounds;
                fallback.IsRoundResetPending = true;
                fallback.RoundsToChampion = 4;
                fallback.PlayerOneWins = 3;
                fallback.PlayerTwoWins = 1;
                fallback.CurrentRespawnSeedIndex = 8;
                fallback.CurrentRespawnSeedLabel = "Needle";
                fallback.PendingRoundWinnerSlot = CombatantSlotId.SlotTwo;
                fallback.PendingChampionSlot = CombatantSlotId.SlotOne;
                fallback.ChampionAnnouncementSlot = CombatantSlotId.SlotTwo;

                AiArenaArenaSnapshot snapshot = AiArenaArenaSourceResolver.ResolveSceneArenaSnapshot(
                    new MonoBehaviour[] { other, fallback });

                Assert.That(snapshot.wrapBounds, Is.EqualTo(wrapBounds));
                Assert.That(snapshot.roundResetPending, Is.True);
                Assert.That(snapshot.roundsToChampion, Is.EqualTo(4));
                Assert.That(snapshot.playerOneWins, Is.EqualTo(3));
                Assert.That(snapshot.playerTwoWins, Is.EqualTo(1));
                Assert.That(snapshot.currentRespawnSeedIndex, Is.EqualTo(8));
                Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Needle"));
                Assert.That(snapshot.pendingRoundWinnerSlot, Is.EqualTo(2));
                Assert.That(snapshot.pendingChampionSlot, Is.EqualTo(1));
                Assert.That(snapshot.championAnnouncementSlot, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(fallbackRoot);
                Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void ResolveSceneArenaSnapshot_ReturnsDefaultWhenSceneHasNoArenaSource()
        {
            AiArenaArenaSnapshot snapshot = AiArenaArenaSourceResolver.ResolveSceneArenaSnapshot(null);

            Assert.That(snapshot.wrapBounds, Is.EqualTo(new Rect(-1280f, -720f, 2560f, 1440f)));
            Assert.That(snapshot.roundsToChampion, Is.EqualTo(1));
            Assert.That(snapshot.currentRespawnSeedLabel, Is.EqualTo("Fallback"));
        }

        private static void ClearSnapshotRegistry()
        {
            MethodInfo method = typeof(AiArenaSnapshotSourceRegistry).GetMethod(
                "ClearForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
        }

        private sealed class SnapshotSourceArena : MonoBehaviour, IAiArenaArenaSnapshotSource
        {
            public int roundsToChampion;

            public AiArenaArenaSnapshot BuildAiArenaArenaSnapshot()
            {
                return new AiArenaArenaSnapshot
                {
                    wrapBounds = new Rect(-640f, -360f, 1280f, 720f),
                    roundsToChampion = roundsToChampion,
                    currentRespawnSeedLabel = "Registered",
                };
            }
        }

        private sealed class MatchController : MonoBehaviour
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

        private sealed class OtherArenaSource : MonoBehaviour
        {
        }
    }
}
