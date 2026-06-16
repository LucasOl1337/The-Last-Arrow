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
                    arrows = 3,
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
            Assert.That(decision.meleePressed, Is.False);
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
