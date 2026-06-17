using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Input;
using ProjectPVP.Gameplay;
using ProjectPVP.Presentation;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class PlayerCombatSystemTests
    {
        private static readonly MethodInfo AwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo JumpSystemField =
            typeof(PlayerController).GetField("_jumpSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CombatSystemField =
            typeof(PlayerController).GetField("_combatSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DashSystemField =
            typeof(PlayerController).GetField("_dashSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MovementSystemField =
            typeof(PlayerController).GetField("_movementSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ContextField =
            typeof(PlayerController).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DeathNotifyRoutineField =
            typeof(PlayerController).GetField("_deathNotifyRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ProjectileOnTriggerEnter2DMethod =
            typeof(ProjectileController).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo HandleArrowTransferOnContactMethod =
            typeof(PlayerCombatSystem).GetMethod("HandleArrowTransferOnContact", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo RegisterActivePlayerMethod =
            typeof(PlayerController).GetMethod("RegisterActivePlayer", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo UnregisterActivePlayerMethod =
            typeof(PlayerController).GetMethod("UnregisterActivePlayer", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearActivePlayersForTestsMethod =
            typeof(PlayerController).GetMethod("ClearActivePlayersForTests", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterActiveProjectileMethod =
            typeof(ProjectileController).GetMethod("RegisterActiveProjectile", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo UnregisterActiveProjectileMethod =
            typeof(ProjectileController).GetMethod("UnregisterActiveProjectile", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearActiveProjectilesForTestsMethod =
            typeof(ProjectileController).GetMethod("ClearActiveProjectilesForTests", BindingFlags.Static | BindingFlags.NonPublic);

        [TearDown]
        public void TearDown()
        {
            DestroyKillImpactFxForTests();
            DestroyAttackCueFxForTests();
            DestroyParryCueFxForTests();
        }

        [Test]
        public void ActivePlayers_CopyDeduplicatesAndUnregisters()
        {
            Assert.That(RegisterActivePlayerMethod, Is.Not.Null);
            Assert.That(UnregisterActivePlayerMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject root = new GameObject("active_registry_player");
            PlayerController player = root.AddComponent<PlayerController>();
            var players = new List<PlayerController>();

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                RegisterActivePlayerMethod.Invoke(null, new object[] { player });
                RegisterActivePlayerMethod.Invoke(null, new object[] { player });

                PlayerController.CopyActivePlayers(players);

                Assert.That(players, Has.Count.EqualTo(1));
                Assert.That(players[0], Is.SameAs(player));

                UnregisterActivePlayerMethod.Invoke(null, new object[] { player });
                PlayerController.CopyActivePlayers(players);

                Assert.That(players, Is.Empty);
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActiveProjectiles_CopyDeduplicatesAndUnregisters()
        {
            Assert.That(RegisterActiveProjectileMethod, Is.Not.Null);
            Assert.That(UnregisterActiveProjectileMethod, Is.Not.Null);
            Assert.That(ClearActiveProjectilesForTestsMethod, Is.Not.Null);

            GameObject root = new GameObject("active_registry_projectile");
            ProjectileController projectile = root.AddComponent<ProjectileController>();
            var projectiles = new List<ProjectileController>();

            try
            {
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                RegisterActiveProjectileMethod.Invoke(null, new object[] { projectile });
                RegisterActiveProjectileMethod.Invoke(null, new object[] { projectile });

                ProjectileController.CopyActiveProjectiles(projectiles);

                Assert.That(projectiles, Has.Count.EqualTo(1));
                Assert.That(projectiles[0], Is.SameAs(projectile));

                UnregisterActiveProjectileMethod.Invoke(null, new object[] { projectile });
                ProjectileController.CopyActiveProjectiles(projectiles);

                Assert.That(projectiles, Is.Empty);
            }
            finally
            {
                ClearActiveProjectilesForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PublicRuntimeProperties_ReturnSafeDefaultsBeforeAwake()
        {
            GameObject root = new GameObject("uninitialized_player_defaults");
            root.SetActive(false);
            PlayerController player = root.AddComponent<PlayerController>();

            try
            {
                Assert.That(player.IsDead, Is.False);
                Assert.That(player.IsGrounded, Is.False);
                Assert.That(player.IsDashing, Is.False);
                Assert.That(player.CurrentArrows, Is.Zero);
                Assert.That(player.CurrentVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(player.AimHoldDirection, Is.EqualTo(Vector2.zero));
                Assert.That(player.ResolvedUltimateDashDistance, Is.Zero);
                Assert.That(player.ResolvedUltimateDashDuration, Is.Zero);
                Assert.That(player.RuntimeContext.Controller, Is.SameAs(player));

                Assert.DoesNotThrow(() => _ = player.CurrentInputFrame);
                Assert.DoesNotThrow(() => _ = player.BuildAiArenaControllerSnapshot(1, Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HandleIncomingProjectile_DoesNotThrowWhenContextHasNoController()
        {
            GameObject ownerRoot = new GameObject("controllerless_context_owner");
            GameObject sourceRoot = new GameObject("controllerless_projectile_source");
            GameObject projectileRoot = new GameObject("controllerless_projectile");

            try
            {
                PlayerContext context = new PlayerContext
                {
                    transform = ownerRoot.transform,
                    Controller = null,
                    arrows = 3,
                };
                PlayerStatResolver statResolver = new PlayerStatResolver(context);
                PlayerAnchorSystem anchorSystem = new PlayerAnchorSystem(context, statResolver);
                PlayerActionLockSystem actionLockSystem = new PlayerActionLockSystem(context, statResolver);
                PlayerCombatSystem combatSystem = new PlayerCombatSystem(context, statResolver, anchorSystem, actionLockSystem);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();
                projectile.Launch(
                    sourceRoot,
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

                bool handled = false;
                Assert.DoesNotThrow(() => handled = combatSystem.HandleIncomingProjectile(projectile));
                Assert.That(handled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(sourceRoot);
                Object.DestroyImmediate(ownerRoot);
            }
        }

        [Test]
        public void ApplyEliminationHits_KillsTargetWithoutLeavingHitReactionState()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("ultimate_attacker");
            GameObject targetRoot = new GameObject("ultimate_target");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                BoxCollider2D targetCollider = target.bodyCollider;

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                InvokeAwake(attacker);
                InvokeAwake(target);

                attacker.ApplyEliminationHits(new Collider2D[] { targetCollider }, 1);

                Assert.That(target.IsDead, Is.True);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(target.LastFatalHitSource, Is.SameAs(attacker));
                Assert.That(target.LastFatalHitCause, Is.EqualTo("Ultimate"));
                Assert.That(target.LastFatalHitSummary, Is.EqualTo(attacker.BotDisplayName + " via Ultimate"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void ApplyEliminationHits_DoesNotKillTargetDuringDodgeWindow()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("ultimate_attacker_dodge");
            GameObject targetRoot = new GameObject("ultimate_target_dodge");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                BoxCollider2D targetCollider = target.bodyCollider;

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                InvokeAwake(attacker);
                InvokeAwake(target);

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.dashParryTimer = 0.2f;

                attacker.ApplyEliminationHits(new Collider2D[] { targetCollider }, 1);

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(target.LastFatalHitSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void ApplyEliminationHits_DeduplicatesTargetBeforeShieldCanBeConsumedTwice()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("ultimate_attacker_duplicate_target");
            GameObject targetRoot = new GameObject("ultimate_shield_duplicate_target");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                BoxCollider2D targetCollider = target.bodyCollider;

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                InvokeAwake(attacker);
                InvokeAwake(target);
                target.SetRoundShield(true);

                attacker.ApplyEliminationHits(new Collider2D[] { targetCollider, targetCollider }, 2);

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HasShield, Is.False);
                Assert.That(target.LastFatalHitSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_KillsTargetWithoutLeavingHitReactionState()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("projectile_attacker");
            GameObject targetRoot = new GameObject("projectile_target");
            GameObject projectileRoot = new GameObject("projectile");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.True);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(target.LastFatalHitSource, Is.SameAs(attacker));
                Assert.That(target.LastFatalHitCause, Is.EqualTo("Projectile"));
                Assert.That(target.LastFatalHitSummary, Is.EqualTo(attacker.BotDisplayName + " via Projectile"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_IgnoresProjectileFromPlayerChildSource()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject playerRoot = new GameObject("projectile_self_child_source_player");
            GameObject sourceChild = new GameObject("projectile_self_child_source_anchor");
            GameObject projectileRoot = new GameObject("projectile_self_child_source");

            try
            {
                PlayerController player = CreatePlayer(playerRoot, 1, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();
                sourceChild.transform.SetParent(playerRoot.transform, false);

                projectile.Launch(
                    sourceChild,
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

                InvokeAwake(player);

                bool handled = player.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.False);
                Assert.That(player.IsDead, Is.False);
                Assert.That(projectile.IsStuck, Is.False);
                Assert.That(projectile.SourceObject, Is.SameAs(sourceChild));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(sourceChild);
                Object.DestroyImmediate(playerRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_KillsTargetEvenWhenProjectileStatsAreConfigured()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("projectile_attacker_tuned");
            GameObject targetRoot = new GameObject("projectile_target_tuned");
            GameObject projectileRoot = new GameObject("projectile_tuned");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                definition.projectileKnockbackForce = 500f;

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.True);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(target.LastFatalHitSource, Is.SameAs(attacker));
                Assert.That(target.LastFatalHitCause, Is.EqualTo("Projectile"));
                Assert.That(target.LastFatalHitSummary, Is.EqualTo(attacker.BotDisplayName + " via Projectile"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_LeavesProjectileCollectibleForOtherPlayers()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("projectile_attacker_collectible");
            GameObject targetRoot = new GameObject("projectile_target_collectible");
            GameObject collectorRoot = new GameObject("projectile_collector_collectible");
            GameObject projectileRoot = new GameObject("projectile_collectible");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                PlayerController collector = CreatePlayer(collectorRoot, 3, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                collectorRoot.transform.position = new Vector3(4f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);
                collector.body.position = new Vector2(4f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);
                InvokeAwake(collector);

                PlayerContext collectorContext = (PlayerContext)ContextField.GetValue(collector);
                collectorContext.arrows = 2;

                bool handled = target.HandleIncomingProjectile(projectile);
                Assert.That(handled, Is.True);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(projectile.SourceObject, Is.Null);

                bool collected = collector.TryCollectProjectile(projectile);

                Assert.That(collected, Is.True);
                Assert.That(collector.CurrentArrows, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
                Object.DestroyImmediate(collectorRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_ConsumesShieldAndPreventsFatalHit()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("shield_attacker");
            GameObject targetRoot = new GameObject("shield_target");
            GameObject projectileRoot = new GameObject("shield_projectile");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                target.SetRoundShield(true);

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HasShield, Is.False);
                Assert.That(target.LastFatalHitSource, Is.Null);
                Assert.That(target.LastFatalHitCause, Is.Empty);
                Assert.That(target.LastFatalHitSummary, Is.Empty);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsDisarmed, Is.False);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(projectile.SourceObject, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_SticksProjectileDuringUltimateProjectileBlock()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject attackerRoot = new GameObject("ultimate_block_attacker");
            GameObject targetRoot = new GameObject("ultimate_block_target");
            GameObject projectileRoot = new GameObject("ultimate_block_projectile");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.ultimateProjectileBlockTimer = 0.2f;

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.False);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsDisarmed, Is.False);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(projectile.SourceObject, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void SeveredProjectileBecomesCollectibleWhenItHitsTheWorld()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ProjectileOnTriggerEnter2DMethod, Is.Not.Null);

            GameObject attackerRoot = new GameObject("sever_attacker");
            GameObject projectileRoot = new GameObject("sever_projectile");
            GameObject worldRoot = new GameObject("sever_world");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();
                BoxCollider2D worldCollider = worldRoot.AddComponent<BoxCollider2D>();

                attackerRoot.transform.position = Vector3.zero;
                attacker.body.position = Vector2.zero;
                worldRoot.transform.position = new Vector3(4f, -2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);

                projectile.SeverByMelee();
                ProjectileOnTriggerEnter2DMethod.Invoke(projectile, new object[] { worldCollider });

                Assert.That(projectile.IsDisarmed, Is.True);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsCollectible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(worldRoot);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_ParriesProjectileByReflectingIt()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ProjectileOnTriggerEnter2DMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);
            DestroyParryCueFxForTests();

            GameObject attackerRoot = new GameObject("projectile_attacker_parry");
            GameObject targetRoot = new GameObject("projectile_target_parry");
            GameObject projectileRoot = new GameObject("projectile_parry");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.arrows = 2;
                targetContext.dashParryTimer = 0.2f;

                ProjectileOnTriggerEnter2DMethod.Invoke(projectile, new object[] { target.bodyCollider });

                Assert.That(target.CurrentArrows, Is.EqualTo(3));
                Assert.That(projectile.SourceObject, Is.SameAs(targetRoot));
                Assert.That(projectile.IsStuck, Is.False);
                Assert.That(projectile.IsDisarmed, Is.False);
                Assert.That(projectile.IsCollectible, Is.False);
                Assert.That(projectile.IsParried, Is.False);
                Assert.That(projectile.CurrentVelocity.x, Is.LessThan(0f));

                ProjectPvpParryCueFx[] effects = Object.FindObjectsByType<ProjectPvpParryCueFx>(FindObjectsSortMode.None);
                Assert.That(effects, Has.Length.EqualTo(1));
                Assert.That((Vector2)effects[0].transform.position, Is.EqualTo(target.RootPosition));
                Assert.That(effects[0].Duration, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_ShieldPreventsParryAndConsumesShield()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);
            DestroyParryCueFxForTests();

            GameObject attackerRoot = new GameObject("projectile_attacker_shield_parry");
            GameObject targetRoot = new GameObject("projectile_target_shield_parry");
            GameObject projectileRoot = new GameObject("projectile_shield_parry");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.dashParryTimer = 0.2f;
                target.SetRoundShield(true);

                Assert.That(target.CanParryProjectile, Is.False);

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HasShield, Is.False);
                Assert.That(target.CurrentArrows, Is.EqualTo(3));
                Assert.That(projectile.SourceObject, Is.Null);
                Assert.That(projectile.IsParried, Is.False);
                Assert.That(projectile.IsStuck, Is.True);
                Assert.That(projectile.IsCollectible, Is.True);
                Assert.That(Object.FindObjectsByType<ProjectPvpParryCueFx>(FindObjectsSortMode.None), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_PassesLastArrowWhenPlayersBump()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_player");
            GameObject poorRoot = new GameObject("arrow_poor_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController richPlayer = CreatePlayer(richRoot, 1, null);
                PlayerController poorPlayer = CreatePlayer(poorRoot, 2, null);

                richRoot.transform.position = Vector3.zero;
                poorRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                poorPlayer.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(poorPlayer);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext poorContext = (PlayerContext)ContextField.GetValue(poorPlayer);
                richContext.arrows = 3;
                poorContext.arrows = 1;

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.True);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(2));
                Assert.That(poorPlayer.CurrentArrows, Is.EqualTo(2));
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(poorRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_DoesNotPassArrowToShieldedPlayer()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_shielded_player");
            GameObject poorRoot = new GameObject("arrow_poor_shielded_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController richPlayer = CreatePlayer(richRoot, 1, null);
                PlayerController poorPlayer = CreatePlayer(poorRoot, 2, null);

                richRoot.transform.position = Vector3.zero;
                poorRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                poorPlayer.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(poorPlayer);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext poorContext = (PlayerContext)ContextField.GetValue(poorPlayer);
                richContext.arrows = 3;
                poorContext.arrows = 1;
                poorPlayer.SetRoundShield(true);

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.False);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(3));
                Assert.That(poorPlayer.CurrentArrows, Is.EqualTo(1));
                Assert.That(poorPlayer.HasShield, Is.True);
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(poorRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_DoesNotPassArrowToDodgingPlayer()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_dodging_player");
            GameObject poorRoot = new GameObject("arrow_poor_dodging_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController richPlayer = CreatePlayer(richRoot, 1, null);
                PlayerController poorPlayer = CreatePlayer(poorRoot, 2, null);

                richRoot.transform.position = Vector3.zero;
                poorRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                poorPlayer.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(poorPlayer);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext poorContext = (PlayerContext)ContextField.GetValue(poorPlayer);
                richContext.arrows = 3;
                poorContext.arrows = 1;
                poorContext.dashParryTimer = 0.2f;

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.False);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(3));
                Assert.That(poorPlayer.CurrentArrows, Is.EqualTo(1));
                Assert.That(poorPlayer.IsDodgeInvulnerable, Is.True);
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(poorRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_DoesNotWasteArrowOnFullTarget()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_full_target_player");
            GameObject poorRoot = new GameObject("arrow_full_target_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController richPlayer = CreatePlayer(richRoot, 1, null);
                PlayerController poorPlayer = CreatePlayer(poorRoot, 2, null);

                richRoot.transform.position = Vector3.zero;
                poorRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                poorPlayer.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(poorPlayer);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext poorContext = (PlayerContext)ContextField.GetValue(poorPlayer);
                richContext.arrows = 4;
                poorContext.arrows = 3;

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.False);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(4));
                Assert.That(poorPlayer.CurrentArrows, Is.EqualTo(3));
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(poorRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_UsesTargetArrowCapacityWhenCheckingIfTargetIsFull()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_high_capacity_player");
            GameObject fullTargetRoot = new GameObject("arrow_low_capacity_full_target");
            CharacterDefinition richDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition targetDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                richDefinition.maxArrows = 4;
                targetDefinition.maxArrows = 3;
                PlayerController richPlayer = CreatePlayer(richRoot, 1, richDefinition);
                PlayerController fullTarget = CreatePlayer(fullTargetRoot, 2, targetDefinition);

                richRoot.transform.position = Vector3.zero;
                fullTargetRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                fullTarget.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(fullTarget);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(fullTarget);
                richContext.arrows = 4;
                targetContext.arrows = 3;

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.False);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(4));
                Assert.That(fullTarget.CurrentArrows, Is.EqualTo(3));
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richDefinition);
                Object.DestroyImmediate(targetDefinition);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(fullTargetRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_RequiresAtLeastOneArrowLead()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject richRoot = new GameObject("arrow_rich_gap_one_player");
            GameObject poorRoot = new GameObject("arrow_poor_gap_one_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController richPlayer = CreatePlayer(richRoot, 1, null);
                PlayerController poorPlayer = CreatePlayer(poorRoot, 2, null);

                richRoot.transform.position = Vector3.zero;
                poorRoot.transform.position = Vector3.zero;
                richPlayer.body.position = Vector2.zero;
                poorPlayer.body.position = Vector2.zero;

                InvokeAwake(richPlayer);
                InvokeAwake(poorPlayer);
                Physics2D.SyncTransforms();

                PlayerContext richContext = (PlayerContext)ContextField.GetValue(richPlayer);
                PlayerContext poorContext = (PlayerContext)ContextField.GetValue(poorPlayer);
                richContext.arrows = 2;
                poorContext.arrows = 1;

                PlayerCombatSystem richCombat = (PlayerCombatSystem)CombatSystemField.GetValue(richPlayer);
                bool transferred = (bool)HandleArrowTransferOnContactMethod.Invoke(richCombat, null);

                Assert.That(transferred, Is.True);
                Assert.That(richPlayer.CurrentArrows, Is.EqualTo(1));
                Assert.That(poorPlayer.CurrentArrows, Is.EqualTo(2));
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(richRoot);
                Object.DestroyImmediate(poorRoot);
            }
        }

        [Test]
        public void HandleArrowTransferOnContact_DoesNotBounceArrowBackDuringSameContact()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(HandleArrowTransferOnContactMethod, Is.Not.Null);
            Assert.That(ClearActivePlayersForTestsMethod, Is.Not.Null);

            GameObject firstRoot = new GameObject("arrow_bounce_first_player");
            GameObject secondRoot = new GameObject("arrow_bounce_second_player");

            try
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);

                PlayerController firstPlayer = CreatePlayer(firstRoot, 1, null);
                PlayerController secondPlayer = CreatePlayer(secondRoot, 2, null);

                firstRoot.transform.position = Vector3.zero;
                secondRoot.transform.position = Vector3.zero;
                firstPlayer.body.position = Vector2.zero;
                secondPlayer.body.position = Vector2.zero;

                InvokeAwake(firstPlayer);
                InvokeAwake(secondPlayer);
                Physics2D.SyncTransforms();

                PlayerContext firstContext = (PlayerContext)ContextField.GetValue(firstPlayer);
                PlayerContext secondContext = (PlayerContext)ContextField.GetValue(secondPlayer);
                firstContext.arrows = 2;
                secondContext.arrows = 1;

                PlayerCombatSystem firstCombat = (PlayerCombatSystem)CombatSystemField.GetValue(firstPlayer);
                PlayerCombatSystem secondCombat = (PlayerCombatSystem)CombatSystemField.GetValue(secondPlayer);

                bool firstTransfer = (bool)HandleArrowTransferOnContactMethod.Invoke(firstCombat, null);
                bool immediateReturnTransfer = (bool)HandleArrowTransferOnContactMethod.Invoke(secondCombat, null);

                Assert.That(firstTransfer, Is.True);
                Assert.That(immediateReturnTransfer, Is.False);
                Assert.That(firstPlayer.CurrentArrows, Is.EqualTo(1));
                Assert.That(secondPlayer.CurrentArrows, Is.EqualTo(2));
            }
            finally
            {
                ClearActivePlayersForTestsMethod.Invoke(null, null);
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_ParriesWhenOnlyDashPressBufferIsSet()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject attackerRoot = new GameObject("projectile_attacker_no_press_parry");
            GameObject targetRoot = new GameObject("projectile_target_no_press_parry");
            GameObject projectileRoot = new GameObject("projectile_no_press_parry");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(2f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(2f, 0f);

                projectile.Launch(
                    attackerRoot,
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

                InvokeAwake(attacker);
                InvokeAwake(target);

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.arrows = 3;
                targetContext.dashPressTimer = 0.2f;
                targetContext.dashParryTimer = 0f;

                bool handled = target.HandleIncomingProjectile(projectile);

                Assert.That(handled, Is.True);
                Assert.That(target.IsDead, Is.False);
                Assert.That(target.CurrentArrows, Is.EqualTo(3));
                Assert.That(target.DashPressTimeLeft, Is.EqualTo(0f));
                Assert.That(projectile.IsStuck, Is.False);
                Assert.That(projectile.IsCollectible, Is.False);
                Assert.That(projectile.SourceObject, Is.SameAs(targetRoot));
                Assert.That(projectile.IsParried, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void TryCheckHeadStomp_KillsTargetAndBouncesStomper()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);

            GameObject stomperRoot = new GameObject("stomper");
            GameObject targetRoot = new GameObject("stomp_target");
            CharacterDefinition stomperDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition targetDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                stomperDefinition.colliderSize = new Vector2(20f, 20f);
                targetDefinition.colliderSize = new Vector2(20f, 20f);

                PlayerController stomper = CreatePlayer(stomperRoot, 1, stomperDefinition);
                PlayerController target = CreatePlayer(targetRoot, 2, targetDefinition);

                stomperRoot.transform.position = new Vector3(0f, 15f, 0f);
                targetRoot.transform.position = Vector3.zero;
                stomper.body.position = new Vector2(0f, 15f);
                target.body.position = Vector2.zero;
                stomper.body.linearVelocity = new Vector2(0f, -100f);

                InvokeAwake(stomper);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(stomper);
                jumpSystem.TryCheckHeadStomp();

                Assert.That(target.IsDead, Is.True);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(stomper.body.linearVelocity.y, Is.GreaterThan(0f));
                Assert.That(target.LastFatalHitSource, Is.SameAs(stomper));
                Assert.That(target.LastFatalHitCause, Is.EqualTo("Head Stomp"));
                Assert.That(target.LastFatalHitSummary, Is.EqualTo(stomper.BotDisplayName + " via Head Stomp"));
            }
            finally
            {
                Object.DestroyImmediate(stomperDefinition);
                Object.DestroyImmediate(targetDefinition);
                Object.DestroyImmediate(stomperRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void TryCheckHeadStomp_DoesNotKillTargetDuringDodgeWindow()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);

            GameObject stomperRoot = new GameObject("stomper_dodge");
            GameObject targetRoot = new GameObject("stomp_target_dodge");
            CharacterDefinition stomperDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition targetDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                stomperDefinition.colliderSize = new Vector2(20f, 20f);
                targetDefinition.colliderSize = new Vector2(20f, 20f);

                PlayerController stomper = CreatePlayer(stomperRoot, 1, stomperDefinition);
                PlayerController target = CreatePlayer(targetRoot, 2, targetDefinition);

                stomperRoot.transform.position = new Vector3(0f, 15f, 0f);
                targetRoot.transform.position = Vector3.zero;
                stomper.body.position = new Vector2(0f, 15f);
                target.body.position = Vector2.zero;
                stomper.body.linearVelocity = new Vector2(0f, -100f);

                InvokeAwake(stomper);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.dashParryTimer = 0.2f;

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(stomper);
                jumpSystem.TryCheckHeadStomp();

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stomperDefinition);
                Object.DestroyImmediate(targetDefinition);
                Object.DestroyImmediate(stomperRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void TryCheckHeadStomp_DoesNotKillTargetDuringDashPressWindow()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);

            GameObject stomperRoot = new GameObject("stomper_press");
            GameObject targetRoot = new GameObject("stomp_target_press");
            CharacterDefinition stomperDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterDefinition targetDefinition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                stomperDefinition.colliderSize = new Vector2(20f, 20f);
                targetDefinition.colliderSize = new Vector2(20f, 20f);

                PlayerController stomper = CreatePlayer(stomperRoot, 1, stomperDefinition);
                PlayerController target = CreatePlayer(targetRoot, 2, targetDefinition);

                stomperRoot.transform.position = new Vector3(0f, 15f, 0f);
                targetRoot.transform.position = Vector3.zero;
                stomper.body.position = new Vector2(0f, 15f);
                target.body.position = Vector2.zero;
                stomper.body.linearVelocity = new Vector2(0f, -100f);

                InvokeAwake(stomper);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.dashPressTimer = 0.2f;

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(stomper);
                jumpSystem.TryCheckHeadStomp();

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.IsDodgeInvulnerable, Is.True);
                Assert.That(stomper.body.linearVelocity.y, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(stomperDefinition);
                Object.DestroyImmediate(targetDefinition);
                Object.DestroyImmediate(stomperRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleJumpAndGravity_PreservesWallNormalForGraceWallJumpAfterWallDetach()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("wall_jump_grace_normal_player");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.wallJumpHorizontalForce = 500f;
                definition.wallJumpVerticalForce = 720f;

                PlayerController player = CreatePlayer(root, 1, definition);
                InvokeAwake(player);

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                context.isGrounded = false;
                context.coyoteTimeLeft = 0f;
                context.isTouchingWall = true;
                context.wallJumpGraceTimer = 0.12f;
                context.wallNormal = Vector2.right;

                Vector2 velocity = new Vector2(-100f, 120f);
                jumpSystem.HandleJumpAndGravity(new PlayerInputFrame
                {
                    axis = -1f,
                    jumpHeld = true,
                }, 0.02f, ref velocity);

                Assert.That(context.isTouchingWall, Is.False);
                Assert.That(context.wallDetachIgnoreTimer, Is.GreaterThan(0f));
                Assert.That(context.wallJumpGraceTimer, Is.GreaterThan(0f));
                Assert.That(context.wallNormal, Is.EqualTo(Vector2.right));

                context.jumpBufferLeft = 0.12f;

                Assert.That(jumpSystem.TryConsumeJump(ref velocity), Is.True);
                Assert.That(velocity.x, Is.EqualTo(definition.wallJumpHorizontalForce).Within(0.0001f));
                Assert.That(velocity.y, Is.EqualTo(definition.wallJumpVerticalForce).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryConsumeJump_AllowsWallJumpWhileStillTouchingWallAfterGraceTimerExpires()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("wall_jump_touching_wall_player");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.wallJumpHorizontalForce = 520f;
                definition.wallJumpVerticalForce = 740f;

                PlayerController player = CreatePlayer(root, 1, definition);
                InvokeAwake(player);

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                context.isGrounded = false;
                context.coyoteTimeLeft = 0f;
                context.isTouchingWall = true;
                context.wallJumpGraceTimer = 0f;
                context.wallNormal = Vector2.left;
                context.jumpBufferLeft = 0.12f;

                Vector2 velocity = new Vector2(0f, -80f);

                Assert.That(jumpSystem.TryConsumeJump(ref velocity), Is.True);
                Assert.That(velocity.x, Is.EqualTo(-definition.wallJumpHorizontalForce).Within(0.0001f));
                Assert.That(velocity.y, Is.EqualTo(definition.wallJumpVerticalForce).Within(0.0001f));
                Assert.That(context.jumpBufferLeft, Is.Zero);
                Assert.That(context.isTouchingWall, Is.False);
                Assert.That(context.wallDetachIgnoreTimer, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefreshCollisionState_PreservesWallNormalDuringWallJumpGraceDetach()
        {
            PlayerContext context = new PlayerContext
            {
                isTouchingWall = true,
                wallJumpGraceTimer = 0.12f,
                wallDetachIgnoreTimer = 0.12f,
                wallNormal = Vector2.left,
            };
            PlayerCollisionSystem collisionSystem = new PlayerCollisionSystem(context);

            collisionSystem.RefreshCollisionState();

            Assert.That(context.isTouchingWall, Is.False);
            Assert.That(context.wallJumpGraceTimer, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(context.wallDetachIgnoreTimer, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(context.wallNormal, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void HandleActiveMelee_KillsOverlappingTarget()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);

            GameObject attackerRoot = new GameObject("melee_attacker");
            GameObject targetRoot = new GameObject("melee_target");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(70f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(70f, 0f);

                InvokeAwake(attacker);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(attacker);
                Assert.That(combatSystem, Is.Not.Null);

                combatSystem.TryUseMelee(new PlayerInputFrame { meleePressed = true });
                combatSystem.HandleActiveMelee();

                Assert.That(target.IsDead, Is.True);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(target.LastFatalHitSource, Is.SameAs(attacker));
                Assert.That(target.LastFatalHitCause, Is.EqualTo("Melee"));
                Assert.That(target.LastFatalHitSummary, Is.EqualTo(attacker.BotDisplayName + " via Melee"));
            }
            finally
            {
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleActiveMelee_DoesNotKillTargetDuringDodgeWindow()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);

            GameObject attackerRoot = new GameObject("melee_attacker_dodge");
            GameObject targetRoot = new GameObject("melee_target_dodge");

            try
            {
                PlayerController attacker = CreatePlayer(attackerRoot, 1, null);
                PlayerController target = CreatePlayer(targetRoot, 2, null);

                attackerRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(70f, 0f, 0f);
                attacker.body.position = Vector2.zero;
                target.body.position = new Vector2(70f, 0f);

                InvokeAwake(attacker);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerContext targetContext = (PlayerContext)ContextField.GetValue(target);
                targetContext.dashParryTimer = 0.2f;

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(attacker);
                Assert.That(combatSystem, Is.Not.Null);

                combatSystem.TryUseMelee(new PlayerInputFrame { meleePressed = true });
                combatSystem.HandleActiveMelee();

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.IsHitStunned, Is.False);
                Assert.That(target.IsKnockedBack, Is.False);
                Assert.That(target.LastFatalHitSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void TrySeverProjectileWithMelee_DoesNotSeverOwnProjectile()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);

            GameObject playerRoot = new GameObject("melee_own_projectile_player");
            GameObject projectileRoot = new GameObject("melee_own_projectile");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.meleeCanSeverProjectiles = true;
                PlayerController player = CreatePlayer(playerRoot, 1, definition);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();
                BoxCollider2D projectileCollider = projectileRoot.AddComponent<BoxCollider2D>();
                projectile.hitCollider = projectileCollider;

                projectile.Launch(
                    playerRoot,
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

                InvokeAwake(player);

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                Assert.That(combatSystem, Is.Not.Null);

                bool severed = combatSystem.TrySeverProjectileWithMelee(projectileCollider);

                Assert.That(severed, Is.False);
                Assert.That(projectile.IsDisarmed, Is.False);
                Assert.That(projectile.IsCollectible, Is.False);
                Assert.That(projectile.SourceObject, Is.SameAs(playerRoot));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(playerRoot);
            }
        }

        [Test]
        public void TryUseMelee_SpawnsReadableAttackCueWhenAccepted()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            DestroyAttackCueFxForTests();

            GameObject root = new GameObject("melee_attack_cue_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                combatSystem.TryUseMelee(new PlayerInputFrame { meleePressed = true });

                ProjectPvpAttackCueFx[] effects = Object.FindObjectsByType<ProjectPvpAttackCueFx>(FindObjectsSortMode.None);
                Assert.That(effects, Has.Length.EqualTo(1));
                Assert.That(effects[0].Kind, Is.EqualTo(ProjectPvpAttackCueKind.Melee));
                Assert.That((Vector2)effects[0].transform.position, Is.EqualTo(player.MeleeHitboxCenter));
                Assert.That(effects[0].transform.localScale.x, Is.EqualTo(player.MeleeHitboxSize.x).Within(0.001f));
                Assert.That(effects[0].Duration, Is.EqualTo(0.12f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryUseMelee_DoesNotSpawnAttackCueWhenCooldownBlocksAction()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);
            DestroyAttackCueFxForTests();

            GameObject root = new GameObject("blocked_melee_attack_cue_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                context.meleeCooldownLeft = 0.2f;

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                combatSystem.TryUseMelee(new PlayerInputFrame { meleePressed = true });

                Assert.That(Object.FindObjectsByType<ProjectPvpAttackCueFx>(FindObjectsSortMode.None), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryUseUltimate_SpawnsWarningCueWhenAccepted()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            DestroyAttackCueFxForTests();

            GameObject root = new GameObject("ultimate_attack_cue_player");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.ultimateDashDistance = 120f;
                PlayerController player = CreatePlayer(root, 1, definition);
                InvokeAwake(player);

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                combatSystem.TryUseUltimate(new PlayerInputFrame { ultimatePressed = true });

                ProjectPvpAttackCueFx[] effects = Object.FindObjectsByType<ProjectPvpAttackCueFx>(FindObjectsSortMode.None);
                Assert.That(effects, Has.Length.EqualTo(1));
                Assert.That(effects[0].Kind, Is.EqualTo(ProjectPvpAttackCueKind.Ultimate));
                Assert.That((Vector2)effects[0].transform.position, Is.EqualTo(player.UltimateHitboxCenter));
                Assert.That(effects[0].transform.localScale.x, Is.EqualTo(player.UltimateHitboxRadius * 2f).Within(0.001f));
                Assert.That(effects[0].Duration, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryCollectProjectile_RequiresCollectibleProjectileAndFreeArrowSlot()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("collect_projectile_player");
            GameObject projectileRoot = new GameObject("collect_projectile");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                projectile.Launch(
                    root,
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

                InvokeAwake(player);
                var context = (PlayerContext)ContextField.GetValue(player);
                context.arrows = 2;

                bool collectedWhileFlying = player.TryCollectProjectile(projectile);
                Assert.That(collectedWhileFlying, Is.False);
                Assert.That(player.CurrentArrows, Is.EqualTo(2));

                context.arrows = 3;
                projectile.Stick(true);

                bool collectedAtMaxArrows = player.TryCollectProjectile(projectile);
                Assert.That(collectedAtMaxArrows, Is.False);
                Assert.That(player.CurrentArrows, Is.EqualTo(3));

                context.arrows = 2;
                bool collectedWhileStuck = player.TryCollectProjectile(projectile);
                Assert.That(collectedWhileStuck, Is.True);
                Assert.That(player.CurrentArrows, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryCollectProjectile_AllowsCollectingDisarmedRecoverableProjectiles()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("collect_disarmed_projectile_player");
            GameObject projectileRoot = new GameObject("collect_disarmed_projectile");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                projectile.Launch(
                    root,
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

                InvokeAwake(player);
                var context = (PlayerContext)ContextField.GetValue(player);
                context.arrows = 2;

                projectile.SeverByMelee();

                bool collectedDisarmed = player.TryCollectProjectile(projectile);

                Assert.That(collectedDisarmed, Is.True);
                Assert.That(player.CurrentArrows, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryCollectProjectile_ConsumesProjectileBeforeAnotherPlayerCanCollectIt()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject firstRoot = new GameObject("collect_once_first_player");
            GameObject secondRoot = new GameObject("collect_once_second_player");
            GameObject projectileRoot = new GameObject("collect_once_projectile");

            try
            {
                PlayerController firstPlayer = CreatePlayer(firstRoot, 1, null);
                PlayerController secondPlayer = CreatePlayer(secondRoot, 2, null);
                ProjectileController projectile = projectileRoot.AddComponent<ProjectileController>();

                projectile.Launch(
                    firstRoot,
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

                InvokeAwake(firstPlayer);
                InvokeAwake(secondPlayer);

                PlayerContext firstContext = (PlayerContext)ContextField.GetValue(firstPlayer);
                PlayerContext secondContext = (PlayerContext)ContextField.GetValue(secondPlayer);
                firstContext.arrows = 2;
                secondContext.arrows = 2;
                projectile.Stick(true);

                bool firstCollected = firstPlayer.TryCollectProjectile(projectile);
                bool secondCollected = secondPlayer.TryCollectProjectile(projectile);

                Assert.That(firstCollected, Is.True);
                Assert.That(secondCollected, Is.False);
                Assert.That(firstPlayer.CurrentArrows, Is.EqualTo(3));
                Assert.That(secondPlayer.CurrentArrows, Is.EqualTo(2));
                Assert.That(projectile.IsCollectible, Is.False);
                Assert.That(projectile.BuildAiArenaProjectileSnapshot().isValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(secondRoot);
                Object.DestroyImmediate(firstRoot);
            }
        }

        [Test]
        public void UpdateAimHoldState_ReleasesShotAfterHoldCycle()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(MovementSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("aim_hold_cycle_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerMovementSystem movementSystem = (PlayerMovementSystem)MovementSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(movementSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.arrows = 3;
                context.facing = 1;
                context.aimHoldDirection = Vector2.right;
                context.shootHeldLastFrame = false;

                bool pressed = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootHeld = true,
                    aim = Vector2.right,
                });

                Assert.That(pressed, Is.False);
                Assert.That(context.aimHoldActive, Is.True);
                Assert.That(context.shootHeldLastFrame, Is.True);

                bool released = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootHeld = false,
                    aim = Vector2.right,
                });

                Assert.That(released, Is.True);
                Assert.That(context.aimHoldActive, Is.False);
                Assert.That(context.shootHeldLastFrame, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UpdateAimHoldState_StartsHoldFromBufferedShootPressWithoutHeldInput()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(MovementSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("aim_hold_buffered_press_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerMovementSystem movementSystem = (PlayerMovementSystem)MovementSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(movementSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.arrows = 3;
                context.facing = 1;
                context.aimHoldDirection = Vector2.right;
                context.shootHeldLastFrame = false;

                bool pressed = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootPressed = true,
                    shootHeld = false,
                    aim = Vector2.up,
                });

                Assert.That(pressed, Is.False);
                Assert.That(context.aimHoldActive, Is.True);
                Assert.That(context.shootHeldLastFrame, Is.True);
                Assert.That(context.aimHoldDirection, Is.EqualTo(Vector2.up));

                bool released = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootPressed = false,
                    shootHeld = false,
                    aim = Vector2.up,
                });

                Assert.That(released, Is.True);
                Assert.That(context.aimHoldActive, Is.False);
                Assert.That(context.shootHeldLastFrame, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UpdateAimHoldState_RearmsShootPressAfterArrowsAreRefilled()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(MovementSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("aim_hold_refill_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerMovementSystem movementSystem = (PlayerMovementSystem)MovementSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(movementSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.arrows = 0;
                context.facing = 1;
                context.shootHeldLastFrame = false;

                bool blockedWhileEmpty = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootHeld = true,
                    aim = Vector2.right,
                });

                Assert.That(blockedWhileEmpty, Is.False);
                Assert.That(context.aimHoldActive, Is.False);
                Assert.That(context.shootHeldLastFrame, Is.False);

                context.arrows = 1;

                bool pressedAfterRefill = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootHeld = true,
                    aim = Vector2.right,
                });

                Assert.That(pressedAfterRefill, Is.False);
                Assert.That(context.aimHoldActive, Is.True);
                Assert.That(context.shootHeldLastFrame, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryStartDash_UsesSnappedAimDirectionWithUpwardMultiplier()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("directional_dash_player");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                definition.dashDistance = 120f;
                definition.dashDuration = 0.12f;
                definition.dashCooldown = 0.45f;
                definition.dashUpwardMultiplier = 0.5f;

                PlayerController player = CreatePlayer(root, 1, definition);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(dashSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.facing = 1;

                dashSystem.TryStartDash(new PlayerInputFrame
                {
                    dashPrimaryPressed = true,
                    aim = new Vector2(1f, 1f),
                });

                Assert.That(context.dashVelocity.x, Is.GreaterThan(0f));
                Assert.That(context.dashVelocity.y, Is.GreaterThan(0f));
                Assert.That(context.dashVelocity.y, Is.LessThan(context.dashVelocity.x));
                Assert.That(context.dashTimeLeft, Is.EqualTo(definition.dashDuration).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryStartDash_PrioritizesMovementDirectionOverAimDirection()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("movement_dash_priority_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(dashSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.facing = 1;

                dashSystem.TryStartDash(new PlayerInputFrame
                {
                    dashPrimaryPressed = true,
                    axis = -1f,
                    aim = Vector2.right,
                });

                Assert.That(context.dashVelocity.x, Is.LessThan(0f));
                Assert.That(context.dashVelocity.y, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UpdateDashVelocity_ScalesFinalFrameToRemainingDashTime()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("partial_dash_frame_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(dashSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.dashTimeLeft = 0.005f;
                context.dashVelocity = new Vector2(800f, 0f);

                Vector2 baseVelocity = Vector2.zero;
                Vector2 dashVelocity = dashSystem.UpdateDashVelocity(0.02f, ref baseVelocity);

                Assert.That(dashVelocity.x, Is.EqualTo(200f).Within(0.0001f));
                Assert.That(dashVelocity.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.dashTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.dashVelocity, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryStartDash_DoesNotGrantPressParryWhenRequestedDashIsOnCooldown()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("cooldown_dash_press_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(dashSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.dashPrimaryCooldownLeft = 0.35f;
                context.dashSecondaryCooldownLeft = 0.35f;
                context.dashPressTimer = 0f;
                context.dashParryTimer = 0f;

                dashSystem.TryStartDash(new PlayerInputFrame
                {
                    dashPrimaryPressed = true,
                    aim = Vector2.right,
                });

                Assert.That(context.dashTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.dashPressTimer, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(player.IsDodgeInvulnerable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryStartDash_DoesNotRefreshPressParryWhileAlreadyDashing()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("active_dash_press_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(dashSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.dashTimeLeft = 0.1f;
                context.dashPrimaryCooldownLeft = 0f;
                context.dashSecondaryCooldownLeft = 0f;
                context.dashPressTimer = 0f;
                context.dashParryTimer = 0f;

                dashSystem.TryStartDash(new PlayerInputFrame
                {
                    dashPrimaryPressed = true,
                    aim = Vector2.right,
                });

                Assert.That(context.dashTimeLeft, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(context.dashPressTimer, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(player.IsDodgeInvulnerable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UpdateUltimateDashVelocity_ScalesFinalFrameToRemainingDashTime()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("partial_ultimate_dash_frame_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                Assert.That(combatSystem, Is.Not.Null);
                Assert.That(context, Is.Not.Null);

                context.ultimateDashTimeLeft = 0.005f;
                context.ultimateDashVelocity = new Vector2(600f, 0f);

                Vector2 ultimateDashVelocity = combatSystem.UpdateUltimateDashVelocity(0.02f);

                Assert.That(ultimateDashVelocity.x, Is.EqualTo(150f).Within(0.0001f));
                Assert.That(ultimateDashVelocity.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.ultimateDashTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.ultimateDashVelocity, Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_ReturnsFalseAfterPlayerIsAlreadyDead()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject root = new GameObject("kill_idempotence_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                Assert.That(player.TryKill(), Is.True);
                Assert.That(player.IsDead, Is.True);

                Assert.That(player.TryKill(), Is.False);
                Assert.That(player.IsDead, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_ClearsCurrentInputFrameOnDeath()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("kill_clears_input_frame_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                context.currentInputFrame = new PlayerInputFrame
                {
                    axis = 1f,
                    jumpPressed = true,
                    shootHeld = true,
                };

                Assert.That(player.TryKill(), Is.True);
                Assert.That(player.IsDead, Is.True);
                Assert.That(player.CurrentInputFrame, Is.EqualTo(default(PlayerInputFrame)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeadPlayer_CannotStartCombatActions()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("dead_combat_actions_player");
            GameObject projectilePrefabRoot = new GameObject("dead_combat_actions_projectile_prefab");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                ProjectileController projectilePrefab = projectilePrefabRoot.AddComponent<ProjectileController>();
                definition.ultimateDashDistance = 120f;
                definition.ultimateDashDuration = 0.1f;
                definition.projectileBaseSpeed = 1234f;
                definition.projectileGravity = 987f;

                PlayerController player = CreatePlayer(root, 1, definition);
                player.projectilePrefab = projectilePrefab;
                InvokeAwake(player);

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);
                context.arrows = 3;
                context.aimHoldDirection = Vector2.right;
                context.facing = 1;

                Assert.That(player.TryKill(), Is.True);

                combatSystem.FireHeldShot();
                combatSystem.TryUseMelee(new PlayerInputFrame { meleePressed = true });
                combatSystem.TryUseUltimate(new PlayerInputFrame { ultimatePressed = true });
                player.ApplyHitstun(0.25f);
                player.ApplyKnockback(Vector2.right, 300f, 0.2f);
                context.dashParryTimer = 0.2f;
                context.dashPressTimer = 0.2f;
                context.ultimateProjectileBlockTimer = 0.2f;
                context.hitStunTimeLeft = 0.25f;
                context.knockbackVelocity = Vector2.right * 300f;
                context.knockbackTimeLeft = 0.2f;

                AiArenaControllerSnapshot snapshot = player.BuildAiArenaControllerSnapshot(1, Vector2.zero);

                Assert.That(player.CurrentArrows, Is.EqualTo(3));
                Assert.That(player.LastLaunchedProjectile, Is.Null);
                Assert.That(player.IsDodgeInvulnerable, Is.False);
                Assert.That(player.IsHitStunned, Is.False);
                Assert.That(player.IsKnockedBack, Is.False);
                Assert.That(player.CanParryProjectile, Is.False);
                Assert.That(player.CanBlockProjectileWithUltimate, Is.False);
                Assert.That(snapshot.canParryProjectile, Is.False);
                Assert.That(snapshot.canBlockProjectiles, Is.False);
                Assert.That(snapshot.isHitStunned, Is.False);
                Assert.That(snapshot.hitStunTimeLeft, Is.Zero);
                Assert.That(snapshot.projectileBaseSpeed, Is.EqualTo(1234f).Within(0.001f));
                Assert.That(snapshot.projectileGravity, Is.EqualTo(987f).Within(0.001f));
                Assert.That(context.shootCooldownLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.meleeCooldownLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.meleeTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.ultimateCooldownLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.ultimateTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.ultimateDashTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.actionLockEntries, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectilePrefabRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeadPlayer_CannotStartDashOrJumpActions()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DashSystemField, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);
            Assert.That(MovementSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("dead_movement_actions_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                PlayerDashSystem dashSystem = (PlayerDashSystem)DashSystemField.GetValue(player);
                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(player);
                PlayerMovementSystem movementSystem = (PlayerMovementSystem)MovementSystemField.GetValue(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);

                Assert.That(player.TryKill(), Is.True);
                context.jumpBufferLeft = 0.16f;
                context.isGrounded = true;
                context.coyoteTimeLeft = 0.16f;

                dashSystem.TryStartDash(new PlayerInputFrame
                {
                    dashPrimaryPressed = true,
                    jumpPressed = true,
                    jumpHeld = true,
                    aim = Vector2.right,
                });

                Vector2 velocity = Vector2.zero;
                jumpSystem.HandleJumpAndGravity(new PlayerInputFrame { jumpHeld = true }, 0.02f, ref velocity);
                movementSystem.HandleMovement(new PlayerInputFrame { axis = 1f }, 0.02f, ref velocity);
                context.facing = 1;
                movementSystem.UpdateFacing(new PlayerInputFrame { axis = -1f, aim = Vector2.left });
                Vector2 bodyPosition = player.body.position;
                Vector2 forcedMovement = new Vector2(200f, 0f);
                movementSystem.MoveCharacter(ref forcedMovement, 0.02f);
                bool releasedShot = movementSystem.UpdateAimHoldState(new PlayerInputFrame
                {
                    shootPressed = true,
                    shootHeld = true,
                    aim = Vector2.up,
                });

                Assert.That(context.dashTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.dashVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.dashPressTimer, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.pendingDashPrimary, Is.False);
                Assert.That(context.pendingDashSecondary, Is.False);
                Assert.That(velocity, Is.EqualTo(Vector2.zero));
                Assert.That(context.jumpStartTimeLeft, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(context.jumpBufferLeft, Is.EqualTo(0.16f).Within(0.0001f));
                Assert.That(context.facing, Is.EqualTo(1));
                Assert.That(player.body.position, Is.EqualTo(bodyPosition));
                Assert.That(releasedShot, Is.False);
                Assert.That(context.aimHoldActive, Is.False);
                Assert.That(context.shootHeldLastFrame, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FireHeldShot_UsesRawEightDirectionLaunchWithLightAssist()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(CombatSystemField, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject shooterRoot = new GameObject("shot_assist_shooter");
            GameObject targetRoot = new GameObject("shot_assist_target");
            GameObject projectilePrefabRoot = new GameObject("shot_assist_projectile_prefab");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            ProjectileController launchedProjectile = null;

            try
            {
                ProjectileController projectilePrefab = projectilePrefabRoot.AddComponent<ProjectileController>();
                PlayerController shooter = CreatePlayer(shooterRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);

                definition.projectileAssistEnabled = true;
                definition.projectileAssistAcquireConeDeg = 36f;
                definition.projectileAssistMinDistance = 1f;
                definition.projectileAssistMaxRange = 100f;
                definition.projectileInheritVelocityFactor = 0f;

                shooter.projectilePrefab = projectilePrefab;
                shooterRoot.transform.position = Vector3.zero;
                targetRoot.transform.position = new Vector3(8f, 1f, 0f);
                shooter.body.position = Vector2.zero;
                shooter.body.linearVelocity = new Vector2(220f, 80f);
                target.body.position = new Vector2(8f, 1f);

                InvokeAwake(shooter);
                InvokeAwake(target);

                PlayerContext context = (PlayerContext)ContextField.GetValue(shooter);
                context.aimHoldDirection = Vector2.right;
                context.arrows = 3;
                context.shootCooldownLeft = 0f;

                PlayerCombatSystem combatSystem = (PlayerCombatSystem)CombatSystemField.GetValue(shooter);
                combatSystem.FireHeldShot();

                launchedProjectile = shooter.LastLaunchedProjectile;
                Assert.That(launchedProjectile, Is.Not.Null);
                Assert.That(launchedProjectile.AssistEnabledRuntime, Is.True);
                Assert.That(launchedProjectile.AssistTargetLocked, Is.True);
                Assert.That(Vector2.Angle(launchedProjectile.TravelDirection, Vector2.right), Is.LessThan(0.001f));
                Assert.That(launchedProjectile.CurrentVelocity.magnitude, Is.EqualTo(definition.projectileBaseSpeed).Within(0.001f));
            }
            finally
            {
                if (launchedProjectile != null)
                {
                    Object.DestroyImmediate(launchedProjectile.gameObject);
                }

                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(projectilePrefabRoot);
                Object.DestroyImmediate(shooterRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void SetSpawnPosition_ClearsDeadAndHitReactionState()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(ContextField, Is.Not.Null);

            GameObject root = new GameObject("respawn_reset_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                player.spriteRenderer = root.AddComponent<SpriteRenderer>();
                InvokeAwake(player);
                PlayerContext context = (PlayerContext)ContextField.GetValue(player);

                Assert.That(player.TryKill(), Is.True);
                Assert.That(player.DeathFlashTimeLeft, Is.GreaterThan(0f));
                Assert.That(player.VisualSpriteRenderer.color, Is.EqualTo(new Color(1f, 0.58f, 0.26f, 1f)));
                context.hitStunTimeLeft = 0.25f;
                context.knockbackVelocity = Vector2.right * 300f;
                context.knockbackTimeLeft = 0.2f;

                Assert.That(player.IsDead, Is.True);
                Assert.That(context.hitStunTimeLeft, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(context.knockbackTimeLeft, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(context.knockbackVelocity, Is.EqualTo(Vector2.right * 300f));

                player.SetSpawnPosition(new Vector2(120f, 48f));

                Assert.That(player.IsDead, Is.False);
                Assert.That(player.IsHitStunned, Is.False);
                Assert.That(player.IsKnockedBack, Is.False);
                Assert.That(player.DeathFlashTimeLeft, Is.Zero);
                Assert.That(player.VisualSpriteRenderer.color, Is.EqualTo(Color.white));
                Assert.That(player.CurrentVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(player.LastFatalHitSource, Is.Null);
                Assert.That(player.LastFatalHitCause, Is.Empty);
                Assert.That(player.LastFatalHitPosition, Is.EqualTo(Vector2.zero));
                Assert.That(player.LastFatalHitSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SetSpawnPosition_ClearsPendingDeathNotificationRoutine()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(DeathNotifyRoutineField, Is.Not.Null);

            GameObject root = new GameObject("respawn_death_notify_reset_player");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            CharacterAnimationCatalog animationCatalog = ScriptableObject.CreateInstance<CharacterAnimationCatalog>();
            Texture2D texture = new Texture2D(2, 2);
            Sprite deathSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 1f);

            try
            {
                animationCatalog.actionSpriteAnimations.Add(new ActionSpriteAnimation
                {
                    actionName = "death",
                    directionKey = "right",
                    framesPerSecond = 1f,
                    loop = false,
                    frames = new List<Sprite> { deathSprite },
                });
                definition.animationCatalog = animationCatalog;

                PlayerController player = CreatePlayer(root, 1, definition);
                player.spriteRenderer = root.AddComponent<SpriteRenderer>();
                InvokeAwake(player);

                Assert.That(player.TryKill(), Is.True);
                Assert.That(DeathNotifyRoutineField.GetValue(player), Is.Not.Null);

                player.SetSpawnPosition(new Vector2(120f, 48f));

                Assert.That(player.IsDead, Is.False);
                Assert.That(DeathNotifyRoutineField.GetValue(player), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(deathSprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(animationCatalog);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_RingOutBypassesShield()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject root = new GameObject("ring_out_shield_target");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);

                InvokeAwake(player);
                player.SetRoundShield(true);

                Assert.That(player.HasShield, Is.True);
                Assert.That(player.TryKill(null, "Ring Out"), Is.True);

                Assert.That(player.IsDead, Is.True);
                Assert.That(player.LastFatalHitSource, Is.Null);
                Assert.That(player.LastFatalHitCause, Is.EqualTo("Ring Out"));
                Assert.That(player.LastFatalHitSummary, Is.EqualTo("Environment via Ring Out"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_ConsumesShieldAndPreventsGenericDeath()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject root = new GameObject("generic_shield_target");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);

                InvokeAwake(player);
                player.SetRoundShield(true);

                Assert.That(player.HasShield, Is.True);
                Assert.That(player.TryKill(), Is.False);

                Assert.That(player.IsDead, Is.False);
                Assert.That(player.HasShield, Is.False);
                Assert.That(player.LastFatalHitSource, Is.Null);
                Assert.That(player.LastFatalHitCause, Is.Empty);
                Assert.That(player.LastFatalHitSummary, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_ConsumesShieldWithSubtleImpactFeedback()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            DestroyKillImpactFxForTests();

            GameObject cameraRoot = new GameObject("shield_absorb_shake_camera");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();
            GameObject root = new GameObject("shield_absorb_target");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);
                player.SetRoundShield(true);
                PlayerController.ResolveFatalHitCameraShake("Melee", out float meleeIntensity, out float meleeDuration);

                Assert.That(player.TryKill(null, "Projectile"), Is.False);

                Assert.That(player.IsDead, Is.False);
                Assert.That(player.HasShield, Is.False);
                Assert.That(shake.IsShaking, Is.True);
                Assert.That(shake.ActiveIntensity, Is.LessThan(meleeIntensity));
                Assert.That(shake.ActiveDuration, Is.LessThan(meleeDuration));
                Assert.That(Object.FindObjectsByType<ProjectPvpKillImpactFx>(FindObjectsSortMode.None), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryKill_TriggersCameraShakeOnAvailableCamera()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject cameraRoot = new GameObject("kill_shake_camera");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();
            GameObject root = new GameObject("kill_shake_player");

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);

                Assert.That(player.TryKill(), Is.True);
                Assert.That(shake.IsShaking, Is.True);
                Assert.That(cameraRoot.transform.localPosition, Is.Not.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveFatalHitCameraShake_StrengthensHighCommitmentKills()
        {
            PlayerController.ResolveFatalHitCameraShake("Melee", out float meleeIntensity, out float meleeDuration);
            PlayerController.ResolveFatalHitCameraShake("Projectile", out float projectileIntensity, out float projectileDuration);
            PlayerController.ResolveFatalHitCameraShake("Head Stomp", out float headStompIntensity, out float headStompDuration);
            PlayerController.ResolveFatalHitCameraShake("Ring Out", out float ringOutIntensity, out float ringOutDuration);
            PlayerController.ResolveFatalHitCameraShake("Ultimate", out float ultimateIntensity, out float ultimateDuration);

            Assert.That(projectileIntensity, Is.GreaterThan(meleeIntensity));
            Assert.That(projectileDuration, Is.GreaterThan(meleeDuration));
            Assert.That(headStompIntensity, Is.GreaterThan(projectileIntensity));
            Assert.That(headStompDuration, Is.GreaterThan(projectileDuration));
            Assert.That(ringOutDuration, Is.GreaterThan(headStompDuration));
            Assert.That(ultimateIntensity, Is.GreaterThan(headStompIntensity));
            Assert.That(ultimateDuration, Is.EqualTo(ringOutDuration).Within(0.0001f));
        }

        [Test]
        public void TryKill_AppliesCauseSpecificCameraShakeFeedback()
        {
            Assert.That(AwakeMethod, Is.Not.Null);

            GameObject cameraRoot = new GameObject("cause_specific_kill_shake_camera");
            cameraRoot.AddComponent<Camera>();
            ProjectPvpCameraShake shake = cameraRoot.AddComponent<ProjectPvpCameraShake>();
            GameObject meleeRoot = new GameObject("melee_kill_shake_player");
            GameObject ultimateRoot = new GameObject("ultimate_kill_shake_player");

            try
            {
                PlayerController meleeVictim = CreatePlayer(meleeRoot, 1, null);
                PlayerController ultimateVictim = CreatePlayer(ultimateRoot, 2, null);
                InvokeAwake(meleeVictim);
                InvokeAwake(ultimateVictim);

                Assert.That(meleeVictim.TryKill(null, "Melee"), Is.True);
                float meleeIntensity = shake.ActiveIntensity;
                float meleeDuration = shake.ActiveDuration;

                Assert.That(ultimateVictim.TryKill(null, "Ultimate"), Is.True);

                Assert.That(shake.IsShaking, Is.True);
                Assert.That(shake.ActiveIntensity, Is.GreaterThan(meleeIntensity));
                Assert.That(shake.ActiveDuration, Is.GreaterThan(meleeDuration));
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(meleeRoot);
                Object.DestroyImmediate(ultimateRoot);
            }
        }

        [Test]
        public void TryKill_SpawnsKillImpactFxAtFatalHitPosition()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            DestroyKillImpactFxForTests();

            GameObject root = new GameObject("kill_impact_fx_player");
            Vector2 deathPosition = new Vector2(12f, -4f);

            try
            {
                PlayerController player = CreatePlayer(root, 1, null);
                InvokeAwake(player);
                player.body.position = deathPosition;

                Assert.That(player.TryKill(null, "Projectile"), Is.True);

                ProjectPvpKillImpactFx[] effects = Object.FindObjectsByType<ProjectPvpKillImpactFx>(FindObjectsSortMode.None);
                Assert.That(effects, Has.Length.EqualTo(1));
                Assert.That((Vector2)effects[0].transform.position, Is.EqualTo(deathPosition));
                Assert.That(effects[0].BaseColor, Is.EqualTo(ProjectPvpKillImpactFx.ResolveImpactColor("Projectile")));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static PlayerController CreatePlayer(GameObject root, int slotId, CharacterDefinition definition)
        {
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            PlayerController controller = root.AddComponent<PlayerController>();
            controller.slotId = slotId;
            controller.body = body;
            controller.bodyCollider = collider;
            controller.characterDefinition = definition;
            return controller;
        }

        private static void InvokeAwake(PlayerController controller)
        {
            AwakeMethod.Invoke(controller, null);
        }

        private static void DestroyKillImpactFxForTests()
        {
            ProjectPvpKillImpactFx[] effects = Object.FindObjectsByType<ProjectPvpKillImpactFx>(FindObjectsSortMode.None);
            for (int index = 0; index < effects.Length; index += 1)
            {
                if (effects[index] != null)
                {
                    Object.DestroyImmediate(effects[index].gameObject);
                }
            }
        }

        private static void DestroyAttackCueFxForTests()
        {
            ProjectPvpAttackCueFx[] effects = Object.FindObjectsByType<ProjectPvpAttackCueFx>(FindObjectsSortMode.None);
            for (int index = 0; index < effects.Length; index += 1)
            {
                if (effects[index] != null)
                {
                    Object.DestroyImmediate(effects[index].gameObject);
                }
            }
        }

        private static void DestroyParryCueFxForTests()
        {
            ProjectPvpParryCueFx[] effects = Object.FindObjectsByType<ProjectPvpParryCueFx>(FindObjectsSortMode.None);
            for (int index = 0; index < effects.Length; index += 1)
            {
                if (effects[index] != null)
                {
                    Object.DestroyImmediate(effects[index].gameObject);
                }
            }
        }
    }
}
