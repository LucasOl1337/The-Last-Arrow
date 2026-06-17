using NUnit.Framework;
using ProjectPVP.Input;
using UnityEngine;

namespace ProjectPVP.Tests.Editor
{
    public sealed class AiArenaProjectileSnapshotFallbackServiceTests
    {
        [Test]
        public void BuildFromProjectile_ReadsLegacyProjectileAndSourceSlot()
        {
            GameObject sourceRoot = new GameObject("ProjectileSourcePlayer");
            GameObject projectileRoot = new GameObject("LegacyProjectileSnapshotSource");
            PlayerController sourcePlayer = sourceRoot.AddComponent<PlayerController>();
            LegacyProjectileSnapshotSource projectile = projectileRoot.AddComponent<LegacyProjectileSnapshotSource>();

            try
            {
                sourcePlayer.slotId = 2;
                projectileRoot.transform.position = new Vector3(12f, 24f, 0f);
                projectile.SourceObject = sourceRoot;
                projectile.CurrentVelocity = new Vector2(3f, 4f);
                projectile.IsStuck = true;
                projectile.IsDisarmed = true;
                projectile.IsCollectible = true;
                projectile.TravelDirection = Vector2.left;

                AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotFallbackService.BuildFromProjectile(projectile);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.sourceSlotId, Is.EqualTo(2));
                Assert.That(snapshot.isStuck, Is.True);
                Assert.That(snapshot.isDisarmed, Is.True);
                Assert.That(snapshot.isCollectible, Is.True);
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(12f, 24f)));
                Assert.That(snapshot.velocity, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(snapshot.travelDirection, Is.EqualTo(Vector2.left));
            }
            finally
            {
                Object.DestroyImmediate(sourceRoot);
                Object.DestroyImmediate(projectileRoot);
            }
        }

        [Test]
        public void BuildFromProjectile_UsesFallbacksForMissingOptionalProperties()
        {
            GameObject projectileRoot = new GameObject("MinimalProjectileSnapshotSource");
            MinimalProjectileSnapshotSource projectile = projectileRoot.AddComponent<MinimalProjectileSnapshotSource>();

            try
            {
                projectileRoot.transform.position = new Vector3(-5f, 7f, 0f);

                AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotFallbackService.BuildFromProjectile(projectile);

                Assert.That(snapshot.isValid, Is.True);
                Assert.That(snapshot.sourceSlotId, Is.Zero);
                Assert.That(snapshot.isStuck, Is.False);
                Assert.That(snapshot.isDisarmed, Is.False);
                Assert.That(snapshot.isCollectible, Is.False);
                Assert.That(snapshot.position, Is.EqualTo(new Vector2(-5f, 7f)));
                Assert.That(snapshot.velocity, Is.EqualTo(Vector2.zero));
                Assert.That(snapshot.travelDirection, Is.EqualTo(Vector2.right));
            }
            finally
            {
                Object.DestroyImmediate(projectileRoot);
            }
        }

        [Test]
        public void BuildFromProjectile_ReturnsDefaultForMissingProjectile()
        {
            AiArenaProjectileSnapshot snapshot = AiArenaProjectileSnapshotFallbackService.BuildFromProjectile(null);

            Assert.That(snapshot.isValid, Is.False);
            Assert.That(snapshot.sourceSlotId, Is.Zero);
            Assert.That(snapshot.travelDirection, Is.EqualTo(Vector2.zero));
        }

        private sealed class LegacyProjectileSnapshotSource : MonoBehaviour
        {
            public GameObject SourceObject { get; set; }
            public bool IsStuck { get; set; }
            public bool IsDisarmed { get; set; }
            public bool IsCollectible { get; set; }
            public Vector2 CurrentVelocity { get; set; }
            public Vector2 TravelDirection { get; set; }
        }

        private sealed class MinimalProjectileSnapshotSource : MonoBehaviour
        {
        }

        private sealed class PlayerController : MonoBehaviour
        {
            public int slotId;
        }
    }
}
