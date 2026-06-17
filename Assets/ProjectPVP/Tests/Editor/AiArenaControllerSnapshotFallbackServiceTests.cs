using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaControllerSnapshotFallbackServiceTests
    {
        [Test]
        public void BuildFromController_ReadsLegacyControllerProperties()
        {
            GameObject root = new GameObject("LegacyControllerSnapshotSource");
            LegacyControllerSnapshotSource source = root.AddComponent<LegacyControllerSnapshotSource>();

            try
            {
                source.slotId = 2;
                source.characterDefinition = new CharacterDefinitionStub { id = "mizu" };
                source.BotId = "codex-2";
                source.BotDisplayName = "Codex Two";
                source.CurrentVisualActionKey = "shoot";
                source.IsDead = false;
                source.IsGrounded = false;
                source.IsTouchingWall = true;
                source.IsDashing = true;
                source.IsMeleeActive = true;
                source.IsShootAnimating = true;
                source.IsUltimateActive = true;
                source.IsHitStunned = true;
                source.CanParryProjectile = true;
                source.CanBlockProjectileWithUltimate = true;
                source.CurrentArrows = 4;
                source.Facing = -1;
                source.ProjectileInheritVelocityFactor = 0.45f;
                source.ProjectileBaseSpeed = 1234f;
                source.ShootCooldownLeft = 0.1f;
                source.MeleeCooldownLeft = 0.2f;
                source.DashCooldownLeft = 0.3f;
                source.UltimateCooldownLeft = 0.4f;
                source.HitStunTimeLeft = 0.5f;
                source.HorizontalVelocity = 12f;
                source.VerticalVelocity = -6f;
                source.RootPosition = new Vector2(10f, 20f);
                source.MeleeHitboxCenter = new Vector2(1f, 2f);
                source.MeleeHitboxSize = new Vector2(3f, 4f);
                source.UltimateHitboxCenter = new Vector2(5f, 6f);
                source.UltimateHitboxRadius = 7f;

                AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotFallbackService.BuildFromController(
                    source,
                    fallbackSlotId: 1,
                    fallbackPosition: Vector2.zero);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.slotId, Is.EqualTo(2));
                Assert.That(snapshot.botId, Is.EqualTo("codex-2"));
                Assert.That(snapshot.botDisplayName, Is.EqualTo("Codex Two"));
                Assert.That(snapshot.characterId, Is.EqualTo("mizu"));
                Assert.That(snapshot.displayName, Is.EqualTo("LegacyControllerSnapshotSource"));
                Assert.That(snapshot.actionKey, Is.EqualTo("shoot"));
                Assert.That(snapshot.isDead, Is.False);
                Assert.That(snapshot.isGrounded, Is.False);
                Assert.That(snapshot.isTouchingWall, Is.True);
                Assert.That(snapshot.isDashing, Is.True);
                Assert.That(snapshot.isMeleeActive, Is.True);
                Assert.That(snapshot.isShootAnimating, Is.True);
                Assert.That(snapshot.isUltimateActive, Is.True);
                Assert.That(snapshot.isHitStunned, Is.True);
                Assert.That(snapshot.canParryProjectile, Is.True);
                Assert.That(snapshot.canBlockProjectiles, Is.True);
                Assert.That(snapshot.arrows, Is.EqualTo(4));
                Assert.That(snapshot.facing, Is.EqualTo(-1));
                Assert.That(snapshot.projectileInheritVelocityFactor, Is.EqualTo(0.45f));
                Assert.That(snapshot.projectileBaseSpeed, Is.EqualTo(1234f));
                Assert.That(snapshot.shootCooldownLeft, Is.EqualTo(0.1f));
                Assert.That(snapshot.meleeCooldownLeft, Is.EqualTo(0.2f));
                Assert.That(snapshot.dashCooldownLeft, Is.EqualTo(0.3f));
                Assert.That(snapshot.ultimateCooldownLeft, Is.EqualTo(0.4f));
                Assert.That(snapshot.hitStunTimeLeft, Is.EqualTo(0.5f));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(10f, 20f)));
                Assert.That(snapshot.velocity, Is.EqualTo(new Vector2(12f, -6f)));
                Assert.That(snapshot.meleeHitboxCenter, Is.EqualTo(new Vector2(1f, 2f)));
                Assert.That(snapshot.meleeHitboxSize, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(snapshot.ultimateHitboxCenter, Is.EqualTo(new Vector2(5f, 6f)));
                Assert.That(snapshot.ultimateHitboxRadius, Is.EqualTo(7f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildFromController_SuppressesActiveCombatFlagsWhenControllerIsDead()
        {
            GameObject root = new GameObject("DeadLegacyControllerSnapshotSource");
            LegacyControllerSnapshotSource source = root.AddComponent<LegacyControllerSnapshotSource>();

            try
            {
                source.IsDead = true;
                source.IsDashing = true;
                source.IsMeleeActive = true;
                source.IsShootAnimating = true;
                source.IsUltimateActive = true;
                source.IsHitStunned = true;
                source.CanParryProjectile = true;
                source.CanBlockProjectileWithUltimate = true;
                source.HitStunTimeLeft = 0.5f;

                AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotFallbackService.BuildFromController(
                    source,
                    fallbackSlotId: 1,
                    fallbackPosition: Vector2.zero);

                Assert.That(snapshot.isDead, Is.True);
                Assert.That(snapshot.isDashing, Is.False);
                Assert.That(snapshot.isMeleeActive, Is.False);
                Assert.That(snapshot.isShootAnimating, Is.False);
                Assert.That(snapshot.isUltimateActive, Is.False);
                Assert.That(snapshot.isHitStunned, Is.False);
                Assert.That(snapshot.canParryProjectile, Is.False);
                Assert.That(snapshot.canBlockProjectiles, Is.False);
                Assert.That(snapshot.hitStunTimeLeft, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildFromController_UsesFallbacksForMissingOptionalProperties()
        {
            GameObject root = new GameObject("MinimalControllerSnapshotSource");
            MinimalControllerSnapshotSource source = root.AddComponent<MinimalControllerSnapshotSource>();

            try
            {
                root.transform.position = new Vector3(3f, 4f, 0f);

                AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotFallbackService.BuildFromController(
                    source,
                    fallbackSlotId: 2,
                    fallbackPosition: new Vector2(5f, 4f));

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.slotId, Is.EqualTo(2));
                Assert.That(snapshot.botId, Is.Empty);
                Assert.That(snapshot.botDisplayName, Is.EqualTo("MinimalControllerSnapshotSource"));
                Assert.That(snapshot.characterId, Is.Empty);
                Assert.That(snapshot.displayName, Is.EqualTo("MinimalControllerSnapshotSource"));
                Assert.That(snapshot.actionKey, Is.Empty);
                Assert.That(snapshot.isDead, Is.False);
                Assert.That(snapshot.isGrounded, Is.True);
                Assert.That(snapshot.facing, Is.EqualTo(-1));
                Assert.That(snapshot.projectileInheritVelocityFactor, Is.EqualTo(1f));
                Assert.That(snapshot.projectileBaseSpeed, Is.EqualTo(1600f));
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(snapshot.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(snapshot.meleeHitboxCenter, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(snapshot.meleeHitboxSize, Is.EqualTo(Vector2.zero));
                Assert.That(snapshot.ultimateHitboxCenter, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(snapshot.ultimateHitboxRadius, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuildFromController_ReturnsDefaultForMissingController()
        {
            AiArenaControllerSnapshot snapshot = AiArenaControllerSnapshotFallbackService.BuildFromController(
                null,
                fallbackSlotId: 2,
                fallbackPosition: Vector2.one);

            Assert.That(snapshot.isValid, Is.False);
            Assert.That(snapshot.slotId, Is.Zero);
            Assert.That(snapshot.displayName, Is.Null);
        }

        private sealed class LegacyControllerSnapshotSource : MonoBehaviour
        {
            public int slotId;
            public CharacterDefinitionStub characterDefinition;
            public string BotId { get; set; }
            public string BotDisplayName { get; set; }
            public string CurrentVisualActionKey { get; set; }
            public bool IsDead { get; set; }
            public bool IsGrounded { get; set; }
            public bool IsTouchingWall { get; set; }
            public bool IsDashing { get; set; }
            public bool IsMeleeActive { get; set; }
            public bool IsShootAnimating { get; set; }
            public bool IsUltimateActive { get; set; }
            public bool IsHitStunned { get; set; }
            public bool CanParryProjectile { get; set; }
            public bool CanBlockProjectileWithUltimate { get; set; }
            public int CurrentArrows { get; set; }
            public int Facing { get; set; }
            public float ProjectileInheritVelocityFactor { get; set; }
            public float ProjectileBaseSpeed { get; set; }
            public float ShootCooldownLeft { get; set; }
            public float MeleeCooldownLeft { get; set; }
            public float DashCooldownLeft { get; set; }
            public float UltimateCooldownLeft { get; set; }
            public float HitStunTimeLeft { get; set; }
            public float HorizontalVelocity { get; set; }
            public float VerticalVelocity { get; set; }
            public Vector2 RootPosition { get; set; }
            public Vector2 MeleeHitboxCenter { get; set; }
            public Vector2 MeleeHitboxSize { get; set; }
            public Vector2 UltimateHitboxCenter { get; set; }
            public float UltimateHitboxRadius { get; set; }
        }

        private sealed class MinimalControllerSnapshotSource : MonoBehaviour
        {
        }

        public sealed class CharacterDefinitionStub
        {
            public string id;
        }
    }
}
