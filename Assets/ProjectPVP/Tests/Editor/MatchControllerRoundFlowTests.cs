using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectPVP.Tests.Editor
{
    public sealed class MatchControllerRoundFlowTests
    {
        private static readonly MethodInfo PlayerAwakeMethod =
            typeof(ProjectPVP.Gameplay.PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplyWrapMethod =
            typeof(MatchController).GetMethod("ApplyWrap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ApplyRuntimeBotMenuAssignmentsMethod =
            typeof(MatchController).GetMethod("ApplyRuntimeBotMenuAssignments", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ShouldApplySlotTwoBotFallbackMethod =
            typeof(MatchController).GetMethod("ShouldApplySlotTwoBotFallback", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveSlotTwoAutoBotBrainMethod =
            typeof(MatchController).GetMethod("ResolveSlotTwoAutoBotBrain", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo EnsurePlayerTwoDebugBotEnabledMethod =
            typeof(MatchController).GetMethod("EnsurePlayerTwoDebugBotEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveDeathSummaryMethod =
            typeof(MatchController).GetMethod("ResolveDeathSummary", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveDeathPositionMethod =
            typeof(MatchController).GetMethod("ResolveDeathPosition", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo BeginRespawnFreezeMethod =
            typeof(MatchController).GetMethod("BeginRespawnFreeze", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ShowChampionAnnouncementMethod =
            typeof(MatchController).GetMethod("ShowChampionAnnouncement", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo TickFreezeAndAnnouncementsMethod =
            typeof(MatchController).GetMethod("TickFreezeAndAnnouncements", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ShouldShowFinalKillInfoMethod =
            typeof(ProjectPvpMatchRoundHudOverlay).GetMethod("ShouldShowFinalKillInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RespawnPlayersMethod =
            typeof(MatchController).GetMethod("RespawnPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo MatchAwakeMethod =
            typeof(MatchController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo HandlePlayerDeathMethod =
            typeof(MatchController).GetMethod("HandlePlayerDeath", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ResolveQueuedDeathsMethod =
            typeof(MatchController).GetMethod("ResolveQueuedDeaths", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerContextField =
            typeof(ProjectPVP.Gameplay.PlayerController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearActiveProjectilesForTestsMethod =
            typeof(ProjectPVP.Gameplay.ProjectileController).GetMethod("ClearActiveProjectilesForTests", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterActiveProjectileMethod =
            typeof(ProjectPVP.Gameplay.ProjectileController).GetMethod("RegisterActiveProjectile", BindingFlags.Static | BindingFlags.NonPublic);

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
        public void ApplyWrap_KillsPlayerBelowBottomRingOutLimit()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(ApplyWrapMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerRingOutTests");
            GameObject playerObject = new GameObject("RingOutPlayer");
            MatchController matchController = matchObject.AddComponent<MatchController>();

            try
            {
                ProjectPVP.Gameplay.PlayerController player = CreatePlayer(playerObject);
                matchController.defaultWrapBounds = new Rect(-100f, -50f, 200f, 100f);
                matchController.defaultWrapPadding = new Vector2(10f, 10f);
                matchController.verticalRingOutEnabled = true;
                player.transform.position = new Vector3(0f, -61f, 0f);
                player.body.position = new Vector2(0f, -61f);

                PlayerAwakeMethod.Invoke(player, null);

                ApplyWrapMethod.Invoke(matchController, new object[] { player });

                Assert.That(player.IsDead, Is.True);
                Assert.That(player.transform.position.y, Is.EqualTo(-61f).Within(0.001f));
                Assert.That(player.LastFatalHitSource, Is.Null);
                Assert.That(player.LastFatalHitCause, Is.EqualTo("Ring Out"));
                Assert.That(player.LastFatalHitSummary, Is.EqualTo("Environment via Ring Out"));
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ApplyWrap_StillWrapsHorizontallyWhenRingOutIsEnabled()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(ApplyWrapMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerHorizontalWrapTests");
            GameObject playerObject = new GameObject("HorizontalWrapPlayer");
            MatchController matchController = matchObject.AddComponent<MatchController>();

            try
            {
                ProjectPVP.Gameplay.PlayerController player = CreatePlayer(playerObject);
                matchController.defaultWrapBounds = new Rect(-100f, -50f, 200f, 100f);
                matchController.defaultWrapPadding = new Vector2(10f, 10f);
                matchController.verticalRingOutEnabled = true;
                player.transform.position = new Vector3(111f, 0f, 0f);
                player.body.position = new Vector2(111f, 0f);

                PlayerAwakeMethod.Invoke(player, null);

                ApplyWrapMethod.Invoke(matchController, new object[] { player });

                Assert.That(player.IsDead, Is.False);
                Assert.That(player.body.position, Is.EqualTo(new Vector2(-110f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ApplyRuntimeBotMenuAssignments_DisabledRuntimeSlotIsProcessedWithoutFallback()
        {
            Assert.That(ApplyRuntimeBotMenuAssignmentsMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerRuntimeBotMenuTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                CombatantSlotConfig slot = matchController.GetSlot(CombatantSlotId.SlotTwo);
                Assert.That(slot, Is.Not.Null);

                object enabledPayload = CreateRuntimeAssignmentsPayload(
                    CombatantSlotId.SlotTwo,
                    enabled: true,
                    botId: "test-bot",
                    displayName: "Test Bot");
                object[] enabledArguments = { enabledPayload, false };

                bool enabledProcessed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, enabledArguments);

                Assert.That(enabledProcessed, Is.True);
                Assert.That((bool)enabledArguments[1], Is.True);
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));

                object disabledPayload = CreateRuntimeAssignmentsPayload(CombatantSlotId.SlotTwo, enabled: false);
                object[] disabledArguments = { disabledPayload, false };

                bool disabledProcessed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, disabledArguments);

                Assert.That(disabledProcessed, Is.True);
                Assert.That((bool)disabledArguments[1], Is.False);
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyRuntimeBotMenuAssignments_ReapplyPreservesOriginalProfileForDisable()
        {
            Assert.That(ApplyRuntimeBotMenuAssignmentsMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerRuntimeBotReapplyTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();
            CombatantSlotProfile originalProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            CombatantSlotProfile firstOverride = null;
            CombatantSlotProfile secondOverride = null;

            try
            {
                originalProfile.controlMode = CombatantControlMode.Human;
                CombatantSlotConfig slot = matchController.GetSlot(CombatantSlotId.SlotTwo);
                Assert.That(slot, Is.Not.Null);
                slot.playerProfile = originalProfile;

                object firstPayload = CreateRuntimeAssignmentsPayload(
                    CombatantSlotId.SlotTwo,
                    enabled: true,
                    botId: "first-bot",
                    displayName: "First Bot");
                object[] firstArguments = { firstPayload, false };

                bool firstProcessed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, firstArguments);
                firstOverride = slot.playerProfile;

                Assert.That(firstProcessed, Is.True);
                Assert.That((bool)firstArguments[1], Is.True);
                Assert.That(firstOverride, Is.Not.SameAs(originalProfile));
                Assert.That(firstOverride.botId, Is.EqualTo("first-bot"));

                object secondPayload = CreateRuntimeAssignmentsPayload(
                    CombatantSlotId.SlotTwo,
                    enabled: true,
                    botId: "second-bot",
                    displayName: "Second Bot");
                object[] secondArguments = { secondPayload, false };

                bool secondProcessed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, secondArguments);
                secondOverride = slot.playerProfile;

                Assert.That(secondProcessed, Is.True);
                Assert.That((bool)secondArguments[1], Is.True);
                Assert.That(secondOverride, Is.Not.SameAs(originalProfile));
                Assert.That(secondOverride.botId, Is.EqualTo("second-bot"));

                object disabledPayload = CreateRuntimeAssignmentsPayload(CombatantSlotId.SlotTwo, enabled: false);
                object[] disabledArguments = { disabledPayload, false };

                bool disabledProcessed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, disabledArguments);

                Assert.That(disabledProcessed, Is.True);
                Assert.That((bool)disabledArguments[1], Is.False);
                Assert.That(slot.playerProfile, Is.SameAs(originalProfile));
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                if (firstOverride != null && firstOverride != originalProfile)
                {
                    Object.DestroyImmediate(firstOverride);
                }

                if (secondOverride != null && secondOverride != originalProfile && secondOverride != firstOverride)
                {
                    Object.DestroyImmediate(secondOverride);
                }

                Object.DestroyImmediate(originalProfile);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyRuntimeBotMenuAssignments_DuplicateRuntimeSlotsUseLastAssignmentForSlot()
        {
            Assert.That(ApplyRuntimeBotMenuAssignmentsMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerRuntimeBotDuplicateTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                CombatantSlotConfig slot = matchController.GetSlot(CombatantSlotId.SlotTwo);
                Assert.That(slot, Is.Not.Null);

                object payload = CreateRuntimeAssignmentsPayload(
                    CombatantSlotId.SlotTwo,
                    enabled: true,
                    botId: "bot-one",
                    displayName: "Bot One");
                AppendRuntimeAssignment(
                    payload,
                    CombatantSlotId.SlotTwo,
                    enabled: true,
                    botId: "bot-two",
                    displayName: "Bot Two");

                object[] arguments = { payload, false };
                bool processed = (bool)ApplyRuntimeBotMenuAssignmentsMethod.Invoke(matchController, arguments);

                Assert.That(processed, Is.True);
                Assert.That((bool)arguments[1], Is.True);
                Assert.That(slot.playerProfile, Is.Not.Null);
                Assert.That(slot.playerProfile.botId, Is.EqualTo("bot-two"));
                Assert.That(slot.playerProfile.ResolveBotDisplayName(CombatantSlotId.SlotTwo), Is.EqualTo("Bot Two"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SlotTwoBotFallback_RespectsAutoEnableAndForceBrainFlags()
        {
            Assert.That(ShouldApplySlotTwoBotFallbackMethod, Is.Not.Null);
            Assert.That(ResolveSlotTwoAutoBotBrainMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerBotFallbackFlagTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                matchController.autoEnableSlotTwoDebugBotOnPlay = false;

                bool shouldFallbackWhenDisabled = (bool)ShouldApplySlotTwoBotFallbackMethod.Invoke(matchController, null);

                Assert.That(shouldFallbackWhenDisabled, Is.False);

                matchController.autoEnableSlotTwoDebugBotOnPlay = true;
                matchController.autoForceCodexBrokerForSlotTwoOnPlay = false;
                matchController.slotTwoDebugAiBrain = AiBrainKind.LocalHeuristic;

                bool shouldFallbackWhenEnabled = (bool)ShouldApplySlotTwoBotFallbackMethod.Invoke(matchController, null);
                AiBrainKind configuredBrain = (AiBrainKind)ResolveSlotTwoAutoBotBrainMethod.Invoke(matchController, null);

                Assert.That(shouldFallbackWhenEnabled, Is.True);
                Assert.That(configuredBrain, Is.EqualTo(AiBrainKind.LocalHeuristic));

                matchController.autoForceCodexBrokerForSlotTwoOnPlay = true;

                AiBrainKind forcedBrain = (AiBrainKind)ResolveSlotTwoAutoBotBrainMethod.Invoke(matchController, null);

                Assert.That(forcedBrain, Is.EqualTo(AiBrainKind.CodexBroker));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SlotTwoBotAutomation_DefaultsToLocalHeuristicInsteadOfForcingCodexBroker()
        {
            GameObject gameObject = new GameObject("MatchControllerBotDefaultModeTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();

            try
            {
                Assert.That(matchController.autoEnableSlotTwoDebugBotOnPlay, Is.True);
                Assert.That(matchController.autoForceCodexBrokerForSlotTwoOnPlay, Is.False);
                Assert.That(matchController.slotTwoDebugAiBrain, Is.EqualTo(AiBrainKind.LocalHeuristic));
                Assert.That((AiBrainKind)ResolveSlotTwoAutoBotBrainMethod.Invoke(matchController, null), Is.EqualTo(AiBrainKind.LocalHeuristic));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ResolveDeathSummary_UsesFatalHitSourceAndCause()
        {
            Assert.That(ResolveDeathSummaryMethod, Is.Not.Null);
            Assert.That(ResolveDeathPositionMethod, Is.Not.Null);
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerResolveDeathSummaryTests");
            GameObject sourceObject = new GameObject("ResolveDeathSourcePlayer");
            GameObject deadObject = new GameObject("ResolveDeathTargetPlayer");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController source = CreatePlayer(sourceObject, 1);
                ProjectPVP.Gameplay.PlayerController dead = CreatePlayer(deadObject, 2);

                PlayerAwakeMethod.Invoke(source, null);
                PlayerAwakeMethod.Invoke(dead, null);
                dead.transform.position = new Vector3(120f, -34f, 0f);
                dead.body.position = new Vector2(120f, -34f);

                Assert.That(dead.TryKill(source, "Projectile"), Is.True);

                string summary = (string)ResolveDeathSummaryMethod.Invoke(matchController, new object[] { dead });
                Vector2 deathPosition = (Vector2)ResolveDeathPositionMethod.Invoke(matchController, new object[] { dead });

                Assert.That(summary, Is.EqualTo(source.BotDisplayName + " via Projectile"));
                Assert.That(deathPosition, Is.EqualTo(new Vector2(120f, -34f)));
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(deadObject);
            }
        }

        [Test]
        public void RespawnFreeze_ClearsLastRoundDeathSummaryWhenNoChampionAnnouncementIsActive()
        {
            Assert.That(BeginRespawnFreezeMethod, Is.Not.Null);
            Assert.That(TickFreezeAndAnnouncementsMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerRespawnFreezeClearTests");
            MatchController matchController = matchObject.AddComponent<MatchController>();

            try
            {
                SetPrivateField(matchController, "_lastRoundDeathSummary", "Slot 1 via Projectile");
                SetPrivateField(matchController, "_lastRoundDeathPosition", new Vector2(12f, 34f));

                BeginRespawnFreezeMethod.Invoke(matchController, new object[] { 0.05f });
                TickFreezeAndAnnouncementsMethod.Invoke(matchController, new object[] { 0.1f });

                Assert.That(matchController.LastRoundDeathSummary, Is.Empty);
                Assert.That(matchController.LastRoundDeathPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void FinalKillHud_RemainsVisibleDuringRespawnFreeze()
        {
            Assert.That(BeginRespawnFreezeMethod, Is.Not.Null);
            Assert.That(ShouldShowFinalKillInfoMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerFinalKillHudTests");
            GameObject overlayObject = new GameObject("MatchControllerFinalKillHudOverlay");
            MatchController matchController = matchObject.AddComponent<MatchController>();
            ProjectPvpMatchRoundHudOverlay overlay = overlayObject.AddComponent<ProjectPvpMatchRoundHudOverlay>();

            try
            {
                overlay.SetMatchController(matchController);
                SetPrivateField(matchController, "_lastRoundDeathSummary", "Slot 1 via Projectile");
                SetPrivateField(matchController, "_lastRoundDeathPosition", new Vector2(12f, 34f));

                BeginRespawnFreezeMethod.Invoke(matchController, new object[] { 0.5f });

                bool shouldShowFinalKillInfo = (bool)ShouldShowFinalKillInfoMethod.Invoke(overlay, null);

                Assert.That(matchController.IsRespawnFreezeActive, Is.True);
                Assert.That(shouldShowFinalKillInfo, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(overlayObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void ChampionAnnouncement_KeepsLastRoundDeathSummaryVisibleUntilAnnouncementEnds()
        {
            Assert.That(BeginRespawnFreezeMethod, Is.Not.Null);
            Assert.That(ShowChampionAnnouncementMethod, Is.Not.Null);
            Assert.That(TickFreezeAndAnnouncementsMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerChampionAnnouncementTests");
            MatchController matchController = matchObject.AddComponent<MatchController>();

            try
            {
                SetPrivateField(matchController, "_lastRoundDeathSummary", "Slot 1 via Projectile");
                SetPrivateField(matchController, "_lastRoundDeathPosition", new Vector2(12f, 34f));

                BeginRespawnFreezeMethod.Invoke(matchController, new object[] { 0.05f });
                ShowChampionAnnouncementMethod.Invoke(matchController, new object[] { CombatantSlotId.SlotTwo, 0.2f });

                TickFreezeAndAnnouncementsMethod.Invoke(matchController, new object[] { 0.06f });

                Assert.That(matchController.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(matchController.LastRoundDeathSummary, Is.EqualTo("Slot 1 via Projectile"));
                Assert.That(matchController.LastRoundDeathPosition, Is.EqualTo(new Vector2(12f, 34f)));

                TickFreezeAndAnnouncementsMethod.Invoke(matchController, new object[] { 0.2f });

                Assert.That(matchController.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.None));
                Assert.That(matchController.LastRoundDeathSummary, Is.Empty);
                Assert.That(matchController.LastRoundDeathPosition, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void OnDisable_ClearsTransientRoundStateAndPendingReset()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(MatchAwakeMethod, Is.Not.Null);
            Assert.That(HandlePlayerDeathMethod, Is.Not.Null);
            Assert.That(ResolveQueuedDeathsMethod, Is.Not.Null);
            Assert.That(BeginRespawnFreezeMethod, Is.Not.Null);
            Assert.That(ShowChampionAnnouncementMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerDisableResetStateTests");
            GameObject winnerObject = new GameObject("MatchControllerDisableResetWinner");
            GameObject loserObject = new GameObject("MatchControllerDisableResetLoser");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController winner = CreatePlayer(winnerObject, 1);
                ProjectPVP.Gameplay.PlayerController loser = CreatePlayer(loserObject, 2);

                PlayerAwakeMethod.Invoke(winner, null);
                PlayerAwakeMethod.Invoke(loser, null);

                SetPrivateField(matchController, "legacySlotOneController", winner);
                SetPrivateField(matchController, "legacySlotTwoController", loser);
                MatchAwakeMethod.Invoke(matchController, null);

                Assert.That(loser.TryKill(winner, "Projectile"), Is.True);
                HandlePlayerDeathMethod.Invoke(matchController, new object[] { loser });
                BeginRespawnFreezeMethod.Invoke(matchController, new object[] { 0.25f });
                ShowChampionAnnouncementMethod.Invoke(matchController, new object[] { CombatantSlotId.SlotOne, 0.5f });

                Assert.That(matchController.IsRoundResetPending, Is.True);
                Assert.That(matchController.IsRespawnFreezeActive, Is.True);
                Assert.That(matchController.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(matchController.LastRoundDeathSummary, Is.Not.Empty);

                matchObject.SetActive(false);

                Assert.That(matchController.IsRoundResetPending, Is.False);
                Assert.That(matchController.IsRespawnFreezeActive, Is.False);
                Assert.That(matchController.ChampionAnnouncementSlot, Is.EqualTo(CombatantSlotId.None));
                Assert.That(matchController.LastRoundDeathSummary, Is.Empty);
                Assert.That(matchController.LastRoundDeathPosition, Is.EqualTo(Vector2.zero));

                matchObject.SetActive(true);
                HandlePlayerDeathMethod.Invoke(matchController, new object[] { loser });

                Assert.That(matchController.IsRoundResetPending, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(loserObject);
                Object.DestroyImmediate(winnerObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void ResolveQueuedDeaths_HandlesSimultaneousDeathsAsRoundResetWithoutAwardingWins()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(MatchAwakeMethod, Is.Not.Null);
            Assert.That(HandlePlayerDeathMethod, Is.Not.Null);
            Assert.That(ResolveQueuedDeathsMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerSimultaneousDeathTests");
            GameObject playerOneObject = new GameObject("MatchControllerSimultaneousDeathPlayerOne");
            GameObject playerTwoObject = new GameObject("MatchControllerSimultaneousDeathPlayerTwo");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController playerOne = CreatePlayer(playerOneObject, 1);
                ProjectPVP.Gameplay.PlayerController playerTwo = CreatePlayer(playerTwoObject, 2);

                PlayerAwakeMethod.Invoke(playerOne, null);
                PlayerAwakeMethod.Invoke(playerTwo, null);

                SetPrivateField(matchController, "legacySlotOneController", playerOne);
                SetPrivateField(matchController, "legacySlotTwoController", playerTwo);
                MatchAwakeMethod.Invoke(matchController, null);

                MarkPlayerDead(playerOne, playerTwo, "Projectile");
                MarkPlayerDead(playerTwo, playerOne, "Projectile");

                HandlePlayerDeathMethod.Invoke(matchController, new object[] { playerOne });
                HandlePlayerDeathMethod.Invoke(matchController, new object[] { playerTwo });
                ResolveQueuedDeathsMethod.Invoke(matchController, null);

                Assert.That(matchController.PlayerOneWins, Is.Zero);
                Assert.That(matchController.PlayerTwoWins, Is.Zero);
                Assert.That(matchController.PendingRoundWinnerSlot, Is.EqualTo(CombatantSlotId.None));
                Assert.That(matchController.IsRoundResetPending, Is.True);
                Assert.That(matchController.LastRoundDeathSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void HandlePlayerDeath_DropsHeldArrowsAsCollectibleProjectiles()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(MatchAwakeMethod, Is.Not.Null);
            Assert.That(HandlePlayerDeathMethod, Is.Not.Null);
            Assert.That(ClearActiveProjectilesForTestsMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerCorpseDropTests");
            GameObject deadObject = new GameObject("CorpseDropDeadPlayer");
            GameObject liveObject = new GameObject("CorpseDropLivePlayer");
            GameObject projectilePrefabObject = new GameObject("CorpseDropProjectilePrefab");
            var droppedProjectiles = new List<ProjectPVP.Gameplay.ProjectileController>();

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController deadPlayer = CreatePlayer(deadObject, 1);
                ProjectPVP.Gameplay.PlayerController livePlayer = CreatePlayer(liveObject, 2);
                ProjectPVP.Gameplay.ProjectileController projectilePrefab = projectilePrefabObject.AddComponent<ProjectPVP.Gameplay.ProjectileController>();

                deadPlayer.projectilePrefab = projectilePrefab;
                deadPlayer.transform.position = new Vector3(24f, 18f, 0f);
                deadPlayer.body.position = new Vector2(24f, 18f);
                livePlayer.transform.position = new Vector3(96f, 18f, 0f);
                livePlayer.body.position = new Vector2(96f, 18f);

                PlayerAwakeMethod.Invoke(deadPlayer, null);
                PlayerAwakeMethod.Invoke(livePlayer, null);

                SetPrivateField(matchController, "legacySlotOneController", deadPlayer);
                SetPrivateField(matchController, "legacySlotTwoController", livePlayer);
                MatchAwakeMethod.Invoke(matchController, null);
                matchController.corpsesDropArrowsEnabled = true;
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                deadPlayer.SetRoundArrowCount(2);

                Assert.That(deadPlayer.TryKill(livePlayer, "Projectile"), Is.True);

                HandlePlayerDeathMethod.Invoke(matchController, new object[] { deadPlayer });
                ProjectPVP.Gameplay.ProjectileController.CopyActiveProjectiles(droppedProjectiles);

                Assert.That(droppedProjectiles, Has.Count.EqualTo(2));
                foreach (ProjectPVP.Gameplay.ProjectileController droppedProjectile in droppedProjectiles)
                {
                    Assert.That(droppedProjectile.SourceObject, Is.Null);
                    Assert.That(droppedProjectile.IsStuck, Is.True);
                    Assert.That(droppedProjectile.IsCollectible, Is.True);
                    Assert.That(droppedProjectile.IsDisarmed, Is.False);
                    Assert.That(droppedProjectile.CurrentVelocity, Is.EqualTo(Vector2.zero));
                }
            }
            finally
            {
                foreach (ProjectPVP.Gameplay.ProjectileController droppedProjectile in droppedProjectiles)
                {
                    if (droppedProjectile != null)
                    {
                        Object.DestroyImmediate(droppedProjectile.gameObject);
                    }
                }

                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(projectilePrefabObject);
                Object.DestroyImmediate(liveObject);
                Object.DestroyImmediate(deadObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void HandlePlayerDeath_DoesNotDuplicateCorpseDropsForRepeatedDeathEvent()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(MatchAwakeMethod, Is.Not.Null);
            Assert.That(HandlePlayerDeathMethod, Is.Not.Null);
            Assert.That(ClearActiveProjectilesForTestsMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerCorpseDropDuplicateTests");
            GameObject deadObject = new GameObject("CorpseDropDuplicateDeadPlayer");
            GameObject liveObject = new GameObject("CorpseDropDuplicateLivePlayer");
            GameObject projectilePrefabObject = new GameObject("CorpseDropDuplicateProjectilePrefab");
            var droppedProjectiles = new List<ProjectPVP.Gameplay.ProjectileController>();

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController deadPlayer = CreatePlayer(deadObject, 1);
                ProjectPVP.Gameplay.PlayerController livePlayer = CreatePlayer(liveObject, 2);
                ProjectPVP.Gameplay.ProjectileController projectilePrefab = projectilePrefabObject.AddComponent<ProjectPVP.Gameplay.ProjectileController>();

                deadPlayer.projectilePrefab = projectilePrefab;
                PlayerAwakeMethod.Invoke(deadPlayer, null);
                PlayerAwakeMethod.Invoke(livePlayer, null);

                SetPrivateField(matchController, "legacySlotOneController", deadPlayer);
                SetPrivateField(matchController, "legacySlotTwoController", livePlayer);
                MatchAwakeMethod.Invoke(matchController, null);
                matchController.corpsesDropArrowsEnabled = true;
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                deadPlayer.SetRoundArrowCount(2);

                Assert.That(deadPlayer.TryKill(livePlayer, "Projectile"), Is.True);

                HandlePlayerDeathMethod.Invoke(matchController, new object[] { deadPlayer });
                HandlePlayerDeathMethod.Invoke(matchController, new object[] { deadPlayer });
                ProjectPVP.Gameplay.ProjectileController.CopyActiveProjectiles(droppedProjectiles);

                Assert.That(droppedProjectiles, Has.Count.EqualTo(2));
            }
            finally
            {
                foreach (ProjectPVP.Gameplay.ProjectileController droppedProjectile in droppedProjectiles)
                {
                    if (droppedProjectile != null)
                    {
                        Object.DestroyImmediate(droppedProjectile.gameObject);
                    }
                }

                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(projectilePrefabObject);
                Object.DestroyImmediate(liveObject);
                Object.DestroyImmediate(deadObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void RespawnPlayers_RemovesActiveProjectilesFromPreviousRound()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(MatchAwakeMethod, Is.Not.Null);
            Assert.That(RespawnPlayersMethod, Is.Not.Null);
            Assert.That(ClearActiveProjectilesForTestsMethod, Is.Not.Null);
            Assert.That(RegisterActiveProjectileMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerRoundProjectileCleanupTests");
            GameObject slotOneObject = new GameObject("RoundProjectileCleanupSlotOne");
            GameObject slotTwoObject = new GameObject("RoundProjectileCleanupSlotTwo");
            GameObject projectileObject = new GameObject("RoundProjectileCleanupProjectile");
            var activeProjectiles = new List<ProjectPVP.Gameplay.ProjectileController>();

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController slotOne = CreatePlayer(slotOneObject, 1);
                ProjectPVP.Gameplay.PlayerController slotTwo = CreatePlayer(slotTwoObject, 2);
                ProjectPVP.Gameplay.ProjectileController projectile = projectileObject.AddComponent<ProjectPVP.Gameplay.ProjectileController>();

                PlayerAwakeMethod.Invoke(slotOne, null);
                PlayerAwakeMethod.Invoke(slotTwo, null);

                SetPrivateField(matchController, "legacySlotOneController", slotOne);
                SetPrivateField(matchController, "legacySlotTwoController", slotTwo);
                MatchAwakeMethod.Invoke(matchController, null);
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);

                projectile.Launch(
                    slotOneObject,
                    Vector2.zero,
                    Vector2.right,
                    null,
                    false,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Vector2.zero,
                    0f,
                    null);
                projectile.Stick(true);
                RegisterActiveProjectileMethod.Invoke(null, new object[] { projectile });

                ProjectPVP.Gameplay.ProjectileController.CopyActiveProjectiles(activeProjectiles);
                Assert.That(activeProjectiles, Has.Count.EqualTo(1));

                RespawnPlayersMethod.Invoke(matchController, new object[] { true });

                ProjectPVP.Gameplay.ProjectileController.CopyActiveProjectiles(activeProjectiles);
                Assert.That(activeProjectiles, Is.Empty);
            }
            finally
            {
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(projectileObject);
                Object.DestroyImmediate(slotTwoObject);
                Object.DestroyImmediate(slotOneObject);
                Object.DestroyImmediate(matchObject);
            }
        }

        [Test]
        public void RespawnPlayers_AutoBalanceLoadoutGivesTrailingPlayerShieldAndLeaderFewerArrows()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(RespawnPlayersMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerAutoBalanceShieldGrantTests");
            GameObject slotOneObject = new GameObject("MatchControllerAutoBalanceShieldGrantSlotOne");
            GameObject slotTwoObject = new GameObject("MatchControllerAutoBalanceShieldGrantSlotTwo");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController slotOne = CreatePlayer(slotOneObject, 1);
                ProjectPVP.Gameplay.PlayerController slotTwo = CreatePlayer(slotTwoObject, 2);

                PlayerAwakeMethod.Invoke(slotOne, null);
                PlayerAwakeMethod.Invoke(slotTwo, null);

                SetPrivateField(matchController, "legacySlotOneController", slotOne);
                SetPrivateField(matchController, "legacySlotTwoController", slotTwo);
                SetPrivateField(matchController, "slotWins", new int[] { 4, 1 });
                matchController.autoBalanceLoadoutEnabled = true;

                RespawnPlayersMethod.Invoke(matchController, new object[] { false });

                Assert.That(slotOne.CurrentArrows, Is.EqualTo(2));
                Assert.That(slotOne.HasShield, Is.False);
                Assert.That(slotTwo.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotTwo.HasShield, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(slotOneObject);
                Object.DestroyImmediate(slotTwoObject);
            }
        }

        [Test]
        public void RespawnPlayers_AutoBalanceLoadoutDoesNotPenalizeTiedLeaders()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(RespawnPlayersMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerAutoBalanceTieTests");
            GameObject slotOneObject = new GameObject("MatchControllerAutoBalanceTieSlotOne");
            GameObject slotTwoObject = new GameObject("MatchControllerAutoBalanceTieSlotTwo");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController slotOne = CreatePlayer(slotOneObject, 1);
                ProjectPVP.Gameplay.PlayerController slotTwo = CreatePlayer(slotTwoObject, 2);

                PlayerAwakeMethod.Invoke(slotOne, null);
                PlayerAwakeMethod.Invoke(slotTwo, null);

                SetPrivateField(matchController, "legacySlotOneController", slotOne);
                SetPrivateField(matchController, "legacySlotTwoController", slotTwo);
                SetPrivateField(matchController, "slotWins", new int[] { 2, 2 });
                matchController.autoBalanceLoadoutEnabled = true;

                RespawnPlayersMethod.Invoke(matchController, new object[] { false });

                Assert.That(slotOne.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotTwo.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotOne.HasShield, Is.False);
                Assert.That(slotTwo.HasShield, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(slotOneObject);
                Object.DestroyImmediate(slotTwoObject);
            }
        }

        [Test]
        public void RespawnPlayers_AutoBalanceLoadoutIgnoresUnassignedScoredSlots()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(RespawnPlayersMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerAutoBalanceUnassignedSlotTests");
            GameObject slotOneObject = new GameObject("MatchControllerAutoBalanceUnassignedSlotOne");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController slotOne = CreatePlayer(slotOneObject, 1);

                PlayerAwakeMethod.Invoke(slotOne, null);

                SetPrivateField(matchController, "legacySlotOneController", slotOne);
                SetPrivateField<ProjectPVP.Gameplay.PlayerController>(matchController, "legacySlotTwoController", null);
                SetPrivateField(matchController, "slotWins", new int[] { 0, 4 });
                matchController.autoBalanceLoadoutEnabled = true;

                RespawnPlayersMethod.Invoke(matchController, new object[] { false });

                Assert.That(slotOne.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotOne.HasShield, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(slotOneObject);
            }
        }

        [Test]
        public void RespawnPlayers_AutoBalanceLoadoutClearsWhenGapShrinksBelowThreshold()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(RespawnPlayersMethod, Is.Not.Null);

            GameObject matchObject = new GameObject("MatchControllerAutoBalanceShieldClearTests");
            GameObject slotOneObject = new GameObject("MatchControllerAutoBalanceShieldClearSlotOne");
            GameObject slotTwoObject = new GameObject("MatchControllerAutoBalanceShieldClearSlotTwo");

            try
            {
                MatchController matchController = matchObject.AddComponent<MatchController>();
                ProjectPVP.Gameplay.PlayerController slotOne = CreatePlayer(slotOneObject, 1);
                ProjectPVP.Gameplay.PlayerController slotTwo = CreatePlayer(slotTwoObject, 2);

                PlayerAwakeMethod.Invoke(slotOne, null);
                PlayerAwakeMethod.Invoke(slotTwo, null);

                SetPrivateField(matchController, "legacySlotOneController", slotOne);
                SetPrivateField(matchController, "legacySlotTwoController", slotTwo);

                SetPrivateField(matchController, "slotWins", new int[] { 6, 3 });
                matchController.autoBalanceLoadoutEnabled = true;
                RespawnPlayersMethod.Invoke(matchController, new object[] { false });

                Assert.That(slotOne.CurrentArrows, Is.EqualTo(2));
                Assert.That(slotOne.HasShield, Is.False);
                Assert.That(slotTwo.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotTwo.HasShield, Is.True);

                SetPrivateField(matchController, "slotWins", new int[] { 6, 4 });
                RespawnPlayersMethod.Invoke(matchController, new object[] { false });

                Assert.That(slotOne.CurrentArrows, Is.EqualTo(2));
                Assert.That(slotOne.HasShield, Is.False);
                Assert.That(slotTwo.CurrentArrows, Is.EqualTo(3));
                Assert.That(slotTwo.HasShield, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(matchObject);
                Object.DestroyImmediate(slotOneObject);
                Object.DestroyImmediate(slotTwoObject);
            }
        }

        [Test]
        public void EnsurePlayerTwoDebugBotEnabled_ReapplyPreservesOriginalProfileForDisable()
        {
            Assert.That(EnsurePlayerTwoDebugBotEnabledMethod, Is.Not.Null);

            GameObject gameObject = new GameObject("MatchControllerBotShortcutProfileTests");
            MatchController matchController = gameObject.AddComponent<MatchController>();
            CombatantSlotProfile originalProfile = ScriptableObject.CreateInstance<CombatantSlotProfile>();
            CombatantSlotProfile firstOverride = null;
            CombatantSlotProfile secondOverride = null;

            try
            {
                originalProfile.controlMode = CombatantControlMode.Human;
                originalProfile.aiBrain = AiBrainKind.LocalHeuristic;
                CombatantSlotConfig slot = matchController.GetSlot(CombatantSlotId.SlotTwo);
                Assert.That(slot, Is.Not.Null);
                slot.playerProfile = originalProfile;
                matchController.slotTwoDebugAiBrain = AiBrainKind.CodexBroker;

                EnsurePlayerTwoDebugBotEnabledMethod.Invoke(matchController, new object[] { true, true });
                firstOverride = slot.playerProfile;

                Assert.That(firstOverride, Is.Not.SameAs(originalProfile));
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));

                EnsurePlayerTwoDebugBotEnabledMethod.Invoke(matchController, new object[] { true, true });
                secondOverride = slot.playerProfile;

                Assert.That(secondOverride, Is.Not.SameAs(originalProfile));
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.AI));

                EnsurePlayerTwoDebugBotEnabledMethod.Invoke(matchController, new object[] { false, false });

                Assert.That(slot.playerProfile, Is.SameAs(originalProfile));
                Assert.That(slot.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                if (firstOverride != null && firstOverride != originalProfile)
                {
                    Object.DestroyImmediate(firstOverride);
                }

                if (secondOverride != null && secondOverride != originalProfile && secondOverride != firstOverride)
                {
                    Object.DestroyImmediate(secondOverride);
                }

                Object.DestroyImmediate(originalProfile);
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetPrivateField<T>(MatchController matchController, string fieldName, T value)
        {
            FieldInfo field = typeof(MatchController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected MatchController to define private field '{0}'.", fieldName);
            field.SetValue(matchController, value);
        }

        private static object CreateRuntimeAssignmentsPayload(
            CombatantSlotId slotId,
            bool enabled,
            string botId = "",
            string displayName = "")
        {
            Assembly assembly = typeof(MatchController).Assembly;
            Type assignmentsType = assembly.GetType("ProjectPVP.Match.RuntimeBotMenuAssignmentsFile", throwOnError: false);
            Type assignmentType = assembly.GetType("ProjectPVP.Match.RuntimeBotMenuSlotAssignment", throwOnError: false);
            Assert.That(assignmentsType, Is.Not.Null);
            Assert.That(assignmentType, Is.Not.Null);

            object payload = Activator.CreateInstance(assignmentsType);
            object assignment = Activator.CreateInstance(assignmentType);

            SetPublicField(assignmentType, assignment, "slotId", slotId.ToInt());
            SetPublicField(assignmentType, assignment, "enabled", enabled);
            SetPublicField(assignmentType, assignment, "botId", botId);
            SetPublicField(assignmentType, assignment, "displayName", displayName);

            FieldInfo slotsField = assignmentsType.GetField("slots", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(slotsField, Is.Not.Null);
            object slots = slotsField.GetValue(payload);
            Assert.That(slots, Is.Not.Null);
            MethodInfo addMethod = slots.GetType().GetMethod("Add");
            Assert.That(addMethod, Is.Not.Null);
            addMethod.Invoke(slots, new[] { assignment });

            return payload;
        }

        private static void AppendRuntimeAssignment(
            object payload,
            CombatantSlotId slotId,
            bool enabled,
            string botId = "",
            string displayName = "")
        {
            Assembly assembly = typeof(MatchController).Assembly;
            Type assignmentType = assembly.GetType("ProjectPVP.Match.RuntimeBotMenuSlotAssignment", throwOnError: false);
            Assert.That(assignmentType, Is.Not.Null);

            object assignment = Activator.CreateInstance(assignmentType);
            SetPublicField(assignmentType, assignment, "slotId", slotId.ToInt());
            SetPublicField(assignmentType, assignment, "enabled", enabled);
            SetPublicField(assignmentType, assignment, "botId", botId);
            SetPublicField(assignmentType, assignment, "displayName", displayName);

            FieldInfo slotsField = payload.GetType().GetField("slots", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(slotsField, Is.Not.Null);
            object slots = slotsField.GetValue(payload);
            Assert.That(slots, Is.Not.Null);
            MethodInfo addMethod = slots.GetType().GetMethod("Add");
            Assert.That(addMethod, Is.Not.Null);
            addMethod.Invoke(slots, new[] { assignment });
        }

        private static void SetPublicField(Type declaringType, object instance, string fieldName, object value)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Expected runtime assignment field '{0}'.", fieldName);
            field.SetValue(instance, value);
        }

        private static ProjectPVP.Gameplay.PlayerController CreatePlayer(GameObject root, int slotId = 1)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            ProjectPVP.Gameplay.PlayerController controller = root.AddComponent<ProjectPVP.Gameplay.PlayerController>();
            controller.body = body;
            controller.bodyCollider = collider;
            controller.slotId = slotId;
            return controller;
        }

        private static void MarkPlayerDead(ProjectPVP.Gameplay.PlayerController player, ProjectPVP.Gameplay.PlayerController source, string cause)
        {
            Assert.That(PlayerContextField, Is.Not.Null);
            object context = PlayerContextField.GetValue(player);
            Assert.That(context, Is.Not.Null);

            Type contextType = context.GetType();
            FieldInfo isDeadField = contextType.GetField("isDead", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo fatalSourceField = contextType.GetField("lastFatalHitSource", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo fatalCauseField = contextType.GetField("lastFatalHitCause", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo fatalPositionField = contextType.GetField("lastFatalHitPosition", BindingFlags.Instance | BindingFlags.Public);

            Assert.That(isDeadField, Is.Not.Null);
            Assert.That(fatalSourceField, Is.Not.Null);
            Assert.That(fatalCauseField, Is.Not.Null);
            Assert.That(fatalPositionField, Is.Not.Null);

            isDeadField.SetValue(context, true);
            fatalSourceField.SetValue(context, source);
            fatalCauseField.SetValue(context, cause);
            fatalPositionField.SetValue(context, player.RootPosition);
        }
    }
}
