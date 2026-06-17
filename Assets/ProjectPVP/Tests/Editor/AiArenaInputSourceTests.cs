using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaInputSourceTests
    {
        [Test]
        public void CombatantSlotProfile_DefaultControlMode_IsHuman()
        {
            CombatantSlotProfile profile = ScriptableObject.CreateInstance<CombatantSlotProfile>();

            try
            {
                Assert.That(profile.ResolveControlMode(), Is.EqualTo(CombatantControlMode.Human));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CombatantSlotProfile_DefaultAiBrain_IsLocalHeuristic()
        {
            CombatantSlotProfile profile = ScriptableObject.CreateInstance<CombatantSlotProfile>();

            try
            {
                Assert.That(profile.ResolveAiBrain(), Is.EqualTo(AiBrainKind.LocalHeuristic));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void IdleCombatantInputSource_ProducesEmptyFrame()
        {
            GameObject root = new GameObject("IdleInput");
            IdleCombatantInputSource input = root.AddComponent<IdleCombatantInputSource>();

            try
            {
                input.ConfigureForSlot(CombatantSlotId.SlotTwo);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.CurrentFrame.jumpPressed, Is.False);
                Assert.That(input.CurrentFrame.shootPressed, Is.False);
                Assert.That(input.FaceButtonDebug, Is.EqualTo("Idle"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdleCombatantInputSource_ResetInputState_ClearsCachedFrame()
        {
            GameObject root = new GameObject("IdleInputReset");
            IdleCombatantInputSource input = root.AddComponent<IdleCombatantInputSource>();

            try
            {
                input.CaptureFrame();
                Assert.That(input.CurrentFrame.aim, Is.EqualTo(Vector2.right));

                input.ResetInputState();

                Assert.That(input.CurrentFrame.aim, Is.EqualTo(Vector2.zero));
                Assert.That(input.CurrentFrame.frame, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void KeyboardPlayerInputSource_PreferredFamilySelection_UsesPreferredMatchIndex()
        {
            MethodInfo method = typeof(KeyboardPlayerInputSource).GetMethod(
                "ResolvePreferredSlotFromMatches",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            int[] matchedSlots = { 1, 2, 0, 0 };

            Assert.That((int)method.Invoke(null, new object[] { matchedSlots, 2, 0 }), Is.EqualTo(1));
            Assert.That((int)method.Invoke(null, new object[] { matchedSlots, 2, 1 }), Is.EqualTo(2));
            Assert.That((int)method.Invoke(null, new object[] { matchedSlots, 2, 2 }), Is.EqualTo(-1));
        }

        [Test]
        public void CodexBrokerCombatantInputSource_RequestVersionRejectsStaleCallbacks()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "IsCurrentRequestVersion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.That((bool)method.Invoke(null, new object[] { 2, 2 }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { 1, 2 }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { 0, 0 }), Is.False);
        }

        [Test]
        public void CodexBrokerCombatantInputSource_DirectBrokerIntentIsExecutableWithoutAgentActionFlag()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ApplyBrokerEnvelope",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            GameObject root = new GameObject("DirectBrokerIntentInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                input.useAgentDrivenMode = false;
                var envelope = new CodexBrokerIntentEnvelope
                {
                    sessionId = "direct-session",
                    hasAgentAction = false,
                    intent = new CodexStrategyIntent
                    {
                        mode = "pressure",
                        reason = "direct broker",
                    },
                };

                method.Invoke(input, new object[] { JsonUtility.ToJson(envelope) });

                Assert.That(input.SessionId, Is.EqualTo("direct-session"));
                Assert.That(input.HasAgentAction, Is.True);
                Assert.That(input.ControllerOwner, Is.EqualTo("CodexDirect"));
                Assert.That(input.CurrentIntentMode, Is.EqualTo("pressure"));
                Assert.That(input.CurrentIntentReason, Is.EqualTo("direct broker"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_AgentDrivenStartKeepsBrokerDefaultOwnerBeforeFirstAction()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ApplyBrokerEnvelope",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            GameObject root = new GameObject("AgentDrivenStartInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                input.useAgentDrivenMode = true;
                var envelope = new CodexBrokerIntentEnvelope
                {
                    sessionId = "agent-session",
                    hasAgentAction = false,
                    intent = new CodexStrategyIntent
                    {
                        mode = "stabilize",
                        reason = "waiting for first agent action",
                    },
                };

                method.Invoke(input, new object[] { JsonUtility.ToJson(envelope) });

                Assert.That(input.SessionId, Is.EqualTo("agent-session"));
                Assert.That(input.HasAgentAction, Is.False);
                Assert.That(input.ControllerOwner, Is.EqualTo("BrokerDefault"));
                Assert.That(input.CurrentIntentMode, Is.EqualTo("stabilize"));
                Assert.That(input.CurrentIntentReason, Is.EqualTo("waiting for first agent action"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_RoundResetPendingSuppressesStaleIntentActions()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ApplyBrokerEnvelope",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            ClearSnapshotRegistry();
            GameObject selfRoot = new GameObject("ResetSelf");
            GameObject opponentRoot = new GameObject("ResetOpponent");
            GameObject arenaRoot = new GameObject("ResetArena");
            CodexBrokerCombatantInputSource input = selfRoot.AddComponent<CodexBrokerCombatantInputSource>();
            SnapshotSourceController self = selfRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController opponent = opponentRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceArena arena = arenaRoot.AddComponent<SnapshotSourceArena>();

            try
            {
                input.autoStartSession = false;
                input.useAgentDrivenMode = false;
                self.slotId = 1;
                self.currentArrows = 5;
                opponent.slotId = 2;
                opponent.currentArrows = 5;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(220f, 0f, 0f);
                arena.roundResetPending = true;
                AiArenaSnapshotSourceRegistry.Register(self);
                AiArenaSnapshotSourceRegistry.Register(opponent);
                AiArenaSnapshotSourceRegistry.Register(arena);

                var envelope = new CodexBrokerIntentEnvelope
                {
                    hasAgentAction = false,
                    intent = new CodexStrategyIntent
                    {
                        mode = "zone",
                        shootBias = 1f,
                        meleeBias = 1f,
                        dashBias = 1f,
                        jumpBias = 1f,
                        reason = "stale before reset",
                    },
                };
                method.Invoke(input, new object[] { JsonUtility.ToJson(envelope) });

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                method.Invoke(input, new object[] { JsonUtility.ToJson(envelope) });
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.shootPressed, Is.False);
                Assert.That(input.CurrentFrame.shootHeld, Is.False);
                Assert.That(input.CurrentFrame.meleePressed, Is.False);
                Assert.That(input.CurrentFrame.dashPrimaryPressed, Is.False);
                Assert.That(input.CurrentFrame.jumpPressed, Is.False);
                Assert.That(input.FaceButtonDebug, Does.Contain("round_reset"));
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
                Object.DestroyImmediate(arenaRoot);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_UsesHeuristicFallbackWhenNoLiveIntentAvailable()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ResolveDecision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            GameObject root = new GameObject("HeuristicFallbackBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                var snapshot = new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        slotId = 1,
                        facing = 1,
                        arrows = 2,
                    },
                    opponents = new List<AiArenaCombatantObservation>
                    {
                        new AiArenaCombatantObservation
                        {
                            slotId = 2,
                            position = new Vector2(420f, 0f),
                        },
                    },
                    semantics = new AiArenaSemanticObservation
                    {
                        hasTarget = true,
                        targetSlotId = 2,
                        horizontalDistance = 420f,
                        targetDirection = Vector2.right,
                        predictedTargetDirection = Vector2.right,
                        targetInShootRange = true,
                        selfHasArrows = true,
                        shouldZone = true,
                    },
                };

                AiArenaDecisionEnvelope decision = (AiArenaDecisionEnvelope)method.Invoke(input, new object[] { snapshot });
                AiArenaDecisionEnvelope expected = AiArenaHeuristicPolicy.Decide(snapshot);

                Assert.That(decision.debugSummary, Is.EqualTo(expected.debugSummary));
                Assert.That(decision.moveAxis, Is.EqualTo(expected.moveAxis).Within(0.0001f));
                Assert.That(decision.shootPressed, Is.EqualTo(expected.shootPressed));
                Assert.That(decision.ultimatePressed, Is.EqualTo(expected.ultimatePressed));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_UsesLiveIntentBeforeItsExpiry()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ResolveDecision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentIntentField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_currentIntent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hasAgentActionField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_hasAgentAction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lastIntentReceivedTimeField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_lastIntentReceivedTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(currentIntentField, Is.Not.Null);
            Assert.That(hasAgentActionField, Is.Not.Null);
            Assert.That(lastIntentReceivedTimeField, Is.Not.Null);

            GameObject root = new GameObject("LiveIntentBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                currentIntentField.SetValue(input, new CodexStrategyIntent
                {
                    mode = "pressure",
                    reason = "live intent",
                    expiresInMs = 400,
                });
                hasAgentActionField.SetValue(input, true);
                lastIntentReceivedTimeField.SetValue(input, Time.realtimeSinceStartup - 0.2f);

                var snapshot = new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        slotId = 1,
                        facing = 1,
                        arrows = 2,
                        shootCooldownLeft = 0f,
                    },
                    opponents = new List<AiArenaCombatantObservation>
                    {
                        new AiArenaCombatantObservation
                        {
                            slotId = 2,
                            position = new Vector2(200f, 0f),
                        },
                    },
                    semantics = new AiArenaSemanticObservation
                    {
                        hasTarget = true,
                        targetSlotId = 2,
                        horizontalDistance = 200f,
                        targetDirection = Vector2.right,
                        predictedTargetDirection = Vector2.right,
                        targetInShootRange = true,
                        selfHasArrows = true,
                        shouldZone = true,
                    },
                };

                AiArenaDecisionEnvelope decision = (AiArenaDecisionEnvelope)method.Invoke(input, new object[] { snapshot });
                AiArenaDecisionEnvelope expected = AiArenaStrategicPolicy.Decide(snapshot, new CodexStrategyIntent
                {
                    mode = "pressure",
                    reason = "live intent",
                    expiresInMs = 400,
                });

                Assert.That(decision.debugSummary, Is.EqualTo(expected.debugSummary));
                Assert.That(decision.moveAxis, Is.EqualTo(expected.moveAxis).Within(0.0001f));
                Assert.That(decision.shootPressed, Is.EqualTo(expected.shootPressed));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_FallsBackImmediatelyWhenIntentExpiresImmediately()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ResolveDecision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentIntentField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_currentIntent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hasAgentActionField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_hasAgentAction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lastIntentReceivedTimeField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_lastIntentReceivedTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(currentIntentField, Is.Not.Null);
            Assert.That(hasAgentActionField, Is.Not.Null);
            Assert.That(lastIntentReceivedTimeField, Is.Not.Null);

            GameObject root = new GameObject("ZeroExpiryIntentBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                currentIntentField.SetValue(input, new CodexStrategyIntent
                {
                    mode = "pressure",
                    reason = "zero expiry",
                    expiresInMs = 0,
                });
                hasAgentActionField.SetValue(input, true);
                lastIntentReceivedTimeField.SetValue(input, Time.realtimeSinceStartup);

                var snapshot = new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        slotId = 1,
                        facing = 1,
                        arrows = 2,
                        shootCooldownLeft = 0f,
                    },
                    opponents = new List<AiArenaCombatantObservation>
                    {
                        new AiArenaCombatantObservation
                        {
                            slotId = 2,
                            position = new Vector2(200f, 0f),
                        },
                    },
                    semantics = new AiArenaSemanticObservation
                    {
                        hasTarget = true,
                        targetSlotId = 2,
                        horizontalDistance = 200f,
                        targetDirection = Vector2.right,
                        predictedTargetDirection = Vector2.right,
                        targetInShootRange = true,
                        selfHasArrows = true,
                        shouldZone = true,
                    },
                };

                AiArenaDecisionEnvelope decision = (AiArenaDecisionEnvelope)method.Invoke(input, new object[] { snapshot });
                AiArenaDecisionEnvelope expected = AiArenaHeuristicPolicy.Decide(snapshot);

                Assert.That(decision.debugSummary, Is.EqualTo(expected.debugSummary));
                Assert.That(decision.moveAxis, Is.EqualTo(expected.moveAxis).Within(0.0001f));
                Assert.That(decision.shootPressed, Is.EqualTo(expected.shootPressed));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_ForceRefreshesWhenRecoverableProjectileCountChanges()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ShouldForceRefresh",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo previousSnapshotField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_previousSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(previousSnapshotField, Is.Not.Null);

            GameObject root = new GameObject("RecoverableRefreshBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                previousSnapshotField.SetValue(input, new AiArenaSnapshotEnvelope
                {
                    arena = new AiArenaArenaObservation(),
                    semantics = new AiArenaSemanticObservation(),
                    projectiles = new List<AiArenaProjectileObservation>(),
                });

                var snapshot = new AiArenaSnapshotEnvelope
                {
                    arena = new AiArenaArenaObservation(),
                    semantics = new AiArenaSemanticObservation(),
                    projectiles = new List<AiArenaProjectileObservation>
                    {
                        new AiArenaProjectileObservation
                        {
                            isCollectible = true,
                            isStuck = true,
                            sourceSlotId = 1,
                            position = new Vector2(16f, 0f),
                        },
                    },
                };

                bool shouldRefresh = (bool)method.Invoke(input, new object[] { snapshot });

                Assert.That(shouldRefresh, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_ForceRefreshesWhenNearestRecoverableProjectileDistanceChanges()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ShouldForceRefresh",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo previousSnapshotField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_previousSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(previousSnapshotField, Is.Not.Null);

            GameObject root = new GameObject("RecoverableDistanceRefreshBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                previousSnapshotField.SetValue(input, new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        position = Vector2.zero,
                    },
                    arena = new AiArenaArenaObservation(),
                    semantics = new AiArenaSemanticObservation(),
                    projectiles = new List<AiArenaProjectileObservation>
                    {
                        new AiArenaProjectileObservation
                        {
                            isCollectible = true,
                            isStuck = true,
                            sourceSlotId = 1,
                            position = new Vector2(48f, 0f),
                        },
                    },
                });

                var snapshot = new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        position = Vector2.zero,
                    },
                    arena = new AiArenaArenaObservation(),
                    semantics = new AiArenaSemanticObservation(),
                    projectiles = new List<AiArenaProjectileObservation>
                    {
                        new AiArenaProjectileObservation
                        {
                            isCollectible = true,
                            isStuck = true,
                            sourceSlotId = 1,
                            position = new Vector2(120f, 0f),
                        },
                    },
                };

                bool shouldRefresh = (bool)method.Invoke(input, new object[] { snapshot });

                Assert.That(shouldRefresh, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CodexBrokerCombatantInputSource_FallsBackWhenIntentAgeExceedsExtendedWindow()
        {
            MethodInfo method = typeof(CodexBrokerCombatantInputSource).GetMethod(
                "ResolveDecision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentIntentField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_currentIntent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hasAgentActionField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_hasAgentAction",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lastIntentReceivedTimeField = typeof(CodexBrokerCombatantInputSource).GetField(
                "_lastIntentReceivedTime",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(currentIntentField, Is.Not.Null);
            Assert.That(hasAgentActionField, Is.Not.Null);
            Assert.That(lastIntentReceivedTimeField, Is.Not.Null);

            GameObject root = new GameObject("StaleIntentBrokerInput");
            CodexBrokerCombatantInputSource input = root.AddComponent<CodexBrokerCombatantInputSource>();

            try
            {
                currentIntentField.SetValue(input, new CodexStrategyIntent
                {
                    mode = "pressure",
                    reason = "stale intent",
                    expiresInMs = 400,
                });
                hasAgentActionField.SetValue(input, true);
                lastIntentReceivedTimeField.SetValue(input, Time.realtimeSinceStartup - 0.9f);

                var snapshot = new AiArenaSnapshotEnvelope
                {
                    self = new AiArenaCombatantObservation
                    {
                        slotId = 1,
                        facing = 1,
                        arrows = 2,
                        shootCooldownLeft = 0f,
                    },
                    opponents = new List<AiArenaCombatantObservation>
                    {
                        new AiArenaCombatantObservation
                        {
                            slotId = 2,
                            position = new Vector2(200f, 0f),
                        },
                    },
                    semantics = new AiArenaSemanticObservation
                    {
                        hasTarget = true,
                        targetSlotId = 2,
                        horizontalDistance = 200f,
                        targetDirection = Vector2.right,
                        predictedTargetDirection = Vector2.right,
                        targetInShootRange = true,
                        selfHasArrows = true,
                        shouldZone = true,
                    },
                };

                AiArenaDecisionEnvelope decision = (AiArenaDecisionEnvelope)method.Invoke(input, new object[] { snapshot });
                AiArenaDecisionEnvelope expected = AiArenaHeuristicPolicy.Decide(snapshot);

                Assert.That(decision.debugSummary, Is.EqualTo(expected.debugSummary));
                Assert.That(decision.shootPressed, Is.EqualTo(expected.shootPressed));
                Assert.That(decision.moveAxis, Is.EqualTo(expected.moveAxis).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AiArenaHeuristicPolicy_PressesForwardWhenTargetHasNoArrows()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 0,
                        position = new Vector2(250f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 250f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = false,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    selfHasArrows = true,
                    shouldRetreat = true,
                    selfCornered = false,
                    targetCornered = false,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void AiArenaSnapshotSourceRegistry_ReturnsRegisteredControllerSources()
        {
            ClearSnapshotRegistry();
            GameObject root = new GameObject("RegistrySource");
            SnapshotSourceController source = root.AddComponent<SnapshotSourceController>();

            try
            {
                AiArenaSnapshotSourceRegistry.Register(source);
                AiArenaSnapshotSourceRegistry.Register(source);
                var sources = new List<MonoBehaviour>();

                Assert.That(AiArenaSnapshotSourceRegistry.TryGetControllerSources(sources), Is.True);
                Assert.That(sources, Has.Count.EqualTo(1));
                Assert.That(sources[0], Is.SameAs(source));

                AiArenaSnapshotSourceRegistry.Unregister(source);
                Assert.That(AiArenaSnapshotSourceRegistry.TryGetControllerSources(sources), Is.False);
                Assert.That(sources, Is.Empty);
            }
            finally
            {
                ClearSnapshotRegistry();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProjectileController_BuildAiArenaProjectileSnapshot_IgnoresUnlaunchedProjectile()
        {
            GameObject root = new GameObject("UnlaunchedProjectile");
            ProjectPVP.Gameplay.ProjectileController projectile = root.AddComponent<ProjectPVP.Gameplay.ProjectileController>();

            try
            {
                AiArenaProjectileSnapshot snapshot = projectile.BuildAiArenaProjectileSnapshot();

                Assert.That(snapshot.isValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_MovesTowardClosestOpponent()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                self.currentArrows = 5;
                opponent.currentArrows = 5;
                selfRoot.transform.position = new Vector3(-200f, 0f, 0f);
                opponentRoot.transform.position = new Vector3(200f, 0f, 0f);

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.GreaterThan(0f));
                Assert.That(input.CurrentFrame.right, Is.True);
                Assert.That(input.CurrentFrame.aim.x, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_UsesTypedSnapshotSources()
        {
            GameObject selfRoot = new GameObject("TypedSelf");
            GameObject opponentRoot = new GameObject("TypedOpponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            SnapshotSourceController self = selfRoot.AddComponent<SnapshotSourceController>();
            SnapshotSourceController opponent = opponentRoot.AddComponent<SnapshotSourceController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = new Vector3(-160f, 0f, 0f);
                opponentRoot.transform.position = new Vector3(220f, 0f, 0f);

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.GreaterThan(0f));
                Assert.That(input.CurrentFrame.right, Is.True);
                Assert.That(input.CurrentFrame.aim.x, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_FallsBackSafely_OnInvalidResponse()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(128f, 0f, 0f);
                input.simulateInvalidResponse = true;

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.CurrentFrame.shootPressed, Is.False);
                Assert.That(input.FaceButtonDebug, Does.Contain("Fallback"));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_FallsBackSafely_OnTimeout()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(512f, 0f, 0f);
                input.simulateTransportTimeout = true;

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.FaceButtonDebug, Does.Contain("Timeout").IgnoreCase);
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
                Object.DestroyImmediate(opponentRoot);
            }
        }

        [Test]
        public void LocalAiCombatantInputSource_HandlesOpponentResetWithoutException()
        {
            GameObject selfRoot = new GameObject("Self");
            GameObject opponentRoot = new GameObject("Opponent");
            LocalAiCombatantInputSource input = selfRoot.AddComponent<LocalAiCombatantInputSource>();
            PlayerController self = selfRoot.AddComponent<PlayerController>();
            PlayerController opponent = opponentRoot.AddComponent<PlayerController>();

            try
            {
                self.slotId = 1;
                opponent.slotId = 2;
                selfRoot.transform.position = Vector3.zero;
                opponentRoot.transform.position = new Vector3(400f, 0f, 0f);

                input.ConfigureForSlot(CombatantSlotId.SlotOne);
                input.CaptureFrame();
                Object.DestroyImmediate(opponentRoot);
                input.CaptureFrame();

                Assert.That(input.CurrentFrame.axis, Is.EqualTo(0f));
                Assert.That(input.FaceButtonDebug, Does.Contain("Fallback").Or.Contain("NoTarget"));
            }
            finally
            {
                Object.DestroyImmediate(selfRoot);
            }
        }

        [Test]
        public void LocalTransport_UsesDashAgainstImminentProjectileThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(300f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    shouldDashEvade = true,
                    incomingProjectileTime = 0.12f,
                    targetInShootRange = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.dashPrimaryPressed, Is.True);
        }

        [Test]
        public void LocalTransport_PunishesVulnerableTargetInMeleeRange()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 1,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        isShootAnimating = true,
                        position = new Vector2(80f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    horizontalDistance = 80f,
                    verticalDistance = 0f,
                    targetInMeleeRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.meleePressed, Is.True);
        }

        [Test]
        public void LocalTransport_PressesVisibleMidRangeTargetForward()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(320f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    horizontalDistance = 320f,
                    verticalDistance = 0f,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.shootPressed, Is.True);
        }

        [Test]
        public void LocalTransport_DoesNotShootIntoProjectileParryWindow()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        canParryProjectile = true,
                        position = new Vector2(320f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    horizontalDistance = 320f,
                    verticalDistance = 0f,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void LocalTransport_DoesNotShootIntoUltimateProjectileBlock()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        canBlockProjectiles = true,
                        position = new Vector2(320f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    horizontalDistance = 320f,
                    verticalDistance = 0f,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            AiArenaLocalTransport transport = new AiArenaLocalTransport();
            AiArenaTransportResult result = transport.RequestDecisionJson(JsonUtility.ToJson(snapshot), 25);
            AiArenaDecisionEnvelope decision = JsonUtility.FromJson<AiArenaDecisionEnvelope>(result.ResponseJson);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_UsesCodexZoneIntentToPreferShooting()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(420f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 420f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.8f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = true,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "keep distance",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_ZoneIntentDoesNotShootIntoProjectileParryWindow()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        canParryProjectile = true,
                        position = new Vector2(420f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 420f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.95f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = true,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "respect projectile parry",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void StrategicPolicy_ZoneIntentDoesNotShootIntoUltimateProjectileBlock()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        canBlockProjectiles = true,
                        position = new Vector2(420f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 420f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.95f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = true,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "respect projectile block",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void StrategicPolicy_AntiAirDoesNotBypassProjectileDefense()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        canParryProjectile = true,
                        position = new Vector2(360f, 120f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 360f,
                    verticalDistance = 120f,
                    targetDirection = new Vector2(3f, 1f).normalized,
                    predictedTargetDirection = new Vector2(3f, 1f).normalized,
                    targetAbove = true,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.2f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = false,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "respect airborne parry",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_AntiAirRespectsShootCooldown()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0.2f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(360f, 120f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 360f,
                    verticalDistance = 120f,
                    targetDirection = new Vector2(3f, 1f).normalized,
                    predictedTargetDirection = new Vector2(3f, 1f).normalized,
                    targetAbove = true,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.2f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = false,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "respect anti air cooldown",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
        }

        [Test]
        public void StrategicPolicy_ZoneIntentAtPreferredRangeKeepsForwardDrift()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        position = new Vector2(360f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 360f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    selfHasArrows = true,
                    shouldZone = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "zone",
                preferredRange = 360,
                shootBias = 0.8f,
                advanceBias = 0.5f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = true,
                punishRecovery = true,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "keep moving through preferred range",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void StrategicPolicy_PressesDashWhenTargetHasNoArrows()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    dashCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 0,
                        position = new Vector2(250f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 250f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = false,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    selfHasArrows = true,
                    shouldRetreat = true,
                    selfCornered = false,
                    targetCornered = false,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "stabilize",
                preferredRange = 360,
                shootBias = 0.1f,
                advanceBias = 0.2f,
                meleeBias = 0.1f,
                dashBias = 0.2f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = false,
                punishRecovery = false,
                cornerEscapeBias = 0.5f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "hold ground",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.dashPrimaryPressed, Is.True);
        }

        [Test]
        public void StrategicPolicy_UsesLastArrowPressureToShootWhenTargetIsOutOfAmmo()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 0,
                        position = new Vector2(200f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 200f,
                    verticalDistance = 0f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    selfHasArrows = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "stabilize",
                preferredRange = 360,
                shootBias = 0.1f,
                advanceBias = 0.1f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = false,
                punishRecovery = false,
                cornerEscapeBias = 0.1f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "pressure last arrow",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.True);
            Assert.That(decision.shootHeld, Is.True);
            Assert.That(decision.debugSummary, Is.EqualTo("AI LAST ARROW PRESSURE"));
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void StrategicPolicy_UsesArrowLeadPressureToTakeTheShot()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 3,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(200f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 200f,
                    verticalDistance = 0f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    selfHasArrows = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "stabilize",
                preferredRange = 360,
                shootBias = 0.1f,
                advanceBias = 0.1f,
                meleeBias = 0.1f,
                dashBias = 0.1f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = false,
                punishRecovery = false,
                cornerEscapeBias = 0.1f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "press lead advantage",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.shootPressed, Is.True);
            Assert.That(decision.shootHeld, Is.True);
            Assert.That(decision.debugSummary, Is.EqualTo("AI ARROW LEAD PRESSURE"));
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void StrategicPolicy_CollectsNearbyArrowWhenLowOnAmmo()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 0,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 3,
                        position = new Vector2(220f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 220f,
                    verticalDistance = 0f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetInMeleeRange = false,
                    targetInUltimateRange = false,
                    selfHasArrows = false,
                    hasCollectibleProjectile = true,
                    collectibleProjectileDistance = 96f,
                    collectibleProjectileDirection = Vector2.right,
                    shouldCollectProjectile = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "pressure",
                preferredRange = 360,
                shootBias = 0.7f,
                advanceBias = 0.5f,
                meleeBias = 0.2f,
                dashBias = 0.2f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = false,
                punishRecovery = false,
                cornerEscapeBias = 0.2f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "recover ammo",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.debugSummary, Is.EqualTo("AI COLLECT ARROW"));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void HeuristicPolicy_CollectsVerticallyAlignedArrowWithoutHorizontalDrift()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 0,
                    isGrounded = true,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 3,
                        position = new Vector2(0f, 220f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 0f,
                    verticalDistance = 220f,
                    targetDirection = Vector2.up,
                    predictedTargetDirection = Vector2.up,
                    selfHasArrows = false,
                    hasCollectibleProjectile = true,
                    collectibleProjectileDistance = 96f,
                    collectibleProjectileDirection = Vector2.up,
                    shouldCollectProjectile = true,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.debugSummary, Is.EqualTo("AI COLLECT ARROW"));
            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.jumpPressed, Is.True);
            Assert.That(decision.jumpHeld, Is.True);
            Assert.That(decision.shootPressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_CollectsVerticallyAlignedArrowWithoutHorizontalDrift()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 0,
                    isGrounded = true,
                    shootCooldownLeft = 0f,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 3,
                        position = new Vector2(0f, 220f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 0f,
                    verticalDistance = 220f,
                    targetDirection = Vector2.up,
                    predictedTargetDirection = Vector2.up,
                    targetInShootRange = true,
                    selfHasArrows = false,
                    hasCollectibleProjectile = true,
                    collectibleProjectileDistance = 96f,
                    collectibleProjectileDirection = Vector2.up,
                    shouldCollectProjectile = true,
                },
            };

            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "pressure",
                preferredRange = 360,
                shootBias = 0.7f,
                advanceBias = 0.5f,
                meleeBias = 0.2f,
                dashBias = 0.2f,
                jumpBias = 0.1f,
                antiProjectile = "hold",
                antiAir = false,
                punishRecovery = false,
                cornerEscapeBias = 0.2f,
                focusTargetSlot = 2,
                expiresInMs = 400,
                reason = "recover vertical ammo",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.debugSummary, Is.EqualTo("AI COLLECT ARROW"));
            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.jumpPressed, Is.True);
            Assert.That(decision.jumpHeld, Is.True);
            Assert.That(decision.shootPressed, Is.False);
        }

        [Test]
        public void HeuristicPolicy_PrioritizesEvasionOverCollectingWhenThreatIsImminent()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 0,
                    isGrounded = true,
                },
                opponents = new System.Collections.Generic.List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 2,
                        position = new Vector2(240f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 240f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                    shouldCollectProjectile = true,
                    hasCollectibleProjectile = true,
                    collectibleProjectileDistance = 72f,
                    collectibleProjectileDirection = Vector2.right,
                    selfHasArrows = false,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.debugSummary, Is.EqualTo("AI PARRY DASH"));
            Assert.That(decision.dashPrimaryPressed, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.shootPressed, Is.False);
        }

        [Test]
        public void HeuristicPolicy_DashParriesTowardIncomingProjectileInsteadOfTarget()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 1,
                    dashCooldownLeft = 0f,
                    isDashing = false,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 2,
                        position = new Vector2(-240f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 240f,
                    targetDirection = Vector2.left,
                    predictedTargetDirection = Vector2.left,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                    selfHasArrows = true,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.debugSummary, Is.EqualTo("AI PARRY DASH"));
            Assert.That(decision.dashPrimaryPressed, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
        }

        [Test]
        public void HeuristicPolicy_HoldsActiveParryWindowAgainstImminentProjectile()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    canParryProjectile = true,
                    shootCooldownLeft = 0f,
                    meleeCooldownLeft = 0f,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(180f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 180f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    selfHasArrows = true,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.debugSummary, Is.EqualTo("AI PARRY HOLD"));
            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.ultimatePressed, Is.False);
            Assert.That(decision.dashPrimaryPressed, Is.False);
            Assert.That(decision.jumpPressed, Is.False);
        }

        [Test]
        public void HeuristicPolicy_HoldsActiveUltimateProjectileBlockAgainstImminentProjectile()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    canBlockProjectiles = true,
                    dashCooldownLeft = 0f,
                    shootCooldownLeft = 0f,
                    meleeCooldownLeft = 0f,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(180f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 180f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                    selfHasArrows = true,
                },
            };

            AiArenaDecisionEnvelope decision = AiArenaHeuristicPolicy.Decide(snapshot);

            Assert.That(decision.debugSummary, Is.EqualTo("AI PROJECTILE BLOCK"));
            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.ultimatePressed, Is.False);
            Assert.That(decision.dashPrimaryPressed, Is.False);
            Assert.That(decision.jumpPressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_DashAntiProjectileUsesIncomingProjectileDirection()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 1,
                    dashCooldownLeft = 0f,
                    isDashing = false,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 2,
                        position = new Vector2(240f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 240f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.26f,
                    incomingProjectileDirection = Vector2.left,
                    selfHasArrows = true,
                },
            };
            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "stabilize",
                preferredRange = 360,
                antiProjectile = "dash",
                expiresInMs = 400,
                reason = "dash into parry threat",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.dashPrimaryPressed, Is.True);
            Assert.That(decision.moveAxis, Is.GreaterThan(0f));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.meleePressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_HoldsActiveUltimateProjectileBlockDuringProjectileThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    canBlockProjectiles = true,
                    dashCooldownLeft = 0f,
                    shootCooldownLeft = 0f,
                    meleeCooldownLeft = 0f,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(180f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 180f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.12f,
                    incomingProjectileDirection = Vector2.left,
                    shouldDashEvade = true,
                    selfHasArrows = true,
                },
            };
            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "punish",
                preferredRange = 220,
                shootBias = 1f,
                meleeBias = 1f,
                dashBias = 1f,
                antiProjectile = "dash",
                punishRecovery = true,
                expiresInMs = 400,
                reason = "block before punish",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.debugSummary, Is.EqualTo("AI PROJECTILE BLOCK"));
            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.ultimatePressed, Is.False);
            Assert.That(decision.dashPrimaryPressed, Is.False);
            Assert.That(decision.jumpPressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_ParryPreferClearsOffenseDuringProjectileThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    shootCooldownLeft = 0f,
                    meleeCooldownLeft = 0f,
                    dashCooldownLeft = 0f,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(80f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 80f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInMeleeRange = true,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.16f,
                    incomingProjectileDirection = Vector2.left,
                    selfHasArrows = true,
                },
            };
            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "punish",
                preferredRange = 220,
                shootBias = 1f,
                meleeBias = 1f,
                dashBias = 0f,
                antiProjectile = "parry_prefer",
                punishRecovery = true,
                expiresInMs = 400,
                reason = "hold parry before punish",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.moveAxis, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.ultimatePressed, Is.False);
            Assert.That(decision.dashPrimaryPressed, Is.False);
            Assert.That(decision.jumpPressed, Is.False);
        }

        [Test]
        public void StrategicPolicy_JumpAntiProjectileClearsOffenseDuringProjectileThreat()
        {
            var snapshot = new AiArenaSnapshotEnvelope
            {
                schemaVersion = AiArenaSnapshotEnvelope.CurrentSchemaVersion,
                self = new AiArenaCombatantObservation
                {
                    slotId = 1,
                    facing = 1,
                    arrows = 2,
                    isGrounded = true,
                    shootCooldownLeft = 0f,
                    meleeCooldownLeft = 0f,
                    dashCooldownLeft = 0f,
                },
                opponents = new List<AiArenaCombatantObservation>
                {
                    new AiArenaCombatantObservation
                    {
                        slotId = 2,
                        arrows = 1,
                        position = new Vector2(90f, 0f),
                    },
                },
                semantics = new AiArenaSemanticObservation
                {
                    hasTarget = true,
                    targetSlotId = 2,
                    horizontalDistance = 90f,
                    targetDirection = Vector2.right,
                    predictedTargetDirection = Vector2.right,
                    targetInMeleeRange = true,
                    targetInShootRange = true,
                    targetVulnerable = true,
                    shouldPunish = true,
                    incomingProjectileThreat = true,
                    incomingProjectileTime = 0.28f,
                    incomingProjectileDirection = Vector2.left,
                    selfHasArrows = true,
                },
            };
            CodexStrategyIntent intent = new CodexStrategyIntent
            {
                mode = "punish",
                preferredRange = 220,
                shootBias = 1f,
                meleeBias = 1f,
                dashBias = 0f,
                antiProjectile = "jump",
                punishRecovery = true,
                expiresInMs = 400,
                reason = "jump before punish",
            };

            AiArenaDecisionEnvelope decision = AiArenaStrategicPolicy.Decide(snapshot, intent);

            Assert.That(decision.jumpPressed, Is.True);
            Assert.That(decision.jumpHeld, Is.True);
            Assert.That(decision.shootPressed, Is.False);
            Assert.That(decision.shootHeld, Is.False);
            Assert.That(decision.meleePressed, Is.False);
            Assert.That(decision.ultimatePressed, Is.False);
            Assert.That(decision.dashPrimaryPressed, Is.False);
        }

        private sealed class SnapshotSourceController : MonoBehaviour, IAiArenaControllerSnapshotSource
        {
            public int slotId = 1;
            public int currentArrows = 5;
            public bool dead;
            public bool grounded = true;

            public AiArenaControllerSnapshot BuildAiArenaControllerSnapshot(int fallbackSlotId, Vector2 fallbackPosition)
            {
                Vector2 position = transform.position;
                return new AiArenaControllerSnapshot
                {
                    isValid = true,
                    slotId = slotId > 0 ? slotId : fallbackSlotId,
                    displayName = name,
                    isDead = dead,
                    isGrounded = grounded,
                    arrows = currentArrows,
                    facing = position.x >= fallbackPosition.x ? 1 : -1,
                    position = position,
                    velocity = Vector2.zero,
                };
            }
        }

        private sealed class SnapshotSourceArena : MonoBehaviour, IAiArenaArenaSnapshotSource
        {
            public bool roundResetPending;

            public AiArenaArenaSnapshot BuildAiArenaArenaSnapshot()
            {
                return new AiArenaArenaSnapshot
                {
                    wrapBounds = new Rect(-640f, -360f, 1280f, 720f),
                    roundResetPending = roundResetPending,
                    roundsToChampion = 3,
                };
            }
        }

        private static void ClearSnapshotRegistry()
        {
            MethodInfo method = typeof(AiArenaSnapshotSourceRegistry).GetMethod(
                "ClearForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, null);
        }

        private sealed class PlayerController : MonoBehaviour
        {
            public int slotId = 1;
            public int currentArrows = 5;
            public bool dead;
            public bool grounded = true;

            public bool IsDead => dead;
            public bool IsGrounded => grounded;
            public int CurrentArrows => currentArrows;
            public int Facing => 1;
            public float HorizontalVelocity => 0f;
            public float VerticalVelocity => 0f;
            public Vector2 RootPosition => transform.position;
        }
    }
}
