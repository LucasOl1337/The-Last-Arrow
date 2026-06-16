using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPVP.Data;
using ProjectPVP.Gameplay;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class PlayerCombatSystemTests
    {
        private static readonly MethodInfo AwakeMethod =
            typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo JumpSystemField =
            typeof(PlayerController).GetField("_jumpSystem", BindingFlags.Instance | BindingFlags.NonPublic);
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
        public void ApplyEliminationHits_AppliesUltimateHitstunAndKnockback_WithoutKillingTarget()
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

                definition.meleeHitstunDuration = 0.2f;
                FieldInfo ultimateHitstunField = typeof(CharacterDefinition).GetField("ultimateHitstunDuration", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(ultimateHitstunField, Is.Not.Null);
                ultimateHitstunField.SetValue(definition, 0.35f);
                definition.ultimateKnockbackForce = 800f;

                InvokeAwake(attacker);
                InvokeAwake(target);

                attacker.ApplyEliminationHits(new Collider2D[] { targetCollider }, 1);

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HitStunTimeLeft, Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(target.IsKnockedBack, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(attackerRoot);
                Object.DestroyImmediate(targetRoot);
            }
        }

        [Test]
        public void HandleIncomingProjectile_AppliesProjectileHitstunAndKnockback_WithoutKillingTarget()
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

                definition.meleeHitstunDuration = 0.2f;
                definition.projectileHitstunDuration = 0.2f;
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
                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HitStunTimeLeft, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(target.IsKnockedBack, Is.True);
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
        public void HandleIncomingProjectile_UsesProjectileSpecificHitstunDuration_WhenConfigured()
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

                definition.meleeHitstunDuration = 0.2f;
                definition.projectileKnockbackForce = 500f;

                FieldInfo projectileHitstunField = typeof(CharacterDefinition).GetField("projectileHitstunDuration", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(projectileHitstunField, Is.Not.Null);
                projectileHitstunField.SetValue(definition, 0.08f);

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
                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HitStunTimeLeft, Is.EqualTo(0.08f).Within(0.001f));
                Assert.That(target.IsKnockedBack, Is.True);
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
        public void TryCheckHeadStomp_AppliesHitstunAndKnockback_WithoutKillingTarget()
        {
            Assert.That(AwakeMethod, Is.Not.Null);
            Assert.That(JumpSystemField, Is.Not.Null);

            GameObject stomperRoot = new GameObject("stomper");
            GameObject targetRoot = new GameObject("stomp_target");
            CharacterDefinition definition = ScriptableObject.CreateInstance<CharacterDefinition>();

            try
            {
                PlayerController stomper = CreatePlayer(stomperRoot, 1, definition);
                PlayerController target = CreatePlayer(targetRoot, 2, null);

                stomper.bodyCollider.size = new Vector2(20f, 20f);
                target.bodyCollider.size = new Vector2(20f, 20f);
                stomperRoot.transform.position = new Vector3(0f, 15f, 0f);
                targetRoot.transform.position = Vector3.zero;
                stomper.body.position = new Vector2(0f, 15f);
                target.body.position = Vector2.zero;
                stomper.body.linearVelocity = new Vector2(0f, -100f);

                definition.meleeHitstunDuration = 0.2f;
                definition.meleeKnockbackForce = 450f;

                InvokeAwake(stomper);
                InvokeAwake(target);
                Physics2D.SyncTransforms();

                PlayerJumpSystem jumpSystem = (PlayerJumpSystem)JumpSystemField.GetValue(stomper);
                jumpSystem.TryCheckHeadStomp();

                Assert.That(target.IsDead, Is.False);
                Assert.That(target.HitStunTimeLeft, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(target.IsKnockedBack, Is.True);
                Assert.That(stomper.body.linearVelocity.y, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(stomperRoot);
                Object.DestroyImmediate(targetRoot);
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
    }
}
