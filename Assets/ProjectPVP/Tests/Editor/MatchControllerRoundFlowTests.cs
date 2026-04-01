using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class MatchControllerRoundFlowTests
    {
        private static readonly MethodInfo AddWinMethod =
            typeof(MatchController).GetMethod("AddWin", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveChampionSlotMethod =
            typeof(MatchController).GetMethod("ResolveChampionSlot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResetSeriesStateMethod =
            typeof(MatchController).GetMethod("ResetSeriesState", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void GetSpawnPoint_UsesCurrentRespawnSeedPair()
        {
            GameObject gameObject = new GameObject("MatchControllerRoundFlowTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                SetPrivateField(matchController, "roundRespawnSeeds", new List<RoundRespawnSeed>
                {
                    new RoundRespawnSeed
                    {
                        label = "Test Seed",
                        slotOneSpawnPoint = new Vector2(-123f, 45f),
                        slotTwoSpawnPoint = new Vector2(321f, -67f),
                    },
                });
                SetPrivateField(matchController, "currentRespawnSeedIndex", 0);

                Assert.That(matchController.GetSpawnPoint(CombatantSlotId.SlotOne), Is.EqualTo(new Vector2(-123f, 45f)));
                Assert.That(matchController.GetSpawnPoint(CombatantSlotId.SlotTwo), Is.EqualTo(new Vector2(321f, -67f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResetSeriesState_ClearsRoundsAndRespawnSeedCycleAfterChampion()
        {
            Assert.That(AddWinMethod, Is.Not.Null);
            Assert.That(ResolveChampionSlotMethod, Is.Not.Null);
            Assert.That(ResetSeriesStateMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerRoundFlowTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                matchController.maxWins = 3;
                SetPrivateField(matchController, "roundRespawnSeeds", new List<RoundRespawnSeed>
                {
                    new RoundRespawnSeed { label = "A", slotOneSpawnPoint = new Vector2(-1f, -1f), slotTwoSpawnPoint = new Vector2(1f, 1f) },
                    new RoundRespawnSeed { label = "B", slotOneSpawnPoint = new Vector2(-2f, -2f), slotTwoSpawnPoint = new Vector2(2f, 2f) },
                    new RoundRespawnSeed { label = "C", slotOneSpawnPoint = new Vector2(-3f, -3f), slotTwoSpawnPoint = new Vector2(3f, 3f) },
                });
                SetPrivateField(matchController, "currentRespawnSeedIndex", 2);

                AddWinMethod.Invoke(matchController, new object[] { CombatantSlotId.SlotOne });
                AddWinMethod.Invoke(matchController, new object[] { CombatantSlotId.SlotOne });
                AddWinMethod.Invoke(matchController, new object[] { CombatantSlotId.SlotOne });

                CombatantSlotId championSlot = (CombatantSlotId)ResolveChampionSlotMethod.Invoke(matchController, null);
                Assert.That(championSlot, Is.EqualTo(CombatantSlotId.SlotOne));

                ResetSeriesStateMethod.Invoke(matchController, null);

                Assert.That(matchController.PlayerOneWins, Is.Zero);
                Assert.That(matchController.PlayerTwoWins, Is.Zero);
                Assert.That(matchController.CurrentRespawnSeedIndex, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetPrivateField<T>(MatchController matchController, string fieldName, T value)
        {
            FieldInfo field = typeof(MatchController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected MatchController to define private field '{0}'.", fieldName);
            field.SetValue(matchController, value);
        }
    }
}
