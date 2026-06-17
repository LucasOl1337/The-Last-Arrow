using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Gameplay;
using ProjectPVP.Input;
using ProjectPVP.Match;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class RespawnServiceTests
    {
        private static readonly MethodInfo PlayerAwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlayerContextField =
            typeof(PlayerController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void BuildRespawnCommands_SkipsMissingSlotsAndResolvesSpawnPerSlot()
        {
            GameObject playerOneObject = new GameObject("RespawnServicePlayerOne");
            GameObject playerTwoObject = new GameObject("RespawnServicePlayerTwo");

            try
            {
                PlayerController playerOne = CreatePlayer(playerOneObject);
                PlayerController playerTwo = CreatePlayer(playerTwoObject);
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    null,
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                };

                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    slots,
                    slotId => slotId == CombatantSlotId.SlotTwo ? new Vector2(20f, 30f) : new Vector2(-10f, 5f),
                    applyFreeze: true);

                Assert.That(commands, Has.Count.EqualTo(2));
                Assert.That(commands[0].SlotId, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(commands[0].SpawnPoint, Is.EqualTo(new Vector2(-10f, 5f)));
                Assert.That(commands[0].ControlLocked, Is.True);
                Assert.That(commands[1].SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                Assert.That(commands[1].SpawnPoint, Is.EqualTo(new Vector2(20f, 30f)));
                Assert.That(commands[1].ControlLocked, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
            }
        }

        [Test]
        public void BuildRespawnCommands_DeduplicatesRawSlotIds()
        {
            GameObject playerOneObject = new GameObject("RespawnServiceDedupPlayerOne");
            GameObject playerTwoObject = new GameObject("RespawnServiceDedupPlayerTwo");

            try
            {
                PlayerController playerOne = CreatePlayer(playerOneObject);
                PlayerController playerTwo = CreatePlayer(playerTwoObject);
                List<CombatantSlotConfig> slots = new List<CombatantSlotConfig>
                {
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotOne, controller = playerOne },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                    new CombatantSlotConfig { slotId = CombatantSlotId.SlotTwo, controller = playerTwo },
                };

                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    slots,
                    slotId => slotId == CombatantSlotId.SlotTwo ? new Vector2(20f, 30f) : new Vector2(-10f, 5f),
                    applyFreeze: true);

                Assert.That(commands, Has.Count.EqualTo(2));
                Assert.That(commands[0].SlotId, Is.EqualTo(CombatantSlotId.SlotOne));
                Assert.That(commands[1].SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
            }
            finally
            {
                Object.DestroyImmediate(playerOneObject);
                Object.DestroyImmediate(playerTwoObject);
            }
        }

        [Test]
        public void ApplyRespawnCommands_AppliesSlotSelectionLockAndCallback()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject playerObject = new GameObject("RespawnServiceApplyPlayer");

            try
            {
                PlayerController player = CreatePlayer(playerObject);
                PlayerAwakeMethod.Invoke(player, null);
                CombatantSlotConfig slot = new CombatantSlotConfig
                {
                    slotId = CombatantSlotId.SlotTwo,
                    controller = player,
                };
                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    new[] { slot },
                    _ => new Vector2(48f, 96f),
                    applyFreeze: true);

                int callbackCount = 0;
                int appliedCount = RespawnService.ApplyRespawnCommands(
                    commands,
                    command =>
                    {
                        Assert.That(command.SlotId, Is.EqualTo(CombatantSlotId.SlotTwo));
                        callbackCount += 1;
                    });

                Assert.That(appliedCount, Is.EqualTo(1));
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(player.slotId, Is.EqualTo(2));
                Assert.That(player.IsExternallyControlLocked, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void ApplyRespawnCommands_RespawnsPreviouslyDeadPlayerWithoutBurstDamage()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject respawningObject = new GameObject("RespawnServiceBurstRespawningPlayer");
            GameObject enemyObject = new GameObject("RespawnServiceBurstEnemy");

            try
            {
                PlayerController respawningPlayer = CreatePlayer(respawningObject);
                PlayerController enemyPlayer = CreatePlayer(enemyObject);

                respawningObject.transform.position = Vector3.zero;
                enemyObject.transform.position = new Vector3(8f, 0f, 0f);
                respawningPlayer.body.position = Vector2.zero;
                enemyPlayer.body.position = new Vector2(8f, 0f);

                PlayerAwakeMethod.Invoke(respawningPlayer, null);
                PlayerAwakeMethod.Invoke(enemyPlayer, null);

                respawningPlayer.TryKill();

                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    new[]
                    {
                        new CombatantSlotConfig
                        {
                            slotId = CombatantSlotId.SlotOne,
                            controller = respawningPlayer,
                        },
                    },
                    _ => Vector2.zero,
                    applyFreeze: true);

                int appliedCount = RespawnService.ApplyRespawnCommands(commands);

                Assert.That(appliedCount, Is.EqualTo(1));
                Assert.That(respawningPlayer.IsDead, Is.False);
                Assert.That(respawningPlayer.IsExternallyControlLocked, Is.True);
                Assert.That(enemyPlayer.IsDead, Is.False);
                Assert.That(enemyPlayer.IsHitStunned, Is.False);
                Assert.That(enemyPlayer.IsKnockedBack, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(respawningObject);
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void ApplyRespawnCommands_RespawnsLivingPlayerWithoutAffectingOtherPlayers()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject respawningObject = new GameObject("RespawnServiceNoBurstRespawningPlayer");
            GameObject enemyObject = new GameObject("RespawnServiceNoBurstEnemy");

            try
            {
                PlayerController respawningPlayer = CreatePlayer(respawningObject);
                PlayerController enemyPlayer = CreatePlayer(enemyObject);

                respawningObject.transform.position = Vector3.zero;
                enemyObject.transform.position = new Vector3(8f, 0f, 0f);
                respawningPlayer.body.position = Vector2.zero;
                enemyPlayer.body.position = new Vector2(8f, 0f);

                PlayerAwakeMethod.Invoke(respawningPlayer, null);
                PlayerAwakeMethod.Invoke(enemyPlayer, null);

                List<RespawnSlotCommand> commands = RespawnService.BuildRespawnCommands(
                    new[]
                    {
                        new CombatantSlotConfig
                        {
                            slotId = CombatantSlotId.SlotOne,
                            controller = respawningPlayer,
                        },
                    },
                    _ => Vector2.zero,
                    applyFreeze: true);

                int appliedCount = RespawnService.ApplyRespawnCommands(commands);

                Assert.That(appliedCount, Is.EqualTo(1));
                Assert.That(respawningPlayer.IsDead, Is.False);
                Assert.That(respawningPlayer.IsExternallyControlLocked, Is.True);
                Assert.That(enemyPlayer.IsDead, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(respawningObject);
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void SetExternalControlLock_DisablesAndRestoresInputSourceEnabledState()
        {
            GameObject root = new GameObject("RespawnServiceInputLockPlayer");

            try
            {
                PlayerController player = CreatePlayer(root);
                LockAwareInputSource inputSource = root.AddComponent<LockAwareInputSource>();
                player.inputSource = inputSource;

                Assert.That(inputSource.enabled, Is.True);

                player.SetExternalControlLock(true);

                Assert.That(player.IsExternallyControlLocked, Is.True);
                Assert.That(inputSource.enabled, Is.False);

                player.SetExternalControlLock(false);

                Assert.That(player.IsExternallyControlLocked, Is.False);
                Assert.That(inputSource.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetExternalControlLock_ClearsActiveCombatAndBufferedState()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(PlayerContextField, Is.Not.Null);

            GameObject root = new GameObject("RespawnServiceActionLockPlayer");

            try
            {
                PlayerController player = CreatePlayer(root);
                PlayerAwakeMethod.Invoke(player, null);

                PlayerContext context = (PlayerContext)PlayerContextField.GetValue(player);
                context.currentInputFrame = new PlayerInputFrame
                {
                    jumpPressed = true,
                    shootHeld = true,
                    dashPrimaryPressed = true,
                    meleePressed = true,
                    ultimatePressed = true,
                };
                context.aimHoldActive = true;
                context.shootHeldLastFrame = true;
                context.dashTimeLeft = 0.12f;
                context.dashVelocity = new Vector2(400f, 80f);
                context.lastDashVelocity = new Vector2(400f, 80f);
                context.dashParryTimer = 0.2f;
                context.dashPressTimer = 0.2f;
                context.dashComboWindowLeft = 0.15f;
                context.pendingDashPrimary = true;
                context.pendingDashSecondary = true;
                context.meleeTimeLeft = 0.18f;
                context.ultimateTimeLeft = 0.35f;
                context.ultimateTotalDuration = 0.35f;
                context.ultimateDashTimeLeft = 0.16f;
                context.ultimateDashVelocity = new Vector2(-300f, 0f);
                context.lastUltimateDashVelocity = new Vector2(-300f, 0f);
                context.ultimateProjectileBlockTimer = 0.22f;
                context.jumpStartTimeLeft = 0.1f;
                context.dashAnimationHoldTimeLeft = 0.2f;
                context.meleeAnimationTimeLeft = 0.2f;
                context.shootAnimationTimeLeft = 0.2f;
                context.ultimateAnimationTimeLeft = 0.2f;
                context.jumpBufferLeft = 0.12f;
                context.coyoteTimeLeft = 0.12f;
                context.wallJumpGraceTimer = 0.12f;
                context.wallDetachIgnoreTimer = 0.12f;
                context.isTouchingWall = true;
                context.wallNormal = Vector2.left;
                context.actionLockEntries.Add(new ActionLockEntry
                {
                    action = "dash",
                    remaining = 0.1f,
                    cancelable = true,
                });
                player.body.linearVelocity = new Vector2(120f, 40f);

                player.SetExternalControlLock(true);

                Assert.That(player.IsExternallyControlLocked, Is.True);
                Assert.That(player.CurrentInputFrame, Is.EqualTo(default(PlayerInputFrame)));
                Assert.That(player.CurrentVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(player.IsAimHoldActive, Is.False);
                Assert.That(player.IsDashing, Is.False);
                Assert.That(player.IsDashAnimationActive, Is.False);
                Assert.That(player.IsMeleeActive, Is.False);
                Assert.That(player.IsShootAnimating, Is.False);
                Assert.That(player.IsJumpStartActive, Is.False);
                Assert.That(player.IsUltimateActive, Is.False);
                Assert.That(player.IsDodgeInvulnerable, Is.False);
                Assert.That(player.UltimateProjectileBlockTimeLeft, Is.Zero);
                Assert.That(context.pendingDashPrimary, Is.False);
                Assert.That(context.pendingDashSecondary, Is.False);
                Assert.That(context.jumpBufferLeft, Is.Zero);
                Assert.That(context.coyoteTimeLeft, Is.Zero);
                Assert.That(context.wallJumpGraceTimer, Is.Zero);
                Assert.That(context.wallDetachIgnoreTimer, Is.Zero);
                Assert.That(context.isTouchingWall, Is.False);
                Assert.That(context.wallNormal, Is.EqualTo(Vector2.zero));
                Assert.That(context.dashComboWindowLeft, Is.Zero);
                Assert.That(context.dashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.lastDashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.ultimateDashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.lastUltimateDashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.actionLockEntries, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetExternalControlLock_ReappliesLockedStateWhenAlreadyLocked()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);
            Assert.That(PlayerContextField, Is.Not.Null);

            GameObject root = new GameObject("RespawnServiceRepeatedActionLockPlayer");

            try
            {
                PlayerController player = CreatePlayer(root);
                PlayerAwakeMethod.Invoke(player, null);
                player.SetExternalControlLock(true);

                PlayerContext context = (PlayerContext)PlayerContextField.GetValue(player);
                context.currentInputFrame = new PlayerInputFrame
                {
                    jumpPressed = true,
                    dashPrimaryPressed = true,
                };
                context.dashTimeLeft = 0.12f;
                context.dashVelocity = new Vector2(300f, 0f);
                context.lastDashVelocity = new Vector2(300f, 0f);
                context.dashParryTimer = 0.2f;
                context.pendingDashPrimary = true;
                context.jumpBufferLeft = 0.12f;
                player.body.linearVelocity = new Vector2(90f, 0f);

                player.SetExternalControlLock(true);

                Assert.That(player.IsExternallyControlLocked, Is.True);
                Assert.That(player.CurrentInputFrame, Is.EqualTo(default(PlayerInputFrame)));
                Assert.That(player.CurrentVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(player.IsDashing, Is.False);
                Assert.That(player.IsDodgeInvulnerable, Is.False);
                Assert.That(context.pendingDashPrimary, Is.False);
                Assert.That(context.jumpBufferLeft, Is.Zero);
                Assert.That(context.dashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.lastDashVelocity, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnEnable_RespectsExternalControlLockAfterReconfiguringInput()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject root = new GameObject("RespawnServiceOnEnableLockPlayer");

            try
            {
                PlayerController player = CreatePlayer(root);
                LockAwareInputSource inputSource = root.AddComponent<LockAwareInputSource>();
                player.inputSource = inputSource;

                PlayerAwakeMethod.Invoke(player, null);
                player.SetExternalControlLock(true);

                MethodInfo onEnable = typeof(PlayerController).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onEnable, Is.Not.Null);

                onEnable.Invoke(player, null);

                Assert.That(player.IsExternallyControlLocked, Is.True);
                Assert.That(inputSource.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnDisable_ResetsInputStateToPreventStaleHeldInputs()
        {
            Assert.That(PlayerAwakeMethod, Is.Not.Null);

            GameObject root = new GameObject("RespawnServiceOnDisableResetPlayer");

            try
            {
                PlayerController player = CreatePlayer(root);
                ResetTrackingInputSource inputSource = root.AddComponent<ResetTrackingInputSource>();
                player.inputSource = inputSource;

                PlayerAwakeMethod.Invoke(player, null);
                player.SetExternalControlLock(false);

                inputSource.SetBufferedInput();
                Assert.That(inputSource.ResetCount, Is.Zero);

                player.enabled = false;

                Assert.That(inputSource.ResetCount, Is.EqualTo(1));
                Assert.That(inputSource.BufferedInputCleared, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static PlayerController CreatePlayer(GameObject root)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            PlayerController controller = root.AddComponent<PlayerController>();
            controller.body = body;
            controller.bodyCollider = collider;
            return controller;
        }

        private sealed class LockAwareInputSource : MonoBehaviour, ICombatantInputSource
        {
            public PlayerInputFrame CurrentFrame => default;
            public int ActiveGamepadSlot => -1;
            public string FaceButtonDebug => "LockAware";

            public void CaptureFrame()
            {
            }

            public void ConfigureForSlot(CombatantSlotId slotId)
            {
            }

            public void ResetInputState()
            {
            }
        }

        private sealed class ResetTrackingInputSource : MonoBehaviour, ICombatantInputSource
        {
            private bool _bufferedInput;

            public int ResetCount { get; private set; }
            public bool BufferedInputCleared => !_bufferedInput;
            public PlayerInputFrame CurrentFrame => default;
            public int ActiveGamepadSlot => -1;
            public string FaceButtonDebug => "ResetTracking";

            public void CaptureFrame()
            {
            }

            public void ConfigureForSlot(CombatantSlotId slotId)
            {
            }

            public void ResetInputState()
            {
                ResetCount += 1;
                _bufferedInput = false;
            }

            public void SetBufferedInput()
            {
                _bufferedInput = true;
            }
        }
    }
}
