using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

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

        private static void SetPublicField(Type declaringType, object instance, string fieldName, object value)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Expected runtime assignment field '{0}'.", fieldName);
            field.SetValue(instance, value);
        }

        private static ProjectPVP.Gameplay.PlayerController CreatePlayer(GameObject root)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            ProjectPVP.Gameplay.PlayerController controller = root.AddComponent<ProjectPVP.Gameplay.PlayerController>();
            controller.body = body;
            controller.bodyCollider = collider;
            return controller;
        }
    }
}
